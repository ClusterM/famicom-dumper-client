using com.clusterrr.Famicom.DumperConnection;
using System;
using System.IO;

namespace com.clusterrr.Famicom.Dumper
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

        public static void ResetFlash(IFamicomDumperConnection dumper)
        {
            // Exit command set entry if any
            dumper.WriteCpu(0x8000, 0x90);
            dumper.WriteCpu(0x8000, 0x00);
            // Reset
            dumper.WriteCpu(0x8000, 0xF0);
        }

        public static CFIInfo GetCFIInfo(IFamicomDumperConnection dumper)
        {
            try
            {
                ResetFlash(dumper);
                dumper.WriteCpu(0x8AAA, 0x98); // CFI mode
                var cfiRaw = dumper.ReadCpu(0x8000, 0x100);
                if (cfiRaw[0x20] != 0x51 || cfiRaw[0x22] != 0x52 || cfiRaw[0x24] != 0x59)
                {
                    throw new IOException("Can't enter CFI mode. Invalid flash memory? Broken cartridge? Is it inserted?");
                }
                var cfi = new CFIInfo(cfiRaw, CFIInfo.ParseMode.Every2Bytes);
                return cfi;
            }
            finally
            {
                dumper.WriteCpu(0x8000, 0xF0);
            }
        }

        public static void PrintCFIInfo(CFIInfo cfi)
        {
            Console.WriteLine($"Primary Algorithm Command Set and Control Interface ID Code: {cfi.PrimaryAlgorithmCommandSet:X4}h");
            Console.WriteLine($"Alternative Algorithm Command Set and Control Interface ID Code: {cfi.AlternativeAlgorithmCommandSet:X4}h");
            Console.WriteLine($"Vcc Logic Supply Minimum Program / Erase voltage: {cfi.VccLogicSupplyMinimumProgramErase:F1}v");
            Console.WriteLine($"Vcc Logic Supply Maximum Program / Erase voltage: {cfi.VccLogicSupplyMaximumProgramErase:F1}v");
            Console.WriteLine($"Vpp [Programming] Supply Minimum Program / Erase voltage: {cfi.VppSupplyMinimumProgramErasevoltage:F1}v");
            Console.WriteLine($"Vpp [Programming] Supply Maximum Program / Erase voltage: {cfi.VppSupplyMaximumProgramErasevoltage:F1}v");
            Console.WriteLine($"Typical timeout per single byte/word/D-word program: {cfi.TypicalTimeoutPerSingleProgram}us");
            Console.WriteLine($"Typical timeout for maximum-size multi-byte program: {cfi.TypicalTimeoutForMaximumSizeMultiByteProgram}us");
            Console.WriteLine($"Typical timeout per individual block erase: {cfi.TypicalTimeoutPerIndividualBlockErase}ms");
            Console.WriteLine($"Typical timeout for full chip erase: {cfi.TypicalTimeoutForFullChipErase}ms");
            Console.WriteLine($"Maximum timeout per single byte/word/D-word program: {cfi.MaximumTimeoutPerSingleProgram}us");
            Console.WriteLine($"Maximum timeout for maximum-size multi-byte program: {cfi.MaximumTimeoutForMaximumSizeMultiByteProgram}us");
            Console.WriteLine($"Maximum timeout per individual block erase: {cfi.MaximumTimeoutPerIndividualBlockErase}ms");
            Console.WriteLine($"Maximum timeout for full chip erase: {cfi.MaximumTimeoutForFullChipErase}ms");
            Console.WriteLine($"Device size: {cfi.DeviceSize / 1024 / 1024} MByte / {cfi.DeviceSize / 1024 / 1024 * 8} Mbit");
            Console.WriteLine($"Flash device interface: {cfi.FlashDeviceInterfaceCodeDescription.ToString().Replace("_", " ")}");
            Console.WriteLine($"Maximum number of bytes in multi-byte program: {cfi.MaximumNumberOfBytesInMultiProgram}");
            for (int eraseBlockRegion = 0; eraseBlockRegion < cfi.EraseBlockRegionsInfo.Length; eraseBlockRegion++)
            {
                Console.WriteLine($"Erase block region #{eraseBlockRegion + 1}:");
                Console.WriteLine($" - Sectors size: {cfi.EraseBlockRegionsInfo[eraseBlockRegion].SizeOfBlocks} Bytes");
                Console.WriteLine($" - Sectors count: {cfi.EraseBlockRegionsInfo[eraseBlockRegion].NumberOfBlocks}");
            }
        }

        public static void PasswordProgramm(FamicomDumperLocal dumper, byte[] password)
        {
            if (password.Length != 8)
                throw new InvalidDataException("Invalid password length");
            Console.Write("Programming password... ");
            // Password Protection Set Entry
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0x60);
            try
            {
                for (byte i = 0; i < password.Length; i++)
                {
                    dumper.WriteCpu(0x8000, 0xA0);
                    dumper.WriteCpu((ushort)(0x8000 + i), password[i]);
                }
                var verify = dumper.ReadCpu(0x8000, 8);
                for (byte i = 0; i < password.Length; i++)
                    if (password[i] != verify[i])
                        throw new InvalidDataException("Password verification failed");
            }
            finally
            {
                ResetFlash(dumper);
            }
            Console.WriteLine("OK");

            Console.Write("Programming lock register... ");
            // Lock Register Set Entry
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0x40);
            try
            {
                // Bits Program
                dumper.WriteCpu(0x8000, 0xA0);
                dumper.WriteCpu(0x8000, (byte)(1 << 2) ^ 0xFF); // password protection
                var r = dumper.ReadCpu(0x8000);
                if ((r & 7) != 3)
                    throw new InvalidDataException("Lock bit verification failed");
            }
            finally
            {
                ResetFlash(dumper);
            }
            Console.WriteLine("OK");
        }

        public static void PasswordUnlock(FamicomDumperLocal dumper, byte[] password)
        {
            try
            {
                if (password.Length != 8)
                    throw new InvalidDataException("Invalid password length");
                Console.Write("Unlocking password... ");
                // Password Protection Set Entry
                dumper.WriteCpu(0x8AAA, 0xAA);
                dumper.WriteCpu(0x8555, 0x55);
                dumper.WriteCpu(0x8AAA, 0x60);
                // Password unlock
                dumper.WriteCpu(0x8000, 0x25);
                dumper.WriteCpu(0x8000, 0x03);
                for (byte i = 0; i < password.Length; i++)
                    dumper.WriteCpu((ushort)(0x8000 + i), password[i]);
                dumper.WriteCpu(0x8000, 0x29);
                Console.WriteLine("OK");
            }
            finally
            {
                ResetFlash(dumper);
            }
        }

        public static void LockBitsCheckPrint(IFamicomDumperConnection dumper)
        {
            try
            {
                // Lock Register Set Entry
                dumper.WriteCpu(0x8AAA, 0xAA);
                dumper.WriteCpu(0x8555, 0x55);
                dumper.WriteCpu(0x8AAA, 0x40);
                var lockRegister = dumper.ReadCpu(0x8000);
                if ((lockRegister & 1) == 0)
                    Console.WriteLine("WARNING: Secured Silicon Sector Protection Bit is set!");
                if ((lockRegister & 2) == 0)
                    Console.WriteLine("WARNING: Persistent Protection Mode Lock Bit is set!");
                if ((lockRegister & 4) == 0)
                    Console.WriteLine("WARNING: Password Protection Mode Lock Bit is set!");
            }
            finally
            {
                ResetFlash(dumper);
            }
        }

        public static void PPBLockBitCheckPrint(IFamicomDumperConnection dumper)
        {
            try
            {
                // PPB Lock Command Set Entry
                dumper.WriteCpu(0x8AAA, 0xAA);
                dumper.WriteCpu(0x8555, 0x55);
                dumper.WriteCpu(0x8AAA, 0x50);
                var ppbLockStatus = dumper.ReadCpu(0x8000);
                if (ppbLockStatus == 0)
                    Console.WriteLine("WARNING: PPB Lock Bit is set!");
            }
            finally
            {
                ResetFlash(dumper);
            }
        }

        public static byte PPBRead(IFamicomDumperConnection dumper)
        {
            try
            {
                // PPB Command Set Entry
                dumper.WriteCpu(0x8AAA, 0xAA);
                dumper.WriteCpu(0x8555, 0x55);
                dumper.WriteCpu(0x8AAA, 0xC0);
                // PPB Status Read
                return dumper.ReadCpu(0x8000);
            }
            finally
            {
                ResetFlash(dumper);
            }
        }

        public static void PPBSet(IFamicomDumperConnection dumper)
        {
            Console.Write("Writing PPB for sector... ");
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
                DateTime startTime = DateTime.Now;
                while (true)
                {
                    byte b = dumper.ReadCpu(0x8000);
                    if (b == 0x00)
                        break;
                    if ((DateTime.Now - startTime).TotalMilliseconds >= 1500)
                        throw new IOException("PPB write failed");
                }
            }
            finally
            {
                ResetFlash(dumper);
            }
            Console.WriteLine("OK");
        }

        public static void PPBClear(IFamicomDumperConnection dumper)
        {
            LockBitsCheckPrint(dumper);
            PPBLockBitCheckPrint(dumper);
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
                DateTime startTime = DateTime.Now;
                while (true)
                {
                    byte b = dumper.ReadCpu(0x8000);
                    if (b == 0x01)
                        break;
                    if ((DateTime.Now - startTime).TotalMilliseconds >= 1500)
                        throw new IOException("PPB clear failed");
                }
            }
            finally
            {
                ResetFlash(dumper);
            }
            Console.WriteLine("OK");
        }
    }
}
