class VRC3 : IMapper
{
    public string Name { get => "VRC3"; }
    public int Number { get => 73; }
    public int DefaultPrgSize { get => 128 * 1024; }
    public int DefaultChrSize { get => 0; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x4000;

        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            dumper.WriteCpu(0xF000, (byte)bank);
            data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
            Console.WriteLine("OK");
        }
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.MapperControlled;
    }
}
