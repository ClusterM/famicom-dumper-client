namespace com.clusterrr.Famicom.Mappers
{
    class VRC3 : IMapper
    {
        public string Name
        {
            get { return "VRC3"; }
        }

        public int Number
        {
            get { return 73; }
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

        public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x4000;

            for (var bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                dumper.WriteCpu(0xF000, (byte)bank);
                data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            throw new NotSupportedException("This mapper doesn't have a CHR ROM");
        }

        public void EnablePrgRam(IFamicomDumperConnection dumper)
        {
            throw new NotSupportedException("SRAM is not supported by this mapper");
        }
    }
}
