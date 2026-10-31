using System;


namespace MirrorExperiment
{
    /// One trial = one stimulus interval. The participant reports whether the
    /// perceived heading was to the LEFT or to the RIGHT of straight ahead.
    ///
    /// A heading exists at three levels, and all three are kept because they answer
    /// different questions and all three belong in the saved data:
    ///
    ///     Heading                     nominal, from the staircase
    ///       |   split by Delta (combined conditions only)
    ///     VisualHeading               = Heading - Delta/2   still forward-referenced
    ///     VestibularHeading           = Heading + Delta/2
    ///       |   HeadingTransform, per channel
    ///     PhysicalVisualHeading       where the optic flow actually goes
    ///     PhysicalVestibularHeading   where the platform actually carries you
    ///
    /// Worked example - Heading +16, Delta +6, the default transforms (both left
    /// alone):
    ///
    ///     Heading                   = +16     staircase
    ///     VisualHeading             = +13     16 - 6/2
    ///     VestibularHeading         = +19     16 + 6/2
    ///     PhysicalVisualHeading     = +13     the camera - and so both visual fields
    ///     PhysicalVestibularHeading = +19     the platform
    ///
    /// Angle convention: Surge = D*cos(theta), Sway = D*sin(theta). So 0 is straight
    /// ahead, +90 is straight right, 180 is straight back.
    ///
    /// See README.md in this folder for what the experiment measures and why.
    public class Trial
    {
        /// Position in the shuffled running order, so the data keeps its sequence
        /// even if the lines are ever sorted or merged.
        public int Index { get; set; } = -1;

        /// Index into Parameters.Conditions. Identifies which staircase this trial
        /// belongs to, and stays meaningful even if two conditions share settings.
        public int ConditionIndex { get; private set; }

        public ConditionSettings Condition { get; private set; }

        /// Nominal signed heading in degrees, positive = right of straight ahead.
        /// Always expressed in the forward-referenced frame, even though the
        /// two channels may travel in other directions (see HeadingTransform).
        public float Heading { get; private set; }

        public int Difficulty { get; private set; } = 0;

        public float Coherence => Condition.Coherence;
        /// The condition's Delta where there are two cues for it to split - a
        /// visual field and the platform - and 0 everywhere else. A Delta left on a
        /// visual or vestibular condition would otherwise shift its only cue by
        /// half of it, while the answer is still graded against the nominal heading.
        public float Delta => Condition.HasAnyVisual && Condition.HasVestibular ? Condition.Delta : 0f;

        public float VisualHeading => Heading - Delta / 2f;
        public float VestibularHeading => Heading + Delta / 2f;

        /// The two headings after their transforms: the direction the optic flow
        /// simulates, and the direction the platform physically carries the
        /// participant. Equal to VisualHeading / VestibularHeading when the
        /// corresponding transform is left at zero rotation.
        public float PhysicalVisualHeading { get; private set; }
        public float PhysicalVestibularHeading { get; private set; }

        public bool HasVisualInside => Condition.HasVisualInside;
        public bool HasVisualOutside => Condition.HasVisualOutside;
        public bool HasVestibular => Condition.HasVestibular;

        public CorrectAnswerReference CorrectAnswerBasedOn { get; private set; } = CorrectAnswerReference.Nominal;

        /// The heading the correct answer was actually scored against. Recorded
        /// explicitly so the scoring rule is visible in the saved data rather than
        /// only implied by a parameter.
        public float ReferenceHeading { get; private set; }

        public TrialAnswer CorrectAnswer { get; private set; } = TrialAnswer.None;
        public TrialAnswer ReceivedAnswer { get; set; } = TrialAnswer.None;
        public bool AnswerIsCorrect => ReceivedAnswer == CorrectAnswer;

        /// Seconds from the end of the stimulus to the answer: negative for one
        /// given during its last AnswerWindowBeforeEnd. Null on a timeout.
        public float? ResponseTime { get; set; }

        /// How long the stimulus phase actually took, in seconds. Measured rather
        /// than assumed, so "was it really one second?" is answerable from the data.
        public float MeasuredStimulusSeconds { get; set; } = -1f;
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset FinishedAt { get; set; } = DateTimeOffset.MinValue;


        public Trial(int conditionIndex, ConditionSettings condition)
        {
            ConditionIndex = conditionIndex;
            Condition = condition;
        }

        /// Called right before the trial runs, because the heading magnitude comes
        /// from a staircase and therefore depends on all previous answers.
        public void SetHeading(float startHeading, int levelOfDifficulty, float ratio, int sign,
                               CorrectAnswerReference correctAnswerBasedOn,
                               HeadingTransform visualTransform, HeadingTransform vestibularTransform)
        {
            Heading = CalculateHeadingMagnitude(startHeading, levelOfDifficulty, ratio) * sign;
            Difficulty = levelOfDifficulty;
            CorrectAnswerBasedOn = correctAnswerBasedOn;

            PhysicalVisualHeading = visualTransform.Apply(VisualHeading);
            PhysicalVestibularHeading = vestibularTransform.Apply(VestibularHeading);

            // Single-cue scoring uses the direction that cue actually travels, since
            // that is what the participant reports when instructed to go by it alone.
            ReferenceHeading = correctAnswerBasedOn switch
            {
                CorrectAnswerReference.Vestibular => PhysicalVestibularHeading,
                CorrectAnswerReference.Visual => PhysicalVisualHeading,
                _ => Heading
            };

            // Right/left is the sign of the lateral component, not of the angle. For
            // the small forward-referenced headings the two agree, but a rotated
            // platform at 196 degrees carries the participant to the LEFT even
            // though the angle is positive.
            CorrectAnswer = MathF.Sin(ReferenceHeading * MathF.PI / 180f) > 0f
                ? TrialAnswer.Right
                : TrialAnswer.Left;
        }

        private float CalculateHeadingMagnitude(float startHeading, int levelOfDifficulty, float ratio)
        {
            /// Exponential decay toward the impossible task (heading -> 0), same
            /// shape as RaceExperiment.Trial.CalculateMultiplier but with no lower
            /// bound and no fixed table of steps.
            /// [16, 8, 4, 2, 1, 0.5, 0.25, 0.125, ...] for startHeading = 16 and ratio = 0.5
            /// [16, 11.2, 7.84, 5.49, 3.84, 2.69, 1.88, 1.32, ...] for ratio = 0.7
            return startHeading * MathF.Pow(ratio, levelOfDifficulty);
        }
    }
}
