using UnityEngine;


public class ManagedMonoBehaviour : MonoBehaviour
{
    public bool CanUseUpdateMethod { get; protected set; } = false;

    public virtual void ManagedAwake() { }
    public virtual void ManagedOnEnable() { }
    public virtual void ManagedOnDisable() { }
    public virtual void ManagedStart() { }
    public virtual void ManagedUpdate() { }
}