class GTROM : IMapper
{
    public string Name { get => "GTROM"; }
    public int Number { get => 111; }
    public int DefaultPrgSize { get => 512 * 1024; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x8000;
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            dumper.WriteCpu(0x5000, (byte)bank);
            data.AddRange(dumper.ReadCpu(0x8000, 0x8000));
            Console.WriteLine("OK");
        }
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.FourScreenVram;
    }
}
