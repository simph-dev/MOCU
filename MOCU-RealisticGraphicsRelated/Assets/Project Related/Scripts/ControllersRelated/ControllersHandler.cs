using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


public class ControllersHandler : ManagedMonoBehaviour
{
    // todo:
    // Dictionary<InputDevice, (HashSet supportedActionMaps, HashSet activeActionMaps)>

    public ModuleStatusHandler<ControllerDevice_SubStatuses> ControllerConnectionStatus;
    public IReadOnlyList<IControllerDevice> Devices { get => _devices; }


    private List<IControllerDevice> _devices;
    private float _checkDeviceConnectionTimeInterval;   // sec


    public override void ManagedAwake()
    {
        _checkDeviceConnectionTimeInterval = 0.1f;
        ControllerConnectionStatus = new();

        _devices = new List<IControllerDevice>
        {
            //new CedrusHandler()
        };
    }

    public async override void ManagedStart()
    {
        foreach (var device in _devices)
            await device.Init();

        CanUseUpdateMethod = true;
    }
    

    public override void ManagedUpdate()
    {
        foreach (var device in _devices)
        {
            if (device.ConnectionStatus == ModuleStatus.FullyOperational)
            {
                ControllerConnectionStatus.UpdateSubStatus(ControllerDevice_SubStatuses.isConnected, SubStatusState.Complete);
                return;
            }

            ControllerConnectionStatus.UpdateSubStatus(ControllerDevice_SubStatuses.isConnected, SubStatusState.Failed);
            //print($"{device.DisplayName}: {device.ConnectionStatus}");
        }
    }

    public bool CanDeviceTriggerActionMap(InputDevice device, ActionMapName actionMap)
    {
        // todo
        return true;
    }

    /*private IEnumerator CheckGamepadConnection(float checkConnectionTimeInterval)
    {
        while (true)
        {
            try
            {
                if (Gamepad.current?.enabled ?? false)
                    GamepadConnectionStatus.UpdateSubState(ControllerDevice_Statuses.isConnected, true);
                else
                    GamepadConnectionStatus.UpdateSubState(ControllerDevice_Statuses.isConnected, false);
            }
            catch
            {
                GamepadConnectionStatus.UpdateSubState(ControllerDevice_Statuses.isConnected, false);
            }

            yield return new WaitForSeconds(checkConnectionTimeInterval);
        }
    }*/
}

/*
 нужен совет: в InputSystem можно узнать о подключенных устройствах. геймпад и клавиатура подключаются автоматически. но у меня есть одно кастомное, которое симулирует клаву и мне нужно постоянно проверять порт на входные данные
 */