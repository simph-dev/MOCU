using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;


namespace MirrorExperiment
{
    /// The rear-view mirror: a second camera at the eye, looking straight back,
    /// rendering into a texture that is shown on a flat screen in front of the
    /// participant. Optionally with a frame round the screen.
    ///
    /// Nothing of it lives in the scene. The screen, the camera and the texture
    /// between them are all built here from MirrorSettings, so the config file is
    /// the only place the mirror is described. The one exception is the frame
    /// image, which is an asset in a Resources folder and is picked by name.
    ///
    /// Three things about how this works, because they are easy to get wrong:
    ///
    /// 1. It is fixed to the car, not to the head. The whole of it sits at the
    ///    eye's calibrated place and is carried by the camera trajectory and by
    ///    nothing else (see SetAnchor): turning or moving the head moves neither
    ///    the screen nor the picture on it. That is how a parking screen behaves.
    ///    A real mirror would stay put as well, but its picture would shift as the
    ///    head moves; this one does not.
    ///
    /// 2. The camera's field of view is not a free parameter. It is derived from
    ///    the angle the screen subtends at the eye's calibrated place, so that
    ///    angles on the screen are true angles for an eye that is where it was
    ///    calibrated. A wider lens on the same screen would compress every
    ///    heading toward the centre - see MirrorSettings.Magnification.
    ///
    /// 3. It is a picture on a flat surface, not an optical mirror. Both eyes see
    ///    the same image at the depth of the screen; there is no stereo depth
    ///    behind the glass and no parallax through it.
    public class RearViewMirror : MonoBehaviour
    {
        /// Whatever the headset camera applies to the whole picture it also applies
        /// to the screen. Left on here as well, each of these would land twice:
        /// once into the texture and again when the screen showing it is rendered -
        /// and the stars in the mirror would not match the stars seen directly.
        private static readonly FrameSettingsField[] AppliedByHeadsetCamera =
        {
            FrameSettingsField.ExposureControl,
            FrameSettingsField.Tonemapping,
            FrameSettingsField.ColorGrading,
            FrameSettingsField.Bloom,
            FrameSettingsField.Vignette,
            FrameSettingsField.FilmGrain
        };

        private static readonly int UnlitColor = Shader.PropertyToID("_UnlitColor");
        private static readonly int UnlitColorMap = Shader.PropertyToID("_UnlitColorMap");

        private const int MinTextureSize = 16;
        private const int MaxTextureSize = 4096;

        /// How far behind the screen the frame sits, in meters. Enough to keep the
        /// two apart in the depth buffer, far too little to see.
        private const float FrameGap = 0.001f;

        private Camera _camera;
        private Transform _screen;
        private Mesh _screenMesh;
        private Material _screenMaterial;
        private RenderTexture _texture;

        private GameObject _frame;
        private Mesh _frameMesh;
        private Material _frameMaterial;


        /// Takes everything from the config. Called when an experiment starts, so
        /// the mirror can be moved, resized or switched off between runs without
        /// recompiling or touching the scene.
        ///
        /// The material is only a template - the screen and the frame get their own
        /// copies. A copy of one the scene already uses, rather than Shader.Find,
        /// because a shader that nothing references is stripped from a build.
        ///
        /// Returns false, and changes nothing, when the settings name a frame image
        /// that does not exist: the frame is part of what the participant sees, so
        /// a run should not quietly go ahead without it.
        public bool Configure(MirrorSettings settings, Camera headsetCamera, Material materialTemplate, int layer,
                              out string problem)
        {
            problem = null;

            Texture2D frameTexture = null;
            bool wantsFrame = settings.Enabled && !string.IsNullOrEmpty(settings.FrameTexture);

            if (wantsFrame)
            {
                frameTexture = Resources.Load<Texture2D>(settings.FrameTexture);

                if (frameTexture == null)
                {
                    problem = $"Mirror frame \"{settings.FrameTexture}\" not found in any Resources folder";
                    return false;
                }
            }

            gameObject.SetActive(settings.Enabled);

            if (!settings.Enabled)
                return true;

            if (_camera == null)
                Build(materialTemplate);

            float width = Mathf.Max(0.001f, settings.ScreenWidth);
            float height = Mathf.Max(0.001f, settings.ScreenHeight);

            // Turned once, here, to face the eye's place - so that a screen moved
            // off to the side still faces the driver, like a real rear-view mirror,
            // and the distance to its centre is the one number the field of view
            // depends on. It does not turn after the gaze.
            Vector3 center = settings.ScreenCenter();
            float distance = center.magnitude;

            _screen.gameObject.layer = layer;
            _screen.localPosition = center;
            _screen.localRotation = Quaternion.LookRotation(center);

            // Texture coordinate at the participant's left edge of the screen. The
            // camera's own left is u = 0, so 0 shows the picture as the camera took
            // it and 1 mirrors it. Done in the mesh rather than on the camera:
            // flipping a projection also flips triangle winding, and with it which
            // faces get culled.
            BuildQuad(_screenMesh, width, height, settings.FlipHorizontally ? 1f : 0f);

            ConfigureFrame(frameTexture, width, height, Mathf.Max(0f, settings.FrameWidth), layer);

            // The same world the headset camera would see if it turned round.
            var headsetData = headsetCamera.GetComponent<HDAdditionalCameraData>();
            var mirrorData = _camera.GetComponent<HDAdditionalCameraData>();

            _camera.cullingMask = headsetCamera.cullingMask;
            _camera.nearClipPlane = headsetCamera.nearClipPlane;
            _camera.farClipPlane = headsetCamera.farClipPlane;
            _camera.depth = headsetCamera.depth - 1f;

            if (headsetData != null)
            {
                mirrorData.volumeLayerMask = headsetData.volumeLayerMask;
                mirrorData.antialiasing = headsetData.antialiasing;
                mirrorData.SMAAQuality = headsetData.SMAAQuality;
            }

            float magnification = Mathf.Max(0.01f, settings.Magnification);
            float verticalFov = 2f * Mathf.Atan(height / 2f / (distance * magnification)) * Mathf.Rad2Deg;

            _camera.fieldOfView = Mathf.Clamp(verticalFov, 1f, 179f);
            _camera.aspect = width / height;

            // Sized by what the screen subtends at the eye, not by the camera's
            // lens: it is the screen that the headset's pixels are spent on.
            float screenDegrees = 2f * Mathf.Atan(height / 2f / distance) * Mathf.Rad2Deg;
            int textureHeight = Mathf.Clamp(
                Mathf.RoundToInt(screenDegrees * settings.TexturePixelsPerDegree), MinTextureSize, MaxTextureSize);
            int textureWidth = Mathf.Clamp(
                Mathf.RoundToInt(textureHeight * width / height), MinTextureSize, MaxTextureSize);

            ResizeTexture(textureWidth, textureHeight);

            _camera.enabled = true;

            return true;
        }

        /// Puts the mirror at the eye's place in the car: the calibrated home plus
        /// wherever the camera trajectory has carried it. Called every frame by the
        /// experiment, together with the camera rig, so the two never drift apart.
        /// The orientation stays that of the world, which after calibration is the
        /// participant's straight ahead.
        public void SetAnchor(Vector3 eyePlace)
        {
            transform.SetPositionAndRotation(eyePlace, Quaternion.identity);
        }

        private void OnDestroy()
        {
            if (_camera != null)
                _camera.targetTexture = null;

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            if (_screenMaterial != null)
                Destroy(_screenMaterial);

            if (_screenMesh != null)
                Destroy(_screenMesh);

            if (_frameMaterial != null)
                Destroy(_frameMaterial);

            if (_frameMesh != null)
                Destroy(_frameMesh);
        }

        // ...........................................

        private void Build(Material materialTemplate)
        {
            _screenMaterial = new Material(materialTemplate) { name = "RearViewMirrorScreen" };
            _screenMaterial.SetColor(UnlitColor, Color.white);
            _screenMesh = new Mesh { name = "RearViewMirrorScreen" };
            _screen = CreateQuadObject("Screen", transform, _screenMesh, _screenMaterial).transform;

            // A child of the screen, so it goes wherever the screen goes and only
            // needs pushing back a little.
            _frameMaterial = new Material(materialTemplate) { name = "RearViewMirrorFrame" };
            _frameMaterial.SetColor(UnlitColor, Color.white);
            _frameMesh = new Mesh { name = "RearViewMirrorFrame" };
            _frame = CreateQuadObject("Frame", _screen, _frameMesh, _frameMaterial);
            _frame.transform.localPosition = new Vector3(0f, 0f, FrameGap);

            var cameraObject = new GameObject("RearCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Disabled until it has a texture to render into: a camera with no
            // target is a camera that renders to the headset.
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.stereoTargetEye = StereoTargetEyeMask.None;

            var data = cameraObject.AddComponent<HDAdditionalCameraData>();
            data.xrRendering = false;
            data.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            data.backgroundColorHDR = Color.black;
            data.customRenderingSettings = true;

            foreach (var field in AppliedByHeadsetCamera)
            {
                data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
                data.renderingPathCustomFrameSettings.SetEnabled(field, false);
            }
        }

        /// The frame is a second, larger quad just behind the screen, so the screen
        /// covers its middle and only a border of it shows. That keeps it opaque -
        /// no transparent variant of the shader is needed - and the image can be
        /// anything: its middle is never seen, and black is invisible against the
        /// black surround, so an outline of any shape is just the image with black
        /// outside it.
        private void ConfigureFrame(Texture2D frameTexture, float screenWidth, float screenHeight, float frameWidth,
                                    int layer)
        {
            bool show = frameTexture != null && frameWidth > 0f;
            _frame.SetActive(show);

            if (!show)
                return;

            _frame.layer = layer;
            _frameMaterial.SetTexture(UnlitColorMap, frameTexture);
            BuildQuad(_frameMesh, screenWidth + 2f * frameWidth, screenHeight + 2f * frameWidth, 0f);
        }

        private static GameObject CreateQuadObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var quad = new GameObject(name);
            quad.transform.SetParent(parent, false);
            quad.AddComponent<MeshFilter>().sharedMesh = mesh;

            var quadRenderer = quad.AddComponent<MeshRenderer>();
            quadRenderer.sharedMaterial = material;
            quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;

            return quad;
        }

        /// A quad in the local XY plane, seen from its -Z side, with the texture
        /// running left to right from uLeft to 1 - uLeft.
        private static void BuildQuad(Mesh mesh, float width, float height, float uLeft)
        {
            float x = width / 2f;
            float y = height / 2f;
            float uRight = 1f - uLeft;

            mesh.Clear();
            mesh.vertices = new[]
            {
                new Vector3(-x, -y, 0f),
                new Vector3(-x,  y, 0f),
                new Vector3( x,  y, 0f),
                new Vector3( x, -y, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(uLeft,  0f),
                new Vector2(uLeft,  1f),
                new Vector2(uRight, 1f),
                new Vector2(uRight, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
            mesh.RecalculateBounds();
        }

        private void ResizeTexture(int width, int height)
        {
            if (_texture != null && _texture.width == width && _texture.height == height)
                return;

            _camera.targetTexture = null;

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            // Clamped, or the flipped coordinates would bleed the opposite edge in
            // along the border.
            _texture = new RenderTexture(width, height, 24)
            {
                name = "RearViewMirror",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            _camera.targetTexture = _texture;
            _screenMaterial.SetTexture(UnlitColorMap, _texture);
        }
    }
}
