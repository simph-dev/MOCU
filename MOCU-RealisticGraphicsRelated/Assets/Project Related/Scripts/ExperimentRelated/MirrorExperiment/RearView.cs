using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.XR;


namespace MirrorExperiment
{
    /// The rear view: a flat screen in front of the participant showing the scene
    /// behind them, mirror-reversed, drawn as any image - a car's mirror, a parking
    /// display - whose green part is the glass (RearViewSettings.ScreenImage). Three
    /// ways of filling the glass, switched per trial (SetView, InsideView):
    ///
    ///     Screen   one camera at the eye's place looking straight back, the same
    ///              picture for both eyes, whatever the head does - a car's parking
    ///              display. The default.
    ///     Mirror   a camera for each eye, at that eye reflected in the glass, looking
    ///              back through the glass. Each eye gets its own picture: depth
    ///              behind the glass, parallax as the head moves - a real mirror.
    ///     Forward  the Screen view's camera turned round to look forward, at the
    ///              screen's centre, and not reversed: a flat window onto the
    ///              scene ahead. Not a rear view at all, but the same screen.
    ///
    /// Nothing of it lives in the scene. The screen, the cameras and the textures
    /// between them are all built here from RearViewSettings, so the config file is
    /// the only place the screen is described. The exceptions are assets picked by
    /// name from Resources: the screen's image and its shader.
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
    /// 3. None of these cameras sees the cabin layer - the screen with its frame,
    ///    the fixation point. In the Mirror view they look straight at the screen, and
    ///    would otherwise see the screen they render into, and the fixation point a
    ///    second time, reflected.
    ///
    /// 4. The Screen view is a picture on a flat surface. Both eyes see the same
    ///    image at the depth of the screen: no stereo depth behind the glass and no
    ///    parallax through it. That is what the Mirror view is for.
    public class RearView : MonoBehaviour
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

        private static readonly int LeftEyeTexture = Shader.PropertyToID("_LeftEyeTexture");
        private static readonly int RightEyeTexture = Shader.PropertyToID("_RightEyeTexture");
        private static readonly int ScreenImage = Shader.PropertyToID("_ScreenImage");

        /// The screen's shader, both views: draws the screen's image, and on its
        /// green part shows each eye the texture rendered for it - the same one for
        /// both, in the Screen view.
        private const string ScreenShaderResource = "RearViewScreen";

        private const int MinTextureSize = 16;
        private const int MaxTextureSize = 4096;

        private RearViewSettings _settings;
        private Transform _head;
        private InsideView _view = InsideView.Screen;
        private float _width;
        private float _height;
        private Vector3 _center;
        private float _farClip;
        private bool _warnedAboutEyes;

        private Transform _screen;
        private Mesh _screenMesh;
        private MeshRenderer _screenRenderer;

        /// RearViewScreen with the one picture for both eyes, for the Screen view.
        private Material _screenMaterial;

        /// RearViewScreen with a picture per eye, for the Mirror view.
        private Material _stereoMaterial;

        private Camera _screenCamera;
        private Camera _leftEyeCamera;
        private Camera _rightEyeCamera;
        private RenderTexture _screenTexture;
        private RenderTexture _leftEyeTexture;
        private RenderTexture _rightEyeTexture;

        /// All the cameras behind the screen, whichever view is on. The experiment
        /// switches the mirror's star layer on and off in their culling masks, to
        /// show or hide the field inside the mirror.
        public Camera[] Cameras => _screenCamera == null
            ? new Camera[0]
            : new[] { _screenCamera, _leftEyeCamera, _rightEyeCamera };

        /// What share of the stars the current view shows, and how many times
        /// larger (see RearViewSettings.MirrorViewStarFraction). Nothing before the
        /// mirror has been configured.
        public float StarFraction => _settings == null ? 0f
            : _view == InsideView.Mirror ? _settings.MirrorViewStarFraction : 1f;

        public float StarScale => _settings == null || _view != InsideView.Mirror ? 1f
            : _settings.MirrorViewStarScale;

        /// How far the furthest corner of the screen, frame and all, is from the
        /// eye's place, now that the image has said how high it is. 0 before the
        /// screen has been configured.
        public float FurthestCornerDistance { get; private set; }


        /// Takes everything from the config. Called when an experiment starts, so
        /// the mirror can be moved, resized or switched off between runs without
        /// recompiling or touching the scene.
        ///
        /// The screen, frame and all, goes on the cabin layer, which none of the
        /// screen's cameras renders and the headset camera does.
        ///
        /// Returns false, and changes nothing, when an asset it names is missing or
        /// a setting is out of range: the screen is part of what the participant
        /// sees, so a run should not quietly go ahead without it.
        public bool Configure(RearViewSettings settings, Camera headsetCamera, int cabinLayer, out string problem)
        {
            problem = null;

            Texture2D image = null;
            Shader screenShader = null;

            if (settings.Enabled)
            {
                if (settings.MirrorViewStarFraction <= 0f || settings.MirrorViewStarFraction > 1f)
                {
                    problem = "RearView.MirrorViewStarFraction must be above 0 and at most 1";
                    return false;
                }

                if (settings.MirrorViewStarScale <= 0f)
                {
                    problem = "RearView.MirrorViewStarScale must be above 0";
                    return false;
                }

                screenShader = Resources.Load<Shader>(ScreenShaderResource);

                if (screenShader == null)
                {
                    problem = $"Screen shader \"{ScreenShaderResource}\" not found in any Resources folder";
                    return false;
                }

                if (string.IsNullOrEmpty(settings.ScreenImage))
                {
                    problem = "RearView.ScreenImage is empty - name an image in a Resources folder, " +
                              "e.g. ScreenRearMirror, or ScreenPlainGlass for a plain rectangle";
                    return false;
                }

                image = Resources.Load<Texture2D>(settings.ScreenImage);

                if (image == null)
                {
                    problem = $"Screen image \"{settings.ScreenImage}\" not found in any Resources folder";
                    return false;
                }
            }

            gameObject.SetActive(settings.Enabled);

            if (!settings.Enabled)
                return true;

            if (_screenCamera == null)
                Build(screenShader);

            _settings = settings;
            _head = headsetCamera.transform;

            // The width is the config's, the height the image's, so that it is
            // never stretched - unless the config gives a height as well.
            _width = Mathf.Max(0.001f, settings.ScreenWidth);
            _height = settings.ScreenHeight > 0f ? settings.ScreenHeight : _width * HeightPerWidth(image);

            // Turned once, here, to face the eye's place - so that a screen moved
            // off to the side still faces the driver, like a real rear-view mirror,
            // and the distance to its centre is the one number the Screen view's
            // field of view depends on. It does not turn after the gaze.
            Vector3 center = settings.ScreenCenter();
            float distance = center.magnitude;
            _center = center;

            _screen.gameObject.layer = cabinLayer;
            _screen.localPosition = center;
            _screen.localRotation = Quaternion.LookRotation(center);

            FurthestCornerDistance = Mathf.Sqrt(center.sqrMagnitude + _width * _width / 4f + _height * _height / 4f);

            // What the headset camera sees, minus the cabin. Which of the two star
            // layers these cameras render is the experiment's to set, per trial.
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

            // The Screen and Forward views' camera: at the eye's place, unless the
            // config moves it. Which way it looks is the view's (SetView).
            _screenCamera.transform.localPosition = new Vector3(
                settings.ScreenCameraOffsetRight, settings.ScreenCameraOffsetUp, -settings.ScreenCameraOffsetBack);

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

            ResizeTexture(ref _screenTexture, _screenCamera, textureWidth, textureHeight, "RearViewScreen");
            ResizeTexture(ref _leftEyeTexture, _leftEyeCamera, textureWidth, textureHeight, "RearViewLeftEye");
            ResizeTexture(ref _rightEyeTexture, _rightEyeCamera, textureWidth, textureHeight, "RearViewRightEye");

            _screenMaterial.SetTexture(LeftEyeTexture, _screenTexture);
            _screenMaterial.SetTexture(RightEyeTexture, _screenTexture);
            _stereoMaterial.SetTexture(LeftEyeTexture, _leftEyeTexture);
            _stereoMaterial.SetTexture(RightEyeTexture, _rightEyeTexture);

            // The whole image is the screen, frame and all, and the picture behind
            // the car spans all of it, so angles stay true right up to the frame:
            // the frame only covers part of it, as a real one does.
            _screenMaterial.SetTexture(ScreenImage, image);
            _stereoMaterial.SetTexture(ScreenImage, image);

            Debug.Log($"MirrorExperiment: screen \"{settings.ScreenImage}\", {_width:F3} x {_height:F3} m at " +
                      $"{distance:F2} m - {Mathf.Atan(_width / 2f / distance) * Mathf.Rad2Deg:F1} deg either side " +
                      $"and {Mathf.Atan(_height / 2f / distance) * Mathf.Rad2Deg:F1} deg up and down, frame and all");

            // The quad is built, and the camera turned, by the view.
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

        /// Which of the views the screen shows. Only that view's cameras render, so
        /// the others cost nothing. Called before every trial, since conditions of
        /// every kind can be interleaved.
        public void SetView(InsideView view)
        {
            _view = view;

            if (_screenCamera == null || _settings == null)
                return;

            bool mirror = view == InsideView.Mirror;
            bool forward = view == InsideView.Forward;

            _screenCamera.enabled = !mirror;
            _leftEyeCamera.enabled = mirror;
            _rightEyeCamera.enabled = mirror;

            _screenRenderer.sharedMaterial = mirror ? _stereoMaterial : _screenMaterial;

            // Positive pitch looks down, either way round. Looking back, the camera
            // looks straight back; looking forward, it looks at the screen's centre,
            // which the screen faces square on - so its symmetric frustum is cut to
            // the screen exactly, and the screen is a true window.
            _screenCamera.transform.localRotation = forward
                ? Quaternion.LookRotation(_center) * Quaternion.Euler(_settings.ScreenCameraPitch, 0f, 0f)
                : Quaternion.Euler(_settings.ScreenCameraPitch, 180f, 0f);

            // Texture coordinate at the participant's left edge of the screen. The
            // rear cameras look back, so their own left is the participant's right:
            // 1 here reverses the picture, as glass does - and as a car's parking
            // display does too. Both rear views, always: so what is behind the car
            // to its left shows on the left, and stars drift across the screen the
            // same way as they do outside it. Looking forward there is nothing to
            // reverse. Done in the mesh rather than on the cameras: flipping a
            // projection also flips triangle winding, and with it which faces get
            // culled.
            BuildQuad(_screenMesh, _width, _height, forward ? 0f : 1f);

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
        }

        /// How high the image is for its width, as drawn. In the Editor that is
        /// read from the file itself: on import Unity rounds a texture's size to a
        /// power of two unless told not to, which would quietly change the screen's
        /// proportions - 1200 x 400 coming out 1024 x 512. Stretched back over the
        /// quad's true proportions, a rounded texture still looks right. In a
        /// build only the texture's size is known, so there the image has to be
        /// imported at its own size (Non-Power of 2: None), as the ones here are.
        private static float HeightPerWidth(Texture2D image)
        {
#if UNITY_EDITOR
            if (UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(image))
                    is UnityEditor.TextureImporter importer)
            {
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);

                if (width > 0 && height > 0)
                    return (float)height / width;
            }
#endif
            return (float)image.height / image.width;
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
        /// coordinates instead (Configure).
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
        /// or one that does not report its eyes, RearViewSettings.EyeSeparation split
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

        private void Build(Shader screenShader)
        {
            _screenMaterial = new Material(screenShader) { name = "RearViewScreen" };
            _stereoMaterial = new Material(screenShader) { name = "RearViewStereo" };

            _screenMesh = new Mesh { name = "RearViewScreen" };
            var screenObject = CreateQuadObject("Screen", transform, _screenMesh, _screenMaterial);
            _screen = screenObject.transform;
            _screenRenderer = screenObject.GetComponent<MeshRenderer>();

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

            // HDRP's way of keeping a camera out of the headset; Camera.stereoTargetEye
            // is the built-in renderer's, and only earns a warning here.
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
        /// running left to right from uLeft to 1 - uLeft. The second set of
        /// coordinates always runs 0 to 1 left to right as the participant sees
        /// it, whatever the first does: the screen's image is drawn that way round,
        /// and is not reversed with the picture.
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
            mesh.uv2 = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
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
