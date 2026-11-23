using System;
using System.Runtime.InteropServices;


namespace Simph.Core.Time
{
    /// <summary>
    /// The machine's performance counter, read directly, in every process: Unity,
    /// the daemon, MoogProbe.
    ///
    /// Not through Stopwatch. Measured on 2026-11-23 in the Unity 6000.6 Editor:
    /// Unity's Mono counts Stopwatch from the moment the process started
    /// (1359 s), while the counter itself counted from boot (27 416 s). In .NET,
    /// Stopwatch is the raw counter. A t0 sent from Unity to the daemon through
    /// Stopwatch would have been hours off, and differently after every restart.
    ///
    /// Windows only for now. Moving to Linux would read CLOCK_MONOTONIC here.
    /// </summary>
    public sealed class SystemClock : IClock
    {
        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long value);

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long frequency);

        public SystemClock()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("SystemClock reads the Windows performance counter.");

            QueryPerformanceFrequency(out long frequency);
            CounterFrequency = frequency;
        }

        /// <summary>Counter ticks per second on this machine, fixed at boot. For logs.</summary>
        public long CounterFrequency { get; }

        public Timestamp Now
        {
            get
            {
                QueryPerformanceCounter(out long value);
                return Timestamp.FromCounter(value, CounterFrequency);
            }
        }
    }
}
