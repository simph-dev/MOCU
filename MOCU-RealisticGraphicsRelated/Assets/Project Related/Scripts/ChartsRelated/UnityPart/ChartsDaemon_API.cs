using System;

namespace ChartsModule
{
    public interface ChartsDaemon_API
    {
        event Action<string> ChartImageGenerated;

        void GenerateChartAsImage(ChartData chartData) { }
        void GenerateChartAsForm(ChartData chartData) { }
    }
}