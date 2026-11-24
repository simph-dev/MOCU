using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Simph.Core.Geometry;
using Simph.Core.Motion;
using Simph.Core.Motion.Compensation;
using Simph.Core.Motion.Limits;
using Simph.Core.Time;

using Simph.MoogProbe.Protocol;

namespace Simph.MoogProbe
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
            {
                Console.WriteLine(ProbeOptions.Usage);
                return 0;
            }

            if (args.Length == 2 && args[0] == "timing")
                return StimulusTimingReport.Run(args[1]);

            if (!ProbeOptions.TryParse(args, out var options, out string? error))
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine();
                Console.Error.WriteLine(ProbeOptions.Usage);
                return 2;
            }

            try
            {
                options.UseSetup(RigSetup.Load(options.SetupPath));
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is FormatException ||
                                              exception is System.Text.Json.JsonException || exception is ArgumentException)
            {
                Console.Error.WriteLine($"Cannot read the rig setup {options.SetupPath}: {exception.Message}");
                return 2;
            }

            Console.WriteLine();
            Console.WriteLine(options.Header);
            var clock = new SystemClock();
            Console.WriteLine($"performance counter runs at {clock.CounterFrequency} Hz");
            Console.WriteLine();

            var schedule = BuildSchedule(options, out MotionPlan? outbound);

            if (outbound != null && !Approve(outbound, options))
                return 2;

            if (options.MovesThePlatform && !Consent(options))
                return 1;

            return Run(options, schedule, clock);
        }


        // ---------------------------------------------------------------- plan

        private static List<Segment> BuildSchedule(ProbeOptions options, out MotionPlan? outbound)
        {
            outbound = null;

            var neutral = options.Neutral;
            var schedule = new List<Segment>();

            // While holding, surge may carry a slow sine so that consecutive
            // set-points differ and the recorder shows which ones were repeated
            // or skipped, rather than only how many rows there were.
            double amplitude = options.DitherMetres;
            double omega = 2.0 * Math.PI * options.DitherHz;

            Func<TimeSpan, Pose6> hold = amplitude <= 0.0
                ? (Func<TimeSpan, Pose6>)(_ => neutral)
                : elapsed => neutral + Pose6.Translation(
                    surge: amplitude * Math.Sin(omega * elapsed.TotalSeconds),
                    sway: 0, heave: 0);

            if (options.Phase == Phase.Listen)
            {
                schedule.Add(new Segment("listen",
                    TimeSpan.FromSeconds(options.Seconds), hold));
                return schedule;
            }

            if (options.Phase == Phase.Hold)
            {
                schedule.Add(new Segment("hold",
                    TimeSpan.FromSeconds(options.Seconds), hold));
                return schedule;
            }

            schedule.Add(new Segment("settle",
                TimeSpan.FromSeconds(options.SettleSeconds), _ => neutral));

            var profile = new GaussianCdfProfile(options.Sigmas);
            var commanded = TimeSpan.FromSeconds(options.CommandedDuration);
            var target = options.Target;

            var away = new MotionPlan(neutral, target, commanded, Timestamp.Zero, profile);
            var back = new MotionPlan(target, neutral, commanded, Timestamp.Zero, profile);
            outbound = away;

            for (int cycle = 0; cycle < options.Repeats; cycle++)
            {
                schedule.Add(new Segment("out", commanded, Evaluate(away, options), away));
                schedule.Add(new Segment("dwell", TimeSpan.FromSeconds(options.DwellSeconds), _ => target));
                schedule.Add(new Segment("back", commanded, Evaluate(back, options), back));
                schedule.Add(new Segment("dwell", TimeSpan.FromSeconds(options.DwellSeconds), _ => neutral));
            }

            return schedule;
        }

        private static Func<TimeSpan, Pose6> Evaluate(MotionPlan plan, ProbeOptions options)
        {
            if (!options.Precompensate)
                return elapsed => plan.Evaluate(Timestamp.Zero + elapsed);

            var motion = Compensated(plan, options);

            return elapsed => motion.Command(Timestamp.Zero + elapsed);
        }

        private static CompensatedMotion Compensated(MotionPlan plan, ProbeOptions options) =>
            new CompensatedMotion(plan, options.Model);


        // ------------------------------------------------------------- consent

        /// <summary>
        /// Runs what will be sent past the envelope before anything is sent: the
        /// plan, or with --precompensate the compensated command as well. A
        /// rejection stops the run. Warnings are printed and do not, since a
        /// warning means close to a limit, not over it.
        /// </summary>
        private static bool Approve(MotionPlan plan, ProbeOptions options)
        {
            var validator = new MotionValidator(options.Envelope);

            Console.WriteLine(plan.ToString());
            Console.WriteLine($"peak speed        {plan.PeakSpeed}");
            Console.WriteLine($"peak acceleration {plan.PeakAcceleration}");
            Console.WriteLine();

            ValidationResult result;

            if (options.Precompensate)
            {
                var motion = Compensated(plan, options);
                var demand = motion.Measure();

                Console.WriteLine($"filter output speed {demand.PeakSpeed}");
                Console.WriteLine($"filter output accel {demand.PeakAcceleration}");
                Console.WriteLine($"command lowest      {demand.LowestCommand}");
                Console.WriteLine($"command highest     {demand.HighestCommand}");
                Console.WriteLine();

                result = validator.Validate(motion);
            }
            else
            {
                result = validator.Validate(plan);
            }

            Console.WriteLine(result.Describe());
            Console.WriteLine();

            if (!result.Accepted)
            {
                Console.Error.WriteLine("The movement is outside what the platform can do. Nothing was sent.");
                return false;
            }

            return true;
        }

        private static bool Consent(ProbeOptions options)
        {
            if (!options.Armed)
            {
                Console.Error.WriteLine(
                    $"Phase {options.Phase} engages the platform and moves it. Re-run with --arm " +
                    "once the area is clear and nobody is seated.");
                return false;
            }

            if (options.AssumeYes)
                return true;

            Console.WriteLine("This will engage the platform and move it.");
            Console.Write("Type MOVE to continue: ");

            string? answer = Console.ReadLine();

            if (!string.Equals(answer?.Trim(), "MOVE", StringComparison.Ordinal))
            {
                Console.WriteLine("Not confirmed. Nothing was sent.");
                return false;
            }

            return true;
        }


        // ----------------------------------------------------------------- run

        private static int Run(ProbeOptions options, List<Segment> schedule, IClock clock)
        {
            using var dropStream = new CancellationTokenSource();
            int stopRequests = 0;

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Console.WriteLine();

                if (Interlocked.Increment(ref stopRequests) == 1)
                {
                    Console.WriteLine("Stopping: the platform is being asked to disengage. " +
                        "Press Ctrl+C again to drop the stream and let the MBC time out.");
                }
                else
                {
                    dropStream.Cancel();
                }
            };

            // Console output takes tens of microseconds and sometimes much more,
            // so nothing on the send path may do it directly. Reports go through
            // this queue to a thread of their own.
            using var messages = new BlockingCollection<string>();
            var printer = new Thread(() =>
            {
                foreach (var message in messages.GetConsumingEnumerable())
                    Console.WriteLine(message);
            })
            {
                IsBackground = true,
                Name = "moog-probe-console"
            };
            printer.Start();

            using var link = new MachineLink(options.Host, options.Mbc, clock);

            var loop = new PacedLoop(options.Interval, clock);
            Timestamp due = default;
            RunSequencer? sequencer = null;

            sequencer = new RunSequencer(
                engages: options.Phase != Phase.Listen,
                neutral: options.Neutral,
                schedule: schedule,
                engageLimit: TimeSpan.FromSeconds(30),
                parkLimit: TimeSpan.FromSeconds(60),
                report: what => messages.Add(
                    $"  [{sequencer!.ScheduleTime(due).TotalSeconds:F3}s] {what}" +
                    $"   state {link.LatestState?.ToString() ?? "no reply yet"}, responses {link.ResponseCount}"));

            loop.Run((slot, at) =>
            {
                due = at;

                var step = sequencer.Next(link.LatestState, at, Volatile.Read(ref stopRequests) > 0);

                if (step.Finished)
                    return false;

                link.Send(slot, at, step.Command, options.StatusWord, step.Pose, options.CopiesPerCommand);
                return true;
            }, dropStream.Token);

            // Give the last few replies time to arrive before the socket closes.
            Thread.Sleep(50);

            messages.CompleteAdding();
            printer.Join();

            var sent = link.Sent.ToArray();
            var received = link.Received.ToArray();

            var log = new ProbeLog(options.LogRoot, options.Label);

            log.Write(sent, received, loop.SlotsDropped, options.Header,
                link.DatagramCount, link.FirstUnparsed);

            log.Append(Report(schedule, sequencer.ScheduleStarted, received));

            return received.Length == 0 ? 1 : 0;
        }

        /// <summary>
        /// Measures each movement of the schedule from the replies that arrived
        /// between its start and the start of the next one, so the slow tail of
        /// the platform's arrival is included.
        /// </summary>
        private static string Report(List<Segment> schedule, Timestamp started, ReceivedRecord[] received)
        {
            var movements = new List<MovementMeasurement>();
            var from = started;

            for (int i = 0; i < schedule.Count; i++)
            {
                var segment = schedule[i];
                var stop = from + segment.Duration;
                var nextMovement = stop;

                for (int j = i + 1; j < schedule.Count && schedule[j].Plan == null; j++)
                    nextMovement += schedule[j].Duration;

                if (segment.Plan != null)
                {
                    var start = from;
                    var end = nextMovement;
                    var window = received.Where(r => r.HasFeedback && r.Received >= start && r.Received < end).ToArray();

                    if (window.Length > 100)
                    {
                        movements.Add(MovementReport.Measure(segment.Name, segment.Plan,
                            window.Select(r => (r.Received - start).TotalSeconds).ToArray(),
                            window.Select(r => r.Feedback).ToArray()));
                    }
                }

                from = stop;
            }

            return movements.Count == 0 ? string.Empty : MovementReport.Describe(movements);
        }
    }
}
