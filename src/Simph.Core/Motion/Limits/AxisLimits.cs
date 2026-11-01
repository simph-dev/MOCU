namespace Simph.Core.Motion.Limits
{
    /// <summary>
    /// What one degree of freedom is allowed to do. Metres and radians, matching
    /// Pose6. A bound of negative or positive infinity means "not established" -
    /// the validator then leaves that bound unchecked rather than inventing one,
    /// because a guessed safety limit is worse than an absent one: it either
    /// rejects legitimate movements or passes dangerous ones, and nothing tells
    /// you which.
    /// </summary>
    public readonly struct AxisLimits
    {
        public double MinPosition { get; }
        public double MaxPosition { get; }
        public double MaxSpeed { get; }
        public double MaxAcceleration { get; }

        public AxisLimits(double minPosition, double maxPosition, double maxSpeed, double maxAcceleration)
        {
            MinPosition = minPosition;
            MaxPosition = maxPosition;
            MaxSpeed = maxSpeed;
            MaxAcceleration = maxAcceleration;
        }

        public bool PositionKnown =>
            !double.IsInfinity(MinPosition) && !double.IsInfinity(MaxPosition);
    }
}
