﻿class MMC4 : IMapper
{
    public string Name { get => "MMC4"; }
    public int Number { get => 10; }
    public int DefaultPrgSize { get => 256 * 1024; }
    public int DefaultChrSize { get => 0; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x4000;

        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            dumper.WriteCpu(0xA000, (byte)bank);
            data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
            Console.WriteLine("OK");
        }
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x1000;

        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading CHR bank #{bank}/{banks}... ");
            dumper.WriteCpu(0xB000, (byte)bank); // CHR ROM $FD/0000 bank select
            dumper.WriteCpu(0xC000, (byte)bank); // CHR ROM $FE/0000 bank select
            data.AddRange(dumper.ReadPpu(0x0000, 0x1000));
            Console.WriteLine("OK");
        }
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.MapperControlled;
    }
}
