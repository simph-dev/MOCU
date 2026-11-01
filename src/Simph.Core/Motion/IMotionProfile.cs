namespace Simph.Core.Motion
{
    /// <summary>
    /// The shape of a movement in normalised time, with no units and no distance:
    /// how far along the path we are at each fraction of the duration.
    ///
    /// All of it is analytic, so a plan can be evaluated at any instant rather
    /// than stepped through a precomputed array. That is what lets the motion
    /// loop and the virtual camera read the same movement from the same source
    /// without sharing any state.
    /// </summary>
    public interface IMotionProfile
    {
        /// <summary>
        /// Fraction of the total displacement covered by normalised time
        /// <paramref name="tau"/>. Exactly 0 at tau = 0 and exactly 1 at tau = 1,
        /// monotonically increasing between them, and clamped outside [0, 1].
        /// </summary>
        double Displacement(double tau);

        /// <summary>d(Displacement)/d(tau). Multiply by distance / duration for m/s.</summary>
        double VelocityFactor(double tau);

        /// <summary>d2(Displacement)/d(tau)2. Multiply by distance / duration^2 for m/s^2.</summary>
        double AccelerationFactor(double tau);

        /// <summary>The largest VelocityFactor over [0, 1].</summary>
        double PeakVelocityFactor { get; }

        /// <summary>The largest magnitude of AccelerationFactor over [0, 1].</summary>
        double PeakAccelerationFactor { get; }
    }
}
