using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace AudioModule
{
    public class AudioDeviceData
    {
        public Guid Id { get; set; }
        public float Volume { get; set; }   // from 0 to 100
        public string Name { get; set; }
        public AudioDeviceType Type { get; set; }
        public AudioDeviceConnectionStatus ConnectionStatus { get; set; }
    }
}