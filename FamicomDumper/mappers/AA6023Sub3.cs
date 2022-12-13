class Coolboy : IMapper
{
    public string Name { get => "AA6023B submapper 3"; }
    public int Number { get => 268; }
    public byte Submapper { get => 3; }
    public string UnifName { get => null; }
    public int DefaultPrgSize { get => 32 * 1024 * 1024; }
    public int DefaultChrSize { get => 0; }
    public int DefaultPrgRamSize { get => 8 * 1024; }
    public int DefaultChrRamSize { get => 256 * 1024; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        dumper.Reset();
        int banks = size / 0x4000;

        for (var bank = 0; bank < banks; bank++)
        {
            var r0 = (byte)(
                ((bank >> 3) & 0x07) // 5, 4, 3 bits
                | (((bank >> 9) & 0x03) << 4) // 10, 9 bits
                | (1 << 6) // resets 4th mask bit
            );
            var r1 = (byte)(
                (((bank >> 8) & 1) << 1) // 8
                | (((bank >> 7) & 1) << 2) // 7
                | (((bank >> 6) & 1) << 3) // 6
                | (1 << 4) // PRG mask 32KB, inverted
                | (1 << 7) // resets 5th mask bit
            );
            var r2 = (byte)0;
            var r3 = (byte)(
                (1 << 4) // NROM mode
                | ((bank & 7) << 1)  // 2, 1, 0 bits
            );
            dumper.WriteCpu(0x5000, r0, r1, r2, r3);

            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
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
        dumper.WriteCpu(0xA001, 0x00);
        dumper.WriteCpu(0x5003, 0x80);
        dumper.WriteCpu(0xA001, 0x80);
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.MapperControlled;
    }
}
