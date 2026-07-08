using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;


public class InputHandler : ManagedMonoBehaviour
{
    //private InputActionAsset _inputActions;
    private InputLogic      _inputLogic;
    private CedrusHandler   _cedrus;
    private UiHandler       _ui;
    private ControllersHandler _controller;

    public event Action OnInputDevicesChanged;

    public event Action GotAnswer_Up;
    public event Action GotAnswer_Down;
    public event Action GotAnswer_Left;
    public event Action GotAnswer_Right;
    public event Action GotSignal_Start;

    public event Action HeadTotalCalibrationRequest;
    public event Action HeadCalibrationRequest;
    public event Action HeightCalibrationRequest;
    public event Action CedrusReconnectionRequest;

    public event Action ToggleNoise;

    private Task _cedrusTask;
    private CancellationTokenSource _cts;

    //public Dictionary<string, bool> inputDevices;

    /*private Dictionary<string, (Action<InputAction.CallbackContext> OnPressed, Action<InputAction.CallbackContext> OnReleased)> _inputSystem_actionHandlers;
    private Dictionary<string, AnswerFromParticipant> _actionNameToSignalMap;*/

    private InputActions _input;


    private float _checkCedrusPortConnectionTimeInterval    = 0.1f; // sec


    public override void ManagedAwake()
    {
        _inputLogic = GetComponent<InputLogic>();
        _ui = GetComponent<UiHandler>();

        //_inputActions = Resources.Load<InputActionAsset>("Inputs/InputActions");    // name of file
        

        _cts = new CancellationTokenSource();
        _input = new();
        _cedrus = new CedrusHandler();
        _cedrus.PreInitDevice("Cedrus");
        _cedrusTask = Task.Run(() => _cedrus.Init(_cts.Token), _cts.Token);
        //inputDevices = new();


        _input.Controller.Up.performed += _ => HandleUp();
        _input.Controller.Down.performed += _ => HandleDown();
        _input.Controller.Left.performed += _ => HandleLeft();
        _input.Controller.Right.performed += _ => HandleRight();
        _input.Controller.Center.performed += _ => HandleStart();

        _input.Controller.Up.canceled += _ => HandleUpCanceled();
        _input.Controller.Down.canceled += _ => HandleDownCanceled();
        _input.Controller.Left.canceled += _ => HandleLeftCanceled();
        _input.Controller.Right.canceled += _ => HandleRightCanceled();
        _input.Controller.Center.canceled += _ => HandleStartCanceled();

        _input.Other.CenterHead.performed += _ => HandleHead();
        _input.Other.CenterHead_HorizontalAngle.performed += _ => HandleHeadDirection();
        _input.Other.CenterHead_Height.performed += _ => HandleHeadHeight();
        _input.Other.ReconnectCedrus.performed += _ => HandleCedrus();

        _input.Other.ToggleNoise.performed += _ => HandleNoise();

        /*_cedrus.GotAnswer_Up += HandleUp;
        _cedrus.GotAnswer_Down += HandleDown;
        _cedrus.GotAnswer_Left += HandleLeft;
        _cedrus.GotAnswer_Right += HandleRight;
        _cedrus.GotSignal_Start += HandleStart;*/

        /*_inputActions.FindAction("Other/CenterHead").performed += context => HeadTotalCalibrationRequest?.Invoke();
        _inputActions.FindAction("Other/CenterHead_HorizontalAngle").performed += context => HeadCalibrationRequest?.Invoke();
        _inputActions.FindAction("Other/CenterHead_Height").performed += context => HeightCalibrationRequest?.Invoke();

        _inputActions.FindAction("Other/ReconnectCedrus").performed += context => CedrusReconnectionRequest?.Invoke();*/

        //_inputActionsAsClass = new InputActions();

        // INPUT SYSTEM PART (gamepad, keyboard and other devices Unity support)
        // Dictionary stores ActionName (from InputActionAsset) and tuple with two handlers: on "press" event and on "release" event
        /*_inputSystem_actionHandlers = new()
        {
            // Part of "Intercom" action map
            { "Input",          (OnPressed: GotSignalFromInputIntercom,     OnReleased: InputIntercomButtonWasReleased)},
            { "Output",         (OnPressed: GotSignalFromOutputIntercom,    OnReleased: OutputIntercomButtonWasReleased)},

            // Part of "Controller" action map
            { "Left",           (OnPressed: GotSignalFromInputSystem,       OnReleased: InputSystemButtonWasReleased)},
            { "Right",          (OnPressed: GotSignalFromInputSystem,       OnReleased: InputSystemButtonWasReleased)},
            { "Up",             (OnPressed: GotSignalFromInputSystem,       OnReleased: InputSystemButtonWasReleased)},
            { "Down",           (OnPressed: GotSignalFromInputSystem,       OnReleased: InputSystemButtonWasReleased)},
            { "Center",         (OnPressed: GotSignalFromInputSystem,       OnReleased: InputSystemButtonWasReleased)}
        };

        _actionNameToSignalMap = new()
        {
            { "Left",   AnswerFromParticipant.Left },
            { "Right",  AnswerFromParticipant.Right },
            { "Up",     AnswerFromParticipant.Up },
            { "Down",   AnswerFromParticipant.Down },
            { "Center", AnswerFromParticipant.Center }
        };*/

        // activates every action from InputSystem and, if it's in dict, adds its handler
        /*foreach (var actionMap in _inputActions.actionMaps)
        {
            foreach (var action in actionMap.actions)
            {
                action.Enable();

                if (_inputSystem_actionHandlers.TryGetValue(action.name, out var handlers))
                {
                    action.performed += handlers.OnPressed;
                    action.canceled += handlers.OnReleased;
                }
            }
        }*/
    }

    public override void ManagedOnEnable() => _input.Enable();
    public override void ManagedOnDisable()
    {
        _input.Disable();

        if (_cts != null)
        {
            _cts.Cancel(); // Посылаем сигнал всем потокам остановиться
            _cts.Dispose(); // Освобождаем ресурсы
            _cts = null;
        }
    }

    private void HandleUp()
    {
        //Debug.Log("Up arrow pressed");
        GotAnswer_Up?.Invoke();

        try { _inputLogic.GotPressSignalFromInputSystem(AnswerFromParticipant.Up); } catch { }
    }

    private void HandleDown()
    {
        //Debug.Log("Down arrow pressed");
        GotAnswer_Down?.Invoke();

        try { _inputLogic.GotPressSignalFromInputSystem(AnswerFromParticipant.Down); } catch { }
    }

    private void HandleLeft()
    {
        //Debug.Log("Left arrow pressed");
        GotAnswer_Left?.Invoke();

        try { _inputLogic.GotPressSignalFromInputSystem(AnswerFromParticipant.Left); } catch { }
    }

    private void HandleRight()
    {
        //Debug.Log("Right arrow pressed");
        GotAnswer_Right?.Invoke();

        try { _inputLogic.GotPressSignalFromInputSystem(AnswerFromParticipant.Right); } catch { }
    }

    private void HandleStart()
    {
        Debug.Log("Start button pressed");
        GotSignal_Start?.Invoke();

        try { _inputLogic.GotPressSignalFromInputSystem(AnswerFromParticipant.Center); } catch { }
    }

    private void HandleUpCanceled()
    {
        try { _inputLogic.GotReleaseSignalFromInputSystem(AnswerFromParticipant.Up); } catch { }
    }

    private void HandleDownCanceled()
    {
        try { _inputLogic.GotReleaseSignalFromInputSystem(AnswerFromParticipant.Down); } catch { }
    }

    private void HandleLeftCanceled()
    {
        try { _inputLogic.GotReleaseSignalFromInputSystem(AnswerFromParticipant.Left); } catch { }
    }

    private void HandleRightCanceled()
    {
        try { _inputLogic.GotReleaseSignalFromInputSystem(AnswerFromParticipant.Right); } catch { }
    }

    private void HandleStartCanceled()
    {
        try { _inputLogic.GotReleaseSignalFromInputSystem(AnswerFromParticipant.Center); } catch { }
    }

    private void HandleHead()
    {
        //Debug.Log("Right arrow pressed");
        HeadTotalCalibrationRequest?.Invoke();
    }

    private void HandleHeadDirection()
    {
        //Debug.Log("Right arrow pressed");
        HeadCalibrationRequest?.Invoke();
    }

    private void HandleHeadHeight()
    {
        //Debug.Log("Right arrow pressed");
        HeightCalibrationRequest?.Invoke();
    }

    private async void HandleCedrus()
    {
        Debug.Log("Try Repair cedrus");
        try
        {
            // Не обязательно использовать Task.Run, если внутри TryRepair уже есть асинхронные вызовы
            // Но если вы хотите унести тяжелый TryConnect в поток:
            await Task.Run(() => _cedrus.TryRepair(), _cts.Token);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Repair failed: {ex.Message}");
        }
    }

    /*public override void ManagedStart()
    {

        //StartCoroutine(_cedrus.CheckConnection(_checkCedrusPortConnectionTimeInterval));

        // todo later: read flags from config
        *//*foreach (var device in InputSystem.devices)
            inputDevices[device.displayName] = true;

        InputSystem.onDeviceChange += OnDeviceChange;*//*

        //CanUseUpdateMethod = true;
    }*/

    private void OnDeviceChange(UnityEngine.InputSystem.InputDevice device, InputDeviceChange change)
    {
        /*switch (change)
        {
            case InputDeviceChange.Added:
                inputDevices[device.displayName] = true;
                print($"Устройство добавлено: {device.displayName}");
                break;

            case InputDeviceChange.Removed:
                inputDevices.Remove(device.displayName);
                print($"Устройство удалено: {device.displayName}");
                break;
        }

        OnInputDevicesChanged?.Invoke();*/
    }

    //THE NESTING IN FOLLOWING FUNCTIONS MAY SEEM REDUNDANT, BUT LET IT BE JUST IN CASE


    // TODO: yes, it will be working by Ignoring
    private void GotSignalFromInputSystem(InputAction.CallbackContext context)
    {
        // todo: work on it. Maye just ignore if it's not in the list of active devices
        /*var devicesList = InputSystem.devices;
        var device = context.control.device;

        if (_actionNameToSignalMap.TryGetValue(context.action.name, out AnswerFromParticipant signalFromParticipant))
            _inputLogic.GotPressSignalFromInputSystem(signalFromParticipant);*/

    }
    private void InputSystemButtonWasReleased(InputAction.CallbackContext context)
    {
        /*if (_actionNameToSignalMap.TryGetValue(context.action.name, out AnswerFromParticipant signalFromParticipant))
            _inputLogic.GotReleaseSignalFromInputSystem(signalFromParticipant);*/
    }


    /// <summary>
    /// When participant calls
    /// </summary>
    private void GotSignalFromInputIntercom(InputAction.CallbackContext context)
    {
        _inputLogic.IntercomFromParticipantStarted();
    }
    private void InputIntercomButtonWasReleased(InputAction.CallbackContext context)
    {
        _inputLogic.IntercomFromParticipantStopped();
    }

    /// <summary>
    /// When researcher calls
    /// </summary>
    private void GotSignalFromOutputIntercom(InputAction.CallbackContext context)
    {
        _inputLogic.IntercomFromResearcherStarted();
    }
    private void OutputIntercomButtonWasReleased(InputAction.CallbackContext context)
    {
        _inputLogic.IntercomFromResearcherStopped();
    }


    private void HandleNoise()
    {
        ToggleNoise?.Invoke();
    }
}