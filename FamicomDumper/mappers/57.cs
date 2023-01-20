class Mapper57 : IMapper
{
    public string Name { get => "Mapper 57"; }
    public int Number { get => 57; }
    public int DefaultPrgSize { get => 8 * 0x4000; }
    public int DefaultChrSize { get => 8 * 0x2000; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x4000;
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            dumper.WriteCpu(0x8800, (byte)(bank << 5));
            data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
            Console.WriteLine("OK");
        }
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x2000;
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading CHR bank #{bank}/{banks}... ");
            dumper.WriteCpu(0x8000, (byte)(0b10000000 | ((bank & 0b00001000) << 3)));
            dumper.WriteCpu(0x8800, (byte)(bank & 0b00000111));
            Console.WriteLine("OK");
            data.AddRange(dumper.ReadPpu(0x0000, 0x2000));
        }
    }

    public void EnablePrgRam(IFamicomDumperConnection dumper)
    {
        throw new NotSupportedException("PRG RAM is not supported by this mapper");
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.MapperControlled;
    }
}
