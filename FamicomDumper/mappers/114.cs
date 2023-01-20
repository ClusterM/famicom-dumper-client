class Mapper114 : IMapper
{
    public string Name { get => "MMC3 SG PROT. A"; }
    public int Number { get => 114; }
    public int DefaultPrgSize { get => 512 * 1024; }
    public int DefaultChrSize { get => 256 * 1024; }

    private ushort ScrumbleAddress(ushort address)
    {
        switch (address)
        {
            case 0xA001: return 0x8000;
            case 0xA000: return 0x8001;
            case 0x8000: return 0xA000;
            case 0xC000: return 0xA001;
            case 0x8001: return 0xC000;
            case 0xC001: return 0xC001;
            case 0xE000: return 0xE000;
            case 0xE001: return 0xE001;
        }
        return 0;
    }

    private byte ScrumbleValues(byte value)
    {
        switch (value)
        {
            case 0: return 0;
            case 3: return 1;
            case 1: return 2;
            case 5: return 3;
            case 6: return 4;
            case 7: return 5;
            case 2: return 6;
            case 4: return 7;
        }
        return 0;
    }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x2000;
        dumper.WriteCpu(0x6000, 0);
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG banks #{bank}/{banks}... ");
            dumper.WriteCpu(ScrumbleAddress(0x8000), ScrumbleValues(6));
            dumper.WriteCpu(ScrumbleAddress(0x8001), (byte)bank);
            data.AddRange(dumper.ReadCpu(0x8000, 0x2000));
            Console.WriteLine("OK");
        }
        Console.WriteLine("OK");
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x400;
        if (banks > 512) throw new ArgumentOutOfRangeException("CHR size is too big");
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading CHR banks #{bank}/{banks}... ");
            dumper.WriteCpu(0x6000, (byte)((bank >> 8) & 1));
            dumper.WriteCpu(ScrumbleAddress(0x8000), ScrumbleValues(2));
            dumper.WriteCpu(ScrumbleAddress(0x8001), (byte)bank);
            data.AddRange(dumper.ReadPpu(0x1000, 0x0400));
            Console.WriteLine("OK");
        }
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.MapperControlled;
    }
}
