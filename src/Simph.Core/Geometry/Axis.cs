namespace Simph.Core.Geometry
{
    /// <summary>
    /// The six degrees of freedom, in the order MOCU has always used them.
    /// Kept as an enum so limits, validation and logging can walk the axes
    /// instead of repeating six near-identical lines each time.
    /// </summary>
    public enum Axis
    {
        Roll = 0,
        Pitch = 1,
        Yaw = 2,
        Surge = 3,
        Sway = 4,
        Heave = 5
    }


    public static class Axes
    {
        /// <summary>All six, for iteration.</summary>
        public static readonly Axis[] All =
        {
            Axis.Roll, Axis.Pitch, Axis.Yaw, Axis.Surge, Axis.Sway, Axis.Heave
        };

        /// <summary>True for the three translations, which are in metres rather than radians.</summary>
        public static bool IsTranslation(Axis axis) =>
            axis == Axis.Surge || axis == Axis.Sway || axis == Axis.Heave;

        public static string Unit(Axis axis) => IsTranslation(axis) ? "m" : "rad";
    }
}
