namespace com.clusterrr.Famicom.Mappers
{
    class MMC5 : IMapper
    {
        public string Name
        {
            get { return "MMC5"; }
        }

        public int Number
        {
            get { return 5; }
        }

        public string UnifName
        {
            get { return null; }
        }

        public int DefaultPrgSize
        {
            get { return 1024 * 1024; }
        }

        public int DefaultChrSize
        {
            get { return 1024 * 1024; }
        }

        public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x2000;
            dumper.WriteCpu(0x5100, 3); // bank mode #3, four 8KB banks
            for (var bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                dumper.WriteCpu(0x5114, (byte)(bank | 0x80));
                data.AddRange(dumper.ReadCpu(0x8000, 0x2000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x2000;
            dumper.WriteCpu(0x2000, 0); // 8x8 sprites mode
            dumper.WriteCpu(0x5101, 0); // bank mode #0, one 8KB bank
            for (var bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading CHR bank #{0}... ", bank);
                dumper.WriteCpu(0x5127, (byte)bank);
                data.AddRange(dumper.ReadPpu(0x0000, 0x2000));
                Console.WriteLine("OK");
            }
        }

        public void EnablePrgRam(IFamicomDumperConnection dumper)
        {
            dumper.WriteCpu(0x5102, 0x02); // PRG-RAM protection
            dumper.WriteCpu(0x5103, 0x01); // PRG-RAM protection
            dumper.WriteCpu(0x5100, 3); // bank mode #3, four 8KB banks
            dumper.WriteCpu(0x5113, 7); // PRG-RAM bank #7
        }
    }
}
