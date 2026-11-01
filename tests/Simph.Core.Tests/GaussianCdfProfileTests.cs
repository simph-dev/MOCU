using System;

using Simph.Core.Motion;

using Xunit;

namespace Simph.Core.Tests;

public class GaussianCdfProfileTests
{
    [Fact]
    public void Reaches_both_ends_exactly()
    {
        var profile = LabSetup.Profile();

        Assert.Equal(0.0, profile.Displacement(0.0));
        Assert.Equal(1.0, profile.Displacement(1.0));
    }

    [Fact]
    public void Clamps_outside_the_movement()
    {
        var profile = LabSetup.Profile();

        Assert.Equal(0.0, profile.Displacement(-0.5));
        Assert.Equal(1.0, profile.Displacement(1.5));
    }

    [Fact]
    public void Rises_monotonically()
    {
        var profile = LabSetup.Profile();
        double previous = -1;

        for (int i = 0; i <= 10_000; i++)
        {
            double value = profile.Displacement(i / 10_000.0);
            Assert.True(value >= previous, $"went backwards at tau = {i / 10_000.0}");
            previous = value;
        }
    }

    /// <summary>
    /// The peaks the lab's published method reports for 0.13 m in 1 s: 0.31 m/s
    /// and 1.14 m/s^2. Reproducing them is what says this profile is the same
    /// stimulus as the one in the papers.
    /// </summary>
    [Fact]
    public void Reproduces_the_published_peak_velocity_and_acceleration()
    {
        var plan = LabSetup.Intended(headingDegrees: 0);

        double peakSpeed = plan.PeakSpeed.Surge;
        double peakAcceleration = plan.PeakAcceleration.Surge;

        Assert.Equal(0.31, peakSpeed, 2);
        Assert.Equal(1.14, peakAcceleration, 2);
    }

    /// <summary>
    /// The closed forms for velocity and acceleration have to agree with the
    /// slope of the displacement curve they claim to describe. MoogModule's
    /// TrajectoryProfile_CDF.MaxAcceleration did not: its formula
    /// (d / T^2 * 2 * sigmas * e^-0.5) returns 0.47 m/s^2 for this movement
    /// against an actual 1.14, understating it 2.4-fold. Unused there, but had it
    /// ever reached a limit check it would have passed movements well over the
    /// platform's ceiling.
    /// </summary>
    [Theory]
    [InlineData(0.2)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    [InlineData(0.7)]
    [InlineData(0.8)]
    public void Velocity_matches_the_slope_of_the_displacement(double tau)
    {
        var profile = LabSetup.Profile();
        const double h = 1e-4;

        double numeric = (profile.Displacement(tau + h) - profile.Displacement(tau - h)) / (2 * h);

        Assert.Equal(numeric, profile.VelocityFactor(tau), 3);
    }

    /// <summary>
    /// The step here is 1e-2 rather than 1e-4: a second difference divides by the
    /// step squared, so it magnifies whatever error the displacement carries by
    /// that same factor. The erf behind the curve is an approximation good to
    /// about 1.5e-7, and at a step of 1e-4 that magnifies to a tenth - which
    /// looks like a broken formula but is only the error of the measuring
    /// instrument. At 1e-2 the magnification is ten thousand times smaller and
    /// the truncation of the difference itself, a few thousandths, dominates.
    /// </summary>
    [Theory]
    [InlineData(0.2)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    [InlineData(0.7)]
    [InlineData(0.8)]
    public void Acceleration_matches_the_curvature_of_the_displacement(double tau)
    {
        var profile = LabSetup.Profile();
        const double h = 1e-2;

        double numeric =
            (profile.Displacement(tau + h) - 2 * profile.Displacement(tau) + profile.Displacement(tau - h))
            / (h * h);

        Assert.Equal(numeric, profile.AccelerationFactor(tau), 1);
    }

    /// <summary>
    /// Acceleration passes through zero exactly where speed peaks, in the middle.
    /// That is the whole character of this profile: it pushes hardest a third of
    /// the way in and a third from the end, and coasts through the centre.
    /// </summary>
    [Fact]
    public void Accelerates_through_zero_at_the_midpoint_where_speed_peaks()
    {
        var profile = LabSetup.Profile();

        Assert.Equal(0.0, profile.AccelerationFactor(0.5));
        Assert.Equal(profile.PeakVelocityFactor, profile.VelocityFactor(0.5), 9);

        // Hardest push at one sigma either side of the middle.
        double sigma = 1.0 / (2.0 * LabSetup.Sigmas);
        Assert.Equal(profile.PeakAccelerationFactor, Math.Abs(profile.AccelerationFactor(0.5 - sigma)), 6);
        Assert.Equal(profile.PeakAccelerationFactor, Math.Abs(profile.AccelerationFactor(0.5 + sigma)), 6);
    }

    /// <summary>
    /// A 3-sigma Gaussian does not start from rest - it starts at exp(-4.5) of
    /// its peak, about 1.1%. Small, but not zero, and the platform is standing
    /// still when it arrives. This is what the onset check in MotionValidator
    /// measures.
    /// </summary>
    [Fact]
    public void Starts_at_about_one_percent_of_peak_speed()
    {
        var profile = LabSetup.Profile();

        double fractionOfPeak = profile.VelocityFactor(0) / profile.PeakVelocityFactor;

        Assert.Equal(Math.Exp(-0.5 * LabSetup.Sigmas * LabSetup.Sigmas), fractionOfPeak, 6);
        Assert.InRange(fractionOfPeak, 0.010, 0.012);
    }

    /// <summary>
    /// The difference the rescaling makes, in the units that matter.
    ///
    /// MoogModule's profile returned the raw CDF and special-cased the ends to 0
    /// and 1. The raw CDF at 3 sigmas starts at 0.00135, so the first command
    /// after the start jumped that fraction of the whole displacement in one
    /// millisecond - from a standing platform. At the platform's 2.0 m/s^2
    /// ceiling that step takes tens of milliseconds to absorb, which is the same
    /// order as the ~55 ms lag measured on the surge axis during diagonal
    /// movements. Rescaling instead of clamping leaves a step the platform
    /// absorbs in about two milliseconds.
    /// </summary>
    [Fact]
    public void Rescaling_the_ends_removes_a_step_the_platform_needs_tens_of_milliseconds_to_absorb()
    {
        const double accelerationLimit = 2.0;   // ACC_LIM_X from the MBC configuration

        var plan = LabSetup.Commanded();
        double distance = plan.Displacement.Surge;
        double dt = LabSetup.CommandInterval.TotalSeconds;
        double tau = dt / plan.Duration.TotalSeconds;

        // What MoogModule sent: raw CDF, with only the exact endpoints clamped.
        double legacyStep = RawCdfAtThreeSigmas(tau) * distance;
        double legacyCatchUpMs = legacyStep / dt / accelerationLimit * 1000;

        // What this profile sends.
        double rescaledStep = plan.Profile.Displacement(tau) * distance;
        double rescaledCatchUpMs = rescaledStep / dt / accelerationLimit * 1000;

        Assert.InRange(legacyCatchUpMs, 50, 120);
        Assert.InRange(rescaledCatchUpMs, 0, 5);

        Assert.True(legacyCatchUpMs / rescaledCatchUpMs > 20,
            $"expected the legacy step to be far worse, got {legacyCatchUpMs:F1} ms against {rescaledCatchUpMs:F1} ms");
    }

    /// <summary>The un-rescaled curve, reproduced here only to measure what it cost.</summary>
    private static double RawCdfAtThreeSigmas(double tau)
    {
        double sigma = 1.0 / (2.0 * LabSetup.Sigmas);
        double z = (tau - 0.5) / sigma;

        return 0.5 * (1.0 + Erf(z / Math.Sqrt(2)));
    }

    private static double Erf(double x)
    {
        const double a1 = 0.254829592, a2 = -0.284496736, a3 = 1.421413741;
        const double a4 = -1.453152027, a5 = 1.061405429, p = 0.3275911;

        double sign = x < 0 ? -1.0 : 1.0;
        double z = Math.Abs(x);
        double t = 1.0 / (1.0 + p * z);
        double y = 1.0 - (((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t) * Math.Exp(-z * z);

        return sign * y;
    }
}
