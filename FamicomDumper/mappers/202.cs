class Mapper202 : IMapper
{
    public string Name { get => "Mapper 202"; }
    public int Number { get => 202; }
    public int DefaultPrgSize { get => 8 * 0x4000; }
    public int DefaultChrSize { get => 8 * 0x2000; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x4000;
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            dumper.WriteCpu((ushort)(0x8000 | (bank << 1)), 0);
            data.AddRange(dumper.ReadCpu(0xC000, 0x4000));
            Console.WriteLine("OK");
        }
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x2000;
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading CHR bank #{bank}/{banks}... ");
            dumper.WriteCpu((ushort)(0x8000 | (bank << 1)), 0);
            data.AddRange(dumper.ReadPpu(0x0000, 0x2000));
            Console.WriteLine("OK");
        }
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.MapperControlled;
    }
}
