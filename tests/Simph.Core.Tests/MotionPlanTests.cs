using System;

using Simph.Core.Geometry;
using Simph.Core.Motion;
using Simph.Core.Time;

using Xunit;

namespace Simph.Core.Tests;

public class MotionPlanTests
{
    [Fact]
    public void Holds_the_start_before_the_movement_and_the_end_after_it()
    {
        var plan = LabSetup.Intended();

        Assert.Equal(plan.From, plan.Evaluate(plan.T0 - TimeSpan.FromSeconds(1)));
        Assert.Equal(plan.From, plan.Evaluate(plan.T0));
        Assert.Equal(plan.To, plan.Evaluate(plan.End));
        Assert.Equal(plan.To, plan.Evaluate(plan.End + TimeSpan.FromSeconds(10)));
    }

    /// <summary>
    /// TrajectoryGenerator_Linear filled its array for i &lt; totalPoints, so the
    /// last sample sat one step short of the end and the platform was never told
    /// to arrive. Evaluating a function instead means the end point is the end
    /// point.
    /// </summary>
    [Fact]
    public void Arrives_exactly_at_the_end_point()
    {
        var plan = LabSetup.Intended();
        var arrived = plan.Evaluate(plan.End);

        Assert.Equal(plan.To.Surge, arrived.Surge, 12);
        Assert.Equal(plan.To.Sway, arrived.Sway, 12);
        Assert.Equal(plan.To.Heave, arrived.Heave, 12);
    }

    [Fact]
    public void Is_halfway_along_at_the_halfway_point()
    {
        var plan = LabSetup.Intended();
        var middle = plan.Evaluate(plan.T0 + TimeSpan.FromSeconds(plan.Duration.TotalSeconds / 2));

        Assert.Equal((plan.From.Surge + plan.To.Surge) / 2, middle.Surge, 9);
    }

    /// <summary>
    /// Every tick asks the same function for the position at the current instant,
    /// so a tick that comes late reads a later position rather than the next
    /// element of a sequence. Two reads at the same instant agree, and the
    /// sequence can never run backwards - both of which the index-walking
    /// TrajectoryManager could get wrong, the Repair strategy by stepping the
    /// index negative once its remaining time reached zero.
    /// </summary>
    [Fact]
    public void Reading_the_same_instant_twice_gives_the_same_position()
    {
        var plan = LabSetup.Intended();
        var at = plan.T0 + TimeSpan.FromMilliseconds(417);

        Assert.Equal(plan.Evaluate(at), plan.Evaluate(at));
    }

    [Fact]
    public void Never_goes_backwards_however_irregularly_it_is_sampled()
    {
        var plan = LabSetup.Commanded();
        var random = new Random(20261101);

        double previous = double.NegativeInfinity;
        var now = plan.T0;

        while (now < plan.End + TimeSpan.FromMilliseconds(50))
        {
            // Jitter of the kind a real 1 kHz loop produces, including stalls.
            now += TimeSpan.FromMilliseconds(random.NextDouble() * 3);

            double surge = plan.Evaluate(now).Surge;
            Assert.True(surge >= previous, $"surge went backwards at {now}");
            previous = surge;
        }
    }

    /// <summary>
    /// Why the compensation factor is not free. Acceleration scales as the
    /// inverse square of the duration, so commanding the movement 20% shorter -
    /// MoogMovementDurationCorrectionFactor of 0.8 - asks for 56% more
    /// acceleration than the stimulus nominally needs.
    /// </summary>
    [Fact]
    public void Shortening_the_commanded_duration_raises_acceleration_as_the_inverse_square()
    {
        var intended = LabSetup.Intended();
        var commanded = LabSetup.Commanded();

        double ratio = commanded.PeakAcceleration.Surge / intended.PeakAcceleration.Surge;

        Assert.Equal(1 / (LabSetup.CorrectionFactor * LabSetup.CorrectionFactor), ratio, 6);
        Assert.Equal(1.5625, ratio, 4);
    }

    /// <summary>
    /// A heading offset splits the movement between the two horizontal axes by
    /// cosine and sine, so at the widest heading surge still carries nearly all
    /// of it and sway a quarter as much. Their accelerations are in the same
    /// proportion, which is why only one of the two axes comes near the limit.
    /// </summary>
    [Fact]
    public void Splits_the_movement_between_surge_and_sway_by_the_heading()
    {
        var plan = LabSetup.Commanded(headingDegrees: LabSetup.WidestHeadingDegrees);
        double heading = LabSetup.WidestHeadingDegrees * Math.PI / 180.0;

        Assert.Equal(LabSetup.Distance * Math.Cos(heading), plan.Displacement.Surge, 9);
        Assert.Equal(LabSetup.Distance * Math.Sin(heading), plan.Displacement.Sway, 9);

        Assert.Equal(Math.Tan(heading), plan.PeakAcceleration.Sway / plan.PeakAcceleration.Surge, 6);
    }

    [Fact]
    public void Refuses_a_duration_of_zero_or_less()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MotionPlan(Pose6.Zero, Pose6.Translation(0.1, 0, 0), TimeSpan.Zero,
                Timestamp.Zero, LabSetup.Profile()));
    }
}
