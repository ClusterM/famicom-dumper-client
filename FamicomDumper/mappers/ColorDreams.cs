class CNROM : IMapper
{
    public string Name { get => "ColorDreams"; }
    public int Number { get => 11; }
    public int DefaultPrgSize { get => 0x8000 * 4; }
    public int DefaultChrSize { get => 0x2000 * 16; }

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
            var regValue = (byte)(bank & 0b11);
            for (int i = 0; i < lastBank.Length; i++)
            {
                if (lastBank[i] == regValue)
                {
                    dumper.WriteCpu((ushort)(0x8000 + i), regValue);
                    noBusConflict = true;
                    break;
                }
            }
            if (!noBusConflict) // Whatever...
                dumper.WriteCpu((ushort)0x8000, regValue);
            lastBank = dumper.ReadCpu(0x8000, 0x8000);
            data.AddRange(lastBank);
            Console.WriteLine("OK");
        }
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        Console.Write("Reading random PRG... ");
        var prg = dumper.ReadCpu(0x8000, DefaultPrgSize);
        Console.WriteLine("OK");

        var banks = size / 0x2000;
        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading CHR bank #{bank}/{banks}... ");
            // Avoiding bus conflicts
            bool noBusConflict = false;
            var regValue = (byte)((bank & 0b1111) << 4);
            for (var i = 0; i < prg.Length; i++)
            {
                if (prg[i] == regValue)
                {
                    dumper.WriteCpu((ushort)(0x8000 + i), regValue);
                    noBusConflict = true;
                    break;
                }
            }
            if (!noBusConflict) // Whatever...
                dumper.WriteCpu((ushort)0x8000, regValue);
            data.AddRange(dumper.ReadPpu(0x0000, 0x2000));
            Console.WriteLine("OK");
        }
    }
}
