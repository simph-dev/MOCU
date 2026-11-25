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
    /// a first-order lag on each leg, so the filter's output has to run ahead of
    /// the plan by that lag, worked out on the leg lengths. The drives answer
    /// <see cref="PlatformModel.DriveLead"/> earlier than a pure lag would, so
    /// that output is then held back by the same amount: the whole compensated
    /// command runs that much late and lasts that much longer than the plan,
    /// see <see cref="CommandDuration"/>. The filter then needs its input ahead
    /// of the output by the lead its law demands on each axis, see
    /// <see cref="TrajectoryFilterModel"/>.
    ///
    /// The leads vanish at rest, so the command starts and ends where the plan
    /// does. In between it leads by up to 58 mm, and inverting the lag makes it
    /// overshoot the end point by 2.1 mm (16 degrees, 13 cm, 1 s; 3.4 mm at 0.9 s).
    ///
    /// The command is handed out only as a <see cref="CommandTable"/>, one pose
    /// per frame from the plan's T0. Worked out between frames it was wrong: the
    /// end guard missed a frame, and a quarter to three quarters of a frame off
    /// the grid the command stepped 2 to 18 mm in one frame.
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
        private readonly TimeSpan _planDelay;

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

            // The drive lead is taken in whole frames, so that the plan is still
            // sampled on the frame grid: shifted by a fraction of a frame, the
            // frame that straddles the plan's start would pick up part of the
            // step the cut-off profile begins with, and send it as a spike of a
            // few millimetres. The fit spreads over 8.3-13 ms, so 9.3 sent as 9
            // costs nothing that can be measured.
            long leadFrames = (long)Math.Round(model.DriveLead.TotalSeconds / _dt);
            _planDelay = TimeSpan.FromTicks(_frame.Ticks * leadFrames);
        }

        public MotionPlan Plan => _plan;

        public PlatformModel Model => _model;

        public TimeSpan Frame => _frame;

        /// <summary>
        /// How far the command runs behind the plan: the drive lead, in whole
        /// frames.
        /// </summary>
        public TimeSpan PlanDelay => _planDelay;

        /// <summary>
        /// How long the command has to be streamed from the plan's start: the
        /// plan's duration plus <see cref="PlanDelay"/>. Cut at the plan's own
        /// end, its last few milliseconds would be replaced by a step.
        /// </summary>
        public TimeSpan CommandDuration => _plan.Duration + _planDelay;

        /// <summary>
        /// Frames in the command: the frame rate times <see cref="CommandDuration"/>,
        /// 1009 for a 1 s plan (1000 frames of the plan, 9 of the drive lead).
        /// </summary>
        public int FrameCount => (int)Math.Ceiling((double)CommandDuration.Ticks / _frame.Ticks);

        /// <summary>
        /// The command, worked out once: frame k is to be sent at T0 plus k
        /// frames. The first frames repeat the start point until the delayed
        /// plan begins; the last one is the end point.
        /// </summary>
        public CommandTable Table()
        {
            var frames = new Pose6[FrameCount];

            // A plan that is not finite cannot be led: the filter's lead takes the
            // sign of a speed and throws on NaN. Its table is left at the start
            // point, as non-finite as the plan, and the validator refuses it.
            bool finite = !_plan.From.HasNonFinite() && !_plan.To.HasNonFinite();

            for (int k = 0; k < frames.Length; k++)
                frames[k] = finite ? CommandAt(_plan.T0 + TimeSpan.FromTicks(_frame.Ticks * k)) : _plan.From;

            return new CommandTable(this, frames);
        }

        /// <summary>What the filter's output should be at this instant.</summary>
        public Pose6 FilterOutput(Timestamp now)
        {
            // The drives answer as exp(+lead s) / (1 + lag s); the inverse is
            // (1 + lag s) exp(-lead s): the lag's lead below, taken on the plan as
            // it was the drive lead ago.
            var planTime = now - _planDelay;

            return _model.LegGeometry == null || _lagLeadSeconds == 0
                ? _plan.Evaluate(planTime) + _plan.Velocity(planTime + _halfFrame) * _lagLeadSeconds
                : LegLeadOutput(planTime);
        }

        /// <summary>
        /// The same lead, on the leg lengths: each leg is put ahead of where the
        /// plan needs it by its own speed times the lead, and the pose those
        /// lengths make is what the filter has to deliver. Leg speed is taken over
        /// the frame by differencing; the per-leg tests send the result through a
        /// simulated filter and legs and bound what that costs.
        /// </summary>
        private Pose6 LegLeadOutput(Timestamp planTime)
        {
            var geometry = _model.LegGeometry;
            var here = _plan.Evaluate(planTime);
            var next = _plan.Evaluate(planTime + _frame);

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
            {
                var planTime = now - _planDelay;
                return _plan.Velocity(planTime + _halfFrame) + _plan.Acceleration(planTime + _frame) * _lagLeadSeconds;
            }

            return (FilterOutput(now + _frame) - FilterOutput(now)) / _dt;
        }

        /// <summary>What to send at this instant, which must lie on the frame grid.</summary>
        private Pose6 CommandAt(Timestamp now)
        {
            // The profile is cut off at both ends with a little speed left, so the
            // lag's lead jumps by about 0.35 mm on the first and the last frame.
            // Seen as a speed, that jump makes the filter's lead a one-frame
            // spike of about 25 mm on the frame next to it. So outside the
            // delayed plan, and on its last frame, the command is simply the end
            // point. Until 2026-11-24 the spike at the end was sent on every
            // compensated movement (in sent.csv: surge 125.3, 99.9, 125.0 mm);
            // the filter's acceleration limit kept it to microns at the platform.
            if (now < _plan.T0 + _planDelay)
                return _plan.From;

            if (now + _frame >= _plan.End + _planDelay)
                return _plan.To;

            var output = FilterOutput(now);
            var velocity = FilterOutputVelocity(now);
            var command = output;

            foreach (var axis in Axes.All)
                command = command.With(axis, output[axis] + _filters[(int)axis].Lead(velocity[axis]));

            return command;
        }

        /// <summary>
        /// Walks the filter's output over the whole movement and reports what it
        /// asks of the filter, so a plan the filter cannot deliver is caught
        /// before anything moves. The command itself is checked on its table.
        /// </summary>
        public CompensationDemand Measure()
        {
            var peakAcceleration = Pose6.Zero;
            var peakSpeed = Pose6.Zero;

            int frames = FrameCount;
            int leadFrames = (int)(_planDelay.Ticks / _frame.Ticks);

            var before = FilterOutput(_plan.T0 - _frame);
            var at = FilterOutput(_plan.T0);

            for (int n = 0; n <= frames + 2; n++)
            {
                var when = _plan.T0 + TimeSpan.FromTicks(_frame.Ticks * n);
                var after = FilterOutput(when + _frame);

                // The profile is cut off at both ends with a little speed left, so
                // the frames next to them carry a small step. The filter's own
                // clamp takes that up within a few milliseconds; it says nothing
                // about whether the movement itself can be followed.
                bool insideTheMovement = n >= leadFrames + EdgeFrames && n <= frames - EdgeFrames;

                foreach (var axis in Axes.All)
                {
                    double speed = Math.Abs(after[axis] - at[axis]) / _dt;
                    double acceleration = Math.Abs(after[axis] - 2.0 * at[axis] + before[axis]) / (_dt * _dt);

                    if (insideTheMovement)
                    {
                        peakAcceleration = peakAcceleration.With(axis, Math.Max(peakAcceleration[axis], acceleration));
                        peakSpeed = peakSpeed.With(axis, Math.Max(peakSpeed[axis], speed));
                    }
                }

                before = at;
                at = after;
            }

            return new CompensationDemand(peakSpeed, peakAcceleration);
        }
    }


    /// <summary>What a compensated movement asks of the MBC's filter, axis by axis.</summary>
    public sealed class CompensationDemand
    {
        public CompensationDemand(Pose6 peakSpeed, Pose6 peakAcceleration)
        {
            PeakSpeed = peakSpeed;
            PeakAcceleration = peakAcceleration;
        }

        /// <summary>Peak speed of the filter's output.</summary>
        public Pose6 PeakSpeed { get; }

        /// <summary>
        /// Peak acceleration of the filter's output. Above the axis's limit the
        /// filter cannot follow and the compensation stops being exact.
        /// </summary>
        public Pose6 PeakAcceleration { get; }
    }
}
