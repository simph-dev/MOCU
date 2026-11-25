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
        /// <param name="driveDirectShare">
        /// The share of a command that reaches a leg at once, 0 to below 1. Zero is
        /// a pure first-order lag; see <see cref="DriveDirectShare"/>.
        /// </param>
        /// <param name="legGeometry">
        /// When given, the drive lag acts on each leg's length, which is where the
        /// drives really work. Null applies it per axis, which leaves the platform
        /// rising about half a millimetre on a fast diagonal movement.
        /// </param>
        /// <param name="frame">One MBC frame, 1 ms.</param>
        public PlatformModel(Pose6 filterAcceleration, TimeSpan driveLag, double driveDirectShare, HexapodGeometry legGeometry, TimeSpan frame)
        {
            if (driveLag < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(driveLag), driveLag, "Cannot be negative.");
            if (!(driveDirectShare >= 0 && driveDirectShare < 1))
                throw new ArgumentOutOfRangeException(nameof(driveDirectShare), driveDirectShare, "Must be from 0 to below 1.");
            if (frame <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(frame), frame, "Must be positive.");

            foreach (var axis in Axes.All)
                if (!(filterAcceleration[axis] > 0))
                    throw new ArgumentOutOfRangeException(nameof(filterAcceleration), filterAcceleration, $"{axis} must be positive.");

            FilterAcceleration = filterAcceleration;
            DriveLag = driveLag;
            DriveDirectShare = driveDirectShare;
            LegGeometry = legGeometry;
            Frame = frame;
        }

        public Pose6 FilterAcceleration { get; }
        public TimeSpan DriveLag { get; }

        /// <summary>
        /// The share of a leg's command that shows up at once; the rest follows
        /// the first-order lag <see cref="DriveLag"/>:
        /// (1 + share lag s) / (1 + lag s). Fitted on the leg lengths of 38
        /// movement runs over 2026-11-24 and -25: lag 107.4 ms, share 0.092, the
        /// same on each day alone (107.3 / 0.093 and 107.4 / 0.091) and at every
        /// heading and duration tried. A pure lag has the same mean delay, so it
        /// puts the middle of a movement on time but not its end, which arrived
        /// 40 ms late. Why part of a command arrives at once is not known yet.
        /// The model does not describe the first 10 ms of a movement, where the
        /// platform does not move yet; that costs microns. It replaced a lag of
        /// 106.8 ms answering 9.3 ms early, which fits as well but cannot be
        /// inverted without holding the command back 9 frames. Compensated both
        /// ways on 2026-11-25, alternating, 12 movements each: the same platform
        /// to within a millisecond and a few microradians (99% of the path at
        /// 888-893 ms against 889-893, plan 880; bend 0.08-0.11 mm both).
        /// </summary>
        public double DriveDirectShare { get; }

        public HexapodGeometry LegGeometry { get; }
        public TimeSpan Frame { get; }

        /// <summary>
        /// The lab's MB-E-6DOF/12/1000. ACC_LIM from motionbase.xml: 2.0 m/s^2 on
        /// surge, sway and heave, 1.75 rad/s^2 on the rotations. Drives: a 107.4 ms
        /// lag with 9.2% arriving at once, see <see cref="DriveDirectShare"/>; the
        /// first model was a pure 96 ms lag measured on surge and sway on
        /// 2026-11-02. Per leg because
        /// the platform, compensated per axis on 2026-11-24, rose 0.45-0.55 mm at
        /// peak speed, and a per-leg lag through this geometry predicts that rise
        /// at every heading; compensated per leg on the same day it rose
        /// 0.09-0.12 mm, and 0.03-0.06 mm with the lead. config/rig-setup.json
        /// describes the same machine; the tests keep the two equal.
        /// </summary>
        public static PlatformModel MoogMbE6Dof { get; } = new PlatformModel(
            new Pose6(roll: 1.75, pitch: 1.75, yaw: 1.75, surge: 2.0, sway: 2.0, heave: 2.0),
            TimeSpan.FromMilliseconds(107.4),
            0.092,
            HexapodGeometry.MoogMbE6Dof,
            TimeSpan.FromMilliseconds(1));
    }
}
