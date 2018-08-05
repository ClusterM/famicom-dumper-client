using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cluster.Famicom
{
    static class CommonHelper
    {
        public static int GetFlashSize(FamicomDumperConnection dumper)
        {
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0x90);
            var autoselect = dumper.ReadCpu(0x8000, 0x100);
            byte manufacturer = autoselect[0];
            var device = new byte[] { autoselect[2], autoselect[0x1C], autoselect[0x1E] };
            dumper.WriteCpu(0x8000, 0xF0); // Reset            
            Console.WriteLine("Chip manufacturer ID: {0:X2}", manufacturer);
            Console.WriteLine("Chip device ID: {0:X2} {1:X2} {2:X2}", device[0], device[1], device[2]);
            string deviceName;
            int size;
            switch ((UInt32)((device[0] << 16) | (device[1] << 8) | (device[2])))
            {
                case 0x7E2801:
                    deviceName = "S29GL01GP";
                    size = 128 * 1024 * 1024;
                    break;
                case 0x7E2301:
                    deviceName = "S29GL512GP";
                    size = 64 * 1024 * 1024;
                    break;
                case 0x7E2201:
                    deviceName = "S29GL256GP";
                    size = 32 * 1024 * 1024;
                    break;
                case 0x7E2101:
                    deviceName = "S29GL128GP";
                    size = 16 * 1024 * 1024;
                    break;
                default:
                    throw new Exception("Unknown device ID");
            }
            Console.WriteLine("Device name: {0}", deviceName);
            Console.WriteLine("Device size: {0} MBytes / {1} Mbit", size / 1024 / 1024, size / 1024 / 1024 * 8);
            return size;
        }
    }
}
