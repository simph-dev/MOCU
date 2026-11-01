using System;


namespace Simph.Core.Time
{
    /// <summary>
    /// A clock that only moves when told to, for tests and for replaying a
    /// recorded session.
    /// </summary>
    public sealed class ManualClock : IClock
    {
        public Timestamp Now { get; private set; }

        public ManualClock(Timestamp start) => Now = start;
        public ManualClock() : this(Timestamp.Zero) { }

        public void Advance(TimeSpan by) => Now = Now + by;
        public void Set(Timestamp to) => Now = to;
    }
}
