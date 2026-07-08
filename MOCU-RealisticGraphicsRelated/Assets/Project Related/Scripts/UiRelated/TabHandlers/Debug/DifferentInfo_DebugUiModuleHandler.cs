using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;


public class DifferentInfo_DebugUiModuleHandler : ManagedMonoBehaviour
{
    private UiHandler _uiHandler;
    private UiReferences _uiReference;

    private ProfilerRecorder _systemMemoryRecorder;
    private ProfilerRecorder _gcMemoryRecorder;

    private TextElement _memoryValueCell;
    private TextElement _gcValueCell;
    private TextElement _timeWorkingValueCell;



    // BASIC FUNCTIONALITY

    public override void ManagedAwake()
    {
        _uiHandler = GetComponent<UiHandler>();

        _systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        _gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
    }

    public override void ManagedStart()
    {
        _uiReference = _uiHandler.secondaryUiScreen;

        _memoryValueCell = _uiReference.elements.debugTab.diffInfoModule.memory;
        _gcValueCell = _uiReference.elements.debugTab.diffInfoModule.gc;
        _timeWorkingValueCell = _uiReference.elements.debugTab.diffInfoModule.totalTimeWorking;

        CanUseUpdateMethod = true;

        // todo: consider changing 'ControllableUpdate' to 'GetComponent<UIDocument>().rootVisualElement.schedule.Execute(UpdateTimer).Every(1000);'
    }

    public override void ManagedUpdate()
    {
        UpdateDifferentInfoModule();
    }


    // CUSTOM FUNCTIONALITY

    private void UpdateDifferentInfoModule()
    {
        _memoryValueCell.text = $"{_systemMemoryRecorder.LastValue / (1024 * 1024)} MB";
        _gcValueCell.text = $"{_gcMemoryRecorder.LastValue / (1024 * 1024)} MB";
        _timeWorkingValueCell.text = $"{(Time.realtimeSinceStartup / 60):F0} min";
    }
}
