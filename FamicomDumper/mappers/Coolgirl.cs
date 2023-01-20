class Coolgirl : IMapper
{
    public string Name { get => "COOLGIRL"; }
    public int Number { get => 342; }
    public string UnifName { get => "COOLGIRL"; }
    public int DefaultPrgSize { get => 1024 * 1024 * 128; }
    public int DefaultPrgRamSize { get => 32 * 1024; }
    public int DefaultChrRamSize { get => 512 * 1024; }

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

    public void EnablePrgRam(IFamicomDumperConnection dumper)
    {
        dumper.Reset();
        dumper.WriteCpu(0x5007, 0x01); // enable SRAM
        dumper.WriteCpu(0x5005, 0x02); // select bank
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.MapperControlled;
    }
}
