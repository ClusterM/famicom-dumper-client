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
    public static class CoolboyWriter
    {
        public static byte DetectVersion(FamicomDumperConnection dumper)
        {
            byte version;
            Console.Write("Detecting COOLBOY version... ");
            // 0th CHR bank using both methods
            dumper.WriteCpu(0x5000, new byte[] { 0, 0, 0, 0x10 });
            dumper.WriteCpu(0x6000, new byte[] { 0, 0, 0, 0x10 });
            // Writing 0
            dumper.WritePpu(0x0000, new byte[] { 0 });
            // First CHR bank using both methods
            dumper.WriteCpu(0x5000, new byte[] { 0, 0, 1, 0x10 });
            dumper.WriteCpu(0x6000, new byte[] { 0, 0, 1, 0x10 });
            // Writing 1
            dumper.WritePpu(0x0000, new byte[] { 1 });
            // 0th bank using first method
            dumper.WriteCpu(0x6000, new byte[] { 0, 0, 0, 0x10 });
            byte v6000 = dumper.ReadPpu(0x0000, 1)[0];
            // return
            dumper.WriteCpu(0x6000, new byte[] { 0, 0, 1, 0x10 });
            // 0th bank using second method
            dumper.WriteCpu(0x5000, new byte[] { 0, 0, 0, 0x10 });
            byte v5000 = dumper.ReadPpu(0x0000, 1)[0];

            if (v6000 == 0 && v5000 == 1)
                version = 1;
            else if (v6000 == 1 && v5000 == 0)
                version = 2;
            else throw new IOException("Can't detect COOLBOY version");
            Console.WriteLine("Version: {0}", version);
            return version;
        }

        public static void WriteWithGPIO(FamicomDumperConnection dumper, string fileName)
        {
            byte[] PRG;
            try
            {
                var nesFile = new NesFile(fileName);
                PRG = nesFile.PRG;
            }
            catch
            {
                var nesFile = new UnifFile(fileName);
                PRG = nesFile.Fields["PRG0"];
            }
            while (PRG.Length < 512 * 1024)
            {
                var PRGbig = new byte[PRG.Length * 2];
                Array.Copy(PRG, 0, PRGbig, 0, PRG.Length);
                Array.Copy(PRG, 0, PRGbig, PRG.Length, PRG.Length);
                PRG = PRGbig;
            }

            int prgBanks = PRG.Length / 0x2000;

            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
            var version = DetectVersion(dumper);
            var CoolboyReg = (UInt16)(version == 2 ? 0x5000 : 0x6000);
            dumper.WriteCpu(0xA001, 0x00); // RAM protect
            var writeStartTime = DateTime.Now;
            var lastSectorTime = DateTime.Now;
            var timeTotal = new TimeSpan();
            for (int bank = 0; bank < prgBanks; bank += 2)
            {
                int outbank = bank / 16;
                byte r0 = (byte)((outbank & 0x07) | ((outbank & 0xc0) >> 2));
                byte r1 = (byte)(((outbank & 0x30) >> 2) | ((outbank << 1) & 0x10));
                byte r2 = 0;
                byte r3 = 0;
                dumper.WriteCpu(CoolboyReg, new byte[] { r0, r1, r2, r3 });

                int inbank = bank % 64;
                dumper.WriteCpu(0x8000, new byte[] { 6, (byte)(inbank) });
                dumper.WriteCpu(0x8000, new byte[] { 7, (byte)(inbank | 1) });

                var data = new byte[0x4000];
                int pos = bank * 0x2000;
                if (pos % (128 * 1024) == 0)
                {
                    timeTotal = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (prgBanks - bank) / 16);
                    timeTotal = timeTotal.Add(DateTime.Now - writeStartTime);
                    lastSectorTime = DateTime.Now;
                    Console.Write("Erasing sector... ");
                    dumper.EraseCpuFlash(FamicomDumperConnection.MemoryAccessMethod.CoolboyGPIO);
                    Console.WriteLine("OK");
                }
                Array.Copy(PRG, pos, data, 0, data.Length);
                var timePassed = DateTime.Now - writeStartTime;
                Console.Write("Writing {0}/{1} ({2}%, {3:D2}:{4:D2}:{5:D2}/{6:D2}:{7:D2}:{8:D2})... ", bank / 2 + 1, prgBanks / 2, (int)(100 * bank / prgBanks),
                    timePassed.Hours, timePassed.Minutes, timePassed.Seconds, timeTotal.Hours, timeTotal.Minutes, timeTotal.Seconds);
                dumper.WriteCpuFlash(0x0000, data, FamicomDumperConnection.MemoryAccessMethod.CoolboyGPIO, false);
                Console.WriteLine("OK");
            }
        }

        public static void GetInfo(FamicomDumperConnection dumper)
        {
            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
            var version = DetectVersion(dumper);
            var CoolboyReg = (UInt16)(version == 2 ? 0x5000 : 0x6000);
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
            dumper.WriteCpu(CoolboyReg, new byte[] { r0, r1, r2, r3 });
            FlashHelper.GetFlashSizePrintInfo(dumper);
        }

        public static void Write(FamicomDumperConnection dumper, string fileName, IEnumerable<int> badSectors, bool silent, bool needCheck = false, bool writePBBs = false, bool ignoreBadSectors = false)
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
                    PRG = nesFile.PRG;
                }
                catch
                {
                    var nesFile = new UnifFile(fileName);
                    PRG = nesFile.Fields["PRG0"];
                }
            }

            int prgBanks = PRG.Length / 0x4000;

            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
            var version = DetectVersion(dumper);
            var coolboyReg = (ushort)(version == 2 ? 0x5000 : 0x6000);
            FlashHelper.ResetFlash(dumper);
            int flashSize = FlashHelper.GetFlashSizePrintInfo(dumper);
            FlashHelper.LockBitsCheck(dumper);
            if (PRG.Length > flashSize)
                throw new ArgumentOutOfRangeException("PRG.Length", "This ROM is too big for this cartridge");
            try
            {
                PPBErase(dumper, coolboyReg);
            }
            catch (Exception ex)
            {
                if (!silent) Program.PlayErrorSound();
                Console.WriteLine($"ERROR! {ex.Message}. Lets try anyway.");
            }

            var writeStartTime = DateTime.Now;
            var lastSectorTime = DateTime.Now;
            var timeTotal = new TimeSpan();
            int totalErrorCount = 0;
            int currentErrorCount = 0;
            var newBadSectorsList = new List<int>(badSectors);
            for (int bank = 0; bank < prgBanks; bank++)
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
                    dumper.WriteCpu(coolboyReg, new byte[] { r0, r1, r2, r3 });

                    var data = new byte[0x4000];
                    int pos = bank * 0x4000;
                    if (pos % (128 * 1024) == 0)
                    {
                        timeTotal = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (prgBanks - bank) / 8);
                        timeTotal = timeTotal.Add(DateTime.Now - writeStartTime);
                        lastSectorTime = DateTime.Now;
                        Console.Write($"Erasing sector #{bank / 8}... ");
                        dumper.EraseCpuFlash(FamicomDumperConnection.MemoryAccessMethod.Direct);
                        Console.WriteLine("OK");
                    }
                    Array.Copy(PRG, pos, data, 0, data.Length);
                    var timePassed = DateTime.Now - writeStartTime;
                    Console.Write("Writing {0}/{1} ({2}%, {3:D2}:{4:D2}:{5:D2}/{6:D2}:{7:D2}:{8:D2})... ", bank + 1, prgBanks, (int)(100 * bank / prgBanks),
                        timePassed.Hours, timePassed.Minutes, timePassed.Seconds, timeTotal.Hours, timeTotal.Minutes, timeTotal.Seconds);
                    dumper.WriteCpuFlash(0x0000, data, FamicomDumperConnection.MemoryAccessMethod.Direct, false);
                    Console.WriteLine("OK");
                    if ((bank % 8 == 7) || (bank == prgBanks - 1)) // After last bank in sector
                    {
                        if (writePBBs)
                            PPBSet(dumper, coolboyReg, (uint)bank / 8);
                        currentErrorCount = 0;
                    }
                }
                catch (Exception ex)
                {
                    totalErrorCount++;
                    currentErrorCount++;
                    Console.WriteLine($"Error {ex.GetType()}: {ex.Message}");
                    if (!silent) Program.PlayErrorSound();
                    if (currentErrorCount >= 3)
                    {
                        if (!ignoreBadSectors)
                            throw ex;
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
                    Console.Write("Reset... ");
                    dumper.Reset();
                    Console.WriteLine("OK");
                    FlashHelper.ResetFlash(dumper);
                    continue;
                }
            }

            var wrongCrcSectorsList = new List<int>();
            if (needCheck)
            {
                Console.WriteLine("Starting check process");
                Console.Write("Reset... ");
                dumper.Reset();
                Console.WriteLine("OK");

                var readStartTime = DateTime.Now;
                lastSectorTime = DateTime.Now;
                timeTotal = new TimeSpan();
                for (int bank = 0; bank < prgBanks; bank++)
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
                    dumper.WriteCpu(coolboyReg, new byte[] { r0, r1, r2, r3 });

                    var data = new byte[0x4000];
                    int pos = bank * 0x4000;
                    if (pos % (128 * 1024) == 0)
                    {
                        timeTotal = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (prgBanks - bank) / 8);
                        timeTotal = timeTotal.Add(DateTime.Now - readStartTime);
                        lastSectorTime = DateTime.Now;
                    }
                    Array.Copy(PRG, pos, data, 0, data.Length);
                    UInt16 crc = 0;
                    foreach (var a in data)
                    {
                        crc ^= a;
                        for (int i = 0; i < 8; ++i)
                        {
                            if ((crc & 1) != 0)
                                crc = (UInt16)((crc >> 1) ^ 0xA001);
                            else
                                crc = (UInt16)(crc >> 1);
                        }
                    }
                    var timePassed = DateTime.Now - readStartTime;
                    Console.Write("Reading CRC {0}/{1} ({2}%, {3:D2}:{4:D2}:{5:D2}/{6:D2}:{7:D2}:{8:D2})... ", bank + 1, prgBanks, (int)(100 * bank / prgBanks),
                        timePassed.Hours, timePassed.Minutes, timePassed.Seconds, timeTotal.Hours, timeTotal.Minutes, timeTotal.Seconds);
                    var crcr = dumper.ReadCpuCrc(0x8000, 0x4000);
                    if (crcr != crc)
                    {
                        Console.WriteLine($"ERROR: {crcr:X4} != {crc:X4}");
                        if (!silent) Program.PlayErrorSound();
                        wrongCrcSectorsList.Add(bank / 8);
                    }
                    else
                        Console.WriteLine("OK (CRC = {0:X4})", crcr);
                }
            }

            if (totalErrorCount > 0)
                Console.WriteLine($"Write error count: {totalErrorCount}");
            if (newBadSectorsList.Any())
                Console.WriteLine($"Can't write sectors: {string.Join(", ", newBadSectorsList.OrderBy(s => s))}");
            if (wrongCrcSectorsList.Any())
                Console.WriteLine($"Sectors with wrong CRC: {string.Join(", ", wrongCrcSectorsList.Distinct().OrderBy(s => s))}");
            if (newBadSectorsList.Any() || wrongCrcSectorsList.Any())
                throw new IOException("Cartridge is not writed correctly");
        }

        public static byte PPBRead(FamicomDumperConnection dumper, ushort coolboyReg, uint sector)
        {
            // Select sector
            int bank = (int)(sector * 8);
            byte r0 = (byte)(((bank >> 3) & 0x07) // 5, 4, 3 bits
                | (((bank >> 9) & 0x03) << 4) // 10, 9 bits
                | (1 << 6)); // resets 4th mask bit
            byte r1 = (byte)((((bank >> 7) & 0x03) << 2) // 8, 7
                | (((bank >> 6) & 1) << 4) // 6
                | (1 << 7)); // resets 5th mask bit
            byte r2 = 0;
            byte r3 = (byte)((1 << 4) // NROM mode
                | ((bank & 7) << 1)); // 2, 1, 0 bits
            dumper.WriteCpu(coolboyReg, new byte[] { r0, r1, r2, r3 });

            return FlashHelper.PPBRead(dumper);
        }

        public static void PPBSet(FamicomDumperConnection dumper, ushort coolboyReg, uint sector)
        {
            Console.Write($"Writing PPB for sector #{sector}... ");
            // Select sector
            int bank = (int)(sector * 8);
            byte r0 = (byte)(((bank >> 3) & 0x07) // 5, 4, 3 bits
                | (((bank >> 9) & 0x03) << 4) // 10, 9 bits
                | (1 << 6)); // resets 4th mask bit
            byte r1 = (byte)((((bank >> 7) & 0x03) << 2) // 8, 7
                | (((bank >> 6) & 1) << 4) // 6
                | (1 << 7)); // resets 5th mask bit
            byte r2 = 0;
            byte r3 = (byte)((1 << 4) // NROM mode
                | ((bank & 7) << 1)); // 2, 1, 0 bits
            dumper.WriteCpu(coolboyReg, new byte[] { r0, r1, r2, r3 });

            FlashHelper.PPBSet(dumper);
        }

        public static void PPBErase(FamicomDumperConnection dumper, ushort coolboyReg)
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
            dumper.WriteCpu(coolboyReg, new byte[] { r0, r1, r2, r3 });

            FlashHelper.PPBErase(dumper);
        }
    }
}
