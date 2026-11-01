using System;
using System.Diagnostics;


namespace Simph.Core.Time
{
    /// <summary>
    /// A moment on the one monotonic clock the whole system shares: the raw
    /// QueryPerformanceCounter value, whose frequency is Stopwatch.Frequency.
    ///
    /// Everything that gets a time - a sent packet, a received frame, a button
    /// press, the start of a movement - is stamped with one of these, so that
    /// records from the daemon, from Unity and from a test can be laid on one
    /// axis without any conversion. DateTime is not used for this: on Windows
    /// its resolution is coarser than a single tick of the 1 kHz motion loop,
    /// and it is not monotonic, so two consecutive reads can come back equal or
    /// even go backwards when the system clock is adjusted.
    ///
    /// Wall-clock time appears once per session, in the manifest, as a recorded
    /// pairing of one Timestamp with one UTC DateTimeOffset.
    /// </summary>
    public readonly struct Timestamp : IEquatable<Timestamp>, IComparable<Timestamp>
    {
        /// <summary>Raw counter value; Stopwatch.Frequency of these per second.</summary>
        public long Ticks { get; }

        public Timestamp(long ticks) => Ticks = ticks;

        public static Timestamp Zero => new Timestamp(0);

        /// <summary>Counter ticks in one second, read once from the hardware.</summary>
        public static long TicksPerSecond => Stopwatch.Frequency;


        public static Timestamp operator +(Timestamp t, TimeSpan d) => new Timestamp(t.Ticks + ToTicks(d));
        public static Timestamp operator -(Timestamp t, TimeSpan d) => new Timestamp(t.Ticks - ToTicks(d));

        /// <summary>How long from <paramref name="b"/> until <paramref name="a"/>.</summary>
        public static TimeSpan operator -(Timestamp a, Timestamp b) => ToTimeSpan(a.Ticks - b.Ticks);

        public static bool operator <(Timestamp a, Timestamp b) => a.Ticks < b.Ticks;
        public static bool operator >(Timestamp a, Timestamp b) => a.Ticks > b.Ticks;
        public static bool operator <=(Timestamp a, Timestamp b) => a.Ticks <= b.Ticks;
        public static bool operator >=(Timestamp a, Timestamp b) => a.Ticks >= b.Ticks;
        public static bool operator ==(Timestamp a, Timestamp b) => a.Ticks == b.Ticks;
        public static bool operator !=(Timestamp a, Timestamp b) => a.Ticks != b.Ticks;


        /// <summary>
        /// Counter ticks for a duration. Done in this one place because mixing
        /// counter ticks with TimeSpan.Ticks is an easy and silent mistake: they
        /// happen to be equal on most current Windows machines, where the counter
        /// runs at 10 MHz, and differ on anything else.
        /// </summary>
        public static long ToTicks(TimeSpan duration) =>
            (long)Math.Round(duration.TotalSeconds * TicksPerSecond);

        public static TimeSpan ToTimeSpan(long ticks) =>
            TimeSpan.FromSeconds((double)ticks / TicksPerSecond);


        public bool Equals(Timestamp other) => Ticks == other.Ticks;
        public override bool Equals(object obj) => obj is Timestamp other && Equals(other);
        public override int GetHashCode() => Ticks.GetHashCode();
        public int CompareTo(Timestamp other) => Ticks.CompareTo(other.Ticks);
        public override string ToString() => $"{(double)Ticks / TicksPerSecond:F6}s";
    }
}
