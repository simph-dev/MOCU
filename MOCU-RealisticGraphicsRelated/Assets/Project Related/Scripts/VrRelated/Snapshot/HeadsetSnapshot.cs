using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.XR;


namespace SnapshotModule
{
    /// Pictures of what the participant is shown, for figures: from between the
    /// eyes, and from each eye as the headset renders it - all with the head held
    /// exactly straight ahead and level, wherever the real head happens to be.
    ///
    /// Nothing is copied off the headset and its tracking is left alone, so the
    /// participant sees nothing happen. Each picture is rendered by a camera of its
    /// own, kept out of the headset and otherwise set up as the headset camera is:
    /// the same layers, volumes and post-processing. The eye cameras take each eye's
    /// place relative to the head, and its projection - off-centre, as headset
    /// lenses are - from the XR display itself, the same figures the render
    /// pipeline uses for the headset.
    ///
    /// Not tied to any experiment. Put it on an object, call SetAnchor every frame
    /// with where the head is to be held, and Take when pictures are wanted.
    public class HeadsetSnapshot : MonoBehaviour
    {
        private class Shot
        {
            public Camera Camera;
            public RenderTexture Texture;
            public string Suffix;
        }

        /// One eye: where it sits relative to the head, its projection, and the
        /// headset's resolution for it.
        private struct EyeView
        {
            public Matrix4x4 InHead;
            public Matrix4x4 Projection;
            public int Width;
            public int Height;
        }

        /// Unity's view matrices look down -Z; transforms look down +Z.
        private static readonly Matrix4x4 FlipZ = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));

        private const int MaxTextureSize = 16384;

        public bool IsBusy { get; private set; }


        /// Where the head is held for the pictures: the place between the eyes, and
        /// which way is straight ahead and up. To be called every frame together
        /// with whatever else is fixed there, so the pictures move with the scene.
        public void SetAnchor(Vector3 eyePlace, Quaternion orientation)
        {
            transform.SetPositionAndRotation(eyePlace, orientation);
        }

        /// Takes the pictures the settings ask for `delaySeconds` from now and saves
        /// them to `folder` as `<name>_center.png`, `<name>_left.png` and
        /// `<name>_right.png`, telling `saved` each path. The cameras are made now
        /// and read only then, so whatever adapts over frames - automatic exposure,
        /// temporal anti-aliasing - has settled. They copy the headset camera's
        /// layers now, so whatever it is shown now is what they show.
        public void Take(SnapshotSettings settings, Camera headsetCamera, string folder, string name,
                         float delaySeconds, Action<string> saved = null)
        {
            if (IsBusy)
            {
                Debug.LogWarning($"HeadsetSnapshot: still taking the last pictures - {name} skipped");
                return;
            }

            StartCoroutine(TakeAfter(settings, headsetCamera, folder, name, delaySeconds, saved));
        }

        private IEnumerator TakeAfter(SnapshotSettings settings, Camera headsetCamera, string folder, string name,
                                      float delaySeconds, Action<string> saved)
        {
            IsBusy = true;
            var shots = new List<Shot>();

            try
            {
                bool wantEyes = settings.LeftEye || settings.RightEye;
                bool wantHeadsetField = settings.Center && settings.HeadsetFieldOfView;

                var eyes = wantEyes || wantHeadsetField
                    ? ReadEyes(headsetCamera, Mathf.Max(0.1f, settings.EyeResolutionScale))
                    : new List<EyeView>();

                if ((wantEyes || wantHeadsetField) && eyes.Count < 2)
                    Debug.LogWarning("HeadsetSnapshot: no XR display reporting two eyes - no eye pictures, and the centre one takes VerticalFieldOfView");

                if (settings.Center)
                    shots.Add(wantHeadsetField && eyes.Count >= 2
                        ? MakeHeadsetFieldShot(headsetCamera, eyes, settings.Height)
                        : MakeCenterShot(headsetCamera, settings));

                if (eyes.Count >= 2)
                {
                    if (settings.LeftEye)
                        shots.Add(MakeEyeShot("left", headsetCamera, eyes[0]));

                    if (settings.RightEye)
                        shots.Add(MakeEyeShot("right", headsetCamera, eyes[1]));
                }

                yield return new WaitForSecondsRealtime(delaySeconds);
                yield return new WaitForEndOfFrame();

                Directory.CreateDirectory(folder);

                foreach (var shot in shots)
                {
                    string path = Path.Combine(folder, $"{name}_{shot.Suffix}.png");
                    File.WriteAllBytes(path, ReadBack(shot.Texture));

                    Debug.Log($"HeadsetSnapshot: saved {path}");
                    saved?.Invoke(path);
                }
            }
            finally
            {
                foreach (var shot in shots)
                {
                    shot.Camera.targetTexture = null;
                    Destroy(shot.Camera.gameObject);
                    shot.Texture.Release();
                    Destroy(shot.Texture);
                }

                IsBusy = false;
            }
        }

        private Shot MakeCenterShot(Camera headsetCamera, SnapshotSettings settings)
        {
            int width = Mathf.Clamp(settings.Width, 16, MaxTextureSize);
            int height = Mathf.Clamp(settings.Height, 16, MaxTextureSize);

            var shot = MakeShot("center", headsetCamera, Matrix4x4.identity, width, height);
            shot.Camera.fieldOfView = Mathf.Clamp(settings.VerticalFieldOfView, 1f, 179f);
            shot.Camera.aspect = (float)width / height;

            return shot;
        }

        /// From between the eyes, through the field the two eyes cover together:
        /// from the leftmost of their left edges to the rightmost of their right
        /// edges, and likewise up and down. The eyes are taken to look straight
        /// ahead, as they do in headsets whose displays are not canted, and their
        /// few centimetres to either side are left out. Like the eyes' own, the
        /// field need not be centred on straight ahead.
        private Shot MakeHeadsetFieldShot(Camera headsetCamera, List<EyeView> eyes, int height)
        {
            float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;

            foreach (var eye in eyes)
            {
                FrustumPlanes planes = eye.Projection.decomposeProjection;

                left = Mathf.Min(left, planes.left / planes.zNear);
                right = Mathf.Max(right, planes.right / planes.zNear);
                bottom = Mathf.Min(bottom, planes.bottom / planes.zNear);
                top = Mathf.Max(top, planes.top / planes.zNear);
            }

            height = Mathf.Clamp(height, 16, MaxTextureSize);
            int width = Mathf.Clamp(Mathf.RoundToInt(height * (right - left) / (top - bottom)), 16, MaxTextureSize);

            var shot = MakeShot("center", headsetCamera, Matrix4x4.identity, width, height);
            float near = headsetCamera.nearClipPlane;

            shot.Camera.projectionMatrix = Matrix4x4.Frustum(
                left * near, right * near, bottom * near, top * near, near, headsetCamera.farClipPlane);

            Debug.Log($"HeadsetSnapshot: the headset's field - {Mathf.Atan(-left) * Mathf.Rad2Deg:F1} left, " +
                      $"{Mathf.Atan(right) * Mathf.Rad2Deg:F1} right, {Mathf.Atan(top) * Mathf.Rad2Deg:F1} up, " +
                      $"{Mathf.Atan(-bottom) * Mathf.Rad2Deg:F1} down, degrees from straight ahead");

            return shot;
        }

        private Shot MakeEyeShot(string suffix, Camera headsetCamera, EyeView eye)
        {
            var shot = MakeShot(suffix, headsetCamera, eye.InHead, eye.Width, eye.Height);

            // Last: the eye's own frustum, off-centre, exactly as the headset has it.
            shot.Camera.projectionMatrix = eye.Projection;

            return shot;
        }

        /// A camera under the anchor, at `local` - a place and orientation relative
        /// to the held head - rendering into a texture of its own.
        private Shot MakeShot(string suffix, Camera headsetCamera, Matrix4x4 local, int width, int height)
        {
            var cameraObject = new GameObject($"Snapshot_{suffix}");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = local.GetColumn(3);
            cameraObject.transform.localRotation = Quaternion.LookRotation(local.GetColumn(2), local.GetColumn(1));

            var camera = cameraObject.AddComponent<Camera>();

            // Kept out of the headset, and otherwise set up as the headset camera is.
            var data = cameraObject.AddComponent<HDAdditionalCameraData>();
            data.xrRendering = false;

            var headsetData = headsetCamera.GetComponent<HDAdditionalCameraData>();

            if (headsetData != null)
            {
                data.volumeLayerMask = headsetData.volumeLayerMask;
                data.antialiasing = headsetData.antialiasing;
                data.SMAAQuality = headsetData.SMAAQuality;
                data.clearColorMode = headsetData.clearColorMode;
                data.backgroundColorHDR = headsetData.backgroundColorHDR;
            }

            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = $"Snapshot_{suffix}"
            };

            camera.cullingMask = headsetCamera.cullingMask;
            camera.nearClipPlane = headsetCamera.nearClipPlane;
            camera.farClipPlane = headsetCamera.farClipPlane;

            // After every other camera, so that anything they render into - a
            // screen showing another camera's view - is this frame's.
            camera.depth = headsetCamera.depth + 1f;
            camera.targetTexture = texture;

            return new Shot { Camera = camera, Texture = texture, Suffix = suffix };
        }

        /// Each eye the XR display renders, left first: its place in the head's own
        /// axes, from the view matrix the display gives for the headset camera,
        /// together with its projection and resolution. Empty without a running
        /// XR display.
        private static List<EyeView> ReadEyes(Camera headsetCamera, float resolutionScale)
        {
            var eyes = new List<EyeView>();
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);

            var display = displays.Find(d => d.running);

            if (display == null)
                return eyes;

            Matrix4x4 headFromWorld = headsetCamera.transform.worldToLocalMatrix;

            for (int passIndex = 0; passIndex < display.GetRenderPassCount(); passIndex++)
            {
                display.GetRenderPass(passIndex, out var pass);

                for (int i = 0; i < pass.GetRenderParameterCount(); i++)
                {
                    pass.GetRenderParameter(headsetCamera, i, out var parameter);

                    eyes.Add(new EyeView
                    {
                        InHead = headFromWorld * parameter.view.inverse * FlipZ,
                        Projection = parameter.projection,
                        Width = Mathf.Clamp(Mathf.RoundToInt(parameter.viewport.width * pass.renderTargetScaledWidth * resolutionScale), 16, MaxTextureSize),
                        Height = Mathf.Clamp(Mathf.RoundToInt(parameter.viewport.height * pass.renderTargetScaledHeight * resolutionScale), 16, MaxTextureSize)
                    });
                }
            }

            return eyes;
        }

        private static byte[] ReadBack(RenderTexture texture)
        {
            var picture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;

            RenderTexture.active = texture;
            picture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            picture.Apply();
            RenderTexture.active = previous;

            byte[] png = picture.EncodeToPNG();
            Destroy(picture);

            return png;
        }
    }
}
