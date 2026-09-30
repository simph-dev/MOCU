using CustomUxmlElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace CustomUxmlElements
{
    [UxmlElement]
    public partial class DeviceBoxPanel_Microphones : DeviceBoxPanel
    {
        public DeviceBoxPanel_Microphones()
        {
            this.Label = "Microphones";
        }
    }
}