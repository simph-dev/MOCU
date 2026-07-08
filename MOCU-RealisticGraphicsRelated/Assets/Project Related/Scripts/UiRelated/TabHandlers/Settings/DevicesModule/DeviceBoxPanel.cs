using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine.UI;
using UnityEngine.UIElements;


namespace CustomUxmlElements
{
    public class DeviceBoxPanel : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<DeviceBoxPanel, UxmlTraits> { }  // to be able use it from UXML

        private readonly DeviceBoxPanel _container;
        private readonly VisualElement _infoSection;
        private readonly VisualElement _parametersSection;
        private readonly CustomImage _icon;
        private readonly TextElement _type;
        private readonly CustomImage _closeParametersBtn;

        private bool _isExpanded;

        private readonly string _container_ClassName            = "device-box-panel-container";
        private readonly string _infoSection_ClassName          = "device-box-panel-info-section";
        private readonly string _parametersSection_ClassName    = "device-box-panel-parameters-section";
        private readonly string _icon_ClassName                 = "device-box-panel-info-section-icon";
        private readonly string _type_ClassName                 = "device-box-panel-info-section-type";
        private readonly string _closeParametersBtn_ClassName   = "device-box-panel-close-parameters-btn";
        private readonly string _panelMode_ClassName            = "panel-mode";
        private readonly string _paramsMode_ClassName           = "params-mode";

        private readonly Dictionary<DevicePanelStatus, string> _devicePanelStatusToUssClassNameMap = new ()
        {
            { DevicePanelStatus.Ready,       "normal"},
            { DevicePanelStatus.Disabled,    "is-inactive"},
            { DevicePanelStatus.Connecting,  "has-warning"},
            { DevicePanelStatus.NotChosen,   "has-error"},
            { DevicePanelStatus.Error,       "has-error"},
        };

        public DeviceBoxPanel()
        {
            _container = this;
            _infoSection = new();
            _parametersSection = new();
            _icon = new();
            _type = new();
            _closeParametersBtn = new();

            _container.Add(_infoSection);
            _container.Add(_parametersSection);
            _container.Add(_closeParametersBtn);

            _infoSection.Add(_icon);
            _infoSection.Add(_type);

            _isExpanded = false;

            ApplyCommonStyles();
            ApplyPanelModeStyles();

            AddEventListeners();
        }

        public string UiName
        {
            get => _container.name;
        }

        public string Label
        {
            get => _type.text;
            set => _type.text = value;
        }

        public void SetStatus(DevicePanelStatus status)
        {
            foreach (var style in _devicePanelStatusToUssClassNameMap.Values)
                _infoSection.RemoveFromClassList(style);

            _infoSection.AddToClassList(_devicePanelStatusToUssClassNameMap[status]);
        }

        private void AddEventListeners()
        {
            _container.RegisterCallback<ClickEvent>(e => ContainerOnClick());
            _closeParametersBtn.RegisterCallback<ClickEvent>(e => CloseBtnOnClick());
        }

        private void ContainerOnClick()
        {
            if (!_isExpanded)
                ApplyParamsModeStyles();
        }

        private void CloseBtnOnClick()
        {
            if (_isExpanded)
                ApplyPanelModeStyles();
        }

        private void ApplyCommonStyles()
        {
            _container.AddToClassList(_container_ClassName);
            _infoSection.AddToClassList(_infoSection_ClassName);
            _parametersSection.AddToClassList(_parametersSection_ClassName);
            _icon.AddToClassList(_icon_ClassName);
            _type.AddToClassList(_type_ClassName);
            _closeParametersBtn.AddToClassList(_closeParametersBtn_ClassName);
        }

        private void ApplyPanelModeStyles()
        {
            _isExpanded = false;

            _container.RemoveFromClassList(_paramsMode_ClassName);
            _container.AddToClassList(_panelMode_ClassName);
        }

        private void ApplyParamsModeStyles()
        {
            _isExpanded = true;

            _container.RemoveFromClassList(_panelMode_ClassName);
            _container.AddToClassList(_paramsMode_ClassName);
        }
    }
}