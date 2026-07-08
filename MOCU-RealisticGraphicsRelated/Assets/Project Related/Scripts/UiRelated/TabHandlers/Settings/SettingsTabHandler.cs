using UnityEngine;

using AudioControl;


public class SettingsTabHandler : ManagedMonoBehaviour
{
    // FIELDS
    private UiHandler _uiHandler;
    private AudioHandler _audioHandler;
    private UiReferences _uiReference;
    private Devices_SettingsUiModuleHandler _devicesUiModuleHandler;
    

    // MANDATORY STANDARD FUNCTIONALITY

    public override void ManagedAwake()
    {
        _uiHandler = GetComponent<UiHandler>();
        _audioHandler = GetComponent<AudioHandler>();
        _devicesUiModuleHandler = GetComponent<Devices_SettingsUiModuleHandler>();
    }

    public override void ManagedStart()
    {
        _uiReference = _uiHandler.secondaryUiScreen;
        AddEventListeners();
        CanUseUpdateMethod = true;
    }


    // CUSTOM FUNCTIONALITY

    private void AddEventListeners() { }

    public void UpdateAudioDevices()
    {
        _devicesUiModuleHandler.ApplyAudioChanges();
    }
}