using Newtonsoft.Json;
using System;

namespace InterprocessCommunication
{
    public class UnifiedResponseFrom_Server : UnifiedDataTransferObject
    {
        [JsonProperty] public readonly bool? errorOccurred;
        [JsonProperty] public readonly bool? errorIsFatal;
        [JsonProperty] public readonly string? errorMessage;


        public UnifiedResponseFrom_Server(string name, object? extraData = null, bool? errorOccurred = null, bool? errorIsFatal = null, string? errorMessage = null) : base(name, extraData)
        {
            this.errorOccurred = errorOccurred;
            this.errorIsFatal = errorIsFatal;
            this.errorMessage = errorMessage;

            if (errorOccurred == true && (errorIsFatal == null || errorMessage == null))
                throw new ArgumentException("If error occurred, you have to specify is it fatal and provide its decription.");
        }
    }
}