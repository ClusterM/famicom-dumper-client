using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;

namespace com.clusterrr.Famicom
{
    public static class CoolgirlWriter
    {
        public static void GetInfo(FamicomDumperConnection dumper)
        {
            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
            FlashHelper.GetFlashSizePrintInfo(dumper);
            FlashHelper.LockBitsCheckPrint(dumper);
            FlashHelper.PPBLockBitCheckPrint(dumper);
        }

        public static void Write(FamicomDumperConnection dumper, string fileName, IEnumerable<int> badSectors, bool silent, bool needCheck = false, bool checkPause = false, bool writePBBs = false, bool ignoreBadSectors = false)
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

            int prgBanks = PRG.Length / 0x8000;

            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
            dumper.WriteCpu(0x5000, 0);
            dumper.WriteCpu(0x5001, 0);
            FlashHelper.ResetFlash(dumper);
            int flashSize = FlashHelper.GetFlashSizePrintInfo(dumper);
            FlashHelper.LockBitsCheckPrint(dumper);
            if (PRG.Length > flashSize)
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
            var timeTotal = new TimeSpan();
            int totalErrorCount = 0;
            int currentErrorCount = 0;
            var newBadSectorsList = new List<int>();
            for (int bank = 0; bank < prgBanks; bank++)
            {
                while (badSectors.Contains(bank / 4) || newBadSectorsList.Contains(bank / 4)) bank += 4; // bad sector :(
                try
                {
                    byte r0 = (byte)(bank >> 7);
                    byte r1 = (byte)(bank << 1);
                    dumper.WriteCpu(0x5000, r0);
                    dumper.WriteCpu(0x5001, r1);

                    var data = new byte[0x8000];
                    int pos = bank * 0x8000;
                    if (pos % (128 * 1024) == 0)
                    {
                        timeTotal = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (prgBanks - bank) / 4);
                        timeTotal = timeTotal.Add(DateTime.Now - writeStartTime);
                        lastSectorTime = DateTime.Now;
                        Console.Write($"Erasing sector #{bank / 4}... ");
                        dumper.EraseCpuFlashSector();
                        Console.WriteLine("OK");
                    }
                    Array.Copy(PRG, pos, data, 0, data.Length);
                    var timePassed = DateTime.Now - writeStartTime;
                    Console.Write("Writing {0}/{1} ({2}%, {3:D2}:{4:D2}:{5:D2}/{6:D2}:{7:D2}:{8:D2})... ", bank + 1, prgBanks, (int)(100 * bank / prgBanks),
                        timePassed.Hours, timePassed.Minutes, timePassed.Seconds, timeTotal.Hours, timeTotal.Minutes, timeTotal.Seconds);
                    dumper.WriteCpuFlash(0x0000, data);
                    Console.WriteLine("OK");
                    if ((bank % 4 == 3) || (bank == prgBanks - 1)) // After last bank in sector
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
                            throw ex;
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
                    Console.Write("Reset... ");
                    dumper.Reset();
                    Console.WriteLine("OK");
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
                Console.WriteLine("Starting check process");
                if (checkPause)
                {
                    Console.Write("Press enter to continue");
                    if (!silent) Program.PlayDoneSound();
                    Console.ReadLine();
                }
                Console.Write("Reset... ");
                dumper.Reset();
                Console.WriteLine("OK");

                var readStartTime = DateTime.Now;
                lastSectorTime = DateTime.Now;
                timeTotal = new TimeSpan();
                dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
                for (int bank = 0; bank < prgBanks; bank++)
                {
                    while (badSectors.Contains(bank / 4)) bank += 4; // bad sector :(
                    byte r0 = (byte)(bank >> 7);
                    byte r1 = (byte)(bank << 1);
                    dumper.WriteCpu(0x5000, r0);
                    dumper.WriteCpu(0x5001, r1);

                    var data = new byte[0x8000];
                    int pos = bank * 0x8000;
                    if (pos % (128 * 1024) == 0)
                    {
                        timeTotal = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (prgBanks - bank) / 4);
                        timeTotal = timeTotal.Add(DateTime.Now - readStartTime);
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
                    Console.Write("Reading CRC {0}/{1} ({2}%, {3:D2}:{4:D2}:{5:D2}/{6:D2}:{7:D2}:{8:D2})... ", bank + 1, prgBanks, (int)(100 * bank / prgBanks),
                        timePassed.Hours, timePassed.Minutes, timePassed.Seconds, timeTotal.Hours, timeTotal.Minutes, timeTotal.Seconds);
                    var crcr = dumper.ReadCpuCrc(0x8000, 0x8000);
                    if (crcr != crc)
                    {
                        Console.WriteLine($"Check failed: {crcr:X4} != {crc:X4}");
                        if (!silent) Program.PlayErrorSound();
                        wrongCrcSectorsList.Add(bank / 4);
                    }
                    else
                        Console.WriteLine("OK (CRC = {0:X4})", crcr);
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

        /*
        public static void PPBSet(FamicomDumperConnection dumper, uint sector)
        {
            Console.Write($"Writing PPB for sector #{sector}... ");
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
            // Select sector
            byte r0 = (byte)((sector * 4) >> 7);
            byte r1 = (byte)((sector * 4) << 1);
            dumper.WriteCpu(0x5000, r0);
            dumper.WriteCpu(0x5001, r1);
            // PPB Command Set Entry
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0xC0);
            // PPB Program
            dumper.WriteCpu(0x8000, 0xA0);
            dumper.WriteCpu(0x8000, 0x00);
            // Sector 0
            dumper.WriteCpu(0x5000, 0);
            dumper.WriteCpu(0x5001, 0);

            FlashHelper.PPBSet(dumper);
        }
        */

        public static void PPBClear(FamicomDumperConnection dumper)
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

        public static void FindBads(FamicomDumperConnection dumper, bool silent)
        {
            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
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
            var flashSize = FlashHelper.GetFlashSizePrintInfo(dumper);
            int prgBanks = flashSize / 0x8000;
            FlashHelper.LockBitsCheckPrint(dumper);

            Console.Write("Erasing sector #0... ");
            dumper.EraseCpuFlashSector();
            Console.WriteLine("OK");
            var data = new byte[0x8000];
            new Random().NextBytes(data);
            Console.Write("Writing sector #0 for test... ");
            dumper.WriteCpuFlash(0x0000, data);
            Console.WriteLine("OK");
            Console.Write("Reading sector #0 for test... ");
            var datar = dumper.ReadCpu(0x8000, 0x8000);
            for (int i = 0; i < data.Length; i++)
                if (data[i] != datar[i])
                {
                    throw new VerificationException("Check failed");
                }
            Console.WriteLine("OK");

            var writeStartTime = DateTime.Now;
            var lastSectorTime = DateTime.Now;
            var timeTotal = new TimeSpan();
            var badSectors = new List<int>();

            for (int bank = 0; bank < prgBanks; bank += 4)
            {
                byte r0 = (byte)(bank >> 7);
                byte r1 = (byte)(bank << 1);
                dumper.WriteCpu(0x5000, r0);
                dumper.WriteCpu(0x5001, r1);

                timeTotal = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (prgBanks - bank) / 4);
                timeTotal = timeTotal.Add(DateTime.Now - writeStartTime);
                lastSectorTime = DateTime.Now;
                var timePassed = DateTime.Now - writeStartTime;
                Console.Write("Erasing sector {0}/{1} ({2}%, {3:D2}:{4:D2}:{5:D2}/{6:D2}:{7:D2}:{8:D2})... ", bank / 4 + 1, prgBanks / 4, (int)(100 * bank / prgBanks),
                    timePassed.Hours, timePassed.Minutes, timePassed.Seconds, timeTotal.Hours, timeTotal.Minutes, timeTotal.Seconds);
                try
                {
                    dumper.EraseCpuFlashSector();
                    Console.WriteLine("OK");
                }
                catch
                {
                    Console.WriteLine("ERROR!");
                    if (!silent) Program.PlayErrorSound();
                    Console.Write("Trying again... ");
                    dumper.Reset();
                    dumper.WriteCpu(0x5007, 0x04); // enable PRG write
                    dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
                    dumper.WriteCpu(0x5000, r0);
                    dumper.WriteCpu(0x5001, r1);
                    try
                    {
                        dumper.EraseCpuFlashSector();
                        Console.WriteLine("OK");
                    }
                    catch
                    {
                        Console.WriteLine("ERROR! Sector #{0} is bad.", bank / 4);
                        if (!silent) Program.PlayErrorSound();
                        badSectors.Add(bank / 4);
                    }
                }
            }
            if (badSectors.Count > 0)
            {
                foreach (var bad in badSectors)
                    Console.WriteLine("Bad sector: {0}", bad);
                throw new IOException("Bad sectors found");
            }
            else Console.WriteLine("There is no bad sectors");
        }

        public static void ReadCrc(FamicomDumperConnection dumper)
        {
            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
            var flashSize = FlashHelper.GetFlashSizePrintInfo(dumper);
            int prgBanks = flashSize / 0x8000;

            var readStartTime = DateTime.Now;
            var lastSectorTime = DateTime.Now;
            var timeTotal = new TimeSpan();
            UInt16 crc = 0;
            for (int bank = 0; bank < /*16*/prgBanks; bank++)
            {
                byte r0 = (byte)(bank >> 7);
                byte r1 = (byte)(bank << 1);
                dumper.WriteCpu(0x5000, r0);
                dumper.WriteCpu(0x5001, r1);

                var data = new byte[0x8000];
                int pos = bank * 0x8000;
                if (pos % (128 * 1024) == 0)
                {
                    timeTotal = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (prgBanks - bank) / 4);
                    timeTotal = timeTotal.Add(DateTime.Now - readStartTime);
                    lastSectorTime = DateTime.Now;
                }
                var timePassed = DateTime.Now - readStartTime;
                Console.Write("Reading CRC {0}/{1} ({2}%, {3:D2}:{4:D2}:{5:D2}/{6:D2}:{7:D2}:{8:D2})... ", bank + 1, prgBanks, (int)(100 * bank / prgBanks),
                    timePassed.Hours, timePassed.Minutes, timePassed.Seconds, timeTotal.Hours, timeTotal.Minutes, timeTotal.Seconds);
                var crcr = dumper.ReadCpuCrc(0x8000, 0x8000);
                Console.WriteLine("CRC = {0:X4}", crcr);
                crc ^= crcr;
            }
            Console.WriteLine("Total CRC = {0:X4}", crc);
        }

        public static void TestPrgRam(FamicomDumperConnection dumper, int count = -1)
        {
            dumper.Reset();
            dumper.WriteCpu(0x5007, 0x01); // enable SRAM
            var rnd = new Random();
            while (count != 0)
            {
                var data = new byte[][] { new byte[0x2000], new byte[0x2000], new byte[0x2000], new byte[0x2000] };
                for (byte bank = 0; bank < 4; bank++)
                {
                    Console.WriteLine("Writing SRAM, bank #{0}... ", bank);
                    rnd.NextBytes(data[bank]);
                    dumper.WriteCpu(0x5005, bank);
                    dumper.WriteCpu(0x6000, data[bank]);
                }
                for (byte bank = 0; bank < 4; bank++)
                {
                    Console.Write("Reading SRAM, bank #{0}... ", bank);
                    dumper.WriteCpu(0x5005, bank);
                    var rdata = dumper.ReadCpu(0x6000, 0x2000);
                    bool ok = true;
                    for (int b = 0; b < 0x2000; b++)
                    {
                        if (data[bank][b] != rdata[b])
                        {
                            Console.WriteLine("Mismatch at {0:X4}: {1:X2} != {2:X2}", b, rdata[b], data[bank][b]);
                            ok = false;
                        }
                    }
                    if (!ok)
                    {
                        File.WriteAllBytes("sramgood.bin", data[bank]);
                        Console.WriteLine("sramgood.bin writed");
                        File.WriteAllBytes("srambad.bin", rdata);
                        Console.WriteLine("srambad.bin writed");
                        throw new IOException("Test failed");
                    }
                    Console.WriteLine("OK");
                }
                count--;
            }
        }

        public static void FullTest(FamicomDumperConnection dumper, int count = -1)
        {
            while (count != 0)
            {
                TestChrRam(dumper, 1);
                TestPrgRam(dumper, 1);
                if (count > 0) count--;
            }
        }

        public static void TestChrRam(FamicomDumperConnection dumper, int count = -1)
        {
            dumper.Reset();
            dumper.WriteCpu(0x5007, 0x2); // enable CHR writing
            var rnd = new Random();
            var data = new byte[0x2000];
            rnd.NextBytes(data);
            Console.WriteLine("Basic test.");
            Console.Write("Writing CHR RAM... ");
            dumper.WritePpu(0x0000, data);
            Console.Write("Reading CHR RAM... ");
            var rdata = dumper.ReadPpu(0x0000, 0x2000);
            bool ok = true;
            for (int b = 0; b < 0x2000; b++)
            {
                if (data[b] != rdata[b])
                {
                    Console.WriteLine("Mismatch at {0:X4}: {1:X2} != {2:X2}", b, rdata[b], data[b]);
                    ok = false;
                }
            }
            if (!ok)
            {
                File.WriteAllBytes("chrgood.bin", data);
                Console.WriteLine("chrgood.bin writed");
                File.WriteAllBytes("chrbad.bin", rdata);
                Console.WriteLine("chrbad.bin writed");
                throw new IOException("Test failed");
            }
            Console.WriteLine("OK");

            Console.WriteLine("Global test.");
            data = new byte[256 * 1024];
            while (count != 0)
            {
                dumper.Reset();
                dumper.WriteCpu(0x5007, 0x2); // enable CHR writing
                rnd.NextBytes(data);
                for (byte bank = 0; bank < 32; bank++)
                {
                    Console.WriteLine("Writing CHR RAM bank #{0}...", bank);
                    dumper.WriteCpu(0x5003, bank); // select bank
                    var d = new byte[0x2000];
                    Array.Copy(data, bank * 0x2000, d, 0, 0x2000);
                    dumper.WritePpu(0x0000, d);
                }
                for (byte bank = 0; bank < 32; bank++)
                {
                    Console.Write("Reading CHR RAM bank #{0}... ", bank);
                    dumper.WriteCpu(0x5003, bank); // select bank
                    rdata = dumper.ReadPpu(0x0000, 0x2000);
                    ok = true;
                    for (int b = 0; b < 0x2000; b++)
                    {
                        if (data[b + bank * 0x2000] != rdata[b])
                        {
                            Console.WriteLine("Mismatch at {0:X4}: {1:X2} != {2:X2}", b, rdata[b], data[b + bank * 0x2000]);
                            ok = false;
                        }
                    }
                    if (!ok)
                    {
                        File.WriteAllBytes("chrgoodf.bin", data);
                        Console.WriteLine("chrgoodf.bin writed");
                        File.WriteAllBytes("chrbad.bin", rdata);
                        Console.WriteLine("chrbad.bin writed");
                        throw new IOException("Test failed");
                    }
                    Console.WriteLine("OK");
                }
                count--;
            }
        }
    }
}
