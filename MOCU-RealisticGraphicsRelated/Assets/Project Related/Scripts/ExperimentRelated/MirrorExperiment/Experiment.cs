using System;
using System.Collections.Generic;

using DaemonsRelated;


namespace MirrorExperiment
{
    public class Experiment
    {
        private readonly Parameters _config;
        private readonly List<Trial> _trials;

        // One staircase per condition, indexed the same way as Parameters.Conditions.
        // The conditions are interleaved but each converges on its own.
        // min = 0, max = infinity (higher = harder).
        private int[] _difficultyLevels;

        private int _currentTrialIndex;

        public DateTimeOffset StartedAt;
        public DateTimeOffset FinishedAt;
        public bool HasFinished;


        public Experiment(Parameters config)
        {
            _config = config;
            _trials = new List<Trial>();
            _difficultyLevels = new int[0];
            _currentTrialIndex = 0;
            HasFinished = false;
        }

        public string GetTrialsData() => JsonHelper.SerializeJson(_trials);

        public int GetCurrentTrialIndex() => _currentTrialIndex;

        public int GetTotalTrialsCount() => _trials.Count;

        public void GenerateTrials()
        {
            if (_config.Conditions == null || _config.Conditions.Count == 0)
                throw new Exception("Parameters.Conditions is empty - nothing to run");

            for (int i = 0; i < _config.Conditions.Count; i++)
                if (_config.Conditions[i].Enabled && !_config.Conditions[i].HasVisual && !_config.Conditions[i].HasVestibular)
                    throw new Exception($"Condition {i} has neither cue enabled - it would run {_config.TrialsPerCondition} blank trials");

            _trials.Clear();

            // Indexed over the full list, not the enabled subset, so a condition's
            // index stays the same in the data whether or not its neighbours ran.
            _difficultyLevels = new int[_config.Conditions.Count];

            for (int conditionIndex = 0; conditionIndex < _config.Conditions.Count; conditionIndex++)
            {
                var condition = _config.Conditions[conditionIndex];

                if (!condition.Enabled)
                    continue;

                for (int i = 0; i < _config.TrialsPerCondition; i++)
                    _trials.Add(new Trial(conditionIndex, condition));
            }

            if (_trials.Count == 0)
                throw new Exception("Every condition is disabled - nothing to run");

            // Pseudo-randomly interleaved: every condition keeps its own staircase,
            // but the order in which conditions come up is shuffled.
            Shuffle(_trials);

            for (int i = 0; i < _trials.Count; i++)
                _trials[i].Index = i;

            _currentTrialIndex = 0;
            HasFinished = false;
        }

        /// Assigns heading and sign to the upcoming trial. Separate from
        /// MarkTrialStarted because the platform has to know the heading before the
        /// stimulus begins.
        public Trial PrepareCurrentTrial()
        {
            var trial = _trials[_currentTrialIndex];

            // The paper picks the heading sign at random on every trial.
            int sign = UnityEngine.Random.value < 0.5f ? -1 : 1;

            trial.SetHeading(
                startHeading: _config.StartHeading,
                levelOfDifficulty: _difficultyLevels[trial.ConditionIndex],
                ratio: _config.HeadingRatio,
                sign: sign,
                correctAnswerBasedOn: _config.CorrectAnswerBasedOn,
                visualTransform: _config.VisualTransform,
                vestibularTransform: _config.VestibularTransform);

            return trial;
        }

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
                LeaveSameDifficulty();
            else if (trial.AnswerIsCorrect)
                IncreaseDifficulty(trial.ConditionIndex);
            else
                DecreaseDifficulty(trial.ConditionIndex);

            if (_currentTrialIndex >= _trials.Count - 1)
            {
                HasFinished = true;
                return;
            }

            _currentTrialIndex++;
        }

        private void IncreaseDifficulty(int conditionIndex)
        {
            if (UnityEngine.Random.value <= _config.ChanceToMakeTaskHarder)
                _difficultyLevels[conditionIndex]++;
        }

        private void DecreaseDifficulty(int conditionIndex)
        {
            if (_difficultyLevels[conditionIndex] == 0)
                return;

            if (UnityEngine.Random.value <= _config.ChanceToMakeTaskEasier)
                _difficultyLevels[conditionIndex]--;
        }

        private void LeaveSameDifficulty()
        {
            //
        }

        // ...........................................

        private void Shuffle<T>(List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, list.Count);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
