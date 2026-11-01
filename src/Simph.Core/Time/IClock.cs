namespace Simph.Core.Time
{
    /// <summary>
    /// Where time comes from. Passed in rather than read from a static, so that
    /// a test can drive a whole session - staircases, phases, timeouts - through
    /// a clock it moves by hand, with no waiting and no dependence on frame rate.
    /// </summary>
    public interface IClock
    {
        Timestamp Now { get; }
    }
}
