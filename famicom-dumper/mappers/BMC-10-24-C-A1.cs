using System;
using System.Collections.Generic;

namespace com.clusterrr.Famicom.Mappers
{
    public class BMC1024CA1 : IMapper
    {
        public string Name
        {
            get { return "BMC-10-24-C-A1"; }
        }

        public int Number
        {
            get { return -1; }
        }

        public string UnifName
        {
            get { return "BMC-10-24-C-A1"; }
        }

        public int DefaultPrgSize
        {
            get { return 1024 * 1024; }
        }

        public int DefaultChrSize
        {
            get { return 512 * 1024; }
        }

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            byte outbanks = (byte)(size / (256 * 1024));

            for (byte outbank = 0; outbank < outbanks; outbank += 1)
            {
                dumper.Reset();
                dumper.WriteCpu(0xA001, 0x80); // RAM protect
                dumper.WriteCpu((ushort)(0x6828 | (outbank << 1)), 0x00);
                dumper.WriteCpu(0xA001, 0); // disable W-RAM
                byte banks = 32;//(byte)(size / 0x2000);
                for (byte bank = 0; bank < banks - 2; bank += 2)
                {
                    Console.Write("Reading PRG banks #{2}|{0} and #{2}|{1}... ", bank, bank + 1, outbank);
                    dumper.WriteCpu(0x8000, new byte[] { 6, bank });
                    dumper.WriteCpu(0x8000, new byte[] { 7, (byte)(bank | 1) });
                    data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
                    Console.WriteLine("OK");
                }
                Console.Write("Reading last PRG banks #{2}|{0} and #{2}|{1}... ", banks - 2, banks - 1, outbank);
                data.AddRange(dumper.ReadCpu(0xC000, 0x4000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            byte outbanks = (byte)(size / (256 * 1024));
            for (byte outbank = 0; outbank < outbanks; outbank += 1)
            {
                dumper.Reset();
                dumper.WriteCpu(0xA001, 0x80); // RAM protect
                dumper.WriteCpu((ushort)(0x6828 | (outbank << 1)), 0x00);
                dumper.WriteCpu(0xA001, 0); // disable W-RAM

                int banks = 256;
                if (banks > 256) throw new Exception("CHR size is too big");
                for (int bank = 0; bank < banks; bank += 4)
                {
                    Console.Write("Reading CHR banks #{4}|{0}, #{4}|{1}, #{4}|{2}, #{4}|{3}... ", bank, bank + 1, bank + 2, bank + 3, outbank);
                    dumper.WriteCpu(0x8000, new byte[] { 2, (byte)bank });
                    dumper.WriteCpu(0x8000, new byte[] { 3, (byte)(bank | 1) });
                    dumper.WriteCpu(0x8000, new byte[] { 4, (byte)(bank | 2) });
                    dumper.WriteCpu(0x8000, new byte[] { 5, (byte)(bank | 3) });
                    data.AddRange(dumper.ReadPpu(0x1000, 0x1000));
                    Console.WriteLine("OK");
                }
            }
        }

        public void EnablePrgRam(FamicomDumperConnection dumper)
        {
            dumper.WriteCpu(0xA001, 0x80);
        }
    }
}
