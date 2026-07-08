namespace MoogModule
{
    // todo: refactor and move to other place
    public static class TrajectoryMath
    {
        public static double CalculateMaxAcceleration(double distanceMeters, double durationSeconds, double sigmaCount = 3)
        {
            const double expMinusHalf = 0.6065306597; // e^(-0.5)
            double scale = (2 * sigmaCount) * expMinusHalf;
            return distanceMeters / (durationSeconds * durationSeconds) * scale;
        }
    }
}