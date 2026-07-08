using System.Collections;
using System.Threading.Tasks;


public interface IControllerDevice
{
    ModuleStatus ConnectionStatus { get; }  // todo: think about it
    string DisplayName { get; }

    void TryRepair();
    Task Init();
}