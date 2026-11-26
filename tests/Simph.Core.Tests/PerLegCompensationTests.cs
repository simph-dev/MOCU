using System;

using Simph.Core.Geometry;
using Simph.Core.Motion;
using Simph.Core.Motion.Compensation;
using Simph.Core.Platform;
using Simph.Core.Time;

using Xunit;

namespace Simph.Core.Tests;

/// <summary>
/// The drives lag on each leg's length, not on each axis. On 2026-11-24 the
/// platform, compensated per axis, rose 0.45-0.55 mm at peak speed at every
/// heading, and 0.09-0.13 mm compensated per leg. These tests run commands
/// through a simulated filter and per-leg drives to show that compensating per
/// leg keeps the platform level.
/// </summary>
public class PerLegCompensationTests
{
    /// <summary>
    /// The pure 96 ms lag these measurements were made with, so the tests are
    /// about legs against axes alone; the share that arrives at once has its own tests.
    /// </summary>
    private static readonly PlatformModel PerLeg = new PlatformModel(
        PlatformModel.MoogMbE6Dof.FilterAcceleration, TimeSpan.FromMilliseconds(96), 0.0,
        HexapodGeometry.MoogMbE6Dof, PlatformModel.MoogMbE6Dof.Frame);

    private const int Before = 50;
    private const int After = 600;

    /// <summary>What the 96 ms per-leg machine does with the command, with rest before and after.</summary>
    private static Pose6[] ThroughFilterAndLegs(Func<Timestamp, Pose6> command, TimeSpan duration)
    {
        int frames = Before + (int)duration.TotalMilliseconds + After;

        var sent = new Pose6[frames];
        for (int n = 0; n < frames; n++)
            sent[n] = command(Timestamp.Zero + TimeSpan.FromMilliseconds(n - Before));

        return new PlatformResponse(PerLeg).Predict(sent);
    }

    private static double LargestRiseMillimetres(Pose6[] platform, double heave)
    {
        double largest = 0;
        foreach (var pose in platform) largest = Math.Max(largest, Math.Abs(pose.Heave - heave));
        return largest * 1000;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(90)]
    public void Compensated_per_leg_the_platform_stays_level(double headingDegrees)
    {
        var plan = LabSetup.Stimulus(headingDegrees, LabSetup.StimulusDuration);
        var motion = new CompensatedMotion(plan, PerLeg);

        var platform = ThroughFilterAndLegs(LabSetup.Streamed(motion), plan.Duration);
        double rise = LargestRiseMillimetres(platform, plan.From.Heave);

        Assert.True(rise < 0.02, $"rises {rise:F3} mm");
    }

    [Fact]
    public void Compensated_per_leg_the_platform_follows_the_plan()
    {
        var plan = LabSetup.Intended();
        var motion = new CompensatedMotion(plan, PerLeg);

        var platform = ThroughFilterAndLegs(LabSetup.Streamed(motion), plan.Duration);

        for (int n = 0; n < platform.Length; n++)
        {
            var wanted = plan.Evaluate(Timestamp.Zero + TimeSpan.FromMilliseconds(n - Before));

            foreach (var axis in new[] { Axis.Surge, Axis.Sway })
                Assert.True(Math.Abs(platform[n][axis] - wanted[axis]) < 0.0005,
                    $"{axis} {Math.Abs(platform[n][axis] - wanted[axis]) * 1000:F3} mm off at frame {n - Before}");
        }
    }

    /// <summary>
    /// The command tilts only because the led leg lengths are turned back into a
    /// pose, so the sign of that tilt is the geometry's rotation convention. On
    /// 2026-11-25 a command tilted this way, at most roll -0.116 and pitch
    /// -0.183 mrad, halfway through a 16 degree movement forward, left the
    /// platform at 0.13-0.21 mrad of pitch; the same command tilted the other
    /// way, at 0.40-0.50.
    /// </summary>
    [Fact]
    public void A_forward_movement_is_compensated_with_the_tilt_measured_to_level_the_platform()
    {
        var plan = LabSetup.Intended();
        var motion = new CompensatedMotion(plan, PlatformModel.MoogMbE6Dof);

        var table = motion.Table();
        var halfway = table[(int)(plan.Duration.Ticks / 2 / table.Frame.Ticks)];

        Assert.InRange(halfway.Roll * 1000, -0.126, -0.106);
        Assert.InRange(halfway.Pitch * 1000, -0.193, -0.173);
    }
}
