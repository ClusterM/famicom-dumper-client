class NROM : IMapper
{
    public string Name { get => "NROM"; }
    public int Number { get => 0; }
    public int DefaultPrgSize { get => 0x8000; }
    public int DefaultChrSize { get => 0x2000; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        Console.Write("Reading PRG... ");
        data.AddRange(dumper.ReadCpu((ushort)(0x10000 - size), size));
        Console.WriteLine("OK");
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        Console.Write("Reading CHR... ");
        data.AddRange(dumper.ReadPpu(0x0000, size));
        Console.WriteLine("OK");
    }

    public void EnablePrgRam(IFamicomDumperConnection dumper)
    {
        // Actually PRG RAM is present in Family Basic
    }
}
