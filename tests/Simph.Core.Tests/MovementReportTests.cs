using Simph.Core.Geometry;
using Simph.Core.Motion;
using Simph.Core.Time;

using Simph.MoogProbe;

namespace Simph.Core.Tests;

public class MovementReportTests
{
    private static MotionPlan Plan() => LabSetup.Intended();

    /// <summary>The plan itself, sampled every millisecond for two seconds.</summary>
    private static (double[] seconds, Pose6[] poses) Perfect(MotionPlan plan, Func<int, Pose6>? disturb = null)
    {
        var seconds = Enumerable.Range(0, 2000).Select(n => n / 1000.0).ToArray();
        var poses = Enumerable.Range(0, 2000).Select(n =>
        {
            var pose = plan.Evaluate(new Timestamp(Timestamp.ToTicks(TimeSpan.FromMilliseconds(n))));
            return disturb == null ? pose : pose + disturb(n);
        }).ToArray();

        return (seconds, poses);
    }

    [Fact]
    public void A_platform_that_follows_the_plan_has_no_bend_and_arrives_when_the_plan_does()
    {
        var plan = Plan();
        var (seconds, poses) = Perfect(plan);

        var measured = MovementReport.Measure("out", plan, seconds, poses);

        Assert.True(measured.BendMillimetres < 0.001);
        Assert.True(measured.EndErrorMillimetres < 0.001);
        Assert.Equal(plan.PeakSpeed.Surge / Math.Cos(16 * Math.PI / 180), measured.PeakSpeed, 2);

        for (int i = 0; i < MovementReport.Levels.Length; i++)
            Assert.Equal(measured.PlanMilliseconds[i], measured.PlatformMilliseconds[i], 0);
    }

    [Fact]
    public void Reports_the_published_shape_of_a_one_second_stimulus()
    {
        var plan = Plan();
        var (seconds, poses) = Perfect(plan);

        var times = MovementReport.Measure("out", plan, seconds, poses).PlanMilliseconds;

        Assert.Equal(new[] { 120.0, 287, 500, 713, 880 }, times.Select(t => Math.Round(t)).ToArray());
    }

    /// <summary>A sideways bulge of known size, in the middle of the movement.</summary>
    [Fact]
    public void Measures_a_bend_as_the_distance_from_the_straight_line()
    {
        var plan = Plan();
        const double bulge = 0.003;
        double heading = 16 * Math.PI / 180;

        var (seconds, poses) = Perfect(plan, n =>
        {
            double s = Math.Sin(Math.PI * Math.Clamp(n / 1000.0, 0, 1));
            return Pose6.Translation(-Math.Sin(heading) * bulge * s, Math.Cos(heading) * bulge * s, 0);
        });

        Assert.Equal(3.0, MovementReport.Measure("out", plan, seconds, poses).BendMillimetres, 2);
    }

    [Fact]
    public void A_late_platform_shows_up_as_late_crossings()
    {
        var plan = Plan();
        var (seconds, poses) = Perfect(plan);

        var late = poses.Take(1900).ToArray();
        var shifted = seconds.Skip(100).ToArray();

        var measured = MovementReport.Measure("out", plan, shifted, late);

        Assert.Equal(measured.PlanMilliseconds[2] + 100, measured.PlatformMilliseconds[2], 0);
    }

    [Fact]
    public void Works_on_a_movement_that_comes_back()
    {
        var plan = new MotionPlan(LabSetup.Intended().To, LabSetup.Neutral,
            LabSetup.StimulusDuration, Timestamp.Zero, LabSetup.Profile());
        var (seconds, poses) = Perfect(plan);

        var measured = MovementReport.Measure("back", plan, seconds, poses);

        Assert.True(measured.BendMillimetres < 0.001);
        Assert.Equal(measured.PlanMilliseconds[2], measured.PlatformMilliseconds[2], 0);
    }
}
