class Mapper87 : IMapper
{
    public string Name { get => "Mapper 87"; }
    public int Number { get => 87; }
    public int DefaultPrgSize { get => 0x8000; }
    public int DefaultChrSize { get => 0x2000 * 4; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        Console.Write("Reading PRG... ");
        data.AddRange(dumper.ReadCpu(0x8000, size));
        Console.WriteLine("OK");
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x2000;

        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading CHR bank #{bank}/{banks}... ");
            dumper.WriteCpu(0x6000, (byte)(((bank & 1) << 1) | (bank >> 1)));
            data.AddRange(dumper.ReadPpu(0x0000, 0x2000));
            Console.WriteLine("OK");
        }
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return dumper.GetMirroring();
    }
}
