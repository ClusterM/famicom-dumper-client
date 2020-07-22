﻿namespace com.clusterrr.Famicom.Mappers
{
    class MMC2 : IMapper
    {
        public string Name
        {
            get { return "MMC2"; }
        }

        public int Number
        {
            get { return 9; }
        }

        public string UnifName
        {
            get { return null; }
        }

        public int DefaultPrgSize
        {
            get { return 128 * 1024; }
        }

        public int DefaultChrSize
        {
            get { return 0; }
        }

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            int banks = size / 0x2000;
            
            for (int bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                dumper.WriteCpu(0xA000, (byte)bank);
                data.AddRange(dumper.ReadCpu(0x8000, 0x2000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            int banks = size / 0x1000;
            
            for (int bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading CHR bank #{0}... ", bank, bank);
                dumper.WriteCpu(0xB000, (byte)bank); // CHR ROM $FD/0000 bank select
                dumper.WriteCpu(0xC000, (byte)bank); // CHR ROM $FE/0000 bank select
                data.AddRange(dumper.ReadPpu(0x0000, 0x1000));
                Console.WriteLine("OK");
            }
        }

        public void EnablePrgRam(FamicomDumperConnection dumper)
        {
            Console.WriteLine("Warning: SRAM is not supported by this mapper");
        }
    }
}