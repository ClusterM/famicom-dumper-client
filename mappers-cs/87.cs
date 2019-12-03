using System;
using System.Collections.Generic;

namespace Cluster.Famicom.Mappers
{
    public class Mapper87 : IMapper
    {
        public string Name
        {
            get { return "Mapper 87"; }
        }

        public int Number
        {
            get { return 87; }
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

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            Console.Write("Reading PRG... ");
            data.AddRange(dumper.ReadCpu(0x8000, size));
            Console.WriteLine("OK");
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            byte banks = (byte)(size / 0x2000);

            for (int bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading CHR bank #{0}... ", bank);
                dumper.WriteCpu(0x6000, (byte)(((bank&1) << 1) | (bank >> 1)));
                data.AddRange(dumper.ReadPpu(0x0000, 0x2000));
                Console.WriteLine("OK");
            }
        }

        public void EnablePrgRam(FamicomDumperConnection dumper)
        {
            Console.WriteLine("Warning: SRAM is not supported by this mapper");
        }
    }
}
