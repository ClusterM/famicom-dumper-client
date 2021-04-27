namespace com.clusterrr.Famicom.Mappers
{
    class Coolboy : IMapper
    {
        int version = 1;

        public string Name
        {
            get { return "COOLBOY"; }
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
            get
            {
                switch (version)
                {
                    default:
                        return "COOLBOY";
                    case 2:
                        return "MINDKIDS";
                }
            }
        }

        public int DefaultPrgSize
        {
            get { return 1024 * 1024 * 32; }
        }

        public int DefaultChrSize
        {
            get { return 0; }
        }

        public static byte DetectVersion(IFamicomDumperConnection dumper)
        {
            boo();
            byte version;
            Console.Write("Detecting COOLBOY version... ");
            // 0th CHR bank using both methods
            dumper.WriteCpu(0x5000, new byte[] { 0, 0, 0, 0x10 });
            dumper.WriteCpu(0x6000, new byte[] { 0, 0, 0, 0x10 });
            // Writing 0
            dumper.WritePpu(0x0000, new byte[] { 0 });
            // First CHR bank using both methods
            dumper.WriteCpu(0x5000, new byte[] { 0, 0, 1, 0x10 });
            dumper.WriteCpu(0x6000, new byte[] { 0, 0, 1, 0x10 });
            // Writing 1
            dumper.WritePpu(0x0000, new byte[] { 1 });
            // 0th bank using first method
            dumper.WriteCpu(0x6000, new byte[] { 0, 0, 0, 0x10 });
            byte v6000 = dumper.ReadPpu(0x0000, 1)[0];
            // return
            dumper.WriteCpu(0x6000, new byte[] { 0, 0, 1, 0x10 });
            // 0th bank using second method
            dumper.WriteCpu(0x5000, new byte[] { 0, 0, 0, 0x10 });
            byte v5000 = dumper.ReadPpu(0x0000, 1)[0];

            if (v6000 == 0 && v5000 == 1)
                version = 1;
            else if (v6000 == 1 && v5000 == 0)
                version = 2;
            else 
                throw new InvalidDataException("Can't detect COOLBOY version");
            Console.WriteLine("Version: {0}", version);
            return version;
        }

        public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            dumper.Reset();
            version = DetectVersion(dumper);
            UInt16 coolboyReg = (UInt16)(version == 2 ? 0x5000 : 0x6000);
            int banks = size / 0x4000;

            for (var bank = 0; bank < banks; bank++)
            {
                var r0 = (byte)(((bank >> 3) & 0x07) // 5, 4, 3 bits
                    | (((bank >> 9) & 0x03) << 4) // 10, 9 bits
                    | (1 << 6)); // resets 4th mask bit
                var r1 = (byte)((((bank >> 7) & 0x03) << 2) // 8, 7
                    | (((bank >> 6) & 1) << 4) // 6
                    | (1 << 7)); // resets 5th mask bit
                var r2 = (byte)0;
                var r3 = (byte)((1 << 4) // NROM mode
                    | ((bank & 7) << 1)); // 2, 1, 0 bits
                dumper.WriteCpu(coolboyReg, new byte[] { r0, r1, r2, r3 });

                Console.Write("Reading PRG banks #{0}/{1}... ", bank, banks);
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
            dumper.WriteCpu(0x6003, 0x80);
            dumper.WriteCpu(0xA001, 0x80);
        }

        public NesFile.MirroringType GetMirroring(IFamicomDumperConnection dumper)
        {
            return NesFile.MirroringType.MapperControlled;
        }
    }
}
