using System;
using System.Linq;

using Simph.Core.Geometry;
using Simph.Core.Motion;
using Simph.Core.Motion.Limits;
using Simph.Core.Time;

using Xunit;

namespace Simph.Core.Tests;

public class MotionValidatorTests
{
    private static MotionValidator Validator() => new MotionValidator(MotionEnvelope.MoogMbE6Dof);

    private static ValidationIssue? IssueFor(ValidationResult result, Axis axis, string check) =>
        result.Issues.SingleOrDefault(i => i.Axis == axis && i.Check == check);


    /// <summary>
    /// The stimulus as the papers describe it, commanded at its own duration,
    /// sits at a little over half the platform's acceleration ceiling. Nothing to
    /// report.
    /// </summary>
    [Fact]
    public void Accepts_the_stimulus_at_its_intended_duration_without_complaint()
    {
        var result = Validator().Validate(LabSetup.Intended());

        Assert.True(result.Accepted);
        Assert.False(result.HasWarnings, result.Describe());
    }

    [Fact]
    public void The_intended_stimulus_uses_about_half_the_acceleration_available()
    {
        var plan = LabSetup.Intended();
        double fraction = plan.PeakAcceleration.Surge / MotionEnvelope.MoogMbE6Dof[Axis.Surge].MaxAcceleration;

        Assert.InRange(fraction, 0.50, 0.60);
    }

    /// <summary>
    /// The finding this validator was written for. Compensating by commanding the
    /// movement at 0.8 of its duration puts the surge axis at about 85% of the
    /// MBC's 2.0 m/s^2 trajectory-filter limit. The movement is still legal, so
    /// it is not refused - but there is no headroom left for the irregularity of
    /// a real 1 kHz stream, and every irregular step becomes a local spike the
    /// filter clips. That clipping is the braking observed on the surge axis
    /// during diagonal movements.
    /// </summary>
    [Fact]
    public void Warns_that_the_compensated_command_leaves_no_acceleration_headroom_on_surge()
    {
        var result = Validator().Validate(LabSetup.Commanded());

        Assert.True(result.Accepted, "still within the limit, so not refused");

        var issue = IssueFor(result, Axis.Surge, "acceleration");
        Assert.NotNull(issue);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.InRange(issue.Value / issue.Limit, 0.82, 0.90);
    }

    /// <summary>
    /// And why it is surge and not sway. At the widest heading surge carries
    /// 0.125 m of the 0.13 m, sway only 0.036 m, so sway asks for a quarter of
    /// the acceleration and sits far from the limit. The measured lag was ~55 ms
    /// on surge against ~10 ms on sway, in the same direction as this asymmetry.
    /// </summary>
    [Fact]
    public void Says_nothing_about_sway_which_has_room_to_spare()
    {
        var result = Validator().Validate(LabSetup.Commanded());

        Assert.Null(IssueFor(result, Axis.Sway, "acceleration"));

        var plan = LabSetup.Commanded();
        double fraction = plan.PeakAcceleration.Sway / MotionEnvelope.MoogMbE6Dof[Axis.Sway].MaxAcceleration;
        Assert.InRange(fraction, 0.20, 0.30);
    }

    /// <summary>
    /// Straight ahead there is no heading offset, so surge carries the whole
    /// 0.13 m and is the worst case. Even then the compensated command stays
    /// under the limit - only just.
    /// </summary>
    [Fact]
    public void Still_fits_straight_ahead_where_surge_carries_everything()
    {
        var result = Validator().Validate(LabSetup.Commanded(headingDegrees: 0));

        Assert.True(result.Accepted, result.Describe());
        Assert.InRange(LabSetup.Commanded(0).PeakAcceleration.Surge, 1.75, 1.80);
    }

    /// <summary>
    /// Compensating any harder does not fit. At 0.7 the surge acceleration passes
    /// 2.0 m/s^2 and the plan has to be refused rather than silently reshaped by
    /// the filter.
    /// </summary>
    [Fact]
    public void Refuses_a_command_compressed_far_enough_to_pass_the_limit()
    {
        var plan = LabSetup.Stimulus(headingDegrees: 0,
            commandedDuration: LabSetup.StimulusDuration * 0.7);

        var result = Validator().Validate(plan);

        Assert.False(result.Accepted);
        Assert.Contains(result.Rejections, i => i.Axis == Axis.Surge && i.Check == "acceleration");
    }

    [Fact]
    public void Refuses_coordinates_that_are_not_numbers()
    {
        var plan = new MotionPlan(
            LabSetup.Neutral,
            Pose6.Translation(double.NaN, 0, -0.22),
            LabSetup.StimulusDuration, Timestamp.Zero, LabSetup.Profile());

        var result = Validator().Validate(plan);

        Assert.False(result.Accepted);
        Assert.Contains(result.Rejections, i => i.Check == "finite");
    }

    [Fact]
    public void Refuses_an_end_point_outside_the_travel_limits()
    {
        var plan = new MotionPlan(
            LabSetup.Neutral,
            Pose6.Translation(surge: 0.40, sway: 0, heave: -0.22),
            TimeSpan.FromSeconds(4), Timestamp.Zero, LabSetup.Profile());

        var result = Validator().Validate(plan);

        Assert.False(result.Accepted);

        var issue = result.Rejections.Single(i => i.Axis == Axis.Surge && i.Check == "position");
        Assert.Equal(0.259, issue.Limit, 3);
    }

    /// <summary>
    /// Heave is deliberately left unbounded in the envelope: the experiments park
    /// at -0.22 m while the data sheet gives +/- 0.178 m, so the two are measured
    /// from different origins and the offset is not recorded anywhere readable.
    /// Until it is established, the neutral pose must not be refused.
    /// </summary>
    [Fact]
    public void Does_not_refuse_the_neutral_pose_it_actually_parks_at()
    {
        var result = Validator().Validate(LabSetup.Intended());

        Assert.DoesNotContain(result.Issues, i => i.Axis == Axis.Heave && i.Check == "position");
    }

    /// <summary>
    /// With the profile rescaled, the step at the start is small enough that the
    /// platform absorbs it within a couple of milliseconds, so there is nothing
    /// to report. The check still exists to catch a profile that starts further
    /// from rest - which is exactly what the old endpoint clamping produced.
    /// </summary>
    [Fact]
    public void Says_nothing_about_the_start_of_a_rescaled_profile()
    {
        var result = Validator().Validate(LabSetup.Commanded());

        Assert.DoesNotContain(result.Issues, i => i.Check == "onset");
    }

    [Fact]
    public void Warns_when_a_movement_starts_too_far_from_rest()
    {
        // A single sigma keeps so little of the bell that the curve begins at 61%
        // of its peak speed - an extreme stand-in for any profile that does not
        // ease in.
        var plan = new MotionPlan(
            LabSetup.Neutral,
            LabSetup.Neutral + Pose6.Translation(0.13, 0, 0),
            LabSetup.StimulusDuration, Timestamp.Zero, new GaussianCdfProfile(1.0));

        var result = Validator().Validate(plan);
        var issue = IssueFor(result, Axis.Surge, "onset");

        Assert.NotNull(issue);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Reports_every_problem_at_once_rather_than_the_first()
    {
        var plan = new MotionPlan(
            LabSetup.Neutral,
            Pose6.Translation(surge: 0.40, sway: 0.40, heave: -0.22),
            TimeSpan.FromMilliseconds(400), Timestamp.Zero, LabSetup.Profile());

        var result = Validator().Validate(plan);

        Assert.False(result.Accepted);
        Assert.True(result.Rejections.Count() >= 4, result.Describe());
    }
}
