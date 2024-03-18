class BNROM : IMapper
{
    public string Name { get => "BNROM"; }
    public int Number { get => 34; }
    public int DefaultPrgSize { get => 128 * 1024; } // (mapper implementations may support up to 512 KB or 8 MB)

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
            for (int i = 0; i < lastBank.Length; i++)
            {
                if (lastBank[i] == bank)
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
}
