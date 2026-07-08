using Newtonsoft.Json;
using System;
using UnityDaemonsCommon;

namespace InterprocessCommunication
{
    public class UnifiedDataTransferObject
    {
        [JsonProperty]
        public readonly string name;

        [JsonProperty]
        private readonly string? _extraDataJson;

        [JsonProperty]
        private readonly string? _extraDataTypeName;

        public UnifiedDataTransferObject(string name, object? extraData = null)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException("Name cannot be null or empty: ", nameof(name));

            this.name = name;

            if (extraData != null)
            {
                _extraDataJson = CommonUtilities.SerializeJson(extraData);
                _extraDataTypeName = extraData.GetType().FullName;
            }
            else
            {
                _extraDataJson = null;
                _extraDataTypeName = null;
            }
        }

        public T? GetExtraData<T>() where T : class
        {
            if (String.IsNullOrEmpty(_extraDataJson) || _extraDataTypeName == null)
            {
                throw new InvalidOperationException($"GetExtraData: JsonData string and DataTypeName can't be null or empty.");
            }


            if (_extraDataTypeName != typeof(T).FullName)
                throw new InvalidOperationException($"GetExtraData: Expected type {_extraDataTypeName} but got type {typeof(T).FullName}");

            try
            {
                return CommonUtilities.DeserializeJson<T>(_extraDataJson);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"GetExtraData: Failed to deserialize extra data. Error: {ex}");
            }
        }
    }
}
