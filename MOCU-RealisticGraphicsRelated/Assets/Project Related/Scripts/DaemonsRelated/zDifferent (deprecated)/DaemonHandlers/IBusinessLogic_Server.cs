using System;

namespace InterprocessCommunication
{
    public interface IBusinessLogic_Server
    {
        event Action<UnifiedResponseFrom_Server> SendResponse;
        void ProcessCommand(UnifiedCommandFrom_Client command);
    }
}
