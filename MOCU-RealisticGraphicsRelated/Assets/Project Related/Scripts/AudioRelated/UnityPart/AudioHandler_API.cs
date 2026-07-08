using System;
using System.Collections.Generic;


namespace AudioModule
{
    public interface AudioHandler_API
    {
        IEnumerable<(AudioDeviceData deviceData, User deviceUser)> GetInputDevices();
        IEnumerable<(AudioDeviceData deviceData, User deviceUser)> GetOutputDevices();
        IEnumerable<AudioIntercomData> GetIntercoms();
        IEnumerable<AudioClipData> GetClips();

        void PlayClip(User user, AudioClipName clip);
        void PingDevice(Guid deviceId);
        void ChangeDeviceUser(Guid deviceId, User user);
        void ChangeDeviceVolume(Guid deviceId, float volume);
        void ChangeClipParameters(AudioClipData clipData);
        void StartIntercom(User fromUser, User toUser);
        void StopIntercom(User fromUser, User toUser);

        event Action DeviceHasChanged;
        event Action ClipHasChanged;
        event Action IntercomHasStarted;
        event Action IntercomHasStoped;
    }
}