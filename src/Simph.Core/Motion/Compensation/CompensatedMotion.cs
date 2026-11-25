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
    /// Two stages are inverted, last one first. After the filter sit the drives:
    /// on each leg a share of a command arrives at once and the rest follows a
    /// first-order lag, (1 + share lag s) / (1 + lag s), see
    /// <see cref="PlatformModel.DriveDirectShare"/>. Their inverse, worked out on
    /// the leg lengths, is (1 + lag s) / (1 + share lag s): the filter's output
    /// runs ahead of the plan by the lag times the leg's speed, smoothed by a lag
    /// of share times lag (9.9 ms) so that the part that arrives at once is not
    /// sent twice. Both are causal, so the command starts at T0 with the plan.
    /// The filter then needs its input ahead of the output by the lead its law
    /// demands on each axis, see <see cref="TrajectoryFilterModel"/>.
    ///
    /// The leads vanish at rest, so the command starts and ends where the plan
    /// does. In between it leads by up to 58 mm and overshoots the end point by
    /// about 2 mm (16 degrees, 13 cm, 1 s).
    ///
    /// The command is handed out only as a <see cref="CommandTable"/>, one pose
    /// per frame from the plan's T0. Worked out between frames it was wrong: the
    /// end guard missed a frame, and a quarter to three quarters of a frame off
    /// the grid the command stepped 2 to 18 mm in one frame. The smoothing has a
    /// state as well, which a table carries from frame to frame.
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
        private readonly double _directShareLagSeconds;

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
            // dt (1 - alpha) / alpha times its speed over that frame: 106.9 ms at 107.4.
            var driveLag = model.DriveLag;
            double alpha = driveLag > TimeSpan.Zero ? 1.0 - Math.Exp(-_dt / driveLag.TotalSeconds) : 1.0;
            _lagLeadSeconds = _dt * (1.0 - alpha) / alpha;

            _directShareLagSeconds = model.DriveDirectShare * driveLag.TotalSeconds;
        }

        public MotionPlan Plan => _plan;

        public PlatformModel Model => _model;

        public TimeSpan Frame => _frame;

        /// <summary>How long the command is streamed from the plan's start: the plan's duration.</summary>
        public TimeSpan CommandDuration => _plan.Duration;

        /// <summary>
        /// Frames in the command: the frame rate times <see cref="CommandDuration"/>,
        /// 1000 for a 1 s plan at 1 ms a frame.
        /// </summary>
        public int FrameCount => (int)Math.Ceiling((double)CommandDuration.Ticks / _frame.Ticks);

        /// <summary>
        /// The command, worked out once: frame k is to be sent at T0 plus k
        /// frames. The first frame already moves; the last one is the end point.
        /// </summary>
        public CommandTable Table()
        {
            var frames = new Pose6[FrameCount];

            // A plan that is not finite cannot be led: the filter's lead takes the
            // sign of a speed and throws on NaN. Its table is left at the start
            // point, as non-finite as the plan, and the validator refuses it.
            if (_plan.From.HasNonFinite() || _plan.To.HasNonFinite())
            {
                for (int k = 0; k < frames.Length; k++) frames[k] = _plan.From;
                return new CommandTable(this, frames);
            }

            var outputs = FilterOutputs(frames.Length + 1);

            for (int k = 0; k < frames.Length; k++)
                frames[k] = CommandAt(k, outputs);

            return new CommandTable(this, frames);
        }

        /// <summary>
        /// What the filter's output should be on frames 0 .. count - 1 from T0.
        /// Before T0 it is the start point, at rest.
        /// </summary>
        private Pose6[] FilterOutputs(int count)
        {
            var outputs = new Pose6[count];
            var geometry = _model.LegGeometry;

            // On the leg lengths where the drives work, or per axis in the older form.
            double[] Led(Timestamp planTime) => geometry != null && _lagLeadSeconds > 0
                ? LegLead(planTime)
                : AsVector(_plan.Evaluate(planTime) + _plan.Velocity(planTime + _halfFrame) * _lagLeadSeconds);

            Pose6 AsPose(double[] v, Pose6 guess) => geometry != null && _lagLeadSeconds > 0
                ? geometry.PoseFromLegLengths(v, guess)
                : new Pose6(roll: v[0], pitch: v[1], yaw: v[2], surge: v[3], sway: v[4], heave: v[5]);

            var previous = Led(_plan.T0 - _frame);
            var smoothed = (double[])previous.Clone();
            var guess = _plan.From;

            // The lag that undoes the share arriving at once, integrated exactly
            // for an input linear between frames.
            double t = _directShareLagSeconds;
            double decay = t > 0 ? Math.Exp(-_dt / t) : 0.0;

            for (int k = 0; k < count; k++)
            {
                var led = Led(_plan.T0 + TimeSpan.FromTicks(_frame.Ticks * k));

                for (int i = 0; i < led.Length; i++)
                {
                    if (t <= 0) { smoothed[i] = led[i]; continue; }

                    double rate = (led[i] - previous[i]) / _dt;
                    smoothed[i] = led[i] - rate * t + (smoothed[i] - previous[i] + rate * t) * decay;
                }

                guess = AsPose(smoothed, guess);
                outputs[k] = guess;
                previous = led;
            }

            return outputs;
        }

        /// <summary>
        /// Each leg put ahead of where the plan needs it by its own speed times
        /// the lag's lead. Leg speed is taken over the frame by differencing; the
        /// per-leg tests send the result through a simulated filter and legs and
        /// bound what that costs.
        /// </summary>
        private double[] LegLead(Timestamp planTime)
        {
            var geometry = _model.LegGeometry;
            double[] here = geometry.LegLengths(_plan.Evaluate(planTime));
            double[] next = geometry.LegLengths(_plan.Evaluate(planTime + _frame));
            double gain = _lagLeadSeconds / _dt;

            var led = new double[6];
            for (int i = 0; i < 6; i++)
                led[i] = here[i] + (next[i] - here[i]) * gain;

            return led;
        }

        private static double[] AsVector(Pose6 pose) =>
            new[] { pose.Roll, pose.Pitch, pose.Yaw, pose.Surge, pose.Sway, pose.Heave };

        /// <summary>The speed of the filter's output over frame k.</summary>
        private Pose6 FilterOutputVelocity(int k, Pose6[] outputs)
        {
            // Per axis, with nothing smoothed, from the plan's analytic
            // derivatives: differencing would multiply the 1e-7 error of the erf
            // approximation by the lag gain and by 1 / dt. Otherwise there is no
            // analytic form, so it is differenced.
            if ((_model.LegGeometry == null || _lagLeadSeconds == 0) && _directShareLagSeconds == 0)
            {
                var planTime = _plan.T0 + TimeSpan.FromTicks(_frame.Ticks * k);
                return _plan.Velocity(planTime + _halfFrame) + _plan.Acceleration(planTime + _frame) * _lagLeadSeconds;
            }

            return (outputs[k + 1] - outputs[k]) / _dt;
        }

        /// <summary>What to send on frame k.</summary>
        private Pose6 CommandAt(int k, Pose6[] outputs)
        {
            // The profile is cut off at the end with a little speed left, so the
            // lag's lead drops by about 0.35 mm on the last frame. Seen as a
            // speed, that drop makes the filter's lead a one-frame spike of about
            // 25 mm. So the last frame is simply the end point. Until 2026-11-24
            // the spike was sent on every compensated movement (in sent.csv: surge
            // 125.3, 99.9, 125.0 mm); the filter's acceleration limit kept it to
            // microns at the platform.
            if (k + 1 >= FrameCount)
                return _plan.To;

            var output = outputs[k];
            var velocity = FilterOutputVelocity(k, outputs);
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

            if (_plan.From.HasNonFinite() || _plan.To.HasNonFinite())
                return new CompensationDemand(peakSpeed, peakAcceleration);

            int frames = FrameCount;
            var outputs = FilterOutputs(frames + 4);

            for (int n = 0; n <= frames + 2; n++)
            {
                var before = n == 0 ? _plan.From : outputs[n - 1];
                var at = outputs[n];
                var after = outputs[n + 1];

                // The profile is cut off at both ends with a little speed left, so
                // the frames next to them carry a small step. The filter's own
                // clamp takes that up within a few milliseconds; it says nothing
                // about whether the movement itself can be followed.
                bool insideTheMovement = n >= EdgeFrames && n <= frames - EdgeFrames;

                if (!insideTheMovement) continue;

                foreach (var axis in Axes.All)
                {
                    double speed = Math.Abs(after[axis] - at[axis]) / _dt;
                    double acceleration = Math.Abs(after[axis] - 2.0 * at[axis] + before[axis]) / (_dt * _dt);

                    peakAcceleration = peakAcceleration.With(axis, Math.Max(peakAcceleration[axis], acceleration));
                    peakSpeed = peakSpeed.With(axis, Math.Max(peakSpeed[axis], speed));
                }
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
