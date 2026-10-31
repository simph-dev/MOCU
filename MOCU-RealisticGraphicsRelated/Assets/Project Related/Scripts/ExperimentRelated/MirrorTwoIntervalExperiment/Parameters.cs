using System;

using MoogModule;

using FixationPointSettings = MirrorExperiment.FixationPointSettings;
using InsideView = MirrorExperiment.InsideView;
using RearViewSettings = MirrorExperiment.RearViewSettings;
using SnapshotSettings = SnapshotModule.SnapshotSettings;
using StarFieldSettings = MirrorExperiment.StarFieldSettings;


namespace MirrorTwoIntervalExperiment
{
    public class Parameters
    {
        /// Which experiment this config is for, written into the file so that the
        /// exe, given it with -config, starts the right one (ExperimentChoice).
        /// Fixed: read back, it is ignored.
        public ExperimentKind Experiment => ExperimentKind.TwoInterval;

        /// Where sessions are saved, each in a folder of its own. Empty: the
        /// default, Documents/MocuM2I. A relative path is taken from the config
        /// file's own folder, so "Data" keeps the results next to the config - and
        /// the .bat that runs it. Snapshots go into Snapshots inside it. Every
        /// session is also mirrored into the hidden MocuM2I_Vault in the user profile.
        public string DataFolder { get; set; } = "";

        // ---- design ---------------------------------------------------------------------------

        /// Which of the three experiments this run is. See Design.
        public Design Design                                { get; set; } = Design.Center;

        /// What the two intervals are made of when Design is Custom; ignored
        /// otherwise. The defaults are the platform with the stars seen directly,
        /// against the same with the screen's stars added, the screen looking
        /// forward - PeripheralAntiCenter with Center in place of AntiCenter.
        public CueSet CustomVestibular                      { get; set; } = new CueSet { HasVestibular = true, HasVisualOutside = true };
        public CueSet CustomCombined                        { get; set; } = new CueSet { HasVestibular = true, HasVisualOutside = true, HasVisualInside = true, InsideView = InsideView.Forward };

        // ---- trial structure ------------------------------------------------------------------

        /// A multiple of 8: every block of 8 has each combination of which interval
        /// comes first, which one is the Test, and whether the Test is longer or
        /// shorter, once, shuffled.
        public int TrialsNumber                             { get; set; } = 80;

        /// False: one staircase for the whole run, as in RaceExperiment. True: one
        /// for the trials whose Test is the Vestibular interval and one for those
        /// whose Test is the Combined one, interleaved.
        public bool StaircasePerTestInterval                { get; set; } = false;

        public float ChanceToMakeTaskHarder                 { get; set; } = 0.3f;
        public float ChanceToMakeTaskEasier                 { get; set; } = 0.8f;

        // ---- motion ---------------------------------------------------------------------------

        /// The Reference interval's travel, meters, straight forward.
        public float ReferenceDistance                      { get; set; } = 0.08f;

        /// The Test interval is the Reference times 1 + (BaseDiff - 1) * Ratio^level,
        /// or divided by it: [2, 1.7, 1.49, 1.34, 1.24, ...] for 2 and 0.7. The same
        /// shifted exponential as RaceExperiment.
        public float TestDistanceMultiplierBaseDiff         { get; set; } = 2f;
        public float TestDistanceMultiplierRatio            { get; set; } = 0.7f;

        /// Both intervals last this long, whatever their distance: only the
        /// distance differs, and with it the speed.
        public TimeSpan IntervalDuration                    { get; set; } = TimeSpan.FromSeconds(1);
        public float ProfileSigmas                          { get; set; } = 3f;

        /// VR travel relative to the physical travel. 1 = identical.
        public float VisualDistanceMultiplier               { get; set; } = 1f;

        // ---- optic flow -----------------------------------------------------------------------

        /// Fraction of stars that stay put on each noise tick, every visual field.
        public float Coherence                              { get; set; } = 1f;
        public float NoiseUpdateHz                          { get; set; } = 60f;

        /// The torus of stars, made afresh round the eye at the start of every
        /// interval. See MirrorExperiment.StarFieldSettings.
        public StarFieldSettings StarField                  { get; set; } = new StarFieldSettings();

        // ---- screen and fixation --------------------------------------------------------------

        /// The screen in front of the participant. See MirrorExperiment.RearViewSettings.
        /// The taller mirror, 2 : 1, 0.70 m wide at 0.66 m: its glass reaches 27
        /// degrees either side, and the whole image 15 up and down. The hole in the
        /// torus follows its corners, to about 0.94 m.
        public RearViewSettings RearView                    { get; set; } = new RearViewSettings
        {
            ScreenImage = "ScreenRearMirrorTall",
            ScreenWidth = 0.7f
        };

        public FixationPointSettings FixationPoint          { get; set; } = new FixationPointSettings();

        /// Which eye the operator's "Unity view" panel shows when the app starts.
        /// test1 on the Debug tab switches it while the app runs.
        public EyeMirror.Eye OperatorViewEye                { get; set; } = EyeMirror.Eye.Left;

        /// Pictures of the middle of every interval with stars in it, for figures:
        /// the first of each kind in a run, from between the eyes and from each eye,
        /// the head held straight ahead. Off by default. See SnapshotModule.
        public SnapshotSettings Snapshots                   { get; set; } = new SnapshotSettings();

        // ---- positions ------------------------------------------------------------------------

        public DofParameters CameraStartPosition            { get; set; } = new DofParameters { Surge = 0f, Heave = 0f, Sway = 0f };
        public DofParameters MoogParkPosition               { get; set; } = new DofParameters { Heave = -0.22f };

        /// Where Engage leaves the platform, and where it is brought for a pause
        /// and at the end of the run, so Park always starts from here.
        public DofParameters MoogNeutralPosition            { get; set; } = new DofParameters { Heave = -0.22f };

        /// Where every trial starts. Behind neutral, since the two intervals both go
        /// forward one after the other: the longest trial is the Reference plus the
        /// longest Test, 0.08 + 0.16 = 0.24 m with the defaults, which from -0.12
        /// ends at +0.12. The values RaceExperiment used.
        public DofParameters MoogStartPosition              { get; set; } = new DofParameters { Surge = -0.12f, Heave = -0.22f };

        // ---- timing ---------------------------------------------------------------------------

        public TimeSpan MoogRepositionDuration              { get; set; } = TimeSpan.FromSeconds(2);

        /// Back from the end of the second interval to MoogStartPosition. Also the
        /// pause after every trial, platform or not, so the pacing does not vary.
        /// Slower than RaceExperiment's 3 s on purpose: the return can be as long as
        /// 0.24 m, and at 4 s its peak velocity is 0.14 m/s, below the 0.19 m/s of
        /// the 8 cm Reference - at 3 s it would match it.
        public TimeSpan MoogReturnDuration                  { get; set; } = TimeSpan.FromSeconds(4);
        public TimeSpan DelayBetweenMoogAndVr               { get; set; } = TimeSpan.FromMilliseconds(100);
        public float MoogMovementDurationCorrectionFactor   { get; set; } = 0.8f;

        public TimeSpan DelayBeforeStartSound               { get; set; } = TimeSpan.FromSeconds(1.5);
        public TimeSpan DelayAfterStartSignal               { get; set; } = TimeSpan.FromSeconds(0.5);

        /// The empty screen with its fixation point, between the two intervals. At
        /// least as long as the platform still needs to finish the first one.
        public TimeSpan PauseBetweenIntervals               { get; set; } = TimeSpan.FromSeconds(1);

        /// Answers are taken from this long before the second interval ends, so
        /// the participant need not wait out a movement already told apart. The
        /// movement still runs to its end. 0 = only once it has. At most
        /// IntervalDuration.
        public TimeSpan AnswerWindowBeforeEnd               { get; set; } = TimeSpan.FromSeconds(0.5);

        /// From the end of the second interval, however early the answer came.
        public TimeSpan TimeToAnswer                        { get; set; } = TimeSpan.FromSeconds(2);

        /// The platform waits at least this long after the second interval ends,
        /// and at least DelayAfterAnswerSignal after the answer, before it goes back.
        public TimeSpan PauseBeforeReturn                   { get; set; } = TimeSpan.FromSeconds(0.5);
        public TimeSpan DelayAfterAnswerSignal              { get; set; } = TimeSpan.Zero;

        // ---- sounds ---------------------------------------------------------------------------

        public bool PlayStartTrialSound                     { get; set; } = true;
        public bool PlayAnswerReceivedSound                 { get; set; } = true;
        public bool PlayTimeoutSound                        { get; set; } = true;
        public bool PlayCorrectAnswerSound                  { get; set; } = false;
        public bool PlayIncorrectAnswerSound                { get; set; } = false;
    }
}
