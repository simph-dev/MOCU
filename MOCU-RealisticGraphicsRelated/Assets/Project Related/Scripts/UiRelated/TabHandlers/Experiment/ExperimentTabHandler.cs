using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Concurrent;
using System;


public class ExperimentTabHandler : ManagedMonoBehaviour
{
    private UiHandler _uiHandler;
    private UiReferences _uiReference;

    private TextElement _infoField;
    private TextElement _warningsField;

    public event Action<ClickEvent> EngageBtnClicked;
    public event Action<ClickEvent> ParkBtnClicked;
    public event Action<ClickEvent> StartExperimentBtnClicked;
    public event Action<ClickEvent> StoptExperimentBtnClicked;

    private VisualElement _engageBtn;
    private VisualElement _parkBtn;
    private VisualElement _startExperimentBtn;
    private VisualElement _stoptExperimentBtn;


    public override void ManagedAwake()
    {
        _uiHandler = GetComponent<UiHandler>();
    }

    public override void ManagedStart()
    {
        _uiReference = _uiHandler.mainUiScreen;
        _infoField = _uiReference.elements.experimentTab.outputsModule.info;
        _warningsField = _uiReference.elements.experimentTab.outputsModule.warnings;

        _engageBtn = _uiReference.elements.experimentTab.controlsModule.engageMoogBtn;
        _parkBtn = _uiReference.elements.experimentTab.controlsModule.parkMoogBtn;
        _startExperimentBtn = _uiReference.elements.experimentTab.controlsModule.startExperimentBtn;
        _stoptExperimentBtn = _uiReference.elements.experimentTab.controlsModule.stopExperimentBtn;

        _engageBtn.RegisterCallback<ClickEvent>(eventObj => { EngageBtnClicked?.Invoke(eventObj); });
        _parkBtn.RegisterCallback<ClickEvent>(eventObj => { ParkBtnClicked?.Invoke(eventObj); });
        _startExperimentBtn.RegisterCallback<ClickEvent>(eventObj => { StartExperimentBtnClicked?.Invoke(eventObj); });
        _stoptExperimentBtn.RegisterCallback<ClickEvent>(eventObj => { StoptExperimentBtnClicked?.Invoke(eventObj); });

        GlobalExceptionHandler.OnErrorCaught += (message) => PrintToWarnings(message);

        CanUseUpdateMethod = true;
    }


    // todo: rethink, make more abstract (_deferredActions)
    public void PrintToInfo(string message, bool clearTextElement = false)
    {
        /*if (!CanUseUpdateMethod)
        {
            _deferredActions.Enqueue(() => PrintToInfo(message, clearTextElement));
            return;
        }*/

        if (clearTextElement) _infoField.text = "";
        _infoField.text += message;
    }

    public void PrintToWarnings(string message, bool clearTextElement = false)
    {
        /*if (!CanUseUpdateMethod)
        {
            _deferredActions.Enqueue(() => PrintToWarnings(message, clearTextElement));
            return;
        }*/

        if (clearTextElement) _warningsField.text = "";
        _warningsField.text += message;
    }




    public void ControllerButtonWasPressed(string btn_name)
    {
        _uiReference.GetElement(btn_name).AddToClassList("isActive");
    }

    public void ControllerButtonWasReleased(string btn_name)
    {
        _uiReference.GetElement(btn_name).RemoveFromClassList("isActive");
    }
}
