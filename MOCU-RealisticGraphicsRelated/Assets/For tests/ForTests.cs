using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem;


public class ForTests : ManagedMonoBehaviour
{
    private DebugTabHandler _debugTabHandler;
    private AudioHandler _audioHandler;
    //private CedrusHandler _cerusHandler;

    //private int temp_counter = 0;

    public override void ManagedAwake()
    {
        _debugTabHandler = GetComponent<DebugTabHandler>();
        _audioHandler = GetComponent<AudioHandler>();
        //_cerusHandler = GetComponent<CedrusHandler>();
    }

    public override void ManagedStart()
    {


        /*_debugTabHandler.testBtn1Clicked += (eventObj) =>
        {
            if (_cerusHandler.stateTracker.Status != ModuleStatus.FullyOperational)
                _cerusHandler.TryConnect();
        };*/

        // sending lots of commands simultaneously together (stresstest)
        /*_debugTabHandler.testBtn1Clicked += (eventObj) =>
        {
            print($"sended");
            _audioHandler.
            //_audioHandler.SendTestAudioSignalToDevice("Speakers (Realtek High Definition Audio)");
        };*/
        CanUseUpdateMethod = true;
    }

    public override void ManagedUpdate()
    {
        /*if (_audioHandler.stateTracker.Status == ModuleStatus.FullyOperational && temp_counter++ < 300)
            _audioHandler.SendTestAudioSignalToDevice("Speakers (Realtek High Definition Audio)");*/
    }
}
