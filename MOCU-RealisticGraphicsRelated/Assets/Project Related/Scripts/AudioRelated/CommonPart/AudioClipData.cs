using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace AudioModule
{
    public class AudioClipData
    {
        public AudioClipName name { get; set; }
        public float volume { get; set; }
        public string fullFilePath { get; set; }
    }
}