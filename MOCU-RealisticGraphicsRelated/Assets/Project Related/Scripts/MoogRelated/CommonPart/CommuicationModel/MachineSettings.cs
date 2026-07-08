using System;


namespace MoogModule
{
    public class MachineSettings
    {
        public DofParameters StartPosition { get; set; } = default;
        public int MinimalFPS { get; set; } = 0;
        public int DesiredFPS { get; set; } = 0;
        public double MaxAcceleration { get; set; } = 0.0;
        public string HOST_IP { get; set; } = String.Empty;
        public string HOST_PORT { get; set; } = String.Empty;
        public string MBC_IP { get; set; } = String.Empty;
        public string MBC_PORT { get; set; } = String.Empty;
    }
}