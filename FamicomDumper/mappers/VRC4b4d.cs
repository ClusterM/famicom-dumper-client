namespace com.clusterrr.Famicom.Mappers
{
    class VRC4b4d : IMapper
    {
        public string Name
        {
            get { return "VRC4b/VRC4d"; }
        }

        public int Number
        {
            get { return 25; }
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
            get { return 512 * 1024; }
        }

        public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x2000;

            dumper.WriteCpu(0x9001 | 0x9004, 0); // disable swap mode
            for (var bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                dumper.WriteCpu(0x8000, (byte)bank); // PRG Select 0
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
                dumper.WriteCpu(0xB000, (byte)(bank & 0x0F)); // CHR Select 0 low
                dumper.WriteCpu(0xB002 | 0xB008, (byte)(bank >> 4)); // CHR Select 0 high
                data.AddRange(dumper.ReadPpu(0x0000, 0x0400));
                Console.WriteLine("OK");
            }
        }

        public void EnablePrgRam(IFamicomDumperConnection dumper)
        {
            throw new NotSupportedException("SRAM is not supported by this mapper");
        }
    }
}
