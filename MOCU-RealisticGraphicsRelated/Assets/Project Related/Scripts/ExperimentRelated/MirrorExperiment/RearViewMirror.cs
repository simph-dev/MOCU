using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.XR;


namespace MirrorExperiment
{
    /// The rear-view mirror: a flat screen in front of the participant showing the
    /// scene behind them, optionally with a frame round it. Two ways of filling the
    /// screen, switched per trial (SetView, InsideView):
    ///
    ///     Mirror   a camera for each eye, at that eye reflected in the glass, looking
    ///              back through the glass. Each eye gets its own picture: depth
    ///              behind the glass, parallax as the head moves - a real mirror.
    ///     Screen   one camera at the eye's place looking straight back, the same
    ///              picture for both eyes, whatever the head does - a parking screen.
    ///
    /// Nothing of it lives in the scene. The screen, the cameras and the textures
    /// between them are all built here from MirrorSettings, so the config file is
    /// the only place the mirror is described. The exceptions are assets picked by
    /// name from Resources: the frame image and the Mirror view's shader.
    ///
    /// Four things about how this works, because they are easy to get wrong:
    ///
    /// 1. The screen is fixed to the car, not to the head. The whole of it sits at
    ///    the eye's calibrated place and is carried by the camera trajectory and by
    ///    nothing else (see SetAnchor). What the head does changes what the Mirror
    ///    view shows, as with a real mirror, but never where the screen is.
    ///
    /// 2. The Screen view's field of view is not a free parameter. It is derived
    ///    from the angle the screen subtends at the eye's calibrated place, so that
    ///    angles on the screen are true angles. A wider lens on the same screen
    ///    would compress every heading toward the centre - see Magnification. The
    ///    Mirror view has no field of view at all: each eye camera's frustum is cut
    ///    to the glass exactly, and its near plane is the glass itself, so nothing
    ///    on the far side of the glass can show in it.
    ///
    /// 3. None of these cameras sees the cabin layer - the screen, its frame, the
    ///    fixation point. In the Mirror view they look straight at the screen, and
    ///    would otherwise see the screen they render into, and the fixation point a
    ///    second time, reflected.
    ///
    /// 4. The Screen view is a picture on a flat surface. Both eyes see the same
    ///    image at the depth of the screen: no stereo depth behind the glass and no
    ///    parallax through it. That is what the Mirror view is for.
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
        private static readonly int LeftEyeTexture = Shader.PropertyToID("_LeftEyeTexture");
        private static readonly int RightEyeTexture = Shader.PropertyToID("_RightEyeTexture");

        /// The Mirror view's shader: shows each eye the texture rendered for it.
        private const string StereoShaderResource = "StereoMirrorScreen";

        private const int MinTextureSize = 16;
        private const int MaxTextureSize = 4096;

        /// How far behind the screen the frame sits, in meters. Enough to keep the
        /// two apart in the depth buffer, far too little to see.
        private const float FrameGap = 0.001f;

        private MirrorSettings _settings;
        private Transform _head;
        private InsideView _view = InsideView.Mirror;
        private float _width;
        private float _height;
        private float _farClip;
        private bool _warnedAboutEyes;

        private Transform _screen;
        private Mesh _screenMesh;
        private MeshRenderer _screenRenderer;

        /// HDRP Unlit with one texture, for the Screen view.
        private Material _screenMaterial;

        /// StereoMirrorScreen with a texture per eye, for the Mirror view.
        private Material _stereoMaterial;

        private GameObject _frame;
        private Mesh _frameMesh;
        private Material _frameMaterial;

        private Camera _screenCamera;
        private Camera _leftEyeCamera;
        private Camera _rightEyeCamera;
        private RenderTexture _screenTexture;
        private RenderTexture _leftEyeTexture;
        private RenderTexture _rightEyeTexture;

        /// All the cameras behind the screen, whichever view is on. The experiment
        /// switches the star layer on and off in their culling masks, to show or
        /// hide the field inside the mirror.
        public Camera[] Cameras => _screenCamera == null
            ? new Camera[0]
            : new[] { _screenCamera, _leftEyeCamera, _rightEyeCamera };


        /// Takes everything from the config. Called when an experiment starts, so
        /// the mirror can be moved, resized or switched off between runs without
        /// recompiling or touching the scene.
        ///
        /// The material is only a template - the screen and the frame get their own
        /// copies. A copy of one the scene already uses, rather than Shader.Find,
        /// because a shader that nothing references is stripped from a build.
        ///
        /// The screen and the frame go on the cabin layer, which none of the mirror's
        /// cameras renders and the headset camera does.
        ///
        /// Returns false, and changes nothing, when an asset it names is missing:
        /// the mirror is part of what the participant sees, so a run should not
        /// quietly go ahead without it.
        public bool Configure(MirrorSettings settings, Camera headsetCamera, Material materialTemplate, int cabinLayer,
                              out string problem)
        {
            problem = null;

            Texture2D frameTexture = null;
            Shader stereoShader = null;

            if (settings.Enabled)
            {
                stereoShader = Resources.Load<Shader>(StereoShaderResource);

                if (stereoShader == null)
                {
                    problem = $"Mirror shader \"{StereoShaderResource}\" not found in any Resources folder";
                    return false;
                }

                if (settings.ShowFrame)
                {
                    frameTexture = string.IsNullOrEmpty(settings.FrameTexture)
                        ? null
                        : Resources.Load<Texture2D>(settings.FrameTexture);

                    if (frameTexture == null)
                    {
                        problem = $"Mirror frame \"{settings.FrameTexture}\" not found in any Resources folder";
                        return false;
                    }
                }
            }

            gameObject.SetActive(settings.Enabled);

            if (!settings.Enabled)
                return true;

            if (_screenCamera == null)
                Build(materialTemplate, stereoShader);

            _settings = settings;
            _head = headsetCamera.transform;
            _width = Mathf.Max(0.001f, settings.ScreenWidth);
            _height = Mathf.Max(0.001f, settings.ScreenHeight);

            // Turned once, here, to face the eye's place - so that a screen moved
            // off to the side still faces the driver, like a real rear-view mirror,
            // and the distance to its centre is the one number the Screen view's
            // field of view depends on. It does not turn after the gaze.
            Vector3 center = settings.ScreenCenter();
            float distance = center.magnitude;

            _screen.gameObject.layer = cabinLayer;
            _screen.localPosition = center;
            _screen.localRotation = Quaternion.LookRotation(center);

            ConfigureFrame(frameTexture, _width, _height, Mathf.Max(0f, settings.FrameWidth), cabinLayer);

            // What the headset camera sees, minus the cabin.
            var headsetData = headsetCamera.GetComponent<HDAdditionalCameraData>();
            _farClip = headsetCamera.farClipPlane;

            foreach (var camera in Cameras)
            {
                camera.cullingMask = headsetCamera.cullingMask & ~(1 << cabinLayer);
                camera.nearClipPlane = headsetCamera.nearClipPlane;
                camera.farClipPlane = headsetCamera.farClipPlane;
                camera.depth = headsetCamera.depth - 1f;
                camera.aspect = _width / _height;

                if (headsetData != null)
                {
                    var data = camera.GetComponent<HDAdditionalCameraData>();
                    data.volumeLayerMask = headsetData.volumeLayerMask;
                    data.antialiasing = headsetData.antialiasing;
                    data.SMAAQuality = headsetData.SMAAQuality;
                }
            }

            // The Screen view's camera: at the eye's place looking straight back,
            // unless the config moves or tilts it. Positive pitch looks down.
            _screenCamera.transform.localPosition = new Vector3(
                settings.ScreenCameraOffsetRight, settings.ScreenCameraOffsetUp, -settings.ScreenCameraOffsetBack);
            _screenCamera.transform.localRotation = Quaternion.Euler(settings.ScreenCameraPitch, 180f, 0f);

            float magnification = Mathf.Max(0.01f, settings.Magnification);
            float verticalFov = 2f * Mathf.Atan(_height / 2f / (distance * magnification)) * Mathf.Rad2Deg;
            _screenCamera.fieldOfView = Mathf.Clamp(verticalFov, 1f, 179f);

            // Sized by what the screen subtends at the eye, not by any camera's
            // lens: it is the screen that the headset's pixels are spent on.
            float screenDegrees = 2f * Mathf.Atan(_height / 2f / distance) * Mathf.Rad2Deg;
            int textureHeight = Mathf.Clamp(
                Mathf.RoundToInt(screenDegrees * settings.TexturePixelsPerDegree), MinTextureSize, MaxTextureSize);
            int textureWidth = Mathf.Clamp(
                Mathf.RoundToInt(textureHeight * _width / _height), MinTextureSize, MaxTextureSize);

            ResizeTexture(ref _screenTexture, _screenCamera, textureWidth, textureHeight, "RearViewMirrorScreen");
            ResizeTexture(ref _leftEyeTexture, _leftEyeCamera, textureWidth, textureHeight, "RearViewMirrorLeftEye");
            ResizeTexture(ref _rightEyeTexture, _rightEyeCamera, textureWidth, textureHeight, "RearViewMirrorRightEye");

            _screenMaterial.SetTexture(UnlitColorMap, _screenTexture);
            _stereoMaterial.SetTexture(LeftEyeTexture, _leftEyeTexture);
            _stereoMaterial.SetTexture(RightEyeTexture, _rightEyeTexture);

            SetView(_view);

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

        /// Which of the two views the screen shows. Only that view's cameras render,
        /// so the other costs nothing. Called before every trial, since conditions
        /// of both kinds can be interleaved.
        public void SetView(InsideView view)
        {
            _view = view;

            if (_screenCamera == null)
                return;

            bool mirror = view == InsideView.Mirror;

            _screenCamera.enabled = !mirror;
            _leftEyeCamera.enabled = mirror;
            _rightEyeCamera.enabled = mirror;

            _screenRenderer.sharedMaterial = mirror ? _stereoMaterial : _screenMaterial;

            // Texture coordinate at the participant's left edge of the screen. The
            // cameras look back, so their own left is the participant's right: 1
            // here reverses the picture, as glass does. The Mirror view always
            // does; the Screen view as configured. Done in the mesh rather than on
            // the cameras: flipping a projection also flips triangle winding, and
            // with it which faces get culled.
            bool reversed = mirror || _settings.FlipHorizontally;
            BuildQuad(_screenMesh, _width, _height, reversed ? 1f : 0f);

            if (mirror)
                AimEyeCameras();
        }

        private void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        /// Just before rendering, after the headset's pose for this frame has come
        /// in, so that the reflected eyes follow the real ones without lagging.
        private void OnBeforeRender()
        {
            if (_view == InsideView.Mirror && _leftEyeCamera != null && _leftEyeCamera.enabled)
                AimEyeCameras();
        }

        private void OnDestroy()
        {
            foreach (var camera in Cameras)
                camera.targetTexture = null;

            ReleaseTexture(_screenTexture);
            ReleaseTexture(_leftEyeTexture);
            ReleaseTexture(_rightEyeTexture);

            if (_screenMaterial != null)
                Destroy(_screenMaterial);

            if (_stereoMaterial != null)
                Destroy(_stereoMaterial);

            if (_screenMesh != null)
                Destroy(_screenMesh);

            if (_frameMaterial != null)
                Destroy(_frameMaterial);

            if (_frameMesh != null)
                Destroy(_frameMesh);
        }

        // ---- the Mirror view .........................................

        private void AimEyeCameras()
        {
            GetEyes(out Vector3 left, out Vector3 right);
            AimEyeCamera(_leftEyeCamera, left);
            AimEyeCamera(_rightEyeCamera, right);
        }

        /// Puts an eye's camera at that eye reflected in the glass, looking back
        /// through the glass, and cuts its frustum to the glass exactly: an off-axis
        /// projection whose near plane is the glass itself. A ray from the reflected
        /// eye through a point of the glass is the reflection of the ray from the
        /// real eye to that point, so every point of the picture lands on the glass
        /// exactly where the real reflection would.
        ///
        /// The camera is moved, not the world reflected, so no triangle winding
        /// flips; the left-right reversal of a mirror is in the screen's texture
        /// coordinates instead (SetView).
        private void AimEyeCamera(Camera camera, Vector3 eye)
        {
            Vector3 normal = _screen.forward;     // from the participant's side through the glass
            Vector3 glassCenter = _screen.position;

            float eyeToGlass = Vector3.Dot(glassCenter - eye, normal);

            // An eye level with the glass or beyond it has nothing to see in it.
            if (eyeToGlass < 0.001f)
                return;

            Vector3 reflected = eye + 2f * eyeToGlass * normal;
            Quaternion rotation = Quaternion.LookRotation(-normal, _screen.up);

            camera.transform.SetPositionAndRotation(reflected, rotation);

            // Two opposite corners of the glass, in the camera's own axes. The camera
            // faces the glass square on, so they span the frustum at the near plane.
            Quaternion toCamera = Quaternion.Inverse(rotation);
            Vector3 a = toCamera * (_screen.TransformPoint(new Vector3(-_width / 2f, -_height / 2f, 0f)) - reflected);
            Vector3 b = toCamera * (_screen.TransformPoint(new Vector3(_width / 2f, _height / 2f, 0f)) - reflected);

            camera.projectionMatrix = Matrix4x4.Frustum(
                Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x),
                Mathf.Min(a.y, b.y), Mathf.Max(a.y, b.y),
                eyeToGlass, Mathf.Max(_farClip, eyeToGlass + 1f));
        }

        /// Where the two eyes are, in world space. Live from the headset when the
        /// mirror follows the head; otherwise at the eye's calibrated place - this
        /// object's origin - with the head facing straight ahead.
        private void GetEyes(out Vector3 left, out Vector3 right)
        {
            GetEyeOffsets(out Vector3 leftOffset, out Vector3 rightOffset);

            if (_settings.MirrorFollowsHead)
            {
                left = _head.TransformPoint(leftOffset);
                right = _head.TransformPoint(rightOffset);
            }
            else
            {
                left = transform.position + leftOffset;
                right = transform.position + rightOffset;
            }
        }

        /// Each eye relative to the centre eye, in head space, as the headset
        /// reports them - so the participant's real eye distance. Without a headset,
        /// or one that does not report its eyes, MirrorSettings.EyeSeparation split
        /// evenly instead.
        private void GetEyeOffsets(out Vector3 left, out Vector3 right)
        {
            var headset = InputDevices.GetDeviceAtXRNode(XRNode.Head);

            if (headset.isValid
                && headset.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 centerEye)
                && headset.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion centerRotation)
                && headset.TryGetFeatureValue(CommonUsages.leftEyePosition, out Vector3 leftEye)
                && headset.TryGetFeatureValue(CommonUsages.rightEyePosition, out Vector3 rightEye))
            {
                Quaternion toHead = Quaternion.Inverse(centerRotation);
                left = toHead * (leftEye - centerEye);
                right = toHead * (rightEye - centerEye);
                return;
            }

            if (!_warnedAboutEyes)
            {
                Debug.LogWarning($"MirrorExperiment: the headset does not report its eyes - the mirror uses an eye separation of {_settings.EyeSeparation} m from the config");
                _warnedAboutEyes = true;
            }

            float half = Mathf.Max(0f, _settings.EyeSeparation) / 2f;
            left = Vector3.left * half;
            right = Vector3.right * half;
        }

        // ---- building ................................................

        private void Build(Material materialTemplate, Shader stereoShader)
        {
            _screenMaterial = new Material(materialTemplate) { name = "RearViewMirrorScreen" };
            _screenMaterial.SetColor(UnlitColor, Color.white);
            _stereoMaterial = new Material(stereoShader) { name = "RearViewMirrorStereo" };

            _screenMesh = new Mesh { name = "RearViewMirrorScreen" };
            var screenObject = CreateQuadObject("Screen", transform, _screenMesh, _screenMaterial);
            _screen = screenObject.transform;
            _screenRenderer = screenObject.GetComponent<MeshRenderer>();

            // A child of the screen, so it goes wherever the screen goes and only
            // needs pushing back a little.
            _frameMaterial = new Material(materialTemplate) { name = "RearViewMirrorFrame" };
            _frameMaterial.SetColor(UnlitColor, Color.white);
            _frameMesh = new Mesh { name = "RearViewMirrorFrame" };
            _frame = CreateQuadObject("Frame", _screen, _frameMesh, _frameMaterial);
            _frame.transform.localPosition = new Vector3(0f, 0f, FrameGap);

            _screenCamera = CreateRearCamera("ScreenCamera");
            _leftEyeCamera = CreateRearCamera("LeftEyeMirrorCamera");
            _rightEyeCamera = CreateRearCamera("RightEyeMirrorCamera");
        }

        /// Disabled until it has a texture to render into and its view is chosen:
        /// a camera with no target is a camera that renders to the headset.
        private Camera CreateRearCamera(string name)
        {
            var cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(transform, false);

            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.stereoTargetEye = StereoTargetEyeMask.None;

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

            return camera;
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

        /// Clamped, or the reversed coordinates would bleed the opposite edge in
        /// along the border.
        private static void ResizeTexture(ref RenderTexture texture, Camera camera, int width, int height, string name)
        {
            if (texture != null && texture.width == width && texture.height == height)
                return;

            camera.targetTexture = null;
            ReleaseTexture(texture);

            texture = new RenderTexture(width, height, 24)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            camera.targetTexture = texture;
        }

        private static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Destroy(texture);
        }
    }
}
