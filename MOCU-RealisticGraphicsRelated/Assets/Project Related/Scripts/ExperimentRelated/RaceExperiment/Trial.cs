using System;


namespace RaceExperiment
{
    public class Trial
    {
        public TrialInterval FirstInterval { get; private set; }
        public TrialInterval SecondInterval { get; private set; }

        public Polarity DirectionOfChange { get; private set; } = Polarity.Maintain;
        public int Difficulty { get; private set; } = 0;
        public float ResponseTime { get; set; } = 0;

        public Trial(Ethnicity firstColor, IntervalRole firstType, Polarity direction)
        {
            DirectionOfChange = direction;

            FirstInterval = new TrialInterval
            {
                ModelEthnicity = firstColor,
                Role = firstType,
                Distance = 0,
                Duration = TimeSpan.Zero
            };

            SecondInterval = new TrialInterval
            {
                ModelEthnicity = GetOppositeColor(firstColor),
                Role = GetOppositeType(firstType),
                Distance = 0,
                Duration = TimeSpan.Zero
            };
        }

        public void SetDistances(float referenceDistance, TimeSpan referenceDuration, int levelOfDifficulty, float baseDiff, float ratio)
        {
            var testInterval = FirstInterval.Role == IntervalRole.Variable
                ? FirstInterval
                : SecondInterval;

            var referenceInterval = FirstInterval.Role == IntervalRole.Reference
                ? FirstInterval
                : SecondInterval;

            float multiplier = CalculateMultiplier(levelOfDifficulty, (int)DirectionOfChange, baseDiff, ratio);

            referenceInterval.Distance = referenceDistance;
            testInterval.Distance = referenceDistance * multiplier;
            Difficulty = levelOfDifficulty;

            CorrectAnswer = FirstInterval.Distance > SecondInterval.Distance
                ? TrialAnswer.FirstInterval
                : TrialAnswer.SecondInterval;

            referenceInterval.Duration = referenceDuration;
            testInterval.Duration = referenceDuration;
        }

        public void SetDurations(float referenceDistance, TimeSpan referenceDuration, int levelOfDifficulty, float baseDiff, float ratio)
        {
            var testInterval = FirstInterval.Role == IntervalRole.Variable
                ? FirstInterval
                : SecondInterval;

            var referenceInterval = FirstInterval.Role == IntervalRole.Reference
                ? FirstInterval
                : SecondInterval;

            float multiplier = CalculateMultiplier(levelOfDifficulty, (int)DirectionOfChange, baseDiff, ratio);

            referenceInterval.Duration = referenceDuration;
            testInterval.Duration = referenceDuration * multiplier;
            Difficulty = levelOfDifficulty;

            CorrectAnswer = FirstInterval.Duration > SecondInterval.Duration
                ? TrialAnswer.FirstInterval
                : TrialAnswer.SecondInterval;

            referenceInterval.Distance = referenceDistance;
            testInterval.Distance = referenceDistance;
        }

        private float CalculateMultiplier(int levelOfDifficulty, int polarity, float baseDiff, float ratio)
        {
            float multiplier;

            /// Double Exponential Decay [2, 1.414, 1.189, 1.091, 1.044, 1.022, 1.011, 1.005, 1.003, 1.001] (for baseDiff = 2 and ratio = 0.5)
            /// Double Exponential Decay [2, 1.625, 1.404, 1.268, 1.181, 1.124, 1.085, 1.059, 1.041, 1.028] (for baseDiff = 2 and ratio = 0.7)
            //multiplier = MathF.Pow(baseDiff, MathF.Pow(ratio, levelOfDifficulty));

            /// Shifted Exponential Decay [2, 1.500, 1.250, 1.125, 1.063, 1.031, 1.016, 1.008, 1.004, 1.002] (for baseDiff = 2 and ratio = 0.5)
            /// Shifted Exponential Decay [2, 1.700, 1.490, 1.343, 1.240, 1.168, 1.118, 1.082, 1.058, 1.040] (for baseDiff = 2 and ratio = 0.7)
            multiplier = 1f + (baseDiff - 1f) * MathF.Pow(ratio, levelOfDifficulty);

            return MathF.Pow(multiplier, polarity);
        }


        private Ethnicity GetOppositeColor(Ethnicity color) =>
        color == Ethnicity.Eastern ? Ethnicity.Western : Ethnicity.Eastern;

        private IntervalRole GetOppositeType(IntervalRole type) =>
            type == IntervalRole.Reference ? IntervalRole.Variable : IntervalRole.Reference;


        public TrialAnswer CorrectAnswer   { get; private set; } = TrialAnswer.None;
        public TrialAnswer ReceivedAnswer  { get; set; } = TrialAnswer.None;
        public bool AnswerIsCorrect => ReceivedAnswer == CorrectAnswer;
        public DateTimeOffset StartedAt     { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset FinishedAt    { get; set; } = DateTimeOffset.MinValue;
    }
}