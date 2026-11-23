using System;


namespace Simph.Core.Time
{
    /// <summary>
    /// A moment on the machine's monotonic clock, in nanoseconds since the
    /// clock's origin (on Windows, the performance counter, which starts at boot).
    ///
    /// Every process on the machine reads the same clock through
    /// <see cref="SystemClock"/>, so a packet sent by the daemon, a frame drawn by
    /// Unity and a button press can be laid on one axis without conversion.
    /// Nanoseconds rather than raw counter ticks, so a file written on one PC
    /// can be read without knowing that PC's counter frequency. A long holds
    /// about 292 years of them.
    ///
    /// Wall-clock time appears once per session, in the manifest, as one
    /// Timestamp paired with one UTC time.
    /// </summary>
    public readonly struct Timestamp : IEquatable<Timestamp>, IComparable<Timestamp>
    {
        private const long NanosecondsPerSecond = 1_000_000_000;
        private const long NanosecondsPerTimeSpanTick = 100;

        public long Nanoseconds { get; }

        public Timestamp(long nanoseconds) => Nanoseconds = nanoseconds;

        public static Timestamp Zero => new Timestamp(0);

        /// <summary>
        /// A counter reading in nanoseconds. Whole seconds and the remainder are
        /// converted separately: multiplying the raw value by 10^9 directly
        /// overflows a long after about 15 minutes of uptime at 10 MHz.
        /// </summary>
        public static Timestamp FromCounter(long counterValue, long counterFrequency)
        {
            if (counterFrequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(counterFrequency), counterFrequency, "The frequency must be positive.");

            long seconds = counterValue / counterFrequency;
            long remainder = counterValue % counterFrequency;

            return new Timestamp(seconds * NanosecondsPerSecond + remainder * NanosecondsPerSecond / counterFrequency);
        }

        /// <summary>Seconds since the clock's origin, for reports and logs.</summary>
        public double Seconds => (double)Nanoseconds / NanosecondsPerSecond;

        /// <summary>TimeSpan counts in 100 ns ticks; this is the one place that converts them.</summary>
        public static long ToNanoseconds(TimeSpan duration) => duration.Ticks * NanosecondsPerTimeSpanTick;


        public static Timestamp operator +(Timestamp t, TimeSpan d) => new Timestamp(t.Nanoseconds + ToNanoseconds(d));
        public static Timestamp operator -(Timestamp t, TimeSpan d) => new Timestamp(t.Nanoseconds - ToNanoseconds(d));

        /// <summary>How long from <paramref name="b"/> until <paramref name="a"/>, to the nearest 100 ns below.</summary>
        public static TimeSpan operator -(Timestamp a, Timestamp b) =>
            TimeSpan.FromTicks((a.Nanoseconds - b.Nanoseconds) / NanosecondsPerTimeSpanTick);

        public static bool operator <(Timestamp a, Timestamp b) => a.Nanoseconds < b.Nanoseconds;
        public static bool operator >(Timestamp a, Timestamp b) => a.Nanoseconds > b.Nanoseconds;
        public static bool operator <=(Timestamp a, Timestamp b) => a.Nanoseconds <= b.Nanoseconds;
        public static bool operator >=(Timestamp a, Timestamp b) => a.Nanoseconds >= b.Nanoseconds;
        public static bool operator ==(Timestamp a, Timestamp b) => a.Nanoseconds == b.Nanoseconds;
        public static bool operator !=(Timestamp a, Timestamp b) => a.Nanoseconds != b.Nanoseconds;


        public bool Equals(Timestamp other) => Nanoseconds == other.Nanoseconds;
        public override bool Equals(object obj) => obj is Timestamp other && Equals(other);
        public override int GetHashCode() => Nanoseconds.GetHashCode();
        public int CompareTo(Timestamp other) => Nanoseconds.CompareTo(other.Nanoseconds);
        public override string ToString() => $"{Seconds:F6}s";
    }
}
