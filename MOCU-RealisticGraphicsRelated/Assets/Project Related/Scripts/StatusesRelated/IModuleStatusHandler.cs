using System;


public interface IModuleStatusHandler
{
    ModuleStatus Status { get; }
    bool InDebugMode { get; }
    void UpdateSubStatus(Enum subStatusName, SubStatusState subStatusValue);
}