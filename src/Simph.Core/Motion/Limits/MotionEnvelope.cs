using System;

using Simph.Core.Geometry;


namespace Simph.Core.Motion.Limits
{
    /// <summary>
    /// The limits of a particular machine, per axis.
    /// </summary>
    public sealed class MotionEnvelope
    {
        private readonly AxisLimits[] _limits = new AxisLimits[6];

        public MotionEnvelope(
            AxisLimits roll, AxisLimits pitch, AxisLimits yaw,
            AxisLimits surge, AxisLimits sway, AxisLimits heave)
        {
            _limits[(int)Axis.Roll] = roll;
            _limits[(int)Axis.Pitch] = pitch;
            _limits[(int)Axis.Yaw] = yaw;
            _limits[(int)Axis.Surge] = surge;
            _limits[(int)Axis.Sway] = sway;
            _limits[(int)Axis.Heave] = heave;
        }

        public AxisLimits this[Axis axis] => _limits[(int)axis];


        private const double Deg = Math.PI / 180.0;
        private const double Unknown = double.PositiveInfinity;

        /// <summary>
        /// What the lab allows the Moog MB-E-6DOF/12/1000 to do. A choice of the
        /// lab's, unlike Platform.PlatformModel, which describes what the machine
        /// does and has to match its configuration.
        ///
        /// <para><b>Acceleration</b> is set equal to the MBC's configured ACC_LIM
        /// (motionbase.xml: 2.0 m/s^2 on X, Y, Z, 1.75 rad/s^2 on the rotations),
        /// the data sheet allowing far more (0.6 G, about 5.9 m/s^2, on surge and
        /// sway): nothing past it could be delivered as asked anyway.</para>
        ///
        /// <para><b>Speed</b> comes from the data sheet (see the header of
        /// MoogModule.DofParameters), which is stricter than the MBC's
        /// VEL_LIM entries (1.27 m/s on X and Y): surge and sway 0.5 m/s, heave
        /// 0.3 m/s, roll and pitch 30 deg/s, yaw 40 deg/s.</para>
        ///
        /// <para><b>Position</b> bounds are the data sheet's excursions, except on
        /// heave, which is left unestablished: the experiments park at Heave =
        /// -0.22 m while the sheet gives +/-0.178 m, so the two are plainly
        /// measured from different origins and the offset is not recorded
        /// anywhere we can read. Until that is pinned down against the MBC,
        /// claiming a heave bound would only produce false rejections.</para>
        /// </summary>
        public static MotionEnvelope MoogMbE6Dof { get; } = new MotionEnvelope(
            roll:  new AxisLimits(-21 * Deg, 21 * Deg, 30 * Deg, 1.75),
            pitch: new AxisLimits(-22 * Deg, 22 * Deg, 30 * Deg, 1.75),
            yaw:   new AxisLimits(-22 * Deg, 22 * Deg, 40 * Deg, 1.75),
            surge: new AxisLimits(-0.241, 0.259, 0.5, 2.0),
            sway:  new AxisLimits(-0.259, 0.259, 0.5, 2.0),
            heave: new AxisLimits(-Unknown, Unknown, 0.3, 2.0));
    }
}
