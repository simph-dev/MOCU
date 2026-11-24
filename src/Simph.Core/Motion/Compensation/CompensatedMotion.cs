using System;

using Simph.Core.Geometry;
using Simph.Core.Platform;
using Simph.Core.Time;

namespace Simph.Core.Motion.Compensation
{
    /// <summary>
    /// A movement with the platform's response taken out in advance: what to send
    /// so that the platform, rather than the command, follows the plan.
    ///
    /// Two stages are inverted, last one first. After the filter sit the drives,
    /// a first-order lag of about 96 ms on each leg, so the filter's output has to
    /// run ahead of the plan by that lag, worked out on the leg lengths. The filter
    /// then needs its input ahead of that by the lead its law demands on each
    /// axis, see <see cref="TrajectoryFilterModel"/>.
    ///
    /// Both leads vanish at rest, so the command starts and ends where the plan
    /// does. In between it leads by up to a few centimetres, and inverting the lag
    /// makes it overshoot the end point by about a millimetre.
    /// </summary>
    public sealed class CompensatedMotion
    {
        private const int EdgeFrames = 5;

        private readonly MotionPlan _plan;
        private readonly PlatformModel _model;
        private readonly TrajectoryFilterModel[] _filters = new TrajectoryFilterModel[6];
        private readonly TimeSpan _frame;
        private readonly TimeSpan _halfFrame;
        private readonly double _dt;
        private readonly double _lagLeadSeconds;

        public CompensatedMotion(MotionPlan plan, PlatformModel model)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _model = model ?? throw new ArgumentNullException(nameof(model));

            _frame = model.Frame;
            _halfFrame = TimeSpan.FromTicks(_frame.Ticks / 2);
            _dt = _frame.TotalSeconds;

            foreach (var axis in Axes.All)
                _filters[(int)axis] = new TrajectoryFilterModel(model.FilterAcceleration[axis], _frame);

            // A first-order lag sampled at dt moves a fraction alpha of the way to
            // its input each frame. To land on x[n+1] from x[n] its input must be
            // x[n] + (x[n+1] - x[n]) / alpha, which is the plan plus a lead of
            // dt (1 - alpha) / alpha times its speed over that frame: 95.5 ms at 96.
            var driveLag = model.DriveLag;
            double alpha = driveLag > TimeSpan.Zero ? 1.0 - Math.Exp(-_dt / driveLag.TotalSeconds) : 1.0;
            _lagLeadSeconds = _dt * (1.0 - alpha) / alpha;
        }

        public MotionPlan Plan => _plan;

        public PlatformModel Model => _model;

        public TimeSpan Frame => _frame;

        /// <summary>What the filter's output should be at this instant.</summary>
        public Pose6 FilterOutput(Timestamp now) =>
            _model.LegGeometry == null || _lagLeadSeconds == 0
                ? _plan.Evaluate(now) + _plan.Velocity(now + _halfFrame) * _lagLeadSeconds
                : LegLeadOutput(now);

        /// <summary>
        /// The same lead, on the leg lengths: each leg is put ahead of where the
        /// plan needs it by its own speed times the lead, and the pose those
        /// lengths make is what the filter has to deliver. Leg speed is taken over
        /// the frame by differencing; the per-leg tests send the result through a
        /// simulated filter and legs and bound what that costs.
        /// </summary>
        private Pose6 LegLeadOutput(Timestamp now)
        {
            var geometry = _model.LegGeometry;
            var here = _plan.Evaluate(now);
            var next = _plan.Evaluate(now + _frame);

            double[] legsHere = geometry.LegLengths(here);
            double[] legsNext = geometry.LegLengths(next);
            double gain = _lagLeadSeconds / _dt;

            var led = new double[6];
            for (int i = 0; i < 6; i++)
                led[i] = legsHere[i] + (legsNext[i] - legsHere[i]) * gain;

            return geometry.PoseFromLegLengths(led, guess: here + (next - here) * gain);
        }

        /// <summary>The speed of that output over the frame starting at this instant.</summary>
        private Pose6 FilterOutputVelocity(Timestamp now)
        {
            // Per axis, from the plan's analytic derivatives: differencing would
            // multiply the 1e-7 error of the erf approximation by the lag gain and
            // by 1 / dt. Per leg there is no analytic form, so it is differenced.
            if (_model.LegGeometry == null || _lagLeadSeconds == 0)
                return _plan.Velocity(now + _halfFrame) + _plan.Acceleration(now + _frame) * _lagLeadSeconds;

            return (FilterOutput(now + _frame) - FilterOutput(now)) / _dt;
        }

        /// <summary>What to send at this instant.</summary>
        public Pose6 Command(Timestamp now)
        {
            var output = FilterOutput(now);
            var velocity = FilterOutputVelocity(now);
            var command = output;

            foreach (var axis in Axes.All)
                command = command.With(axis, output[axis] + _filters[(int)axis].Lead(velocity[axis]));

            return command;
        }

        /// <summary>
        /// Walks the whole movement and reports what it asks of the machine, so a
        /// plan the filter cannot deliver is caught before anything moves.
        /// </summary>
        public CompensationDemand Measure()
        {
            var peakAcceleration = Pose6.Zero;
            var peakSpeed = Pose6.Zero;
            var lowest = _plan.From;
            var highest = _plan.From;

            int frames = (int)Math.Ceiling(_plan.Duration.TotalSeconds / _dt);

            var before = FilterOutput(_plan.T0 - _frame);
            var at = FilterOutput(_plan.T0);

            for (int n = 0; n <= frames + 2; n++)
            {
                var when = _plan.T0 + TimeSpan.FromTicks(_frame.Ticks * n);
                var after = FilterOutput(when + _frame);
                var command = Command(when);

                // The profile is cut off at both ends with a little speed left, so
                // the frames next to them carry a small step. The filter's own
                // clamp takes that up within a few milliseconds; it says nothing
                // about whether the movement itself can be followed.
                bool insideTheMovement = n >= EdgeFrames && n <= frames - EdgeFrames;

                foreach (var axis in Axes.All)
                {
                    double speed = Math.Abs(after[axis] - at[axis]) / _dt;
                    double acceleration = Math.Abs(after[axis] - 2.0 * at[axis] + before[axis]) / (_dt * _dt);

                    if (insideTheMovement)
                    {
                        peakAcceleration = peakAcceleration.With(axis, Math.Max(peakAcceleration[axis], acceleration));
                        peakSpeed = peakSpeed.With(axis, Math.Max(peakSpeed[axis], speed));
                    }

                    lowest = lowest.With(axis, Math.Min(lowest[axis], command[axis]));
                    highest = highest.With(axis, Math.Max(highest[axis], command[axis]));
                }

                before = at;
                at = after;
            }

            return new CompensationDemand(peakSpeed, peakAcceleration, lowest, highest);
        }
    }


    /// <summary>What a compensated movement asks of the machine, axis by axis.</summary>
    public sealed class CompensationDemand
    {
        public CompensationDemand(Pose6 peakSpeed, Pose6 peakAcceleration, Pose6 lowestCommand, Pose6 highestCommand)
        {
            PeakSpeed = peakSpeed;
            PeakAcceleration = peakAcceleration;
            LowestCommand = lowestCommand;
            HighestCommand = highestCommand;
        }

        /// <summary>Peak speed of the filter's output.</summary>
        public Pose6 PeakSpeed { get; }

        /// <summary>
        /// Peak acceleration of the filter's output. Above the axis's limit the
        /// filter cannot follow and the compensation stops being exact.
        /// </summary>
        public Pose6 PeakAcceleration { get; }

        /// <summary>The lowest value sent on each axis.</summary>
        public Pose6 LowestCommand { get; }

        /// <summary>The highest value sent on each axis.</summary>
        public Pose6 HighestCommand { get; }
    }
}
