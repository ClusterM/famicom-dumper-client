namespace com.clusterrr.Famicom.Mappers
{
    class Mapper57 : IMapper
    {
        public string Name
        {
            get { return "Mapper 57"; }
        }

        public int Number
        {
            get { return 57; }
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
                dumper.WriteCpu(0x8800, (byte)(bank << 5));
                data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x2000;
            for (var bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading CHR bank #{0}... ", bank);
                dumper.WriteCpu(0x8000, (byte)bank);
                dumper.WriteCpu(0x8800, (byte)bank);
                Console.WriteLine("OK");
                data.AddRange(dumper.ReadPpu(0x0000, 0x2000));
            }
        }

        public void EnablePrgRam(IFamicomDumperConnection dumper)
        {
            throw new NotSupportedException("SRAM is not supported by this mapper");
        }
    }
}
