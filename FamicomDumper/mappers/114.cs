using System;

namespace com.clusterrr.Famicom.Mappers
{
    class Mapper114 : IMapper
    {
        public string Name
        {
            get { return "MMC3 SG PROT. A"; }
        }

        public int Number
        {
            get { return 114; }
        }

        public string UnifName
        {
            get { return null; }
        }

        public int DefaultPrgSize
        {
            get { return 512 * 1024; }
        }

        public int DefaultChrSize
        {
            get { return 256 * 1024; }
        }

        private ushort ScrumbleAddress(ushort address)
        {
            switch (address)
            {
                case 0xA001: return 0x8000;
                case 0xA000: return 0x8001;
                case 0x8000: return 0xA000;
                case 0xC000: return 0xA001;
                case 0x8001: return 0xC000;
                case 0xC001: return 0xC001;
                case 0xE000: return 0xE000;
                case 0xE001: return 0xE001;
            }
            return 0;
        }

        private byte ScrumbleValues(byte value)
        {
            switch (value)
            {
                case 0: return 0;
                case 3: return 1;
                case 1: return 2;
                case 5: return 3;
                case 6: return 4;
                case 7: return 5;
                case 2: return 6;
                case 4: return 7;
            }
            return 0;
        }

        public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x2000;
            dumper.WriteCpu(0x6000, 0);
            for (var bank = 0; bank < banks - 2; bank += 2)
            {
                Console.Write("Reading PRG banks #{0} and #{1}... ", bank, bank + 1);
                dumper.WriteCpu(ScrumbleAddress(0x8000), ScrumbleValues(6));
                dumper.WriteCpu(ScrumbleAddress(0x8001), (byte)bank);
                dumper.WriteCpu(ScrumbleAddress(0x8000), ScrumbleValues(7));
                dumper.WriteCpu(ScrumbleAddress(0x8001), (byte)(bank | 1));
                data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
                Console.WriteLine("OK");
            }
            Console.Write("Reading last PRG banks #{0} and #{1}... ", banks - 2, banks - 1);
            data.AddRange(dumper.ReadCpu(0xC000, 0x4000));
            Console.WriteLine("OK");
        }

        public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
        {
            var banks = size / 0x400;
            if (banks > 512) throw new ArgumentOutOfRangeException("size", "CHR size is too big");
            for (var bank = 0; bank < banks; bank += 4)
            {
                Console.Write("Reading CHR banks #{0}, #{1}, #{2}, #{3}... ", bank, bank + 1, bank + 2, bank + 3);
                dumper.WriteCpu(0x6000, (byte)((bank >> 8) & 1));
                dumper.WriteCpu(ScrumbleAddress(0x8000), ScrumbleValues(2));
                dumper.WriteCpu(ScrumbleAddress(0x8001), (byte)bank);
                dumper.WriteCpu(ScrumbleAddress(0x8000), ScrumbleValues(3));
                dumper.WriteCpu(ScrumbleAddress(0x8001), (byte)(bank | 1));
                dumper.WriteCpu(ScrumbleAddress(0x8000), ScrumbleValues(4));
                dumper.WriteCpu(ScrumbleAddress(0x8001), (byte)(bank | 2));
                dumper.WriteCpu(ScrumbleAddress(0x8000), ScrumbleValues(5));
                dumper.WriteCpu(ScrumbleAddress(0x8001), (byte)(bank | 3));
                data.AddRange(dumper.ReadPpu(0x1000, 0x1000));
                Console.WriteLine("OK");
            }
        }

        public void EnablePrgRam(IFamicomDumperConnection dumper)
        {
            throw new NotSupportedException("SRAM is not supported by this mapper");
        }
    }
}
