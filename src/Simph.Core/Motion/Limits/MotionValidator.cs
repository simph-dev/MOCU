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
    /// <see cref="Validate(CommandTable)"/> checks a compensated movement: the
    /// plan, what the command asks of the filter, and then every frame that is
    /// going to be sent. That command leads the plan, overshoots its end and
    /// makes the filter work harder than the plan alone would.</para>
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

            CheckLegs(issues, Sampled(plan), "plan");

            return new ValidationResult(issues);
        }


        /// <summary>
        /// Checks a compensated movement in one call: the plan, then the frames
        /// that will be sent. One call so that the command cannot go out with
        /// only the plan checked. A movement whose plan passes can still fail
        /// here: at 0.13 m in 0.8 s the plan needs 1.77 m/s^2 but its compensated
        /// command 2.70.
        /// </summary>
        public ValidationResult Validate(CommandTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var motion = table.Motion;
            var plan = motion.Plan;
            var issues = new List<ValidationIssue>(Validate(plan).Issues);

            // A plan with NaN in it has already been refused, and every number
            // derived from it would be NaN too.
            if (plan.From.HasNonFinite() || plan.To.HasNonFinite())
                return new ValidationResult(issues);

            for (int k = 0; k < table.Count; k++)
            {
                if (table[k].HasNonFinite())
                {
                    issues.Add(new ValidationIssue(IssueSeverity.Rejected, null, "finite",
                        $"frame {k} of the command is not a finite pose: {table[k]}", double.NaN, double.NaN));
                    return new ValidationResult(issues);
                }
            }

            var demand = motion.Measure();

            foreach (var axis in Axes.All)
            {
                var limits = _envelope[axis];
                string unit = Axes.Unit(axis);

                Compare(issues, axis, "filter speed", demand.PeakSpeed[axis], limits.MaxSpeed, unit + "/s");

                // Against the filter's own ACC_LIM, not the lab's limit: above it
                // the filter cannot follow and the compensation stops being exact.
                Compare(issues, axis, "filter acceleration", demand.PeakAcceleration[axis],
                    motion.Model.FilterAcceleration[axis], unit + "/s2");

                CheckCommandRange(issues, axis, table, limits, unit);
                CheckCommandSteps(issues, axis, table, limits, unit);
            }

            CheckLegs(issues, Frames(table), "command");

            return new ValidationResult(issues);
        }


        /// <summary>The plan every millisecond, its end included.</summary>
        private static IEnumerable<Pose6> Sampled(MotionPlan plan)
        {
            var step = TimeSpan.FromMilliseconds(1);

            for (var t = plan.T0; t < plan.End; t += step)
                yield return plan.Evaluate(t);

            yield return plan.To;
        }

        private static IEnumerable<Pose6> Frames(CommandTable table)
        {
            for (int k = 0; k < table.Count; k++)
                yield return table[k];
        }

        /// <summary>
        /// Each leg's length over the poses, against what the legs can do. The
        /// limits per axis cannot see this: they hold with the other axes at
        /// neutral, and combined they can ask a leg for more than its stroke.
        /// </summary>
        private void CheckLegs(ICollection<ValidationIssue> issues, IEnumerable<Pose6> poses, string what)
        {
            var legs = _envelope.Legs;
            if (legs == null) return;

            double shortest = double.PositiveInfinity, longest = double.NegativeInfinity;
            int shortestLeg = 0, longestLeg = 0;
            var lengths = new double[6];

            foreach (var pose in poses)
            {
                legs.Geometry.LegLengths(pose, lengths);

                for (int i = 0; i < 6; i++)
                {
                    if (lengths[i] < shortest) { shortest = lengths[i]; shortestLeg = i; }
                    if (lengths[i] > longest) { longest = lengths[i]; longestLeg = i; }
                }
            }

            if (shortest < legs.ShortestMetres)
                issues.Add(new ValidationIssue(IssueSeverity.Rejected, null, "leg length",
                    $"the {what} needs leg {shortestLeg} at {shortest:F4} m joint to joint, shorter than its " +
                    $"{legs.ShortestMetres:F4} m", shortest, legs.ShortestMetres));

            if (longest > legs.LongestMetres)
                issues.Add(new ValidationIssue(IssueSeverity.Rejected, null, "leg length",
                    $"the {what} needs leg {longestLeg} at {longest:F4} m joint to joint, longer than its " +
                    $"{legs.LongestMetres:F4} m", longest, legs.LongestMetres));
        }


        /// <summary>
        /// The largest step between two frames, as a multiple of what the axis's
        /// speed limit covers in one frame. A sound command never comes near it:
        /// the standard stimulus steps 0.47 mm at most, against 1 mm allowed on
        /// surge. What it catches is a broken command, such as the 2-18 mm jumps
        /// the command made when it was worked out between frames.
        /// </summary>
        private const double StepLimitInFramesOfTopSpeed = 2.0;

        private static void CheckCommandSteps(ICollection<ValidationIssue> issues, Axis axis,
            CommandTable table, AxisLimits limits, string unit)
        {
            if (double.IsInfinity(limits.MaxSpeed)) return;

            double allowed = StepLimitInFramesOfTopSpeed * limits.MaxSpeed * table.Frame.TotalSeconds;
            double largest = 0;
            int at = 0;

            for (int k = 0; k < table.Count; k++)
            {
                double step = Math.Abs(table[k][axis] - table[k - 1][axis]);
                if (step > largest) { largest = step; at = k; }
            }

            if (largest > allowed)
                issues.Add(new ValidationIssue(IssueSeverity.Rejected, axis, "command step",
                    $"the command steps {largest:F5} {unit} in one frame at frame {at}, more than the " +
                    $"{allowed:F5} {unit} allowed", largest, allowed));
        }


        /// <summary>
        /// The compensated command overshoots the plan's end point, so a plan
        /// that stops exactly at a travel limit sends a command beyond it.
        /// </summary>
        private static void CheckCommandRange(ICollection<ValidationIssue> issues, Axis axis,
            CommandTable table, AxisLimits limits, string unit)
        {
            if (!limits.PositionKnown) return;

            double lowest = table.Lowest[axis];
            double highest = table.Highest[axis];

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
