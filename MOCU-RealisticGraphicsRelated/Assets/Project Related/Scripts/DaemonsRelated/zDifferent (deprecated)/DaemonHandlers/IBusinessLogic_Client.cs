using System;

namespace InterprocessCommunication
{
    public interface IBusinessLogic_Client
    {
        void ProcessResponse(UnifiedResponseFrom_Server response);
    }
}
