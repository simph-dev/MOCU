using System;
using System.Runtime.InteropServices;
using System.Threading;

using Simph.Core.Time;

namespace Simph.MoogProbe
{
    /// <summary>
    /// A fixed-rate loop on an absolute time grid: slot n is due at
    /// <c>start + n * interval</c>, computed from the start, never from the
    /// previous wake-up, so a late slot never delays the ones after it and the
    /// average rate stays exact. (MOCU-MoogControl schedules from the previous
    /// wake-up and so sends 988.8 packets/s on the lab PC instead of 1000.)
    ///
    /// A slot that is already past when we reach it is dropped rather than sent
    /// late, and counted: the set-point is a function of time, so a fresh pose
    /// beats a stale one, and the count tells us whether the host kept up.
    /// </summary>
    public sealed class PacedLoop
    {
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint milliseconds);

        private readonly IClock _clock;
        private readonly long _intervalNanoseconds;
        private readonly int _spinIterations;

        public PacedLoop(TimeSpan interval, IClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _intervalNanoseconds = Timestamp.ToNanoseconds(interval);

            if (_intervalNanoseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(interval), interval, "The interval must be positive.");

            // Short enough that a slot is never overshot by a whole spin, long
            // enough that the loop is not hammering the counter.
            _spinIterations = 64;
        }

        public long SlotsDropped { get; private set; }

        /// <summary>
        /// Runs <paramref name="body"/> once per slot until it returns false or
        /// <paramref name="cancellation"/> is signalled. The body is handed the
        /// slot index and the time the slot was due, not the time it actually
        /// woke up: the pose must be computed from the grid, or the jitter we are
        /// trying to measure gets written into the trajectory itself.
        /// </summary>
        public void Run(Func<long, Timestamp, bool> body, CancellationToken cancellation)
        {
            if (body is null) throw new ArgumentNullException(nameof(body));

            var thread = Thread.CurrentThread;
            var previousPriority = thread.Priority;

            // The slot itself is timed by spinning on the counter, so the 15.6 ms
            // scheduler tick does not limit it. These two only reduce how often
            // and how long the thread gets preempted.
            TimeBeginPeriod(1);
            thread.Priority = ThreadPriority.Highest;

            try
            {
                long start = _clock.Now.Nanoseconds;

                for (long slot = 0; !cancellation.IsCancellationRequested; slot++)
                {
                    long due = start + slot * _intervalNanoseconds;

                    if (_clock.Now.Nanoseconds >= due + _intervalNanoseconds)
                    {
                        // Already past the end of this slot's window. Skip it.
                        SlotsDropped++;
                        continue;
                    }

                    SpinUntil(due, cancellation);

                    if (cancellation.IsCancellationRequested)
                        break;

                    if (!body(slot, new Timestamp(due)))
                        break;
                }
            }
            finally
            {
                thread.Priority = previousPriority;
                TimeEndPeriod(1);
            }
        }

        private void SpinUntil(long dueNanoseconds, CancellationToken cancellation)
        {
            while (_clock.Now.Nanoseconds < dueNanoseconds)
            {
                if (cancellation.IsCancellationRequested)
                    return;

                Thread.SpinWait(_spinIterations);
            }
        }
    }
}
