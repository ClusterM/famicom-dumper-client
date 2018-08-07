using System;
using System.Collections.Generic;

namespace Cluster.Famicom.Mappers
{
    public class CNROM : IMapper
    {
        public string Name
        {
            get { return "CNROM"; }
        }

        public int Number
        {
            get { return 3; }
        }

        public string UnifName
        {
            get { return null; }
        }

        public int DefaultPrgSize
        {
            get { return 0x8000; }
        }

        public int DefaultChrSize
        {
            get { return 0x2000*4; }
        }

        byte[] prg;

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            Console.Write("Dumping PRG... ");
            prg = dumper.ReadCpu(0x8000, size);
            data.AddRange(prg);
            Console.WriteLine("OK");
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            if (prg == null)
            {
                prg = dumper.ReadCpu(0x8000, DefaultPrgSize);
            }
            byte banks = (byte)(size / 0x2000);

            for (int bank = 0; bank < banks; bank++)
            {
                Console.Write("Dumping CHR bank #{0}... ", bank);
                for (int i = 0; i < prg.Length; i++)
                {
                    if (prg[i] == bank)
                    {
                        dumper.WriteCpu((ushort)(0x8000 + i), (byte)bank);
                        break;
                    }
                }
                byte[] d = dumper.ReadPpu(0x0000, 0x2000);
                data.AddRange(d);
                Console.WriteLine("OK");
            }
        }

        public void EnablePrgRam(FamicomDumperConnection dumper)
        {
            Console.WriteLine("Warning: SRAM is not supported by this mapper");
        }
    }
}
