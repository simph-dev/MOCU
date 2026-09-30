using UnityEngine;
using UnityEngine.Rendering;


namespace MirrorExperiment
{
    /// The fixation dot, built in code from FixationPointSettings, like the mirror.
    ///
    /// Drawn with the FixationPoint material: the project's HDRP Unlit fixation
    /// shader, transparent, depth test Always. So nothing ever hides it - neither a
    /// star passing in front nor the mirror screen it may sit on. The material is
    /// loaded from MirrorExperiment/Resources, because nothing in the scene refers
    /// to it any more and a build would otherwise leave it out.
    ///
    /// Unity's own sphere, as the scene object used to be. Unlit, it looks like a
    /// flat disc from where the participant sits, but from any other viewpoint - a
    /// camera recording the scene from the side - it is still a ball.
    public class FixationPoint : MonoBehaviour
    {
        private const string MaterialResource = "FixationPoint";

        private Transform _head;
        private Vector3 _carEye;
        private FixationAnchor _anchor;

        /// From the anchor's origin - the eye's place in the car, or the head - to
        /// the dot, in world axes.
        private Vector3 _offset;

        private bool _built;


        /// Takes everything from the config; called at startup and on every Start.
        /// Returns false, and changes nothing, if the material cannot be found.
        public bool Configure(FixationPointSettings settings, MirrorSettings mirror, Transform head, int layer,
                              out string problem)
        {
            problem = null;

            if (!settings.Enabled)
            {
                gameObject.SetActive(false);
                return true;
            }

            if (!_built && !Build(out problem))
                return false;

            gameObject.SetActive(true);

            _head = head;
            _anchor = settings.Anchor;
            _offset = settings.Anchor == FixationAnchor.Mirror
                ? mirror.ScreenCenter()
                : new Vector3(0f, 0f, settings.Distance);

            gameObject.layer = layer;
            transform.localScale = Vector3.one * Mathf.Max(0.0001f, settings.Diameter);

            return true;
        }

        /// The eye's place in the car: the calibrated home plus the camera
        /// trajectory. Supplied every frame, together with the camera rig. Body and
        /// Mirror hang off it; Head ignores it.
        public void SetCarAnchor(Vector3 eyePlace)
        {
            _carEye = eyePlace;
        }

        /// LateUpdate, so that the head has been tracked for this frame - the
        /// ordering the PositionConstraint used to give.
        private void LateUpdate()
        {
            if (_head == null)
                return;

            Vector3 origin = _anchor == FixationAnchor.Head ? _head.position : _carEye;
            transform.position = origin + _offset;
        }

        // ...........................................

        /// Renderer and mesh only once the material is known to exist: until then
        /// the object draws nothing, rather than a default-material sphere.
        private bool Build(out string problem)
        {
            var material = Resources.Load<Material>(MaterialResource);

            if (material == null)
            {
                problem = $"Fixation point material \"{MaterialResource}\" not found in any Resources folder";
                return false;
            }

            gameObject.AddComponent<MeshFilter>().sharedMesh = BuiltInSphere();

            var sphereRenderer = gameObject.AddComponent<MeshRenderer>();
            sphereRenderer.sharedMaterial = material;
            sphereRenderer.shadowCastingMode = ShadowCastingMode.Off;
            sphereRenderer.receiveShadows = false;

            _built = true;
            problem = null;
            return true;
        }

        /// Unity's built-in sphere mesh, diameter 1. Borrowed from a primitive that
        /// is switched off at once - before it can be drawn - and thrown away. The
        /// mesh is a built-in asset and outlives it; it must never be destroyed.
        private static Mesh BuiltInSphere()
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            primitive.SetActive(false);

            Mesh sphere = primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);

            return sphere;
        }
    }
}
