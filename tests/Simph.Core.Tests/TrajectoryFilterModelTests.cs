using Simph.Core.Geometry;
using Simph.Core.Motion;
using Simph.Core.Motion.Compensation;
using Simph.Core.Motion.Limits;
using Simph.Core.Platform;
using Simph.Core.Time;

namespace Simph.Core.Tests;

/// <summary>
/// The MBC's trajectory filter as a model. The expected figures come from the
/// recorder run of 2026-11-02, in which the stimulus below went through the real
/// filter: peak speed out of it, and how far its path left the straight line.
/// Holding them here means the model is checked against the hardware rather than
/// against itself.
/// </summary>
public class TrajectoryFilterModelTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1);
    private const double Limit = 2.0;
    private const double DriveLagSeconds = 0.096;

    private static TrajectoryFilterModel Filter() => new TrajectoryFilterModel(Limit, Frame);

    /// <summary>Samples an axis of a plan one frame apart, with rest before and after.</summary>
    private static double[] Sampled(Func<Timestamp, Pose6> pose, TimeSpan duration, Axis axis)
    {
        const int before = 200, after = 1200;
        int count = before + (int)duration.TotalMilliseconds + after;
        var series = new double[count];

        for (int n = 0; n < count; n++)
            series[n] = pose(Timestamp.Zero + TimeSpan.FromMilliseconds(n - before))[axis];

        return series;
    }

    private static double PeakSpeed(double[] series)
    {
        double peak = 0;

        for (int n = 1; n < series.Length; n++)
            peak = Math.Max(peak, Math.Abs(series[n] - series[n - 1]) / Frame.TotalSeconds);

        return peak;
    }

    /// <summary>Largest distance from the straight line between the two ends, in metres.</summary>
    private static double Bend(double[] x, double[] y)
    {
        double x0 = x[0], y0 = y[0];
        double dx = x[^1] - x0, dy = y[^1] - y0, length = Math.Sqrt(dx * dx + dy * dy);
        double worst = 0;

        for (int n = 0; n < x.Length; n++)
            worst = Math.Max(worst, Math.Abs((x[n] - x0) * dy - (y[n] - y0) * dx) / length);

        return worst;
    }

    private static double[] DriveLag(double[] input)
    {
        double alpha = 1.0 - Math.Exp(-Frame.TotalSeconds / DriveLagSeconds);
        var output = new double[input.Length];
        double y = input[0];

        for (int n = 0; n < input.Length; n++)
        {
            y += alpha * (input[n] - y);
            output[n] = y;
        }

        return output;
    }


    [Theory]
    [InlineData(800, 0.3086, 0.1026, 4.44)]
    [InlineData(1000, 0.2646, 0.0840, 3.34)]
    public void Reproduces_what_the_real_filter_did_to_the_stimulus(
        int milliseconds, double surgeSpeed, double swaySpeed, double bendMm)
    {
        var duration = TimeSpan.FromMilliseconds(milliseconds);
        var plan = LabSetup.Stimulus(LabSetup.WidestHeadingDegrees, duration);

        var surge = Filter().Run(Sampled(plan.Evaluate, duration, Axis.Surge));
        var sway = Filter().Run(Sampled(plan.Evaluate, duration, Axis.Sway));

        Assert.Equal(surgeSpeed, PeakSpeed(surge), 3);
        Assert.Equal(swaySpeed, PeakSpeed(sway), 3);
        Assert.Equal(bendMm, Bend(surge, sway) * 1000, 1);
    }

    [Fact]
    public void Lags_a_moving_input_by_the_distance_it_would_need_to_stop()
    {
        var filter = Filter();

        // Steady speed, long enough to settle.
        const double speed = 0.3;
        var input = Enumerable.Range(0, 3000).Select(n => speed * n * Frame.TotalSeconds).ToArray();
        var output = filter.Run(input);

        // The law reads the previous frame on both sides.
        double measured = input[^2] - output[^2];

        Assert.Equal(speed * speed / (2 * Limit) + speed * Frame.TotalSeconds / 2, measured, 6);
        Assert.Equal(filter.Lead(speed), measured, 6);
    }

    /// <summary>
    /// Why a diagonal bends and a straight line does not: twice the speed, four
    /// times the distance behind.
    /// </summary>
    [Fact]
    public void The_lag_grows_with_the_square_of_the_speed()
    {
        var filter = Filter();

        Assert.InRange(filter.Lead(0.3) / filter.Lead(0.15), 3.9, 4.0);
    }

    [Fact]
    public void Gets_there_in_the_end_whatever_the_speed_on_the_way()
    {
        var input = new double[3000];
        for (int n = 100; n < input.Length; n++) input[n] = 0.125;

        Assert.Equal(0.125, Filter().Run(input)[^1], 6);
    }

    /// <summary>
    /// A jump in the input does not reach the output as one: it is taken up at
    /// the configured acceleration, and that is what shields the platform from an
    /// irregular stream.
    /// </summary>
    [Fact]
    public void Never_changes_speed_faster_than_its_limit()
    {
        var input = new double[1000];
        for (int n = 100; n < input.Length; n += 2) input[n] = 0.01;

        var output = Filter().Run(input);

        for (int n = 2; n < output.Length; n++)
        {
            double acceleration = Math.Abs(output[n] - 2 * output[n - 1] + output[n - 2])
                / (Frame.TotalSeconds * Frame.TotalSeconds);

            Assert.True(acceleration <= Limit * 1.0001, $"{acceleration} at frame {n}");
        }
    }


    // --------------------------------------------------------- the compensation

    private static CompensatedMotion Compensated(MotionPlan plan, double driveLagSeconds) =>
        new CompensatedMotion(plan, new PlatformModel(PlatformModel.MoogMbE6Dof.FilterAcceleration,
            TimeSpan.FromSeconds(driveLagSeconds), 0.0, legGeometry: null, Frame));

    /// <summary>
    /// Sent through the same filter, the compensated command comes out as the plan
    /// itself, one frame later, on both axes at once. That is the bend gone.
    /// </summary>
    [Theory]
    [InlineData(800)]
    [InlineData(1000)]
    public void The_filter_turns_the_compensated_command_back_into_the_plan(int milliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(milliseconds);
        var plan = LabSetup.Stimulus(LabSetup.WidestHeadingDegrees, duration);
        var motion = Compensated(plan, 0);

        var surge = Filter().Run(Sampled(LabSetup.Streamed(motion), duration, Axis.Surge));
        var sway = Filter().Run(Sampled(LabSetup.Streamed(motion), duration, Axis.Sway));
        var wanted = Sampled(plan.Evaluate, duration, Axis.Surge);

        Assert.True(Bend(surge, sway) < 0.00005, $"bend {Bend(surge, sway) * 1000:F4} mm");

        for (int n = 1; n < wanted.Length - 1; n++)
            Assert.True(Math.Abs(surge[n + 1] - wanted[n + 1]) < 0.00005, $"frame {n}");
    }

    /// <summary>
    /// Through the filter and the drives together the plan arrives on the platform
    /// itself, which is the point of all this: the specified one-second stimulus,
    /// with no correction factor and no delay.
    /// </summary>
    [Fact]
    public void With_the_drives_compensated_too_the_platform_follows_the_plan()
    {
        var duration = LabSetup.StimulusDuration;
        var plan = LabSetup.Intended();
        var motion = Compensated(plan, DriveLagSeconds);

        var surge = DriveLag(Filter().Run(Sampled(LabSetup.Streamed(motion), duration, Axis.Surge)));
        var sway = DriveLag(Filter().Run(Sampled(LabSetup.Streamed(motion), duration, Axis.Sway)));
        var wanted = Sampled(plan.Evaluate, duration, Axis.Surge);

        Assert.True(Bend(surge, sway) < 0.0001, $"bend {Bend(surge, sway) * 1000:F3} mm");

        for (int n = 0; n < wanted.Length; n++)
            Assert.True(Math.Abs(surge[n] - wanted[n]) < 0.0005, $"{Math.Abs(surge[n] - wanted[n]) * 1000:F3} mm at frame {n}");
    }

    [Fact]
    public void The_stimulus_is_within_what_the_filter_can_deliver()
    {
        var demand = Compensated(LabSetup.Intended(), DriveLagSeconds).Measure();

        Assert.True(demand.PeakAcceleration.Surge < Limit, $"{demand.PeakAcceleration.Surge:F3} m/s^2");
        Assert.True(demand.PeakSpeed.Surge < MotionEnvelope.MoogMbE6Dof[Axis.Surge].MaxSpeed);
    }

    [Fact]
    public void A_movement_too_fast_to_compensate_shows_up_in_the_demand()
    {
        var plan = LabSetup.Stimulus(0, TimeSpan.FromMilliseconds(300));

        Assert.True(Compensated(plan, DriveLagSeconds).Measure().PeakAcceleration.Surge > Limit);
    }

    [Fact]
    public void The_command_overshoots_the_end_point_only_slightly()
    {
        var plan = LabSetup.Intended();
        var table = Compensated(plan, DriveLagSeconds).Table();

        Assert.InRange(table.Highest.Surge - plan.To.Surge, 0, 0.003);
    }

    [Fact]
    public void Starts_and_ends_on_the_plan()
    {
        var plan = LabSetup.Intended();
        var table = Compensated(plan, DriveLagSeconds).Table();

        // The first frame already moves.
        Assert.Equal(plan.From, table[-1]);
        Assert.Equal(plan.To, table[table.Count - 1]);
    }

    [Fact]
    public void Leaves_a_motionless_axis_alone()
    {
        var plan = LabSetup.Intended();
        var table = Compensated(plan, DriveLagSeconds).Table();

        Assert.Equal(plan.From.Heave, table[500].Heave);
    }
}
