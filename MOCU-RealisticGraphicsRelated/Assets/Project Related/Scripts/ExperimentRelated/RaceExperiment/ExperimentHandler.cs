using MoogModule;
using System;
using System.Collections;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Temporal;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.HighDefinition;


namespace RaceExperiment
{
    public class ExperimentHandler : ManagedMonoBehaviour
    {
        private Parameters _parameters;
        private Experiment _experiment;
        //private TemporalResponseHandler _input;
        private TemporalSound _sound;
        private CrowdSpawner _scene;
        private DebugTabHandler _debugTabHandler;
        private ExperimentTabHandler _experimentTabHandler;
        private MoogHandler _moogHandler;
        private InputHandler _input;

        private Temporal2IntervalTrialState _trialState;
        private Temporal2IntervalExperimentState _experimentState;
        private DofParameters _whereCameraShouldBe;
        private Trial _currentTrial;
        private Transform _cameras;
        private Transform _vrCamera;

        private Vector3 _manualOffset = Vector3.zero;
        private float _yRotationOffset = 0f;


        public override void ManagedAwake()
        {
            _parameters = new Parameters {
                TrialsNumber = 120,
                ReferenceDistance = 0,
                StimulusType = ExperimentStimulusType.VisualVisual,
                DistanceMultiplier = 2.5f,
                BackwardMovementDuration = TimeSpan.FromSeconds(1),
            };
            _experiment = new Experiment(_parameters);

            _debugTabHandler = GetComponent<DebugTabHandler>();
            _experimentTabHandler = GetComponent<ExperimentTabHandler>();
            _moogHandler = GetComponent<MoogHandler>();
            _input = GetComponent<InputHandler>();
            //_input = GetComponent<TemporalResponseHandler>();
            _sound = GameObject.Find("Audio").GetComponent<TemporalSound>();
            _scene = GameObject.Find("Script").GetComponent<CrowdSpawner>();

            _trialState = Temporal2IntervalTrialState.None;
            _experimentState = Temporal2IntervalExperimentState.None;
            _whereCameraShouldBe = _parameters.CameraStartPosition;
            _cameras = GameObject.Find("Cameras").transform;
            _vrCamera = _cameras.Find("VrHelmetCamera");
            _currentTrial = null;

            _input.GotAnswer_Up += HandleInput_Up;
            _input.GotAnswer_Down += HandleInput_Down;
            _input.GotSignal_Start += HandleInput_Start;

            _input.HeadTotalCalibrationRequest += CalibrateHead;
            _input.HeadCalibrationRequest += CalibrateHeadRotation;
            _input.HeightCalibrationRequest += CalibrateHeadPosition;

            _input.ToggleNoise += _sound.ToggleWhiteNoise;

            /*_debugTabHandler.testBtn1Clicked += (eventObj) => EngageMoog();
            _debugTabHandler.testBtn2Clicked += (eventObj) => StartExperiment();*/

            _experimentTabHandler.EngageBtnClicked += (eventObj) => EngageMoog();
            _experimentTabHandler.ParkBtnClicked += (eventObj) => DisengageMoog();
            _experimentTabHandler.StartExperimentBtnClicked += (eventObj) => StartExperiment();
            _experimentTabHandler.StoptExperimentBtnClicked += (eventObj) => StartCoroutine(StopExperiment());
        }

        public override void ManagedUpdate()
        {
            Vector3 basePosition = new Vector3
            {
                z = _whereCameraShouldBe.Surge,
                y = _parameters.CameraStartPosition.Heave,
                x = _parameters.CameraStartPosition.Sway
            };

            _cameras.rotation = Quaternion.Euler(0, _yRotationOffset, 0);
            _cameras.position = basePosition + _manualOffset;

            if (Time.frameCount % 30 == 0)
                PrintStateInfo();
        }

        // ..........................................................

        public void CalibrateHead()
        {
            _yRotationOffset = -_vrCamera.localEulerAngles.y;
            Quaternion targetRot = Quaternion.Euler(0, _yRotationOffset, 0);
            _manualOffset = -(targetRot * _vrCamera.localPosition);

            ManagedUpdate();
            Debug.Log("Head was calibrated");
        }

        public void CalibrateHeadRotation()
        {
            Vector3 currentWorldEyePos = _cameras.position + (_cameras.rotation * _vrCamera.localPosition);
            _yRotationOffset = -_vrCamera.localEulerAngles.y;
            Vector3 basePos = new Vector3(_whereCameraShouldBe.Sway, _parameters.CameraStartPosition.Heave, _whereCameraShouldBe.Surge);
            Quaternion nextRot = Quaternion.Euler(0, _yRotationOffset, 0);
            _manualOffset = currentWorldEyePos - basePos - (nextRot * _vrCamera.localPosition);

            ManagedUpdate();
            Debug.Log("Head roration was calibrated");
        }

        public void CalibrateHeadPosition()
        {
            Quaternion currentRot = Quaternion.Euler(0, _yRotationOffset, 0);
            _manualOffset = -(currentRot * _vrCamera.localPosition);

            ManagedUpdate();
            Debug.Log("Head position was calibrated");
        }

        private void EngageMoog()
        {
            if (_experimentState != Temporal2IntervalExperimentState.None)
            {
                _experimentTabHandler.PrintToWarnings("Can't Engage now\n");
                return;
            }
            _moogHandler.Engage();
        }

        private void DisengageMoog()
        {
            if (_experimentState == Temporal2IntervalExperimentState.Started)
            {
                _experimentTabHandler.PrintToWarnings("Can't Disengage now\n");
                return;
            }

            _moogHandler.Disengage();
        }

        /*private IEnumerator ParkMoog()
        {
            yield return MoveMoogToOriginPosition();
            DisengageMoog();
        }*/

        private void StartExperiment()
        {
            if (_experimentState == Temporal2IntervalExperimentState.Started)
            {
                _experimentTabHandler.PrintToWarnings("Experiment is already started");
                return;
            }

            CanUseUpdateMethod = true;
            CalibrateHeadPosition();
            StartCoroutine(Loop());
        }

        private IEnumerator StopExperiment()
        {
            if (_experimentState != Temporal2IntervalExperimentState.Started)
            {
                _experimentTabHandler.PrintToWarnings("Experiment is not started\n");
                yield break;
            }

            if (_trialState != Temporal2IntervalTrialState.None && _trialState != Temporal2IntervalTrialState.ReadyToStart)
            {
                _experimentTabHandler.PrintToWarnings("Trial is still running!\n");
                yield break;
            }

            _trialState = Temporal2IntervalTrialState.None;
            yield return MoveMoogToOriginPosition();
            _experimentState = Temporal2IntervalExperimentState.Finished;
            Debug.Log("Experiment finished");
        }

        private IEnumerator Loop()
        {
            Debug.Log("Experiment started");
            _experimentState = Temporal2IntervalExperimentState.Started;
            _experiment.GenerateTrials();
            yield return MoveMoogToStartPosition();

            while (!_experiment.HasFinished)
            {
                yield return Initializing();
                // here waiting for 'start btn' event
                yield return WaitingToStartSignal();
                yield return FirstInterval();
                yield return InterIntervalPause();
                yield return SecondInterval();
                yield return AnswerPhase();
                yield return PreReturningPause();
                yield return Returning();
                yield return Analyzation();
                _experiment.Save(_experiment.StartedAt);
            }

            _trialState = Temporal2IntervalTrialState.None;
        }

        private IEnumerator MoveMoogToStartPosition()
        {
            if (_parameters.StimulusType != ExperimentStimulusType.CombinedCombined)
                yield break;

            var trajectoryParameters = new MoveByTrajectoryParameters
            {
                StartPoint = new DofParameters { Heave = -0.22f },
                EndPoint = _parameters.MoogStartPosition,
                MovementDuration = TimeSpan.FromSeconds(2),
                TrajectoryType = TrajectoryType.Linear,
                TrajectoryProfile = TrajectoryProfile.CDF,
                DelayHandling = DelayCompensationStrategy.Ignore,
                TrajectoryTypeSettings = new TrajectoryTypeSettings { Linear = new TrajectoryTypeSettings_Linear { } },
                TrajectoryProfileSettings = new TrajectoryProfileSettings { CDF = new TrajectoryProfileSettings_CDF { Sigmas = 3 } }
            };

            _moogHandler.MoveByTrajectory(trajectoryParameters);
            yield return new WaitForSeconds(3);
        }

        private IEnumerator MoveMoogToOriginPosition()
        {
            if (_parameters.StimulusType != ExperimentStimulusType.CombinedCombined)
                yield break;

            var trajectoryParameters = new MoveByTrajectoryParameters
            {
                StartPoint = _parameters.MoogStartPosition,
                EndPoint = new DofParameters { Heave = -0.22f },
                MovementDuration = TimeSpan.FromSeconds(2),
                TrajectoryType = TrajectoryType.Linear,
                TrajectoryProfile = TrajectoryProfile.CDF,
                DelayHandling = DelayCompensationStrategy.Ignore,
                TrajectoryTypeSettings = new TrajectoryTypeSettings { Linear = new TrajectoryTypeSettings_Linear { } },
                TrajectoryProfileSettings = new TrajectoryProfileSettings { CDF = new TrajectoryProfileSettings_CDF { Sigmas = 3 } }
            };

            _moogHandler.MoveByTrajectory(trajectoryParameters);
            yield return new WaitForSeconds(3);
        }

        private IEnumerator Initializing()
        {
            _trialState = Temporal2IntervalTrialState.Initializing;

            yield return new WaitForSeconds((float)_parameters.DelayBeforeStartSound.TotalSeconds);

            _sound.PlaySound_Start();
            _trialState = Temporal2IntervalTrialState.ReadyToStart;
        }

        private IEnumerator WaitingToStartSignal()
        {
            while (_trialState != Temporal2IntervalTrialState.PreFirstIntervalPause)
                yield return null;

            yield return new WaitForSeconds((float)_parameters.DelayAfterStartSignal.TotalSeconds);
            _currentTrial = _experiment.StartTrial();

            _trialState = Temporal2IntervalTrialState.FirstInterval;
        }

        private IEnumerator FirstInterval()
        {
            // resolves visual glitch of sudden tp
            _whereCameraShouldBe = new DofParameters { Surge = 0 };
            yield return null;

            if (_currentTrial.FirstInterval.ModelEthnicity == Ethnicity.Western)
                _scene.ShowWestern();
            else if (_currentTrial.FirstInterval.ModelEthnicity == Ethnicity.Eastern)
                _scene.ShowEastern();
            else
                Debug.Log("You shouldn't see that message");

            yield return new WaitForSeconds((float)_parameters.PauseBetweenSeeingAndMovingDuration.TotalSeconds);

            if (_parameters.StimulusType == ExperimentStimulusType.CombinedCombined)
            {
                var toPoint = _parameters.MoogStartPosition;
                toPoint.Surge += _currentTrial.FirstInterval.Distance;

                var trajectoryParameters = new MoveByTrajectoryParameters
                {
                    StartPoint = _parameters.MoogStartPosition,
                    EndPoint = toPoint,
                    MovementDuration = _currentTrial.FirstInterval.Duration * _parameters.MoogMovementDurationCorrectionFactor,
                    TrajectoryType = TrajectoryType.Linear,
                    TrajectoryProfile = TrajectoryProfile.CDF,
                    DelayHandling = DelayCompensationStrategy.Ignore,
                    TrajectoryTypeSettings = new TrajectoryTypeSettings { Linear = new TrajectoryTypeSettings_Linear { } },
                    TrajectoryProfileSettings = new TrajectoryProfileSettings { CDF = new TrajectoryProfileSettings_CDF { Sigmas = 3 } }
                };

                /*if (_experiment.GetCurrentTrialIndex() == 0)
                    _moogHandler.RecordFeedback(TimeSpan.FromSeconds(10));*/

                _moogHandler.MoveByTrajectory(trajectoryParameters);
                yield return new WaitForSeconds((float)_parameters.DelayBetweenMoogAndVr.TotalSeconds);
            }

            TimeSpan elapsed = TimeSpan.Zero;
            var trajectoryManager = new TrajectoryManager(new MoveByTrajectoryParameters
            {
                StartPoint = new DofParameters { Surge = 0 },
                EndPoint = new DofParameters { Surge = _currentTrial.FirstInterval.Distance * _parameters.DistanceMultiplier },
                MovementDuration = _currentTrial.FirstInterval.Duration,
                //MovementDuration = _parameters.FirstMovementDuration,
                TrajectoryType = TrajectoryType.Linear,
                TrajectoryProfile = TrajectoryProfile.CDF,
                DelayHandling = DelayCompensationStrategy.Jump,
                TrajectoryTypeSettings = new TrajectoryTypeSettings { Linear = new TrajectoryTypeSettings_Linear { } },
                TrajectoryProfileSettings = new TrajectoryProfileSettings { CDF = new TrajectoryProfileSettings_CDF { Sigmas = 3 } }
            });

            while (elapsed <= _currentTrial.FirstInterval.Duration)
            {
                elapsed += TimeSpan.FromSeconds(Time.deltaTime);
                float progress = Mathf.Clamp01((float)(elapsed / _currentTrial.FirstInterval.Duration));
                var nextPosition = trajectoryManager.GetNextPosition();

                if (nextPosition == null)
                    break;

                _whereCameraShouldBe = trajectoryManager.GetNextPosition().Value;
                yield return null;
            }

            _trialState = Temporal2IntervalTrialState.InterIntervalPause;
            _scene.Hide();
        }

        private IEnumerator InterIntervalPause()
        {
            yield return new WaitForSeconds((float)_parameters.PauseBetweenIntervalsDuration.TotalSeconds);
            _trialState = Temporal2IntervalTrialState.SecondInterval;
        }

        private IEnumerator SecondInterval()
        {
            // resolves visual glitch of sudden tp
            _whereCameraShouldBe = new DofParameters { Surge = 0 };
            yield return null;

            if (_currentTrial.SecondInterval.ModelEthnicity == Ethnicity.Western)
                _scene.ShowWestern();
            else if (_currentTrial.SecondInterval.ModelEthnicity == Ethnicity.Eastern)
                _scene.ShowEastern();
            else
                Debug.Log("You shouldn't see that message");

            yield return new WaitForSeconds((float)_parameters.PauseBetweenSeeingAndMovingDuration.TotalSeconds);

            if (_parameters.StimulusType == ExperimentStimulusType.CombinedCombined)
            {
                var toPoint = _parameters.MoogStartPosition;
                toPoint.Surge += _currentTrial.FirstInterval.Distance;
                toPoint.Surge += _currentTrial.SecondInterval.Distance;

                var fromPoint = _parameters.MoogStartPosition;
                fromPoint.Surge += _currentTrial.FirstInterval.Distance;

                var trajectoryParameters = new MoveByTrajectoryParameters
                {
                    StartPoint = fromPoint,
                    EndPoint = toPoint,
                    MovementDuration = _currentTrial.SecondInterval.Duration * _parameters.MoogMovementDurationCorrectionFactor,
                    TrajectoryType = TrajectoryType.Linear,
                    TrajectoryProfile = TrajectoryProfile.CDF,
                    DelayHandling = DelayCompensationStrategy.Ignore,
                    TrajectoryTypeSettings = new TrajectoryTypeSettings { Linear = new TrajectoryTypeSettings_Linear { } },
                    TrajectoryProfileSettings = new TrajectoryProfileSettings { CDF = new TrajectoryProfileSettings_CDF { Sigmas = 3 } }
                };

                _moogHandler.MoveByTrajectory(trajectoryParameters);
                yield return new WaitForSeconds((float)_parameters.DelayBetweenMoogAndVr.TotalSeconds);
            }

            TimeSpan elapsed = TimeSpan.Zero;
            var trajectoryManager = new TrajectoryManager(new MoveByTrajectoryParameters
            {
                StartPoint = new DofParameters { Surge = 0 },
                EndPoint = new DofParameters { Surge = _currentTrial.SecondInterval.Distance * _parameters.DistanceMultiplier },
                MovementDuration = _currentTrial.SecondInterval.Duration,
                TrajectoryType = TrajectoryType.Linear,
                TrajectoryProfile = TrajectoryProfile.CDF,
                DelayHandling = DelayCompensationStrategy.Jump,
                TrajectoryTypeSettings = new TrajectoryTypeSettings { Linear = new TrajectoryTypeSettings_Linear { } },
                TrajectoryProfileSettings = new TrajectoryProfileSettings { CDF = new TrajectoryProfileSettings_CDF { Sigmas = 3 } }
            });

            while (elapsed <= _currentTrial.SecondInterval.Duration)
            {
                elapsed += TimeSpan.FromSeconds(Time.deltaTime);
                float progress = Mathf.Clamp01((float)(elapsed / _currentTrial.SecondInterval.Duration));
                var nextPosition = trajectoryManager.GetNextPosition();

                if (nextPosition == null)
                    break;

                _whereCameraShouldBe = trajectoryManager.GetNextPosition().Value;
                yield return null;
            }

            //Debug.Log(_currentTrial.FirstInterval.Distance);
            //Debug.Log(_currentTrial.FirstInterval.Distance + _currentTrial.SecondInterval.Distance);
            //Debug.Log(_whereCameraShouldBe.Surge);
            //Debug.Log(trajectoryManager.GetDevLog());

            _trialState = Temporal2IntervalTrialState.AnswerPhase;
            _scene.Hide();
        }

        private IEnumerator AnswerPhase()
        {
            float timeout = (float)_parameters.TimeToAnswer.TotalSeconds;
            float elapsed = 0f;

            while (_currentTrial.ReceivedAnswer == TrialAnswer.None && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            _currentTrial.ResponseTime = elapsed;

            if (_currentTrial.ReceivedAnswer == TrialAnswer.None && elapsed >= timeout)
            {
                _currentTrial.ResponseTime = -1f;

                _experiment.SetParticipantAnswer(TrialAnswer.Timeout);
                _sound.PlaySound_AnswerIsLate();
            }            

            //_experimentTabHandler.PrintToInfo(@$"received answer: {_currentTrial?.ReceivedAnswer}", false);

            _trialState = Temporal2IntervalTrialState.PreReturningPause;
        }

        private IEnumerator PreReturningPause()
        {
            yield return new WaitForSeconds((float)_parameters.DelayAfterAnswerSignal.TotalSeconds);
            _trialState = Temporal2IntervalTrialState.Returning;
        }

        private IEnumerator Returning()
        {
            /*TimeSpan elapsed = TimeSpan.Zero;
            var trajectoryManager = new TrajectoryManager(new MoveByTrajectoryParameters
            {
                StartPoint = new DofParameters { Surge = _currentTrial.FirstInterval.Distance + _currentTrial.SecondInterval.Distance },
                EndPoint = new DofParameters { Surge = 0 },
                MovementDuration = _parameters.BackwardMovementDuration,
                TrajectoryType = TrajectoryType.Linear,
                TrajectoryProfile = TrajectoryProfile.CDF,
                DelayHandling = DelayCompensationStrategy.Jump,
                TrajectoryTypeSettings = new TrajectoryTypeSettings { Linear = new TrajectoryTypeSettings_Linear { } },
                TrajectoryProfileSettings = new TrajectoryProfileSettings { CDF = new TrajectoryProfileSettings_CDF { Sigmas = 3 } }
            });

            while (elapsed <= _parameters.BackwardMovementDuration)
            {
                elapsed += TimeSpan.FromSeconds(Time.deltaTime);
                float progress = Mathf.Clamp01((float)(elapsed / _parameters.BackwardMovementDuration));
                var nextPosition = trajectoryManager.GetNextPosition();

                if (nextPosition == null)
                    break;

                _whereCameraShouldBe = trajectoryManager.GetNextPosition().Value;
                yield return null;
            }*/

            if (_parameters.StimulusType == ExperimentStimulusType.CombinedCombined)
            {
                var fromPoint = _parameters.MoogStartPosition;
                fromPoint.Surge += _currentTrial.FirstInterval.Distance;
                fromPoint.Surge += _currentTrial.SecondInterval.Distance;

                var trajectoryParameters = new MoveByTrajectoryParameters
                {
                    StartPoint = fromPoint,
                    EndPoint = _parameters.MoogStartPosition,
                    MovementDuration = _parameters.BackwardMovementDuration * _parameters.MoogMovementDurationCorrectionFactor,
                    TrajectoryType = TrajectoryType.Linear,
                    TrajectoryProfile = TrajectoryProfile.CDF,
                    DelayHandling = DelayCompensationStrategy.Ignore,
                    TrajectoryTypeSettings = new TrajectoryTypeSettings { Linear = new TrajectoryTypeSettings_Linear { } },
                    TrajectoryProfileSettings = new TrajectoryProfileSettings { CDF = new TrajectoryProfileSettings_CDF { Sigmas = 3 } }
                };

                _moogHandler.MoveByTrajectory(trajectoryParameters);
                yield return new WaitForSeconds((float)_parameters.DelayBetweenMoogAndVr.TotalSeconds);
            }

            yield return new WaitForSeconds((float)_parameters.BackwardMovementDuration.TotalSeconds);
            _trialState = Temporal2IntervalTrialState.Analyzation;
        }

        private IEnumerator Analyzation()
        {
            _experiment.FinishTrial();
            yield return null;
        }

        // ................

        private void HandleInput_Up()
        {
            if (_trialState != Temporal2IntervalTrialState.AnswerPhase) return;

            _experiment.SetParticipantAnswer(TrialAnswer.SecondInterval);
            _sound.PlaySound_GotAnswer();
        }

        private void HandleInput_Down()
        {
            if (_trialState != Temporal2IntervalTrialState.AnswerPhase) return;

            _experiment.SetParticipantAnswer(TrialAnswer.FirstInterval);
            _sound.PlaySound_GotAnswer();
        }

        private void HandleInput_Start()
        {
            if (_trialState != Temporal2IntervalTrialState.ReadyToStart) return;

            _trialState = Temporal2IntervalTrialState.PreFirstIntervalPause;
        }

        private void PrintStateInfo()
        {
            // 1. Безопасно получаем текущий индекс (если null, считаем за 0)
            int trialIndex = _experiment?.GetCurrentTrialIndex() ?? 0;

            // 2. Считаем текущий номер (от 1) и блок (от 1)
            int trialNumber = trialIndex + 1;
            int currentBlock = (trialIndex / 8) + 1;
            int totalTrials = _parameters?.TrialsNumber ?? 0;

            // 3. Формируем строку без "мусорных" пробелов от табуляции кода
            string info =
                $"trial: {trialNumber} / {totalTrials}\n" +
                $"level: {_currentTrial?.Difficulty}\n" +
                $"block: {currentBlock}\n\n" +
                $"trial state: {_trialState}\n" +
                $"noise: {_sound?.GetNoiseStatus}\n" +
                $"experiment state: {_experimentState}\n\n" +
                $"correct answer: {_currentTrial?.CorrectAnswer.ToString() ?? "-"}\n" +
                $"received answer: {_currentTrial?.ReceivedAnswer.ToString() ?? "-"}";

            _experimentTabHandler?.PrintToInfo(info, true);
        }
    }
}