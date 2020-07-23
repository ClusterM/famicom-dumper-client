namespace com.clusterrr.Famicom.Mappers
{
    class Mapper202 : IMapper
    {
        public string Name
        {
            get { return "Mapper 202"; }
        }

        public int Number
        {
            get { return 202; }
        }

        public string UnifName
        {
            get { return null; }
        }

        public int DefaultPrgSize
        {
            get { return 8 * 0x4000; }
        }

        public int DefaultChrSize
        {
            get { return 8 * 0x2000; }
        }

        public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x4000;
            for (var bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                dumper.WriteCpu((ushort)(0x8000 | (bank << 1)), 0);
                data.AddRange(dumper.ReadCpu(0xC000, 0x4000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x2000;
            for (var bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading CHR bank #{0}... ", bank);
                dumper.WriteCpu((ushort)(0x8000 | (bank << 1)), 0);
                data.AddRange(dumper.ReadPpu(0x0000, 0x2000));
                Console.WriteLine("OK");
            }
        }

        public void EnablePrgRam(IFamicomDumperConnection dumper)
        {
            throw new NotSupportedException("SRAM is not supported by this mapper");
        }
    }
}
