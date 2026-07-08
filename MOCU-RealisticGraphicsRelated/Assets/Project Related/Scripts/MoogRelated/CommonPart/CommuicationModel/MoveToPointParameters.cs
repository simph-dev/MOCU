using System;


namespace MoogModule
{
    public class MoveToPointParameters
    {
        public DofParameters Coordinate     { get; set; } = default;
        public DateTime ScheduledStartTime  { get; set; } = DateTime.MinValue;
    }
}