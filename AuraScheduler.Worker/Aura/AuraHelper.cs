using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AuraServiceLib;

namespace AuraScheduler.Worker.Aura
{
    internal class AuraHelper
    {
        private IAuraSdk2 _sdk;
        private IAuraSyncDeviceCollection _devices;

        public bool HasControl { get; private set; }

        internal enum AuraDeviceType : uint
        {
            ALL = 0,
            MB_RGB = 0x10000,
            MB_ADDRESABLE = 0x11000,
            DESKTOP_RGB = 0x12000,
            VGA_RGB = 0x20000,
            DISPLAY_RGB = 0x30000,
            HEADSET_RGB = 0x40000,
            MICROPHONE_RGB = 0x50000,
            EXTERNAL_HARD_DRIVER_RGB = 0x60000,
            EXTERNAL_BLUE_RAY_RGB = 0x61000,
            DRAM_RGB = 0x70000,
            KEYBOARD_RGB = 0x80000,
            NB_KB_RGB = 0x81000,
            NB_KB_4ZONE_RGB = 0x81001,
            MOUSE_RGB = 0x90000,
            CHASSIS_RGB = 0xB0000,
            PROJECTOR_RGB = 0xC0000
        }

        public AuraHelper()
        {
            _sdk = (IAuraSdk2)new AuraSdk();

            // enumerate all devices
            _devices = _sdk.Enumerate((uint)AuraDeviceType.ALL);
        }

        public void TakeControl()
        {
            if (!HasControl)
            {
                // Acquire control
                _sdk.SwitchMode();

                HasControl = true;
            }
        }

        public void SetLightsToColor(Color c)
        {
            // Traverse all devices
            foreach (IAuraSyncDevice dev in _devices)
            {
                // Traverse all LED's
                foreach (IAuraRgbLight light in dev.Lights)
                {
                    // Set all LED's to blue
                    light.Red = c.R;
                    light.Blue = c.B;
                    light.Green = c.G;
                }

                // Apply colors that we have just set
                dev.Apply();
            }
        }

        public void ReleaseControl()
        {
            if (HasControl)
            {
                _sdk.ReleaseControl(0);
                HasControl = false;
            }
        }
    }
}
