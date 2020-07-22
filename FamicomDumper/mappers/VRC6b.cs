namespace com.clusterrr.Famicom.Mappers
{
    class VRC6b : IMapper
    {
        public string Name
        {
            get { return "VRC6b"; }
        }

        public int Number
        {
            get { return 26; }
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
            get { return 256 * 1024; }
        }

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            int banks = size / 0x4000;
            
            for (int bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                dumper.WriteCpu(0x8000, (byte)bank);
                data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            dumper.WriteCpu(0xB003, 0xE0);
            int banks = size / 0x400;
            
            for (int bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading CHR bank #{0}... ", bank, bank);
                dumper.WriteCpu(0xD000, (byte)bank);
                data.AddRange(dumper.ReadPpu(0x0000, 0x0400));
                Console.WriteLine("OK");
            }
        }

        public void EnablePrgRam(FamicomDumperConnection dumper)
        {
            dumper.WriteCpu(0xB003, 0xE0);
        }
    }
}
