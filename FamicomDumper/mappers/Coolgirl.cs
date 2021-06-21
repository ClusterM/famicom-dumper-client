class Coolgirl : IMapper
{
    public string Name
    {
        get { return "COOLGIRL"; }
    }

    public int Number
    {
        get { return -1; }
    }

    public byte Submapper
    {
        get { return 0; }
    }

    public string UnifName
    {
        get { return "COOLGIRL"; }
    }

    public int DefaultPrgSize
    {
        get { return 1024 * 1024 * 128; }
    }

    public int DefaultChrSize
    {
        get { return 0; }
    }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x8000;
        Console.Write("Reset... ");
        dumper.Reset();
        Console.WriteLine("OK");
        dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
        for (int bank = 0; bank < banks; bank++)
        {
            var r0 = (byte)(bank >> 7);
            var r1 = (byte)(bank << 1);
            dumper.WriteCpu(0x5000, r0);
            dumper.WriteCpu(0x5001, r1);

            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            data.AddRange(dumper.ReadCpu(0x8000, 0x8000));
            Console.WriteLine("OK");
        }
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        throw new NotSupportedException("This mapper doesn't have a CHR ROM");
    }

    public void EnablePrgRam(IFamicomDumperConnection dumper)
    {
        dumper.Reset();
        dumper.WriteCpu(0x5007, 0x01); // enable SRAM
        dumper.WriteCpu(0x5005, 0x02); // select bank
    }

    public NesFile.MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return NesFile.MirroringType.MapperControlled;
    }
}
