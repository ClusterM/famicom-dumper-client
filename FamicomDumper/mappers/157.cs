// Bandai DATACH Joint ROM System

class BANDAI_LZ93D50_BARCODE : IMapper
{
    public string Name { get => "BANDAI BARCODE"; }
    public int Number { get => 157; }
    public int DefaultPrgSize { get => 256 * 1024; }
    public int DefaultChrSize { get => 0; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x4000;

        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            dumper.WriteCpu(0x8008, (byte)(bank & 0xFF));
            data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
            Console.WriteLine("OK");
        }
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.MapperControlled;
    }
}
