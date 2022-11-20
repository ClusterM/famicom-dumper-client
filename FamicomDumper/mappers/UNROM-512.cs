class UNROM512 : IMapper
{
    public string Name { get => "UNROM-512"; }
    public int Number { get => 30; }
    public byte Submapper { get => 0; }
    public string UnifName { get => null; }
    public int DefaultPrgSize { get => 512 * 1024; }
    public int DefaultChrSize { get => 0; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = (byte)(size / 0x4000);
        Console.Write("Reading last PRG bank... ");
        var lastBank = dumper.ReadCpu(0xC000, 0x4000);
        Console.WriteLine("OK");
        for (int bank = 0; bank < banks - 1; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            // Avoiding bus conflicts            
            var nonConflictable = Enumerable.Range(0, lastBank.Length)
                .Where(a => lastBank[a] == (byte)bank)
                .Select(a => a + 0xC000);
            if (nonConflictable.Any())
            {
                dumper.WriteCpu((ushort)(nonConflictable.First()), (byte)bank);
            }
            else
            {
                // Whatever...
                Console.Write("oops, bus conflict... ");
                dumper.WriteCpu(0xC000, (byte)bank);
            }
            data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
            Console.WriteLine("OK");
        }
        data.AddRange(lastBank);
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        throw new NotSupportedException("This mapper doesn't have a CHR ROM");
    }

    public void EnablePrgRam(IFamicomDumperConnection dumper)
    {
        throw new NotSupportedException("PRG RAM is not supported by this mapper");
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return dumper.GetMirroring();
    }
}
