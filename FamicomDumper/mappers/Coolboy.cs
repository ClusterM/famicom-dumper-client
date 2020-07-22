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

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            dumper.Reset();
            version = CoolboyWriter.DetectVersion(dumper);
            UInt16 coolboyReg = (UInt16)(version == 2 ? 0x5000 : 0x6000);
            int banks = size / 0x4000;

            for (int bank = 0; bank < banks; bank++)
            {
                byte r0 = (byte)(((bank >> 3) & 0x07) // 5, 4, 3 bits
                    | (((bank >> 9) & 0x03) << 4) // 10, 9 bits
                    | (1 << 6)); // resets 4th mask bit
                byte r1 = (byte)((((bank >> 7) & 0x03) << 2) // 8, 7
                    | (((bank >> 6) & 1) << 4) // 6
                    | (1 << 7)); // resets 5th mask bit
                byte r2 = 0;
                byte r3 = (byte)((1 << 4) // NROM mode
                    | ((bank & 7) << 1)); // 2, 1, 0 bits
                dumper.WriteCpu(coolboyReg, new byte[] { r0, r1, r2, r3 });

                Console.Write("Reading PRG banks #{0}/{1}... ", bank, banks);
                data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            return;
        }

        public void EnablePrgRam(FamicomDumperConnection dumper)
        {
            dumper.Reset();
            dumper.WriteCpu(0xA001, 0x00);
            dumper.WriteCpu(0x6003, 0x80);
            dumper.WriteCpu(0xA001, 0x80);
        }
    }
}
