using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaemonsRelated
{
    public class MinimalDataTransferObject : IDataTransferObject
    {
        public IEnumerable<DaemonErrorReport> DaemonErrorReports    { get; set; } = Enumerable.Empty<DaemonErrorReport>();
        public bool DoTerminateTheDaemon                            { get; set; } = false;
        public string CustomMessage                                 { get; set; } = String.Empty;
    }
}
