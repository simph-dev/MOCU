using System;
using System.Collections.Generic;
using System.Linq;


namespace MirrorTwoIntervalExperiment
{
    public class Experiment
    {
        private readonly Parameters _config;
        private readonly List<Trial> _trials;

        // One staircase, or one per Test interval kind (Trial.StaircaseIndex).
        // min = 0, max = infinity (higher = harder).
        private readonly int[] _difficultyLevels;

        private int _currentTrialIndex;

        public DateTimeOffset StartedAt;
        public DateTimeOffset FinishedAt;
        public bool HasFinished;


        public Experiment(Parameters config)
        {
            _config = config;
            _trials = new List<Trial>();
            _difficultyLevels = new int[Enum.GetValues(typeof(IntervalKind)).Length];
            _currentTrialIndex = 0;
            HasFinished = false;
        }

        public int GetCurrentTrialIndex() => _currentTrialIndex;

        public int GetTotalTrialsCount() => _trials.Count;

        /// What would stop the run from going as configured, or null. Called when
        /// Start is pressed, so a mistake in the config is reported to the
        /// researcher rather than ending the run halfway through its setup.
        public static string FindProblem(Parameters config)
        {
            if (config.TrialsNumber < 8 || config.TrialsNumber % 8 != 0)
                return $"TrialsNumber must be a positive multiple of 8, not {config.TrialsNumber}";

            if (config.ReferenceDistance <= 0f)
                return "ReferenceDistance must be above 0";

            if (config.TestDistanceMultiplierBaseDiff <= 1f)
                return "TestDistanceMultiplierBaseDiff must be above 1, or the Test never differs from the Reference";

            if (config.TestDistanceMultiplierRatio <= 0f || config.TestDistanceMultiplierRatio >= 1f)
                return "TestDistanceMultiplierRatio must be between 0 and 1";

            foreach (var (name, cues) in new[] { ("Vestibular", DesignCues.Vestibular(config)), ("Combined", DesignCues.Combined(config)) })
            {
                if (cues == null || cues.IsEmpty)
                    return $"The {name} interval of design {config.Design} has no cue enabled";

                if (cues.HasVisualInside && !config.RearView.Enabled)
                    return $"The {name} interval of design {config.Design} shows stars on the screen, but RearView.Enabled is false";
            }

            return null;
        }

        public void GenerateTrials()
        {
            string problem = FindProblem(_config);

            if (problem != null)
                throw new Exception(problem);

            _trials.Clear();
            Array.Clear(_difficultyLevels, 0, _difficultyLevels.Length);

            var vestibular = DesignCues.Vestibular(_config);
            var combined = DesignCues.Combined(_config);

            var kinds = new[] { IntervalKind.Vestibular, IntervalKind.Combined };
            var directions = new[] { Polarity.Decrease, Polarity.Increase };

            for (int block = 0; block < _config.TrialsNumber / 8; block++)
            {
                var trials = (from first in kinds
                              from test in kinds
                              from direction in directions
                              select new Trial(first, test, direction, vestibular, combined, _config.StaircasePerTestInterval))
                             .ToList();

                Shuffle(trials);
                _trials.AddRange(trials);
            }

            for (int i = 0; i < _trials.Count; i++)
                _trials[i].Index = i;

            _currentTrialIndex = 0;
            HasFinished = false;
        }

        /// Sets the Test's distance for the upcoming trial from its staircase.
        public Trial PrepareCurrentTrial()
        {
            var trial = _trials[_currentTrialIndex];

            trial.SetDistances(
                referenceDistance: _config.ReferenceDistance,
                levelOfDifficulty: _difficultyLevels[trial.StaircaseIndex],
                baseDiff: _config.TestDistanceMultiplierBaseDiff,
                ratio: _config.TestDistanceMultiplierRatio);

            return trial;
        }

        /// The furthest any single interval can travel: the Reference times the
        /// largest multiplier, at level 0.
        public static float LongestInterval(Parameters config) =>
            config.ReferenceDistance * Math.Max(1f, config.TestDistanceMultiplierBaseDiff);

        public void MarkTrialStarted()
        {
            if (_currentTrialIndex == 0)
                StartedAt = DateTimeOffset.Now;

            _trials[_currentTrialIndex].StartedAt = DateTimeOffset.Now;
        }

        public void SetParticipantAnswer(TrialAnswer answer)
        {
            _trials[_currentTrialIndex].ReceivedAnswer = answer;
        }

        public void FinishTrial()
        {
            var trial = _trials[_currentTrialIndex];
            trial.FinishedAt = DateTimeOffset.Now;
            FinishedAt = trial.FinishedAt;

            if (trial.ReceivedAnswer == TrialAnswer.Timeout)
            {
                // same difficulty
            }
            else if (trial.AnswerIsCorrect)
                IncreaseDifficulty(trial.StaircaseIndex);
            else
                DecreaseDifficulty(trial.StaircaseIndex);

            if (_currentTrialIndex >= _trials.Count - 1)
            {
                HasFinished = true;
                return;
            }

            _currentTrialIndex++;
        }

        private void IncreaseDifficulty(int staircase)
        {
            if (UnityEngine.Random.value <= _config.ChanceToMakeTaskHarder)
                _difficultyLevels[staircase]++;
        }

        private void DecreaseDifficulty(int staircase)
        {
            if (_difficultyLevels[staircase] == 0)
                return;

            if (UnityEngine.Random.value <= _config.ChanceToMakeTaskEasier)
                _difficultyLevels[staircase]--;
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, list.Count);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
