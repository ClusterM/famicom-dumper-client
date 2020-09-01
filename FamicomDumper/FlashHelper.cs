using com.clusterrr.Famicom.DumperConnection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace com.clusterrr.Famicom
{
    static class FlashHelper
    {
        private enum FlashDeviceInterface
        {
            x8_only = 0x0000,
            x16_only = 0x0001,
            x8_and_x16_via_byte_pin = 0x0002,
            x32_only = 0x0003,
            x8_and_x16_via_word_pin = 0x0004,
        }

        public static void ResetFlash(FamicomDumperConnection dumper)
        {
            // Exit command set entry if any
            dumper.WriteCpu(0x8001, 0x90); 
            dumper.WriteCpu(0x8001, 0x00);
            // Reset
            dumper.WriteCpu(0x8000, 0xF0);
        }

        public static int GetFlashSizePrintInfo(FamicomDumperConnection dumper)
        {
            dumper.WriteCpu(0x8AAA, 0x98); // CFI mode
            var cfi = dumper.ReadCpu(0x8000, 0x200);
            dumper.WriteCpu(0x8000, 0xF0); // Reset
            if (cfi[0x20] != 0x51 || cfi[0x22] != 0x52 || cfi[0x24] != 0x59)
            {
                throw new IOException("Can't enter CFI mode. Invalid flash memory? Broken cartridge? Is it inserted?");
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

        public static void LockBitsCheck(FamicomDumperConnection dumper)
        {
            // Lock Register Set Entry
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0x40);
            var lockRegister = dumper.ReadCpu(0x8000, 1)[0];
            if ((lockRegister & 1) == 0)
                Console.WriteLine("WARNING: Secured Silicon Sector Protection Bit is set!");
            if ((lockRegister & 2) == 0)
                Console.WriteLine("WARNING: Persistent Protection Mode Lock Bit is set!");
            if ((lockRegister & 4) == 0)
                Console.WriteLine("WARNING: Password Protection Mode Lock Bit is set!");
            // PPB Lock Command Set Exit 
            dumper.WriteCpu(0x8000, 0x90);
            dumper.WriteCpu(0x8000, 0x00);
        }

        public static void PPBLockBitCheck(FamicomDumperConnection dumper)
        {
            // PPB Lock Command Set Entry
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0x50);
            var ppbLockStatus = dumper.ReadCpu(0x8000, 1)[0];
            if (ppbLockStatus == 0)
                Console.WriteLine("WARNING: PPB Lock Bit is set!");
            // PPB Lock Command Set Exit 
            dumper.WriteCpu(0x8000, 0x90);
            dumper.WriteCpu(0x8000, 0x00);
        }

        public static byte PPBRead(FamicomDumperConnection dumper)
        {
            // PPB Command Set Entry
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0xC0);
            // PPB Status Read
            var result = dumper.ReadCpu(0x8000, 1)[0];
            // PPB Command Set Exit
            dumper.WriteCpu(0x8000, 0x90);
            dumper.WriteCpu(0x8000, 0x00);
            return result;
        }

        public static void PPBSet(FamicomDumperConnection dumper)
        {
            // PPB Command Set Entry
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0xC0);
            // PPB Program
            dumper.WriteCpu(0x8000, 0xA0);
            dumper.WriteCpu(0x8000, 0x00);
            // Check
            try
            {
                while (true)
                {
                    var b0 = dumper.ReadCpu(0x8000, 1)[0];
                    var b1 = dumper.ReadCpu(0x8000, 1)[0];
                    var tg = b0 ^ b1;
                    if ((tg & (1 << 6)) == 0) // DQ6 = not toggle
                    {
                        break;
                    }
                    else// DQ6 = toggle
                    {
                        if ((b0 & (1 << 5)) != 0) // DQ5 = 1
                        {
                            b0 = dumper.ReadCpu(0x8000, 1)[0];
                            b1 = dumper.ReadCpu(0x8000, 1)[0];
                            tg = b0 ^ b1;
                            if ((tg & (1 << 6)) == 0) // DQ6 = not toggle
                                break;
                            else
                                throw new IOException("PPB write failed (DQ5 is set)");
                        }
                    }
                }
                var r = dumper.ReadCpu(0x8000, 1)[0];
                if ((r & 1) != 0) // DQ0 = 1
                    throw new IOException("PPB write failed (DQ0 is not set)");
            }
            finally
            {
                // PPB Command Set Exit
                dumper.WriteCpu(0x8000, 0x90);
                dumper.WriteCpu(0x8000, 0x00);
            }
            Console.WriteLine("OK");
        }

        public static void PPBErase(FamicomDumperConnection dumper)
        {
            PPBLockBitCheck(dumper);
            Console.Write($"Erasing all PBBs... ");
            // PPB Command Set Entry
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0xC0);
            // All PPB Erase
            dumper.WriteCpu(0x8000, 0x80);
            dumper.WriteCpu(0x8000, 0x30);
            // Check
            try
            {
                while (true)
                {
                    var b0 = dumper.ReadCpu(0x8000, 1)[0];
                    var b1 = dumper.ReadCpu(0x8000, 1)[0];
                    var tg = b0 ^ b1;
                    if ((tg & (1 << 6)) == 0) // DQ6 = not toggle
                    {
                        break;
                    }
                    else// DQ6 = toggle
                    {
                        if ((b0 & (1 << 5)) != 0) // DQ5 = 1
                        {
                            b0 = dumper.ReadCpu(0x8000, 1)[0];
                            b1 = dumper.ReadCpu(0x8000, 1)[0];
                            tg = b0 ^ b1;
                            if ((tg & (1 << 6)) == 0) // DQ6 = not toggle
                                break;
                            else
                                throw new IOException("PPB erase failed (DQ5 is set)");
                        }
                    }
                }
                var r = dumper.ReadCpu(0x8000, 1)[0];
                if ((r & 1) != 1) // DQ0 = 0
                    throw new IOException("PPB erase failed (DQ0 is not set)");
            }
            finally
            {
                // PPB Command Set Exit
                dumper.WriteCpu(0x8000, 0x90);
                dumper.WriteCpu(0x8000, 0x00);
            }
            Console.WriteLine("OK");
        }
    }
}
