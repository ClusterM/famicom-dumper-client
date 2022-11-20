using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using com.clusterrr.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;

namespace com.clusterrr.Famicom.Dumper
{
    public class Unrom512Writer
    {
        const int BANK_SIZE = 0x4000;
        const int MAPPER_NUMBER = 30;
        static string[] MAPPER_STRINGS = { "UNROM", "UNROM-512", "UNROM-512-8", "UNROM-512-16", "UNROM-512-32" };

        private readonly IFamicomDumperConnectionExt dumper;

        public Unrom512Writer(IFamicomDumperConnectionExt dumper)
        {
            this.dumper = dumper;
        }

        void WriteFlashCmd(uint address, byte value)
        {
            dumper.WriteCpu(0xC000, (byte)(address >> 14));
            dumper.WriteCpu((ushort)(0x8000 | (address & 0x3FFF)), value);
        }

        void ResetFlash()
        {
            dumper.WriteCpu(0x8000, 0xF0);
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
                case ".unif":
                    var unif = UnifFile.FromFile(filename);
                    var mapper = unif.Mapper;
                    if (mapper.StartsWith("NES-") || mapper.StartsWith("UNL-") || mapper.StartsWith("HVC-") || mapper.StartsWith("BTL-") || mapper.StartsWith("BMC-"))
                        mapper = mapper[4..];
                    if (!MAPPER_STRINGS.Contains(mapper))
                        Console.WriteLine($"WARNING! Invalid mapper: {mapper}, most likely it will not work after writing.");
                    PRG = unif.PRG0;
                    break;
                default:
                    throw new InvalidDataException($"Unknown extension: {extension}, can't detect file format");
            }

            int banks = PRG.Length / BANK_SIZE;

            Program.Reset(dumper);
            ResetFlash();
            WriteFlashCmd(0x5555, 0xAA);
            WriteFlashCmd(0x2AAA, 0x55);
            WriteFlashCmd(0x5555, 0x90);
            var id = dumper.ReadCpu(0x8000, 2);
            int size = id[1] switch
            {
                0xB5 => 128 * 1024,
                0xB6 => 256 * 1024,
                0xB7 => 512 * 1024,
                _ => 0
            };
            Console.WriteLine($"Device size: " + (size > 0 ? $"{size / 1024} KByte / {size / 1024 * 8} Kbit" : "unknown"));
            if ((size > 0) && (PRG.Length > size))
                throw new InvalidDataException("This ROM is too big for this cartridge");

            Console.Write($"Erasing flash chip... ");
            dumper.EraseUnrom512();
            Console.WriteLine("OK");

            for (int bank = 0; bank < banks; bank++)
            {
                try
                {
                    var data = new byte[BANK_SIZE];
                    int pos = bank * BANK_SIZE;
                    Array.Copy(PRG, pos, data, 0, data.Length);
                    Console.Write($"Writing bank #{bank}/{banks} ({100 * bank / banks}%)... ");
                    dumper.WriteUnrom512((uint)pos, data);
                    Console.WriteLine("OK");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR {ex.GetType()}: {ex.Message}");
                    if (!silent) Program.PlayErrorSound();
                }
            }

            var wrongCrcSectorsList = new List<int>();
            if (needCheck)
            {
                Console.WriteLine("Starting verification process");
                var readStartTime = DateTime.Now;

                for (int bank = 0; bank < banks; bank++)
                {
                    dumper.WriteCpu(0xC000, (byte)bank);
                    int pos = bank * BANK_SIZE;
                    ushort crc = Crc16Calculator.CalculateCRC16(PRG, pos, BANK_SIZE);
                    Console.Write($"Reading CRC of bank #{bank}/{banks} ({100 * bank / banks}%)... ");
                    var crcr = dumper.ReadCpuCrc(0x8000, BANK_SIZE);
                    if (crcr != crc)
                    {
                        Console.WriteLine($"Verification failed: {crcr:X4} != {crc:X4}");
                        if (!silent) Program.PlayErrorSound();
                        wrongCrcSectorsList.Add(bank);
                    }
                    else
                        Console.WriteLine($"OK (CRC = {crcr:X4})");
                }
                if (wrongCrcSectorsList.Any())
                    Console.WriteLine($"Banks with wrong CRC: {string.Join(", ", wrongCrcSectorsList.Distinct().OrderBy(s => s))}");
            }

            if (wrongCrcSectorsList.Any())
                throw new IOException("Cartridge is not writed correctly");
        }
    }
}
