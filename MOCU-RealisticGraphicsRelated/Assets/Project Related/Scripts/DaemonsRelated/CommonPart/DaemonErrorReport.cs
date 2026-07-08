using System;

#nullable enable


namespace DaemonsRelated
{
    public class DaemonErrorReport
    {
        public string message   { get; set; } = String.Empty;
        public bool isFatal     { get; set; } = false;
        public object? payload  { get; set; } = null;
    }
}