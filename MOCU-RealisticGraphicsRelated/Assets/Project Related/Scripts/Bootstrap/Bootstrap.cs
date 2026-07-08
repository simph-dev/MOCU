/*
 * The Bootstrap class manages the lifecycle of various components in a Unity project by controlling the invocation of initialization and update methods.
 * Specifically, it handles the ManagedAwake, ManagedStart, and ManagedUpdate methods of components.
 * The ManagedUpdate method is particularly useful for centralizing the check of CanUseUpdateMethod,
 * ensuring that each component only updates when it's ready, thereby avoiding redundant checks within individual components.
 * 
 * The order is important, as some components depend on others being initialized first.
 */


using ChartsModule;
using MoogModule;
using RaceExperiment;
using Temporal;
using UnityEngine;


public class Bootstrap : MonoBehaviour
{
    private ManagedMonoBehaviour[] _components;
    private GameObject _gameObject_scripts;
    private GameObject _gameObject_audio;
    private GameObject _gameObject_guiMainMonitor;
    private GameObject _gameObject_guiSecondaryMonitor;


    private void Awake()
    {
        _gameObject_scripts = GameObject.Find("Scripts");
        _gameObject_audio = GameObject.Find("Audio");
        _gameObject_guiMainMonitor = GameObject.Find("GUI_main_monitor");
        _gameObject_guiSecondaryMonitor = GameObject.Find("GUI_second_monitor");

        _components = new ManagedMonoBehaviour []
        {
            // MAIN
            EnsureComponent<GlobalExceptionHandler>(),
            EnsureComponent<GeneralScript>(),
            EnsureComponent<UnityMainThreadDispatcher>(),

            // UI
            EnsureComponent<UiReferences>(_gameObject_guiMainMonitor),
            EnsureComponent<UiReferences>(_gameObject_guiSecondaryMonitor),
            EnsureComponent<UiHandler>(),
            EnsureComponent<ExperimentTabHandler>(),
            EnsureComponent<DebugTabHandler>(),
                EnsureComponent<Daemons_DebugUiModuleHandler>(),
                EnsureComponent<DifferentInfo_DebugUiModuleHandler>(),
                EnsureComponent<Console_DebugUiModuleHandler>(),
                EnsureComponent<Fps_DebugUiModuleHandler>(),
            EnsureComponent<SettingsTabHandler>(),
                EnsureComponent<Devices_SettingsUiModuleHandler>(),

            // MODULES
            EnsureComponent<AudioHandler_new>(),    // in test mode
            EnsureComponent<ConfigHandler>(),       // important to be as high as possible
            EnsureComponent<StatusesHandler>(),
            EnsureComponent<AudioHandler>(),
            EnsureComponent<DaemonsHandler>(),
            EnsureComponent<AnswerHandler>(),
            EnsureComponent<ControllersHandler>(),
            EnsureComponent<InputHandler>(),
            EnsureComponent<VrHandler>(),
            EnsureComponent<InputLogic>(),

            // MOOG
            //EnsureComponent<JoystickForMoogTest>(),
            //EnsureComponent<TrajectoryMakerForMoogTest>(),
            EnsureComponent<MoogHandler>(),

            // TESTS
            EnsureComponent<ForTests>(),
            EnsureComponent<ChartsHandler>(),
            //EnsureComponent<FixedUpdateMonitor>(),

            // SIDE EXPERIMENTS
            EnsureComponent<ExperimentHandler>(),
            EnsureComponent<TemporalSound>(_gameObject_audio),
            //EnsureComponent<MeshisHandler>(),
            //EnsureComponent<TemporalResponseHandler>(),
        };


        foreach (var component in _components)
            component.ManagedAwake();
    }

    private void OnEnable()
    {
        foreach (var component in _components)
            component.ManagedOnEnable();
    }

    private void OnDisable()
    {
        foreach (var component in _components)
            component.ManagedOnDisable();
    }

    private void Start()
    {
        foreach (var component in _components)
            component.ManagedStart();
    }

    private void Update()
    {
        foreach (var component in _components)
            if (component.CanUseUpdateMethod)
                component.ManagedUpdate();   
    }



    private T EnsureComponent<T>(GameObject GO = null) where T : ManagedMonoBehaviour
    {
        GO ??= _gameObject_scripts;
        T component = GO.GetComponent<T>();

        if (component == null)
            component = GO.AddComponent<T>();

        return component;
    }
}