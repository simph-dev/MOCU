namespace MoogModule
{
    /// <summary>
    /// Defines the geometric shape of the trajectory path in space, independently of how the object moves along it over time.
    /// While <see cref="TrajectoryProfile"/> controls the timing and speed variation along the path,
    /// <see cref="TrajectoryType"/> determines the actual form of the path itself (e.g., straight line, circular arc).
    /// </summary>
    public enum TrajectoryType
    {
        None,
        Linear,
        Circular,
        Custom
    }
}