using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DaemonsRelated.DaemonPart
{
    public interface IDaemonLogic
    {
        event Action<string> TerminateDaemon;
        void Run();
        void DoBeforeExit();
    }
}