using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Simph.Core.Geometry;
using Simph.Core.Motion;

namespace Simph.MoogProbe
{
    /// <summary>What the platform really did during one movement.</summary>
    public sealed class MovementMeasurement
    {
        public MovementMeasurement(string name, double bendMillimetres, double peakSpeed, double endErrorMillimetres,
            IReadOnlyList<double> planMilliseconds, IReadOnlyList<double> platformMilliseconds)
        {
            Name = name;
            BendMillimetres = bendMillimetres;
            PeakSpeed = peakSpeed;
            EndErrorMillimetres = endErrorMillimetres;
            PlanMilliseconds = planMilliseconds;
            PlatformMilliseconds = platformMilliseconds;
        }

        public string Name { get; }

        /// <summary>Largest distance of the platform from the straight line.</summary>
        public double BendMillimetres { get; }

        public double PeakSpeed { get; }

        /// <summary>How far from the end point it finished.</summary>
        public double EndErrorMillimetres { get; }

        /// <summary>When the plan reaches each of <see cref="MovementReport.Levels"/>.</summary>
        public IReadOnlyList<double> PlanMilliseconds { get; }

        /// <summary>When the platform did, from the same start.</summary>
        public IReadOnlyList<double> PlatformMilliseconds { get; }
    }


    /// <summary>
    /// Measures movements from the pose inside the MBC's replies. That pose is the
    /// recorder's Feedback DOF to within 0.03 um, row for row, and it arrives on
    /// the host's clock, so a run reports its own bend and timing with no recorder
    /// and nothing to copy off the XP machine.
    /// </summary>
    public static class MovementReport
    {
        /// <summary>Fractions of the path at which arrival times are reported.</summary>
        public static readonly double[] Levels = { 0.01, 0.10, 0.50, 0.90, 0.99 };

        private const int SpeedWindow = 5;

        /// <param name="name">How to label the movement.</param>
        /// <param name="plan">What the platform was meant to do.</param>
        /// <param name="seconds">Sample times, from the start of the command.</param>
        /// <param name="poses">Where the platform was at each of them.</param>
        public static MovementMeasurement Measure(string name, MotionPlan plan,
            IReadOnlyList<double> seconds, IReadOnlyList<Pose6> poses)
        {
            if (poses.Count != seconds.Count)
                throw new ArgumentException("One pose per sample.", nameof(poses));

            if (poses.Count < 2 * SpeedWindow + 2)
                throw new ArgumentException("Too few samples to measure.", nameof(poses));

            double dx = plan.Displacement.Surge, dy = plan.Displacement.Sway, dz = plan.Displacement.Heave;
            double lengthSquared = dx * dx + dy * dy + dz * dz;

            if (lengthSquared <= 0)
                throw new ArgumentException("The plan does not translate.", nameof(plan));

            var origin = poses[0];
            var progress = new double[poses.Count];
            double bend = 0;

            for (int n = 0; n < poses.Count; n++)
            {
                double x = poses[n].Surge - origin.Surge;
                double y = poses[n].Sway - origin.Sway;
                double z = poses[n].Heave - origin.Heave;

                double along = (x * dx + y * dy + z * dz) / lengthSquared;
                double ox = x - along * dx, oy = y - along * dy, oz = z - along * dz;

                progress[n] = along;
                bend = Math.Max(bend, Math.Sqrt(ox * ox + oy * oy + oz * oz));
            }

            double peak = 0;

            for (int n = SpeedWindow; n < poses.Count - SpeedWindow; n++)
            {
                double elapsed = seconds[n + SpeedWindow] - seconds[n - SpeedWindow];
                if (elapsed <= 0) continue;

                double x = poses[n + SpeedWindow].Surge - poses[n - SpeedWindow].Surge;
                double y = poses[n + SpeedWindow].Sway - poses[n - SpeedWindow].Sway;
                double z = poses[n + SpeedWindow].Heave - poses[n - SpeedWindow].Heave;

                peak = Math.Max(peak, Math.Sqrt(x * x + y * y + z * z) / elapsed);
            }

            var last = poses[poses.Count - 1];
            double endError = Math.Sqrt(
                Square(last.Surge - origin.Surge - dx) +
                Square(last.Sway - origin.Sway - dy) +
                Square(last.Heave - origin.Heave - dz));

            return new MovementMeasurement(name, bend * 1000.0, peak, endError * 1000.0,
                Levels.Select(level => PlanTime(plan, level)).ToArray(),
                Levels.Select(level => Crossing(progress, seconds, level)).ToArray());
        }

        public static string Describe(IEnumerable<MovementMeasurement> movements)
        {
            var text = new StringBuilder();
            string levels = string.Join("", Levels.Select(l => $"{l * 100,6:F0}%"));

            text.AppendLine("movements, measured from the pose in the MBC's replies");
            text.AppendLine($"  ms from command start{levels}    bend mm  peak m/s  end mm");

            foreach (var m in movements)
            {
                string plan = string.Join("", m.PlanMilliseconds.Select(t => $"{t,7:F0}"));
                string real = string.Join("", m.PlatformMilliseconds.Select(t => $"{t,7:F0}"));

                text.AppendLine($"  {m.Name,-8} plan        {plan}");
                text.AppendLine($"  {"",-8} platform    {real}   {m.BendMillimetres,7:F2}   {m.PeakSpeed,6:F3}  {m.EndErrorMillimetres,6:F2}");
            }

            return text.ToString();
        }

        private static double PlanTime(MotionPlan plan, double level)
        {
            double low = 0, high = 1;

            for (int i = 0; i < 50; i++)
            {
                double middle = (low + high) / 2;

                if (plan.Profile.Displacement(middle) < level) low = middle;
                else high = middle;
            }

            return low * plan.Duration.TotalMilliseconds;
        }

        internal static double Crossing(double[] progress, IReadOnlyList<double> seconds, double level)
        {
            for (int n = 1; n < progress.Length; n++)
            {
                if (progress[n - 1] < level && progress[n] >= level)
                {
                    double fraction = (level - progress[n - 1]) / (progress[n] - progress[n - 1]);
                    return 1000.0 * (seconds[n - 1] + fraction * (seconds[n] - seconds[n - 1]));
                }
            }

            return double.NaN;
        }

        private static double Square(double value) => value * value;
    }
}
