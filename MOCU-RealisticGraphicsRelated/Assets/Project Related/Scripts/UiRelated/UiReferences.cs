using CustomUxmlElements;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UIElements;

// Refactor later, looks strange

public class UiReferences : ManagedMonoBehaviour
{
    public dynamic elements { get => _elements; }
    private dynamic _elements;

    private Dictionary<string, VisualElement> _elements_old;
    public VisualElement root;
    public Dictionary<string, string> _tab_body_relations;


    public override void ManagedAwake ()
    {
        _elements_old = new();
        _tab_body_relations = new();

        root = GetComponent<UIDocument>().rootVisualElement;

        SetupTabBodyRelations();
        Init();
        //SetupReferences();
    }

    private void Init()
    {
        // SEMANTIC CONTAINERS ====================================================================

        // general
        _elements = new SafeExpandoObject();
        _elements.window = new SafeExpandoObject();
        _elements.window.modals = new SafeExpandoObject();

        // tabs
        _elements.experimentTab = new SafeExpandoObject();
        _elements.debugTab = new SafeExpandoObject();
        _elements.settingsTab = new SafeExpandoObject();
        _elements.camerasTab = new SafeExpandoObject();
        _elements.infoTab = new SafeExpandoObject();
        _elements.dataTab = new SafeExpandoObject();

        // experiment tab sub-containers
        _elements.experimentTab.inputsModule = new SafeExpandoObject();
        _elements.experimentTab.parametersModule = new SafeExpandoObject();
        _elements.experimentTab.controlsModule = new SafeExpandoObject();
        _elements.experimentTab.statusesModule = new SafeExpandoObject();
        _elements.experimentTab.outputsModule = new SafeExpandoObject();

        // debug tab sub-containers
        _elements.debugTab.globalTimelineModule = new SafeExpandoObject();
            _elements.debugTab.globalTimelineModule.data = new SafeExpandoObject();
            _elements.debugTab.globalTimelineModule.chart = new SafeExpandoObject();
        _elements.debugTab.lastTrialModule = new SafeExpandoObject();
            _elements.debugTab.lastTrialModule.data = new SafeExpandoObject();
            _elements.debugTab.lastTrialModule.charts = new SafeExpandoObject();
        _elements.debugTab.fpsModule = new SafeExpandoObject();
        _elements.debugTab.devicesModule = new SafeExpandoObject();
        _elements.debugTab.daemonsModule = new SafeExpandoObject();
        _elements.debugTab.diffInfoModule = new SafeExpandoObject();
        _elements.debugTab.consoleModule = new SafeExpandoObject();
        _elements.debugTab.trajectoriesChartModule = new SafeExpandoObject();
        _elements.debugTab.accelerationChartModule = new SafeExpandoObject();

        // settings tab sub-containers
        _elements.settingsTab.themesModule = new SafeExpandoObject();
        _elements.settingsTab.modulesModule = new SafeExpandoObject();
        _elements.settingsTab.configsModule = new SafeExpandoObject();
        _elements.settingsTab.pathsModule = new SafeExpandoObject();
        _elements.settingsTab.devicesModule = new SafeExpandoObject();

        // LINKS ==================================================================================

        // general
        _elements.window.header = root.Q<VisualElement>("main-header");
        _elements.window.body = root.Q<VisualElement>("main-body");

        // modal windows
        _elements.window.modals.modalWindows = root.Q<VisualElement>("modal-windows");
        _elements.window.modals.exitWindow = root.Q<VisualElement>("exit-confirmation-modal-window");

        // WINDOW HEADER --------------------------------------------------------------------------

        // tab btns
        _elements.experimentTab.tabBtn = root.Q<VisualElement>("experiment-tab");
        _elements.debugTab.tabBtn = root.Q<VisualElement>("debug-tab");
        _elements.settingsTab.tabBtn = root.Q<VisualElement>("settings-tab");
        _elements.camerasTab.tabBtn = root.Q<VisualElement>("cameras-tab");
        _elements.infoTab.tabBtn = root.Q<VisualElement>("info-tab");
        _elements.dataTab.tabBtn = root.Q<VisualElement>("data-tab");

        // window close/minimize btns
        _elements.window.modals.closeBtn = root.Q<VisualElement>("close-game-btn");
        _elements.window.modals.confirmCloseBtn = root.Q<VisualElement>("exit-confirm-btn");
        _elements.window.modals.cancelCloseBtn = root.Q<VisualElement>("exit-cancel-btn");
        _elements.window.modals.minimizeBtn = root.Q<VisualElement>("minimize-game-btn");

        // WINDOW BODY - GENERAL ------------------------------------------------------------------

        // tab bodies
        _elements.experimentTab.body = root.Q<VisualElement>("experiment-body");
        _elements.debugTab.body = root.Q<VisualElement>("debug-body");
        _elements.settingsTab.body = root.Q<VisualElement>("settings-body");
        _elements.camerasTab.body = root.Q<VisualElement>("cameras-body");
        _elements.infoTab.body = root.Q<VisualElement>("info-body");
        _elements.dataTab.body = root.Q<VisualElement>("data-body");

        // WINDOW BODY - EXPERIMENT TAB RELATED ---------------------------------------------------

        // inputs
        _elements.experimentTab.inputsModule.protocolDropdown = root.Q<DropdownField>("protocol-dropdown");
        _elements.experimentTab.inputsModule.updateProtocolBtn = root.Q<VisualElement>("protocol-update-btn");
        _elements.experimentTab.inputsModule.saveAsNewProtocolBtn = root.Q<VisualElement>("protocol-saveAsNew-btn");
        _elements.experimentTab.inputsModule.researcherNameInput = root.Q<TextField>("researcher-name-input");
        _elements.experimentTab.inputsModule.participantNameInput = root.Q<TextField>("participant-name-input");

        // outputs
        _elements.experimentTab.outputsModule.info = root.Q<TextElement>("info-module-textbox");
        _elements.experimentTab.outputsModule.warnings = root.Q<TextElement>("warnings-module-textbox");

        // statuses
        _elements.experimentTab.statusesModule.moog = root.Q<VisualElement>("moog-status-block");
        _elements.experimentTab.statusesModule.vr = root.Q<VisualElement>("oculus-status-block");
        _elements.experimentTab.statusesModule.cedrus = root.Q<VisualElement>("cedrus-status-block");
        _elements.experimentTab.statusesModule.gamepad = root.Q<VisualElement>("gamepad-status-block");
        _elements.experimentTab.statusesModule.audio = root.Q<VisualElement>("audio-status-block");
        _elements.experimentTab.statusesModule.eeg = root.Q<VisualElement>("eeg-status-block");
        _elements.experimentTab.statusesModule.trials = root.Q<VisualElement>("trials-status-block");
        _elements.experimentTab.statusesModule.running = root.Q<VisualElement>("running-status-block");

        // controls
        _elements.experimentTab.controlsModule.connectToMoogBtn = root.Q<VisualElement>("moog-connect-btn");
        _elements.experimentTab.controlsModule.engageMoogBtn = root.Q<VisualElement>("moog-engage-btn");
        _elements.experimentTab.controlsModule.parkMoogBtn = root.Q<VisualElement>("moog-park-btn");
        _elements.experimentTab.controlsModule.generateTrialsBtn = root.Q<VisualElement>("trials-generate-btn");
        _elements.experimentTab.controlsModule.generateTestTrialsBtn = root.Q<VisualElement>("test-trials-generate-btn");
        _elements.experimentTab.controlsModule.startExperimentBtn = root.Q<VisualElement>("experiment-start-btn");
        _elements.experimentTab.controlsModule.stopExperimentBtn = root.Q<VisualElement>("experiment-stop-btn");
        _elements.experimentTab.controlsModule.pauseExperimentBtn = root.Q<VisualElement>("experiment-pause-btn");
        _elements.experimentTab.controlsModule.resumeExperimentBtn = root.Q<VisualElement>("experiment-resume-btn");

        _elements.experimentTab.controlsModule.upBtn = root.Q<VisualElement>("controller-up-btn");
        _elements.experimentTab.controlsModule.leftBtn = root.Q<VisualElement>("controller-left-btn");
        _elements.experimentTab.controlsModule.righBtn = root.Q<VisualElement>("controller-right-btn");
        _elements.experimentTab.controlsModule.downBtn = root.Q<VisualElement>("controller-down-btn");
        _elements.experimentTab.controlsModule.centerBtn = root.Q<VisualElement>("controller-center-btn");

        // intercom
        _elements.experimentTab.controlsModule.intercom = root.Q<VisualElement>("controls-intercom-part");

        // WINDOW BODY - DEBUG TAB RELATED --------------------------------------------------------

        // test btns
        _elements.debugTab.testBtn1 = root.Q<VisualElement>("debug-test-btn-1");
        _elements.debugTab.testBtn2 = root.Q<VisualElement>("debug-test-btn-2");

        // console
        _elements.debugTab.consoleModule.console = root.Q<TextElement>("debug-console-module-textbox");

        // global timeline
        _elements.debugTab.globalTimelineModule.data.totalTime = root.Q<TextElement>("experiment-timeline-data-total-time");
        _elements.debugTab.globalTimelineModule.data.status = root.Q<TextElement>("experiment-timeline-data-status");
        _elements.debugTab.globalTimelineModule.data.totalTrials = root.Q<TextElement>("experiment-timeline-data-total-trials");
        _elements.debugTab.globalTimelineModule.data.defectiveTrials = root.Q<TextElement>("experiment-timeline-data-defective-trials");
        _elements.debugTab.globalTimelineModule.data.dangerousTrials = root.Q<TextElement>("experiment-timeline-data-dangerous-trials");

        // trial
        _elements.debugTab.lastTrialModule.data.unityFps = root.Q<TextElement>("trial-data-average-unity-fps");
        _elements.debugTab.lastTrialModule.data.totalTimeMoog = root.Q<TextElement>("trial-data-total-time-moog");
        _elements.debugTab.lastTrialModule.data.totalTimeVr = root.Q<TextElement>("trial-data-total-time-oculus");
        _elements.debugTab.lastTrialModule.data.trialStatus = root.Q<TextElement>("trial-data-trial-status");
        _elements.debugTab.lastTrialModule.data.receivedResponse = root.Q<TextElement>("trial-data-answer-get-in");
        _elements.debugTab.lastTrialModule.data.expectedResponse = root.Q<TextElement>("trial-data-received-answer");
        _elements.debugTab.lastTrialModule.data.responseIsCorrect = root.Q<TextElement>("trial-data-received-answer-is-correct");

        // diff. info
        _elements.debugTab.diffInfoModule.memory = root.Q<TextElement>("debug-memory-value");
        _elements.debugTab.diffInfoModule.gc = root.Q<TextElement>("debug-gc-value");
        _elements.debugTab.diffInfoModule.totalTimeWorking = root.Q<TextElement>("debug-time-working-value");

        // fps
        _elements.debugTab.fpsModule.fps = root.Q<TextElement>("debug-current-fps-value");
        _elements.debugTab.fpsModule.lastFrameTime = root.Q<TextElement>("debug-last-frame-value");
        _elements.debugTab.fpsModule.averageFrameTime = root.Q<TextElement>("debug-average-frame-value");

        // daemons
        _elements.debugTab.daemonsModule.totalNumber = root.Q<TextElement>("number_of_running_daemons_label");
        _elements.debugTab.daemonsModule.activities = root.Q<ScrollView>("debug-daemons-activities");
        _elements.debugTab.daemonsModule.activitiesText = root.Q<TextElement>("debug-daemons-activities-text");

        // WINDOW BODY - SETTINGS TAB RELATED -----------------------------------------------------

        // devices main window
        _elements.settingsTab.devicesModule.microphoneResearcher = root.Q<VisualElement>("settings-device-box-microphone-researcher");
        _elements.settingsTab.devicesModule.microphoneParticipant = root.Q<VisualElement>("settings-device-box-microphone-participant");
        _elements.settingsTab.devicesModule.speakerResearcher = root.Q<VisualElement>("settings-device-box-speaker-researcher");
        _elements.settingsTab.devicesModule.speakerParticipant = root.Q<VisualElement>("settings-device-box-speaker-participant");
        _elements.settingsTab.devicesModule.camera1 = root.Q<VisualElement>("settings-device-box-camera-1");
        _elements.settingsTab.devicesModule.camera2 = root.Q<VisualElement>("settings-device-box-camera-2");
        _elements.settingsTab.devicesModule.controller = root.Q<VisualElement>("settings-device-box-controller");

        // devices secondary window
        _elements.settingsTab.devicesModule.choosingWindow = root.Q<VisualElement>("settings-devices-choose-device-window");
        _elements.settingsTab.devicesModule.backToMainBtn = root.Q<VisualElement>("settings-devices-back-btn");
    }



    private void SetupTabBodyRelations()
    {
        _tab_body_relations.Add("experiment-tab", "experiment-body");
        _tab_body_relations.Add("debug-tab", "debug-body");
        _tab_body_relations.Add("data-tab", "data-body");
        _tab_body_relations.Add("info-tab", "info-body");
        _tab_body_relations.Add("cameras-tab", "cameras-body");
        _tab_body_relations.Add("settings-tab", "settings-body");
    }

    public VisualElement GetElement(string name)
    {
        return root.Q<VisualElement>(name);
        //return _elements_old.ContainsKey(name) ? _elements_old[name] : null;
    }

    public string GetTabBodyRelation(string tabName)
    {
        return _tab_body_relations.ContainsKey(tabName) ? _tab_body_relations[tabName] : null;
    }

    public List<VisualElement> GetHeaderTabs()
    {
        return root.Query<VisualElement>().Class("tab").ToList();
    }

    public List<SettingsDeviceBox> GetDevicesBoxes()
    {
        return root.Query<SettingsDeviceBox>().ToList();
    }
}