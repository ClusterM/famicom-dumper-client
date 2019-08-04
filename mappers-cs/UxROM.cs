using System;
using System.Collections.Generic;

namespace Cluster.Famicom.Mappers
{
    public class UxROM : IMapper
    {
        public string Name
        {
            get { return "UxROM"; }
        }

        public int Number
        {
            get { return 2; }
        }

        public string UnifName
        {
            get { return null; }
        }

        public int DefaultPrgSize
        {
            get { return 256 * 1024; }
        }

        public int DefaultChrSize
        {
            get { return 0; /* 0x2000; */ }
        }

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            byte banks = (byte)(size / 0x4000);
            Console.Write("Reading last PRG bank... ");
            byte[] lastBank = dumper.ReadCpu(0xC000, 0x4000);
            Console.WriteLine("OK");
            for (int bank = 0; bank < banks-1; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                // Avoiding bus conflicts
                bool noBusConflict = false;
                for (int i = 0; i < lastBank.Length; i++)
                {
                    if (lastBank[i] == bank)
                    {
                        dumper.WriteCpu((ushort)(0xC000 + i), (byte)bank);
                        noBusConflict = true;
                        break;
                    }
                }
                if (!noBusConflict) // Whatever...
                    dumper.WriteCpu((ushort)0x8000, (byte)bank);
                data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
                Console.WriteLine("OK");
            }
            data.AddRange(lastBank);
            
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            Console.Write("Reading CHR... ");
            data.AddRange(dumper.ReadPpu(0x0000, size));
            Console.WriteLine("OK");
        }

        public void EnablePrgRam(FamicomDumperConnection dumper)
        {
            Console.WriteLine("Warning: SRAM is not supported by this mapper");
        }
    }
}
