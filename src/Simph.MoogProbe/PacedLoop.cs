using System;
using System.Runtime.InteropServices;
using System.Threading;

using Simph.Core.Time;

namespace Simph.MoogProbe
{
    /// <summary>
    /// A fixed-rate loop on an absolute time grid: slot n is due at
    /// <c>start + n * interval</c>, computed from the start, never from the
    /// previous wake-up.
    ///
    /// This is the one real difference from MOCU-MoogControl's IntervalExecutor,
    /// and it is the difference that matters. That one does
    ///
    ///     QueryPerformanceCounter(out now);
    ///     target = now + _interval.Ticks;
    ///
    /// on every iteration, so each period is measured from wherever the previous
    /// one happened to finish. Any overshoot is absorbed into the next period
    /// instead of being corrected. Its spin granularity overshoots by about 11 us
    /// a tick, so that loop run verbatim on the lab PC sends 988.8 packets/s, not
    /// 1000: very steady, but 1.1% slow for as long as it runs.
    ///
    /// It also mixes two tick units: <c>now</c> comes from the performance
    /// counter while <c>_interval.Ticks</c> is TimeSpan's 100 ns unit. They agree
    /// only because the counter on this machine happens to run at 10 MHz. On
    /// hardware where it runs at 3.58 MHz or 1 MHz the loop silently runs 3x or
    /// 10x too fast. Timestamp.ToTicks exists to make that conversion impossible
    /// to forget, and is used here.
    ///
    /// A slot that is already past when we reach it is dropped rather than sent
    /// late, and counted. For a position command stream that is the right choice:
    /// the set-point is a function of time, so a fresh pose beats a stale one.
    /// The count is what tells us afterwards whether the host kept up at all.
    /// </summary>
    public sealed class PacedLoop
    {
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint milliseconds);

        private readonly long _intervalTicks;
        private readonly int _spinIterations;

        public PacedLoop(TimeSpan interval)
        {
            _intervalTicks = Timestamp.ToTicks(interval);

            if (_intervalTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(interval), interval,
                    "The interval must be longer than one tick of the performance counter.");

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

            // The slot itself is timed by spinning on the performance counter, so
            // the 15.6 ms scheduler tick does not limit it. These two only reduce
            // how often and how long the thread gets preempted. This is less
            // isolation than MOCU-MoogControl's SystemOptimizer gives its loop
            // (High priority class and half the cores to itself), and the tails
            // of the gap distribution show it.
            TimeBeginPeriod(1);
            thread.Priority = ThreadPriority.Highest;

            try
            {
                long start = Stopwatch64();

                for (long slot = 0; !cancellation.IsCancellationRequested; slot++)
                {
                    long due = start + slot * _intervalTicks;

                    if (Stopwatch64() >= due + _intervalTicks)
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

        private void SpinUntil(long due, CancellationToken cancellation)
        {
            while (Stopwatch64() < due)
            {
                if (cancellation.IsCancellationRequested)
                    return;

                Thread.SpinWait(_spinIterations);
            }
        }

        private static long Stopwatch64() => System.Diagnostics.Stopwatch.GetTimestamp();
    }
}
