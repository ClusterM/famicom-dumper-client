using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;

namespace com.clusterrr.Famicom
{
    public static class CoolgirlWriter
    {
        const int BANK_SIZE = 0x8000;

        public static void PrintFlashInfo(IFamicomDumperConnectionExt dumper)
        {
            Program.Reset(dumper);
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
            var cfi = FlashHelper.GetCFIInfo(dumper);
            FlashHelper.PrintCFIInfo(cfi);
            FlashHelper.LockBitsCheckPrint(dumper);
            FlashHelper.PPBLockBitCheckPrint(dumper);
        }

        public static void Write(IFamicomDumperConnectionExt dumper, string fileName, IEnumerable<int> badSectors, bool silent, bool needCheck = false, bool writePBBs = false, bool ignoreBadSectors = false)
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
                    PRG = nesFile.Fields["PRG0"];
                }
            }

            int banks = PRG.Length / BANK_SIZE;

            Program.Reset(dumper);
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
            dumper.WriteCpu(0x5000, 0);
            dumper.WriteCpu(0x5001, 0);
            FlashHelper.ResetFlash(dumper);
            var cfi = FlashHelper.GetCFIInfo(dumper);
            Console.WriteLine($"Device size: {cfi.DeviceSize / 1024 / 1024} MByte / {cfi.DeviceSize / 1024 / 1024 * 8} Mbit");
            Console.WriteLine($"Maximum number of bytes in multi-byte program: {cfi.MaximumNumberOfBytesInMultiProgram}");
            if (dumper.ProtocolVersion >= 3)
                dumper.SetMaximumNumberOfBytesInMultiProgram(cfi.MaximumNumberOfBytesInMultiProgram);
            FlashHelper.LockBitsCheckPrint(dumper);
            if (PRG.Length > cfi.DeviceSize)
                throw new ArgumentOutOfRangeException("PRG.Length", "This ROM is too big for this cartridge");
            try
            {
                PPBClear(dumper);
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
            var newBadSectorsList = new List<int>();
            for (int bank = 0; bank < banks; bank++)
            {
                while (badSectors.Contains(bank / 4) || newBadSectorsList.Contains(bank / 4)) bank += 4; // bad sector :(
                try
                {
                    byte r0 = (byte)(bank >> 7);
                    byte r1 = (byte)(bank << 1);
                    dumper.WriteCpu(0x5000, r0);
                    dumper.WriteCpu(0x5001, r1);

                    var data = new byte[BANK_SIZE];
                    int pos = bank * BANK_SIZE;
                    if (pos % (128 * 1024) == 0)
                    {
                        timeEstimated = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (banks - bank) / 4);
                        timeEstimated = timeEstimated.Add(DateTime.Now - writeStartTime);
                        lastSectorTime = DateTime.Now;
                        Console.Write($"Erasing sector #{bank / 4}... ");
                        dumper.EraseFlashSector();
                        Console.WriteLine("OK");
                    }
                    Array.Copy(PRG, pos, data, 0, data.Length);
                    var timePassed = DateTime.Now - writeStartTime;
                    Console.Write($"Writing bank #{bank}/{banks} ({100 * bank / banks}%, {timePassed.Hours:D2}:{timePassed.Minutes:D2}:{timePassed.Seconds:D2}/{timeEstimated.Hours:D2}:{timeEstimated.Minutes:D2}:{timeEstimated.Seconds:D2})... ");
                    dumper.WriteFlash(0x0000, data);
                    Console.WriteLine("OK");
                    if ((bank % 4 == 3) || (bank == banks - 1)) // After last bank in sector
                    {
                        if (writePBBs)
                            FlashHelper.PPBSet(dumper);
                        currentErrorCount = 0;
                    }
                }
                catch (Exception ex)
                {
                    totalErrorCount++;
                    currentErrorCount++;
                    Console.WriteLine($"ERROR {ex.GetType()}: {ex.Message}");
                    if (!silent) Program.PlayErrorSound();
                    if (currentErrorCount >= 5)
                    {
                        if (!ignoreBadSectors)
                            throw;
                        else
                        {
                            newBadSectorsList.Add(bank / 4);
                            currentErrorCount = 0;
                            Console.WriteLine($"Lets skip sector #{bank / 4}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Lets try again");
                    }
                    bank = (bank & ~3) - 1;
                    Program.Reset(dumper);
                    dumper.WriteCpu(0x5007, 0x04); // enable PRG write
                    dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
                    FlashHelper.ResetFlash(dumper);
                    continue;
                }
            }
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
                dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
                for (int bank = 0; bank < banks; bank++)
                {
                    while (badSectors.Contains(bank / 4)) bank += 4; // bad sector :(
                    byte r0 = (byte)(bank >> 7);
                    byte r1 = (byte)(bank << 1);
                    dumper.WriteCpu(0x5000, r0);
                    dumper.WriteCpu(0x5001, r1);

                    int pos = bank * BANK_SIZE;
                    if (pos % (128 * 1024) == 0)
                    {
                        timeEstimated = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (banks - bank) / 4);
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
                        wrongCrcSectorsList.Add(bank / 4);
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

        public static void PPBClear(IFamicomDumperConnectionExt dumper)
        {
            // enable PRG write
            dumper.WriteCpu(0x5007, 0x04);
            // mask = 32K
            dumper.WriteCpu(0x5002, 0xFE);
            // Sector 0
            dumper.WriteCpu(0x5000, 0);
            dumper.WriteCpu(0x5001, 0);

            FlashHelper.PPBClear(dumper);
        }
    }
}
