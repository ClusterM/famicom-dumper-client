class MMC1 : IMapper
{
    public string Name
    {
        get { return "MMC1"; }
    }

    public int Number
    {
        get { return 1; }
    }

    public byte Submapper
    {
        get { return 0; }
    }

    public string UnifName
    {
        get { return null; }
    }

    public int DefaultPrgSize
    {
        get { return 256 * 1024; }
    }

    public int DefaultChrSize
    {
        get { return 128 * 1024; }
    }

    void WriteMMC1(IFamicomDumperConnection dumper, ushort address, byte data)
    {
        var buffer = new byte[5];
        for (var i = 0; i < 5; i++)
        {
            buffer[i] = (byte)(data >> i);
        }
        dumper.WriteCpu(address, buffer);
    }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        dumper.WriteCpu(0x8000, 0x80);
        WriteMMC1(dumper, 0x8000, 0b11100);

        var banks = size / 0x4000;

        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            WriteMMC1(dumper, 0xE000, (byte)bank);
            data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
            Console.WriteLine("OK");
        }
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        dumper.WriteCpu(0x8000, 0x80);
        WriteMMC1(dumper, 0x8000, 0b11100);

        var banks = size / 0x1000;

        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading CHR bank #{bank}/{banks}... ");
            WriteMMC1(dumper, 0xA000, (byte)bank);
            data.AddRange(dumper.ReadPpu(0x0000, 0x1000));
            Console.WriteLine("OK");
        }
    }

    public void EnablePrgRam(IFamicomDumperConnection dumper)
    {
        dumper.WriteCpu(0x8000, 0x80);
        WriteMMC1(dumper, 0xE000, 0x00);
    }

    public NesFile.MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return NesFile.MirroringType.MapperControlled;
    }
}
