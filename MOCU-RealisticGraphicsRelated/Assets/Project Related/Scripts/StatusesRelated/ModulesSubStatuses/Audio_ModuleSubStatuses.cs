public enum Audio_ModuleSubStatuses
{
    StartAudioProcess           = SubStatusImportance.Critical,
    StartNamedPipeConnection    = SubStatusImportance.Critical,
    SetConfigs                  = SubStatusImportance.Critical,
    GetAudioDevices             = SubStatusImportance.Critical,
    AtLeastOneOutputIsWorking   = SubStatusImportance.Critical,

    AllDevicesAreChoosen        = SubStatusImportance.NonCritical
}

/*
BackendIsReady (c)
InitParamsSet (c)
AllDevicesAreChoosen (n)
*/