using System;
using System.Collections.Generic;
using System.Linq;
using DaemonsRelated;



namespace AudioModule
{
    public class AudioDataTransferObject : IDataTransferObject
    {
        public IEnumerable<AudioClipData> ClipChanges               { get; set; } = Enumerable.Empty<AudioClipData>();
        public IEnumerable<AudioDeviceData> DeviceChanges           { get; set; } = Enumerable.Empty<AudioDeviceData>();
        public IEnumerable<AudioIntercomData> IntercomCommands      { get; set; } = Enumerable.Empty<AudioIntercomData>();
        public IEnumerable<PlayAudioClipCommand> PlayClipCommands   { get; set; } = Enumerable.Empty<PlayAudioClipCommand>();

        // Common for every 'IDataTransferObject'
        public IEnumerable<DaemonErrorReport> DaemonErrorReports { get; set; } = Enumerable.Empty<DaemonErrorReport>();
        public bool DoTerminateTheDaemon { get; set; } = false;
        public string CustomMessage { get; set; } = String.Empty;
    }

    public class PlayAudioClipCommand
    {
        public AudioClipData ClipData { get; set; }
        public Guid OutputDeviceId { get; set; }
        public bool InterruptPlayingClips { get; set; } = false;
    }
}