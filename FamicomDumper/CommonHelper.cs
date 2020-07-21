using com.clusterrr.Famicom.DumperConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.clusterrr.Famicom
{
    static class CommonHelper
    {
        private enum FlashDeviceInterface
        {
            x8_only = 0x0000,
            x16_only = 0x0001,
            x8_and_x16_via_byte_pin = 0x0002,
            x32_only = 0x0003,
            x8_and_x16_via_word_pin = 0x0004,
        }

        public static int GetFlashSizePrintInfo(FamicomDumperConnection dumper)
        {
            dumper.WriteCpu(0x8000, 0xF0); // Reset
            dumper.WriteCpu(0x8AAA, 0x98); // CFI mode
            var cfi = dumper.ReadCpu(0x8000, 0x200);
            dumper.WriteCpu(0x8000, 0xF0); // Reset
            if (cfi[0x20] != 0x51 || cfi[0x22] != 0x52 || cfi[0x24] != 0x59)
            {
                throw new Exception("Can't enter CFI mode. Invalid flash memory? Broken cartridge? Is it inserted?");
            }
            int size = 1 << cfi[0x27 * 2];
            FlashDeviceInterface flashDeviceInterface = (FlashDeviceInterface)(cfi[0x28 * 2] | (cfi[0x29 * 2] << 8));
            Console.WriteLine("Primary Algorithm Command Set and Control Interface ID Code: {0:X2}{1:X2}h", cfi[0x13 * 2], cfi[0x14 * 2]);
            Console.WriteLine("Vcc Logic Supply Minimum Program / Erase voltage: {0}v", (cfi[0x1B * 2] >> 4) + 0.1 * (cfi[0x1B * 2] & 0x0F));
            Console.WriteLine("Vcc Logic Supply Maximum Program / Erase voltage: {0}v", (cfi[0x1C * 2] >> 4) + 0.1 * (cfi[0x1C * 2] & 0x0F));
            Console.WriteLine("Vpp [Programming] Supply Minimum Program / Erase voltage: {0}v", (cfi[0x1D * 2] >> 4) + 0.1 * (cfi[0x1D * 2] & 0x0F));
            Console.WriteLine("Vpp [Programming] Supply Maximum Program / Erase voltage: {0}v", (cfi[0x1E * 2] >> 4) + 0.1 * (cfi[0x1E * 2] & 0x0F));
            Console.WriteLine("Maximum number of bytes in multi-byte program: {0}", 1 << (cfi[0x2A * 2] | (cfi[0x2B * 2] << 8)));
            Console.WriteLine("Device size: {0} MByte / {1} Mbit", size / 1024 / 1024, size / 1024 / 1024 * 8);
            Console.WriteLine("Flash device interface: {0}", flashDeviceInterface.ToString().Replace("_"," "));            
            return size;
        }
    }
}
