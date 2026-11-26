using System;

using Simph.Core.Geometry;


namespace Simph.Core.Platform
{
    /// <summary>
    /// Where the six legs of a Moog 6-DOF platform are attached, and so how long
    /// each leg is for a given pose of the platform, and back.
    ///
    /// <para>Three pairs of joints on the base, three on the platform, the platform
    /// pairs turned 60 degrees from the base pairs. Each leg runs from a base joint
    /// to the nearer joint of the neighbouring platform pair. The numbers come from
    /// the MBC's own configuration (geometry.xml): base joints 0.7874 m out and
    /// 0.1016 m to either side, platform joints 0.5715 m and 0.1016 m, the two
    /// joint planes 0.47752 m apart with the platform settled. That reproduces the
    /// configured leg length at neutral, 0.881 m, to 0.2 mm.</para>
    ///
    /// <para>Poses are in the MBC's DOF terms: surge forward, heave measured from
    /// the settled position and negative upwards, so neutral is heave -0.22. The
    /// MBC's vertical axis points down; the leg lengths are worked out with it
    /// pointing up, and turning that one axis over reverses the sense of roll
    /// and pitch but not of yaw. Measured on 2026-11-25: with roll and pitch
    /// taken the other way, the per-leg compensation of a 16 degree movement
    /// doubled the platform's tilt (pitch 0.40-0.50 mrad) instead of removing
    /// it (0.13-0.21). The sign of yaw and the order of the rotations (yaw, then
    /// pitch, then roll) are still assumed. The layout is symmetric about the
    /// surge axis, so which side is positive sway does not change any leg
    /// length.</para>
    /// </summary>
    public sealed class HexapodGeometry
    {
        private readonly double[] _baseX = new double[6], _baseY = new double[6];
        private readonly double[] _platformX = new double[6], _platformY = new double[6];

        public HexapodGeometry(double baseRadialMetres, double baseTransverseMetres,
            double platformRadialMetres, double platformTransverseMetres, double settledJointSeparationMetres)
        {
            SettledJointSeparationMetres = settledJointSeparationMetres;

            // Base pairs at the back and at 60 degrees either side of the front
            // (actuators A and F at the back, the MBC's default orientation).
            double[] pairDegrees = { 180, 300, 60 };
            int leg = 0;

            foreach (double degrees in pairDegrees)
            {
                double pair = degrees * Math.PI / 180.0;
                double sixty = Math.PI / 3.0;

                Place(leg++, pair, +baseTransverseMetres, pair + sixty, -platformTransverseMetres);
                Place(leg++, pair, -baseTransverseMetres, pair - sixty, +platformTransverseMetres);
            }

            void Place(int i, double baseAngle, double baseSide, double platformAngle, double platformSide)
            {
                _baseX[i] = baseRadialMetres * Math.Cos(baseAngle) - baseSide * Math.Sin(baseAngle);
                _baseY[i] = baseRadialMetres * Math.Sin(baseAngle) + baseSide * Math.Cos(baseAngle);
                _platformX[i] = platformRadialMetres * Math.Cos(platformAngle) - platformSide * Math.Sin(platformAngle);
                _platformY[i] = platformRadialMetres * Math.Sin(platformAngle) + platformSide * Math.Cos(platformAngle);
            }
        }

        /// <summary>The lab's MB-E-6DOF/12/1000, from the MBC's geometry.xml.</summary>
        public static HexapodGeometry MoogMbE6Dof { get; } =
            new HexapodGeometry(0.7874, 0.1016, 0.5715, 0.1016, 0.47752);

        /// <summary>Vertical distance between the joint planes with the platform settled.</summary>
        public double SettledJointSeparationMetres { get; }


        /// <summary>Length of each leg, joint to joint, with the platform at this pose.</summary>
        public double[] LegLengths(Pose6 pose)
        {
            var lengths = new double[6];
            LegLengths(pose, lengths);
            return lengths;
        }

        public void LegLengths(Pose6 pose, double[] lengths)
        {
            // Roll and pitch negated: the MBC's DOF has its vertical axis down,
            // the frame below has it up (see the class comment).
            double cr = Math.Cos(-pose.Roll), sr = Math.Sin(-pose.Roll);
            double cp = Math.Cos(-pose.Pitch), sp = Math.Sin(-pose.Pitch);
            double cy = Math.Cos(pose.Yaw), sy = Math.Sin(pose.Yaw);
            double height = SettledJointSeparationMetres - pose.Heave;

            for (int i = 0; i < 6; i++)
            {
                double px = _platformX[i], py = _platformY[i];

                double x = cy * cp * px + (cy * sp * sr - sy * cr) * py + pose.Surge;
                double y = sy * cp * px + (sy * sp * sr + cy * cr) * py + pose.Sway;
                double z = -sp * px + cp * sr * py + height;

                double dx = x - _baseX[i], dy = y - _baseY[i];
                lengths[i] = Math.Sqrt(dx * dx + dy * dy + z * z);
            }
        }


        /// <summary>
        /// The pose that gives these leg lengths, found by Newton's method from a
        /// nearby guess. There is no closed form for a Stewart platform; from a
        /// guess within a few centimetres it converges in two or three steps.
        /// Lengths no pose can make, or a guess too far away, throw rather than
        /// return a pose that does not fit.
        /// </summary>
        public Pose6 PoseFromLegLengths(double[] lengths, Pose6 guess)
        {
            if (lengths == null || lengths.Length != 6) throw new ArgumentException("Six leg lengths are needed.", nameof(lengths));

            const double step = 1e-7;
            const int maxIterations = 20;
            const double toleranceMetres = 1e-12;

            // A pose whose legs are off by more than a nanometre is not an answer;
            // a converged one is off by about 1e-15 m.
            const double acceptedMetres = 1e-9;

            var pose = guess;
            var current = new double[6];
            var shifted = new double[6];
            var jacobian = new double[6, 7];

            for (int iteration = 0; ; iteration++)
            {
                LegLengths(pose, current);

                double worst = 0;
                for (int i = 0; i < 6; i++)
                    worst = Math.Max(worst, Math.Abs(current[i] - lengths[i]));

                if (worst < toleranceMetres) break;

                // Math.Max carries a NaN through, and NaN fails this test too.
                if (iteration == maxIterations)
                {
                    if (worst < acceptedMetres) break;
                    throw new ArithmeticException(
                        $"No pose found for these leg lengths: a leg still off by {worst:G3} m after {maxIterations} steps.");
                }

                for (int j = 0; j < 6; j++)
                {
                    var axis = (Axis)j;
                    LegLengths(pose.With(axis, pose[axis] + step), shifted);

                    for (int i = 0; i < 6; i++)
                        jacobian[i, j] = (shifted[i] - current[i]) / step;
                }

                for (int i = 0; i < 6; i++)
                    jacobian[i, 6] = current[i] - lengths[i];

                var correction = Solve(jacobian);

                for (int j = 0; j < 6; j++)
                {
                    var axis = (Axis)j;
                    pose = pose.With(axis, pose[axis] - correction[j]);
                }
            }

            return pose;
        }

        /// <summary>Gaussian elimination with partial pivoting on a 6 x 7 augmented matrix.</summary>
        private static double[] Solve(double[,] m)
        {
            const int n = 6;

            for (int column = 0; column < n; column++)
            {
                int pivot = column;
                for (int row = column + 1; row < n; row++)
                    if (Math.Abs(m[row, column]) > Math.Abs(m[pivot, column])) pivot = row;

                for (int k = 0; k <= n; k++)
                {
                    double swap = m[column, k];
                    m[column, k] = m[pivot, k];
                    m[pivot, k] = swap;
                }

                for (int row = 0; row < n; row++)
                {
                    if (row == column) continue;
                    double factor = m[row, column] / m[column, column];
                    for (int k = column; k <= n; k++) m[row, k] -= factor * m[column, k];
                }
            }

            var x = new double[n];
            for (int i = 0; i < n; i++) x[i] = m[i, n] / m[i, i];
            return x;
        }
    }
}
