using DaemonsRelated;
using InterprocessCommunication;
using System;
using UnityEngine;
using UnityEngine.UIElements;


public class Daemons_DebugUiModuleHandler : ManagedMonoBehaviour
{
    private UiHandler _uiHandler;
    private UiReferences _uiReference;
    private DaemonsHandler _daemonsHandler;
    
    private VisualTreeAsset _daemonActivitieTemplate;
    private TextElement _numberOfDaemonsCell;
    private ScrollView _daemonsActivities;
    private TextElement _daemonsActivitiesText;

    private System.Diagnostics.Stopwatch _testStopwatch;



    public override void ManagedAwake()
    {
        _uiHandler = GetComponent<UiHandler>();
        _daemonsHandler = GetComponent<DaemonsHandler>();

        _daemonActivitieTemplate = Resources.Load<VisualTreeAsset>("GUI/UXML/DebugModules/DaemonsSubModule_ActivityTemplate");
    }

    public override void ManagedStart()
    {
        _uiReference = _uiHandler.secondaryUiScreen;

        _daemonsActivities = _uiReference.elements.debugTab.daemonsModule.activities;
        _daemonsActivitiesText = _uiReference.elements.debugTab.daemonsModule.activitiesText;
        _daemonsActivities.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        _numberOfDaemonsCell = _uiReference.elements.debugTab.daemonsModule.totalNumber;

        _daemonsHandler.NewMessageLogged += NewMessageLogged;
        _daemonsHandler.DaemonStatusChanged += DaemonStatusChanged;

        CanUseUpdateMethod = true;

        _testStopwatch = new();
        _testStopwatch.Start();
    }

    public override void ManagedUpdate()
    {

    }



    private void NewMessageLogged(InterprocessCommunicationMessageLog message)
    {
        if (_daemonsActivitiesText.text.Length != 0)
            _daemonsActivitiesText.text += "\n";    // new line for every message except the first one

        _daemonsActivitiesText.text += $"{message.messageLabel} - {message.daemonName} - {message.messageSemanticType} - {message.messageSourceType}";
        //Debug.Log($"messageLabel: {message.messageLabel}. MS: {_testStopwatch.Elapsed.TotalMilliseconds}");
        //Debug.Log($"messageContent: {message.messageContent}");
    }

    private void DaemonStatusChanged()
    {
        var daemons = _daemonsHandler.GetDaemonsInfo();

        Debug.Log($"Updated info (daemons):");

        foreach (var daemon in daemons)
            Debug.Log($"{daemon.type} - {daemon.status}");

        Debug.Log($"...\n");

    }

    // todo: add color to message according to its type
    // todo: do max capacity for logs. it's VisualElements, they 'eat' a lot
    public void AddDaemonActivity(InterprocessCommunicationMessageLog messageLog)
    {
        /*var instance = _daemonActivitieTemplate.CloneTree();

        var iconClass = $"is{messageLog.messageSourceType}";   // 
        var icon = instance.Q<VisualElement>(className: "debug-daemons-activity-icon");
        var name = (TextElement)instance.Q<VisualElement>(className: "debug-daemons-activity-daemon");
        var description = (TextElement)instance.Q<VisualElement>(className: "debug-daemons-activity-message");

        icon.AddToClassList(iconClass);
        name.text = messageLog.daemonName.ToString();
        description.text = $" - {messageLog.messageLabel}";

        _daemonsActivities.Add(instance);

        // note: this **** needs to be postponed because UI updates with some delay
        // Force layout update and scroll to the bottom ensuring full visibility
        _daemonsActivities.schedule.Execute(() =>
        {
            _daemonsActivities.ScrollTo(instance);
            _daemonsActivities.schedule.Execute(() =>
            {
                _daemonsActivities.verticalScroller.value = _daemonsActivities.verticalScroller.highValue > 0 ? _daemonsActivities.verticalScroller.highValue : 0;
            }).ExecuteLater(0); // Execute on the next frame to ensure layout is updated
        }).ExecuteLater(0); // Execute on the next frame*/
    }
}
