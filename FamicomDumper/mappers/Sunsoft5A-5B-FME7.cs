class Sunsoft5A_5B_FME7 : IMapper
{
    public string Name { get => "FME-7"; }
    public int Number { get => 69; }
    public byte Submapper { get => 0; }
    public string UnifName { get => null; }
    public int DefaultPrgSize { get => 512 * 1024; }
    public int DefaultChrSize { get => 256 * 1024; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x2000;
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            dumper.WriteCpu(0x8000, 9); // Bank $9 - CPU $8000-$9FFF
            dumper.WriteCpu(0xA000, (byte)bank);
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
            dumper.WriteCpu(0x8000, 0); // Bank $0 - PPU $0000-$03FF
            dumper.WriteCpu(0xA000, (byte)bank);
            data.AddRange(dumper.ReadPpu(0x0000, 0x0400));
            Console.WriteLine("OK");
        }
    }

    public void EnablePrgRam(IFamicomDumperConnection dumper)
    {
        dumper.WriteCpu(0x8000, 8);    // Bank $8 - CPU $6000-$7FFF
        dumper.WriteCpu(0x5103, 0xC0); // PRG RAM Enabled (0x80) + PRG RAM (0x40), bank #0 (should i select #0?)
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.MapperControlled;
    }
}
