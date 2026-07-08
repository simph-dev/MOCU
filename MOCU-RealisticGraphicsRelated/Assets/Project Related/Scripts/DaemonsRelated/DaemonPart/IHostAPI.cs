using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// todo: rename, it has no sense

namespace DaemonsRelated.DaemonPart
{
    public interface IHostAPI
    {
        event Action<string> TerminateDaemon;
    }
}