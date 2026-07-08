using System;
using System.Linq;
using System.Collections.Generic;


namespace ChartsModule
{
    public class ChartData
    {
        public IEnumerable<SeriesData> Series   { get; set; } = Enumerable.Empty<SeriesData>();
        public ChartType Type                   { get; set; } = ChartType.None;
        public string Title                     { get; set; } = String.Empty;
        public string XLabel                    { get; set; } = "X";
        public string YLabel                    { get; set; } = "Y";
        public bool DoShowLegend                { get; set; } = false;
        public int Width                        { get; set; } = 800;
        public int Height                       { get; set; } = 600;
        public string BackgroundColor           { get; set; } = "#FFFFFF";
    }
}