using System;
using UnityEngine;
using UnityEngine.InputSystem;


public class JoystickForMoogTest : ManagedMonoBehaviour
{
    private Joystick _joystick;
    private InputAction _stickAction;

    public event Action<Vector2> OnStickPositionChanged;

    public override void ManagedAwake()
    {
        _joystick = Joystick.current;

        if (_joystick == null)
            Debug.LogError("No joystick connected.");
        else
            Debug.Log("Joystick connected: " + _joystick.displayName);

        _stickAction = new InputAction(type: InputActionType.Value, binding: "<Joystick>/stick");
        _stickAction.performed += OnStickChanged;
        _stickAction.Enable();
    }

    private void OnStickChanged(InputAction.CallbackContext context)
    {
        var stickPosition = context.ReadValue<Vector2>();
        Debug.Log($"Stick Position Changed: X = {stickPosition.x}, Y = {stickPosition.y}");
        OnStickPositionChanged?.Invoke(stickPosition);
    }

    private void OnDestroy()
    {
        if (_stickAction != null)
        {
            _stickAction.performed -= OnStickChanged;
            _stickAction.Disable();
        }
    }
}