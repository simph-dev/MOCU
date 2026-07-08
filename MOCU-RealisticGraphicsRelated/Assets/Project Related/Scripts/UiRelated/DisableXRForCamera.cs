using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class DisableXRForCamera : MonoBehaviour
{
    void Awake()
    {
        var data = GetComponent<HDAdditionalCameraData>();
        if (data != null)
        {
            data.xrRendering = false; // Гарантируем отключение XR через код
        }

        var cam = GetComponent<Camera>();
        cam.stereoTargetEye = StereoTargetEyeMask.None; // Запрещаем камере смотреть в шлем
    }

    void Start()
    {
        // Находим камеру монитора и жестко отключаем ей VR функции
        var monitorCam = GameObject.Find("SecondMonitorCamera").GetComponent<Camera>();
        monitorCam.stereoTargetEye = StereoTargetEyeMask.None; // Решает ошибку со скрина image_135d4d.png

        var monitorData = monitorCam.GetComponent<HDAdditionalCameraData>();
        if (monitorData != null) monitorData.xrRendering = false;
    }
}