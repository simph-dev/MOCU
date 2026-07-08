namespace Temporal
{
    public enum Temporal2IntervalTrialState
    {
        None,
        Initializing,
        ReadyToStart,
        PreFirstIntervalPause,
        FirstInterval,
        InterIntervalPause,
        SecondInterval,
        AnswerPhase,
        PreReturningPause,
        Returning,
        Analyzation
    }

    public enum Temporal2IntervalExperimentState
    {
        None,
        Started,
        Finished
    }
}