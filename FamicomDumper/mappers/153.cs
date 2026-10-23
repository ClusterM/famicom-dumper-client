// The only game "Famicom Jump II - Saikyou no 7 Nin (Japan)" uses this mapper

class BANDAI_LZ93D50_WRAM_CHRRAM_PRGROM : IMapper
{
    public string Name { get => "153 BANDAI SRAM"; }
    public int Number { get => 153; }
    public int DefaultPrgSize { get => 512 * 1024; }
    public int DefaultChrSize { get => 0; }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = size / 0x4000;

        for (var bank = 0; bank < banks; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");

            var outerBank = (byte)((bank & 0x10) >> 4);
            dumper.WriteCpu(0x8000, outerBank);
            dumper.WriteCpu(0x8001, outerBank);
            dumper.WriteCpu(0x8002, outerBank);
            dumper.WriteCpu(0x8003, outerBank);

            dumper.WriteCpu(0x8008, (byte)(bank & 0xF));
            data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
            Console.WriteLine("OK");
        }
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return MirroringType.MapperControlled;
    }
}
