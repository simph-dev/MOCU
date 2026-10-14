using UnityEngine;
using UnityEngine.Rendering;


namespace MirrorExperiment
{
    /// 3D cloud of white triangles ("stars") producing the optic flow stimulus.
    ///
    /// Density and star size come from the real MoogDots parameter logs:
    ///     STAR_DENSITY   0.00125      (stars/cm^3  ->  1250 stars/m^3)
    ///     STAR_SIZE      0.5 0.5      (cm)
    /// The shape does not. The paper's cloud was a 130 x 130 x 100 cm box ahead of
    /// the participant; this one is a ring all the way round them, square in
    /// cross-section, with a hole in the middle sized so that no star comes between
    /// the eye and the screen during a trial (see StarFieldSettings).
    ///
    /// Two things about how this works, because they are easy to get wrong:
    ///
    /// 1. The stars never move under their own power. They are static in world
    ///    space and all optic flow comes from the camera travelling through them.
    ///    "Moving coherently" simply means "not relocated on this frame".
    ///
    /// 2. Relocation is permanent. A star picked by the coherence draw is
    ///    overwritten with a fresh uniformly random position anywhere in the
    ///    ring and stays there; there is no stored "home" position to return
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
        [Tooltip("No star is closer than this to the eye's place at the start of the trial. Worked out " +
                 "from the screen and the travel when the app starts and on every Start.")]
        public float HoleRadius = 0.84f;

        [Tooltip("Replaced with Parameters.StarField from the config when the app starts " +
                 "and again on every Start, so edit the JSON, not this.")]
        public float TorusWidth = 1f;
        public float Density = 1250f;
        public Vector2 StarSize = new Vector2(0.01f, 0.01f);
        public bool StarsFaceCenter = false;

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
        private Matrix4x4[] _mirrorBatch;
        private int _starCount;

        /// The mirror's share of the cloud (see SetMirrorStars). 0: not drawn.
        private float _mirrorFraction;
        private float _mirrorScale = 1f;
        private Matrix4x4 _mirrorScaleMatrix = Matrix4x4.identity;

        /// The ring: centred on the eye's place at the start of the trial, its axis
        /// upright, its walls at the inner and outer radius, and as high as it is
        /// thick, half of that each side of eye height.
        private Vector3 _center;
        private float _innerRadius;
        private float _outerRadius;
        private float _halfHeight;

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

        /// The layer the mirror's share of the cloud is drawn on: the one the
        /// mirror's cameras render instead of this object's own. -1 if there is no
        /// such layer, and then that share is never drawn.
        public int MirrorLayer { get; set; } = -1;

        /// Noise updates per second actually achieved during the last stimulus.
        /// 0 until a trial with less than 100% coherence has run.
        public float MeasuredNoiseHz =>
            _visibleTimeThisTrial > 0f ? _noiseTicksThisTrial / _visibleTimeThisTrial : 0f;


        private void Awake()
        {
            Rebuild();
        }

        /// Takes the geometry from the config, and the hole from the experiment,
        /// which works it out from the screen and the travel. Called when the app
        /// starts and again on every Start, so the cloud can be changed without
        /// recompiling or touching the scene.
        public void Configure(StarFieldSettings settings, float holeRadius)
        {
            if (settings == null)
                return;

            HoleRadius = holeRadius;
            TorusWidth = settings.TorusWidth;
            Density = settings.DensityPerCubicMeter;
            StarSize = new Vector2(settings.StarWidth, settings.StarHeight);
            StarsFaceCenter = settings.StarsFaceCenter;

            Rebuild();
        }

        /// Reallocates for the current geometry. Star count follows from the
        /// ring's volume and the density, so both arrays have to be resized
        /// whenever either changes.
        private void Rebuild()
        {
            _mesh ??= CreateTriangleMesh();

            float width = Mathf.Max(0.001f, TorusWidth);
            _innerRadius = Mathf.Max(0f, HoleRadius);
            _outerRadius = _innerRadius + width;
            _halfHeight = width / 2f;

            float volume = Mathf.PI * (_outerRadius * _outerRadius - _innerRadius * _innerRadius) * width;

            _starCount = Mathf.Max(1, Mathf.RoundToInt(volume * Density));
            _matrices = new Matrix4x4[_starCount];
            _drawBatch = new Matrix4x4[Mathf.Min(MaxInstancesPerDrawCall, _starCount)];
            _mirrorBatch = new Matrix4x4[_drawBatch.Length];

            Regenerate(transform.position);
            _previewAnchored = false;
        }

        /// Centres the ring on the eye's place and gives every star a fresh
        /// position. Call at the start of every trial, before the camera moves:
        /// the ring then stays where it is while the car goes through it.
        public void Regenerate(Vector3 eyePlace)
        {
            _center = eyePlace;

            for (int i = 0; i < _starCount; i++)
                _matrices[i] = MakeStarMatrix(RandomPointInRing());
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

        /// What the mirror's cameras are shown: the first `fraction` of the stars,
        /// drawn a second time on MirrorLayer, `scale` times larger. Positions are
        /// drawn independently and uniformly, and the noise relocates each star
        /// independently and uniformly too, so the first stars of the array are a
        /// uniform cloud of their own, only sparser - and still the same stars,
        /// moving and flickering with all the others. 0 draws none of them.
        public void SetMirrorStars(float fraction, float scale)
        {
            _mirrorFraction = Mathf.Clamp01(fraction);
            _mirrorScale = Mathf.Max(0.01f, scale);
            _mirrorScaleMatrix = Matrix4x4.Scale(new Vector3(_mirrorScale, _mirrorScale, 1f));
        }

        /// LateUpdate, not Update: coroutines resume between the two, and it is the
        /// experiment's coroutine that shows and hides the cloud and switches the
        /// cameras' culling masks. Drawn after it, the cloud goes out - or not - in
        /// the same frame as the masks meant for it. Drawn before it, the last frame
        /// of a stimulus went out with the cloud already queued and the masks
        /// already reset, and the stars flashed for one frame in a field the
        /// condition had kept dark.
        private void LateUpdate()
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

        /// Awake centres the ring on this GameObject's transform, which normally
        /// sits at the origin - on the floor, well below eye height. A trial
        /// re-centres it on the eye before showing anything, but a preview has to
        /// do it for itself or the cloud appears round the participant's feet.
        ///
        /// Done once per preview, not per frame: re-centring regenerates every star,
        /// so doing it continuously would replace the flow with pure noise.
        private void AnchorPreview()
        {
            _coherence = PreviewCoherence;
            _noiseUpdateHz = 60f;

            if (_previewAnchored)
                return;

            var anchor = Camera.main != null ? Camera.main.transform : transform;
            Regenerate(anchor.position);

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
                    _matrices[i] = MakeStarMatrix(RandomPointInRing());
        }

        /// Every star, every frame: the ring already holds only the stars there
        /// are to see, so nothing is left out at drawing time - and nothing can
        /// appear or vanish as the car moves.
        private void Draw()
        {
            var shadows = CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            int mirrorStars = MirrorLayer >= 0 ? Mathf.RoundToInt(_starCount * _mirrorFraction) : 0;
            bool mirrorScaled = !Mathf.Approximately(_mirrorScale, 1f);

            int count = 0;
            int mirrorCount = 0;

            for (int i = 0; i < _starCount; i++)
            {
                _drawBatch[count++] = _matrices[i];

                if (count == _drawBatch.Length)
                {
                    DrawBatch(_drawBatch, count, shadows, gameObject.layer);
                    count = 0;
                }

                if (i >= mirrorStars)
                    continue;

                // The scale on the right, so it acts in the star's own plane and
                // about its own centre, leaving its position alone.
                _mirrorBatch[mirrorCount++] = mirrorScaled ? _matrices[i] * _mirrorScaleMatrix : _matrices[i];

                if (mirrorCount == _mirrorBatch.Length)
                {
                    DrawBatch(_mirrorBatch, mirrorCount, shadows, MirrorLayer);
                    mirrorCount = 0;
                }
            }

            if (count > 0)
                DrawBatch(_drawBatch, count, shadows, gameObject.layer);

            if (mirrorCount > 0)
                DrawBatch(_mirrorBatch, mirrorCount, shadows, MirrorLayer);
        }

        private void DrawBatch(Matrix4x4[] batch, int count, ShadowCastingMode shadows, int layer)
        {
            Graphics.DrawMeshInstanced(
                _mesh, 0, StarMaterial, batch, count, null,
                shadows, ReceiveShadows, layer);
        }

        /// Uniform over the ring's volume. No minimum-spacing constraint on
        /// purpose, as in the original: an even layout would give the observer a
        /// positional cue and let them read local geometry instead of integrating
        /// motion across many stars.
        private Vector3 RandomPointInRing()
        {
            // Uniform in the square of the radius, not the radius itself: the outer
            // wall is a longer circle than the inner, and holds that many more stars.
            float fromAxis = Mathf.Sqrt(Random.Range(_innerRadius * _innerRadius, _outerRadius * _outerRadius));
            float angle = Random.Range(0f, 2f * Mathf.PI);
            float up = Random.Range(-_halfHeight, _halfHeight);

            return _center + new Vector3(fromAxis * Mathf.Sin(angle), up, fromAxis * Mathf.Cos(angle));
        }

        /// Straight ahead, all of them, as in the earlier experiments - or, with
        /// StarsFaceCenter, upright and turned to face the eye's place, once, where
        /// the star is put. Either way the star does not turn after that: the
        /// stars are not billboards.
        private Matrix4x4 MakeStarMatrix(Vector3 position)
        {
            Quaternion facing = StarsFaceCenter ? Quaternion.LookRotation(position - _center) : Quaternion.identity;

            return Matrix4x4.TRS(position, facing, new Vector3(StarSize.x, StarSize.y, 1f));
        }

        private Mesh CreateTriangleMesh()
        {
            var mesh = new Mesh { name = "Star" };

            // Same triangle as GenerateStarField() in MoogDots: apex at the top,
            // base at the bottom, all three vertices coplanar.
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
