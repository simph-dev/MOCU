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
        /// <param name="legGeometry">
        /// When given, the drive lag acts on each leg's length, which is where the
        /// drives really work. Null applies it per axis, which leaves the platform
        /// rising about half a millimetre on a fast diagonal movement.
        /// </param>
        /// <param name="frame">One MBC frame, 1 ms.</param>
        public PlatformModel(Pose6 filterAcceleration, TimeSpan driveLag, HexapodGeometry legGeometry, TimeSpan frame)
        {
            if (driveLag < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(driveLag), driveLag, "Cannot be negative.");
            if (frame <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(frame), frame, "Must be positive.");

            foreach (var axis in Axes.All)
                if (!(filterAcceleration[axis] > 0))
                    throw new ArgumentOutOfRangeException(nameof(filterAcceleration), filterAcceleration, $"{axis} must be positive.");

            FilterAcceleration = filterAcceleration;
            DriveLag = driveLag;
            LegGeometry = legGeometry;
            Frame = frame;
        }

        public Pose6 FilterAcceleration { get; }
        public TimeSpan DriveLag { get; }
        public HexapodGeometry LegGeometry { get; }
        public TimeSpan Frame { get; }

        /// <summary>
        /// The lab's MB-E-6DOF/12/1000. ACC_LIM from motionbase.xml: 2.0 m/s^2 on
        /// surge, sway and heave, 1.75 rad/s^2 on the rotations. Drive lag 96 ms,
        /// measured on surge and sway on 2026-11-02. Per leg because the platform,
        /// compensated per axis on 2026-11-24, rose 0.45-0.55 mm at peak speed,
        /// and a per-leg lag through this geometry predicts that rise at every
        /// heading. That the per-leg compensation removes it is a prediction, not
        /// yet measured.
        /// </summary>
        public static PlatformModel MoogMbE6Dof { get; } = new PlatformModel(
            new Pose6(roll: 1.75, pitch: 1.75, yaw: 1.75, surge: 2.0, sway: 2.0, heave: 2.0),
            TimeSpan.FromMilliseconds(96),
            HexapodGeometry.MoogMbE6Dof,
            TimeSpan.FromMilliseconds(1));
    }
}
