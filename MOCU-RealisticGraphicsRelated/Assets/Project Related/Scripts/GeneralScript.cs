using MirrorExperiment;
using MoogModule;
using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;


public class GeneralScript : ManagedMonoBehaviour
{
    public override void ManagedAwake() { }
    public override void ManagedStart()
    {
        QualitySettings.vSyncCount = 0;                             // Disable VSync
        Application.targetFrameRate = 90;                           // Application fps (when VR is on -- automatically switchs to VR fps (90))
        Invoke(nameof(InitializeVR), 1.0f);

        CanUseUpdateMethod = true;
    }

    public override void ManagedOnDisable()
    {
        var manager = XRGeneralSettings.Instance.Manager;

        if (manager != null && manager.isInitializationComplete)
        {
            Debug.Log("Stopping XR Subsystems...");
            manager.StopSubsystems();

            Debug.Log("Deinitializing XR Loader...");
            manager.DeinitializeLoader();
        }
    }

    private void InitializeVR()
    {
        Debug.Log("Initializing XR...");
        var manager = XRGeneralSettings.Instance.Manager;
        manager.InitializeLoaderSync();

        if (manager.activeLoader != null)
        {
            manager.StartSubsystems();
            Debug.Log("XR Started!");
        }
        else
        {
            Debug.LogError("Failed to initialize XR Loader.");
        }

        XRSettings.gameViewRenderMode = GameViewRenderMode.None;    // prevents rendering VR view on monitor (works only in Build version)
        XRSettings.showDeviceView = false;

        var RH = GetComponent<ExperimentHandler>();
        StartCoroutine(DelayedCalibration(RH));
    }

    private IEnumerator DelayedCalibration(ExperimentHandler rh)
    {
        var vr = GetComponent<VrHandler>();

        Debug.Log("Ждем калибровки и подключения шлема...");

        yield return new WaitUntil(() =>
            vr.XRConnectionStatus.Status == ModuleStatus.FullyOperational
        );

        Debug.Log("Шлем готов!");

        // Full calibration, not rotation-only: CalibrateHeadRotation deliberately
        // preserves the current world eye position, so on its own it leaves the eye
        // at whatever height the headset started at instead of bringing it to
        // CameraStartPosition. Harmless while that was 1.7 and the eye already sat
        // there; visible now that it is zero.
        GetComponent<ExperimentHandler>().CalibrateHead();
    }
}