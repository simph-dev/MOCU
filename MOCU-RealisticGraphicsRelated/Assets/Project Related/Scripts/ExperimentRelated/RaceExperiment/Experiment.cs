using DaemonsRelated;
using System;
using System.Collections.Generic;
using System.Linq;
using Temporal;
using UnityEngine;
using static Unity.Collections.AllocatorManager;


// todo: class has a lot of problems (center problem for example). for demo only, redo ASAP
namespace RaceExperiment
{
    public class Experiment
    {
        private List<Trial> _trials;
        private Parameters _config;
        private int _difficultyLevel;   // min = 1, max = infinity (higher = harder)
        private int _currentTrialIndex;

        public DateTimeOffset StartedAt;
        public DateTimeOffset FinishedAt;
        public bool HasFinished;

        public Experiment(Parameters config)
        {
            _config = config;
            _trials = new();
            _currentTrialIndex = 0;
            HasFinished = false;
            _difficultyLevel = 0;
        }

        public string GetTrialsData()
        {
            return JsonHelper.SerializeJson(_trials);
        }

        public int GetCurrentTrialIndex()
        {
            return _currentTrialIndex;
        }

        public void GenerateTrials()
        {
            if (_config.TrialsNumber < 8)
                throw new Exception("Number of trials must be at least 8");

            if (_config.TrialsNumber % 8 != 0)
                throw new Exception("Number of trials must be a multiple of 8");

            _trials.Clear();
            _trials.Capacity = _config.TrialsNumber;

            Ethnicity[] colors = new[] { Ethnicity.Eastern, Ethnicity.Western };
            IntervalRole[] types = new[] { IntervalRole.Reference, IntervalRole.Variable };
            Polarity[] directions           = new[] { Polarity.Decrease, Polarity.Increase };

            int blocksOfTrials = _config.TrialsNumber / 8;

            var allCombinations = from c in colors
                                  from t in types
                                  from d in directions
                                  select new Trial(c, t, d);

            for (int i = 0; i < blocksOfTrials; i++)
            {
                var block = allCombinations.ToList();
                Shuffle(block);
                _trials.AddRange(block);
            }
            
            SetTrialAdditionalData();
        }

        public void PrepareNextTrial()
        {
            var currentTrial = _trials[_currentTrialIndex];
            var answerWasCorrect = currentTrial.AnswerIsCorrect;

            if (currentTrial.ReceivedAnswer == TrialAnswer.Timeout)
                LeaveSameDifficulty();
            else if(answerWasCorrect)
                IncreaseDifficulty();
            else
                DecreaseDifficulty();

            _currentTrialIndex += 1;
            SetTrialAdditionalData();
        }

        public void SetParticipantAnswer(TrialAnswer answer)
        {
            _trials[_currentTrialIndex].ReceivedAnswer = answer;
        }

        public Trial StartTrial()
        {
            // todo: temp. move later for more proper place
            if (_currentTrialIndex == 0)
                StartedAt = DateTimeOffset.Now;

            var trial = _trials[_currentTrialIndex];
            trial.StartedAt = DateTimeOffset.Now;
            //Debug.Log($"_difficultyLevel: {_difficultyLevel}");
            //Debug.Log($"right answer: {trial.CorrectAnswer}");
            return trial;
        }

        public void FinishTrial()
        {
            _trials[_currentTrialIndex].FinishedAt = DateTimeOffset.Now;

            if (_currentTrialIndex >= _trials.Count - 1)
            {
                HasFinished = true;
                return;
            }

            PrepareNextTrial();
        }

        public void Save(DateTimeOffset startedAt)
        {
            FinishedAt = DateTimeOffset.Now;
            new SavedData { Trials = _trials, Parameters = _config }.Save(startedAt);
        }

        private void IncreaseDifficulty()
        {
            if (UnityEngine.Random.value <= _config.ChanceToMakeTaskHarder)
                _difficultyLevel++;
        }

        private void DecreaseDifficulty()
        {
            if (_difficultyLevel == 0)
                return;

            if (UnityEngine.Random.value <= _config.ChanceToMakeTaskEasier)
                _difficultyLevel--;

            /*if (UnityEngine.Random.value > _config.ChanceToMakeTaskEasier || _levelOfSimplicity == (int)((_config.Multipliers.Count / 2)) || _levelOfSimplicity == -(int)((_config.Multipliers.Count / 2)))
                LeaveSameDifficulty();
            else
                _levelOfSimplicity = (Math.Abs(_levelOfSimplicity) + 1) * (int)MathUtils.RandomSign();*/
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

        private Ethnicity GetComplementaryColor(Ethnicity color)
        {
            return color == Ethnicity.Eastern ? Ethnicity.Western : Ethnicity.Eastern;
        }

        private IntervalRole GetComplementaryDistanceType(IntervalRole type)
        {
            return type == IntervalRole.Reference ? IntervalRole.Variable : IntervalRole.Reference;
        }

        private void SetTrialAdditionalData()
        {
            var trialData = _trials[_currentTrialIndex];

            if (_config.StimulusType == ExperimentStimulusType.CombinedCombined)
                trialData.SetDistances(referenceDistance: _config.ReferenceDistance, referenceDuration: _config.ReferenceDuration, levelOfDifficulty: _difficultyLevel, baseDiff: _config.TestDistanceMultiplierBaseDiff, ratio: _config.TestDistanceMultiplierRatio);
            else if (_config.StimulusType == ExperimentStimulusType.VisualVisual)
                trialData.SetDurations(referenceDistance: _config.ReferenceDistance, referenceDuration: _config.ReferenceDuration, levelOfDifficulty: _difficultyLevel, baseDiff: _config.TestDurationMultiplierBaseDiff, ratio: _config.TestDurationMultiplierRatio);
            else
                throw new Exception($"Unknown stimulus type: {_config.StimulusType}");
        }
    }
}