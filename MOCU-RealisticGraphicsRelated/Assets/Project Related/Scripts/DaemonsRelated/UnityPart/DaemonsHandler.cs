using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

using DaemonsRelated;
using InterprocessCommunication;
using Debug = UnityEngine.Debug;

#nullable enable
#pragma warning disable CS8618


class DaemonsHandler : ManagedMonoBehaviour
{
    public event Action<InterprocessCommunicationMessageLog> NewMessageLogged;
    public event Action DaemonStatusChanged;    // notification that 'GetDaemonsInfo' needs to be called

    private string _daemonsFolderPath;
    private Dictionary<DaemonType, (string fullPath, bool isHidden, IDaemonUser businessLogic)> _daemonControlPaths;
    private Dictionary<DaemonType, HostSideDaemonContainer> _daemons;
    private List<DaemonHandler_Client> _daemonHandlers;
    private Dictionary<Guid, InterprocessCommunicationMessageLog> _messages;

    private DebugTabHandler _debugTabHandler;

    public override void ManagedAwake()
    {
        _debugTabHandler = GetComponent<DebugTabHandler>();

        _messages = new();
        _daemonHandlers = new();
        _daemonsFolderPath = Path.Combine(Application.streamingAssetsPath, "Daemons");

        _daemons = new()
        {
            { 
                DaemonType.Audio, new HostSideDaemonContainer(
                    type: DaemonType.Audio,
                    fullPath: Path.Combine(_daemonsFolderPath, "AudioControl.exe"),
                    isHidden: true,
                    priority: ProcessPriorityClass.BelowNormal
                )
            },

            {
                DaemonType.Moog, new HostSideDaemonContainer(
                    type: DaemonType.Moog,
                    fullPath: Path.Combine(_daemonsFolderPath, "MoogControl.exe"),
                    isHidden: true,
                    priority: ProcessPriorityClass.High
                )
            },

            {
                DaemonType.Charts, new HostSideDaemonContainer(
                    type: DaemonType.Charts,
                    fullPath: Path.Combine(_daemonsFolderPath, "ChartViewer.exe"),
                    isHidden: true,
                    priority: ProcessPriorityClass.BelowNormal
                )
            }
        };
    }

    public override void ManagedStart()
    {
        CanUseUpdateMethod = true;
    }

    private void OnApplicationQuit()
    {
        foreach (var daemon in _daemons.Values)
            try { daemon.Stop(); } catch { }
    }


    public HostSideDaemonContainer GetDaemon(DaemonType daemonType)
    {
        var daemon = _daemons[daemonType];
        var communicator = daemon.Communicator;

        communicator.MessageReceived        += message => HandleMessageLogging(daemonType, InterprocessCommunicator_EventType.MessageReceived, message);
        communicator.MessageSent            += message => HandleMessageLogging(daemonType, InterprocessCommunicator_EventType.MessageSent, message);
        communicator.ConnectionEstablished  += message => HandleMessageLogging(daemonType, InterprocessCommunicator_EventType.ConnectionEstablished, message);
        communicator.ConnectionBroked       += message => HandleMessageLogging(daemonType, InterprocessCommunicator_EventType.ConnectionBroked, message);
        communicator.ErrorOccurred          += message => HandleMessageLogging(daemonType, InterprocessCommunicator_EventType.ErrorOccurred, message);

        return daemon;
    }

    public IEnumerable<(DaemonType type, ModuleStatus status)> GetDaemonsInfo()
    {
        return _daemons.Select(item => (type: item.Key, status: item.Value.Status));
    }

    private void HandleMessageLogging(DaemonType daemonName, InterprocessCommunicator_EventType messageSourceType, string messageContent)
    {
        var messageSemanticType = DebugMessageType.Info;

        if (messageSourceType == InterprocessCommunicator_EventType.MessageReceived)
            messageSemanticType = GetMessageSemanticType(messageContent);

        if (messageSourceType == InterprocessCommunicator_EventType.ConnectionBroked)
            messageSemanticType = DebugMessageType.Error;

        if (messageSourceType == InterprocessCommunicator_EventType.ErrorOccurred)
            messageSemanticType = DebugMessageType.Error;

        // ========================================================================================

        var messageLog = new InterprocessCommunicationMessageLog()
        {
            daemonName = daemonName,
            messageSourceType = messageSourceType,
            messageContent = messageContent,
            messageSemanticType = messageSemanticType,
            messageLabel = TimeSpan.FromSeconds(Time.realtimeSinceStartup).ToString(@"hh\:mm\:ss")
        };

        _messages[Guid.NewGuid()] = messageLog;
        NewMessageLogged?.Invoke(messageLog);
    }

    private DebugMessageType GetMessageSemanticType(string message)
    {
        try
        {
            var errorReports = JsonHelper.DeserializeJson<MinimalDataTransferObject>(message).DaemonErrorReports;
            var messageType = DebugMessageType.Info;

            if (errorReports.Any())
                messageType = DebugMessageType.Warning;

            if (errorReports.Any(report => report.isFatal))
                messageType = DebugMessageType.Error;

            return messageType;
        }
        catch
        {
            return DebugMessageType.Error;
        }
    }
}

#nullable disable
#pragma warning restore CS8618