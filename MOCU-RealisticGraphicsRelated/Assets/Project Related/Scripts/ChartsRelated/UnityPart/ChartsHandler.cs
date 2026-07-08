using System.IO;
using UnityEngine;
using UnityEngine.InputSystem.XR;

using InterprocessCommunication;
using MoogModule;
using System;
using System.Collections.Generic;
using System.Linq;


namespace ChartsModule
{
    public class ChartsHandler : ManagedMonoBehaviour
    {
        // todo: take from config
        // if you need to specify a specific path. Otherwise, it will save it to a temporary storage, like 'AppData'
        private string _outputPath = @"C:\Users\Levael\Downloads";


        private ChartsHostSideBridge _daemon;

        // unity components
        private DaemonsHandler _daemonsHandler;
        private DebugTabHandler _debugTabHandler;
        private MoogHandler _moogHandler;

        public Action<Texture2D> StaticChartUpdated;

        public override void ManagedAwake()
        {
            _daemonsHandler = GetComponent<DaemonsHandler>();
            _debugTabHandler = GetComponent<DebugTabHandler>();
            _moogHandler = GetComponent<MoogHandler>();

            _moogHandler.GotFeedback += InteractiveChart;

            /*_debugTabHandler.testBtn1Clicked += (eventObj) => Engage();     // test
            _debugTabHandler.testBtn2Clicked += (eventObj) => TestMethod(); // test*/
        }

        public override void ManagedStart()
        {
            var daemonWrapper = _daemonsHandler.GetDaemon(DaemonType.Charts);
            var communicator = daemonWrapper.Communicator;

            _daemon = new ChartsHostSideBridge(communicator);
            _daemon.ChartImageGenerated += GotStaticChart;
            //_daemon.Test();

            //communicator.ConnectionEstablished += message => stateTracker.UpdateSubStatus(Moog_ModuleSubStatuses.Communicator, SubStatusState.Complete);
            //communicator.ConnectionBroked += message => stateTracker.UpdateSubStatus(Moog_ModuleSubStatuses.Communicator, SubStatusState.Failed);

            //daemonWrapper.Start();                    // <----------
        }

        // .............................

        public void StaticChart(ChartData parameters)
        {
            _daemon.GenerateChartAsImage(parameters);
        }

        public void StaticChart(MoogFeedback moogFeedback)
        {
            StaticChart(ParseMoogFeedback(moogFeedback));
        }

        public void InteractiveChart(ChartData parameters)
        {
            _daemon.GenerateChartAsForm(parameters);
        }

        public void InteractiveChart(MoogFeedback moogFeedback)
        {
            InteractiveChart(ParseMoogFeedback(moogFeedback));
        }

        // .............................

        private void GotStaticChart(string pathToImage)
        {
            try
            {
                StaticChartUpdated?.Invoke(ParsePngToTexture2D(pathToImage));
            }
            catch (Exception ex)
            {
                Debug.Log($"Couldn't 'ParsePngToTexture2D' in 'GotStaticChart' in 'ChartsHandler': {ex}");
            }
        }

        private ChartData ParseMoogFeedback(MoogFeedback feedback)
        {
            if (!feedback.Commands.Any() && !feedback.Responses.Any())
            {
                Debug.Log($"No data to present");
                return null;
            }

            // Находим самую раннюю временную метку
            var baseTime = feedback.Commands.Select(c => c.timestamp)
                .Concat(feedback.Responses.Select(r => r.timestamp))
                .ToList()
                .Min();

            // Функция для преобразования DateTime в миллисекунды с базового времени
            Func<DateTime, double> toMs = dt => (dt - baseTime).TotalMilliseconds;

            // Создаем серию для команд
            var commandPoints = feedback.Commands.Select(c => new PointData
            {
                X = toMs(c.timestamp),
                Y = c.position.Surge
            }).ToList();

            var commandSeries = new SeriesData
            {
                Series = commandPoints,
                Title = "Commands",
                ConnectPoints = true,
                Color = "#0000FF", // Синий
                PointSize = 5,
                LineSize = 1
            };

            // Создаем серию для ответов
            var responsePoints = feedback.Responses.Select(r => new PointData
            {
                X = toMs(r.timestamp),
                Y = r.feedback.Position.Surge
            }).ToList();

            var responseSeries = new SeriesData
            {
                Series = responsePoints,
                Title = "Responses",
                ConnectPoints = false,
                Color = "#FF0000", // Красный
                PointSize = 5
            };

            // Создаем ChartData
            var chartData = new ChartData
            {
                Series = new List<SeriesData> { commandSeries, responseSeries },
                Type = ChartType.Displacement,
                Title = "Surge Displacement over Time",
                XLabel = "Time (ms)",
                YLabel = "Surge (m)",
                DoShowLegend = true,
                Width = 800,
                Height = 600,
                BackgroundColor = "#2F2F2F"
            };

            return chartData;
        }

        private Texture2D ParsePngToTexture2D(string fullPathToImage)
        {
            // Read all file bytes
            byte[] pngBytes = File.ReadAllBytes(fullPathToImage);

            // Create a placeholder texture. Size (2×2) will be replaced by the real one.
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);

            // Decode PNG into the texture. 'true' is for memory saving (optimization only)
            texture.LoadImage(pngBytes, true);

            return texture;
        }
    }
}