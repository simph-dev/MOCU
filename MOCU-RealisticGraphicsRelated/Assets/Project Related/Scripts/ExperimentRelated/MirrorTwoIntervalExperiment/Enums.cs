namespace MirrorTwoIntervalExperiment
{
    /// The two kinds of interval every trial pairs: the platform alone, or the
    /// platform with optic flow. What each is made of is the Design's.
    public enum IntervalKind
    {
        Vestibular,
        Combined
    }

    public enum IntervalRole
    {
        /// Always Parameters.ReferenceDistance.
        Reference,

        /// Reference times the staircase's multiplier, or divided by it.
        Test
    }

    public enum Polarity
    {
        Decrease = -1,
        Increase = 1
    }

    public enum TrialAnswer
    {
        None = -1,
        Timeout = 0,
        FirstInterval = 1,
        SecondInterval = 2
    }

    public enum TrialState
    {
        None,
        Initializing,
        ReadyToStart,
        PreFirstIntervalPause,
        FirstInterval,
        InterIntervalPause,
        SecondInterval,
        AnswerPhase,
        Returning,
        Analyzation
    }

    public enum ExperimentState
    {
        None,
        Started,

        /// Held between trials, platform at its neutral position. The run resumes
        /// from the same trial with every staircase intact.
        Paused,

        Finished
    }
}
