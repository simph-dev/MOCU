/// <summary>
/// The InputSystemTests class is responsible for programmatically creating input action maps and actions
/// by parsing them from enums. Each action map is dynamically constructed based on defined enums, 
/// and corresponding actions are registered within those maps.
///
/// This class also utilizes a middleware 'CommonHandler', which filters input from devices 
/// that are currently inactive for the specified action maps. This middleware ensures
/// that only valid devices and action maps trigger the associated input actions.
///
/// Key features:
/// 1. Programmatic creation of action maps and actions from enums.
/// 2. Middleware ('CommonHandler') intercepts and validates input device support and active state
///    for the associated action maps before invoking action handlers.
/// 3. Event-based input system allowing other components to subscribe to specific action events.
/// </summary>


using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;


public class InputSystemTests : ManagedMonoBehaviour
{
    public event Action<InputAction.CallbackContext> OnIntercomAction;
    public event Action<InputAction.CallbackContext> OnDPadAction;
    public event Action<InputAction.CallbackContext> OnJoystickAction;


    private Dictionary<
        ActionMapName,
        (Type actions, Action<InputAction.CallbackContext> personalHandler)
    > ReadableMapping;

    private Dictionary<
        (string mapName, string actionName),
        (ActionMapName mapName, Enum actionName, Action<InputAction.CallbackContext> personalHandler)
    > ActualMapping;

    private ControllersHandler _controllersHandler;


    public override void ManagedAwake()
    {
        _controllersHandler = GetComponent<ControllersHandler>();

        ReadableMapping = new()
        {
            { ActionMapName.Intercom,  ( typeof(IntercomAction), OnIntercomAction ) },
            { ActionMapName.DPad,      ( typeof(DPadAction),     OnDPadAction ) },
            { ActionMapName.Joystick,  ( typeof(JoystickAction), OnJoystickAction ) },

            // Add new pair if needed
        };

        ActualMapping = new();
        InitializeActionMaps();

        CanUseUpdateMethod = true;
    }

    public override void ManagedStart() { }




    private void InitializeActionMaps()
    {
        foreach (var entry in ReadableMapping)
            CreateActionMap(entry.Key, entry.Value.actions, entry.Value.personalHandler);
    }

    private void CreateActionMap(ActionMapName actionMapName, Type actionEnumType, Action<InputAction.CallbackContext> handler)
    {
        var actionEnumValues = Enum.GetValues(actionEnumType);
        var actionMap = new InputActionMap(actionMapName.ToString());

        foreach (Enum actionName in actionEnumValues)
        {
            var inputAction = actionMap.AddAction(actionName.ToString());

            inputAction.started += CommonHandler;
            inputAction.canceled += CommonHandler;

            ActualMapping.Add((actionMap.name, inputAction.name), (actionMapName, actionName, handler));
        }

        actionMap.Enable();
    }

    private void CommonHandler(InputAction.CallbackContext context)
    {
        var device = context.control.device;
        var actionMapString = context.action.actionMap.name;
        var actionString = context.action.name;

        if (!ActualMapping.TryGetValue((actionMapString, actionString), out var mapping))
        {
            Debug.LogWarning($"ActionMap {actionMapString} or Action {actionString} not found in 'ActualMapping'");
            return;
        }

        if (_controllersHandler.CanDeviceTriggerActionMap(device: device, actionMap: mapping.mapName))
            mapping.personalHandler?.Invoke(context);
    }
}