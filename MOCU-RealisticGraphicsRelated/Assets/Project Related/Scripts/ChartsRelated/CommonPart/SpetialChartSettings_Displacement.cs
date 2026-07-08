using System;


namespace ChartsModule
{
    public class SpetialChartSettings_Displacement
    {
        public double MaxAcceleration           { get; set; } = double.MaxValue;  // meters per second per second
        public TimeSpan MaxDelay                { get; set; } = TimeSpan.MaxValue;

        public string CommandColor              { get; set; } = "#4169E1";  // RoyalBlue
        public string ResponseColor             { get; set; } = "#2E8B57";  // SeaGreen
        public string StateLabelColor           { get; set; } = "#A0522D";  // Sienna
        public string ErrorLabelColor           { get; set; } = "#000000";  // Black
        public string ExceededAccelerationColor { get; set; } = "#FF8C00";  // DarkOrange
        public string ExceededDelayColor        { get; set; } = "#B22222";  // Firebrick
    }
}