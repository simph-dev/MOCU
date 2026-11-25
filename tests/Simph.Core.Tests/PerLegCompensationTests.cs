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
/// heading; these tests run commands through a simulated filter and per-leg
/// drives to show where that comes from and that compensating per leg removes it.
/// What the real platform does with the per-leg command is not yet measured.
/// </summary>
public class PerLegCompensationTests
{
    private static readonly PlatformModel PerLeg = PlatformModel.MoogMbE6Dof;

    private static readonly PlatformModel PerAxis = new PlatformModel(
        PerLeg.FilterAcceleration, PerLeg.DriveLag, PerLeg.DriveLead, legGeometry: null, PerLeg.Frame);

    private const int Before = 50;
    private const int After = 600;

    /// <summary>
    /// Sends a command stream through the MBC's filter on every translation axis,
    /// turns the filter's output into leg lengths, lags each leg by the drive time
    /// constant and turns the lagged lengths back into the platform's pose.
    /// </summary>
    private static Pose6[] ThroughFilterAndLegs(Func<Timestamp, Pose6> command, TimeSpan duration)
    {
        var geometry = PerLeg.LegGeometry;
        int frames = Before + (int)duration.TotalMilliseconds + After;
        double dt = PerLeg.Frame.TotalSeconds;
        double alpha = 1.0 - Math.Exp(-dt / PerLeg.DriveLag.TotalSeconds);

        var sent = new Pose6[frames];
        for (int n = 0; n < frames; n++)
            sent[n] = command(Timestamp.Zero + TimeSpan.FromMilliseconds(n - Before));

        var filtered = new double[3][];
        Axis[] moving = { Axis.Surge, Axis.Sway, Axis.Heave };
        for (int a = 0; a < 3; a++)
        {
            var input = new double[frames];
            for (int n = 0; n < frames; n++) input[n] = sent[n][moving[a]];
            filtered[a] = new TrajectoryFilterModel(PerLeg.FilterAcceleration[moving[a]], PerLeg.Frame).Run(input);
        }

        var legs = geometry.LegLengths(sent[0]);
        var pose = sent[0];
        var platform = new Pose6[frames];

        for (int n = 0; n < frames; n++)
        {
            var wanted = geometry.LegLengths(Pose6.Translation(filtered[0][n], filtered[1][n], filtered[2][n]));
            for (int i = 0; i < 6; i++) legs[i] += alpha * (wanted[i] - legs[i]);

            pose = geometry.PoseFromLegLengths(legs, pose);
            platform[n] = pose;
        }

        return platform;
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
    public void Compensated_per_axis_the_platform_rises_as_it_did_on_the_day(double headingDegrees)
    {
        var plan = LabSetup.Stimulus(headingDegrees, LabSetup.StimulusDuration);
        var motion = new CompensatedMotion(plan, PerAxis);

        var platform = ThroughFilterAndLegs(motion.Command, plan.Duration);

        // Measured 0.45-0.55 mm; the per-leg model accounts for about 80% of it.
        Assert.InRange(LargestRiseMillimetres(platform, plan.From.Heave), 0.35, 0.55);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(90)]
    public void Compensated_per_leg_the_platform_stays_level(double headingDegrees)
    {
        var plan = LabSetup.Stimulus(headingDegrees, LabSetup.StimulusDuration);
        var motion = new CompensatedMotion(plan, PerLeg);

        var platform = ThroughFilterAndLegs(motion.Command, plan.Duration);
        double rise = LargestRiseMillimetres(platform, plan.From.Heave);

        Assert.True(rise < 0.02, $"rises {rise:F3} mm");
    }

    [Fact]
    public void Compensated_per_leg_the_platform_follows_the_plan()
    {
        var plan = LabSetup.Intended();
        var motion = new CompensatedMotion(plan, PerLeg);

        var platform = ThroughFilterAndLegs(motion.Command, plan.Duration);

        for (int n = 0; n < platform.Length; n++)
        {
            var wanted = plan.Evaluate(Timestamp.Zero + TimeSpan.FromMilliseconds(n - Before));

            foreach (var axis in new[] { Axis.Surge, Axis.Sway })
                Assert.True(Math.Abs(platform[n][axis] - wanted[axis]) < 0.0005,
                    $"{axis} {Math.Abs(platform[n][axis] - wanted[axis]) * 1000:F3} mm off at frame {n - Before}");
        }
    }
}
