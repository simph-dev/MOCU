using System;


namespace InterprocessCommunication
{
    public interface IInterprocessCommunicator
    {
        bool IsOperational { get; }
        void Start();
        void Stop(string reason = "unknown");
        void SendMessage(string message);

        event Action<string> MessageReceived;
        event Action<string> MessageSent;
        event Action<string> ConnectionEstablished;
        event Action<string> ConnectionBroked;
        event Action<string> ErrorOccurred;
    }
}