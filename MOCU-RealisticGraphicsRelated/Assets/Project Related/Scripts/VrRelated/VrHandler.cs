using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;


public class VrHandler : ManagedMonoBehaviour
{
    public ModuleStatusHandler<VrHeadset_Statuses> XRConnectionStatus;

    private float _checkXRConnectionTimeInterval;   // sec


    public override void ManagedAwake()
    {
        _checkXRConnectionTimeInterval = 0.1f;
        XRConnectionStatus = new();
    }

    public override void ManagedStart()
    {
        StartCoroutine(CheckConnection(_checkXRConnectionTimeInterval));
        CanUseUpdateMethod = true;
    }


    private IEnumerator CheckConnection(float checkConnectionTimeInterval)
    {
        while (true)
        {
            try
            {
                List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
                SubsystemManager.GetSubsystems(subsystems);

                if (subsystems.Count != 1)
                {
                    XRConnectionStatus.UpdateSubStatus(VrHeadset_Statuses.isConnected, SubStatusState.Failed);
                    XRConnectionStatus.UpdateSubStatus(VrHeadset_Statuses.iOnHead, SubStatusState.Failed);
                }
                else if (subsystems[0].running && IsHeadsetWorn())
                {
                    XRConnectionStatus.UpdateSubStatus(VrHeadset_Statuses.isConnected, SubStatusState.Complete);
                    XRConnectionStatus.UpdateSubStatus(VrHeadset_Statuses.iOnHead, SubStatusState.Complete);
                }
                else
                {
                    XRConnectionStatus.UpdateSubStatus(VrHeadset_Statuses.isConnected, SubStatusState.Complete);
                    XRConnectionStatus.UpdateSubStatus(VrHeadset_Statuses.iOnHead, SubStatusState.StillNotSet);
                    // 'null' and not 'false' because I need status to be yellow, not red (half working)
                }
            }
            catch
            {
                XRConnectionStatus.UpdateSubStatus(VrHeadset_Statuses.isConnected, SubStatusState.Failed);
                XRConnectionStatus.UpdateSubStatus(VrHeadset_Statuses.iOnHead, SubStatusState.Failed);

                Debug.LogError("Crash in 'CheckConnection'");
            }

            yield return new WaitForSeconds(checkConnectionTimeInterval);
        }
    }

    private bool IsHeadsetWorn()
    {
        List<XRNodeState> nodeStates = new List<XRNodeState>();
        InputTracking.GetNodeStates(nodeStates);

        foreach (var nodeState in nodeStates)
        {
            if (nodeState.nodeType == XRNode.Head)
                return nodeState.tracked;
        }

        return false;
    }
}
