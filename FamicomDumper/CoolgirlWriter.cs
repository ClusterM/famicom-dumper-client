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

        public static void PrintFlashInfo(IFamicomDumperConnection dumper)
        {
            Program.Reset(dumper);
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
            var cfi = FlashHelper.GetCFIInfo(dumper);
            FlashHelper.PrintCFIInfo(cfi);
            FlashHelper.LockBitsCheckPrint(dumper);
            FlashHelper.PPBLockBitCheckPrint(dumper);
        }

        public static void Write(IFamicomDumperConnection dumper, string fileName, IEnumerable<int> badSectors, bool silent, bool needCheck = false, bool writePBBs = false, bool ignoreBadSectors = false)
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

                    var data = new byte[BANK_SIZE];
                    int pos = bank * BANK_SIZE;
                    if (pos % (128 * 1024) == 0)
                    {
                        timeEstimated = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (banks - bank) / 4);
                        timeEstimated = timeEstimated.Add(DateTime.Now - readStartTime);
                        lastSectorTime = DateTime.Now;
                    }
                    Array.Copy(PRG, pos, data, 0, data.Length);
                    ushort crc = 0;
                    foreach (var a in data)
                    {
                        crc ^= a;
                        for (int i = 0; i < 8; ++i)
                        {
                            if ((crc & 1) != 0)
                                crc = (ushort)((crc >> 1) ^ 0xA001);
                            else
                                crc = (ushort)(crc >> 1);
                        }
                    }
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

        public static void PPBClear(IFamicomDumperConnection dumper)
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

        public static void FindBads(IFamicomDumperConnection dumper, bool silent)
        {
            Program.Reset(dumper);
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
            try
            {
                PPBClear(dumper);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}. Lets try anyway.");
            }
            dumper.WriteCpu(0x5000, 0);
            dumper.WriteCpu(0x5001, 0);
            var cfi = FlashHelper.GetCFIInfo(dumper);
            Console.WriteLine($"Device size: {cfi.DeviceSize / 1024 / 1024} MByte / {cfi.DeviceSize / 1024 / 1024 * 8} Mbit");
            uint banks = cfi.DeviceSize / BANK_SIZE;
            FlashHelper.LockBitsCheckPrint(dumper);

            Console.Write("Erasing sector #0... ");
            dumper.EraseFlashSector();
            Console.WriteLine("OK");
            var data = new byte[BANK_SIZE];
            new Random().NextBytes(data);
            Console.Write("Writing sector #0 for test... ");
            dumper.WriteFlash(0x0000, data);
            Console.WriteLine("OK");
            Console.Write("Reading sector #0 for test... ");
            var datar = dumper.ReadCpu(0x8000, BANK_SIZE);
            for (int i = 0; i < data.Length; i++)
                if (data[i] != datar[i])
                {
                    throw new VerificationException("Check failed");
                }
            Console.WriteLine("OK");

            var writeStartTime = DateTime.Now;
            var lastSectorTime = DateTime.Now;
            TimeSpan timeEstimated;
            var badSectors = new List<int>();

            for (int bank = 0; bank < banks; bank += 4)
            {
                byte r0 = (byte)(bank >> 7);
                byte r1 = (byte)(bank << 1);
                dumper.WriteCpu(0x5000, r0);
                dumper.WriteCpu(0x5001, r1);

                timeEstimated = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (banks - bank) / 4);
                timeEstimated = timeEstimated.Add(DateTime.Now - writeStartTime);
                lastSectorTime = DateTime.Now;
                var timePassed = DateTime.Now - writeStartTime;
                Console.Write($"Erasing sector #{bank / 4}/{banks / 4} ({100 * bank / banks}%, {timePassed.Hours:D2}:{timePassed.Minutes:D2}:{timePassed.Seconds:D2}/{timeEstimated.Hours:D2}:{timeEstimated.Minutes:D2}:{timeEstimated.Seconds:D2})... ");
                try
                {
                    dumper.EraseFlashSector();
                    Console.WriteLine("OK");
                }
                catch
                {
                    Console.WriteLine("ERROR!");
                    if (!silent) Program.PlayErrorSound();
                    Console.Write("Trying again... ");
                    Program.Reset(dumper);
                    dumper.WriteCpu(0x5007, 0x04); // enable PRG write
                    dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
                    dumper.WriteCpu(0x5000, r0);
                    dumper.WriteCpu(0x5001, r1);
                    try
                    {
                        dumper.EraseFlashSector();
                        Console.WriteLine("OK");
                    }
                    catch
                    {
                        Console.WriteLine($"ERROR! Sector #{bank / 4} is bad.");
                        if (!silent) Program.PlayErrorSound();
                        badSectors.Add(bank / 4);
                    }
                }
            }
            if (badSectors.Count > 0)
            {
                foreach (var bad in badSectors)
                    Console.WriteLine($"Bad sector: {bad}");
                throw new IOException("Bad sectors found");
            }
            else Console.WriteLine("There is no bad sectors");
        }

        public static void ReadCrc(IFamicomDumperConnection dumper)
        {
            Program.Reset(dumper);
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
            var cfi = FlashHelper.GetCFIInfo(dumper);
            Console.WriteLine($"Device size: {cfi.DeviceSize / 1024 / 1024} MByte / {cfi.DeviceSize / 1024 / 1024 * 8} Mbit");
            uint banks = cfi.DeviceSize / BANK_SIZE;

            var readStartTime = DateTime.Now;
            var lastSectorTime = DateTime.Now;
            var timeEstimated = new TimeSpan();
            ushort crc = 0;
            for (int bank = 0; bank < banks; bank++)
            {
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
                var timePassed = DateTime.Now - readStartTime;
                Console.Write($"Reading CRC of bank #{bank}/{banks} ({100 * bank / banks}%, {timePassed.Hours:D2}:{timePassed.Minutes:D2}:{timePassed.Seconds:D2}/{timeEstimated.Hours:D2}:{timeEstimated.Minutes:D2}:{timeEstimated.Seconds:D2})... ");
                var crcr = dumper.ReadCpuCrc(0x8000, BANK_SIZE);
                Console.WriteLine($"CRC = {crcr:X4}");
                crc ^= crcr;
            }
            Console.WriteLine($"Total CRC = {crc:X4}");
        }

        public static void TestPrgRam(IFamicomDumperConnection dumper, int count = -1)
        {
            Program.Reset(dumper);
            dumper.WriteCpu(0x5007, 0x01); // enable PRG RAM
            var rnd = new Random();
            while (count != 0)
            {
                var data = new byte[][] { new byte[0x2000], new byte[0x2000], new byte[0x2000], new byte[0x2000] };
                for (byte bank = 0; bank < 4; bank++)
                {
                    Console.WriteLine($"Writing PRG RAM, bank #{bank}/{4}... ");
                    rnd.NextBytes(data[bank]);
                    dumper.WriteCpu(0x5005, bank);
                    dumper.WriteCpu(0x6000, data[bank]);
                }
                for (byte bank = 0; bank < 4; bank++)
                {
                    Console.Write($"Reading PRG RAM, bank #{bank}/{4}... ");
                    dumper.WriteCpu(0x5005, bank);
                    var rdata = dumper.ReadCpu(0x6000, 0x2000);
                    bool ok = true;
                    for (int b = 0; b < 0x2000; b++)
                    {
                        if (data[bank][b] != rdata[b])
                        {
                            Console.WriteLine($"Mismatch at {b:X4}: {rdata[b]:X2} != {data[bank][b]:X2}");
                            ok = false;
                        }
                    }
                    if (!ok)
                    {
                        File.WriteAllBytes("prgramgood.bin", data[bank]);
                        Console.WriteLine("prgramgood.bin writed");
                        File.WriteAllBytes("prgrambad.bin", rdata);
                        Console.WriteLine("prgrambad.bin writed");
                        throw new IOException("Test failed");
                    }
                    Console.WriteLine("OK");
                }
                count--;
            }
        }

        public static void FullTest(IFamicomDumperConnection dumper, int count = -1, int chrSize = -1)
        {
            while (count != 0)
            {
                TestChrRam(dumper, 1, chrSize);
                TestPrgRam(dumper, 1);
                if (count > 0) count--;
            }
        }

        public static void TestChrRam(IFamicomDumperConnection dumper, int count = -1, int chrSize = -1)
        {
            if (chrSize < 0) chrSize = 256 * 1024;
            Console.WriteLine($"Testing CHR RAM, size: {chrSize / 1024}KB");
            Program.Reset(dumper);
            dumper.WriteCpu(0x5007, 0x2); // enable CHR writing
            var rnd = new Random();
            var data = new byte[0x2000];
            rnd.NextBytes(data);
            Console.WriteLine("Single bank test.");
            Console.Write("Writing CHR RAM... ");
            dumper.WritePpu(0x0000, data);
            Console.Write("Reading CHR RAM... ");
            var rdata = dumper.ReadPpu(0x0000, 0x2000);
            bool ok = true;
            for (int b = 0; b < 0x2000; b++)
            {
                if (data[b] != rdata[b])
                {
                    Console.WriteLine($"Mismatch at {b:X4}: {rdata[b]:X2} != {data[b]:X2}");
                    ok = false;
                }
            }
            if (!ok)
            {
                File.WriteAllBytes("chrramgood.bin", data);
                Console.WriteLine("chrramgood.bin writed");
                File.WriteAllBytes("chrrambad.bin", rdata);
                Console.WriteLine("chrrambad.bin writed");
                throw new IOException("Test failed");
            }
            Console.WriteLine("OK");

            Console.WriteLine("Multibank test.");
            data = new byte[chrSize];
            for (; count != 0; count--)
            {
                dumper.WriteCpu(0x5007, 0x2); // enable CHR writing
                rnd.NextBytes(data);
                for (byte bank = 0; bank < data.Length / 0x2000; bank++)
                {
                    Console.WriteLine($"Writing CHR RAM bank #{bank}/{data.Length / 0x2000}...");
                    dumper.WriteCpu(0x5003, (byte)(bank & 0b00011111)); // select bank, low 5 bits
                    dumper.WriteCpu(0x5005, (byte)((bank & 0b00100000) << 2)); // select bank, 6th bit
                    var d = new byte[0x2000];
                    Array.Copy(data, bank * 0x2000, d, 0, 0x2000);
                    dumper.WritePpu(0x0000, d);
                }
                for (byte bank = 0; bank < data.Length / 0x2000; bank++)
                {
                    Console.Write($"Reading CHR RAM bank #{bank}/{data.Length / 0x2000}... ");
                    dumper.WriteCpu(0x5003, (byte)(bank & 0b00011111)); // select bank, low 5 bits
                    dumper.WriteCpu(0x5005, (byte)((bank & 0b00100000) << 2)); // select bank, 6th bit
                    rdata = dumper.ReadPpu(0x0000, 0x2000);
                    ok = true;
                    for (int b = 0; b < 0x2000; b++)
                    {
                        if (data[b + bank * 0x2000] != rdata[b])
                        {
                            Console.WriteLine($"Mismatch at {b:X4}: {rdata[b]:X2} != {data[b + bank * 0x2000]:X2}");
                            ok = false;
                        }
                    }
                    if (!ok)
                    {
                        File.WriteAllBytes("chrramgoodf.bin", data);
                        Console.WriteLine("chrramgoodf.bin writed");
                        File.WriteAllBytes("chrrambad.bin", rdata);
                        Console.WriteLine("chrrambad.bin writed");
                        throw new IOException("Test failed");
                    }
                    Console.WriteLine("OK");
                }
            }
        }
    }
}
