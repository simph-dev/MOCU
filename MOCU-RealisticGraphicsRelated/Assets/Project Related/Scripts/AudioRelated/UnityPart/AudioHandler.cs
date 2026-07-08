using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

using AudioControl;
using InterprocessCommunication;

using Debug = UnityEngine.Debug;
using AudioModule;

// todo: not allow intercom (on the client side) if any device is missing (null)


public class AudioHandler : ManagedMonoBehaviour, IDaemonUser
{
    private DaemonHandler_Client _daemon;
    private AudioHostSideBridge _newDaemon;

    private UiHandler _uiHandler;
    private ConfigHandler _configHandler;
    private ExperimentTabHandler _experimentTabHandler;
    private SettingsTabHandler _settingsTabHandler;
    private InputLogic _inputLogic;
    private DaemonsHandler _daemonsHandler;
    private DebugTabHandler _debugTabHandler;

    private Dictionary<string, UnifiedCommandFrom_Client> partlyOptimizedJsonCommands; // in theory, should reduce the delay when sending commands. todo: review later
    private Dictionary<string, (Audio_ModuleSubStatuses? subState, Action<UnifiedResponseFrom_Server> action)> commandsToExecuteAccordingToServerResponse;  // serverResponse -> updState -> executeNextCommand

    public ModuleStatusHandler<Audio_ModuleSubStatuses> stateTracker { get; private set; }
    public AudioDevices audioDevices { get; private set; }


    /// <summary>
    /// Returns data that client has (since last update).
    /// May not be the most relevant at the moment. To get most updated one -- call 'RequestAudioDataUpdate' and then 'GetAudioData').
    /// In case of UI (to get ost apdated data) enough to call 'RequestAudioDataUpdate'. UI update method will be called automatically.
    /// </summary>
    public UnifiedAudioDataPacket GetAudioData()
    {
        return audioDevices.PackAllData();
    }

    public void SendTestAudioSignalToDevice(string audioOutputDeviceName, string audioFileName = "test.mp3")    // todo: move 'audioFileName' to config
    {
        if (_daemon == null || !_daemon.isConnectionOk || !_daemon.isProcessOk)
        {
            Debug.LogError("Custom: 'SendTestAudioSignalToDevice' is unavailable right now");
            return;
        }
        _daemon.SendCommand(new UnifiedCommandFrom_Client(name: "PlayAudioFile_Command", extraData: new PlayAudioFile_CommandDetails(audioFileName: audioFileName, audioOutputDeviceName: audioOutputDeviceName)));
    }
    public void PlayAudioClip(string clipName, string deviceName) { }


    #region MANDATORY STANDARD FUNCTIONALITY

    public override void ManagedAwake()
    {
        _uiHandler = GetComponent<UiHandler>();
        _configHandler = GetComponent<ConfigHandler>();
        _experimentTabHandler = GetComponent<ExperimentTabHandler>();
        _settingsTabHandler = GetComponent<SettingsTabHandler>();
        _inputLogic = GetComponent<InputLogic>();
        _daemonsHandler = GetComponent<DaemonsHandler>();
        _debugTabHandler = GetComponent<DebugTabHandler>();

        stateTracker = new();
        audioDevices = new();

        partlyOptimizedJsonCommands = new() {
            { "StartIntercomStream_ResearcherToParticipant_Command", new UnifiedCommandFrom_Client(name: "StartOutgoingIntercomStream_Command") },
            { "StartIntercomStream_ParticipantToResearcher_Command", new UnifiedCommandFrom_Client(name: "StartIncomingIntercomStream_Command") },
            { "StopIntercomStream_ResearcherToParticipant_Command", new UnifiedCommandFrom_Client(name: "StopOutgoingIntercomStream_Command") },
            { "StopIntercomStream_ParticipantToResearcher_Command", new UnifiedCommandFrom_Client(name: "StopIncomingIntercomStream_Command") },
        };

        // todo: rename action to "{commandName}_commandHandler"
        /// 1) sends configs (audio files path)
        /// 2) sends desired data (which devices to use)
        /// 3) 
        commandsToExecuteAccordingToServerResponse = new()
        {
            { "SetConfigurations_Response",       (subState: Audio_ModuleSubStatuses.SetConfigs,        action: SendClientAudioDataDesire) },
            { "AudioDataHasBeenUpdated_Response", (subState: Audio_ModuleSubStatuses.GetAudioDevices,   action: GotServerAudioDataDecision) }
        };
    }

    public override void ManagedStart()
    {
        // Reading from config Audio Devices Data
        audioDevices.UpdateFromServerData(new UnifiedAudioDataPacket(audioDevicesInfo: _configHandler.defaultConfig.AudioConfig, null, null));
        audioDevices.GotUpdateFromServer += () => _settingsTabHandler.UpdateAudioDevices();
        audioDevices.GotUpdateFromServer += () => RecheckStatus();
        audioDevices.GotUpdateFromServer += () => _configHandler.UpdateSubConfig(audioDevices.PackMainData());
        audioDevices.GotUpdateFromClient += () => SendClientAudioDataDesire();

        /*_newDaemon = new AudioHostSideBridge(_daemonsHandler.GetDaemonCommunicator(DaemonType.Audio));
        _newDaemon.MessageReceived += message => print($"AudioHandler got message from its daemon: {message}");
        _debugTabHandler.testBtn1Clicked += (eventObj) =>
        {
            print($"AudioHandler is trying to send message to its daemon");
            _newDaemon.TestMethod1();
        };*/

        /*_daemon = await _daemonsHandler.GenerateDaemon(DaemonType.Audio);

        stateTracker.UpdateSubStatus(Audio_ModuleSubStatuses.StartAudioProcess, _daemon.isProcessOk ? SubStatusState.Complete : SubStatusState.Failed);
        stateTracker.UpdateSubStatus(Audio_ModuleSubStatuses.StartNamedPipeConnection, _daemon.isConnectionOk ? SubStatusState.Complete : SubStatusState.Failed);

        if (_daemon.isProcessOk && _daemon.isConnectionOk)
            SendConfigurationDetails();
        else
            CloseConnectionWithDaemon("AudioHandler / Start : daemon error");

        AddEventListeners();*/
    }


    #endregion MANDATORY STANDARD FUNCTIONALITY

    private void AddEventListeners ()
    {
        // Event listeners for intercom
        // todo: is there any check of status?
        _inputLogic.startOutgoingIntercomStream += () => {
            _daemon.SendCommand(partlyOptimizedJsonCommands["StartIntercomStream_ResearcherToParticipant_Command"]);
        };

        _inputLogic.stopOutgoingIntercomStream += () => {
            _daemon.SendCommand(partlyOptimizedJsonCommands["StopIntercomStream_ResearcherToParticipant_Command"]);
        };

        _inputLogic.startIncomingIntercomStream += () => {
            _daemon.SendCommand(partlyOptimizedJsonCommands["StartIntercomStream_ParticipantToResearcher_Command"]);
        };

        _inputLogic.stopIncomingIntercomStream += () => {
            _daemon.SendCommand(partlyOptimizedJsonCommands["StopIntercomStream_ParticipantToResearcher_Command"]);
        };
    }

    public void ProcessResponse(UnifiedResponseFrom_Server response)
    {
        try
        {
            var receivedCommand = commandsToExecuteAccordingToServerResponse[response.name];
            var subStateName = receivedCommand.subState;
            var funcToBeExecuted = receivedCommand.action;

            if (response.errorOccurred == true && response.errorIsFatal != true)
            {
                _experimentTabHandler.PrintToWarnings($"Minor error: {response.errorMessage}");
            }
            else if (response.errorOccurred == true && response.errorIsFatal == true)
            {
                stateTracker.UpdateSubStatus(subStateName, SubStatusState.Failed);
                _experimentTabHandler.PrintToWarnings($"Fatal error: {response.errorMessage}");
                return;
            }

            // In case everything is fine
            if (subStateName != null)
                stateTracker.UpdateSubStatus(subStateName, SubStatusState.Complete);

            funcToBeExecuted.Invoke(response);
        }
        catch
        {
            _experimentTabHandler.PrintToWarnings($"Total fail while trying read incoming message from server");
        }
    }



    #region PRIVATE METHODS

    private void CloseConnectionWithDaemon(string message)
    {
        _experimentTabHandler.PrintToWarnings(message);
        _daemon.StopDaemon();
    }

    private void SendConfigurationDetails(UnifiedResponseFrom_Server response = null)
    {
        if (!IsDaemonOk())
        {
            Debug.LogError("Custom: 'SendConfigurationDetails' is unavailable right now. 'IsDaemonOk' returned 'false'");
            return;
        }

        var commandName = "SetConfigurations_Command";
        var payloadData = new SetConfigurations_CommandDetails(unityAudioDirectory: Path.Combine(Application.streamingAssetsPath, "Audio"));
        var fullCommand = new UnifiedCommandFrom_Client(name: commandName, extraData: payloadData);

        _daemon.SendCommand(fullCommand);
    }

    private void SendClientAudioDataDesire(UnifiedResponseFrom_Server response = null)
    {
        if (!IsDaemonOk())
        {
            Debug.LogError("Custom: 'SendClientAudioDataDesire' is unavailable right now. 'IsDaemonOk' returned 'false'");
            return;
        }

        var data = audioDevices.PackMainData();
        _daemon.SendCommand(new UnifiedCommandFrom_Client(name: "SetUpdatedAudioDevicesInfo_Command", extraData: data));
    }

    /// <summary>
    /// Called after successful devices update on the server side
    /// </summary>
    private void GotServerAudioDataDecision(UnifiedResponseFrom_Server response)
    {
        var data = response.GetExtraData<UnifiedAudioDataPacket>();
        audioDevices.UpdateFromServerData(data);    // UI and Config will be updated automaticaly (via event)
    }

    private bool IsDaemonOk()
    {
        return (_daemon != null && _daemon.isConnectionOk && _daemon.isProcessOk);
    }

    private void RecheckStatus()
    {
        var atLeastOneIsOff = (
            audioDevices.OutputResearcher.Name == null ||
            audioDevices.OutputParticipant.Name == null ||
            audioDevices.InputResearcher.Name == null ||
            audioDevices.InputParticipant.Name == null
        );

        var atLeastOneOutputIsWorking = (
            (audioDevices.OutputResearcher.Name != null && audioDevices.OutputResearcher.Volume != 0) ||
            (audioDevices.OutputParticipant.Name != null && audioDevices.OutputParticipant.Volume != 0)
        );

        var AllDevicesAreChoosen_State = atLeastOneIsOff ? SubStatusState.Failed : SubStatusState.Complete;
        var AtLeastOneOutputIsWorking_State = atLeastOneOutputIsWorking ? SubStatusState.Complete : SubStatusState.Failed;

        stateTracker.UpdateSubStatus(Audio_ModuleSubStatuses.AllDevicesAreChoosen, AllDevicesAreChoosen_State);
        stateTracker.UpdateSubStatus(Audio_ModuleSubStatuses.AtLeastOneOutputIsWorking, AtLeastOneOutputIsWorking_State);
    }

    #endregion PRIVATE METHODS
}