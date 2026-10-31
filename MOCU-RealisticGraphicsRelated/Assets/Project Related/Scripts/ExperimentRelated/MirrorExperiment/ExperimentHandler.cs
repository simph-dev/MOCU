using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using MoogModule;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

using HeadsetSnapshot = SnapshotModule.HeadsetSnapshot;

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

        /// Everything this experiment shows is built in code, under this root of its
        /// own - the star field, the fixation point, the mirror and the volume
        /// feeding the operator's view. The scene holds none of it, so none of it
        /// can be lost from the scene or left disagreeing with the config.
        private Transform _root;

        /// Null until the config first asks for it, so a run without a screen has
        /// no rear camera costing a render every frame.
        private RearView _rearView;

        /// Built in code at startup, like the screen, and switched off rather than
        /// destroyed when the config says so.
        private FixationPoint _fixationPoint;

        /// Debug: the head moved with the keyboard. On the MirrorExperiment object,
        /// off unless switched on there while the app runs.
        private KeyboardHead _keyboardHead;

        /// Where the operator's "Unity view" panel gets its picture from: the
        /// EyeMirror pass copies every headset frame into this texture.
        private const string OperatorViewTexture = "GUI/UnityViewFromUi";

        /// HDRP Unlit, white, Double-Sided and GPU Instancing on. In
        /// MirrorExperiment/Resources, so a build includes it without the scene.
        private const string StarMaterialResource = "StarMaterial";

        /// The stars have a layer of their own, so that the headset camera can be
        /// shown the field seen directly or not by its culling mask alone. Any other
        /// camera - the Scene view, one recording from the side - always sees them.
        private const string StarsLayerName = "Stars";

        /// What the mirror's cameras are shown instead: a share of the same stars,
        /// drawn a second time on a layer of its own and possibly larger, so that
        /// stars in the mirror can be made to look like stars seen directly (see
        /// RearViewSettings.MirrorViewStarFraction). The headset camera never renders
        /// it, and the mirror's cameras never render the Stars layer.
        private const string MirrorStarsLayerName = "MirrorStars";

        /// The inside of the car - the mirror's screen, its frame, the fixation
        /// point - has a layer of its own too. The headset camera renders it; the
        /// mirror's cameras do not, since in the Mirror view they look straight at
        /// the screen they render into.
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

        /// Set by the Pause button. Takes effect at once: a trial it cuts into is
        /// dropped and run again after Resume. The platform is never stopped
        /// halfway - it follows a wall-clock trajectory - but finishes the move it
        /// is making before it goes back (DropTrial).
        private bool _pauseRequested;

        /// When answers start being taken, realtime seconds: AnswerWindowBeforeEnd
        /// before the stimulus is due to end. Set as it starts.
        private float _answersOpenAt;

        /// When the stimulus actually ended, and when the answer came - or the time
        /// to answer ran out - in realtime seconds.
        private float _motionEndedAt;
        private float _answeredAt;

        /// Pictures for figures (SnapshotModule), posed with the car in
        /// ApplyCameraPose; and the conditions already taken in this run.
        private HeadsetSnapshot _snapshot;
        private readonly HashSet<int> _snapshotsTaken = new();

        /// The pass copying the headset's picture to the operator's panel, kept to
        /// switch which eye it copies.
        private EyeMirror _eyeMirror;

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
            _headsetCamera = _vrCamera.GetComponent<Camera>();

            _cabinLayer = LayerMask.NameToLayer(CabinLayerName);

            if (_cabinLayer < 0)
                Debug.LogError($"MirrorExperiment: no \"{CabinLayerName}\" layer in Tags and Layers - the mirror cannot be built");
            else
                SetLayerRendered(_headsetCamera, 1 << _cabinLayer, true);

            _root = new GameObject("MirrorExperiment").transform;

            _keyboardHead = _root.gameObject.AddComponent<KeyboardHead>();
            _keyboardHead.SetHead(_vrCamera);

            _starField = CreateStarField();

            CreateEyeMirrorVolume();

            _fixationPoint = CreateChild("FixationPoint").AddComponent<FixationPoint>();

            _snapshot = CreateChild("Snapshot").AddComponent<HeadsetSnapshot>();

            // Built already here, not only on Start, so that the star cloud knows
            // how far the screen's corners reach. Start reports a bad setting to
            // the researcher; this early there is only the log.
            string screenProblem = ConfigureRearView();

            if (screenProblem != null)
                Debug.LogError($"MirrorExperiment: {screenProblem}");

            // Already here, not only on Start, so that the AlwaysVisible preview
            // shows the cloud the config describes rather than the Inspector's.
            // After the screen, whose image says how far its corners reach - and
            // with the screen still on.
            _starField?.Configure(_parameters.StarField, StarHoleRadius());

            // The headset camera does not render the stars' layer until told to.
            ShowStarsTo(outside: true, inside: true);

            // Until Start: the fixation point alone, in front of the head.
            ShowIdleLook();

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
        /// Also moves what is fixed to the car - the mirror and, unless it is set
        /// to follow the head, the fixation point. They
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

            _cameras.rotation = _rigRotation;
            _cameras.position = basePosition + _manualOffset;

            // Debug: the eye goes where the keyboard puts it, relative to its
            // calibrated place. The rig is moved so as to cancel whatever position
            // the headset reports, which leaves only the headset's rotation. The car
            // below is left alone, as when a participant moves their head.
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

        // Three calibrations, bound to three hotkeys. "Position" below means all
        // three axes - lateral, forward and height - not just height, whatever the
        // CenterHead_Height hotkey is called. "Rotation" means the head's whole
        // rotation - yaw, pitch and roll: "look straight ahead and keep still" is
        // the zero (see _rigRotation). Before any calibration, level is the
        // headset's own: gravity.
        //
        //   CalibrateHead          both: eye to CameraStartPosition, gaze to world +Z
        //                          run at startup, from GeneralScript - where only
        //                          the angles matter, since until Start everything
        //                          shown follows the head - and again on every Start
        //   CalibrateHeadPosition  position only, leaves the calibrated rotation alone
        //   CalibrateHeadRotation  rotation only, and deliberately leaves the
        //                          participant where they are: it solves for an offset
        //                          that preserves the current world eye position

        /// Eye to CameraStartPosition on all three axes, gaze to world +Z, level.
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

        /// Recentres the eye on CameraStartPosition - lateral, forward and height -
        /// without touching the calibrated rotation.
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

        /// Builds the screen the first time the config asks for one, and from then
        /// on re-applies the config to it - which is also what switches it off.
        /// Returns what is wrong with the rear-view settings, or null.
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

            // The cabin layer: always rendered by the headset camera, so the screen
            // stays in every condition, and never by the screen's own cameras.
            _rearView.Configure(
                _parameters.RearView,
                _headsetCamera,
                _cabinLayer,
                out string problem);

            return problem;
        }

        /// Re-applies the config to the fixation point, which is also what switches
        /// it off. Returns what is wrong with its settings, or null.
        ///
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

            // The cabin layer, so that the mirror does not show it a second time,
            // reflected. The headset camera's own layer if there is no cabin layer -
            // then there is no mirror either.
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
                Debug.LogError($"MirrorExperiment: {problem}");
        }

        /// Shows the stars, or not, to the headset camera - the field seen directly -
        /// and to the mirror's cameras - the field in the mirror - by the two star
        /// layers in their culling masks: each camera only ever its own layer, and
        /// that one on or off. Both on outside the stimulus, so the preview and
        /// every other view of the scene work as usual.
        ///
        /// Also hands the star field the share and scale of the stars the mirror's
        /// current view asks for, so it is to be called again once the view has
        /// changed, before anything is shown.
        private void ShowStarsTo(bool outside, bool inside)
        {
            if (_starField == null)
                return;

            int starsBit = 1 << _starField.gameObject.layer;
            int mirrorStarsBit = _starField.MirrorLayer >= 0 ? 1 << _starField.MirrorLayer : 0;

            SetLayerRendered(_headsetCamera, starsBit, outside);
            SetLayerRendered(_headsetCamera, mirrorStarsBit, false);

            // Off with the config, the mirror is kept but switched off: then its
            // share of the stars is not drawn at all.
            bool mirrorOn = _rearView != null && _rearView.gameObject.activeSelf;

            _starField.SetMirrorStars(mirrorOn ? _rearView.StarFraction : 0f, mirrorOn ? _rearView.StarScale : 1f);

            if (_rearView == null)
                return;

            foreach (var camera in _rearView.Cameras)
            {
                SetLayerRendered(camera, starsBit, false);
                SetLayerRendered(camera, mirrorStarsBit, inside);
            }
        }

        /// Refuses a run whose conditions need stars there are none of - the log
        /// says why. Otherwise those trials would run as blank screens.
        private string FindMissingStars()
        {
            if (_starField != null || !_parameters.Conditions.Exists(c => c.Enabled && c.HasAnyVisual))
                return null;

            return "No star field (see the log for why) but some conditions are visual";
        }

        private static void SetLayerRendered(Camera camera, int layerBit, bool rendered)
        {
            camera.cullingMask = rendered ? camera.cullingMask | layerBit : camera.cullingMask & ~layerBit;
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
            _eyeMirror = volume.AddPassOfType<EyeMirror>();
            _eyeMirror.targetTexture = target;
            _eyeMirror.eye = _parameters.OperatorViewEye;
        }

        /// The star cloud, on the Stars layer (see StarsLayerName). The Inspector's
        /// debug toggles - AlwaysVisible, PreviewCoherence - are still there on the
        /// object while the app runs, and reset with every Play, so a preview
        /// cannot be left switched on into a real session.
        ///
        /// Null if the material or the layer is missing. There is then no optic flow
        /// at all, and Start refuses conditions that need it.
        private StarField CreateStarField()
        {
            var material = Resources.Load<Material>(StarMaterialResource);

            if (material == null)
            {
                Debug.LogError($"MirrorExperiment: star material \"{StarMaterialResource}\" not found in any Resources folder - no stars will be drawn");
                return null;
            }

            int layer = LayerMask.NameToLayer(StarsLayerName);

            if (layer < 0)
            {
                Debug.LogError($"MirrorExperiment: no \"{StarsLayerName}\" layer in Tags and Layers - no stars will be drawn");
                return null;
            }

            var stars = CreateChild("Stars");
            stars.layer = layer;

            var starField = stars.AddComponent<StarField>();
            starField.StarMaterial = material;

            // -1 if the layer is missing. Only the mirror needs it, and refuses to
            // be built without it (ConfigureRearView).
            starField.MirrorLayer = LayerMask.NameToLayer(MirrorStarsLayerName);

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

            string problem = Experiment.FindConditionProblem(_parameters)
                             ?? FindMissingStars()
                             ?? ConfigureRearView()
                             ?? ConfigureFixationPoint(running: true);

            // Before giving up on a bad config, not after: a mirror configured just
            // now copied the headset camera's culling mask, star layers and all,
            // whether or not what came after it passed.
            ShowStarsTo(outside: true, inside: true);

            if (problem != null)
            {
                // The screen may have just been switched on with the config.
                ShowIdleLook();
                _experimentTabHandler.PrintToWarnings($"Bad config, fix it and press Start again:\n{ParametersFile.FilePath}\n{problem}\n");
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

                // Pause takes effect at once (RequestPause). Before the start
                // signal nothing has moved yet: straight on to the pause.
                if (_pauseRequested)
                    continue;

                yield return Stimulus();

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

            _trialState = TrialState.None;
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

            bool moving = _trialState == TrialState.Stimulus;
            bool answering = _trialState == TrialState.AnswerPhase && _currentTrial?.ReceivedAnswer == TrialAnswer.None;

            _experimentTabHandler.PrintToWarnings(moving || answering
                ? "Pausing now - this trial is dropped and runs again after Resume\n"
                : "Pausing now\n");
        }

        /// Pause pressed in the middle of a trial, before its answer: the trial is
        /// dropped - not recorded, and its staircase not moved - and runs again
        /// after Resume, with a heading drawn afresh. The stars go at once. The
        /// platform finishes the move it is making, since it cannot be stopped
        /// halfway, and then goes back to neutral, where the pause holds it.
        private IEnumerator DropTrial()
        {
            _trialState = TrialState.Returning;
            _starField?.Hide();
            ShowStarsTo(outside: true, inside: true);
            _cameraOffset = DofParameters.Zero;

            // An answer from the last moments of a stimulus cut short is not kept:
            // the trial will be run again in full.
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

        /// Just holds, between trials. The next trial has not been prepared yet and
        /// every staircase is untouched, so the run continues exactly where it left
        /// off. A trial Pause cut into has already brought the platform back to
        /// neutral (DropTrial).
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

            // Set here, well before the stimulus, so the switch between the Mirror
            // and the Screen view is over by the time there is anything to see.
            if (_rearView != null)
            {
                _rearView.SetView(_currentTrial.Condition.InsideView);

                // The new view may show another share of the stars, at another size.
                ShowStarsTo(outside: true, inside: true);
            }

            // The stimulus trajectory is defined as starting from MoogNeutralPosition,
            // so if the platform is not believed to be there, commanding it would make
            // it lurch to that assumed origin first. Happens when the platform was
            // parked during a break and not engaged again. Halt instead of moving it:
            // never move the platform on an assumption, and never run a trial whose
            // vestibular stimulus would be wrong.
            if (_currentTrial.HasVestibular && (_moogParked || _moogPosition != _parameters.MoogNeutralPosition))
            {
                _experimentTabHandler.PrintToWarnings(_moogParked
                    ? "Platform is parked - press Engage, then Resume\n"
                    : "Platform is not at its neutral position - press Engage, then Resume\n");

                _pauseRequested = true;
                yield break;
            }

            // No pre-positioning: the platform already sits at its home point and
            // the stimulus travels straight out from there, so engage and park can
            // happen from where it stands.
            yield return WaitUnlessPaused((float)_parameters.DelayBeforeStartSound.TotalSeconds);

            if (_pauseRequested)
                yield break;

            if (_parameters.PlayStartTrialSound)
                _sound.PlaySound_Start();

            _trialState = TrialState.ReadyToStart;
        }

        private IEnumerator WaitingToStartSignal()
        {
            while (_trialState != TrialState.PreStimulusPause && !_pauseRequested)
                yield return null;

            yield return WaitUnlessPaused((float)_parameters.DelayAfterStartSignal.TotalSeconds);
        }

        private IEnumerator Stimulus()
        {
            float stimulusStartedAt = Time.realtimeSinceStartup;

            _answersOpenAt = stimulusStartedAt + (float)(
                _parameters.DelayBetweenMoogAndVr + _parameters.StimulusDuration - _parameters.AnswerWindowBeforeEnd).TotalSeconds;

            _trialState = TrialState.Stimulus;
            _experiment.MarkTrialStarted();

            // --- vestibular half: the platform leads, VR follows after a fixed delay
            if (_currentTrial.HasVestibular)
            {
                var from = _parameters.MoogNeutralPosition;
                var to = from + HeadingToMoogDisplacement(_currentTrial.PhysicalVestibularHeading);
                var commanded = _parameters.StimulusDuration * _parameters.MoogMovementDurationCorrectionFactor;

                _moogHandler.MoveByTrajectory(MakeTrajectory(from, to, commanded, DelayCompensationStrategy.Ignore));

                _moogPosition = to;
                _moogBusyUntil = Time.realtimeSinceStartup + MoogTravelWaitSeconds(commanded);
            }

            // Waited on every trial, not just the ones with a platform, so that the
            // stimulus starts at the same moment after the participant's button press
            // regardless of condition.
            yield return WaitUnlessPaused((float)_parameters.DelayBetweenMoogAndVr.TotalSeconds);

            // Pause ends the stimulus at once - nothing more is shown, and the trial
            // is dropped (Loop).
            if (_pauseRequested)
            {
                _currentTrial.MeasuredStimulusSeconds = Time.realtimeSinceStartup - stimulusStartedAt;
                yield break;
            }

            // --- visual half
            if (!_currentTrial.Condition.HasAnyVisual || _starField == null)
            {
                yield return WaitUnlessPaused((float)_parameters.StimulusDuration.TotalSeconds);
            }
            else
            {
                // A fixed point in the world, tracking nothing.
                //
                // Calibration exists precisely to cancel the height the headset
                // starts with and park the eye at CameraStartPosition, and to cancel
                // the head's yaw so that the participant's straight-ahead lines up
                // with world +Z. So after calibration the cloud belongs at
                // CameraStartPosition, its axis upright in the world - which is also
                // the frame the camera trajectory moves in.
                _starField.Regenerate(CameraHomePosition());

                // One cloud and one camera motion for both fields: the screen's
                // cameras ride with the car, so inside and outside always show the
                // same movement - behind the car on the screen, as a car's mirror or
                // parking display shows it, and ahead of it outside. Which of the
                // two get to see the stars is the condition's choice.
                ShowStarsTo(_currentTrial.HasVisualOutside, _currentTrial.HasVisualInside);
                _starField.Show(_currentTrial.Coherence, _parameters.NoiseUpdateHz);

                if (_parameters.Snapshots.Enabled && _snapshotsTaken.Add(_currentTrial.ConditionIndex))
                    TakeSnapshots(_currentTrial.Condition.Label);

                var trajectoryManager = new TrajectoryManager(MakeTrajectory(
                    DofParameters.Zero,
                    HeadingToCameraDisplacement(_currentTrial.PhysicalVisualHeading),
                    _parameters.StimulusDuration,
                    DelayCompensationStrategy.Jump));

                TimeSpan elapsed = TimeSpan.Zero;

                while (elapsed <= _parameters.StimulusDuration && !_pauseRequested)
                {
                    elapsed += TimeSpan.FromSeconds(Time.deltaTime);

                    var nextPosition = trajectoryManager.GetNextPosition();
                    if (nextPosition == null)
                        break;

                    _cameraOffset = nextPosition.Value;
                    yield return null;
                }

                // Both in the same frame is safe only because StarField draws in
                // LateUpdate, after this coroutine: hidden now, the cloud is not
                // drawn into the masks put back now.
                _starField.Hide();
                ShowStarsTo(outside: true, inside: true);
            }

            _currentTrial.MeasuredStimulusSeconds = Time.realtimeSinceStartup - stimulusStartedAt;
        }

        /// The middle of the stimulus that has just started, saved by the snapshot
        /// module: the head held at the eye's place in the car, straight ahead and
        /// level (see SetAnchor in ApplyCameraPose).
        private void TakeSnapshots(string label)
        {
            _snapshot.Take(
                _parameters.Snapshots,
                _headsetCamera,
                Path.Combine(ParametersFile.DefaultFolderPath, "Snapshots"),
                $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{label}",
                (float)_parameters.StimulusDuration.TotalSeconds / 2f,
                path => _experimentTabHandler.PrintToWarnings($"Snapshot saved: {Path.GetFileName(path)}\n"));
        }

        /// The answer may already be in, from the last AnswerWindowBeforeEnd of the
        /// stimulus. The time to answer runs from the stimulus's end either way,
        /// and so does the response time.
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

        /// The platform back to neutral, ready for the next trial: once its move is
        /// over, PauseBeforeReturn after the stimulus ended and
        /// DelayAfterAnswerSignal after the answer - whichever comes last, so an
        /// early answer does not send it straight back.
        private IEnumerator Returning()
        {
            float until = Mathf.Max(
                _moogBusyUntil,
                _motionEndedAt + (float)_parameters.PauseBeforeReturn.TotalSeconds,
                _answeredAt + (float)_parameters.DelayAfterAnswerSignal.TotalSeconds);

            while (Time.realtimeSinceStartup < until)
                yield return null;

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

        /// Where the eye sits once calibrated, in world coordinates.
        private Vector3 CameraHomePosition()
        {
            return new Vector3(
                _parameters.CameraStartPosition.Sway,
                _parameters.CameraStartPosition.Heave,
                _parameters.CameraStartPosition.Surge);
        }

        /// The hole in the middle of the star torus: the furthest corner of the
        /// screen, frame and all, plus however far a trial carries the car, plus a
        /// star. The torus is made where the car starts and the car may go that far
        /// in any direction, so wherever it is on its way no star is between the
        /// eye and the screen, nor touching its corners - and none can be, whatever
        /// the head does inside the car.
        ///
        /// The corners are the built screen's, whose image says how high it is.
        /// With no screen built, the plain rectangle the settings describe stands
        /// in: the screen is where the car's inside ends either way.
        private float StarHoleRadius()
        {
            float travel = _parameters.Distance * Mathf.Abs(_parameters.VisualDistanceMultiplier);
            float star = Mathf.Max(_parameters.StarField.StarWidth, _parameters.StarField.StarHeight);

            float corner = _rearView != null && _rearView.gameObject.activeSelf && _rearView.FurthestCornerDistance > 0f
                ? _rearView.FurthestCornerDistance
                : _parameters.RearView.FurthestCornerDistance();

            return corner + travel + star;
        }

        /// Platform displacement for an already-transformed physical heading. The
        /// transform itself lives in Trial, so that the trial record carries the
        /// direction the participant was actually carried in, and so that vestibular
        /// scoring and the platform command cannot drift apart.
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

        /// The first answer of the trial counts, given in the answer phase or
        /// already in the last AnswerWindowBeforeEnd of the stimulus.
        private bool AnswersOpen =>
            _currentTrial != null && _currentTrial.ReceivedAnswer == TrialAnswer.None
            && (_trialState == TrialState.AnswerPhase
                || _trialState == TrialState.Stimulus && Time.realtimeSinceStartup >= _answersOpenAt);

        private void HandleInput_Left() => TakeAnswer(TrialAnswer.Left);

        private void HandleInput_Right() => TakeAnswer(TrialAnswer.Right);

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
                $"rear view: {DescribeRearView()}\n" +
                $"head: {DescribeHead()}\n" +
                $"calibration: {DescribeCalibration()}\n" +
                $"operator view: {(_eyeMirror != null ? _eyeMirror.eye.ToString().ToLowerInvariant() + " eye" : "-")}\n" +
                $"config: {ParametersFile.FilePath}\n" +
                $"star noise: {_parameters.NoiseUpdateHz:F0} Hz asked / {(_starField != null ? _starField.MeasuredNoiseHz.ToString("F1") : "-")} Hz measured";

            _experimentTabHandler?.PrintToInfo(info, true);
        }

        /// How the screen is filled in the current trial: the view changes from
        /// trial to trial.
        private string DescribeRearView()
        {
            if (!_parameters.RearView.Enabled)
                return "off";

            if (_currentTrial == null)
                return "on";

            return $"on, {_currentTrial.Condition.InsideView.ToString().ToLowerInvariant()} view";
        }

        /// Where the eye is relative to its place in the car, in centimeters. It
        /// changes as the participant leans in the chair, while the car - the
        /// screen, the fixation point - stays put. If it does not change, the
        /// headset is tracking its rotation only, not its position, and the whole
        /// scene follows the head about.
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
                $"{trial.Condition.Label}, heading {trial.Heading:F2} deg\n" +
                $"answered {trial.ReceivedAnswer}, expected {trial.CorrectAnswer} -> {verdict}\n" +
                $"response time {(trial.ResponseTime == null ? "-" : trial.ResponseTime.Value.ToString("+0.00;-0.00;0.00") + " s after the end")}, " +
                $"stimulus {(trial.MeasuredStimulusSeconds < 0f ? "-" : trial.MeasuredStimulusSeconds.ToString("F2") + " s")}";
        }
    }
}
