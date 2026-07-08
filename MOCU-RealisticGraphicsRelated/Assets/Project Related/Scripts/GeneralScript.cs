using MoogModule;
using RaceExperiment;
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

        GetComponent<ExperimentHandler>().CalibrateHeadRotation();
    }
}