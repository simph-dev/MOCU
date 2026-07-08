using CustomUxmlElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace CustomUxmlElements
{
    public class DeviceBoxPanel_Microphones : DeviceBoxPanel
    {
        public new class UxmlFactory : UxmlFactory<DeviceBoxPanel_Microphones, UxmlTraits> { }

        public DeviceBoxPanel_Microphones()
        {
            this.Label = "Microphones";
        }
    }
}