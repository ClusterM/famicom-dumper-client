using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Cluster.Famicom.Mappers
{
    public class Coolboy : IMapper
    {
        public string Name
        {
            get { return "Coolboy"; }
        }

        public int Number
        {
            get { return -1; }
        }
        public string UnifName
        {
            get { return "COOLBOY"; }
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
            int outbanks = size / (128 * 1024);

            int outbankSize = 512;
            
            for (int outbank = 0; outbank < outbanks; outbank += outbankSize / 128)
            {
                byte r0 = (byte)((outbank & 0x07) | ((outbank & 0xc0) >> 2));
                byte r1 = (byte)(((outbank & 0x30) >> 2) | ((outbank << 1) & 0x10));
                byte r2 = 0;
                byte r3 = 0;
                dumper.WriteCpu(0x6000, new byte[] { r0 });
                dumper.WriteCpu(0x6001, new byte[] { r1 });
                dumper.WriteCpu(0x6002, new byte[] { r2 });
                dumper.WriteCpu(0x6003, new byte[] { r3 });

                int banks = outbankSize * 1024 / 0x2000;
                for (int bank = 0; bank < banks - 2; bank += 2)
                {
                    Console.Write("Reading PRG banks #{2}|{0} and #{2}|{1}... ", bank, bank + 1, outbank);
                    dumper.WriteCpu(0x8000, new byte[] { 6, (byte)(bank) });
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
