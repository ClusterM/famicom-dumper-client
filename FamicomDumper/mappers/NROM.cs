namespace com.clusterrr.Famicom.Mappers
{
    class NROM : IMapper
    {
        public string Name
        {
            get { return "NROM"; }
        }

        public int Number
        {
            get { return 0; }
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
            get { return 0x2000; }
        }

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            Console.Write("Reading PRG... ");
            data.AddRange(dumper.ReadCpu(0x8000, size));
            Console.WriteLine("OK");
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            Console.Write("Reading CHR... ");
            data.AddRange(dumper.ReadPpu(0x0000, size));
            Console.WriteLine("OK");
        }

        public void EnablePrgRam(FamicomDumperConnection dumper)
        {
            Console.WriteLine("Warning: SRAM is not supported by this mapper");
        }
    }
}
