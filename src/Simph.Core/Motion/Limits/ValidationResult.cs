using System.Collections.Generic;
using System.Linq;
using System.Text;

using Simph.Core.Geometry;


namespace Simph.Core.Motion.Limits
{
    public enum IssueSeverity
    {
        /// <summary>Worth recording, nothing wrong.</summary>
        Note,

        /// <summary>Allowed, but close enough to a limit that a small disturbance crosses it.</summary>
        Warning,

        /// <summary>Not to be sent.</summary>
        Rejected
    }


    public sealed class ValidationIssue
    {
        public IssueSeverity Severity { get; }

        /// <summary>The axis concerned, or null when the issue is about the whole plan.</summary>
        public Axis? Axis { get; }

        public string Check { get; }
        public string Message { get; }

        /// <summary>What the plan asks for, and what is allowed, in the axis's own units.</summary>
        public double Value { get; }
        public double Limit { get; }

        public ValidationIssue(IssueSeverity severity, Axis? axis, string check, string message,
            double value, double limit)
        {
            Severity = severity;
            Axis = axis;
            Check = check;
            Message = message;
            Value = value;
            Limit = limit;
        }

        public override string ToString() =>
            Axis.HasValue ? $"[{Severity}] {Axis}: {Message}" : $"[{Severity}] {Message}";
    }


    /// <summary>
    /// The verdict on one plan. Carries every issue found rather than the first,
    /// so a rejection can say everything that is wrong at once - the operator
    /// reads this, and a second round trip to discover a second problem wastes
    /// their time.
    /// </summary>
    public sealed class ValidationResult
    {
        public IReadOnlyList<ValidationIssue> Issues { get; }

        public ValidationResult(IReadOnlyList<ValidationIssue> issues) => Issues = issues;

        public bool Accepted => Issues.All(i => i.Severity != IssueSeverity.Rejected);

        public bool HasWarnings => Issues.Any(i => i.Severity == IssueSeverity.Warning);

        public IEnumerable<ValidationIssue> Rejections =>
            Issues.Where(i => i.Severity == IssueSeverity.Rejected);

        /// <summary>One line per issue, for a log or for the operator's screen.</summary>
        public string Describe()
        {
            if (Issues.Count == 0) return "accepted, no issues";

            var text = new StringBuilder(Accepted ? "accepted" : "REJECTED");

            foreach (var issue in Issues)
                text.Append('\n').Append("  ").Append(issue);

            return text.ToString();
        }

        public override string ToString() => Describe();
    }
}
