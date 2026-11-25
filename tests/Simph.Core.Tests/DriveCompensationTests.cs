using System;

using Simph.Core.Geometry;
using Simph.Core.Motion;
using Simph.Core.Motion.Compensation;
using Simph.Core.Platform;
using Simph.Core.Time;

using Xunit;

namespace Simph.Core.Tests;

/// <summary>
/// On each leg a share of a command arrives at once and the rest follows a
/// first-order lag. Fitted on the leg lengths of 2026-11-24 and -25: 9.2% and
/// 107.4 ms. Compensated for a pure 96 ms lag instead, the platform reached 99%
/// of the path at 916-924 ms instead of 880. These tests run commands through a
/// simulated filter and drives built on the fitted model, to show that it
/// reproduces that late arrival and that compensating for the direct share
/// removes it, with a command that starts at T0. What the real platform does
/// with the command is measured separately.
/// </summary>
public class DriveCompensationTests
{
    /// <summary>The drives as fitted, standing in for the platform.</summary>
    private static readonly PlatformModel Fitted = new PlatformModel(
        PlatformModel.MoogMbE6Dof.FilterAcceleration,
        TimeSpan.FromMilliseconds(107.4), 0.092,
        HexapodGeometry.MoogMbE6Dof, TimeSpan.FromMilliseconds(1));

    /// <summary>The pure 96 ms lag the platform was compensated with until 2026-11-24.</summary>
    private static readonly PlatformModel PureLag96 = new PlatformModel(
        Fitted.FilterAcceleration, TimeSpan.FromMilliseconds(96), 0.0,
        Fitted.LegGeometry, Fitted.Frame);

    private const int Before = 50;
    private const int After = 900;

    /// <summary>
    /// The MBC's filter on every translation axis, then on each leg the direct
    /// share of the filter's output at once and the rest through the lag, then
    /// back to a pose.
    /// </summary>
    private static Pose6[] ThroughFilterAndLegs(Func<Timestamp, Pose6> command, TimeSpan duration, PlatformModel machine)
    {
        var geometry = machine.LegGeometry;
        int frames = Before + (int)duration.TotalMilliseconds + After;
        double dt = machine.Frame.TotalSeconds;
        double alpha = 1.0 - Math.Exp(-dt / machine.DriveLag.TotalSeconds);
        double share = machine.DriveDirectShare;

        var sent = new Pose6[frames];
        for (int n = 0; n < frames; n++)
            sent[n] = command(Timestamp.Zero + TimeSpan.FromMilliseconds(n - Before));

        Axis[] moving = { Axis.Surge, Axis.Sway, Axis.Heave };
        var filtered = new double[3][];
        for (int a = 0; a < 3; a++)
        {
            var input = new double[frames];
            for (int n = 0; n < frames; n++) input[n] = sent[n][moving[a]];
            filtered[a] = new TrajectoryFilterModel(machine.FilterAcceleration[moving[a]], machine.Frame).Run(input);
        }

        var lagged = geometry.LegLengths(Pose6.Translation(filtered[0][0], filtered[1][0], filtered[2][0]));
        var legs = new double[6];
        var pose = sent[0];
        var platform = new Pose6[frames];

        for (int n = 0; n < frames; n++)
        {
            var wanted = geometry.LegLengths(Pose6.Translation(filtered[0][n], filtered[1][n], filtered[2][n]));

            for (int leg = 0; leg < 6; leg++)
            {
                lagged[leg] += alpha * (wanted[leg] - lagged[leg]);
                legs[leg] = share * wanted[leg] + (1 - share) * lagged[leg];
            }

            pose = geometry.PoseFromLegLengths(legs, pose);
            platform[n] = pose;
        }

        return platform;
    }

    /// <summary>Milliseconds from the plan's start until the platform first reaches this fraction of the path.</summary>
    private static double Reaches(Pose6[] platform, MotionPlan plan, double fraction)
    {
        double dx = plan.Displacement.Surge, dy = plan.Displacement.Sway;
        double lengthSquared = dx * dx + dy * dy;
        double previous = 0;

        for (int n = 0; n < platform.Length; n++)
        {
            double progress = ((platform[n].Surge - plan.From.Surge) * dx + (platform[n].Sway - plan.From.Sway) * dy) / lengthSquared;

            if (n > 0 && previous < fraction && progress >= fraction)
                return n - 1 + (fraction - previous) / (progress - previous) - Before;

            previous = progress;
        }

        return double.NaN;
    }

    private static double PlanReaches(MotionPlan plan, double fraction)
    {
        double low = 0, high = 1;

        for (int i = 0; i < 50; i++)
        {
            double middle = (low + high) / 2;
            if (plan.Profile.Displacement(middle) < fraction) low = middle; else high = middle;
        }

        return low * plan.Duration.TotalMilliseconds;
    }

    [Fact]
    public void Compensated_for_a_pure_96_ms_lag_the_platform_arrives_late_as_it_did_on_the_day()
    {
        var plan = LabSetup.Intended();
        var motion = new CompensatedMotion(plan, PureLag96);

        var platform = ThroughFilterAndLegs(LabSetup.Streamed(motion), plan.Duration, Fitted);

        // Measured on 2026-11-24: middle at 497-501 ms, 99% at 916-924 ms.
        Assert.InRange(Reaches(platform, plan, 0.50), 497, 503);
        Assert.InRange(Reaches(platform, plan, 0.99), 905, 925);
    }

    /// <summary>
    /// Measured against what a pure lag compensated for itself achieves in the
    /// same simulation, so the test is about the direct share alone. That
    /// reference is itself 2.6-2.9 ms late at 99% for 0.9-1 s movements (about
    /// 0.05 mm of path), from the frame-by-frame inversion, not from the drives.
    /// </summary>
    [Theory]
    [InlineData(900)]
    [InlineData(1000)]
    [InlineData(2000)]
    public void Compensated_for_the_direct_share_the_platform_arrives_as_a_pure_lag_compensated_for_itself(int milliseconds)
    {
        var plan = LabSetup.Stimulus(LabSetup.WidestHeadingDegrees, TimeSpan.FromMilliseconds(milliseconds));
        var withShare = new CompensatedMotion(plan, Fitted);
        var pureLag = new CompensatedMotion(plan, PureLag96);

        var platform = ThroughFilterAndLegs(LabSetup.Streamed(withShare), withShare.CommandDuration, Fitted);
        var reference = ThroughFilterAndLegs(LabSetup.Streamed(pureLag), pureLag.CommandDuration, PureLag96);

        foreach (double fraction in new[] { 0.10, 0.50, 0.90, 0.99 })
        {
            double late = Reaches(platform, plan, fraction) - PlanReaches(plan, fraction);
            double referenceLate = Reaches(reference, plan, fraction) - PlanReaches(plan, fraction);

            Assert.True(Math.Abs(late - referenceLate) < 0.5,
                $"{fraction:P0} reached {late:F1} ms off the plan, the pure lag {referenceLate:F1}");
            Assert.True(Math.Abs(late) < 3.5, $"{fraction:P0} reached {late:F1} ms off the plan");
        }
    }

    [Fact]
    public void Compensated_for_the_direct_share_the_platform_stays_on_the_path_and_level()
    {
        var plan = LabSetup.Intended();
        var motion = new CompensatedMotion(plan, Fitted);

        var platform = ThroughFilterAndLegs(LabSetup.Streamed(motion), motion.CommandDuration, Fitted);

        for (int n = 0; n < platform.Length; n++)
        {
            var wanted = plan.Evaluate(Timestamp.Zero + TimeSpan.FromMilliseconds(n - Before));

            foreach (var axis in new[] { Axis.Surge, Axis.Sway, Axis.Heave })
                Assert.True(Math.Abs(platform[n][axis] - wanted[axis]) < 0.0005,
                    $"{axis} {Math.Abs(platform[n][axis] - wanted[axis]) * 1000:F3} mm off at frame {n - Before}");
        }
    }

    /// <summary>
    /// The command starts with the plan and lasts as long: 1000 frames for a
    /// 1 s plan at 1 ms a frame, the first already moving, the last on the target.
    /// </summary>
    [Fact]
    public void The_command_starts_at_t0_and_ends_exactly_on_the_target()
    {
        var plan = LabSetup.Intended();
        var table = new CompensatedMotion(plan, Fitted).Table();

        Assert.Equal(1000, table.Count);
        Assert.Equal(plan.From, table[-1]);
        Assert.NotEqual(plan.From, table[0]);
        Assert.NotEqual(plan.To, table[998]);
        Assert.Equal(plan.To, table[999]);
    }

    /// <summary>
    /// The profile is cut off with a little speed left, so the command leaves rest
    /// and arrives with a step of a fraction of a millimetre, as it always has.
    /// Nothing larger.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.092)]
    public void The_command_never_steps_by_more_than_a_millimetre_between_frames(double share)
    {
        var plan = LabSetup.Intended();
        var model = new PlatformModel(Fitted.FilterAcceleration, Fitted.DriveLag, share, Fitted.LegGeometry, Fitted.Frame);
        var table = new CompensatedMotion(plan, model).Table();

        var previous = table[-5];

        for (int n = -4; n <= table.Count + 5; n++)
        {
            var command = table[n];

            foreach (var axis in Axes.All)
                Assert.True(Math.Abs(command[axis] - previous[axis]) < 0.001,
                    $"{axis} steps {Math.Abs(command[axis] - previous[axis]) * 1000:F2} mm at frame {n}");

            previous = command;
        }
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.0)]
    [InlineData(double.NaN)]
    public void A_direct_share_outside_zero_to_one_is_refused(double share)
    {
        var model = PlatformModel.MoogMbE6Dof;

        Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformModel(model.FilterAcceleration,
            model.DriveLag, share, model.LegGeometry, model.Frame));
    }
}
