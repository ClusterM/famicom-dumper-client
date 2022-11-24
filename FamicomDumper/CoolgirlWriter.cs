using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using com.clusterrr.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;

namespace com.clusterrr.Famicom.Dumper
{
    public class CoolgirlWriter
    {
        const int BANK_SIZE = 0x8000;
        const int MAPPER_NUMBER = 342;
        const string MAPPER_STRING = "COOLGIRL";

        private readonly IFamicomDumperConnectionExt dumper;

        public CoolgirlWriter(IFamicomDumperConnectionExt dumper)
        {
            this.dumper = dumper;
        }

        public void PrintFlashInfo()
        {
            Program.Reset(dumper);
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
            var cfi = FlashHelper.GetCFIInfo(dumper);
            FlashHelper.PrintCFIInfo(cfi);
            FlashHelper.LockBitsCheckPrint(dumper);
            FlashHelper.PPBLockBitCheckPrint(dumper);
        }

        public void Write(string filename, IEnumerable<int> badSectors, bool silent, bool needCheck = false, bool writePBBs = false, bool ignoreBadSectors = false)
        {
            byte[] PRG;
            var extension = Path.GetExtension(filename).ToLower();
            switch (extension)
            {
                case ".bin":
                    PRG = File.ReadAllBytes(filename);
                    break;
                case ".nes":
                    var nes = NesFile.FromFile(filename);
                    if (nes.Mapper != MAPPER_NUMBER)
                        Console.WriteLine($"WARNING! Invalid mapper: {nes.Mapper}, most likely it will not work after writing.");
                    PRG = nes.PRG;
                    break;
                case ".unf":
                    var unif = UnifFile.FromFile(filename);
                    var mapper = unif.Mapper;
                    if (mapper.StartsWith("NES-") || mapper.StartsWith("UNL-") || mapper.StartsWith("HVC-") || mapper.StartsWith("BTL-") || mapper.StartsWith("BMC-"))
                        mapper = mapper[4..];
                    if (mapper != MAPPER_STRING)
                        Console.WriteLine($"WARNING! Invalid mapper: {mapper}, most likely it will not work after writing.");
                    PRG = unif.PRG0;
                    break;
                default:
                    throw new InvalidDataException($"Unknown extension: {extension}, can't detect file format");
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
            if (PRG.Length > cfi.DeviceSize)
                throw new InvalidDataException("This ROM is too big for this cartridge");
            try
            {
                PPBClear();
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
            bool sectorContainsData = false;
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
                    if (pos % cfi.GetSectorSizeAt(pos) == 0)
                    {
                        timeEstimated = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (banks - bank) / 4);
                        timeEstimated = timeEstimated.Add(DateTime.Now - writeStartTime);
                        lastSectorTime = DateTime.Now;
                        Console.Write($"Erasing sector #{bank / 4}... ");
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
                    if ((bank % 4 == 3) || (bank == banks - 1)) // After last bank in sector
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
                    if (pos % cfi.GetSectorSizeAt(pos) == 0)
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

        public void PPBClear()
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
