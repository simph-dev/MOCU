#nullable enable

using System;
using System.Collections.Generic;


namespace AudioControl {
    public enum ClientCommands
    {
        SetConfigurations,
        SetNewAudioData,
        PlayAudioFile,

        StartOutgoingIntercomStream,
        StartIncomingIntercomStream,
        StopOutgoingIntercomStream,
        StopIncomingIntercomStream
    }

    public enum ServerResponses
    {
        ConfigurationsHaveBeenSet,
        AudioDataHasBeenUpdated
    }


    public class AudioDevicesInfo
    {
        public string?  audioOutputDeviceName_Researcher;
        public string?  audioOutputDeviceName_Participant;
        public string?  audioInputDeviceName_Researcher;
        public string?  audioInputDeviceName_Participant;

        public float?   audioOutputDeviceVolume_Researcher;
        public float?   audioOutputDeviceVolume_Participant;
        public float?   audioInputDeviceVolume_Researcher;
        public float?   audioInputDeviceVolume_Participant;
    }

    public class UnifiedAudioDataPacket
    {
        public readonly AudioDevicesInfo? audioDevicesInfo;
        public IReadOnlyList<string> inputAudioDevices;
        public IReadOnlyList<string> outputAudioDevices;

        public UnifiedAudioDataPacket(AudioDevicesInfo? audioDevicesInfo, IReadOnlyList<string> inputAudioDevices, IReadOnlyList<string> outputAudioDevices) {
            this.audioDevicesInfo = audioDevicesInfo;
            this.inputAudioDevices = inputAudioDevices ?? Array.Empty<string>();
            this.outputAudioDevices = outputAudioDevices ?? Array.Empty<string>();
        }
    }

    public class PlayAudioFile_CommandDetails
    {
        public string audioFileName;
        public string audioOutputDeviceName;

        public PlayAudioFile_CommandDetails(string audioFileName, string audioOutputDeviceName)
        {
            this.audioFileName = audioFileName;
            this.audioOutputDeviceName = audioOutputDeviceName;
        }
    }

    public class SetConfigurations_CommandDetails
    {
        public string unityAudioDirectory;
        // todo: will be more

        public SetConfigurations_CommandDetails(string unityAudioDirectory)
        {
            this.unityAudioDirectory = unityAudioDirectory;
        }
    }

}

#nullable disable