using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Simph.MoogProbe
{
    /// <summary>A position in the floor plane at a moment of a stimulus.</summary>
    public readonly struct TracePoint
    {
        public TracePoint(double seconds, double surge, double sway)
        {
            Seconds = seconds;
            Surge = surge;
            Sway = sway;
        }

        public double Seconds { get; }
        public double Surge { get; }
        public double Sway { get; }
    }


    /// <summary>
    /// One stimulus as the experiment logged it. Every time is in seconds from the
    /// moment the platform was commanded, or from the moment the stars were shown
    /// on a trial without the platform, so all of it can be laid on one axis.
    /// </summary>
    public sealed class StimulusTrace
    {
        public StimulusTrace(int trial, bool hasPlatform, double? starsShown,
            IReadOnlyList<TracePoint> command, IReadOnlyList<TracePoint> platform,
            IReadOnlyList<TracePoint> rig, IReadOnlyList<TracePoint> eye,
            IReadOnlyList<double> motionToPhoton)
        {
            Trial = trial;
            HasPlatform = hasPlatform;
            StarsShown = starsShown;
            Command = command;
            Platform = platform;
            Rig = rig;
            Eye = eye;
            MotionToPhoton = motionToPhoton;
        }

        public int Trial { get; }
        public bool HasPlatform { get; }

        /// <summary>When the stars were switched on, if the trial had any.</summary>
        public double? StarsShown { get; }

        /// <summary>What went out to the platform.</summary>
        public IReadOnlyList<TracePoint> Command { get; }

        /// <summary>Where the platform reported itself.</summary>
        public IReadOnlyList<TracePoint> Platform { get; }

        /// <summary>The camera rig in each frame: what the experiment asked for.</summary>
        public IReadOnlyList<TracePoint> Rig { get; }

        /// <summary>The eye in each frame: the rig plus what the headset tracked.</summary>
        public IReadOnlyList<TracePoint> Eye { get; }

        /// <summary>The runtime's latency in seconds, where it reported one.</summary>
        public IReadOnlyList<double> MotionToPhoton { get; }
    }


    /// <summary>What one stimulus shows, at each of <see cref="MovementReport.Levels"/>.</summary>
    public sealed class StimulusTiming
    {
        public StimulusTiming(int trial, bool hasPlatform, double[] command, double[] platform,
            double[] rig, double[] eye, double firstFrameWithStars, double platformAtStars,
            double peakTrackedMillimetres, double platformTravelMillimetres,
            IReadOnlyList<double> frameGaps, IReadOnlyList<double> motionToPhoton)
        {
            Trial = trial;
            HasPlatform = hasPlatform;
            Command = command;
            Platform = platform;
            Rig = rig;
            Eye = eye;
            FirstFrameWithStars = firstFrameWithStars;
            PlatformAtStars = platformAtStars;
            PeakTrackedMillimetres = peakTrackedMillimetres;
            PlatformTravelMillimetres = platformTravelMillimetres;
            FrameGaps = frameGaps;
            MotionToPhoton = motionToPhoton;
        }

        public int Trial { get; }
        public bool HasPlatform { get; }

        public double[] Command { get; }
        public double[] Platform { get; }
        public double[] Rig { get; }
        public double[] Eye { get; }

        /// <summary>In ms; NaN if the trial showed no stars.</summary>
        public double FirstFrameWithStars { get; }

        /// <summary>How far the platform had gone by that frame, in mm.</summary>
        public double PlatformAtStars { get; }

        /// <summary>
        /// The most the headset's tracking moved the eye off the rig. Close to the
        /// platform's own travel means the platform's motion is in the camera too.
        /// </summary>
        public double PeakTrackedMillimetres { get; }

        public double PlatformTravelMillimetres { get; }

        /// <summary>Between consecutive frames, in ms.</summary>
        public IReadOnlyList<double> FrameGaps { get; }

        /// <summary>The runtime's latency in ms, where it reported one.</summary>
        public IReadOnlyList<double> MotionToPhoton { get; }
    }


    /// <summary>
    /// Lays what the eyes were given and what the platform did on one clock. Reads
    /// the folder StimulusTimingLog writes in the experiment. A frame is stamped
    /// before it is rendered, so the visual times here are the earliest the picture
    /// could have been seen: the true lag of the picture is this one plus the time
    /// to the display.
    /// </summary>
    public static class StimulusTimingReport
    {
        private const double TicksPerSecond = 1e7;

        /// <summary>Any row further than this from its trial's marks is on another clock.</summary>
        private const double SaneSeconds = 60;

        /// <summary>A path shorter than this has not moved: its noise has no progress.</summary>
        private const double SmallestPath = 0.001;

        public static StimulusTiming Measure(StimulusTrace trace)
        {
            double[] frames = trace.Rig.Select(p => p.Seconds).ToArray();

            double firstFrame = double.NaN, platformThen = double.NaN;

            if (trace.StarsShown is double shown)
            {
                int after = Array.FindIndex(frames, t => t > shown);

                if (after >= 0)
                {
                    firstFrame = frames[after] * 1000.0;
                    platformThen = Travelled(trace.Platform, frames[after]) * 1000.0;
                }
            }

            double peakTracked = 0;

            for (int n = 0; n < Math.Min(trace.Rig.Count, trace.Eye.Count); n++)
            {
                double surge = trace.Eye[n].Surge - trace.Rig[n].Surge - (trace.Eye[0].Surge - trace.Rig[0].Surge);
                double sway = trace.Eye[n].Sway - trace.Rig[n].Sway - (trace.Eye[0].Sway - trace.Rig[0].Sway);

                peakTracked = Math.Max(peakTracked, Math.Sqrt(surge * surge + sway * sway));
            }

            return new StimulusTiming(trace.Trial, trace.HasPlatform,
                Crossings(trace.Command), Crossings(trace.Platform),
                Crossings(trace.Rig), Crossings(trace.Eye),
                firstFrame, platformThen,
                peakTracked * 1000.0, PathLength(trace.Platform) * 1000.0,
                Gaps(frames),
                trace.MotionToPhoton.Where(s => s > 0).Select(s => s * 1000.0).ToArray());
        }

        public static string Describe(IReadOnlyList<StimulusTiming> trials)
        {
            var text = new StringBuilder();

            var withPlatform = trials.Where(t => t.HasPlatform).ToList();
            var visualOnly = trials.Where(t => !t.HasPlatform).ToList();

            text.AppendLine($"stimulus timing: {withPlatform.Count} trials with the platform, {visualOnly.Count} without");
            text.AppendLine();

            if (withPlatform.Count > 0)
            {
                text.AppendLine($"with the platform, ms from the moment it was commanded, median of the trials");
                text.AppendLine($"  {"",-24}{Header()}");
                text.AppendLine($"  {"command",-24}{Row(withPlatform, t => t.Command)}");
                text.AppendLine($"  {"platform",-24}{Row(withPlatform, t => t.Platform)}");
                text.AppendLine($"  {"camera, as asked",-24}{Row(withPlatform, t => t.Rig)}");
                text.AppendLine($"  {"eye, with the tracking",-24}{Row(withPlatform, t => t.Eye)}");
                text.AppendLine();

                var late = Lateness(withPlatform, t => t.Rig);

                text.AppendLine("  how far the camera is behind the platform, ms (+ = the picture is late)");
                text.AppendLine($"  {"median",-24}{Signed(late.Select(Median))}");
                text.AppendLine($"  {"lowest",-24}{Signed(late.Select(Low))}");
                text.AppendLine($"  {"highest",-24}{Signed(late.Select(High))}");
                text.AppendLine();

                var stars = withPlatform.Where(t => !double.IsNaN(t.FirstFrameWithStars)).ToList();

                if (stars.Count > 0)
                {
                    text.AppendLine($"  stars first drawn {Median(stars.Select(t => t.FirstFrameWithStars)):F0} ms after the command, " +
                        $"with the platform {Median(stars.Select(t => t.PlatformAtStars)):F1} mm on its way");
                }

                text.AppendLine($"  head tracking moved the eye {Median(withPlatform.Select(t => t.PeakTrackedMillimetres)):F1} mm at most, " +
                    $"over a platform travel of {Median(withPlatform.Select(t => t.PlatformTravelMillimetres)):F1} mm");
                text.AppendLine();
            }

            if (visualOnly.Count > 0)
            {
                text.AppendLine("without the platform, ms from the moment the stars were shown, median of the trials");
                text.AppendLine($"  {"",-24}{Header()}");
                text.AppendLine($"  {"camera, as asked",-24}{Row(visualOnly, t => t.Rig)}");
                text.AppendLine($"  {"eye, with the tracking",-24}{Row(visualOnly, t => t.Eye)}");
                text.AppendLine($"  head tracking moved the eye {Median(visualOnly.Select(t => t.PeakTrackedMillimetres)):F1} mm at most");
                text.AppendLine();
            }

            var gaps = trials.SelectMany(t => t.FrameGaps).ToList();

            if (gaps.Count > 0)
            {
                double typical = Median(gaps);

                text.AppendLine($"frames: {1000.0 / typical:F1} Hz, gap median {typical:F1} ms, longest {gaps.Max():F1} ms, " +
                    $"{gaps.Count(g => g > 1.5 * typical)} of {gaps.Count} over 1.5 frames");
            }

            var latency = trials.SelectMany(t => t.MotionToPhoton).ToList();

            text.AppendLine(latency.Count > 0
                ? $"motion to photon as the runtime reports it: {Median(latency):F1} ms, add it to the camera's lateness above"
                : "motion to photon: not reported, so the picture is later than the above by an unknown time to the display");

            return text.ToString();
        }

        public static IReadOnlyList<StimulusTrace> ReadFolder(string folder)
        {
            const string suffix = "_marks.csv";

            var traces = new List<StimulusTrace>();

            foreach (string marksFile in Directory.GetFiles(folder, "trial_*" + suffix).OrderBy(f => f, StringComparer.Ordinal))
            {
                string stem = marksFile.Substring(0, marksFile.Length - suffix.Length);
                string framesFile = stem + "_frames.csv";
                string moogFile = stem + "_moog.csv";

                if (!File.Exists(framesFile))
                    continue;

                int trial = int.Parse(Path.GetFileName(stem).Substring("trial_".Length), CultureInfo.InvariantCulture);

                traces.Add(Parse(trial, File.ReadAllLines(marksFile), File.ReadAllLines(framesFile),
                    File.Exists(moogFile) ? File.ReadAllLines(moogFile) : Array.Empty<string>()));
            }

            return traces;
        }

        /// <param name="trial">The trial's number.</param>
        /// <param name="marks">The lines of its marks file, header first.</param>
        /// <param name="frames">The lines of its frames file, header first.</param>
        /// <param name="moog">The lines of its moog file, or none.</param>
        public static StimulusTrace Parse(int trial, IReadOnlyList<string> marks,
            IReadOnlyList<string> frames, IReadOnlyList<string> moog)
        {
            var moments = new Dictionary<string, long>();

            foreach (string line in marks.Skip(1))
            {
                string[] cells = line.Split(',');
                moments[cells[0]] = long.Parse(cells[1], CultureInfo.InvariantCulture);
            }

            bool hasPlatform = moments.ContainsKey("moog");

            if (!hasPlatform && !moments.ContainsKey("visual") && !moments.ContainsKey("start"))
                throw new InvalidDataException($"Trial {trial} has none of the marks it needs.");

            long zero = hasPlatform ? moments["moog"]
                : moments.TryGetValue("visual", out long visual) ? visual
                : moments["start"];

            double Seconds(long ticks)
            {
                double seconds = (ticks - zero) / TicksPerSecond;

                if (Math.Abs(seconds) > SaneSeconds)
                    throw new InvalidDataException($"Trial {trial}: a row is {seconds:F0} s from its marks, so the clocks differ.");

                return seconds;
            }

            var rig = new List<TracePoint>();
            var eye = new List<TracePoint>();
            var latency = new List<double>();

            if (frames.Count > 0)
            {
                var column = Columns(frames[0]);

                foreach (string line in frames.Skip(1))
                {
                    string[] cells = line.Split(',');
                    double when = Seconds(long.Parse(cells[column["utc_ticks"]], CultureInfo.InvariantCulture));

                    rig.Add(new TracePoint(when, Number(cells[column["rig_surge"]]), Number(cells[column["rig_sway"]])));
                    eye.Add(new TracePoint(when, Number(cells[column["eye_surge"]]), Number(cells[column["eye_sway"]])));
                    latency.Add(Number(cells[column["motion_to_photon_s"]]));
                }
            }

            var command = new List<TracePoint>();
            var platform = new List<TracePoint>();

            if (moog.Count > 0)
            {
                var column = Columns(moog[0]);

                foreach (string line in moog.Skip(1))
                {
                    string[] cells = line.Split(',');
                    double when = Seconds(long.Parse(cells[column["utc_ticks"]], CultureInfo.InvariantCulture));

                    var point = new TracePoint(when, Number(cells[column["surge"]]), Number(cells[column["sway"]]));

                    (cells[column["kind"]] == "command" ? command : platform).Add(point);
                }
            }

            double? starsShown = moments.TryGetValue("visual", out long shown) ? Seconds(shown) : (double?)null;

            return new StimulusTrace(trial, hasPlatform, starsShown, command, platform, rig, eye, latency);
        }

        /// <summary>
        /// Prints the report for a folder of a session.
        /// </summary>
        public static int Run(string folder)
        {
            try
            {
                var trials = ReadFolder(folder).Select(Measure).ToList();

                if (trials.Count == 0)
                {
                    Console.Error.WriteLine($"No trials in {folder}.");
                    return 1;
                }

                Console.WriteLine(Describe(trials));
                return 0;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        // ...........................................

        /// <summary>
        /// When a path reached each level of its length. The far end is the point
        /// furthest from the start and not the last row, because a recording can run
        /// on into the return, and each level counts at its first crossing.
        /// </summary>
        private static double[] Crossings(IReadOnlyList<TracePoint> path)
        {
            double[] none = MovementReport.Levels.Select(_ => double.NaN).ToArray();

            if (path.Count < 2)
                return none;

            var start = path[0];
            var end = path.OrderByDescending(p => Distance(p, start)).First();

            double dx = end.Surge - start.Surge, dy = end.Sway - start.Sway;
            double lengthSquared = dx * dx + dy * dy;

            if (lengthSquared < SmallestPath * SmallestPath)
                return none;

            double[] progress = path
                .Select(p => ((p.Surge - start.Surge) * dx + (p.Sway - start.Sway) * dy) / lengthSquared)
                .ToArray();

            double[] seconds = path.Select(p => p.Seconds).ToArray();

            return MovementReport.Levels.Select(level => MovementReport.Crossing(progress, seconds, level)).ToArray();
        }

        private static double PathLength(IReadOnlyList<TracePoint> path) =>
            path.Count == 0 ? 0 : path.Max(p => Distance(p, path[0]));

        /// <summary>How far a path had got from its start by a moment.</summary>
        private static double Travelled(IReadOnlyList<TracePoint> path, double seconds)
        {
            if (path.Count == 0)
                return double.NaN;

            var reached = path.TakeWhile(p => p.Seconds <= seconds).DefaultIfEmpty(path[0]).Last();

            return Distance(reached, path[0]);
        }

        private static double Distance(TracePoint a, TracePoint b)
        {
            double surge = a.Surge - b.Surge, sway = a.Sway - b.Sway;
            return Math.Sqrt(surge * surge + sway * sway);
        }

        private static double[] Gaps(double[] seconds)
        {
            var gaps = new double[Math.Max(0, seconds.Length - 1)];

            for (int n = 0; n < gaps.Length; n++)
                gaps[n] = (seconds[n + 1] - seconds[n]) * 1000.0;

            return gaps;
        }

        /// <summary>For each level, the camera's time less the platform's, one value per trial.</summary>
        private static List<List<double>> Lateness(IReadOnlyList<StimulusTiming> trials, Func<StimulusTiming, double[]> camera)
        {
            return Enumerable.Range(0, MovementReport.Levels.Length)
                .Select(level => trials
                    .Select(t => camera(t)[level] - t.Platform[level])
                    .Where(value => !double.IsNaN(value))
                    .ToList())
                .ToList();
        }

        private static string Header() =>
            string.Join("", MovementReport.Levels.Select(level => $"{level * 100,7:F0}%"));

        private static string Row(IReadOnlyList<StimulusTiming> trials, Func<StimulusTiming, double[]> times) =>
            string.Join("", Enumerable.Range(0, MovementReport.Levels.Length)
                .Select(level => $"{Median(trials.Select(t => times(t)[level])),8:F0}"));

        private static string Signed(IEnumerable<double> values) =>
            string.Join("", values.Select(value => double.IsNaN(value) ? $"{"-",8}" : $"{value,8:+0;-0;0}"));

        private static double Median(IEnumerable<double> values)
        {
            double[] sorted = values.Where(value => !double.IsNaN(value)).OrderBy(value => value).ToArray();

            if (sorted.Length == 0)
                return double.NaN;

            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }

        private static double Low(IReadOnlyCollection<double> values) => values.Count == 0 ? double.NaN : values.Min();

        private static double High(IReadOnlyCollection<double> values) => values.Count == 0 ? double.NaN : values.Max();

        private static Dictionary<string, int> Columns(string header)
        {
            string[] names = header.Split(',');
            return Enumerable.Range(0, names.Length).ToDictionary(n => names[n].Trim(), n => n);
        }

        private static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);
    }
}
