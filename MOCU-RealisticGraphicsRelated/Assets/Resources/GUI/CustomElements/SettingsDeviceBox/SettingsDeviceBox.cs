using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.UIElements;


namespace CustomUxmlElements
{
    public class SettingsDeviceBox : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<SettingsDeviceBox, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription _textAttribute = new UxmlStringAttributeDescription { name = "text", defaultValue = "" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);

                var settingsDeviceBox = (SettingsDeviceBox)ve;
                settingsDeviceBox.DeviceAlias = _textAttribute.GetValueFromBag(bag, cc);
            }
        }
        

        private SettingsDeviceBox _container;
        private CustomSlider _slider;
        private CustomImage _image;
        private TextElement _label;

        private Dictionary<DevicePanelStatus, string> _deviceCardStatusToUssClassNameMap = new ()
        {
            { DevicePanelStatus.Ready,       "normal"},
            { DevicePanelStatus.Disabled,    "is-inactive"},
            { DevicePanelStatus.Connecting,  "has-warning"},
            { DevicePanelStatus.NotChosen,   "has-error"},
            { DevicePanelStatus.Error,       "has-error"},
        };

        public SettingsDeviceBox()
        {
            _container = this;
            _slider = new();
            _image = new();
            _label = new();

            _container.Add(_slider);
            _container.Add(_image);
            _container.Add(_label);
        }

        public string DeviceAlias
        {
            get => _label.text;
            set => _label.text = value;
        }

        public float SliderValue
        {
            get => _slider.Value;
            set => _slider.Value = value;
        }

        public string UiName
        {
            get => _container.name;
        }

        public void SetStatus(DevicePanelStatus status)
        {
            foreach (var style in _deviceCardStatusToUssClassNameMap.Values)
                _label.RemoveFromClassList(style);

            _label.AddToClassList(_deviceCardStatusToUssClassNameMap[status]);
        }
        
        public void HardHide()
        {
            _container.style.display = DisplayStyle.None;
        }

        public void HardShow()
        {
            _container.style.display = DisplayStyle.Flex;
        }

        public void SoftHideSlider()
        {
            _slider.SoftHide();
        }

        public void SoftUnhideSlider()
        {
            if (!_slider.ClassListContains("hidden-slider"))
                _slider.SoftShow();
        }
    }
}