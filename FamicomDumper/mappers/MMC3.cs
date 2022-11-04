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

    public byte Submapper
    {
        get { return 0; }
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
        if (banks > 256) throw new ArgumentOutOfRangeException("size", "CHR size is too big");
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
