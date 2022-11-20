class BMC1024CA1 : IMapper
{
    public string Name { get => "BMC-10-24-C-A1"; }
    public int Number { get => -1; }
    public byte Submapper { get => 0; }
    public string UnifName { get => "BMC-10-24-C-A1"; }
    public int DefaultPrgSize { get => 1024 * 1024; }
    public int DefaultChrSize { get => 512 * 1024; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var outbanks = size / (256 * 1024);

        for (var outbank = 0; outbank < outbanks; outbank += 1)
        {
            dumper.Reset();
            dumper.WriteCpu(0xA001, 0x80); // RAM protect
            dumper.WriteCpu((ushort)(0x6828 | (outbank << 1)), 0x00);
            dumper.WriteCpu(0xA001, 0); // disable W-RAM
            const int banks = 32;
            for (var bank = 0; bank < banks; bank += 2)
            {
                Console.Write($"Reading PRG banks #{outbank}|{bank} and #{outbank}|{bank + 1}... ");
                dumper.WriteCpu(0x8000, 6, (byte)bank);
                dumper.WriteCpu(0x8000, 7, (byte)(bank | 1));
                data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
                Console.WriteLine("OK");
            }
            Console.WriteLine("OK");
        }
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var outbanks = size / (256 * 1024);
        for (var outbank = 0; outbank < outbanks; outbank += 1)
        {
            dumper.Reset();
            dumper.WriteCpu(0xA001, 0x80); // RAM protect
            dumper.WriteCpu((ushort)(0x6828 | (outbank << 1)), 0x00);
            dumper.WriteCpu(0xA001, 0); // disable W-RAM

            const int banks = 256;
            for (var bank = 0; bank < banks; bank += 4)
            {
                Console.Write($"Reading CHR banks #{outbank}|{bank}, #{outbank}|{bank + 1}, #{outbank}|{bank + 2}, #{outbank}|{bank + 3}... ");
                dumper.WriteCpu(0x8000, 2, (byte)bank);
                dumper.WriteCpu(0x8000, 3, (byte)(bank | 1));
                dumper.WriteCpu(0x8000, 4, (byte)(bank | 2));
                dumper.WriteCpu(0x8000, 5, (byte)(bank | 3));
                data.AddRange(dumper.ReadPpu(0x1000, 0x1000));
                Console.WriteLine("OK");
            }
        }
    }

    public void EnablePrgRam(IFamicomDumperConnection dumper)
    {
        dumper.WriteCpu(0xA001, 0x80);
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.MapperControlled;
    }
}
