#nullable enable

using System;


namespace MoogModule
{
    public class MoveByTrajectoryParameters
    {
        public int DesiredFps                                       { get; set; } = 1000;
        public TimeSpan MovementDuration                            { get; set; } = TimeSpan.Zero;
        public DateTime ScheduledStartTime                          { get; set; } = DateTime.MinValue;
        public DofParameters StartPoint                             { get; set; } = default;
        public DofParameters EndPoint                               { get; set; } = default;
        public TrajectoryType TrajectoryType                        { get; set; } = TrajectoryType.None;
        public TrajectoryProfile TrajectoryProfile                  { get; set; } = TrajectoryProfile.None;
        public DelayCompensationStrategy DelayHandling              { get; set; } = DelayCompensationStrategy.None;
        public TrajectoryTypeSettings? TrajectoryTypeSettings       { get; set; } = null;
        public TrajectoryProfileSettings? TrajectoryProfileSettings { get; set; } = null;
    }
}