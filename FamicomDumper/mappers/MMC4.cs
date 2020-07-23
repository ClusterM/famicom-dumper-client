namespace com.clusterrr.Famicom.Mappers
{
    class MMC4 : IMapper
    {
        public string Name
        {
            get { return "MMC4"; }
        }

        public int Number
        {
            get { return 10; }
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

        public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x4000;

            for (var bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                dumper.WriteCpu(0xA000, (byte)bank);
                data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x1000;

            for (var bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading CHR bank #{0}... ", bank, bank);
                dumper.WriteCpu(0xB000, (byte)bank); // CHR ROM $FD/0000 bank select
                dumper.WriteCpu(0xC000, (byte)bank); // CHR ROM $FE/0000 bank select
                data.AddRange(dumper.ReadPpu(0x0000, 0x1000));
                Console.WriteLine("OK");
            }
        }

        public void EnablePrgRam(IFamicomDumperConnection dumper)
        {
            throw new NotSupportedException("SRAM is not supported by this mapper");
        }
    }
}
