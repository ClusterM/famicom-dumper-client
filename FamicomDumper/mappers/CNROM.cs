class CNROM : IMapper
{
    public string Name { get => "CNROM"; }
    public int Number { get => 3; }
    public byte Submapper { get => 0; }
    public string UnifName { get => null; }
    public int DefaultPrgSize { get => 0x8000; }
    public int DefaultChrSize { get => 0x2000 * 4; }

    byte[] prg;

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        Console.Write("Reading PRG... ");
        prg = dumper.ReadCpu(0x8000, size);
        data.AddRange(prg);
        Console.WriteLine("OK");
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        if (prg == null)
        {
            prg = dumper.ReadCpu(0x8000, DefaultPrgSize);
        }
        var banks = size / 0x2000;

        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading CHR bank #{bank}/{banks}... ");
            // Avoiding bus conflicts
            for (var i = 0; i < prg.Length; i++)
            {
                if (prg[i] == bank)
                {
                    dumper.WriteCpu((ushort)(0x8000 + i), (byte)bank);
                    break;
                }
            }
            data.AddRange(dumper.ReadPpu(0x0000, 0x2000));
            Console.WriteLine("OK");
        }
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
