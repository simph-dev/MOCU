using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace InterprocessCommunication
{
    public enum InterprocessCommunicator_EventType
    {
        MessageReceived,
        MessageSent,
        ConnectionEstablished,
        ConnectionBroked,
        ErrorOccurred
    }
}