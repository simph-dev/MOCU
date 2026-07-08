using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

using Debug = UnityEngine.Debug;


public class StatusesHandler : ManagedMonoBehaviour
{
    //private CedrusHandler _cedrus;
    private UiHandler _uiHandler;
    private InputHandler _inputHandler;
    private VrHandler _vrHandler;
    private AudioHandler _audioHandler;
    private ExperimentTabHandler _experimentTabHandler;

    private Dictionary<string, (IModuleStatusHandler stateTracker, VisualElement visualElement)> _deviceNameToUxmlBlockMap;
    private Dictionary<ModuleStatus, string> _deviceConnectionStatusToColorMap;


    public override void ManagedAwake()
    {
        //_cedrus = GetComponent<CedrusHandler>();
        _uiHandler = GetComponent<UiHandler>();
        _inputHandler = GetComponent<InputHandler>();
        _vrHandler = GetComponent<VrHandler>();
        _audioHandler = GetComponent<AudioHandler>();
        _experimentTabHandler = GetComponent<ExperimentTabHandler>();
    }

    public override void ManagedStart()
    {
        // don't move it to "Awake", otherwise ".mainUiScreen.GetElement("moog-status-block")" can be inaccessible

        _deviceNameToUxmlBlockMap = new()
        {
            //{ "Moog",       (stateTracker: _inputHandler.XRConnectionStatus, visualElement: _uiHandler.mainUiScreen.GetElement("gamepad-status-block"))_uiHandler.mainUiScreen.GetElement("moog-status-block") },
            
            { "VR",     (stateTracker: (IModuleStatusHandler)_vrHandler.XRConnectionStatus, visualElement: (VisualElement)_uiHandler.mainUiScreen.elements.experimentTab.statusesModule.vr) },
            //{ "Cedrus_old", (stateTracker: _cedrus.stateTracker, visualElement: (VisualElement)_uiHandler.mainUiScreen.elements.experimentTab.statusesModule.cedrus) },
            //{ "Gamepad",    (stateTracker: _inputHandler.GamepadConnectionStatus, visualElement: (VisualElement)_uiHandler.mainUiScreen.elements.experimentTab.statusesModule.gamepad) },
            { "Audio",      (stateTracker: (IModuleStatusHandler) _audioHandler.stateTracker, visualElement: (VisualElement)_uiHandler.mainUiScreen.elements.experimentTab.statusesModule.audio) },

            //{ "EEG",        _uiHandler.mainUiScreen.GetElement("eeg-status-block") },
            //{ "Trials",     _uiHandler.mainUiScreen.GetElement("trials-status-block") },
            //{ "Running",    _uiHandler.mainUiScreen.GetElement("running-status-block") }
        };

        _deviceConnectionStatusToColorMap = new()
        {
            { ModuleStatus.FullyOperational,        "#5580ED" },    // blue
            { ModuleStatus.PartiallyOperational,    "#9855ED" },    // violet
            { ModuleStatus.NotOperational,          "#FC5858" },    // red
            { ModuleStatus.InSetup,                 "#FCBA58" },    // yellow
            { ModuleStatus.Inactive,                "#9A9B9B" }     // gray
        };

        CanUseUpdateMethod = true;
    }


    public override void ManagedUpdate()
    {
        // todo: change later to "update when changed" or "update with a lower frequency"

        foreach (var device in _deviceNameToUxmlBlockMap)
            UpdateStatusBox(device.Key);
    }



    private void UpdateStatusBox(string deviceName)
    {
        if (_deviceNameToUxmlBlockMap.TryGetValue(deviceName, out var paramsTuple))
        {
            VisualElement statusBlockColor = paramsTuple.visualElement.Q<VisualElement>(className: "status-block-color");

            if (ColorUtility.TryParseHtmlString(_deviceConnectionStatusToColorMap[paramsTuple.stateTracker.Status], out Color color))
            {
                statusBlockColor.style.backgroundColor = new StyleColor(color);
            }
            else
            {
                _experimentTabHandler.PrintToWarnings("Couldn't translate color");
            }
        }
    }
}