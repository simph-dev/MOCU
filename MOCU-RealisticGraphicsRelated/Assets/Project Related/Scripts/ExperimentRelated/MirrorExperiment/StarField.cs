using UnityEngine;
using UnityEngine.Rendering;


namespace MirrorExperiment
{
    /// 3D cloud of white triangles ("stars") producing the optic flow stimulus.
    ///
    /// Defaults come from the real MoogDots parameter logs:
    ///     STAR_VOLUME    130 130 100  (cm)
    ///     STAR_DENSITY   0.00125      (stars/cm^3  ->  1250 stars/m^3  ->  2112 stars)
    ///     STAR_SIZE      0.5 0.5      (cm)
    ///     CLIP_PLANES    5 100        (cm, set on the camera - not here)
    ///
    /// Two things about how this works, because they are easy to get wrong:
    ///
    /// 1. The stars never move under their own power. They are static in world
    ///    space and all optic flow comes from the camera travelling through them.
    ///    "Moving coherently" simply means "not relocated on this frame".
    ///
    /// 2. Relocation is permanent. A star picked by the coherence draw is
    ///    overwritten with a fresh uniformly random position anywhere in the
    ///    volume and stays there; there is no stored "home" position to return
    ///    to. This matches ModifyStarField() in the original OpenGL source, and
    ///    is what makes the paper's survival argument (0.65^12 < 1% over 12
    ///    frames) hold.
    public class StarField : MonoBehaviour
    {
        [Header("Material")]
        [Tooltip("HDRP Unlit, plain white, Double-Sided ON, GPU Instancing ON. " +
                 "The original disables face culling entirely, so the triangles must be double-sided.")]
        public Material StarMaterial;

        [Header("Geometry (meters) - overwritten by the config file")]
        [Tooltip("These are only what the preview uses before an experiment starts. " +
                 "Pressing Start replaces them with Parameters.StarField from the config, " +
                 "so edit the JSON, not this.")]
        public Vector3 Volume = new Vector3(1.30f, 1.30f, 1.00f);
        public float DistanceToCloudCenter = 0.66f;
        public float Density = 1250f;
        public Vector2 StarSize = new Vector2(0.01f, 0.01f);

        [Header("Rendering")]
        public bool CastShadows = false;
        public bool ReceiveShadows = false;

        [Header("Debug")]
        [Tooltip("Draw the cloud continuously, outside any trial. For checking placement, " +
                 "material and layer without having to run the experiment. Turn off before a session.")]
        public bool AlwaysVisible = false;

        [Tooltip("Coherence used while AlwaysVisible is on.")]
        [Range(0f, 1f)] public float PreviewCoherence = 0.65f;

        // Graphics.DrawMeshInstanced draws at most 1023 instances per call, so the
        // cloud is submitted in batches. Worth replacing with a ComputeBuffer and
        // DrawMeshInstancedProcedural later, but this needs no custom shader.
        private const int MaxInstancesPerDrawCall = 1023;

        /// Ceiling on how much noise backlog a single frame will work through.
        private const int MaxNoiseTicksPerFrame = 4;

        private Mesh _mesh;
        private Matrix4x4[] _matrices;
        private Matrix4x4[] _drawBatch;
        private int _starCount;

        private Vector3 _cloudCenter;
        private Quaternion _cloudRotation = Quaternion.identity;

        private float _coherence = 1f;
        private float _noiseUpdateHz = 60f;
        private float _noiseAccumulator;
        private bool _isVisible;
        private bool _previewAnchored;

        // Measured, not requested. On the old system nobody could say afterwards
        // whether the star field had actually been running at 60 Hz or at 45, so
        // the achieved rate is counted here and reported rather than assumed.
        private int _noiseTicksThisTrial;
        private float _visibleTimeThisTrial;

        public int StarCount => _starCount;
        public bool IsVisible => _isVisible;

        /// Noise updates per second actually achieved during the last stimulus.
        /// 0 until a trial with less than 100% coherence has run.
        public float MeasuredNoiseHz =>
            _visibleTimeThisTrial > 0f ? _noiseTicksThisTrial / _visibleTimeThisTrial : 0f;


        private void Awake()
        {
            Rebuild();
        }

        /// Takes the geometry from the config. Called when an experiment starts, so
        /// the cloud can be changed without recompiling or touching the scene.
        public void Configure(StarFieldSettings settings)
        {
            if (settings == null)
                return;

            Volume = new Vector3(settings.VolumeWidth, settings.VolumeHeight, settings.VolumeDepth);
            DistanceToCloudCenter = settings.DistanceToCloudCenter;
            Density = settings.DensityPerCubicMeter;
            StarSize = new Vector2(settings.StarWidth, settings.StarHeight);

            Rebuild();
        }

        /// Reallocates for the current geometry. Star count follows from volume and
        /// density, so both arrays have to be resized whenever either changes.
        private void Rebuild()
        {
            _mesh ??= CreateTriangleMesh();

            _starCount = Mathf.Max(1, Mathf.RoundToInt(Volume.x * Volume.y * Volume.z * Density));
            _matrices = new Matrix4x4[_starCount];
            _drawBatch = new Matrix4x4[Mathf.Min(MaxInstancesPerDrawCall, _starCount)];

            Regenerate(transform.position, transform.rotation);
            _previewAnchored = false;
        }

        /// Re-centres the cloud on the observer and gives every star a fresh
        /// position. Call at the start of every trial, before the camera moves.
        public void Regenerate(Vector3 eyePosition, Quaternion straightAhead)
        {
            _cloudRotation = straightAhead;
            _cloudCenter = eyePosition + straightAhead * (Vector3.forward * DistanceToCloudCenter);

            for (int i = 0; i < _starCount; i++)
                _matrices[i] = MakeStarMatrix(RandomPointInCloud());
        }

        public void Show(float coherence, float noiseUpdateHz)
        {
            _coherence = Mathf.Clamp01(coherence);
            _noiseUpdateHz = Mathf.Max(1f, noiseUpdateHz);
            _noiseAccumulator = 0f;
            _noiseTicksThisTrial = 0;
            _visibleTimeThisTrial = 0f;
            _isVisible = true;
        }

        public void Hide()
        {
            _isVisible = false;
        }

        private void Update()
        {
            if (StarMaterial == null)
                return;

            if (_isVisible)
            {
                _previewAnchored = false;
            }
            else if (AlwaysVisible)
            {
                AnchorPreview();
            }
            else
            {
                _previewAnchored = false;
                return;
            }

            AdvanceNoise(Time.deltaTime);
            Draw();
        }

        /// Awake anchors the cloud on this GameObject's transform, which normally
        /// sits at the origin - on the floor, well below eye height. A trial
        /// re-anchors on the camera rig before showing anything, but a preview has to
        /// do it for itself or the cloud appears under the participant's feet.
        ///
        /// Done once per preview, not per frame: re-anchoring regenerates every star,
        /// so doing it continuously would replace the flow with pure noise.
        private void AnchorPreview()
        {
            _coherence = PreviewCoherence;
            _noiseUpdateHz = 60f;

            if (_previewAnchored)
                return;

            var anchor = Camera.main != null ? Camera.main.transform : transform;
            Regenerate(anchor.position, Quaternion.Euler(0f, anchor.eulerAngles.y, 0f));

            _previewAnchored = true;
        }

        // ...........................................

        private void AdvanceNoise(float deltaTime)
        {
            // At 100% coherence nothing is ever relocated: the cloud stays frozen
            // for the whole trial and the flow is pure self-motion.
            if (_coherence >= 1f)
                return;

            float step = 1f / _noiseUpdateHz;
            _noiseAccumulator += deltaTime;
            _visibleTimeThisTrial += deltaTime;

            // After a long stall, catching up tick by tick would relocate the cloud
            // dozens of times in one frame for no visible benefit - the result is the
            // same uniform cloud either way. Cap the backlog and let MeasuredNoiseHz
            // record that the rate was missed.
            _noiseAccumulator = Mathf.Min(_noiseAccumulator, step * MaxNoiseTicksPerFrame);

            // A loop rather than a single pass so that a dropped frame produces the
            // right number of noise updates instead of silently slowing the noise.
            while (_noiseAccumulator >= step)
            {
                _noiseAccumulator -= step;
                _noiseTicksThisTrial++;
                RelocateIncoherentStars();
            }
        }

        private void RelocateIncoherentStars()
        {
            for (int i = 0; i < _starCount; i++)
                if (Random.value > _coherence)
                    _matrices[i] = MakeStarMatrix(RandomPointInCloud());
        }

        private void Draw()
        {
            var shadows = CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            for (int offset = 0; offset < _starCount; offset += MaxInstancesPerDrawCall)
            {
                int count = Mathf.Min(MaxInstancesPerDrawCall, _starCount - offset);
                System.Array.Copy(_matrices, offset, _drawBatch, 0, count);

                Graphics.DrawMeshInstanced(
                    _mesh, 0, StarMaterial, _drawBatch, count, null,
                    shadows, ReceiveShadows, gameObject.layer);
            }
        }

        private Vector3 RandomPointInCloud()
        {
            // Plain uniform sampling per axis, exactly as in the original. No
            // minimum-spacing constraint on purpose: an even layout would give the
            // observer a positional cue and let them read local geometry instead of
            // integrating motion across many stars.
            Vector3 local = new Vector3(
                Random.Range(-Volume.x / 2f, Volume.x / 2f),
                Random.Range(-Volume.y / 2f, Volume.y / 2f),
                Random.Range(-Volume.z / 2f, Volume.z / 2f));

            return _cloudCenter + _cloudRotation * local;
        }

        private Matrix4x4 MakeStarMatrix(Vector3 position)
        {
            return Matrix4x4.TRS(position, _cloudRotation, new Vector3(StarSize.x, StarSize.y, 1f));
        }

        private Mesh CreateTriangleMesh()
        {
            var mesh = new Mesh { name = "Star" };

            // Same triangle as GenerateStarField() in MoogDots: apex at the top,
            // base at the bottom, all three vertices coplanar. Orientation is fixed
            // in world space - the original does not billboard the stars.
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.0f,  0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward };
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
