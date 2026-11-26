using System;

using Simph.Core.Geometry;


namespace Simph.Core.Motion.Limits
{
    /// <summary>
    /// The limits of a particular machine, per axis and, when known, per leg.
    /// </summary>
    public sealed class MotionEnvelope
    {
        private readonly AxisLimits[] _limits = new AxisLimits[6];

        /// <param name="legs">How long each leg may be; null leaves the legs unchecked.</param>
        public MotionEnvelope(
            AxisLimits roll, AxisLimits pitch, AxisLimits yaw,
            AxisLimits surge, AxisLimits sway, AxisLimits heave,
            LegLimits legs = null)
        {
            Legs = legs;
            _limits[(int)Axis.Roll] = roll;
            _limits[(int)Axis.Pitch] = pitch;
            _limits[(int)Axis.Yaw] = yaw;
            _limits[(int)Axis.Surge] = surge;
            _limits[(int)Axis.Sway] = sway;
            _limits[(int)Axis.Heave] = heave;
        }

        public AxisLimits this[Axis axis] => _limits[(int)axis];

        /// <summary>How long each leg may be, or null when not established.</summary>
        public LegLimits Legs { get; }


        private const double Deg = Math.PI / 180.0;

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
        /// <para><b>Position</b> bounds are the data sheet's excursions. Its heave
        /// excursion, +/-0.178 m, is taken about neutral (MBC heave -0.22, which
        /// is negative upwards): neutral is mid-stroke (CDS32830 5.2.2), and the
        /// actuator length limits there (0.020 to 0.320 m of stroke) put the
        /// legs' own ends at heave -0.411 and -0.029, centred on -0.22. Those
        /// numbers come from HexapodGeometry and the retracted joint length
        /// measured on 2026-11-25 (0.7186 m). Each bound holds with the other
        /// axes at neutral only: surge 0.259 with heave -0.398, for one, asks a
        /// leg for 0.44 m of stroke. That is what <see cref="LegLimits"/> catches.</para>
        /// </summary>
        public static MotionEnvelope MoogMbE6Dof { get; } = new MotionEnvelope(
            roll:  new AxisLimits(-21 * Deg, 21 * Deg, 30 * Deg, 1.75),
            pitch: new AxisLimits(-22 * Deg, 22 * Deg, 30 * Deg, 1.75),
            yaw:   new AxisLimits(-22 * Deg, 22 * Deg, 40 * Deg, 1.75),
            surge: new AxisLimits(-0.241, 0.259, 0.5, 2.0),
            sway:  new AxisLimits(-0.259, 0.259, 0.5, 2.0),
            heave: new AxisLimits(-0.398, -0.042, 0.3, 2.0),
            legs: LegLimits.MoogMbE6Dof);
    }
}
