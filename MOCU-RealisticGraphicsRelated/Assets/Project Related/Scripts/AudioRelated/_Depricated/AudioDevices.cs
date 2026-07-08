/*
    Data from the server comes in a batch and is updated all together (with one event call at the end).
    Called from 'AudioHandler' class

    The changes from the client come one at a time and everyone sends their request to the server to change the data (piece by piece).
    Called from 'Devices_SettingsUiModuleHandler' class
 */

using System;
using System.Collections.Generic;
using AudioControl;


public class AudioDevices
{
    public readonly AudioDevice OutputResearcher;
    public readonly AudioDevice OutputParticipant;
    public readonly AudioDevice InputResearcher;
    public readonly AudioDevice InputParticipant;

    public IReadOnlyList<string> Inputs;
    public IReadOnlyList<string> Outputs;

    public event Action GotUpdateFromClient;
    public event Action GotUpdateFromServer;

    public AudioDevices (UnifiedAudioDataPacket unifiedAudioDataPacket = null)
    {
        OutputResearcher    = new();
        OutputParticipant   = new();
        InputResearcher     = new();
        InputParticipant    = new();

        Inputs = Array.Empty<string>();
        Outputs = Array.Empty<string>();

        OutputResearcher.DeviceGotChangedByClient += HandleUpdateFromClient;
        OutputParticipant.DeviceGotChangedByClient += HandleUpdateFromClient;
        InputResearcher.DeviceGotChangedByClient += HandleUpdateFromClient;
        InputParticipant.DeviceGotChangedByClient += HandleUpdateFromClient;

        if (unifiedAudioDataPacket != null)
            UpdateFromServerData(unifiedAudioDataPacket);
    }

    public AudioDevicesInfo PackMainData()
    {
        return new AudioDevicesInfo()
        {
            audioOutputDeviceName_Researcher = OutputResearcher.Name,
            audioOutputDeviceName_Participant = OutputParticipant.Name,
            audioInputDeviceName_Researcher = InputResearcher.Name,
            audioInputDeviceName_Participant = InputParticipant.Name,

            audioOutputDeviceVolume_Researcher = OutputResearcher.Volume,
            audioOutputDeviceVolume_Participant = OutputParticipant.Volume,
            audioInputDeviceVolume_Researcher = InputResearcher.Volume,
            audioInputDeviceVolume_Participant = InputParticipant.Volume,
        };
    }

    public UnifiedAudioDataPacket PackAllData()
    {
        return new(audioDevicesInfo: PackMainData(), inputAudioDevices: Inputs, outputAudioDevices: Outputs);
    }

    public void UpdateFromServerData(UnifiedAudioDataPacket data)
    {
        OutputResearcher.SetNameByServer(data.audioDevicesInfo.audioOutputDeviceName_Researcher);
        OutputResearcher.SetVolumeByServer(data.audioDevicesInfo.audioOutputDeviceVolume_Researcher);
        OutputParticipant.SetNameByServer(data.audioDevicesInfo.audioOutputDeviceName_Participant);
        OutputParticipant.SetVolumeByServer(data.audioDevicesInfo.audioOutputDeviceVolume_Participant);
        InputResearcher.SetNameByServer(data.audioDevicesInfo.audioInputDeviceName_Researcher);
        InputResearcher.SetVolumeByServer(data.audioDevicesInfo.audioInputDeviceVolume_Researcher);
        InputParticipant.SetNameByServer(data.audioDevicesInfo.audioInputDeviceName_Participant);
        InputParticipant.SetVolumeByServer(data.audioDevicesInfo.audioInputDeviceVolume_Participant);

        Inputs = data.inputAudioDevices;
        Outputs = data.outputAudioDevices;

        HandleUpdateFromServer();
    }

    private void HandleUpdateFromClient()
    {
        GotUpdateFromClient?.Invoke();
    }

    private void HandleUpdateFromServer()
    {
        GotUpdateFromServer?.Invoke();
    }
}