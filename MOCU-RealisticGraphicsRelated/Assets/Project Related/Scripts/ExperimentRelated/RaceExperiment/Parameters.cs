using System;
using System.Collections.Generic;

using Temporal;
using MoogModule;


namespace RaceExperiment
{
    public class Parameters
    {
        //public IReadOnlyList<float> Multipliers             { get; set; } = new List<float> { 0.5f, 0.75f, 1.125f, 1.25f, 1.5f, 1.75f, 2.0f }.AsReadOnly();
        //public IReadOnlyList<float> Multipliers             { get; set; } = new List<float> { 0.5f, 0.75f, 0.875f, 0.9375f, 0.96875f, 1.0625f, 1.125f, 1.25f, 1.5f, 2f }.AsReadOnly();

        public int TrialsNumber                             { get; set; } = 0;      // Better be modulus 4
        public float DistanceMultiplier                     { get; set; } = 1.0f;   // Moog is basis, ad VR may be longer
        public float ReferenceDistance                      { get; set; } = 0.08f;  // meters
        public TimeSpan ReferenceDuration                   { get; set; } = TimeSpan.FromSeconds(1);
        public float TestDistanceMultiplierBaseDiff         { get; set; } = 2f;
        public float TestDistanceMultiplierRatio            { get; set; } = 0.7f;
        public float TestDurationMultiplierBaseDiff         { get; set; } = 2f;
        public float TestDurationMultiplierRatio            { get; set; } = 0.7f;
        public DofParameters CameraStartPosition            { get; set; } = new DofParameters { Surge = 0, Heave = 1.7f, Sway = 0 };
        public DofParameters MoogStartPosition              { get; set; } = new DofParameters { Surge = -0.12f, Heave = -0.22f, Sway = 0 };
        public ExperimentStimulusType StimulusType  { get; set; } = ExperimentStimulusType.None;
        public float StartDistanceToTarget                  { get; set; } = 2.5f;
        public TimeSpan DelayBetweenMoogAndVr               { get; set; } = TimeSpan.FromMilliseconds(100); // Moog starts with delay
        public float MoogMovementDurationCorrectionFactor   { get; set; } = 0.8f;   // It takes to Moog more time (1 / 1.25) ~1100ms (-100 from prev param)

        public TimeSpan FirstMovementDuration               { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan SecondMovementDuration              { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan BackwardMovementDuration            { get; set; } = TimeSpan.FromSeconds(3);
        public TimeSpan PauseBetweenIntervalsDuration       { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan DelayBeforeStartSound               { get; set; } = TimeSpan.FromSeconds(1.5);
        public TimeSpan DelayAfterStartSignal               { get; set; } = TimeSpan.FromSeconds(0.5);
        public TimeSpan TimeToAnswer                        { get; set; } = TimeSpan.FromSeconds(2);
        public TimeSpan DelayAfterAnswerSignal              { get; set; } = TimeSpan.FromSeconds(0);

        public bool PlayStartTrialSound                     { get; set; } = true;
        public bool PlayFinishTrialSound                    { get; set; } = true;
        public bool PlayTimeoutSound                        { get; set; } = true;
        public bool PlayAnswerReceivedSound                 { get; set; } = true;
        public bool PlayCorrectAnswerSound                  { get; set; } = false;
        public bool PlayIncorrectAnswerSound                { get; set; } = false;

        public float ChanceToMakeTaskHarder                 { get; set; } = 0.3f;
        public float ChanceToMakeTaskEasier                 { get; set; } = 0.8f;
    }
}