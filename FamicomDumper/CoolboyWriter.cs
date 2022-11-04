using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace com.clusterrr.Famicom
{
    public static class CoolboyWriter
    {
        const int BANK_SIZE = 0x4000;

        public static byte DetectVersion(IFamicomDumperConnectionExt dumper)
        {
            byte version;
            Console.Write("Detecting COOLBOY version... ");
            // 0th CHR bank using both methods
            dumper.WriteCpu(0x5000, 0, 0, 0, 0x10);
            dumper.WriteCpu(0x6000, 0, 0, 0, 0x10);
            // Writing 0
            dumper.WritePpu(0x0000, 0);
            // First CHR bank using both methods
            dumper.WriteCpu(0x5000, 0, 0, 1, 0x10);
            dumper.WriteCpu(0x6000, 0, 0, 1, 0x10);
            // Writing 1
            dumper.WritePpu(0x0000, 1);
            // 0th bank using first method
            dumper.WriteCpu(0x6000, 0, 0, 0, 0x10);
            byte v6000 = dumper.ReadPpu(0x0000);
            // return
            dumper.WriteCpu(0x6000, 0, 0, 1, 0x10);
            // 0th bank using second method
            dumper.WriteCpu(0x5000, 0, 0, 0, 0x10);
            byte v5000 = dumper.ReadPpu(0x0000);

            if (v6000 == 0 && v5000 == 1)
                version = 1;
            else if (v6000 == 1 && v5000 == 0)
                version = 2;
            else throw new IOException("Can't detect COOLBOY version");
            Console.WriteLine($"Version: {version}");
            return version;
        }

        public static void PrintFlashInfo(IFamicomDumperConnectionExt dumper)
        {
            Program.Reset(dumper);
            var version = DetectVersion(dumper);
            var CoolboyReg = (ushort)(version == 2 ? 0x5000 : 0x6000);
            int bank = 0;
            byte r0 = (byte)(((bank >> 3) & 0x07) // 5, 4, 3 bits
                | (((bank >> 9) & 0x03) << 4) // 10, 9 bits
                | (1 << 6)); // resets 4th mask bit
            byte r1 = (byte)((((bank >> 7) & 0x03) << 2) // 8, 7
                | (((bank >> 6) & 1) << 4) // 6
                | (1 << 7)); // resets 5th mask bit
            byte r2 = 0;
            byte r3 = (byte)((1 << 4) // NROM mode
                | ((bank & 7) << 1)); // 2, 1, 0 bits
            dumper.WriteCpu(CoolboyReg, r0, r1, r2, r3);
            var cfi = FlashHelper.GetCFIInfo(dumper);
            FlashHelper.PrintCFIInfo(cfi);
            FlashHelper.LockBitsCheckPrint(dumper);
            FlashHelper.PPBLockBitCheckPrint(dumper);
        }

        public static void Write(IFamicomDumperConnectionExt dumper, string fileName, IEnumerable<int> badSectors, bool silent, bool needCheck = false, bool writePBBs = false, bool ignoreBadSectors = false, bool coolboyGpioMode = false)
        {
            byte[] PRG;
            if (Path.GetExtension(fileName).ToLower() == ".bin")
            {
                PRG = File.ReadAllBytes(fileName);
            }
            else
            {
                try
                {
                    var nesFile = new NesFile(fileName);
                    PRG = nesFile.PRG.ToArray();
                }
                catch
                {
                    var nesFile = new UnifFile(fileName);
                    PRG = nesFile["PRG0"].ToArray();
                }
            }

            int banks = PRG.Length / BANK_SIZE;

            Program.Reset(dumper);
            var version = DetectVersion(dumper);
            var coolboyReg = (ushort)(version == 2 ? 0x5000 : 0x6000);
            FlashHelper.ResetFlash(dumper);
            var cfi = FlashHelper.GetCFIInfo(dumper);
            Console.WriteLine($"Device size: {cfi.DeviceSize / 1024 / 1024} MByte / {cfi.DeviceSize / 1024 / 1024 * 8} Mbit");
            Console.WriteLine($"Maximum number of bytes in multi-byte program: {cfi.MaximumNumberOfBytesInMultiProgram}");
            if (dumper.ProtocolVersion >= 3)
                dumper.SetMaximumNumberOfBytesInMultiProgram(cfi.MaximumNumberOfBytesInMultiProgram);
            if (PRG.Length > cfi.DeviceSize)
                throw new ArgumentOutOfRangeException("PRG.Length", "This ROM is too big for this cartridge");
            try
            {
                PPBClear(dumper, coolboyReg);
            }
            catch (Exception ex)
            {
                if (!silent) Program.PlayErrorSound();
                Console.WriteLine($"ERROR! {ex.Message}. Lets continue anyway.");
            }

            var writeStartTime = DateTime.Now;
            var lastSectorTime = DateTime.Now;
            var timeEstimated = new TimeSpan();
            int totalErrorCount = 0;
            int currentErrorCount = 0;
            var newBadSectorsList = new List<int>(badSectors);
            bool sectorContainsData = false;
            for (int bank = 0; bank < banks; bank++)
            {
                while (badSectors.Contains(bank / 8) || newBadSectorsList.Contains(bank / 8)) bank += 8; // bad sector :(
                try
                {
                    byte r0 = (byte)(((bank >> 3) & 0x07) // 5, 4, 3 bits
                        | (((bank >> 9) & 0x03) << 4) // 10, 9 bits
                        | (1 << 6)); // resets 4th mask bit
                    byte r1 = (byte)((((bank >> 7) & 0x03) << 2) // 8, 7
                        | (((bank >> 6) & 1) << 4) // 6
                        | (1 << 7)); // resets 5th mask bit
                    byte r2 = 0;
                    byte r3 = (byte)((1 << 4) // NROM mode
                        | ((bank & 7) << 1)); // 2, 1, 0 bits
                    if (coolboyGpioMode) dumper.SetCoolboyGpioMode(false);
                    dumper.WriteCpu(coolboyReg, r0, r1, r2, r3);
                    if (coolboyGpioMode) dumper.SetCoolboyGpioMode(true);

                    var data = new byte[BANK_SIZE];
                    int pos = bank * BANK_SIZE;
                    if (pos % (128 * 1024) == 0)
                    {
                        timeEstimated = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (banks - bank) / 8);
                        timeEstimated = timeEstimated.Add(DateTime.Now - writeStartTime);
                        lastSectorTime = DateTime.Now;
                        Console.Write($"Erasing sector #{bank / 8}... ");
                        dumper.EraseFlashSector();
                        sectorContainsData = false;
                        Console.WriteLine("OK");
                    }
                    Array.Copy(PRG, pos, data, 0, data.Length);
                    var timePassed = DateTime.Now - writeStartTime;
                    Console.Write($"Writing bank #{bank}/{banks} ({100 * bank / banks}%, {timePassed.Hours:D2}:{timePassed.Minutes:D2}:{timePassed.Seconds:D2}/{timeEstimated.Hours:D2}:{timeEstimated.Minutes:D2}:{timeEstimated.Seconds:D2})... ");
                    dumper.WriteFlash(0x0000, data);
                    sectorContainsData |= data.Where(b => b != 0xFF).Any();
                    Console.WriteLine("OK");
                    if ((bank % 8 == 7) || (bank == banks - 1)) // After last bank in sector
                    {
                        if (writePBBs && sectorContainsData)
                            FlashHelper.PPBSet(dumper);
                        currentErrorCount = 0;
                    }
                }
                catch (Exception ex)
                {
                    totalErrorCount++;
                    currentErrorCount++;
                    Console.WriteLine($"Error {ex.GetType()}: {ex.Message}");
                    if (!silent) Program.PlayErrorSound();
                    if (currentErrorCount >= 5)
                    {
                        if (!ignoreBadSectors)
                            throw;
                        else
                        {
                            newBadSectorsList.Add(bank / 8);
                            currentErrorCount = 0;
                            Console.WriteLine($"Lets skip sector #{bank / 8}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Lets try again");
                    }
                    bank = (bank & ~7) - 1;
                    Program.Reset(dumper);
                    FlashHelper.ResetFlash(dumper);
                    continue;
                }
            }
            if (coolboyGpioMode) dumper.SetCoolboyGpioMode(false);

            if (totalErrorCount > 0)
                Console.WriteLine($"Write error count: {totalErrorCount}");
            if (newBadSectorsList.Any())
                Console.WriteLine($"Can't write sectors: {string.Join(", ", newBadSectorsList.OrderBy(s => s))}");

            var wrongCrcSectorsList = new List<int>();
            if (needCheck)
            {
                Console.WriteLine("Starting verification process");
                Program.Reset(dumper);

                var readStartTime = DateTime.Now;
                lastSectorTime = DateTime.Now;
                timeEstimated = new TimeSpan();
                for (int bank = 0; bank < banks; bank++)
                {
                    while (badSectors.Contains(bank / 8)) bank += 8; // bad sector :(
                    byte r0 = (byte)(((bank >> 3) & 0x07) // 5, 4, 3 bits
                        | (((bank >> 9) & 0x03) << 4) // 10, 9 bits
                        | (1 << 6)); // resets 4th mask bit
                    byte r1 = (byte)((((bank >> 7) & 0x03) << 2) // 8, 7
                        | (((bank >> 6) & 1) << 4) // 6
                        | (1 << 7)); // resets 5th mask bit
                    byte r2 = 0;
                    byte r3 = (byte)((1 << 4) // NROM mode
                        | ((bank & 7) << 1)); // 2, 1, 0 bits
                    dumper.WriteCpu(coolboyReg, r0, r1, r2, r3);

                    int pos = bank * BANK_SIZE;
                    if (pos % (128 * 1024) == 0)
                    {
                        timeEstimated = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (banks - bank) / 8);
                        timeEstimated = timeEstimated.Add(DateTime.Now - readStartTime);
                        lastSectorTime = DateTime.Now;
                    }
                    ushort crc = Crc16Calculator.CalculateCRC16(PRG, pos, BANK_SIZE);
                    var timePassed = DateTime.Now - readStartTime;
                    Console.Write($"Reading CRC of bank #{bank}/{banks} ({100 * bank / banks}%, {timePassed.Hours:D2}:{timePassed.Minutes:D2}:{timePassed.Seconds:D2}/{timeEstimated.Hours:D2}:{timeEstimated.Minutes:D2}:{timeEstimated.Seconds:D2})... ");
                    var crcr = dumper.ReadCpuCrc(0x8000, BANK_SIZE);
                    if (crcr != crc)
                    {
                        Console.WriteLine($"Verification failed: {crcr:X4} != {crc:X4}");
                        if (!silent) Program.PlayErrorSound();
                        wrongCrcSectorsList.Add(bank / 8);
                    }
                    else
                        Console.WriteLine($"OK (CRC = {crcr:X4})");
                }
                if (totalErrorCount > 0)
                    Console.WriteLine($"Write error count: {totalErrorCount}");
                if (newBadSectorsList.Any())
                    Console.WriteLine($"Can't write sectors: {string.Join(", ", newBadSectorsList.OrderBy(s => s))}");
                if (wrongCrcSectorsList.Any())
                    Console.WriteLine($"Sectors with wrong CRC: {string.Join(", ", wrongCrcSectorsList.Distinct().OrderBy(s => s))}");
            }

            if (newBadSectorsList.Any() || wrongCrcSectorsList.Any())
                throw new IOException("Cartridge is not writed correctly");
        }

        public static void PPBClear(IFamicomDumperConnectionExt dumper, ushort coolboyReg)
        {
            // Sector 0
            int bank = 0;
            byte r0 = (byte)(((bank >> 3) & 0x07) // 5, 4, 3 bits
                | (((bank >> 9) & 0x03) << 4) // 10, 9 bits
                | (1 << 6)); // resets 4th mask bit
            byte r1 = (byte)((((bank >> 7) & 0x03) << 2) // 8, 7
                | (((bank >> 6) & 1) << 4) // 6
                | (1 << 7)); // resets 5th mask bit
            byte r2 = 0;
            byte r3 = (byte)((1 << 4) // NROM mode
                | ((bank & 7) << 1)); // 2, 1, 0 bits
            dumper.WriteCpu(coolboyReg, r0, r1, r2, r3);

            FlashHelper.PPBClear(dumper);
        }
    }
}
