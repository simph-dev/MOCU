using System;
using System.Collections;

using MoogModule;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// The environment is MirrorExperiment's - the star torus, the screen, the
// fixation point - and is used as it is. Aliased in one by one rather than
// through a `using` that would also drag in its Trial, Parameters, Experiment and
// the rest, which share names with the classes here.
using FixationPoint = MirrorExperiment.FixationPoint;
using KeyboardHead = MirrorExperiment.KeyboardHead;
using RearView = MirrorExperiment.RearView;
using StarField = MirrorExperiment.StarField;
using TemporalSound = RaceExperiment.TemporalSound;


namespace MirrorTwoIntervalExperiment
{
    /// Two-interval distance discrimination in MirrorExperiment's car: the
    /// platform drives forward twice, one interval after the other, once on its
    /// own and once with optic flow, and the participant reports which of the two
    /// went further. The trial structure is RaceExperiment's; everything seen is
    /// MirrorExperiment's. See README.md.
    public class ExperimentHandler : ManagedMonoBehaviour
    {
        private Parameters _parameters;
        private Experiment _experiment;
        private TemporalSound _sound;
        private StarField _starField;
        private ExperimentTabHandler _experimentTabHandler;
        private MoogHandler _moogHandler;
        private InputHandler _input;

        private TrialState _trialState;
        private ExperimentState _experimentState;
        private Trial _currentTrial;

        /// Camera displacement relative to CameraStartPosition, in Moog DOF terms.
        /// Back to zero - the centre of the torus - after every interval.
        private DofParameters _cameraOffset;

        /// Where we last commanded the platform to be, so moves can chain.
        private DofParameters _moogPosition;

        /// When the platform's last move is expected to be over, in realtime
        /// seconds, so the next one is not sent while it is still going.
        private float _moogBusyUntil;

        /// Park pressed and Engage not since. Kept apart from _moogPosition because
        /// by default the park pose and the neutral one are the same numbers, so
        /// the position alone cannot tell a parked platform from an engaged one.
        /// False at startup: whether the platform is engaged then is not known here.
        private bool _moogParked;

        private Transform _cameras;
        private Transform _vrCamera;
        private Camera _headsetCamera;

        /// Everything this experiment shows is built in code under this root, as
        /// in MirrorExperiment.
        private Transform _root;
        private RearView _rearView;
        private FixationPoint _fixationPoint;
        private KeyboardHead _keyboardHead;

        private const string OperatorViewTexture = "GUI/UnityViewFromUi";
        private const string StarMaterialResource = "StarMaterial";
        private const string StarsLayerName = "Stars";
        private const string MirrorStarsLayerName = "MirrorStars";
        private const string CabinLayerName = "Cabin";
        private int _cabinLayer = -1;

        private Vector3 _manualOffset = Vector3.zero;
        private float _yRotationOffset = 0f;

        private string _configError;
        private Coroutine _loopCoroutine;
        private SessionWriter _sessionWriter;
        private Trial _lastCompletedTrial;

        /// Set by the Pause button, honoured at the top of the next trial.
        private bool _pauseRequested;

        private const float MoogSettleMarginSeconds = 0.3f;


        public override void ManagedAwake()
        {
            LoadParameters();
            _experiment = new Experiment(_parameters);

            _experimentTabHandler = GetComponent<ExperimentTabHandler>();
            _moogHandler = GetComponent<MoogHandler>();
            _input = GetComponent<InputHandler>();
            _sound = GameObject.Find("Audio").GetComponent<TemporalSound>();

            _trialState = TrialState.None;
            _experimentState = ExperimentState.None;
            _cameraOffset = DofParameters.Zero;
            _moogPosition = _parameters.MoogParkPosition;
            _currentTrial = null;

            _cameras = GameObject.Find("Cameras").transform;
            _vrCamera = _cameras.Find("VrHelmetCamera");
            _headsetCamera = _vrCamera.GetComponent<Camera>();

            _cabinLayer = LayerMask.NameToLayer(CabinLayerName);

            if (_cabinLayer < 0)
                Debug.LogError($"MirrorTwoIntervalExperiment: no \"{CabinLayerName}\" layer in Tags and Layers - the screen cannot be built");
            else
                SetLayerRendered(_headsetCamera, 1 << _cabinLayer, true);

            _root = new GameObject("MirrorTwoIntervalExperiment").transform;

            _keyboardHead = _root.gameObject.AddComponent<KeyboardHead>();
            _keyboardHead.SetHead(_vrCamera);

            _starField = CreateStarField();

            CreateEyeMirrorVolume();

            _fixationPoint = CreateChild("FixationPoint").AddComponent<FixationPoint>();

            foreach (string problem in new[] { ConfigureRearView(), ConfigureFixationPoint() })
                if (problem != null)
                    Debug.LogError($"MirrorTwoIntervalExperiment: {problem}");

            _starField?.Configure(_parameters.StarField, StarHoleRadius());

            ShowStarsTo(outside: true, inside: true);

            // Down and Up only, as in RaceExperiment. Left and Right do nothing.
            _input.GotAnswer_Down += HandleInput_First;
            _input.GotAnswer_Up += HandleInput_Second;
            _input.GotSignal_Start += HandleInput_Start;

            _input.HeadTotalCalibrationRequest += CalibrateHead;
            _input.HeadCalibrationRequest += CalibrateHeadRotation;
            _input.HeightCalibrationRequest += CalibrateHeadPosition;

            _input.ToggleNoise += _sound.ToggleWhiteNoise;

            _experimentTabHandler.EngageBtnClicked += (eventObj) => EngageMoog();
            _experimentTabHandler.ParkBtnClicked += (eventObj) => DisengageMoog();
            _experimentTabHandler.StartExperimentBtnClicked += (eventObj) => StartExperiment();
            _experimentTabHandler.StoptExperimentBtnClicked += (eventObj) => StartCoroutine(StopExperiment());
            _experimentTabHandler.PauseExperimentBtnClicked += (eventObj) => RequestPause();
            _experimentTabHandler.ResumeExperimentBtnClicked += (eventObj) => RequestResume();
        }

        public override void ManagedStart()
        {
            CanUseUpdateMethod = true;
        }

        public override void ManagedUpdate()
        {
            ApplyCameraPose();

            if (Time.frameCount % 30 == 0)
                PrintStateInfo();
        }

        /// See MirrorExperiment.ExperimentHandler.ApplyCameraPose.
        private void ApplyCameraPose()
        {
            Vector3 basePosition = new Vector3
            {
                x = _parameters.CameraStartPosition.Sway + _cameraOffset.Sway,
                y = _parameters.CameraStartPosition.Heave,
                z = _parameters.CameraStartPosition.Surge + _cameraOffset.Surge
            };

            _cameras.rotation = Quaternion.Euler(0, _yRotationOffset, 0);
            _cameras.position = basePosition + _manualOffset;

            if (_keyboardHead != null && _keyboardHead.Active)
            {
                _cameras.rotation = Quaternion.Euler(_keyboardHead.Pitch, _yRotationOffset + _keyboardHead.Yaw, 0f);
                _cameras.position = basePosition + _keyboardHead.Offset - _cameras.rotation * _vrCamera.localPosition;
            }

            if (_rearView != null)
                _rearView.SetAnchor(basePosition);

            if (_fixationPoint != null)
                _fixationPoint.SetCarAnchor(basePosition);
        }

        // ..........................................................
        // Calibration: the same three as MirrorExperiment.

        public void CalibrateHead()
        {
            _yRotationOffset = -_vrCamera.localEulerAngles.y;
            Quaternion targetRot = Quaternion.Euler(0, _yRotationOffset, 0);
            _manualOffset = -(targetRot * _vrCamera.localPosition);

            ApplyCameraPose();
            Debug.Log("Head was calibrated");
        }

        public void CalibrateHeadRotation()
        {
            Vector3 currentWorldEyePos = _cameras.position + (_cameras.rotation * _vrCamera.localPosition);
            _yRotationOffset = -_vrCamera.localEulerAngles.y;
            Vector3 basePos = new Vector3(
                _parameters.CameraStartPosition.Sway + _cameraOffset.Sway,
                _parameters.CameraStartPosition.Heave,
                _parameters.CameraStartPosition.Surge + _cameraOffset.Surge);
            Quaternion nextRot = Quaternion.Euler(0, _yRotationOffset, 0);
            _manualOffset = currentWorldEyePos - basePos - (nextRot * _vrCamera.localPosition);

            ApplyCameraPose();
            Debug.Log("Head rotation was calibrated");
        }

        public void CalibrateHeadPosition()
        {
            Quaternion currentRot = Quaternion.Euler(0, _yRotationOffset, 0);
            _manualOffset = -(currentRot * _vrCamera.localPosition);

            ApplyCameraPose();
            Debug.Log("Head position was calibrated (all three axes)");
        }

        // ..........................................................

        private void EngageMoog()
        {
            if (_experimentState == ExperimentState.Started)
            {
                _experimentTabHandler.PrintToWarnings("Can't Engage now\n");
                return;
            }

            _moogHandler.Engage();
            _moogPosition = _parameters.MoogNeutralPosition;
            _moogParked = false;
        }

        private void DisengageMoog()
        {
            if (_experimentState == ExperimentState.Started)
            {
                _experimentTabHandler.PrintToWarnings("Can't Disengage now\n");
                return;
            }

            _moogHandler.Disengage();
            _moogPosition = _parameters.MoogParkPosition;
            _moogParked = true;
        }

        private void LoadParameters()
        {
            try
            {
                _parameters = ParametersFile.Load();
                _configError = null;
            }
            catch (Exception e)
            {
                _parameters ??= new Parameters();
                _configError = e.Message;
                Debug.LogError($"MirrorTwoIntervalExperiment: could not read {ParametersFile.FilePath}\n{e.Message}");
            }
        }

        private string ConfigureRearView()
        {
            if (_rearView == null)
            {
                if (!_parameters.RearView.Enabled || _starField == null)
                    return null;

                if (_cabinLayer < 0)
                    return $"RearView.Enabled needs a \"{CabinLayerName}\" layer in Tags and Layers";

                if (_starField.MirrorLayer < 0)
                    return $"RearView.Enabled needs a \"{MirrorStarsLayerName}\" layer in Tags and Layers";

                _rearView = CreateChild("RearView").AddComponent<RearView>();
            }

            _rearView.Configure(_parameters.RearView, _headsetCamera, _cabinLayer, out string problem);

            return problem;
        }

        private string ConfigureFixationPoint()
        {
            _fixationPoint.Configure(
                _parameters.FixationPoint,
                _parameters.RearView,
                _vrCamera,
                _cabinLayer >= 0 ? _cabinLayer : _vrCamera.gameObject.layer,
                out string problem);

            return problem;
        }

        /// See MirrorExperiment.ExperimentHandler.ShowStarsTo.
        private void ShowStarsTo(bool outside, bool inside)
        {
            if (_starField == null)
                return;

            int starsBit = 1 << _starField.gameObject.layer;
            int mirrorStarsBit = _starField.MirrorLayer >= 0 ? 1 << _starField.MirrorLayer : 0;

            SetLayerRendered(_headsetCamera, starsBit, outside);
            SetLayerRendered(_headsetCamera, mirrorStarsBit, false);

            bool screenOn = _rearView != null && _rearView.gameObject.activeSelf;

            _starField.SetMirrorStars(screenOn ? _rearView.StarFraction : 0f, screenOn ? _rearView.StarScale : 1f);

            if (_rearView == null)
                return;

            foreach (var camera in _rearView.Cameras)
            {
                SetLayerRendered(camera, starsBit, false);
                SetLayerRendered(camera, mirrorStarsBit, inside);
            }
        }

        private string FindMissingStars()
        {
            if (_starField != null)
                return null;

            if (DesignCues.Vestibular(_parameters).HasAnyVisual || DesignCues.Combined(_parameters).HasAnyVisual)
                return "No star field (see the log for why) but the design has optic flow";

            return null;
        }

        private static void SetLayerRendered(Camera camera, int layerBit, bool rendered)
        {
            camera.cullingMask = rendered ? camera.cullingMask | layerBit : camera.cullingMask & ~layerBit;
        }

        private void CreateEyeMirrorVolume()
        {
            var target = Resources.Load<RenderTexture>(OperatorViewTexture);

            if (target == null)
            {
                Debug.LogError($"MirrorTwoIntervalExperiment: no {OperatorViewTexture} render texture in Resources - the operator's view of the headset will stay blank");
                return;
            }

            var volume = CreateChild("EyeMirrorVolume").AddComponent<CustomPassVolume>();
            volume.isGlobal = true;
            volume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
            volume.AddPassOfType<EyeMirror>().targetTexture = target;
        }

        private StarField CreateStarField()
        {
            var material = Resources.Load<Material>(StarMaterialResource);

            if (material == null)
            {
                Debug.LogError($"MirrorTwoIntervalExperiment: star material \"{StarMaterialResource}\" not found in any Resources folder - no stars will be drawn");
                return null;
            }

            int layer = LayerMask.NameToLayer(StarsLayerName);

            if (layer < 0)
            {
                Debug.LogError($"MirrorTwoIntervalExperiment: no \"{StarsLayerName}\" layer in Tags and Layers - no stars will be drawn");
                return null;
            }

            var stars = CreateChild("Stars");
            stars.layer = layer;

            var starField = stars.AddComponent<StarField>();
            starField.StarMaterial = material;
            starField.MirrorLayer = LayerMask.NameToLayer(MirrorStarsLayerName);

            return starField;
        }

        private GameObject CreateChild(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root, false);
            return child;
        }

        private void StartExperiment()
        {
            if (_experimentState == ExperimentState.Started || _experimentState == ExperimentState.Paused)
            {
                _experimentTabHandler.PrintToWarnings(
                    _experimentState == ExperimentState.Paused
                        ? "Experiment is paused - press Resume, or Stop first to start over\n"
                        : "Experiment is already started\n");
                return;
            }

            LoadParameters();

            if (_configError != null)
            {
                _experimentTabHandler.PrintToWarnings($"Bad config, fix it and press Start again:\n{ParametersFile.FilePath}\n{_configError}\n");
                return;
            }

            string problem = Experiment.FindProblem(_parameters)
                             ?? FindMissingStars()
                             ?? ConfigureRearView()
                             ?? ConfigureFixationPoint();

            ShowStarsTo(outside: true, inside: true);

            if (problem != null)
            {
                _experimentTabHandler.PrintToWarnings($"Bad config, fix it and press Start again:\n{ParametersFile.FilePath}\n{problem}\n");
                return;
            }

            if (_loopCoroutine != null)
                StopCoroutine(_loopCoroutine);

            _experiment = new Experiment(_parameters);
            _sessionWriter = null;
            _currentTrial = null;
            _lastCompletedTrial = null;
            _pauseRequested = false;
            _trialState = TrialState.None;
            _cameraOffset = DofParameters.Zero;

            if (_starField != null)
            {
                _starField.Hide();
                _starField.Configure(_parameters.StarField, StarHoleRadius());
            }

            float longestTrial = Experiment.LongestInterval(_parameters) + _parameters.ReferenceDistance;
            Debug.Log($"MirrorTwoIntervalExperiment: design {_parameters.Design} - " +
                      $"{DesignCues.Vestibular(_parameters).Label} vs {DesignCues.Combined(_parameters).Label}; " +
                      $"platform surge from {_parameters.MoogStartPosition.Surge:F3} to at most {_parameters.MoogStartPosition.Surge + longestTrial:F3} m");

            CanUseUpdateMethod = true;
            CalibrateHeadPosition();
            _loopCoroutine = StartCoroutine(Loop());
        }

        private IEnumerator StopExperiment()
        {
            if (_experimentState != ExperimentState.Started && _experimentState != ExperimentState.Paused)
            {
                _experimentTabHandler.PrintToWarnings("Experiment is not started\n");
                yield break;
            }

            if (_trialState != TrialState.None && _trialState != TrialState.ReadyToStart)
            {
                _experimentTabHandler.PrintToWarnings("Trial is still running!\n");
                yield break;
            }

            _pauseRequested = false;

            if (_loopCoroutine != null)
            {
                StopCoroutine(_loopCoroutine);
                _loopCoroutine = null;
            }

            _trialState = TrialState.None;
            _starField?.Hide();
            yield return MoveMoogTo(_parameters.MoogParkPosition, _parameters.MoogReturnDuration);
            _experimentState = ExperimentState.Finished;
            Debug.Log("Experiment stopped");
        }

        private IEnumerator Loop()
        {
            Debug.Log("Experiment started");
            _experimentState = ExperimentState.Started;
            _experiment.GenerateTrials();

            while (!_experiment.HasFinished)
            {
                yield return HoldWhilePaused();
                yield return Initializing();

                // Initializing bails out and asks for a pause when the platform is
                // not where the trial needs it.
                if (_pauseRequested)
                    continue;

                yield return WaitingToStartSignal();

                _experiment.MarkTrialStarted();

                _trialState = TrialState.FirstInterval;
                yield return Interval(_currentTrial.FirstInterval);

                _trialState = TrialState.InterIntervalPause;
                yield return InterIntervalPause();

                _trialState = TrialState.SecondInterval;
                yield return Interval(_currentTrial.SecondInterval);

                _trialState = TrialState.AnswerPhase;
                yield return AnswerPhase();
                yield return Returning();
                yield return Analyzation();
            }

            // Trials leave the platform at MoogStartPosition, behind neutral. Brought
            // forward before the run counts as finished, so that Park and Engage -
            // live again once it has - find it where they expect it.
            _trialState = TrialState.None;

            if (_moogPosition == _parameters.MoogStartPosition)
                yield return MoveMoogTo(_parameters.MoogNeutralPosition, _parameters.MoogRepositionDuration);

            _experimentState = ExperimentState.Finished;
            _loopCoroutine = null;
            Debug.Log("Experiment finished");
        }

        // ..........................................................

        private void RequestPause()
        {
            if (_experimentState != ExperimentState.Started)
            {
                _experimentTabHandler.PrintToWarnings("Nothing to pause\n");
                return;
            }

            if (_pauseRequested)
                return;

            _pauseRequested = true;
            _experimentTabHandler.PrintToWarnings("Pausing after the current trial\n");
        }

        private void RequestResume()
        {
            if (!_pauseRequested && _experimentState != ExperimentState.Paused)
            {
                _experimentTabHandler.PrintToWarnings("Experiment is not paused\n");
                return;
            }

            _pauseRequested = false;
        }

        /// Holds between trials. The platform waits at MoogStartPosition between
        /// trials, so it is first brought forward to neutral, where Park and Engage
        /// expect it; the next trial takes it back.
        private IEnumerator HoldWhilePaused()
        {
            if (!_pauseRequested)
                yield break;

            _trialState = TrialState.None;
            _cameraOffset = DofParameters.Zero;
            _starField?.Hide();

            if (_moogPosition == _parameters.MoogStartPosition)
                yield return MoveMoogTo(_parameters.MoogNeutralPosition, _parameters.MoogRepositionDuration);

            _experimentState = ExperimentState.Paused;
            Debug.Log("Experiment paused");

            while (_pauseRequested)
                yield return null;

            _experimentState = ExperimentState.Started;
            Debug.Log("Experiment resumed");
        }

        private IEnumerator Initializing()
        {
            _trialState = TrialState.Initializing;
            _cameraOffset = DofParameters.Zero;
            _starField?.Hide();

            _currentTrial = _experiment.PrepareCurrentTrial();

            PrepareView(_currentTrial.FirstInterval);

            bool needsPlatform = _currentTrial.FirstInterval.Cues.HasVestibular || _currentTrial.SecondInterval.Cues.HasVestibular;

            if (needsPlatform && _moogPosition != _parameters.MoogStartPosition)
            {
                // From neutral - after Engage, or a pause - the way back is known.
                // From anywhere else it is not, and the platform is not moved on a
                // guess; nor is a parked one, wherever it is.
                if (_moogParked || _moogPosition != _parameters.MoogNeutralPosition)
                {
                    _experimentTabHandler.PrintToWarnings(_moogParked
                        ? "Platform is parked - press Engage, then Resume\n"
                        : "Platform is not at its neutral position - press Engage, then Resume\n");

                    _pauseRequested = true;
                    yield break;
                }

                yield return MoveMoogTo(_parameters.MoogStartPosition, _parameters.MoogRepositionDuration);
            }

            yield return new WaitForSeconds((float)_parameters.DelayBeforeStartSound.TotalSeconds);

            if (_parameters.PlayStartTrialSound)
                _sound.PlaySound_Start();

            _trialState = TrialState.ReadyToStart;
        }

        private IEnumerator WaitingToStartSignal()
        {
            while (_trialState != TrialState.PreFirstIntervalPause)
                yield return null;

            yield return new WaitForSeconds((float)_parameters.DelayAfterStartSignal.TotalSeconds);
        }

        /// Switches the screen to the view an interval's stars will be shown in,
        /// ahead of time: before the trial for the first interval, in the pause for
        /// the second. The screen is empty either way until the stars come on.
        private void PrepareView(TrialInterval interval)
        {
            if (_rearView == null || !interval.Cues.HasVisualInside)
                return;

            _rearView.SetView(interval.Cues.InsideView);

            // The new view may show another share of the stars, at another size.
            ShowStarsTo(outside: true, inside: true);
        }

        /// One interval: the platform forward from wherever it is by the interval's
        /// distance, the camera forward from the centre of the torus by the same,
        /// each only if the interval has it. The same length of time whatever it
        /// has, so nothing in the timing gives the interval away.
        private IEnumerator Interval(TrialInterval interval)
        {
            float startedAt = Time.realtimeSinceStartup;

            // --- vestibular: the platform leads, VR follows after a fixed delay
            if (interval.Cues.HasVestibular)
            {
                var from = _moogPosition;
                var to = from + new DofParameters { Surge = interval.Distance };
                var commanded = _parameters.IntervalDuration * _parameters.MoogMovementDurationCorrectionFactor;

                _moogHandler.MoveByTrajectory(MakeTrajectory(from, to, commanded, DelayCompensationStrategy.Ignore));

                _moogPosition = to;
                _moogBusyUntil = Time.realtimeSinceStartup + MoogTravelWaitSeconds(commanded);
            }

            yield return new WaitForSeconds((float)_parameters.DelayBetweenMoogAndVr.TotalSeconds);

            // --- visual
            if (!interval.Cues.HasAnyVisual || _starField == null)
            {
                yield return new WaitForSeconds((float)_parameters.IntervalDuration.TotalSeconds);
            }
            else
            {
                // A fresh torus round the eye's place for every interval, so the
                // second cannot be compared star by star with the first.
                _starField.Regenerate(CameraHomePosition());

                ShowStarsTo(interval.Cues.HasVisualOutside, interval.Cues.HasVisualInside);
                _starField.Show(_parameters.Coherence, _parameters.NoiseUpdateHz);

                var trajectoryManager = new TrajectoryManager(MakeTrajectory(
                    DofParameters.Zero,
                    new DofParameters { Surge = interval.Distance * _parameters.VisualDistanceMultiplier },
                    _parameters.IntervalDuration,
                    DelayCompensationStrategy.Jump));

                TimeSpan elapsed = TimeSpan.Zero;

                while (elapsed <= _parameters.IntervalDuration)
                {
                    elapsed += TimeSpan.FromSeconds(Time.deltaTime);

                    var nextPosition = trajectoryManager.GetNextPosition();
                    if (nextPosition == null)
                        break;

                    _cameraOffset = nextPosition.Value;
                    yield return null;
                }

                // Safe in the same frame because StarField draws in LateUpdate.
                _starField.Hide();
                ShowStarsTo(outside: true, inside: true);
            }

            // Back to the centre of the torus, unseen: the stars are off.
            _cameraOffset = DofParameters.Zero;

            interval.MeasuredSeconds = Time.realtimeSinceStartup - startedAt;
        }

        /// The empty screen and its fixation point. Lasts PauseBetweenIntervals, or
        /// longer if the platform has not finished the first interval yet.
        private IEnumerator InterIntervalPause()
        {
            PrepareView(_currentTrial.SecondInterval);

            float until = Time.realtimeSinceStartup + (float)_parameters.PauseBetweenIntervals.TotalSeconds;

            if (_currentTrial.SecondInterval.Cues.HasVestibular)
                until = Mathf.Max(until, _moogBusyUntil);

            while (Time.realtimeSinceStartup < until)
                yield return null;
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

            if (_currentTrial.ReceivedAnswer == TrialAnswer.None)
            {
                _currentTrial.ResponseTime = -1f;
                _experiment.SetParticipantAnswer(TrialAnswer.Timeout);

                if (_parameters.PlayTimeoutSound)
                    _sound.PlaySound_AnswerIsLate();
            }

            _trialState = TrialState.Returning;
        }

        /// The platform back to MoogStartPosition, ready for the next trial. The
        /// pause is the same length whether or not it had anywhere to go.
        private IEnumerator Returning()
        {
            yield return new WaitForSeconds((float)_parameters.DelayAfterAnswerSignal.TotalSeconds);

            // Not before the second interval's move is over.
            while (Time.realtimeSinceStartup < _moogBusyUntil)
                yield return null;

            if (_moogPosition != _parameters.MoogStartPosition
                && (_currentTrial.FirstInterval.Cues.HasVestibular || _currentTrial.SecondInterval.Cues.HasVestibular))
            {
                _moogHandler.MoveByTrajectory(MakeTrajectory(
                    _moogPosition, _parameters.MoogStartPosition,
                    _parameters.MoogReturnDuration, DelayCompensationStrategy.Ignore));

                _moogPosition = _parameters.MoogStartPosition;
            }

            yield return new WaitForSeconds(MoogTravelWaitSeconds(_parameters.MoogReturnDuration));

            _trialState = TrialState.Analyzation;
        }

        private IEnumerator Analyzation()
        {
            if (_currentTrial.ReceivedAnswer != TrialAnswer.Timeout)
            {
                if (_currentTrial.AnswerIsCorrect && _parameters.PlayCorrectAnswerSound)
                    _sound.PlaySound_AnswerIsRight();
                else if (!_currentTrial.AnswerIsCorrect && _parameters.PlayIncorrectAnswerSound)
                    _sound.PlaySound_AnswerIsWrong();
            }

            _lastCompletedTrial = _currentTrial;
            _experiment.FinishTrial();

            _sessionWriter ??= new SessionWriter(_experiment.StartedAt, _parameters);
            _sessionWriter.AppendTrial(_currentTrial);

            yield return null;
        }

        // ..........................................................

        private Vector3 CameraHomePosition()
        {
            return new Vector3(
                _parameters.CameraStartPosition.Sway,
                _parameters.CameraStartPosition.Heave,
                _parameters.CameraStartPosition.Surge);
        }

        /// The hole in the middle of the torus: the furthest corner of the screen,
        /// plus the longest one interval can carry the car - the camera goes back
        /// to the centre between the two - plus a star. See
        /// MirrorExperiment.ExperimentHandler.StarHoleRadius.
        private float StarHoleRadius()
        {
            float travel = Experiment.LongestInterval(_parameters) * Mathf.Abs(_parameters.VisualDistanceMultiplier);
            float star = Mathf.Max(_parameters.StarField.StarWidth, _parameters.StarField.StarHeight);

            float corner = _rearView != null && _rearView.gameObject.activeSelf && _rearView.FurthestCornerDistance > 0f
                ? _rearView.FurthestCornerDistance
                : _parameters.RearView.FurthestCornerDistance();

            return corner + travel + star;
        }

        private MoveByTrajectoryParameters MakeTrajectory(
            DofParameters from, DofParameters to, TimeSpan duration, DelayCompensationStrategy delayHandling)
        {
            return new MoveByTrajectoryParameters
            {
                StartPoint = from,
                EndPoint = to,
                MovementDuration = duration,
                TrajectoryType = TrajectoryType.Linear,
                TrajectoryProfile = TrajectoryProfile.CDF,
                DelayHandling = delayHandling,
                TrajectoryTypeSettings = new TrajectoryTypeSettings { Linear = new TrajectoryTypeSettings_Linear { } },
                TrajectoryProfileSettings = new TrajectoryProfileSettings { CDF = new TrajectoryProfileSettings_CDF { Sigmas = _parameters.ProfileSigmas } }
            };
        }

        /// How long to actually wait for a platform move commanded with the given
        /// duration: it runs slower than commanded (MoogMovementDurationCorrectionFactor).
        private float MoogTravelWaitSeconds(TimeSpan commandedDuration)
        {
            float factor = Mathf.Clamp(_parameters.MoogMovementDurationCorrectionFactor, 0.1f, 1f);
            return (float)commandedDuration.TotalSeconds / factor + MoogSettleMarginSeconds;
        }

        private IEnumerator MoveMoogTo(DofParameters target, TimeSpan duration)
        {
            if (_moogPosition == target)
                yield break;

            _moogHandler.MoveByTrajectory(MakeTrajectory(_moogPosition, target, duration, DelayCompensationStrategy.Ignore));
            _moogPosition = target;

            yield return new WaitForSeconds(MoogTravelWaitSeconds(duration));
        }

        // ..........................................................

        private void HandleInput_First()
        {
            if (_trialState != TrialState.AnswerPhase) return;

            _experiment.SetParticipantAnswer(TrialAnswer.FirstInterval);

            if (_parameters.PlayAnswerReceivedSound)
                _sound.PlaySound_GotAnswer();
        }

        private void HandleInput_Second()
        {
            if (_trialState != TrialState.AnswerPhase) return;

            _experiment.SetParticipantAnswer(TrialAnswer.SecondInterval);

            if (_parameters.PlayAnswerReceivedSound)
                _sound.PlaySound_GotAnswer();
        }

        private void HandleInput_Start()
        {
            if (_trialState != TrialState.ReadyToStart) return;

            _trialState = TrialState.PreFirstIntervalPause;
        }

        private void PrintStateInfo()
        {
            int trialIndex = _experiment?.GetCurrentTrialIndex() ?? 0;
            int totalTrials = _experiment?.GetTotalTrialsCount() ?? 0;

            // Blocks of 8, as Experiment.GenerateTrials makes them.
            string info =
                $"trial: {trialIndex + 1} / {totalTrials}\n" +
                $"block: {(totalTrials > 0 ? $"{trialIndex / 8 + 1} / {totalTrials / 8} (trial {trialIndex % 8 + 1} of 8)" : "-")}\n" +
                $"design: {_parameters.Design} ({DesignCues.Vestibular(_parameters)?.Label} vs {DesignCues.Combined(_parameters)?.Label})\n" +
                $"config: {ParametersFile.FilePath}\n" +
                $"first: {DescribeInterval(_currentTrial?.FirstInterval)}\n" +
                $"second: {DescribeInterval(_currentTrial?.SecondInterval)}\n" +
                $"level: {_currentTrial?.Difficulty} (staircase {_currentTrial?.StaircaseIndex})\n" +
                $"correct answer: {_currentTrial?.CorrectAnswer.ToString() ?? "-"}\n" +
                $"received answer: {_currentTrial?.ReceivedAnswer.ToString() ?? "-"}\n\n" +

                $"--- last completed ---\n" +
                $"{DescribeCompletedTrial(_lastCompletedTrial)}\n\n" +

                $"trial state: {_trialState}\n" +
                $"experiment state: {_experimentState}\n" +
                $"white noise: {_sound?.GetNoiseStatus}\n" +
                $"head: {DescribeHead()}\n" +
                $"star noise: {_parameters.NoiseUpdateHz:F0} Hz asked / {(_starField != null ? _starField.MeasuredNoiseHz.ToString("F1") : "-")} Hz measured";

            _experimentTabHandler?.PrintToInfo(info, true);
        }

        private static string DescribeInterval(TrialInterval interval)
        {
            if (interval == null)
                return "-";

            return $"{interval.Kind} ({interval.Cues.Label}), {interval.Role}, {interval.Distance * 100f:F1} cm";
        }

        private string DescribeHead()
        {
            Vector3 eyePlace = CameraHomePosition() + new Vector3(_cameraOffset.Sway, 0f, _cameraOffset.Surge);
            Vector3 cm = (_vrCamera.position - eyePlace) * 100f;

            return $"{cm.x:+0;-0;0} right, {cm.y:+0;-0;0} up, {cm.z:+0;-0;0} forward (cm from its place in the car)";
        }

        private static string DescribeCompletedTrial(Trial trial)
        {
            if (trial == null)
                return "-";

            string verdict = trial.ReceivedAnswer == TrialAnswer.Timeout
                ? "TIMEOUT"
                : trial.AnswerIsCorrect ? "correct" : "WRONG";

            return
                $"{DescribeInterval(trial.FirstInterval)}  then  {DescribeInterval(trial.SecondInterval)}\n" +
                $"answered {trial.ReceivedAnswer}, expected {trial.CorrectAnswer} -> {verdict}\n" +
                $"response time {(trial.ResponseTime < 0f ? "-" : trial.ResponseTime.ToString("F2") + " s")}, " +
                $"intervals {trial.FirstInterval.MeasuredSeconds:F2} s and {trial.SecondInterval.MeasuredSeconds:F2} s";
        }
    }
}
