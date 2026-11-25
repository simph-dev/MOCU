using System;

using Simph.Core.Geometry;
using Simph.Core.Motion;
using Simph.Core.Motion.Compensation;
using Simph.Core.Platform;
using Simph.Core.Time;

using Xunit;

namespace Simph.Core.Tests;

/// <summary>
/// The drives answer earlier than a pure first-order lag. Fitted on the runs of
/// 2026-11-24: a 106.8 ms lag that answers 9.3 ms early. Compensated for a pure
/// 96 ms lag, the platform reached 99% of the path at 916-924 ms instead of
/// 880. These tests run commands through a simulated filter and drives built on
/// the fitted model, to show that it reproduces that late arrival and that
/// compensating for the lead removes it. What the real platform does with the
/// new command is measured separately.
/// </summary>
public class DriveLeadCompensationTests
{
    /// <summary>The drives as fitted, standing in for the platform.</summary>
    private static readonly PlatformModel Fitted = new PlatformModel(
        PlatformModel.MoogMbE6Dof.FilterAcceleration,
        TimeSpan.FromMilliseconds(106.8), TimeSpan.FromMilliseconds(9.3),
        HexapodGeometry.MoogMbE6Dof, TimeSpan.FromMilliseconds(1));

    /// <summary>The pure 96 ms lag the platform was compensated with until 2026-11-24.</summary>
    private static readonly PlatformModel PureLag96 = new PlatformModel(
        Fitted.FilterAcceleration, TimeSpan.FromMilliseconds(96), TimeSpan.Zero,
        Fitted.LegGeometry, Fitted.Frame);

    private const int Before = 50;
    private const int After = 900;

    /// <summary>
    /// The MBC's filter on every translation axis, then each leg lagged as the
    /// machine model says, answering its lead early, then back to a pose.
    /// </summary>
    private static Pose6[] ThroughFilterAndLegs(Func<Timestamp, Pose6> command, TimeSpan duration, PlatformModel machine)
    {
        var geometry = machine.LegGeometry;
        int frames = Before + (int)duration.TotalMilliseconds + After;
        double dt = machine.Frame.TotalSeconds;
        double alpha = 1.0 - Math.Exp(-dt / machine.DriveLag.TotalSeconds);
        double leadFrames = machine.DriveLead.TotalSeconds / dt;

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

        var wanted = new double[frames][];
        for (int n = 0; n < frames; n++)
            wanted[n] = geometry.LegLengths(Pose6.Translation(filtered[0][n], filtered[1][n], filtered[2][n]));

        var legs = (double[])wanted[0].Clone();
        var pose = sent[0];
        var platform = new Pose6[frames];

        for (int n = 0; n < frames; n++)
        {
            // Answering early: the leg follows the filter's output as it will be
            // leadFrames later, read between frames.
            double at = Math.Min(n + leadFrames, frames - 1);
            int i = (int)Math.Floor(at);
            int j = Math.Min(i + 1, frames - 1);
            double f = at - i;

            for (int leg = 0; leg < 6; leg++)
            {
                double target = wanted[i][leg] * (1 - f) + wanted[j][leg] * f;
                legs[leg] += alpha * (target - legs[leg]);
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

        var platform = ThroughFilterAndLegs(motion.Command, plan.Duration, Fitted);

        // Measured on 2026-11-24: middle at 497-501 ms, 99% at 916-924 ms.
        Assert.InRange(Reaches(platform, plan, 0.50), 497, 503);
        Assert.InRange(Reaches(platform, plan, 0.99), 905, 925);
    }

    /// <summary>
    /// Measured against what a pure lag compensated for itself achieves in the
    /// same simulation, so the test is about the lead alone. That reference is
    /// itself 2.6-2.9 ms late at 99% for 0.9-1 s movements (about 0.05 mm of
    /// path), from the frame-by-frame inversion, not from the drives.
    /// </summary>
    [Theory]
    [InlineData(900)]
    [InlineData(1000)]
    [InlineData(2000)]
    public void Compensated_for_the_lead_too_the_platform_arrives_as_a_pure_lag_compensated_for_itself(int milliseconds)
    {
        var plan = LabSetup.Stimulus(LabSetup.WidestHeadingDegrees, TimeSpan.FromMilliseconds(milliseconds));
        var withLead = new CompensatedMotion(plan, Fitted);
        var pureLag = new CompensatedMotion(plan, PureLag96);

        var platform = ThroughFilterAndLegs(withLead.Command, withLead.CommandDuration, Fitted);
        var reference = ThroughFilterAndLegs(pureLag.Command, pureLag.CommandDuration, PureLag96);

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
    public void Compensated_for_the_lead_the_platform_stays_on_the_path_and_level()
    {
        var plan = LabSetup.Intended();
        var motion = new CompensatedMotion(plan, Fitted);

        var platform = ThroughFilterAndLegs(motion.Command, motion.CommandDuration, Fitted);

        for (int n = 0; n < platform.Length; n++)
        {
            var wanted = plan.Evaluate(Timestamp.Zero + TimeSpan.FromMilliseconds(n - Before));

            foreach (var axis in new[] { Axis.Surge, Axis.Sway, Axis.Heave })
                Assert.True(Math.Abs(platform[n][axis] - wanted[axis]) < 0.0005,
                    $"{axis} {Math.Abs(platform[n][axis] - wanted[axis]) * 1000:F3} mm off at frame {n - Before}");
        }
    }

    [Fact]
    public void The_command_waits_out_the_lead_and_ends_exactly_on_the_target()
    {
        var plan = LabSetup.Intended();
        var motion = new CompensatedMotion(plan, Fitted);

        // 9.3 ms in whole frames.
        Assert.Equal(TimeSpan.FromMilliseconds(9), motion.PlanDelay);
        Assert.Equal(plan.Duration + TimeSpan.FromMilliseconds(9), motion.CommandDuration);

        Assert.Equal(plan.From, motion.Command(plan.T0 + TimeSpan.FromMilliseconds(8)));
        Assert.Equal(plan.To, motion.Command(plan.T0 + motion.CommandDuration));
    }

    /// <summary>
    /// The profile is cut off with a little speed left, so the command leaves rest
    /// with a step of about 0.35 mm, as it always has. Nothing larger: in
    /// particular no spike on the frame before the delayed plan starts.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(9.3)]
    public void The_command_never_steps_by_more_than_a_millimetre_between_frames(double leadMilliseconds)
    {
        var plan = LabSetup.Intended();
        var model = new PlatformModel(Fitted.FilterAcceleration, Fitted.DriveLag,
            TimeSpan.FromMilliseconds(leadMilliseconds), Fitted.LegGeometry, Fitted.Frame);
        var motion = new CompensatedMotion(plan, model);

        var previous = motion.Command(plan.T0 - TimeSpan.FromMilliseconds(5));

        for (int n = -4; n <= (int)motion.CommandDuration.TotalMilliseconds + 5; n++)
        {
            var command = motion.Command(plan.T0 + TimeSpan.FromMilliseconds(n));

            foreach (var axis in Axes.All)
                Assert.True(Math.Abs(command[axis] - previous[axis]) < 0.001,
                    $"{axis} steps {Math.Abs(command[axis] - previous[axis]) * 1000:F2} mm at frame {n}");

            previous = command;
        }
    }

    [Fact]
    public void Leading_by_more_than_the_lag_is_refused()
    {
        var model = PlatformModel.MoogMbE6Dof;

        Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformModel(model.FilterAcceleration,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(11), model.LegGeometry, model.Frame));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformModel(model.FilterAcceleration,
            model.DriveLag, TimeSpan.FromMilliseconds(-1), model.LegGeometry, model.Frame));
    }
}
