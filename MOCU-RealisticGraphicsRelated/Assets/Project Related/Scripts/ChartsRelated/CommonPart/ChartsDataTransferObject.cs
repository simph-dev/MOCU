#nullable enable

using System;
using System.Linq;
using System.Collections.Generic;

using DaemonsRelated;


namespace ChartsModule
{
    public class ChartsDataTransferObject : IDataTransferObject
    {
        // Commands
        public bool SaveAsImage { get; set; } = false;
        public bool OpenAsForm { get; set; } = false;

        // Parameters
        public ChartData? ChartData { get; set; } = null;

        // Responses
        public string ImageFullPath { get; set; } = String.Empty;

        // Common for every DTO
        public IEnumerable<DaemonErrorReport> DaemonErrorReports { get; set; } = Enumerable.Empty<DaemonErrorReport>();
        public bool DoTerminateTheDaemon { get; set; } = false;
        public string CustomMessage { get; set; } = String.Empty;
    }
}