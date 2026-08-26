namespace MirrorExperiment
{
    /// Single-interval trial, unlike the two-interval structure of RaceExperiment.
    public enum TrialState
    {
        None,
        Initializing,
        ReadyToStart,
        PreStimulusPause,
        Stimulus,
        AnswerPhase,
        Returning,
        Analyzation
    }

    public enum ExperimentState
    {
        None,
        Started,

        /// Held between trials, platform parked. The run resumes from the same trial
        /// with every staircase intact.
        Paused,

        Finished
    }
}
