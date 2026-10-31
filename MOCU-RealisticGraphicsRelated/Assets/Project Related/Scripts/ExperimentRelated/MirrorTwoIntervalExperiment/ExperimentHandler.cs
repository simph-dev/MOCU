using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using MoogModule;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// The environment is MirrorExperiment's - the star torus, the screen, the
// fixation point - and is used as it is. Aliased in one by one rather than
// through a `using` that would also drag in its Trial, Parameters, Experiment and
// the rest, which share names with the classes here.
using FixationAnchor = MirrorExperiment.FixationAnchor;
using FixationPoint = MirrorExperiment.FixationPoint;
using FixationPointSettings = MirrorExperiment.FixationPointSettings;
using HeadsetSnapshot = SnapshotModule.HeadsetSnapshot;
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
    public class ExperimentHandler : ManagedMonoBehaviour, IHeadCalibration
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

        /// How the camera rig is turned so that the head, as it was at calibration
        /// - "look straight ahead and keep still" - looks along world +Z with the
        /// horizon level: its yaw, pitch and roll all undone.
        private Quaternion _rigRotation = Quaternion.identity;

        /// The head's pitch, yaw and roll at the last calibration, signed degrees,
        /// for the info panel: what was taken as zero.
        private Vector3 _calibratedHead;

        private string _configError;
        private Coroutine _loopCoroutine;
        private SessionWriter _sessionWriter;
        private Trial _lastCompletedTrial;

        /// Set by the Pause button, honoured at the top of the next trial.
        private bool _pauseRequested;

        /// When answers start being taken, realtime seconds: AnswerWindowBeforeEnd
        /// before the second interval is due to end. Set as it starts.
        private float _answersOpenAt;

        /// When the second interval actually ended, and when the answer came - or
        /// the time to answer ran out - in realtime seconds.
        private float _motionEndedAt;
        private float _answeredAt;

        /// Pictures for figures (SnapshotModule), posed with the car in
        /// ApplyCameraPose; and the kinds of interval already taken in this run.
        private HeadsetSnapshot _snapshot;
        private readonly HashSet<string> _snapshotsTaken = new();

        /// The pass copying the headset's picture to the operator's panel, kept to
        /// switch which eye it copies.
        private EyeMirror _eyeMirror;

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

            _snapshot = CreateChild("Snapshot").AddComponent<HeadsetSnapshot>();

            _fixationPoint = CreateChild("FixationPoint").AddComponent<FixationPoint>();

            string screenProblem = ConfigureRearView();

            if (screenProblem != null)
                Debug.LogError($"MirrorTwoIntervalExperiment: {screenProblem}");

            // With the screen still on, whose image says how far its corners reach.
            _starField?.Configure(_parameters.StarField, StarHoleRadius());

            ShowStarsTo(outside: true, inside: true);
            ShowIdleLook();

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

            GetComponent<DebugTabHandler>().testBtn1Clicked += (eventObj) => ToggleOperatorViewEye();
        }

        /// test1 on the Debug tab: the other eye in the operator's view. Stays until
        /// switched back - Start does not reset it to the config's.
        private void ToggleOperatorViewEye()
        {
            if (_eyeMirror == null)
                return;

            _eyeMirror.eye = _eyeMirror.eye == EyeMirror.Eye.Left ? EyeMirror.Eye.Right : EyeMirror.Eye.Left;
            _experimentTabHandler.PrintToWarnings($"Operator view: {_eyeMirror.eye.ToString().ToLowerInvariant()} eye\n");
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

            _cameras.rotation = _rigRotation;
            _cameras.position = basePosition + _manualOffset;

            if (_keyboardHead != null && _keyboardHead.Active)
            {
                _cameras.rotation = Quaternion.Euler(0f, _keyboardHead.Yaw, 0f) * _rigRotation * Quaternion.Euler(_keyboardHead.Pitch, 0f, 0f);
                _cameras.position = basePosition + _keyboardHead.Offset - _cameras.rotation * _vrCamera.localPosition;
            }

            if (_rearView != null)
                _rearView.SetAnchor(basePosition);

            if (_fixationPoint != null)
                _fixationPoint.SetCarAnchor(basePosition);

            // The head is held at the eye's place in the car, straight ahead and level.
            if (_snapshot != null)
                _snapshot.SetAnchor(basePosition, Quaternion.identity);
        }

        // ..........................................................
        // Calibration: the same three as MirrorExperiment, except that the two
        // that set the rotation take the head's whole rotation as zero - pitch and
        // roll as well as yaw (see _rigRotation). Before any calibration, level is
        // the headset's own: gravity.

        public void CalibrateHead()
        {
            TakeHeadAsLevel();
            _manualOffset = -(_rigRotation * _vrCamera.localPosition);

            ApplyCameraPose();
            Debug.Log($"Head was calibrated: {DescribeCalibration()}");
        }

        public void CalibrateHeadRotation()
        {
            Vector3 currentWorldEyePos = _cameras.position + (_cameras.rotation * _vrCamera.localPosition);
            TakeHeadAsLevel();
            Vector3 basePos = new Vector3(
                _parameters.CameraStartPosition.Sway + _cameraOffset.Sway,
                _parameters.CameraStartPosition.Heave,
                _parameters.CameraStartPosition.Surge + _cameraOffset.Surge);
            _manualOffset = currentWorldEyePos - basePos - (_rigRotation * _vrCamera.localPosition);

            ApplyCameraPose();
            Debug.Log($"Head rotation was calibrated: {DescribeCalibration()}");
        }

        public void CalibrateHeadPosition()
        {
            _manualOffset = -(_rigRotation * _vrCamera.localPosition);

            ApplyCameraPose();
            Debug.Log("Head position was calibrated (all three axes)");
        }

        /// The head's rotation undone: the rig rotation that leaves the head, as it
        /// is now, looking along world +Z, level and unrolled.
        private void TakeHeadAsLevel()
        {
            Vector3 angles = _vrCamera.localEulerAngles;

            _calibratedHead = new Vector3(
                Mathf.DeltaAngle(0f, angles.x), Mathf.DeltaAngle(0f, angles.y), Mathf.DeltaAngle(0f, angles.z));

            _rigRotation = Quaternion.Inverse(_vrCamera.localRotation);
        }

        /// In Unity positive yaw turns the head right, positive pitch tilts it
        /// down and positive roll tilts its top to the left.
        private string DescribeCalibration()
        {
            return $"yaw {Describe(_calibratedHead.y, "right", "left")}, " +
                   $"pitch {Describe(_calibratedHead.x, "down", "up")}, " +
                   $"roll {Describe(_calibratedHead.z, "left", "right")} taken as zero";

            static string Describe(float degrees, string positive, string negative) =>
                degrees == 0f ? "0" : $"{Mathf.Abs(degrees):0.0} {(degrees > 0f ? positive : negative)}";
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

        /// During the run the dot is where the config puts it. Before Start, and
        /// once the run is over, it hangs straight ahead of the head instead,
        /// following it wherever the body goes (FixationAnchor.Head).
        private string ConfigureFixationPoint(bool running)
        {
            var settings = running ? _parameters.FixationPoint : new FixationPointSettings
            {
                Enabled = _parameters.FixationPoint.Enabled,
                Anchor = FixationAnchor.Head,
                Distance = _parameters.FixationPoint.Distance,
                Diameter = _parameters.FixationPoint.Diameter
            };

            _fixationPoint.Configure(
                settings,
                _parameters.RearView,
                _vrCamera,
                _cabinLayer >= 0 ? _cabinLayer : _vrCamera.gameObject.layer,
                out string problem);

            return problem;
        }

        /// Before Start, and once the run is over: the fixation point alone, in
        /// front of the head. The screen comes back on with Start, which applies
        /// the config to it again (RearView.Configure switches it on).
        private void ShowIdleLook()
        {
            if (_rearView != null)
                _rearView.gameObject.SetActive(false);

            string problem = ConfigureFixationPoint(running: false);

            if (problem != null)
                Debug.LogError($"MirrorTwoIntervalExperiment: {problem}");
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

        /// The data folder is made, or found unusable, before the run begins - not
        /// after its first trial, with the participant in the chair.
        private string CheckDataFolder()
        {
            try
            {
                Directory.CreateDirectory(ParametersFile.DataFolderFor(_parameters));
                return null;
            }
            catch (Exception e)
            {
                return $"DataFolder \"{_parameters.DataFolder}\" cannot be used: {e.Message}";
            }
        }

        private string DescribeDataFolder()
        {
            try { return ParametersFile.DataFolderFor(_parameters); }
            catch (Exception) { return $"\"{_parameters.DataFolder}\" - not a usable path"; }
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
            _eyeMirror = volume.AddPassOfType<EyeMirror>();
            _eyeMirror.targetTexture = target;
            _eyeMirror.eye = _parameters.OperatorViewEye;
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
                             ?? CheckDataFolder()
                             ?? ConfigureRearView()
                             ?? ConfigureFixationPoint(running: true);

            ShowStarsTo(outside: true, inside: true);

            if (problem != null)
            {
                // The screen may have just been switched on with the config.
                ShowIdleLook();
                _experimentTabHandler.PrintToWarnings($"Bad config, fix it and press Start again:\n{ParametersFile.FilePath}\n{problem}\n");
                return;
            }

            if (_loopCoroutine != null)
                StopCoroutine(_loopCoroutine);

            _experiment = new Experiment(_parameters);
            _sessionWriter = null;
            _snapshotsTaken.Clear();
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

            WarnIfStarHoleGrew();

            float longestTrial = Experiment.LongestInterval(_parameters) + _parameters.ReferenceDistance;
            Debug.Log($"MirrorTwoIntervalExperiment: design {_parameters.Design} - " +
                      $"{DesignCues.Vestibular(_parameters).Label} vs {DesignCues.Combined(_parameters).Label}; " +
                      $"platform surge from {_parameters.MoogStartPosition.Surge:F3} to at most {_parameters.MoogStartPosition.Surge + longestTrial:F3} m");

            // Angles and position both: the participant has been told to look
            // straight ahead and keep still. At startup only the angles matter -
            // until Start everything shown follows the head.
            CanUseUpdateMethod = true;
            CalibrateHead();
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
            ShowIdleLook();
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

                // Pause takes effect at once (RequestPause). Before the start
                // signal nothing has moved yet: straight on to the pause.
                if (_pauseRequested)
                    continue;

                _experiment.MarkTrialStarted();

                _trialState = TrialState.FirstInterval;
                yield return Interval(_currentTrial.FirstInterval);

                if (_pauseRequested)
                {
                    yield return DropTrial();
                    continue;
                }

                _trialState = TrialState.InterIntervalPause;
                yield return InterIntervalPause();

                if (_pauseRequested)
                {
                    yield return DropTrial();
                    continue;
                }

                _answersOpenAt = Time.realtimeSinceStartup + (float)(
                    _parameters.DelayBetweenMoogAndVr + _parameters.IntervalDuration - _parameters.AnswerWindowBeforeEnd).TotalSeconds;

                _trialState = TrialState.SecondInterval;
                yield return Interval(_currentTrial.SecondInterval);

                if (_pauseRequested)
                {
                    yield return DropTrial();
                    continue;
                }

                _trialState = TrialState.AnswerPhase;
                yield return AnswerPhase();

                // Paused before an answer came: nothing to record.
                if (_currentTrial.ReceivedAnswer == TrialAnswer.None)
                {
                    yield return DropTrial();
                    continue;
                }

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
            ShowIdleLook();
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

            bool moving = _trialState is TrialState.FirstInterval or TrialState.InterIntervalPause or TrialState.SecondInterval;
            bool answering = _trialState == TrialState.AnswerPhase && _currentTrial?.ReceivedAnswer == TrialAnswer.None;

            _experimentTabHandler.PrintToWarnings(moving || answering
                ? "Pausing now - this trial is dropped and runs again after Resume\n"
                : "Pausing now\n");
        }

        /// Pause pressed in the middle of a trial, before its answer: the trial is
        /// dropped - not recorded, and the staircase not moved - and runs again
        /// after Resume. The stars go at once. The platform finishes the move it is
        /// making, since it cannot be stopped halfway, and then goes straight to
        /// neutral, where the pause holds it.
        private IEnumerator DropTrial()
        {
            _trialState = TrialState.Returning;
            _starField?.Hide();
            ShowStarsTo(outside: true, inside: true);
            _cameraOffset = DofParameters.Zero;

            // An answer from the last moments of a second interval cut short is not
            // kept: the trial will be run again in full.
            _currentTrial.ReceivedAnswer = TrialAnswer.None;
            _currentTrial.ResponseTime = null;

            while (Time.realtimeSinceStartup < _moogBusyUntil)
                yield return null;

            yield return MoveMoogTo(_parameters.MoogNeutralPosition, _parameters.MoogReturnDuration);

            Debug.Log($"Trial {_currentTrial.Index + 1} dropped by Pause - it runs again after Resume");
        }

        /// Waits this long in real time, or until Pause is pressed.
        private IEnumerator WaitUnlessPaused(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;

            while (Time.realtimeSinceStartup < until && !_pauseRequested)
                yield return null;
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
        /// expect it; the next trial takes it back. A trial Pause cut into has
        /// already brought it there (DropTrial).
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

            yield return WaitUnlessPaused((float)_parameters.DelayBeforeStartSound.TotalSeconds);

            if (_pauseRequested)
                yield break;

            if (_parameters.PlayStartTrialSound)
                _sound.PlaySound_Start();

            _trialState = TrialState.ReadyToStart;
        }

        private IEnumerator WaitingToStartSignal()
        {
            while (_trialState != TrialState.PreFirstIntervalPause && !_pauseRequested)
                yield return null;

            yield return WaitUnlessPaused((float)_parameters.DelayAfterStartSignal.TotalSeconds);
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

            yield return WaitUnlessPaused((float)_parameters.DelayBetweenMoogAndVr.TotalSeconds);

            // Pause ends the interval at once - nothing more is shown, and the trial
            // is dropped (Loop).
            if (_pauseRequested)
            {
                interval.MeasuredSeconds = Time.realtimeSinceStartup - startedAt;
                yield break;
            }

            // --- visual
            if (!interval.Cues.HasAnyVisual || _starField == null)
            {
                yield return WaitUnlessPaused((float)_parameters.IntervalDuration.TotalSeconds);
            }
            else
            {
                // A fresh torus round the eye's place for every interval, so the
                // second cannot be compared star by star with the first.
                _starField.Regenerate(CameraHomePosition());

                ShowStarsTo(interval.Cues.HasVisualOutside, interval.Cues.HasVisualInside);
                _starField.Show(_parameters.Coherence, _parameters.NoiseUpdateHz);

                if (_parameters.Snapshots.Enabled && _snapshotsTaken.Add(interval.Cues.Label))
                    TakeSnapshots(interval.Cues.Label);

                var trajectoryManager = new TrajectoryManager(MakeTrajectory(
                    DofParameters.Zero,
                    new DofParameters { Surge = interval.Distance * _parameters.VisualDistanceMultiplier },
                    _parameters.IntervalDuration,
                    DelayCompensationStrategy.Jump));

                TimeSpan elapsed = TimeSpan.Zero;

                while (elapsed <= _parameters.IntervalDuration && !_pauseRequested)
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

        /// The middle of the interval that has just started, saved by the snapshot
        /// module: from between the eyes and from each eye, the head held at the
        /// eye's place in the car, straight ahead and level (see SetAnchor in
        /// ApplyCameraPose).
        private void TakeSnapshots(string label)
        {
            _snapshot.Take(
                _parameters.Snapshots,
                _headsetCamera,
                Path.Combine(ParametersFile.DataFolderFor(_parameters), "Snapshots"),
                $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{_parameters.Design}_{label}",
                (float)_parameters.IntervalDuration.TotalSeconds / 2f,
                path => _experimentTabHandler.PrintToWarnings($"Snapshot saved: {Path.GetFileName(path)}\n"));
        }

        /// The empty screen and its fixation point. Lasts PauseBetweenIntervals, or
        /// longer if the platform has not finished the first interval yet.
        private IEnumerator InterIntervalPause()
        {
            PrepareView(_currentTrial.SecondInterval);

            float until = Time.realtimeSinceStartup + (float)_parameters.PauseBetweenIntervals.TotalSeconds;

            if (_currentTrial.SecondInterval.Cues.HasVestibular)
                until = Mathf.Max(until, _moogBusyUntil);

            while (Time.realtimeSinceStartup < until && !_pauseRequested)
                yield return null;
        }

        /// The answer may already be in, from the last AnswerWindowBeforeEnd of the
        /// second interval. The time to answer runs from that interval's end either
        /// way, and so does the response time.
        private IEnumerator AnswerPhase()
        {
            _motionEndedAt = Time.realtimeSinceStartup;
            float timeout = (float)_parameters.TimeToAnswer.TotalSeconds;

            while (_currentTrial.ReceivedAnswer == TrialAnswer.None && Time.realtimeSinceStartup - _motionEndedAt < timeout
                   && !_pauseRequested)
                yield return null;

            // Paused before an answer: left unanswered, and the trial dropped (Loop).
            if (_currentTrial.ReceivedAnswer == TrialAnswer.None && _pauseRequested)
                yield break;

            if (_currentTrial.ReceivedAnswer == TrialAnswer.None)
            {
                _answeredAt = Time.realtimeSinceStartup;
                _experiment.SetParticipantAnswer(TrialAnswer.Timeout);

                if (_parameters.PlayTimeoutSound)
                    _sound.PlaySound_AnswerIsLate();
            }
            else
            {
                _currentTrial.ResponseTime = _answeredAt - _motionEndedAt;
            }

            _trialState = TrialState.Returning;
        }

        /// The platform back to MoogStartPosition, ready for the next trial: once
        /// its second move is over, PauseBeforeReturn after the motion ended and
        /// DelayAfterAnswerSignal after the answer - whichever comes last, so an
        /// early answer does not send it straight back. The pause after it is the
        /// same length whether or not it had anywhere to go.
        private IEnumerator Returning()
        {
            float until = Mathf.Max(
                _moogBusyUntil,
                _motionEndedAt + (float)_parameters.PauseBeforeReturn.TotalSeconds,
                _answeredAt + (float)_parameters.DelayAfterAnswerSignal.TotalSeconds);

            while (Time.realtimeSinceStartup < until)
                yield return null;

            // Paused meanwhile: straight to neutral, where the pause holds it, rather
            // than back to the start and then forward again.
            var home = _pauseRequested ? _parameters.MoogNeutralPosition : _parameters.MoogStartPosition;

            if (_moogPosition != home
                && (_currentTrial.FirstInterval.Cues.HasVestibular || _currentTrial.SecondInterval.Cues.HasVestibular))
            {
                _moogHandler.MoveByTrajectory(MakeTrajectory(
                    _moogPosition, home,
                    _parameters.MoogReturnDuration, DelayCompensationStrategy.Ignore));

                _moogPosition = home;
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

            _sessionWriter ??= new SessionWriter(_experiment.StartedAt, _parameters, ParametersFile.DataFolderFor(_parameters));
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

        /// The hole in the middle of the torus: StarField.HoleRadius, the same in
        /// every experiment, unless the screen and the travel need more - the
        /// furthest corner of the screen, plus the longest one interval can carry
        /// the car (the camera goes back to the centre between the two), plus a
        /// star. Start warns when they do. See
        /// MirrorExperiment.ExperimentHandler.StarHoleRadius.
        private float StarHoleRadius() => Mathf.Max(_parameters.StarField.HoleRadius, NeededStarHoleRadius());

        /// Not an error: the run goes ahead, only with its stars further off than
        /// the other experiments'.
        private void WarnIfStarHoleGrew()
        {
            float needed = NeededStarHoleRadius();

            if (needed > _parameters.StarField.HoleRadius)
                _experimentTabHandler.PrintToWarnings(
                    $"Stars start at {needed:F2} m, beyond StarField.HoleRadius {_parameters.StarField.HoleRadius:F2} m: " +
                    "the screen and the longest movement need that much\n");
        }

        private float NeededStarHoleRadius()
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

        /// The first answer of the trial counts, given in the answer phase or
        /// already in the last AnswerWindowBeforeEnd of the second interval.
        private bool AnswersOpen =>
            _currentTrial != null && _currentTrial.ReceivedAnswer == TrialAnswer.None
            && (_trialState == TrialState.AnswerPhase
                || _trialState == TrialState.SecondInterval && Time.realtimeSinceStartup >= _answersOpenAt);

        private void HandleInput_First() => TakeAnswer(TrialAnswer.FirstInterval);

        private void HandleInput_Second() => TakeAnswer(TrialAnswer.SecondInterval);

        private void TakeAnswer(TrialAnswer answer)
        {
            if (!AnswersOpen) return;

            _answeredAt = Time.realtimeSinceStartup;
            _experiment.SetParticipantAnswer(answer);

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
                $"data: {DescribeDataFolder()}\n" +
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
                $"operator view: {(_eyeMirror != null ? _eyeMirror.eye.ToString().ToLowerInvariant() + " eye" : "-")}\n" +
                $"calibration: {DescribeCalibration()}\n" +
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
                $"response time {(trial.ResponseTime == null ? "-" : trial.ResponseTime.Value.ToString("+0.00;-0.00;0.00") + " s after the end")}, " +
                $"intervals {trial.FirstInterval.MeasuredSeconds:F2} s and {trial.SecondInterval.MeasuredSeconds:F2} s";
        }
    }
}
