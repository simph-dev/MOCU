using System;
using System.Linq;
using System.Collections.Generic;


namespace ChartsModule
{
    public class SeriesData
    {
        public IEnumerable<PointData> Series    { get; set; } = Enumerable.Empty<PointData>();
        public string Title                     { get; set; } = String.Empty;
        public bool ConnectPoints               { get; set; } = true;
        public string Color                     { get; set; } = "#0000FF";  // Hex color for both points and line
        public int PointSize                    { get; set; } = 3;
        public int LineSize                     { get; set; } = 1;
    }
}