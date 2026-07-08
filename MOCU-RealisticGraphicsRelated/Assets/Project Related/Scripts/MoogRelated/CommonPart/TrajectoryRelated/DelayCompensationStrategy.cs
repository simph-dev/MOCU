using System.ComponentModel;


namespace MoogModule
{
    public enum DelayCompensationStrategy
    {
        [Description("Not choosen")]
        None,

        [Description("Ignore delay (shifted)")]
        Ignore,

        [Description("Repair delay (by new CDF)")]
        Repair,

        [Description("Jump forward instantly")]
        Jump
    }
}