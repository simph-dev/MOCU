using MoogModule.Daemon;
using System;
using System.Collections.Generic;
using System.Linq;


namespace MoogModule
{
    public class MoogFeedback
    {
        public IEnumerable<(DateTime timestamp, DofParameters position)> Commands       { get; set; } = Enumerable.Empty<(DateTime, DofParameters)>();
        public IEnumerable<(DateTime timestamp, MoogRealTimeState feedback)> Responses  { get; set; } = Enumerable.Empty<(DateTime, MoogRealTimeState)>();
    }
}