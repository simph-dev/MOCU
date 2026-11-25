using System;

using Simph.Core.Geometry;


namespace Simph.Core.Platform
{
    /// <summary>
    /// What the Moog does to a command between our packet and the platform's
    /// movement, as measured: the MBC's trajectory filter, then the drives. This
    /// is a description of the machine, used to compensate for it; what the lab
    /// allows the platform to do is a separate thing, the MotionEnvelope.
    /// </summary>
    public sealed class PlatformModel
    {
        /// <param name="filterAcceleration">
        /// ACC_LIM of the MBC's DOF trajectory filter on each axis. Has to match
        /// the MBC's configuration exactly, or the compensation is wrong.
        /// </param>
        /// <param name="driveLag">Time constant of the drives' first-order lag. Zero leaves the drives uncompensated.</param>
        /// <param name="driveLead">
        /// How far ahead of that lag the drives answer. Zero is a pure first-order
        /// lag; see <see cref="DriveLead"/>.
        /// </param>
        /// <param name="legGeometry">
        /// When given, the drive lag acts on each leg's length, which is where the
        /// drives really work. Null applies it per axis, which leaves the platform
        /// rising about half a millimetre on a fast diagonal movement.
        /// </param>
        /// <param name="frame">One MBC frame, 1 ms.</param>
        public PlatformModel(Pose6 filterAcceleration, TimeSpan driveLag, TimeSpan driveLead, HexapodGeometry legGeometry, TimeSpan frame)
        {
            if (driveLag < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(driveLag), driveLag, "Cannot be negative.");
            if (driveLead < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(driveLead), driveLead, "Cannot be negative.");
            if (driveLead > driveLag) throw new ArgumentOutOfRangeException(nameof(driveLead), driveLead, "Cannot exceed the drive lag.");
            if (frame <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(frame), frame, "Must be positive.");

            foreach (var axis in Axes.All)
                if (!(filterAcceleration[axis] > 0))
                    throw new ArgumentOutOfRangeException(nameof(filterAcceleration), filterAcceleration, $"{axis} must be positive.");

            FilterAcceleration = filterAcceleration;
            DriveLag = driveLag;
            DriveLead = driveLead;
            LegGeometry = legGeometry;
            Frame = frame;
        }

        public Pose6 FilterAcceleration { get; }
        public TimeSpan DriveLag { get; }

        /// <summary>
        /// The drives answer a leg's command like the first-order lag
        /// <see cref="DriveLag"/>, but this much earlier: exp(+lead s) / (1 + lag s).
        /// Put another way, about lead / lag of a command (9%) shows up at once
        /// and the rest follows the lag. Fitted on the 17 movement runs of
        /// 2026-11-24, one run at a time: lag 105.7-107.2 ms, lead 8.3-13 ms,
        /// whatever the duration (0.9-2 s), distance, heading or compensation. A
        /// pure 96 ms lag has the same mean delay, so it puts the middle of a
        /// movement on time but not its end, which arrived 40 ms late. The lead
        /// does not describe the first 10 ms of a movement, where the platform
        /// does not move yet; that costs microns. Measured as a compensation on
        /// 2026-11-24, alternating with the pure 96 ms lag, 12 movements each:
        /// 99% of the path at 881-893 ms against 916-925 (plan 880), 90% at
        /// 712-714 (plan 713).
        /// </summary>
        public TimeSpan DriveLead { get; }

        public HexapodGeometry LegGeometry { get; }
        public TimeSpan Frame { get; }

        /// <summary>
        /// The lab's MB-E-6DOF/12/1000. ACC_LIM from motionbase.xml: 2.0 m/s^2 on
        /// surge, sway and heave, 1.75 rad/s^2 on the rotations. Drives: a 106.8 ms
        /// lag answering 9.3 ms early, see <see cref="DriveLead"/>; it replaced the
        /// pure 96 ms lag measured on surge and sway on 2026-11-02. Per leg because
        /// the platform, compensated per axis on 2026-11-24, rose 0.45-0.55 mm at
        /// peak speed, and a per-leg lag through this geometry predicts that rise
        /// at every heading; compensated per leg on the same day it rose
        /// 0.09-0.12 mm, and 0.03-0.06 mm with the lead. config/rig-setup.json
        /// describes the same machine; the tests keep the two equal.
        /// </summary>
        public static PlatformModel MoogMbE6Dof { get; } = new PlatformModel(
            new Pose6(roll: 1.75, pitch: 1.75, yaw: 1.75, surge: 2.0, sway: 2.0, heave: 2.0),
            TimeSpan.FromMilliseconds(106.8),
            TimeSpan.FromMilliseconds(9.3),
            HexapodGeometry.MoogMbE6Dof,
            TimeSpan.FromMilliseconds(1));
    }
}
