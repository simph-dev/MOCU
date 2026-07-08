using InterprocessCommunication;
using System;

// todo: delete later
internal interface IDaemonUser : IBusinessLogic_Client { }


namespace DaemonsRelated
{
    public interface IDaemonUser
    {
        event Action<string> ReceivedMessageFromDaemon;
        event Action<string> SentMessageToDaemon;
    }
}