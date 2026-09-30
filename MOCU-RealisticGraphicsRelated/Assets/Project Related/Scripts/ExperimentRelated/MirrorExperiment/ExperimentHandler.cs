using System;
using System.Collections;

using MoogModule;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// TemporalSound lives in the RaceExperiment namespace but is a generic sound
// player, so it is aliased in rather than shared through a `using` that would
// also drag in RaceExperiment's Trial, Parameters, Experiment and TrialAnswer.
using TemporalSound = RaceExperiment.TemporalSound;


namespace MirrorExperiment
{
    /// Single-interval heading discrimination (Yakubovich et al. 2020), with the
    /// vestibular cue mirrored: VR simulates driving forward while the platform
    /// physically travels backward, like reversing a car.
    ///
    /// Built on the same infrastructure as RaceExperiment, but the trial structure
    /// is one interval with a left/right response instead of two intervals with a
    /// which-was-larger response.
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
        private DofParameters _cameraOffset;

        /// Where we last commanded the platform to be, so moves can chain.
        private DofParameters _moogPosition;

        private Transform _cameras;
        private Transform _vrCamera;

        /// Everything this experiment shows is built in code, under this root of its
        /// own - the star field, the fixation point, the mirror and the volume
        /// feeding the operator's view. The scene holds none of it, so none of it
        /// can be lost from the scene or left disagreeing with the config.
        private Transform _root;

        /// Null until the config first asks for it, so a run without a mirror has
        /// no rear camera costing a render every frame.
        private RearViewMirror _mirror;

        /// Built in code at startup, like the mirror, and switched off rather than
        /// destroyed when the config says so.
        private FixationPoint _fixationPoint;

        /// Where the operator's "Unity view" panel gets its picture from: the
        /// EyeMirror pass copies every headset frame into this texture.
        private const string OperatorViewTexture = "GUI/UnityViewFromUi";

        /// HDRP Unlit, white, Double-Sided and GPU Instancing on. In
        /// MirrorExperiment/Resources, so a build includes it without the scene.
        private const string StarMaterialResource = "StarMaterial";

        private Vector3 _manualOffset = Vector3.zero;
        private float _yRotationOffset = 0f;

        /// Non-null when the config file could not be read. The experiment refuses
        /// to start in that state rather than quietly running on defaults.
        private string _configError;

        /// Handle on the running trial loop. Kept so a second run cannot leave the
        /// previous loop alive in the background - Stop only parks the platform, it
        /// does not by itself end the coroutine that is waiting inside a phase.
        private Coroutine _loopCoroutine;

        /// Created on the first completed trial, so an aborted run leaves no empty
        /// session folder behind.
        private SessionWriter _sessionWriter;

        /// Kept so the researcher can still read the outcome of the trial that just
        /// finished while the next one is already being prepared.
        private Trial _lastCompletedTrial;

        /// Set by the Pause button, honoured at the top of the next trial. Never
        /// mid-trial: the platform follows a wall-clock trajectory, so stopping it
        /// halfway would leave the participant parked in the middle of a movement.
        private bool _pauseRequested;

        /// Small extra slack on top of the platform's expected travel time, for
        /// command latency and settling.
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

            _root = new GameObject("MirrorExperiment").transform;

            _starField = CreateStarField();

            // Already here, not only on Start, so that the AlwaysVisible preview
            // shows the cloud the config describes rather than the Inspector's.
            _starField?.Configure(_parameters.StarField);

            CreateEyeMirrorVolume();

            _fixationPoint = CreateChild("FixationPoint").AddComponent<FixationPoint>();

            // Already here, not only on Start, so they can be looked at in the
            // headset before a run begins. Start reports a bad setting to the
            // researcher; this early there is only the log.
            foreach (string problem in new[] { ConfigureMirror(), ConfigureFixationPoint() })
                if (problem != null)
                    Debug.LogError($"MirrorExperiment: {problem}");

            _input.GotAnswer_Left += HandleInput_Left;
            _input.GotAnswer_Right += HandleInput_Right;
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
            // On from the start so that head calibration works before the run begins.
            CanUseUpdateMethod = true;
        }

        public override void ManagedUpdate()
        {
            ApplyCameraPose();

            if (Time.frameCount % 30 == 0)
                PrintStateInfo();
        }

        /// Puts the camera rig where the current offset and calibration say it should
        /// be. Split out so the calibration methods can re-apply the pose without
        /// going through ManagedUpdate and dragging the UI refresh along with it.
        ///
        /// Also moves what is fixed to the car - the mirror, the clear zone round the
        /// head and, unless it is set to follow the head, the fixation point. They
        /// go where the calibrated eye goes, not where the head is, so they follow
        /// the stimulus trajectory and no head movement.
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

            if (_mirror != null)
                _mirror.SetAnchor(basePosition);

            if (_fixationPoint != null)
                _fixationPoint.SetCarAnchor(basePosition);

            _starField?.SetClearZoneCenter(basePosition);
        }

        // ..........................................................

        // Three calibrations, bound to three hotkeys and used at three moments.
        // "Position" below means all three axes - lateral, forward and height - not
        // just height, whatever the CenterHead_Height hotkey is called.
        //
        //   CalibrateHead          both: eye to CameraStartPosition, gaze to world +Z
        //                          run once at startup, from GeneralScript
        //   CalibrateHeadPosition  position only, leaves the calibrated gaze direction
        //                          alone - run again on every Start
        //   CalibrateHeadRotation  rotation only, and deliberately leaves the
        //                          participant where they are: it solves for an offset
        //                          that preserves the current world eye position

        /// Eye to CameraStartPosition on all three axes, gaze to world +Z.
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

        /// Recentres the eye on CameraStartPosition - lateral, forward and height -
        /// without touching the gaze direction fixed at startup.
        public void CalibrateHeadPosition()
        {
            Quaternion currentRot = Quaternion.Euler(0, _yRotationOffset, 0);
            _manualOffset = -(currentRot * _vrCamera.localPosition);

            ApplyCameraPose();
            Debug.Log("Head position was calibrated (all three axes)");
        }

        // ..........................................................

        // Engage and Disengage bypass MoveByTrajectory entirely - they are daemon-level
        // commands with no position feedback coming back - so _moogPosition, which is
        // only ever a belief about where the platform is, has to be corrected by hand
        // here. Assumed: engaging raises the platform to its operating home, parking
        // lowers it to the park pose.

        private void EngageMoog()
        {
            // Only a running experiment blocks these. Finished must not, or the rig
            // would need an app restart before the next participant; and Paused must
            // not either, since parking during a break is the whole point of Pause.
            if (_experimentState == ExperimentState.Started)
            {
                _experimentTabHandler.PrintToWarnings("Can't Engage now\n");
                return;
            }

            _moogHandler.Engage();
            _moogPosition = _parameters.MoogNeutralPosition;
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
        }

        /// Reads the config file. Called at startup and again on every start, so the
        /// file can be edited and re-run without restarting Unity.
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
                Debug.LogError($"MirrorExperiment: could not read {ParametersFile.FilePath}\n{e.Message}");
            }
        }

        /// Builds the mirror the first time the config asks for one, and from then
        /// on re-applies the config to it - which is also what switches it off.
        /// Returns what is wrong with the mirror settings, or null.
        private string ConfigureMirror()
        {
            if (_mirror == null)
            {
                if (!_parameters.Mirror.Enabled || _starField == null)
                    return null;

                _mirror = CreateChild("RearViewMirror").AddComponent<RearViewMirror>();
            }

            // The stars' material and layer, because the screen has to be visible
            // to exactly the cameras the stars are visible to.
            _mirror.Configure(
                _parameters.Mirror,
                _vrCamera.GetComponent<Camera>(),
                _starField.StarMaterial,
                _starField.gameObject.layer,
                out string problem);

            return problem;
        }

        /// Re-applies the config to the fixation point, which is also what switches
        /// it off. Returns what is wrong with its settings, or null.
        private string ConfigureFixationPoint()
        {
            // The stars' layer where there are stars, for the same reason as the
            // mirror's: visible to exactly the cameras the stars are visible to.
            int layer = _starField != null ? _starField.gameObject.layer : _vrCamera.gameObject.layer;

            _fixationPoint.Configure(
                _parameters.FixationPoint,
                _parameters.Mirror,
                _vrCamera,
                layer,
                out string problem);

            return problem;
        }

        /// The volume that runs EyeMirror, which copies every headset frame into
        /// the operator's "Unity view" panel. Built here rather than kept in the
        /// scene: a pass saved in a scene is dropped if the scene is ever loaded
        /// while its class does not compile, and that has already happened once.
        private void CreateEyeMirrorVolume()
        {
            var target = Resources.Load<RenderTexture>(OperatorViewTexture);

            if (target == null)
            {
                Debug.LogError($"MirrorExperiment: no {OperatorViewTexture} render texture in Resources - the operator's view of the headset will stay blank");
                return;
            }

            var volume = CreateChild("EyeMirrorVolume").AddComponent<CustomPassVolume>();
            volume.isGlobal = true;
            volume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
            volume.AddPassOfType<EyeMirror>().targetTexture = target;
        }

        /// The star cloud, drawn on the headset camera's own layer. The Inspector's
        /// debug toggles - AlwaysVisible, PreviewCoherence - are still there on the
        /// object while the app runs, and reset with every Play, so a preview
        /// cannot be left switched on into a real session.
        ///
        /// Null if the material is missing. There is then no optic flow at all, and
        /// everything that uses the star field already copes with its absence.
        private StarField CreateStarField()
        {
            var material = Resources.Load<Material>(StarMaterialResource);

            if (material == null)
            {
                Debug.LogError($"MirrorExperiment: star material \"{StarMaterialResource}\" not found in any Resources folder - no stars will be drawn");
                return null;
            }

            var stars = CreateChild("Stars");
            stars.layer = _vrCamera.gameObject.layer;

            if ((_vrCamera.GetComponent<Camera>().cullingMask & (1 << stars.layer)) == 0)
                Debug.LogWarning($"MirrorExperiment: the headset camera does not render its own layer {LayerMask.LayerToName(stars.layer)} - the stars will not be seen");

            var starField = stars.AddComponent<StarField>();
            starField.StarMaterial = material;

            return starField;
        }

        /// A new, empty object under the experiment's own root.
        private GameObject CreateChild(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root, false);
            return child;
        }

        private void StartExperiment()
        {
            // Paused counts as running. Starting from here would throw away the
            // half-finished run and its staircases; to begin again, Stop then Start.
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

            string sceneProblem = ConfigureMirror() ?? ConfigureFixationPoint();

            if (sceneProblem != null)
            {
                _experimentTabHandler.PrintToWarnings($"Bad config, fix it and press Start again:\n{ParametersFile.FilePath}\n{sceneProblem}\n");
                return;
            }

            // Everything that carries over between runs is rebuilt here, so a second
            // participant does not need the app restarted. A fresh Experiment also
            // means a fresh StartedAt, and therefore a new data file rather than the
            // previous run's being overwritten.
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
                _starField.Configure(_parameters.StarField);
            }

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

            yield return MoveMoogTo(_parameters.MoogNeutralPosition, _parameters.MoogRepositionDuration);

            while (!_experiment.HasFinished)
            {
                yield return HoldWhilePaused();
                yield return Initializing();

                // Initializing can bail out and ask for a pause - the platform not
                // being where the stimulus needs it. Go back round rather than fall
                // into WaitingToStartSignal, which would then wait forever for a
                // state the trial never reached.
                if (_pauseRequested)
                    continue;

                // here waiting for 'start btn' event
                yield return WaitingToStartSignal();
                yield return Stimulus();
                yield return AnswerPhase();
                yield return Returning();
                yield return Analyzation();
            }

            _trialState = TrialState.None;
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

        /// Just holds, between trials. The next trial has not been prepared yet and
        /// every staircase is untouched, so the run continues exactly where it left
        /// off.
        ///
        /// The platform is deliberately left alone: parking and unparking is the
        /// researcher's call, made with the Park and Engage buttons, which stay live
        /// while paused. Doing it automatically would move the platform under a
        /// participant who may be climbing out of the chair.
        private IEnumerator HoldWhilePaused()
        {
            if (!_pauseRequested)
                yield break;

            _experimentState = ExperimentState.Paused;
            _trialState = TrialState.None;
            _cameraOffset = DofParameters.Zero;
            _starField?.Hide();
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

            // The stimulus trajectory is defined as starting from MoogNeutralPosition,
            // so if the platform is not believed to be there, commanding it would make
            // it lurch to that assumed origin first. Happens when the platform was
            // parked during a break and not engaged again. Halt instead of moving it:
            // never move the platform on an assumption, and never run a trial whose
            // vestibular stimulus would be wrong.
            if (_currentTrial.HasVestibular && _moogPosition != _parameters.MoogNeutralPosition)
            {
                _experimentTabHandler.PrintToWarnings(
                    "Platform is not at its neutral position - press Engage, then Resume\n");

                _pauseRequested = true;
                yield break;
            }

            // No pre-positioning: the platform already sits at its home point and
            // the stimulus travels straight out from there, so engage and park can
            // happen from where it stands.
            yield return new WaitForSeconds((float)_parameters.DelayBeforeStartSound.TotalSeconds);

            if (_parameters.PlayStartTrialSound)
                _sound.PlaySound_Start();

            _trialState = TrialState.ReadyToStart;
        }

        private IEnumerator WaitingToStartSignal()
        {
            while (_trialState != TrialState.PreStimulusPause)
                yield return null;

            yield return new WaitForSeconds((float)_parameters.DelayAfterStartSignal.TotalSeconds);
        }

        private IEnumerator Stimulus()
        {
            _trialState = TrialState.Stimulus;
            _experiment.MarkTrialStarted();

            float stimulusStartedAt = Time.realtimeSinceStartup;

            // --- vestibular half: the platform leads, VR follows after a fixed delay
            if (_currentTrial.HasVestibular)
            {
                var from = _parameters.MoogNeutralPosition;
                var to = from + HeadingToMoogDisplacement(_currentTrial.PhysicalVestibularHeading);

                _moogHandler.MoveByTrajectory(MakeTrajectory(
                    from, to,
                    _parameters.StimulusDuration * _parameters.MoogMovementDurationCorrectionFactor,
                    DelayCompensationStrategy.Ignore));

                _moogPosition = to;
            }

            // Waited on every trial, not just the ones with a platform, so that the
            // stimulus starts at the same moment after the participant's button press
            // regardless of condition.
            yield return new WaitForSeconds((float)_parameters.DelayBetweenMoogAndVr.TotalSeconds);

            // --- visual half
            if (!_currentTrial.HasVisual || _starField == null)
            {
                yield return new WaitForSeconds((float)_parameters.StimulusDuration.TotalSeconds);
            }
            else
            {
                // A fixed point in the world, tracking nothing.
                //
                // Calibration exists precisely to cancel the height the headset
                // starts with and park the eye at CameraStartPosition, and to cancel
                // the head's yaw so that the participant's straight-ahead lines up
                // with world +Z. So after calibration the cloud belongs at
                // CameraStartPosition, axis-aligned with the world - which is also
                // the frame the camera trajectory moves in.
                _starField.Regenerate(CameraHomePosition(), Quaternion.identity);
                _starField.Show(_currentTrial.Coherence, _parameters.NoiseUpdateHz);

                var trajectoryManager = new TrajectoryManager(MakeTrajectory(
                    DofParameters.Zero,
                    HeadingToCameraDisplacement(_currentTrial.PhysicalVisualHeading),
                    _parameters.StimulusDuration,
                    DelayCompensationStrategy.Jump));

                TimeSpan elapsed = TimeSpan.Zero;

                while (elapsed <= _parameters.StimulusDuration)
                {
                    elapsed += TimeSpan.FromSeconds(Time.deltaTime);

                    var nextPosition = trajectoryManager.GetNextPosition();
                    if (nextPosition == null)
                        break;

                    _cameraOffset = nextPosition.Value;
                    yield return null;
                }

                _starField.Hide();
            }

            _currentTrial.MeasuredStimulusSeconds = Time.realtimeSinceStartup - stimulusStartedAt;
            _trialState = TrialState.AnswerPhase;
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

        private IEnumerator Returning()
        {
            yield return new WaitForSeconds((float)_parameters.DelayAfterAnswerSignal.TotalSeconds);

            _cameraOffset = DofParameters.Zero;

            if (_currentTrial.HasVestibular && _moogPosition != _parameters.MoogNeutralPosition)
            {
                _moogHandler.MoveByTrajectory(MakeTrajectory(
                    _moogPosition, _parameters.MoogNeutralPosition,
                    _parameters.MoogReturnDuration, DelayCompensationStrategy.Ignore));

                _moogPosition = _parameters.MoogNeutralPosition;
            }

            // The pause is the same length whether or not the platform had anywhere
            // to go. With interleaved conditions the pacing must not vary: a visual
            // trial that comes round sooner than a vestibular one both tells the
            // participant what the last trial was and gives each condition a
            // different amount of rest.
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

            // Written here rather than at the end of the run: a session that is
            // aborted halfway still leaves everything up to that point on disk.
            _sessionWriter ??= new SessionWriter(_experiment.StartedAt, _parameters);
            _sessionWriter.AppendTrial(_currentTrial);

            yield return null;
        }

        // ..........................................................

        /// Platform displacement for an already-transformed physical heading. The
        /// transform itself lives in Trial, so that the trial record carries the
        /// direction the participant was actually carried in, and so that vestibular
        /// scoring and the platform command cannot drift apart.
        /// Where the eye sits once calibrated, in world coordinates.
        private Vector3 CameraHomePosition()
        {
            return new Vector3(
                _parameters.CameraStartPosition.Sway,
                _parameters.CameraStartPosition.Heave,
                _parameters.CameraStartPosition.Surge);
        }

        private DofParameters HeadingToMoogDisplacement(float physicalHeadingDegrees)
        {
            float rad = physicalHeadingDegrees * Mathf.Deg2Rad;

            return new DofParameters
            {
                Surge = _parameters.Distance * Mathf.Cos(rad),
                Sway = _parameters.Distance * Mathf.Sin(rad)
            };
        }

        /// VR camera displacement for an already-transformed visual heading, in the
        /// same shape as HeadingToMoogDisplacement.
        private DofParameters HeadingToCameraDisplacement(float physicalHeadingDegrees)
        {
            float rad = physicalHeadingDegrees * Mathf.Deg2Rad;
            float distance = _parameters.Distance * _parameters.VisualDistanceMultiplier;

            return new DofParameters
            {
                Surge = distance * Mathf.Cos(rad),
                Sway = distance * Mathf.Sin(rad)
            };
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
        /// duration. The platform runs slower than commanded - that is what
        /// MoogMovementDurationCorrectionFactor documents (0.8 meaning it takes about
        /// 1/0.8 as long) - so waiting the commanded duration alone would step on a
        /// still-moving platform.
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

        private void HandleInput_Left()
        {
            if (_trialState != TrialState.AnswerPhase) return;

            _experiment.SetParticipantAnswer(TrialAnswer.Left);

            if (_parameters.PlayAnswerReceivedSound)
                _sound.PlaySound_GotAnswer();
        }

        private void HandleInput_Right()
        {
            if (_trialState != TrialState.AnswerPhase) return;

            _experiment.SetParticipantAnswer(TrialAnswer.Right);

            if (_parameters.PlayAnswerReceivedSound)
                _sound.PlaySound_GotAnswer();
        }

        private void HandleInput_Start()
        {
            if (_trialState != TrialState.ReadyToStart) return;

            _trialState = TrialState.PreStimulusPause;
        }

        private void PrintStateInfo()
        {
            int trialIndex = _experiment?.GetCurrentTrialIndex() ?? 0;
            int totalTrials = _experiment?.GetTotalTrialsCount() ?? 0;

            // Two blocks on purpose. _currentTrial is assigned during Initializing, so
            // it is already the NEXT trial while the previous answer is still what the
            // researcher wants to see. Showing the finished trial separately means the
            // outcome no longer has to be caught in the gap before the next one.
            string info =
                $"trial: {trialIndex + 1} / {totalTrials}\n" +
                $"condition: {_currentTrial?.Condition.Label ?? "-"}\n" +
                $"heading: {(_currentTrial != null ? _currentTrial.Heading.ToString("F2") + " deg" : "-")}\n" +
                $"coherence: {(_currentTrial != null ? (_currentTrial.Coherence * 100f).ToString("F0") + "%" : "-")}\n" +
                $"level: {_currentTrial?.Difficulty}\n" +
                $"scored vs: {_parameters.CorrectAnswerBasedOn} ({(_currentTrial != null ? _currentTrial.ReferenceHeading.ToString("F2") : "-")} deg)\n" +
                $"correct answer: {_currentTrial?.CorrectAnswer.ToString() ?? "-"}\n" +
                $"received answer: {_currentTrial?.ReceivedAnswer.ToString() ?? "-"}\n\n" +

                $"--- last completed ---\n" +
                $"{DescribeCompletedTrial(_lastCompletedTrial)}\n\n" +

                $"trial state: {_trialState}\n" +
                $"experiment state: {_experimentState}\n" +
                $"white noise: {_sound?.GetNoiseStatus}\n" +
                $"mirror: {(!_parameters.Mirror.Enabled ? "off" : _parameters.Mirror.FlipHorizontally ? "on, flipped" : "on, not flipped")}\n" +
                $"star noise: {_parameters.NoiseUpdateHz:F0} Hz asked / {(_starField != null ? _starField.MeasuredNoiseHz.ToString("F1") : "-")} Hz measured";

            _experimentTabHandler?.PrintToInfo(info, true);
        }

        private static string DescribeCompletedTrial(Trial trial)
        {
            if (trial == null)
                return "-";

            string verdict = trial.ReceivedAnswer == TrialAnswer.Timeout
                ? "TIMEOUT"
                : trial.AnswerIsCorrect ? "correct" : "WRONG";

            return
                $"{trial.Condition.Label}, heading {trial.Heading:F2} deg\n" +
                $"answered {trial.ReceivedAnswer}, expected {trial.CorrectAnswer} -> {verdict}\n" +
                $"response time {(trial.ResponseTime < 0f ? "-" : trial.ResponseTime.ToString("F2") + " s")}, " +
                $"stimulus {(trial.MeasuredStimulusSeconds < 0f ? "-" : trial.MeasuredStimulusSeconds.ToString("F2") + " s")}";
        }
    }
}
