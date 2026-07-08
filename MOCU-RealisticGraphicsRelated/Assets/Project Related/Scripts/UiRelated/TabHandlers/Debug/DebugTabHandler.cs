using DaemonsRelated;
using System;
using UnityEngine;
using UnityEngine.UIElements;


public class DebugTabHandler : ManagedMonoBehaviour
{
    public event Action<ClickEvent> testBtn1Clicked;
    public event Action<ClickEvent> testBtn2Clicked;

    private UiHandler _uiHandler;
    private UiReferences _uiReference;
    private Daemons_DebugUiModuleHandler _daemonsUiModuleHandler;
    private Console_DebugUiModuleHandler _consoleUiModuleHandler;

    private VisualElement _testBtn1;
    private VisualElement _testBtn2;


    public override void ManagedAwake()
    {
        _uiHandler = GetComponent<UiHandler>();
        _daemonsUiModuleHandler = GetComponent<Daemons_DebugUiModuleHandler>();
        _consoleUiModuleHandler = GetComponent<Console_DebugUiModuleHandler>();
    }

    public override void ManagedStart()
    {
        _uiReference = _uiHandler.secondaryUiScreen;

        _testBtn1 = _uiReference.elements.debugTab.testBtn1;
        _testBtn2 = _uiReference.elements.debugTab.testBtn2;

        _testBtn1.RegisterCallback<ClickEvent>(eventObj => { testBtn1Clicked?.Invoke(eventObj); });
        _testBtn2.RegisterCallback<ClickEvent>(eventObj => { testBtn2Clicked?.Invoke(eventObj); });

        CanUseUpdateMethod = true;
    }



    public void PrintToConsole(string message, bool clearTextElement = false)
    {
        _consoleUiModuleHandler.PrintToConsole(message: message, clearTextElement: clearTextElement);
    }

    public void AddDaemonActivity(InterprocessCommunicationMessageLog messageLog)
    {
        _daemonsUiModuleHandler.AddDaemonActivity(messageLog);
    }
}
