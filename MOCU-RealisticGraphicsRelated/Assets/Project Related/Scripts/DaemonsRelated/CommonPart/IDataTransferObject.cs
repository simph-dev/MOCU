using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaemonsRelated
{
    public interface IDataTransferObject
    {
        IEnumerable<DaemonErrorReport> DaemonErrorReports { get; set; }
        bool DoTerminateTheDaemon { get; set; }
        string CustomMessage { get; set; }
    }
}