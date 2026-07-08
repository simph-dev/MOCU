using System;


namespace MoogModule
{
    public class TrajectoryProfile_CDF : ITrajectoryProfile
    {
        private const float SqrtTwo = 1.414213562f;
        private const double ExpMinusHalf = 0.6065306597; // Approximation of e^(-0.5)
        private readonly float Sigmas;

        public TrajectoryProfile_CDF(TrajectoryProfileSettings_CDF settings)
        {
            Sigmas = settings.Sigmas;
        }

        /// <param name="x">Normalized time in range [0, 1]</param>
        public float NormalizedDisplacement(float x)
        {
            if (x < 0f || x > 1f)
                throw new ArgumentOutOfRangeException(nameof(x), "Time must be in range [0, 1].");

            if (x == 0f) return 0f;
            if (x == 1f) return 1f;

            float mean = 0.5f;
            float sigma = 1.0f / (2 * Sigmas);

            return 0.5f * (1.0f + Erf((x - mean) / (sigma * SqrtTwo)));
        }

        public float InverseNormalizedDisplacement(float y)
        {
            if (y < 0f || y > 1f)
                throw new ArgumentOutOfRangeException(nameof(y), "Displacement must be in range [0, 1].");

            if (y == 0f) return 0f;
            if (y == 1f) return 1f;

            float sigma = 1.0f / (2f * Sigmas);

            return 0.5f + ErfInv(2f * y - 1f) * sigma * SqrtTwo;
        }

        /// <param name="distance">Total physical distance [meters]</param>
        /// <param name="duration">Total duration [seconds]</param>
        /// <param name="sigmas">Number of sigmas within half the bell curve</param>
        /// <returns>Maximum acceleration in meters per second squared</returns>
        public double MaxAcceleration(double distance, double duration)
        {
            double scale = (2 * Sigmas) * ExpMinusHalf;
            return distance / (duration * duration) * scale;
        }

        /// <summary>
        /// Approximation of the error function (erf) using numerical constants.
        /// Based on Abramowitz and Stegun formula 7.1.26.
        /// </summary>
        /// <param name="z">Input value</param>
        /// <returns>Approximate value of erf(x)</returns>
        private float Erf(float z)
        {
            float sign = z < 0 ? -1 : 1;
            z = Math.Abs(z);

            float a1 = 0.254829592f;
            float a2 = -0.284496736f;
            float a3 = 1.421413741f;
            float a4 = -1.453152027f;
            float a5 = 1.061405429f;
            float p = 0.3275911f;

            float t = 1.0f / (1.0f + p * z);
            float y = 1.0f - (((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t) * (float)Math.Exp(-z * z);

            return sign * y;
        }

        /// <summary>
        /// Approximation of the inverse error function erf⁻¹(z), valid for z ∈ (-1, 1).
        /// Uses a fast Winitzki approximation for |z| ≤ 0.8, and a more accurate rational approximation near the edges.
        /// This hybrid approach ensures both speed and accuracy across the full domain.
        /// </summary>
        /// <param name="z">Value in range (-1, 1), where typically z = 2y - 1 for normalized CDF</param>
        /// <returns>Approximate value of erf⁻¹(z)</returns>
        private float ErfInv(float z)
        {
            if (z <= -1f || z >= 1f)
                throw new ArgumentOutOfRangeException(nameof(z), "z must be in (-1, 1)");

            float absZ = Math.Abs(z);
            float result;

            if (absZ <= 0.8f)
            {
                // Winitzki approximation (fast and accurate for central values)
                float a = 0.147f;
                float ln = (float)Math.Log(1f - z * z);
                float first = (2f / (MathF.PI * a)) + (ln / 2f);
                float second = ln / a;
                result = Math.Sign(z) * (float)Math.Sqrt(Math.Sqrt(first * first - second) - first);
            }
            else
            {
                // Improved rational approximation (accurate for |z| > 0.8)
                float ln = (float)Math.Log(1f - absZ * absZ);
                float t = 1f / (float)Math.Sqrt(-ln);
                float p = (((1.641345311f * t + 3.429567803f) * t - 1.62490649f) * t - 1.970840454f) /
                          ((1.637067800f * t + 3.543889200f) * t + 1f);
                result = z < 0f ? -p : p;
            }

            return result;
        }
    }
}