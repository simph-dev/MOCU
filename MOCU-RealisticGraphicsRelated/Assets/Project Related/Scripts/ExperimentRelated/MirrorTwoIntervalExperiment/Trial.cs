using System;

using Newtonsoft.Json;


namespace MirrorTwoIntervalExperiment
{
    public class TrialInterval
    {
        public IntervalKind Kind { get; set; }
        public IntervalRole Role { get; set; }
        public CueSet Cues { get; set; }

        /// Platform travel, meters, straight forward. The camera travels this times
        /// VisualDistanceMultiplier.
        public float Distance { get; set; }

        /// How long the interval actually took, seconds.
        public float MeasuredSeconds { get; set; } = -1f;
    }


    /// Two intervals, both straight forward and the same length of time, one
    /// after the other: one Vestibular, one Combined. One of them is the
    /// Reference, the other the Test, whose distance the staircase sets. The
    /// participant reports which of the two went further.
    public class Trial
    {
        public int Index { get; set; } = -1;

        public TrialInterval FirstInterval { get; private set; }
        public TrialInterval SecondInterval { get; private set; }

        /// Whether the Test is longer or shorter than the Reference.
        public Polarity DirectionOfChange { get; private set; }

        /// Which staircase this trial belongs to: 0, or with a staircase per Test
        /// interval, the Test's kind.
        public int StaircaseIndex { get; private set; }

        public int Difficulty { get; private set; }
        public float Multiplier { get; private set; } = 1f;

        public TrialAnswer CorrectAnswer { get; private set; } = TrialAnswer.None;
        public TrialAnswer ReceivedAnswer { get; set; } = TrialAnswer.None;
        public bool AnswerIsCorrect => ReceivedAnswer == CorrectAnswer;

        public float ResponseTime { get; set; } = 0f;
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset FinishedAt { get; set; } = DateTimeOffset.MinValue;

        [JsonIgnore]
        public TrialInterval TestInterval => FirstInterval.Role == IntervalRole.Test ? FirstInterval : SecondInterval;

        [JsonIgnore]
        public TrialInterval ReferenceInterval => FirstInterval.Role == IntervalRole.Reference ? FirstInterval : SecondInterval;


        public Trial(IntervalKind firstKind, IntervalKind testKind, Polarity direction,
                     CueSet vestibular, CueSet combined, bool staircasePerTestInterval)
        {
            DirectionOfChange = direction;
            StaircaseIndex = staircasePerTestInterval ? (int)testKind : 0;

            IntervalKind secondKind = firstKind == IntervalKind.Vestibular ? IntervalKind.Combined : IntervalKind.Vestibular;

            FirstInterval = MakeInterval(firstKind, testKind, vestibular, combined);
            SecondInterval = MakeInterval(secondKind, testKind, vestibular, combined);
        }

        /// Called right before the trial runs: the Test's distance comes from a
        /// staircase, and so depends on every answer before it.
        public void SetDistances(float referenceDistance, int levelOfDifficulty, float baseDiff, float ratio)
        {
            Difficulty = levelOfDifficulty;
            Multiplier = MathF.Pow(1f + (baseDiff - 1f) * MathF.Pow(ratio, levelOfDifficulty), (int)DirectionOfChange);

            ReferenceInterval.Distance = referenceDistance;
            TestInterval.Distance = referenceDistance * Multiplier;

            CorrectAnswer = FirstInterval.Distance > SecondInterval.Distance
                ? TrialAnswer.FirstInterval
                : TrialAnswer.SecondInterval;
        }

        private static TrialInterval MakeInterval(IntervalKind kind, IntervalKind testKind, CueSet vestibular, CueSet combined)
        {
            return new TrialInterval
            {
                Kind = kind,
                Role = kind == testKind ? IntervalRole.Test : IntervalRole.Reference,
                Cues = kind == IntervalKind.Vestibular ? vestibular : combined
            };
        }
    }
}
