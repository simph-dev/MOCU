using System;
using System.Collections.Generic;

using MoogModule;


namespace MirrorExperiment
{
    public class Parameters
    {
        // ---- trial structure ------------------------------------------------------------------

        public int TrialsPerCondition                       { get; set; } = 80;     // 7 conditions -> 560 trials

        /// The conditions to run, interleaved, one staircase each: every visual
        /// field on its own and both together, with the platform and without, and
        /// the platform alone - per being the stars seen directly, cen the ones on
        /// the screen. Replace the list to run anything else, down to a single
        /// condition. See ConditionSettings for the three cues.
        public List<ConditionSettings> Conditions            { get; set; } = new List<ConditionSettings>
        {
            new() { HasVisualOutside = true,  HasVisualInside = false, HasVestibular = false },     // vis per
            new() { HasVisualOutside = false, HasVisualInside = true,  HasVestibular = false },     // vis cen
            new() { HasVisualOutside = true,  HasVisualInside = true,  HasVestibular = false },     // vis both
            new() { HasVisualOutside = false, HasVisualInside = false, HasVestibular = true  },     // ves
            new() { HasVisualOutside = true,  HasVisualInside = false, HasVestibular = true  },     // comb per
            new() { HasVisualOutside = false, HasVisualInside = true,  HasVestibular = true  },     // comb cen
            new() { HasVisualOutside = true,  HasVisualInside = true,  HasVestibular = true  },     // comb both
        };

        /// Heading magnitude at difficulty level 0, degrees. Every level multiplies
        /// it by HeadingRatio, so 16 and 0.5 reproduce the paper's logarithmic
        /// spacing (16, 8, 4, 2, 1, 0.5, 0.25) and keep going below it.
        public float StartHeading                           { get; set; } = 16f;
        public float HeadingRatio                           { get; set; } = 0.5f;

        public float ChanceToMakeTaskHarder                 { get; set; } = 0.3f;
        public float ChanceToMakeTaskEasier                 { get; set; } = 0.8f;

        /// What counts as a correct answer. Nominal (the staircase heading) by
        /// default; switch to Vestibular or Visual for runs where the participant
        /// is instructed to report one cue only, which changes the answer at
        /// headings below Delta/2 and therefore also drives the staircase there.
        public CorrectAnswerReference CorrectAnswerBasedOn   { get; set; } = CorrectAnswerReference.Nominal;

        // ---- motion ---------------------------------------------------------------------------

        public float Distance                               { get; set; } = 0.13f;  // meters
        public TimeSpan StimulusDuration                    { get; set; } = TimeSpan.FromSeconds(1);
        public float ProfileSigmas                          { get; set; } = 3f;     // gives peak v 0.31 m/s, peak a 1.12 m/s^2

        /// Where each cue actually travels, relative to the nominal heading. See
        /// HeadingTransform. By default both go forward along the nominal heading,
        /// as a car does: the platform, and the camera with it. The field seen
        /// directly then flows as driving forward does, and the screen, looking
        /// back like a parking camera, shows the scene behind receding - the same
        /// motion, seen from the back of the car.
        public HeadingTransform VisualTransform             { get; set; } = new HeadingTransform { Rotation = 0f, SwapLeftRight = false };
        public HeadingTransform VestibularTransform         { get; set; } = new HeadingTransform { Rotation = 0f, SwapLeftRight = false };

        /// VR travel can be scaled relative to the physical travel. 1 = identical.
        public float VisualDistanceMultiplier               { get; set; } = 1f;

        // ---- optic flow -----------------------------------------------------------------------

        /// How often the incoherent stars are relocated, in Hz. 60 Hz = the 16.7 ms
        /// frame interval of the paper, confirmed by Adam. The headset renders at
        /// 90 Hz and cannot divide evenly into 60, so noise ticks land on alternating
        /// 1- and 2-frame gaps; StarField accumulates time rather than counting
        /// frames so the average rate stays correct. The original did the same thing,
        /// by hand, in MoogCom.cpp. See README.md for the full history and for why
        /// the coherence value cannot simply be carried over.
        public float NoiseUpdateHz                          { get; set; } = 60f;

        /// The torus of stars round the participant: its width, density and star
        /// size. Its hole is worked out from Mirror and Distance. The material is
        /// Resources/StarMaterial, and the debug toggles are on the Stars object's
        /// Inspector while the app runs.
        public StarFieldSettings StarField                  { get; set; } = new StarFieldSettings();

        // ---- mirror ---------------------------------------------------------------------------

        /// The rear-view screen. On by default. See RearViewSettings, and README.md
        /// for which way left and right end up.
        public RearViewSettings RearView                    { get; set; } = new RearViewSettings();

        // ---- fixation -------------------------------------------------------------------------

        /// The dot the participant keeps their eyes on: whether it is shown, what it
        /// is fixed to - the body, the mirror or the head - and its size. See
        /// FixationPointSettings.
        public FixationPointSettings FixationPoint          { get; set; } = new FixationPointSettings();

        // ---- positions ------------------------------------------------------------------------

        /// Where the camera rig sits before the stimulus. All zero: this scene has no
        /// environment, and the star cloud is anchored on the rig rather than on the
        /// world, so the absolute height carries no meaning. (RaceExperiment used
        /// 1.7 because it had a corridor with a floor at zero.)
        public DofParameters CameraStartPosition            { get; set; } = new DofParameters { Surge = 0f, Heave = 0f, Sway = 0f };
        public DofParameters MoogParkPosition               { get; set; } = new DofParameters { Heave = -0.22f };

        /// Home point of the platform. Every trial starts here, travels one full
        /// displacement out and comes back, so the platform is always at home
        /// between trials and engage/park need no repositioning first.
        public DofParameters MoogNeutralPosition            { get; set; } = new DofParameters { Heave = -0.22f };

        // ---- timing ---------------------------------------------------------------------------

        public TimeSpan MoogRepositionDuration              { get; set; } = TimeSpan.FromSeconds(2);
        /// Also the length of the pause after every trial, platform or not, so the
        /// pacing does not vary by condition. Slow on purpose: the return travels the
        /// same 0.13 m as the stimulus, and stretching it to 3 s drops peak velocity
        /// from 0.31 to 0.10 m/s and peak acceleration from 1.12 to 0.12 m/s^2, which
        /// keeps it well clear of the stimulus it follows.
        public TimeSpan MoogReturnDuration                  { get; set; } = TimeSpan.FromSeconds(3);
        public TimeSpan DelayBetweenMoogAndVr               { get; set; } = TimeSpan.FromMilliseconds(100);
        public float MoogMovementDurationCorrectionFactor   { get; set; } = 0.8f;

        public TimeSpan DelayBeforeStartSound               { get; set; } = TimeSpan.FromSeconds(1.5);
        public TimeSpan DelayAfterStartSignal               { get; set; } = TimeSpan.FromSeconds(0.5);
        public TimeSpan TimeToAnswer                        { get; set; } = TimeSpan.FromSeconds(2);
        public TimeSpan DelayAfterAnswerSignal              { get; set; } = TimeSpan.Zero;

        // ---- sounds ---------------------------------------------------------------------------

        public bool PlayStartTrialSound                     { get; set; } = true;
        public bool PlayAnswerReceivedSound                 { get; set; } = true;
        public bool PlayTimeoutSound                        { get; set; } = true;
        public bool PlayCorrectAnswerSound                  { get; set; } = false;
        public bool PlayIncorrectAnswerSound                { get; set; } = false;
    }
}
