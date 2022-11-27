using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using RemoteDumper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static com.clusterrr.Famicom.DumperConnection.CFIInfo;

namespace com.clusterrr.Famicom.Dumper.FlashWriters
{
    public enum FlashEraseMode { Chip, Sector }
    public record FlashInfo
    {
        public int DeviceSize;
        public int MaximumNumberOfBytesInMultiProgram;
        public EraseBlockRegionInfo[]? Regions;
    }


    public abstract class FlashWriter
    {
        const int MAX_WRITE_ERROR_COUNT = 5;

        protected abstract IFamicomDumperConnectionExt Dumper { get; }
        protected abstract int BankSize { get; }
        protected abstract FlashEraseMode EraseMode { get; }
        protected virtual bool UseSubmappers { get => false; }
        protected virtual bool CanUsePpbs { get => false; }
        protected virtual bool NeedEnlarge { get => false; }
        protected virtual void Init()
        {
        }
        protected abstract bool CheckMapper(ushort mapper, byte submapper);
        protected abstract bool CheckMapper(string mapper);
        protected abstract FlashInfo GetFlashInfo();
        protected virtual void InitBanking()
        {
        }
        protected abstract void Erase(int offset);
        protected abstract void Write(byte[] data, int offset);
        protected abstract ushort ReadCrc(int offset);
        protected virtual void PPBClear()
        {
        }
        protected virtual void PPBSet(int offset)
        {
        }
        public abstract void PrintFlashInfo();

        public void Write(string filename, IEnumerable<int>? badSectors = null, bool silent = false, bool needCheck = false, bool writePBBs = false, bool ignoreBadSectors = false)
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
                    if (!CheckMapper(nes.Mapper, nes.Submapper))
                        Console.WriteLine($"WARNING! Invalid mapper: {nes.Mapper}{(UseSubmappers ? $".{nes.Submapper}" : "")}, most likely it will not work after writing.");
                    PRG = nes.PRG;
                    break;
                case ".unf":
                    var unif = UnifFile.FromFile(filename);
                    var mapper = unif.Mapper!;
                    if (mapper.StartsWith("NES-") || mapper.StartsWith("UNL-") || mapper.StartsWith("HVC-") || mapper.StartsWith("BTL-") || mapper.StartsWith("BMC-"))
                        mapper = mapper[4..];
                    if (!CheckMapper(mapper))
                        Console.WriteLine($"WARNING! Invalid mapper: {unif.Mapper}, most likely it will not work after writing.");
                    PRG = unif.PRG0!;
                    break;
                default:
                    throw new InvalidDataException($"Unknown file extension: {extension}, can't detect file format");
            }

            Program.Reset(Dumper);
            Init();
            InitBanking();
            FlashHelper.ResetFlash(Dumper);
            var flash = GetFlashInfo();
            if (flash.DeviceSize > 4 * 1024 * 1024)
                Console.WriteLine($"Device size: {flash.DeviceSize / 1024 / 1024} MByte / {flash.DeviceSize / 1024 / 1024 * 8} Mbit");
            else
                Console.WriteLine($"Device size: {flash.DeviceSize / 1024 } KByte / {flash.DeviceSize / 1024 * 8} Kbit");
            if (flash.MaximumNumberOfBytesInMultiProgram > 0)
            {
#if DEBUG
                Console.WriteLine($"Maximum number of bytes in multi-byte program: {flash.MaximumNumberOfBytesInMultiProgram}");
#endif
                if (Dumper.ProtocolVersion >= 3)
                    Dumper.SetMaximumNumberOfBytesInMultiProgram((uint)flash.MaximumNumberOfBytesInMultiProgram);
            }
            if (PRG.Length > flash.DeviceSize)
                throw new InvalidDataException("This ROM is too big for this cartridge");

            if (NeedEnlarge)
            {
                var pow = (int)Math.Ceiling(Math.Log(PRG.Length, 2));
                var upSize = (int)Math.Pow(2, pow);
                PRG = Enumerable.Concat(PRG, Enumerable.Repeat((byte)0xFF, upSize - PRG.Length)).ToArray();
                while (PRG.Length < flash.DeviceSize)
                    PRG = Enumerable.Concat(PRG, PRG).ToArray();
            }

            try
            {
                if (CanUsePpbs)
                {
                    Console.Write("Erasing PPBs... ");
                    PPBClear();
                    Console.WriteLine("OK");
                }
            }
            catch (Exception ex)
            {
                if (!silent) Program.PlayErrorSound();
                Console.WriteLine($"ERROR! {ex.Message}. Lets continue anyway.");
            }

            int banks = PRG.Length / BankSize;
            int region = 0;
            int totalSector = 0;
            int currentRegionSector = 0;
            int totalBank = 0;
            int currentSectorBank = 0;
            int totalErrorCount = 0;
            int currentErrorCount = 0;
            var newBadSectorsList = new List<int>();
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (totalBank < banks)
            {
                try
                {
                    int offset = totalBank * BankSize;

                    if (EraseMode == FlashEraseMode.Sector)
                    {
                        if (currentSectorBank * BankSize >= flash.Regions![region].SizeOfBlocks)
                        {
                            totalSector++;
                            currentRegionSector++;
                            currentSectorBank = 0;
                        }
                        if (currentRegionSector > flash.Regions[region].NumberOfBlocks)
                        {
                            region++;
                            currentRegionSector = 0;
                        }
                        if ((badSectors != null) && (badSectors.Contains(totalSector) || newBadSectorsList.Contains(totalSector)))
                        {
                            // Bad sector :( Skip it
                            Console.WriteLine($"Sector #{totalSector} is bad, let's skip it.");
                            totalBank += flash.Regions[region].SizeOfBlocks / BankSize;
                            currentSectorBank += flash.Regions[region].SizeOfBlocks / BankSize;
                            continue;
                        }
                    }

                    if (currentSectorBank == 0)
                    {
                        // TODO: Should i add option to skip empty sectors erasing?
                        /*
                        if (PRG.Skip(offset).Take(flash.Regions[region].SizeOfBlocks).Where(b => b != 0xFF).Any())
                        {
                        */
                        // Erase sector
                        switch (EraseMode)
                        {
                            case FlashEraseMode.Chip:
                                Console.Write($"Erasing sector chip... ");
                                break;
                            case FlashEraseMode.Sector:
                                Console.Write($"Erasing sector #{totalSector}... ");
                                break;
                        }
                        Erase(offset);
                        Console.WriteLine("OK");
                        /*
                        }
                        else
                        {
                            // Skip sector
                            Console.WriteLine($"Sector #{totalSector} is empty, let's skip it.");
                            totalBank += flash.Regions[region].SizeOfBlocks / bankSize;
                            currentSectorBank += flash.Regions[region].SizeOfBlocks / bankSize;
                            continue;
                        }
                        */
                    }

                    var data = PRG.Skip(offset).Take(BankSize).ToArray();
                    var timePassed = stopwatch.Elapsed;
                    var timeEstimated = offset > 0 ? timePassed * PRG.Length / offset : new TimeSpan();
                    Console.Write($"Writing bank #{totalBank}/{banks} ({(offset > 0 ? 100L * offset / PRG.Length : 0)}%, {timePassed.Hours:D2}:{timePassed.Minutes:D2}:{timePassed.Seconds:D2}/{timeEstimated.Hours:D2}:{timeEstimated.Minutes:D2}:{timeEstimated.Seconds:D2})... ");
                    Write(data, offset);
                    Console.WriteLine("OK");

                    if (EraseMode == FlashEraseMode.Sector)
                    {
                        if ((currentSectorBank + 1) * BankSize >= flash.Regions![region].SizeOfBlocks)
                        {
                            if (CanUsePpbs && writePBBs)
                                PPBSet(offset);
                            currentErrorCount = 0;
                        }
                    }

                    totalBank++;
                    currentSectorBank++;
                }
                catch (Exception ex)
                {
                    switch (EraseMode)
                    {
                        case FlashEraseMode.Chip:
                            throw;
                        case FlashEraseMode.Sector:
                            totalErrorCount++;
                            currentErrorCount++;
                            Console.WriteLine($"ERROR: {ex.Message}");
                            if (!silent) Program.PlayErrorSound();
                            if (currentErrorCount >= MAX_WRITE_ERROR_COUNT)
                            {
                                if (!ignoreBadSectors)
                                    throw;
                                else
                                {
                                    newBadSectorsList.Add(totalSector);
                                    currentErrorCount = 0;
                                    Console.WriteLine($"Lets skip sector #{currentRegionSector}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Lets try again");
                            }
                            // Back to the first bank of the sector
                            totalBank -= currentSectorBank;
                            currentSectorBank = 0;
                            Program.Reset(Dumper);
                            InitBanking();
                            FlashHelper.ResetFlash(Dumper);
                            continue;
                    }
                }
            }

            var wrongCrcSectorsList = new HashSet<int>();
            if (needCheck)
            {
                Console.WriteLine("Starting verification process");
                Program.Reset(Dumper);
                InitBanking();

                banks = PRG.Length / BankSize;
                region = 0;
                totalSector = 0;
                currentRegionSector = 0;
                totalBank = 0;
                currentSectorBank = 0;
                stopwatch = new Stopwatch();
                stopwatch.Start();

                while (totalBank < banks)
                {
                    int offset = totalBank * BankSize;

                    if (EraseMode == FlashEraseMode.Sector)
                    {
                        if (currentSectorBank * BankSize >= flash.Regions![region].SizeOfBlocks)
                        {
                            totalSector++;
                            currentRegionSector++;
                            currentSectorBank = 0;
                        }
                        if (currentRegionSector > flash.Regions[region].NumberOfBlocks)
                        {
                            region++;
                            currentRegionSector = 0;
                        }
                        if ((badSectors != null) && badSectors.Contains(totalSector) || newBadSectorsList.Contains(totalSector))
                        {
                            // Bad sector :( Skip it
                            Console.WriteLine($"Sector #{totalSector} is bad, let's skip it.");
                            totalBank += flash.Regions[region].SizeOfBlocks / BankSize;
                            currentSectorBank += flash.Regions[region].SizeOfBlocks / BankSize;
                            continue;
                        }
                        /*
                        if (currentSectorBank == 0)
                        {
                            if (!PRG.Skip(offset).Take(flash.Regions[region].SizeOfBlocks).Where(b => b != 0xFF).Any())
                            {
                                // Skip sector
                                Console.WriteLine($"Sector #{totalSector} is empty, let's skip it.");
                                totalBank += flash.Regions[region].SizeOfBlocks / BankSize;
                                currentSectorBank += flash.Regions[region].SizeOfBlocks / BankSize;
                                continue;
                            }
                        }
                        */
                    }

                    ushort crc = Crc16Calculator.CalculateCRC16(PRG, offset, BankSize);
                    var timePassed = stopwatch.Elapsed;
                    var timeEstimated = offset > 0 ? timePassed * PRG.Length / offset : new TimeSpan();
                    Console.Write($"Reading CRC of bank #{totalBank}/{banks} ({(offset > 0 ? 100L * offset / PRG.Length : 0)}%, {timePassed.Hours:D2}:{timePassed.Minutes:D2}:{timePassed.Seconds:D2}/{timeEstimated.Hours:D2}:{timeEstimated.Minutes:D2}:{timeEstimated.Seconds:D2})... ");
                    var crcr = ReadCrc(offset);
                    if (crcr != crc)
                    {
                        Console.WriteLine($"Verification failed: {crcr:X4} != {crc:X4}");
                        if (!silent) Program.PlayErrorSound();
                        switch (EraseMode)
                        {
                            case FlashEraseMode.Chip:
                                wrongCrcSectorsList.Add(totalBank);
                                break;
                            case FlashEraseMode.Sector:
                                wrongCrcSectorsList.Add(currentRegionSector);
                                break;
                        }
                    }
                    else Console.WriteLine($"OK (CRC = {crcr:X4})");

                    totalBank++;
                    currentSectorBank++;
                }
            }

            if (totalErrorCount > 0)
                Console.WriteLine($"Write error count: {totalErrorCount}");
            if (newBadSectorsList.Any())
                Console.WriteLine($"Can't write sectors: {string.Join(", ", newBadSectorsList.OrderBy(s => s))}");
            if (wrongCrcSectorsList.Any())
                Console.WriteLine($"{(EraseMode == FlashEraseMode.Sector ? "Sectors" : "Banks")} with wrong CRC: {string.Join(", ", wrongCrcSectorsList.Distinct().OrderBy(s => s))}");

            if (newBadSectorsList.Any() || wrongCrcSectorsList.Any())
                throw new IOException("Cartridge is not writed correctly");
        }
    }
}
