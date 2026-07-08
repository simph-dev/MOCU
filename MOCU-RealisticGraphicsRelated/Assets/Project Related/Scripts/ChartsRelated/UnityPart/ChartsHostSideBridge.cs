using System;
using UnityEngine;

using DaemonsRelated;
using InterprocessCommunication;
using MoogModule;


namespace ChartsModule
{
    public class ChartsHostSideBridge : ChartsDaemon_API, IDaemonHostBridge
    {
        private IInterprocessCommunicator _communicator;

        public event Action<string> ChartImageGenerated;

        public ChartsHostSideBridge(IInterprocessCommunicator communicator)
        {
            _communicator = communicator;
            _communicator.MessageReceived += message => HandleIncomingMessage(message);
        }

        /*public void Test()
        {
            GenerateChartAsForm();
        }*/

        public void GenerateChartAsImage(ChartData chartData)
        {
            var DTO = new ChartsDataTransferObject { ChartData = chartData, SaveAsImage = true };
            var json = JsonHelper.SerializeJson(DTO);
            _communicator.SendMessage(json);
        }
        public void GenerateChartAsForm(ChartData chartData)
        {
            var DTO = new ChartsDataTransferObject { ChartData = chartData, OpenAsForm = true };
            var json = JsonHelper.SerializeJson(DTO);
            _communicator.SendMessage(json);
        }

        // ........................................................................................

        private void HandleIncomingMessage(string message)
        {
            //Debug.Log($"Message from daemon (charts): {message}");

            var DTO = JsonHelper.DeserializeJson<ChartsDataTransferObject>(message);

            // CUSTOM MESSAGE
            if (!String.IsNullOrEmpty(DTO.CustomMessage))
                UnityEngine.Debug.Log($"Custom message in 'HandleIncomingMessage': {DTO.CustomMessage}");

            // RETURNED IMAGE FULL PATH
            if (!String.IsNullOrEmpty(DTO.ImageFullPath))
                ChartImageGenerated?.Invoke(DTO.ImageFullPath);
        }
    }
}