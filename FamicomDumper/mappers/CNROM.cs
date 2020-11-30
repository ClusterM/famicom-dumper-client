namespace com.clusterrr.Famicom.Mappers
{
    class CNROM : IMapper
    {
        public string Name
        {
            get { return "CNROM"; }
        }

        public int Number
        {
            get { return 3; }
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
            get { return 0x8000; }
        }

        public int DefaultChrSize
        {
            get { return 0x2000 * 4; }
        }

        byte[] prg;

        public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            Console.Write("Reading PRG... ");
            prg = dumper.ReadCpu(0x8000, size);
            data.AddRange(prg);
            Console.WriteLine("OK");
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
                Console.Write("Reading CHR bank #{0}... ", bank);
                // Avoiding bus conflicts
                for (var i = 0; i < prg.Length; i++)
                {
                    if (prg[i] == bank)
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
            throw new NotSupportedException("SRAM is not supported by this mapper");
        }

        public NesFile.MirroringType GetMirroring(IFamicomDumperConnection dumper)
        {
            return dumper.GetMirroring();
        }
    }
}
