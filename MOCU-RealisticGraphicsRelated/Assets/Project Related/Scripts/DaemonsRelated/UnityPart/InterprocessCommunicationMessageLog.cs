using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InterprocessCommunication;

#nullable enable


namespace DaemonsRelated
{
    public class InterprocessCommunicationMessageLog
    {
        public DaemonType daemonName;
        public InterprocessCommunicator_EventType messageSourceType;
        public string messageLabel = String.Empty;
        public DebugMessageType messageSemanticType;
        public string messageContent;
        public DateTime dateTime;

        public InterprocessCommunicationMessageLog()
        {
            dateTime = DateTime.Now;
        }
    }
}