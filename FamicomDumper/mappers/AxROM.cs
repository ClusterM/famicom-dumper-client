using System;
using System.Collections.Generic;

namespace com.clusterrr.Famicom.Mappers
{
    public class AxROM : IMapper
    {
        public string Name
        {
            get { return "AxROM"; }
        }

        public int Number
        {
            get { return 7; }
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
            get { return 0; }
        }

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            Console.Write("Reading random PRG... ");
            byte[] lastBank = dumper.ReadCpu(0x8000, 0x8000);
            Console.WriteLine("OK");

            byte banks = (byte)(size / 0x8000);
            for (int bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                // Avoiding bus conflicts
                bool noBusConflict = false;
                for (int i = 0; i < lastBank.Length; i++)
                {
                    if (lastBank[i] == bank)
                    {
                        dumper.WriteCpu((ushort)(0x8000 + i), (byte)bank);
                        noBusConflict = true;
                        break;
                    }
                }
                if (!noBusConflict) // Whatever...
                    dumper.WriteCpu((ushort)0x8000, (byte)bank);
                lastBank = dumper.ReadCpu(0x8000, 0x8000);
                data.AddRange(lastBank);
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
        }

        public void EnablePrgRam(FamicomDumperConnection dumper)
        {
            Console.WriteLine("Warning: SRAM is not supported by this mapper");
        }
    }
}
