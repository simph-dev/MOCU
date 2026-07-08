using System.Collections.Generic;
using System;
using System.Linq;


public class ModuleStatusHandler<TEnum> : IModuleStatusHandler where TEnum : Enum
{
    private Dictionary<TEnum, SubStatusState> _subStatuses;

    public bool InDebugMode { get; private set; }
    public ModuleStatus Status { get; private set; }

    public ModuleStatusHandler(bool inDebugMode = false)
    {
        InDebugMode = inDebugMode;
        Status = ModuleStatus.Inactive;
        InitDictionary();
    }

    public void UpdateSubStatus(Enum subStatusName, SubStatusState subStatusValue)
    {
        var _subStatusName = (TEnum)subStatusName;

        if (!_subStatuses.ContainsKey(_subStatusName))
            throw new KeyNotFoundException($"SubStatus '{_subStatusName}' not found.");

        if (_subStatuses[_subStatusName] == subStatusValue)
            return;

        _subStatuses[_subStatusName] = subStatusValue;
        RecalculateModuleStatus();
    }




    private void InitDictionary()
    {
        _subStatuses = new Dictionary<TEnum, SubStatusState>();

        foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
            _subStatuses[value] = SubStatusState.StillNotSet;
    }

    private void RecalculateModuleStatus()
    {
        if (_subStatuses.Values.All(subStatusValue => subStatusValue == SubStatusState.StillNotSet))
        {
            Status = ModuleStatus.Inactive;
            return;
        }

        if (_subStatuses.Values.All(subStatusValue => subStatusValue == SubStatusState.Complete))
        {
            Status = ModuleStatus.FullyOperational;
            return;
        }

        if (_subStatuses.Where(subStatus => IsCritical(subStatus.Key)).All(subStatus => subStatus.Value == SubStatusState.Complete))
        {
            Status = ModuleStatus.PartiallyOperational;
            return;
        }

        if (_subStatuses.Where(subStatus => IsCritical(subStatus.Key)).Any(subStatus => subStatus.Value == SubStatusState.Failed))
        {
            Status = ModuleStatus.NotOperational;
            return;
        }

        Status = ModuleStatus.InSetup;
    }

    private bool IsCritical(TEnum subStatusName)
    {
        return (SubStatusImportance)(object)subStatusName == SubStatusImportance.Critical;
    }
}