using System;


namespace Simph.Core.Motion
{
    /// <summary>
    /// The lab's standard profile: velocity is a Gaussian bell, so displacement
    /// is its integral - the S-shaped normal CDF - and the platform starts and
    /// stops gently with its highest speed in the middle.
    ///
    /// <para><b>Sigmas</b> is how many standard deviations fit in half the
    /// duration. It is the only shape parameter, and it decides how much of the
    /// bell is kept: at 3 the movement begins at 0.1% of peak velocity, which is
    /// what the lab's published heading experiments used.</para>
    ///
    /// <para><b>Renormalisation.</b> The raw CDF does not reach 0 and 1 at the
    /// ends - at 3 sigmas it spans 0.00135 to 0.99865 - so the curve is rescaled
    /// to (C(tau) - C(0)) / (C(1) - C(0)). The previous implementation instead
    /// returned the raw CDF and special-cased the endpoints to 0 and 1, which
    /// left a step of 0.00135 of the whole displacement between the first sample
    /// and the second. Over 0.13 m that is 0.18 mm inside one millisecond, an
    /// implied 0.18 m/s from standstill: far above anything the platform will
    /// follow, so the MBC's trajectory filter clips it. Rescaling removes the
    /// step without altering the shape, so the stimulus is unchanged.</para>
    ///
    /// <para>Even rescaled, the first sample is not free: velocity at tau = 0 is
    /// exp(-sigmas^2 / 2) of peak, reached from rest within one tick. At 3 sigmas
    /// that is 1.1% of peak, and the implied acceleration still exceeds the
    /// platform's configured limit. Raising Sigmas shrinks it quickly, but it
    /// also raises peak velocity and acceleration (as sigmas and sigmas^2), which
    /// changes the stimulus - a question for Adam, not a free fix.</para>
    /// </summary>
    public sealed class GaussianCdfProfile : IMotionProfile
    {
        private const double Sqrt2 = 1.4142135623730951;
        private const double Sqrt2Pi = 2.5066282746310002;
        private const double ExpMinusHalf = 0.6065306597126334;

        /// <summary>Standard deviations in half the duration. 3 in the published experiments.</summary>
        public double Sigmas { get; }

        private readonly double _sigmaTau;   // standard deviation in normalised time
        private readonly double _norm;       // C(1) - C(0), the span the raw CDF actually covers
        private readonly double _cAtZero;    // C(0)

        public GaussianCdfProfile(double sigmas)
        {
            if (!(sigmas > 0) || double.IsInfinity(sigmas))
                throw new ArgumentOutOfRangeException(nameof(sigmas), sigmas, "Sigmas must be finite and positive.");

            Sigmas = sigmas;
            _sigmaTau = 1.0 / (2.0 * sigmas);

            _cAtZero = RawCdf(0.0);
            _norm = RawCdf(1.0) - _cAtZero;

            PeakVelocityFactor = 2.0 * sigmas / (Sqrt2Pi * _norm);
            PeakAccelerationFactor = ExpMinusHalf * 4.0 * sigmas * sigmas / (Sqrt2Pi * _norm);
        }

        public double PeakVelocityFactor { get; }
        public double PeakAccelerationFactor { get; }


        public double Displacement(double tau)
        {
            if (tau <= 0.0) return 0.0;
            if (tau >= 1.0) return 1.0;

            return (RawCdf(tau) - _cAtZero) / _norm;
        }

        public double VelocityFactor(double tau)
        {
            if (tau < 0.0 || tau > 1.0) return 0.0;

            return StandardNormalPdf(Z(tau)) / (_sigmaTau * _norm);
        }

        public double AccelerationFactor(double tau)
        {
            if (tau < 0.0 || tau > 1.0) return 0.0;

            double z = Z(tau);
            return -z * StandardNormalPdf(z) / (_sigmaTau * _sigmaTau * _norm);
        }


        /// <summary>Normalised time expressed in standard deviations from the middle.</summary>
        private double Z(double tau) => (tau - 0.5) / _sigmaTau;

        private double RawCdf(double tau) => 0.5 * (1.0 + Erf(Z(tau) / Sqrt2));

        private static double StandardNormalPdf(double z) => Math.Exp(-0.5 * z * z) / Sqrt2Pi;


        /// <summary>
        /// Abramowitz and Stegun 7.1.26. Absolute error below 1.5e-7, which is far
        /// finer than any margin this is compared against, and monotonic.
        /// </summary>
        private static double Erf(double x)
        {
            const double a1 = 0.254829592;
            const double a2 = -0.284496736;
            const double a3 = 1.421413741;
            const double a4 = -1.453152027;
            const double a5 = 1.061405429;
            const double p = 0.3275911;

            double sign = x < 0 ? -1.0 : 1.0;
            double z = Math.Abs(x);

            double t = 1.0 / (1.0 + p * z);
            double y = 1.0 - (((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t) * Math.Exp(-z * z);

            return sign * y;
        }
    }
}
