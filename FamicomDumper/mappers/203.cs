class Mapper203 : IMapper
{
    public string Name { get => "Mapper 203"; }
    public int Number { get => 203; }
    public int DefaultPrgSize { get => 4 * 0x4000; }
    public int DefaultChrSize { get => 4 * 0x2000; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x4000;
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            dumper.WriteCpu(0x8000, (byte)(bank << 2));
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
            dumper.WriteCpu(0x8000, (byte)bank);
            data.AddRange(dumper.ReadPpu(0x0000, 0x2000));
            Console.WriteLine("OK");
        }
    }
}
