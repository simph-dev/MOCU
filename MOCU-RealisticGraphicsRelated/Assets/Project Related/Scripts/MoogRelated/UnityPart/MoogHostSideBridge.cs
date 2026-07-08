using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using AudioModule;
using DaemonsRelated;
using InterprocessCommunication;
using MoogModule.Daemon;


namespace MoogModule
{
    public class MoogHostSideBridge : MoogDaemon_API, IDaemonHostBridge
    {
        private IInterprocessCommunicator _communicator;

        public event Action<MoogRealTimeState> MachineStateChanged;
        public event Action<MoogRealTimeState> State;
        public event Action<MoogFeedback> Feedback;

        private string _premadeCommand_Engage;
        private string _premadeCommand_Disengage;
        private string _premadeCommand_Reset;
        private string _premadeCommand_StartReceivingFeedback;
        private string _premadeCommand_StopReceivingFeedback;

        public MoogHostSideBridge(IInterprocessCommunicator communicator)
        {
            _communicator                           = communicator;
            _communicator.MessageReceived           += message => HandleIncomingMessage(message);

            _premadeCommand_Engage                  = JsonHelper.SerializeJson(new MoogDataTransferObject { EngageCommand = true });
            _premadeCommand_Disengage               = JsonHelper.SerializeJson(new MoogDataTransferObject { DisengageCommand = true });
            _premadeCommand_Reset                   = JsonHelper.SerializeJson(new MoogDataTransferObject { ResetCommand = true });
            _premadeCommand_StartReceivingFeedback  = JsonHelper.SerializeJson(new MoogDataTransferObject { StartReceivingFeedback = true });
            _premadeCommand_StopReceivingFeedback   = JsonHelper.SerializeJson(new MoogDataTransferObject { StopReceivingFeedback = true });
        }

        // ########################################################################################

        public void Connect(MachineSettings parameters)
        {
            var DTO = new MoogDataTransferObject { ConnectCommand = true, ConnectParameters = parameters };
            var json = JsonHelper.SerializeJson(DTO);
            _communicator.SendMessage(json);
        }

        public void Engage()
        {
            _communicator.SendMessage(_premadeCommand_Engage);
        }

        public void Disengage()
        {
            _communicator.SendMessage(_premadeCommand_Disengage);
        }

        public void Reset()
        {
            _communicator.SendMessage(_premadeCommand_Reset);
        }

        public void StartReceivingFeedback()
        {
            _communicator.SendMessage(_premadeCommand_StartReceivingFeedback);
        }

        public void StopReceivingFeedback()
        {
            _communicator.SendMessage(_premadeCommand_StopReceivingFeedback);
        }

        public void MoveToPoint(MoveToPointParameters parameters)
        {
            var DTO = new MoogDataTransferObject { MoveToPointCommand = true, MoveToPointParameters = parameters };
            var json = JsonHelper.SerializeJson(DTO);
            _communicator.SendMessage(json);
        }

        public void MoveByTrajectory(MoveByTrajectoryParameters parameters)
        {
            var DTO = new MoogDataTransferObject { MoveByTrajectoryCommand = true, MoveByTrajectoryParameters = parameters };
            var json = JsonHelper.SerializeJson(DTO);
            _communicator.SendMessage(json);
        }


        public void Test(object input = null)
        {
            var DTO = new MoogDataTransferObject { CustomMessage = "test request" };
            var json = JsonHelper.SerializeJson(DTO);
            _communicator.SendMessage(json);
        }

        // ########################################################################################

        // todo
        private void HandleIncomingMessage(string message)
        {
            try
            {
                //UnityEngine.Debug.Log($"Got message from Moog: {message}");
                var DTO = JsonHelper.DeserializeJson<MoogDataTransferObject>(message);

                // CUSTOM MESSAGE
                if (!String.IsNullOrEmpty(DTO.CustomMessage))
                    UnityEngine.Debug.Log($"Custom message in 'HandleIncomingMessage': {DTO.CustomMessage}");

                // 
                if (DTO.Feedback != null)
                    Feedback?.Invoke(DTO.Feedback);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error in 'HandleIncomingMessage': {ex.Message}");
            }
        }
    }
}