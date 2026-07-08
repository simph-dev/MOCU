using System;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;


public class Fps_DebugUiModuleHandler : ManagedMonoBehaviour
{
    private UiHandler _uiHandler;
    private UiReferences _uiReference;

    private ProfilerRecorder _mainThreadTimeRecorder;
    private long[] _framesDurationsBuffer;
    private int _frameIndex = 0;
    private int _framesDurationsBufferSize = 15;

    private TextElement _currentFpsValueCell;
    private TextElement _lastFrameTimeValueCell;
    private TextElement _averageFrameTimeValueCell;


    // BASIC FUNCTIONALITY

    public override void ManagedAwake()
    {
        _uiHandler = GetComponent<UiHandler>();
    }

    public override void ManagedStart()
    {
        _mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        // 1 is recorder.Capacity (no need in more than 1 because I take only the last one)

        _uiReference = _uiHandler.secondaryUiScreen;

        _currentFpsValueCell = _uiReference.elements.debugTab.fpsModule.fps;
        _lastFrameTimeValueCell = _uiReference.elements.debugTab.fpsModule.lastFrameTime;
        _averageFrameTimeValueCell = _uiReference.elements.debugTab.fpsModule.averageFrameTime;

        CanUseUpdateMethod = true;
    }

    public override void ManagedUpdate()
    {
        UpdateFpsModule();
    }


    // CUSTOM FUNCTIONALITY

    private double GetAverageFrameDuration(ProfilerRecorder profilerRecorder)                               // function should be called only once per frame. returns ns
    {
        if (_framesDurationsBuffer == null) _framesDurationsBuffer = new long[_framesDurationsBufferSize];  // first init

        _framesDurationsBuffer[_frameIndex++] = profilerRecorder.LastValue;                                 // upd last frame + increment index
        if (_frameIndex == _framesDurationsBuffer.Length) _frameIndex = 0;                                  // jump to start in case of overflow

        if (Array.Exists(_framesDurationsBuffer, element => element == 0)) return 0;                        // if not all cells are full

        return _framesDurationsBuffer.Sum() / _framesDurationsBuffer.Length;                                // arithmetic mean

        // if func is called several times per same frame -- "average frame duration" will be closer to "last frame duration"
        // if func is called not every frame -- "average frame duration" will be more "average" and not for "_framesDurationsBufferSize" last frames
    }
    private void UpdateFpsModule()
    {
        var averageFrameDuration = GetAverageFrameDuration(_mainThreadTimeRecorder);

        _currentFpsValueCell.text = $"{1000 / (averageFrameDuration * (1e-6f)):F0}";
        _lastFrameTimeValueCell.text = $"{_mainThreadTimeRecorder.LastValue * (1e-6f):F1} ms";
        _averageFrameTimeValueCell.text = $"{averageFrameDuration * (1e-6f):F1} ms";
    }
}
