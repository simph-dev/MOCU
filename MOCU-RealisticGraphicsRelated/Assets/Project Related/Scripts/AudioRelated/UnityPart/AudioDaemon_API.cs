/*
    Is a mirror of 'AudioHost_API'
 */


using System;
using System.Collections;
using System.Collections.Generic;
using DaemonsRelated;


namespace AudioModule
{
    public interface AudioDaemon_API
    {
        void PlayClips(IEnumerable<PlayAudioClipCommand> playCommandsData);
        void UpdateDevicesData(IEnumerable<AudioDeviceData> devicesData);
        void UpdateClipsData(IEnumerable<AudioClipData> clipsData);
        void UpdateIntercomStates(IEnumerable<AudioIntercomData> intercomsData);

        event Action<IEnumerable<AudioDeviceData>> DevicesDataChanged;
        event Action<IEnumerable<AudioClipData>> ClipsDataChanged;
        event Action<IEnumerable<AudioIntercomData>> IntercomStatesChanged;
        event Action<IEnumerable<DaemonErrorReport>> ErrorsOccurred;
    }
}