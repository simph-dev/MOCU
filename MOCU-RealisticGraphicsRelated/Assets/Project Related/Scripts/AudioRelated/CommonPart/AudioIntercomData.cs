using System;
using System.Collections.Generic;
using System.Linq;


namespace AudioModule
{
    public class AudioIntercomData
    {
        public IEnumerable<Guid> fromDevices { get; set; } = Enumerable.Empty<Guid>();
        public IEnumerable<Guid> toDevices { get; set; } = Enumerable.Empty<Guid>();
        public bool isOn { get; set; } = false;
        public Guid id { get; set; } = Guid.Empty;
    }
}