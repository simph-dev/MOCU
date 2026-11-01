using System.Diagnostics;


namespace Simph.Core.Time
{
    /// <summary>The real hardware counter. One instance per process is enough.</summary>
    public sealed class SystemClock : IClock
    {
        public Timestamp Now => new Timestamp(Stopwatch.GetTimestamp());
    }
}
