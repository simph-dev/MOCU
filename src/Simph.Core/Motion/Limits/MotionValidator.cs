using System;
using System.Collections.Generic;

using Simph.Core.Geometry;
using Simph.Core.Motion.Compensation;


namespace Simph.Core.Motion.Limits
{
    /// <summary>
    /// Checks a movement against a machine's envelope before anything is sent.
    ///
    /// <para>The MBC has limits of its own, but it never refuses a command: its
    /// trajectory filter delivers what it can, and past its acceleration limit
    /// that is no longer the shape that was asked for. The platform then travels
    /// a different path from the one the experiment believes it commanded, and
    /// nothing in the data says so. For a study measuring perception against a
    /// known stimulus, a silently altered stimulus is worse than a refused one,
    /// so this fails loudly instead.</para>
    ///
    /// <para><see cref="Validate(MotionPlan)"/> checks a plan as it stands.
    /// <see cref="Validate(CompensatedMotion)"/> checks a compensated movement,
    /// the plan and then the command actually sent, which leads the plan,
    /// overshoots its end and makes the filter work harder than the plan alone
    /// would.</para>
    /// </summary>
    public sealed class MotionValidator
    {
        private readonly MotionEnvelope _envelope;
        private readonly double _warnFraction;
        private readonly TimeSpan _onsetCatchUpBudget;

        /// <param name="envelope">The machine's limits.</param>
        /// <param name="warnFraction">
        /// Fraction of a limit above which a plan is allowed but flagged. The
        /// default of 0.8 is a judgement call, not a property of the machine: the
        /// MBC never refuses a command, and its trajectory filter lags any moving
        /// input by v^2 / (2 ACC_LIM), near the limit or not. Past the limit the
        /// output simply cannot keep the shape that was asked for.
        /// </param>
        /// <param name="onsetCatchUpBudget">
        /// How long the platform may trail the command at the start of a movement
        /// before it is worth reporting. The default of 5 ms is half a frame at
        /// 90 Hz: beyond that the inertial stimulus shifts against the visual one
        /// by a visible fraction of a frame, which matters in an experiment that
        /// measures how the two are combined.
        /// </param>
        public MotionValidator(MotionEnvelope envelope, double warnFraction = 0.8,
            TimeSpan? onsetCatchUpBudget = null)
        {
            _envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
            _warnFraction = warnFraction;
            _onsetCatchUpBudget = onsetCatchUpBudget ?? TimeSpan.FromMilliseconds(5);
        }


        public ValidationResult Validate(MotionPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var issues = new List<ValidationIssue>();

            if (plan.From.HasNonFinite() || plan.To.HasNonFinite())
            {
                issues.Add(new ValidationIssue(IssueSeverity.Rejected, null, "finite",
                    $"end points are not finite numbers: {plan.From} -> {plan.To}", double.NaN, double.NaN));

                // Nothing else can be judged once a coordinate is NaN.
                return new ValidationResult(issues);
            }

            var peakSpeed = plan.PeakSpeed;
            var peakAcceleration = plan.PeakAcceleration;
            var onsetSpeed = plan.Velocity(plan.T0);

            foreach (var axis in Axes.All)
            {
                var limits = _envelope[axis];
                string unit = Axes.Unit(axis);

                CheckPosition(issues, axis, plan.From, limits, "start", unit);
                CheckPosition(issues, axis, plan.To, limits, "end", unit);

                Compare(issues, axis, "speed", peakSpeed[axis], limits.MaxSpeed, unit + "/s");
                Compare(issues, axis, "acceleration", peakAcceleration[axis], limits.MaxAcceleration, unit + "/s2");

                CheckOnset(issues, axis, onsetSpeed[axis], limits, unit);
            }

            return new ValidationResult(issues);
        }


        /// <summary>
        /// Checks a compensated movement in one call: the plan, then what is sent.
        /// One call so that the command cannot go out with only the plan checked.
        /// A movement whose plan passes can still fail here: at 0.13 m in 0.8 s the
        /// plan needs 1.77 m/s^2 but its compensated command 2.70.
        /// </summary>
        public ValidationResult Validate(CompensatedMotion motion)
        {
            if (motion == null) throw new ArgumentNullException(nameof(motion));

            var plan = motion.Plan;
            var issues = new List<ValidationIssue>(Validate(plan).Issues);

            // A plan with NaN in it has already been refused, and every number
            // derived from it would be NaN too.
            if (plan.From.HasNonFinite() || plan.To.HasNonFinite())
                return new ValidationResult(issues);

            var demand = motion.Measure();

            foreach (var axis in Axes.All)
            {
                var limits = _envelope[axis];
                string unit = Axes.Unit(axis);

                Compare(issues, axis, "filter speed", demand.PeakSpeed[axis], limits.MaxSpeed, unit + "/s");
                Compare(issues, axis, "filter acceleration", demand.PeakAcceleration[axis], limits.MaxAcceleration, unit + "/s2");

                CheckCommandRange(issues, axis, demand, limits, unit);
            }

            return new ValidationResult(issues);
        }


        /// <summary>
        /// The compensated command overshoots the plan's end point, so a plan
        /// that stops exactly at a travel limit sends a command beyond it.
        /// </summary>
        private static void CheckCommandRange(ICollection<ValidationIssue> issues, Axis axis,
            CompensationDemand demand, AxisLimits limits, string unit)
        {
            if (!limits.PositionKnown) return;

            double lowest = demand.LowestCommand[axis];
            double highest = demand.HighestCommand[axis];

            if (lowest < limits.MinPosition)
                issues.Add(new ValidationIssue(IssueSeverity.Rejected, axis, "command position",
                    $"the command reaches {lowest:F4} {unit}, below the travel limit {limits.MinPosition:F4} {unit}",
                    lowest, limits.MinPosition));

            if (highest > limits.MaxPosition)
                issues.Add(new ValidationIssue(IssueSeverity.Rejected, axis, "command position",
                    $"the command reaches {highest:F4} {unit}, above the travel limit {limits.MaxPosition:F4} {unit}",
                    highest, limits.MaxPosition));
        }


        private void CheckPosition(ICollection<ValidationIssue> issues, Axis axis, Pose6 pose,
            AxisLimits limits, string which, string unit)
        {
            if (!limits.PositionKnown) return;

            double value = pose[axis];

            if (value < limits.MinPosition)
                issues.Add(new ValidationIssue(IssueSeverity.Rejected, axis, "position",
                    $"{which} point {value:F4} {unit} is below the travel limit {limits.MinPosition:F4} {unit}",
                    value, limits.MinPosition));

            else if (value > limits.MaxPosition)
                issues.Add(new ValidationIssue(IssueSeverity.Rejected, axis, "position",
                    $"{which} point {value:F4} {unit} is above the travel limit {limits.MaxPosition:F4} {unit}",
                    value, limits.MaxPosition));
        }


        private void Compare(ICollection<ValidationIssue> issues, Axis axis, string check,
            double value, double limit, string unit)
        {
            if (double.IsInfinity(limit) || value <= 0) return;

            double fraction = value / limit;

            if (fraction > 1.0)
                issues.Add(new ValidationIssue(IssueSeverity.Rejected, axis, check,
                    $"peak {check} {value:F3} {unit} exceeds the limit {limit:F3} {unit} " +
                    $"({fraction * 100:F0}% of it)", value, limit));

            else if (fraction >= _warnFraction)
                issues.Add(new ValidationIssue(IssueSeverity.Warning, axis, check,
                    $"peak {check} {value:F3} {unit} is {fraction * 100:F0}% of the limit " +
                    $"{limit:F3} {unit}", value, limit));
        }


        /// <summary>
        /// A profile whose velocity is not zero where it starts asks the platform,
        /// which is standing still, to be moving at that speed one command later.
        /// The receiver cannot oblige: it absorbs the step at its acceleration
        /// limit, taking dv / aLimit to get there, and trails the command for that
        /// long. The movement still arrives, but its beginning is not the shape
        /// that was commanded.
        ///
        /// <para>The size of the step is set by the profile, not by the distance
        /// or the duration alone. A Gaussian kept to 3 sigmas starts at 1.1% of
        /// its peak speed, which the platform absorbs in about 2 ms - harmless.
        /// The same profile with its ends forced to 0 and 1 instead of rescaled
        /// starts with a jump of 0.135% of the whole displacement in a single
        /// command, two orders of magnitude worse, and the catch-up lands in the
        /// tens of milliseconds.</para>
        /// </summary>
        private void CheckOnset(ICollection<ValidationIssue> issues, Axis axis, double onsetSpeed,
            AxisLimits limits, string unit)
        {
            if (double.IsInfinity(limits.MaxAcceleration)) return;

            double speed = Math.Abs(onsetSpeed);
            if (speed <= 0) return;

            var catchUp = TimeSpan.FromSeconds(speed / limits.MaxAcceleration);

            if (catchUp >= _onsetCatchUpBudget)
                issues.Add(new ValidationIssue(IssueSeverity.Warning, axis, "onset",
                    $"the movement starts at {speed:F4} {unit}/s rather than from rest; absorbing that " +
                    $"at the limit {limits.MaxAcceleration:F3} {unit}/s2 takes " +
                    $"{catchUp.TotalMilliseconds:F1} ms, during which the platform trails the command",
                    catchUp.TotalMilliseconds, _onsetCatchUpBudget.TotalMilliseconds));
        }
    }
}
