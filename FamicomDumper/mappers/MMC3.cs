class MMC3 : IMapper
{
    public string Name { get => "MMC3"; }
    public int Number { get => 4; }
    public int DefaultPrgSize { get => 512 * 1024; }
    public int DefaultChrSize { get => 256 * 1024; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x2000;
        if (banks > 256) throw new ArgumentOutOfRangeException("PRG size is too big");
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG banks #{bank}/{banks}... ");
            dumper.WriteCpu(0x8000, 6, (byte)bank);
            data.AddRange(dumper.ReadCpu(0x8000, 0x2000));
            Console.WriteLine("OK");
        }
        Console.WriteLine("OK");
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x400;
        if (banks > 256) throw new ArgumentOutOfRangeException("CHR size is too big");
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading CHR banks #{bank}/{banks}... ");
            dumper.WriteCpu(0x8000, 2, (byte)bank);
            data.AddRange(dumper.ReadPpu(0x1000, 0x0400));
            Console.WriteLine("OK");
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
