namespace com.clusterrr.Famicom.Mappers
{
    class MMC3 : IMapper
    {
        public string Name
        {
            get { return "MMC3"; }
        }

        public int Number
        {
            get { return 4; }
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
            if (banks > 256) throw new ArgumentOutOfRangeException("size", "PRG size is too big");
            for (var bank = 0; bank < banks - 2; bank += 2)
            {
                Console.Write("Reading PRG banks #{0} and #{1}... ", bank, bank + 1);
                dumper.WriteCpu(0x8000, new byte[] { 6, (byte)bank });
                dumper.WriteCpu(0x8000, new byte[] { 7, (byte)(bank | 1) });
                data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
                Console.WriteLine("OK");
            }
            Console.Write("Reading last PRG banks #{0} and #{1}... ", banks - 2, banks - 1);
            data.AddRange(dumper.ReadCpu(0xC000, 0x4000));
            Console.WriteLine("OK");
        }

        public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x400;
            if (banks > 256) throw new ArgumentOutOfRangeException("size", "CHR size is too big");
            for (var bank = 0; bank < banks; bank += 4)
            {
                Console.Write("Reading CHR banks #{0}, #{1}, #{2}, #{3}... ", bank, bank + 1, bank + 2, bank + 3);
                dumper.WriteCpu(0x8000, new byte[] { 2, (byte)bank });
                dumper.WriteCpu(0x8000, new byte[] { 3, (byte)(bank | 1) });
                dumper.WriteCpu(0x8000, new byte[] { 4, (byte)(bank | 2) });
                dumper.WriteCpu(0x8000, new byte[] { 5, (byte)(bank | 3) });
                data.AddRange(dumper.ReadPpu(0x1000, 0x1000));
                Console.WriteLine("OK");
            }
        }

        public void EnablePrgRam(IFamicomDumperConnection dumper)
        {
            dumper.WriteCpu(0xA001, 0x80);
        }
    }
}
