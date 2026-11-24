using System;

using Simph.Core.Geometry;
using Simph.Core.Time;


namespace Simph.Core.Motion
{
    /// <summary>
    /// One movement, described rather than performed: where from, where to, how
    /// long, which shape, and when it starts. Immutable and stateless, so the
    /// same plan can be sent to the daemon, handed to the virtual camera and
    /// replayed in a test, and all three will agree exactly.
    ///
    /// <para>A function of time rather than an array of positions: evaluated at
    /// the current instant there is no index to keep in step with the clock, so
    /// it cannot run backwards, stall, or depend on the rate of the loop that
    /// asks.</para>
    /// </summary>
    public sealed class MotionPlan
    {
        public Pose6 From { get; }
        public Pose6 To { get; }

        /// <summary>Commanded duration. Strictly positive.</summary>
        public TimeSpan Duration { get; }

        /// <summary>When the movement begins on the shared clock.</summary>
        public Timestamp T0 { get; }

        public IMotionProfile Profile { get; }


        public MotionPlan(Pose6 from, Pose6 to, TimeSpan duration, Timestamp t0, IMotionProfile profile)
        {
            if (duration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration must be positive.");

            Profile = profile ?? throw new ArgumentNullException(nameof(profile));

            From = from;
            To = to;
            Duration = duration;
            T0 = t0;
        }


        public Timestamp End => T0 + Duration;

        public Pose6 Displacement => To - From;

        /// <summary>
        /// Where the platform should be at <paramref name="now"/>. Clamped: before
        /// T0 it is From, after End it is To, so a tick that lands outside the
        /// window holds position instead of throwing. Holding is the only safe
        /// behaviour for a loop that must keep sending.
        /// </summary>
        public Pose6 Evaluate(Timestamp now) =>
            Pose6.Lerp(From, To, Profile.Displacement(Tau(now)));

        /// <summary>Commanded velocity at <paramref name="now"/>, per axis.</summary>
        public Pose6 Velocity(Timestamp now) =>
            Displacement * (Profile.VelocityFactor(Tau(now)) / Duration.TotalSeconds);

        /// <summary>Commanded acceleration at <paramref name="now"/>, per axis.</summary>
        public Pose6 Acceleration(Timestamp now) =>
            Displacement * (Profile.AccelerationFactor(Tau(now)) / (Duration.TotalSeconds * Duration.TotalSeconds));


        /// <summary>
        /// Largest speed each axis reaches, as a magnitude. Scales as 1 / duration,
        /// so shortening a commanded movement raises it proportionally.
        /// </summary>
        public Pose6 PeakSpeed => Magnitude(Displacement) * (Profile.PeakVelocityFactor / Duration.TotalSeconds);

        /// <summary>
        /// Largest acceleration each axis reaches, as a magnitude. Scales as
        /// 1 / duration^2 - so commanding a movement 20% shorter to compensate for
        /// the platform's own lag raises it by 56%.
        /// </summary>
        public Pose6 PeakAcceleration => Magnitude(Displacement) *
            (Profile.PeakAccelerationFactor / (Duration.TotalSeconds * Duration.TotalSeconds));


        public MotionPlan WithT0(Timestamp t0) => new MotionPlan(From, To, Duration, t0, Profile);

        public MotionPlan WithDuration(TimeSpan duration) => new MotionPlan(From, To, duration, T0, Profile);

        public MotionPlan WithEndPoint(Pose6 to) => new MotionPlan(From, to, Duration, T0, Profile);


        /// <summary>Normalised time, unclamped; the profile clamps it.</summary>
        private double Tau(Timestamp now) => (now - T0).TotalSeconds / Duration.TotalSeconds;

        private static Pose6 Magnitude(Pose6 p)
        {
            var result = Pose6.Zero;
            foreach (var axis in Axes.All) result = result.With(axis, Math.Abs(p[axis]));
            return result;
        }

        public override string ToString() =>
            $"MotionPlan {From} -> {To} over {Duration.TotalMilliseconds:F0} ms at t0 {T0}";
    }
}
