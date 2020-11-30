﻿namespace com.clusterrr.Famicom.Mappers
{
    class Namco_129_163 : IMapper
    {
        public string Name
        {
            get { return "Namco 129/163"; }
        }

        public int Number
        {
            get { return 19; }
        }

        public byte Submapper
        {
            get { return 0; }
        }

        public string UnifName
        {
            get { return null; }
        }

        public int DefaultPrgSize
        {
            get { return 512 * 1024; }
        }

        public int DefaultChrSize
        {
            get { return 256 * 1024; }
        }

        public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x2000;

            for (var bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                dumper.WriteCpu(0xE000, (byte)bank);
                data.AddRange(dumper.ReadCpu(0x8000, 0x2000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x400;

            for (var bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading CHR bank #{0}... ", bank);
                dumper.WriteCpu(0x8000, (byte)bank);
                data.AddRange(dumper.ReadPpu(0x0000, 0x0400));
                Console.WriteLine("OK");
            }
        }

        public void EnablePrgRam(IFamicomDumperConnection dumper)
        {
            dumper.WriteCpu(0xF800, 0x40);
        }

        public NesFile.MirroringType GetMirroring(IFamicomDumperConnection dumper)
        {
            return NesFile.MirroringType.MapperControlled;
        }
    }
}
