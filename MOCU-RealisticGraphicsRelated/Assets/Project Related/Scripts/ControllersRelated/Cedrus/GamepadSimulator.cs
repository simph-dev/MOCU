using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using System.Linq;


public class GamepadSimulator
{
    private GamepadState _gamepadState;
    private Gamepad _virtualGamepad;


    public void Init(string deviceName)
    {
        _gamepadState = new GamepadState();

        _virtualGamepad = InputSystem.devices
            .OfType<Gamepad>()
            .FirstOrDefault(d => d.name == deviceName);

        if (_virtualGamepad == null)
            _virtualGamepad = InputSystem.AddDevice<Gamepad>(deviceName);
    }


    public void SimulateButtonPress(GamepadButton button, bool isPressed)
    {
        if (_virtualGamepad == null) return;
        _gamepadState = _gamepadState.WithButton(button, isPressed);
        InputSystem.QueueStateEvent(_virtualGamepad, _gamepadState);
    }
}