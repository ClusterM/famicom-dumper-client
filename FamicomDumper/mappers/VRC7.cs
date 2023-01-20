class VRC7 : IMapper
{
    public string Name { get => "VRC7"; }
    public int Number { get => 85; }
    public int DefaultPrgSize { get => 512 * 1024; }
    public int DefaultChrSize { get => 256 * 1024; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x2000;

        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            dumper.WriteCpu(0x8000, (byte)bank);
            data.AddRange(dumper.ReadCpu(0x8000, 0x2000));
            Console.WriteLine("OK");
        }
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x400;

        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading CHR bank #{bank}/{banks}... ");
            dumper.WriteCpu(0xA000, (byte)bank);
            data.AddRange(dumper.ReadPpu(0x0000, 0x0400));
            Console.WriteLine("OK");
        }
    }

    public void EnablePrgRam(IFamicomDumperConnection dumper)
    {
        dumper.WriteCpu(0xE000, 0x80);
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.MapperControlled;
    }
}
