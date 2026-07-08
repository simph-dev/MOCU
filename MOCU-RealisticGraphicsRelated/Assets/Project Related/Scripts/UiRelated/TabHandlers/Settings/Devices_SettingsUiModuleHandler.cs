// TODO: refactor everything //
// ========================= //

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

using CustomDataStructures;
using CustomUxmlElements;
using AudioControl;
using System.Linq;

using Debug = UnityEngine.Debug;
using System.Xml.Linq;
using System.Collections;


// add to documentation: the only update this class can get are from outside. it only sends what is wanted to be changed


public class Devices_SettingsUiModuleHandler : ManagedMonoBehaviour
{
    public InterlinkedCollection<DeviceParametersSet> devicesInterlinkedCollection;         // connects data objects with UI stuff
    private VisualTreeAsset _chooseDeviceRowTemplate;

    private AudioHandler _audioHandler;
    private UiReferences _uiReference;
    private UiHandler _uiHandler;

    private Dictionary<DevicePanelStatus, string> _deviceCardStatusToUssClassNameMap;        // classes for card labels
    private Dictionary<DeviceOptionStatus, string> _deviceOptionStatusToUssClassNameMap;    // classes for options

    private VisualElement _backToMainBtn;
    private VisualElement _openChoosingWindowBtn;
    private VisualElement _currentlyOpenedDeviceBox;

    private DeviceBoxPanel_Microphones temp;



    public override void ManagedAwake()
    {
        _uiHandler = GetComponent<UiHandler>();
        _audioHandler = GetComponent<AudioHandler>();

        _chooseDeviceRowTemplate = Resources.Load<VisualTreeAsset>("GUI/UXML/SettingsModules/DeviceOptionModule");

        _deviceOptionStatusToUssClassNameMap = new()
        {
            {DeviceOptionStatus.FreeToChoose,       "ready-to-use-device"},
            {DeviceOptionStatus.CurrentlyChosen,    "current-device"},
            {DeviceOptionStatus.AlreadyChosen,      "already-chosen-device"},
        };

        temp = new DeviceBoxPanel_Microphones();
    }

    public override void ManagedStart()
    {
        _uiReference = _uiHandler.secondaryUiScreen;

        var devicesModule = _uiReference.root.Q<VisualElement>("settings-devices-module-window");
        //var box = new DeviceBoxPanel_Microphones();
        devicesModule.Add(temp);


        /*_backToMainBtn = _uiReference.elements.settingsTab.devicesModule.backToMainBtn;
        _openChoosingWindowBtn = _uiReference.elements.settingsTab.devicesModule.choosingWindow;
        _currentlyOpenedDeviceBox = null;

        DeviceParametersSet audioDevice_InputResearcher = null;
        DeviceParametersSet audioDevice_InputParticipant = null;
        DeviceParametersSet audioDevice_OutputResearcher = null;
        DeviceParametersSet audioDevice_OutputParticipant = null;


        audioDevice_InputResearcher = new DeviceParametersSet {
            uiElement               = _uiReference.elements.settingsTab.devicesModule.microphoneResearcher,
            deviceObject            = _audioHandler.audioDevices.InputResearcher,
            deviceStatus            = DevicePanelStatus.NotChosen,
            deviceType              = DeviceType.AudioInput,
            allowMultipleDevices    = false,

            GetDevicesList          = () => AssignStatusesToListOfOptions(_audioHandler.audioDevices.Inputs, audioDevice_InputResearcher)
        };

        audioDevice_InputParticipant = new DeviceParametersSet {
            uiElement               = _uiReference.elements.settingsTab.devicesModule.microphoneParticipant,
            deviceObject            = _audioHandler.audioDevices.InputParticipant,
            deviceStatus            = DevicePanelStatus.NotChosen,
            deviceType              = DeviceType.AudioInput,
            allowMultipleDevices    = false,

            GetDevicesList          = () => AssignStatusesToListOfOptions(_audioHandler.audioDevices.Inputs, audioDevice_InputParticipant)
        };

        audioDevice_OutputResearcher = new DeviceParametersSet {
            uiElement               = _uiReference.elements.settingsTab.devicesModule.speakerResearcher,
            deviceObject            = _audioHandler.audioDevices.OutputResearcher,
            deviceStatus            = DevicePanelStatus.NotChosen,
            deviceType              = DeviceType.AudioOutput,
            allowMultipleDevices    = false,

            GetDevicesList          = () => AssignStatusesToListOfOptions(_audioHandler.audioDevices.Outputs, audioDevice_OutputResearcher)
        };

        audioDevice_OutputParticipant = new DeviceParametersSet
        {
            uiElement               = _uiReference.elements.settingsTab.devicesModule.speakerParticipant,
            deviceObject            = _audioHandler.audioDevices.OutputParticipant,
            deviceStatus            = DevicePanelStatus.NotChosen,
            deviceType              = DeviceType.AudioOutput,
            allowMultipleDevices    = false,

            GetDevicesList          = () => AssignStatusesToListOfOptions(_audioHandler.audioDevices.Outputs, audioDevice_OutputParticipant)
        };


        devicesInterlinkedCollection = new()
        {
            audioDevice_InputResearcher,
            audioDevice_InputParticipant,
            audioDevice_OutputResearcher,
            audioDevice_OutputParticipant,
        };

        SomeStuff_RenameLater();*/
        CanUseUpdateMethod = true;
    }


    private void SomeStuff_RenameLater()
    {
        foreach (var device in devicesInterlinkedCollection)
        {
            var deviceCardUiElement = device.uiElement;

            deviceCardUiElement.pickingMode = PickingMode.Position;
            deviceCardUiElement.RegisterCallback<ClickEvent>(eventObj => { DeviceBoxClicked(eventObj); });
            // 'TrickleDown' is for propagation from parent to child, to prevent the slider from triggering 

            var customSlider = deviceCardUiElement.Q<CustomSlider>();
            if (customSlider != null)
                customSlider.RegisterCallback<ClickEvent>(eventObj => DeviceVolumeSliderClicked(eventObj));
        }

        _backToMainBtn.RegisterCallback<ClickEvent>(CloseDeviceBoxParameters);
    }

    public void ApplyAudioChanges()
    {
        UpdateAudioDevicesData();
        UpdateAudioDevicesUi();
    }

    private void UpdateAudioDevicesData()
    {
        try
        {
            foreach (var device in devicesInterlinkedCollection)
            {
                /*var chosenDeviceName    = device.GetDeviceName(parameters);
                var chosenDeviceVolume  = device.GetDeviceVolume(parameters) ?? 0f;
                var deviceStatus        = (!String.IsNullOrEmpty(chosenDeviceName)) ? DevicePanelStatus.Ready : DevicePanelStatus.NotChosen;

                devicesInterlinkedCollection.UpdateSingleValue(device.uiElement, "chosenDeviceName", chosenDeviceName);
                devicesInterlinkedCollection.UpdateSingleValue(device.uiElement, "chosenDeviceVolume", chosenDeviceVolume);*/

                var deviceStatus = (!String.IsNullOrEmpty(device.deviceObject.Name)) ? DevicePanelStatus.Ready : DevicePanelStatus.NotChosen;
                devicesInterlinkedCollection.UpdateSingleValue(device.uiElement, "deviceStatus", deviceStatus);
            }

            // must be after after previuos 'foreach' cause it depends on it 
            foreach (var device in devicesInterlinkedCollection)
            {
                var listOfOptions = device.GetDevicesList();
                devicesInterlinkedCollection.UpdateSingleValue(device.uiElement, "listOfOptions", listOfOptions);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in UpdateAudioDevicesData: {ex}");
        }
    }
    
    private void UpdateAudioDevicesUi()
    {
        try
        {
            CloseDeviceBoxParameters();

            foreach (var device in devicesInterlinkedCollection)
            {
                var volume = device.deviceObject.Volume;
                var status = device.deviceStatus;

                device.uiElement.SetStatus(status);
                device.uiElement.SliderValue = volume ?? 0.0f;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in UpdateAudioDevicesUi: {ex}");
        }
    }

    private IReadOnlyList<(string deviceName, DeviceOptionStatus status)> AssignStatusesToListOfOptions (IReadOnlyList<string> list, DeviceParametersSet currentDevice)
    {
        var result = new List<(string deviceName, DeviceOptionStatus status)>();

        foreach (var option in list)
        {
            var currentOption = (deviceName: option, status: DeviceOptionStatus.FreeToChoose);

            if (option == currentDevice.deviceObject.Name)
            {
                currentOption.status = DeviceOptionStatus.CurrentlyChosen;
                result.Add(currentOption);
                continue;
            }

            foreach (var device in devicesInterlinkedCollection)
            {
                if (device == currentDevice)
                    continue;

                if (option == device.deviceObject.Name)
                {
                    currentOption.status = DeviceOptionStatus.AlreadyChosen;
                    break;
                }
            }

            result.Add(currentOption);
        }

        return result;
    }


    private void WeelSoundChange(WheelEvent evt)
    {
        var parentName = ((VisualElement)evt.currentTarget).name;
        var slider = _uiReference.GetElement(parentName).Q<CustomSlider>();
        slider.value += evt.delta.y > 0 ? -2 : +2;
    }

    private void DeviceBoxClicked(ClickEvent eventObj)
    {
        if (_openChoosingWindowBtn.style.display == DisplayStyle.Flex) return;

        var currentElement = eventObj.currentTarget as SettingsDeviceBox;
        OpenDeviceBoxParameters(currentElement);
    }

    private void DeviceVolumeSliderClicked(ClickEvent eventObj)
    {
        // If cliked on slider than there is no need to call device box event
        eventObj.StopImmediatePropagation();

        var slider = eventObj.currentTarget as CustomSlider;
        var device = devicesInterlinkedCollection[slider.parent];
        var deviceObject = (AudioDevice)device.deviceObject;

        deviceObject.SetVolumeByClient(slider.value);
    }

    private void OpenDeviceBoxParameters(SettingsDeviceBox clickedDeviceBox)
    {
        _currentlyOpenedDeviceBox = clickedDeviceBox;

        foreach (var deviceBox in _uiReference.GetDevicesBoxes())   // hide every device box except clicked one
        {
            if (deviceBox == clickedDeviceBox) continue;
            deviceBox.HardHide();
        }

        _openChoosingWindowBtn.style.display = DisplayStyle.Flex;   // show device parameters window
        _backToMainBtn.style.visibility = Visibility.Visible;       // show close btn
        clickedDeviceBox.SoftUnhideSlider();

        FillDeviceOptions(devicesInterlinkedCollection[clickedDeviceBox]);
    }

    private void FillDeviceOptions(DeviceParametersSet deviceParametersSet)
    {
        var devicesList = ((VisualElement)_uiReference.elements.settingsTab.devicesModule.choosingWindow).Q<ScrollView>();
        devicesList.Clear();

        foreach (var option in deviceParametersSet.listOfOptions)
        {
            var instance = _chooseDeviceRowTemplate.CloneTree();
            instance.Q<TextElement>(className: "device-option-full-name").text = option.deviceName;

            // Bell btn visibility (for output audio devices only)
            if (deviceParametersSet.deviceType == DeviceType.AudioOutput)
            {
                var bellIcon = instance.Q<VisualElement>(className: "device-option-bell-btn");
                bellIcon.RegisterCallback<ClickEvent>(SendTestSoundToAudioOutputDevice);
                bellIcon.style.visibility = Visibility.Visible;
            }

            instance.AddToClassList(_deviceOptionStatusToUssClassNameMap[option.status]);

            instance.Q<VisualElement>(className: "device-option-full-name").RegisterCallback<ClickEvent>(DeviceOptionOnClick);
            
            devicesList.Add(instance);
        }
    }

    private void CloseDeviceBoxParameters(ClickEvent clickEvent = null)
    {
        _currentlyOpenedDeviceBox = null;

        foreach (var deviceBox in _uiReference.GetDevicesBoxes())
        {
            deviceBox.HardShow();
            deviceBox.SoftHideSlider();
        }

        // hide device parameters window
        _openChoosingWindowBtn.style.display = DisplayStyle.None;
        // hide close btn
        _backToMainBtn.style.visibility = Visibility.Hidden;
    }

    

    private void SendTestSoundToAudioOutputDevice(ClickEvent clickEvent)
    {
        var outputDeviceName = ((VisualElement)clickEvent.currentTarget).parent.parent.Q<TextElement>(className: "device-option-full-name").text;
        _audioHandler.SendTestAudioSignalToDevice(audioOutputDeviceName: outputDeviceName);
    }



    // todo: tied to 'AudioDevice'
    private void DeviceOptionOnClick(ClickEvent clickEvent)
    {
        if (_currentlyOpenedDeviceBox == null)
        {
            print("error in 'DeviceOptionOnClick'. '_currentlyOpenedDeviceBox' can't be 'null'");
            return;
        }

        var optionName = ((TextElement)clickEvent.currentTarget).text;
        var device = devicesInterlinkedCollection[_currentlyOpenedDeviceBox];
        var optionStatus = device.listOfOptions.FirstOrDefault(option => option.deviceName == optionName).status;

        if (optionStatus == DeviceOptionStatus.CurrentlyChosen)
            ((AudioDevice)device.deviceObject).SetNameByClient(null);
        else if (optionStatus == DeviceOptionStatus.AlreadyChosen)
        {
            foreach (var thatDevice in devicesInterlinkedCollection)
            {
                if (thatDevice.deviceObject.Name == optionName)
                {
                    ((AudioDevice)thatDevice.deviceObject).SetNameByClient(null);
                    ((AudioDevice)device.deviceObject).SetNameByClient(optionName);
                    break;
                }
            }
        }
        else if (optionStatus == DeviceOptionStatus.FreeToChoose)
            ((AudioDevice)device.deviceObject).SetNameByClient(optionName);
        else
            print("you should not see that message");

        CloseDeviceBoxParameters();
    }
}




public class DeviceParametersSet
{
    [CanBeKey(true)]
    public SettingsDeviceBox uiElement { get; set; }

    [CanBeKey(true)]
    public ISettingsDevice deviceObject { get; set; }

    
    [CanBeKey(false)]
    public Func<IReadOnlyList<(string deviceName, DeviceOptionStatus status)>?> GetDevicesList { get; set; }

    [CanBeKey(false)]
    public IReadOnlyList<(string deviceName, DeviceOptionStatus status)>? listOfOptions { get; set; }

    [CanBeKey(false)]
    public DevicePanelStatus deviceStatus { get; set; }
    
    [CanBeKey(false)]
    public DeviceType deviceType { get; set; }

    [CanBeKey(false)]
    public bool allowMultipleDevices { get; set; }
}

public enum DevicePanelStatus
{
    Ready,              // is ok and can be used
    Disabled,           // can't be chosen
    Connecting,         // in the process of connecting
    NotChosen,          // when the value is null
    Error,              // failed to connect or disconnected during runtime (for any reason)
    Warning             // If sound is too high or smth like that
}

public enum DeviceOptionStatus
{
    FreeToChoose,       // Device is available to be chosen
    CurrentlyChosen,    // Device is currently chosen by this object
    AlreadyChosen       // Device is already chosen by another object
}

public enum DeviceType
{
    AudioOutput,
    AudioInput,
    Camera,
    Controller
}

// todo: move from here, it's temporary
public interface ISettingsDevice
{
    public string? Name { get; }

    public float? Volume { get; }
}