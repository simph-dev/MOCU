namespace ChartsModule
{
    public class PointData
    {
        public double X             { get; set; } = 0;
        public double Y             { get; set; } = 0;
        public string Label         { get; set; } = string.Empty;
        public string TooltipText   { get; set; } = string.Empty;   // seen when hover
    }
}