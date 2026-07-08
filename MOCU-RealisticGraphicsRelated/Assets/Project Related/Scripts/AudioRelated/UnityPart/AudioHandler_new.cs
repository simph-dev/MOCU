using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

using AudioModule;
using InterprocessCommunication;
using DaemonsRelated;
using MoogModule;


public class AudioHandler_new : ManagedMonoBehaviour
{
    public event Action DeviceHasChanged;
    public event Action ClipHasChanged;
    public event Action IntercomHasStarted;
    public event Action IntercomHasStoped;

    private AudioHostSideBridge _daemon;
    private Dictionary<Guid, (AudioDeviceData, User)> _devices;
    private Dictionary<AudioClipName, AudioClipData> _clips;
    private Dictionary<(Guid fromDevice, Guid toDevice), AudioIntercomData> _intercoms;

    // unity components
    private DaemonsHandler _daemonsHandler;
    private DebugTabHandler _debugTabHandler;

    // ############################################################################################

    public override void ManagedAwake()
    {
        _daemonsHandler = GetComponent<DaemonsHandler>();
        _debugTabHandler = GetComponent<DebugTabHandler>();
    }
    public override void ManagedStart()
    {
        var daemonWrapper = _daemonsHandler.GetDaemon(DaemonType.Audio);
        var communicator = daemonWrapper.Communicator;

        _daemon = new AudioHostSideBridge(communicator);

        InitDaemonTest();   // will be sent only after 'ConnectionEstablished' (automatically, it's in the queue)

        /*_debugTabHandler.testBtn1Clicked += (eventObj) => _daemon.TestMethod1();    // test
        _debugTabHandler.testBtn2Clicked += (eventObj) => _daemon.TestMethod2();    // test*/

        //daemonWrapper.Start();
    }

    // ############################################################################################

    public void ChangeClipParameters(AudioClipData clipData)
    {
        throw new NotImplementedException();
    }

    public void ChangeDeviceUser(Guid deviceId, User user)
    {
        throw new NotImplementedException();
    }

    public void ChangeDeviceVolume(Guid deviceId, float volume)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<AudioClipData> GetClips()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<(AudioDeviceData deviceData, User deviceUser)> GetInputDevices()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<AudioIntercomData> GetIntercoms()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<(AudioDeviceData deviceData, User deviceUser)> GetOutputDevices()
    {
        throw new NotImplementedException();
    }

    public void PingDevice(Guid deviceId)
    {
        throw new NotImplementedException();
    }

    public void PlayClip(User user, AudioClipName clip)
    {
        throw new NotImplementedException();
    }

    public void StartIntercom(User fromUser, User toUser)
    {
        throw new NotImplementedException();
    }

    public void StopIntercom(User fromUser, User toUser)
    {
        throw new NotImplementedException();
    }

    // ############################################################################################

    private void InitDaemonTest()
    {
        try
        {
            _daemon.UpdateClipsData(
                new List<AudioClipData>() {
                    new AudioClipData() {
                        name = AudioClipName.PingDevice,
                        volume = 100,
                        fullFilePath = @"C:\Users\Levael\GitHub\MOCU\MOCU-UnityCore\Assets\StreamingAssets\Audio\test.mp3"
                    },

                    new AudioClipData() {
                        name = AudioClipName.CorrectAnswer,
                        volume = 85,
                        fullFilePath = @"C:\Users\Levael\GitHub\MOCU\MOCU-UnityCore\Assets\StreamingAssets\Audio\audioTestSample.mp3"
                    }
                }
            );
        }
        catch
        {
            Debug.LogWarning("Failed 'InitDaemonTest'.");
        }
    }
}