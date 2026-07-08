using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem;
using System.Linq;


public class KeyboardSimulator
{
    private KeyboardState _keyboardState;
    private Keyboard _virtualKeyboard;


    public void Init(string deviceName)
    {
        _keyboardState = new KeyboardState();

        InputSystem.RegisterLayout<Keyboard>(
            matches: new InputDeviceMatcher()
                .WithInterface("Keyboard")
                .WithManufacturer("Custom")
                .WithProduct(deviceName));

        _virtualKeyboard = (Keyboard)InputSystem.devices.FirstOrDefault(device =>
            device.description.product == deviceName &&
            device.description.manufacturer == "Custom"
        );

        if (_virtualKeyboard == null)
        {
            _virtualKeyboard = (Keyboard)InputSystem.AddDevice(new InputDeviceDescription
            {
                product = deviceName,
                manufacturer = "Custom",
                interfaceName = "Keyboard"
            });
        }
    }


    public void SimulateKeyPress(Key key, bool isPressed)
    {
        if (isPressed)
            _keyboardState.Press(key);
        else
            _keyboardState.Release(key);

        InputSystem.QueueStateEvent(_virtualKeyboard, _keyboardState);
    }
}