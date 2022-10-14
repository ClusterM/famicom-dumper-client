class CNROM : IMapper
{
    public string Name
    {
        get { return "ColorDreams"; }
    }

    public int Number
    {
        get { return 11; }
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
        get { return 0x8000 * 4; }
    }

    public int DefaultChrSize
    {
        get { return 0x2000 * 16; }
    }

    byte[] prg;

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        Console.Write("Reading random PRG... ");
        byte[] lastBank = dumper.ReadCpu(0x8000, 0x8000);
        Console.WriteLine("OK");

        var banks = size / 0x8000;
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            // Avoiding bus conflicts
            bool noBusConflict = false;
            var regValue = bank & 0b11;
            for (int i = 0; i < lastBank.Length; i++)
            {
                if (lastBank[i] == regValue)
                {
                    dumper.WriteCpu((ushort)(0x8000 + i), (byte)bank);
                    noBusConflict = true;
                    break;
                }
            }
            if (!noBusConflict) // Whatever...
                dumper.WriteCpu((ushort)0x8000, (byte)bank);
            lastBank = dumper.ReadCpu(0x8000, 0x8000);
            data.AddRange(lastBank);
            Console.WriteLine("OK");
        }
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
            var regValue = (bank & 0b1111) << 4;
            for (var i = 0; i < prg.Length; i++)
            {
                if (prg[i] == (bank & 0b1111))
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

    public NesFile.MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return dumper.GetMirroring();
    }
}
