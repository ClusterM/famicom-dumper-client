using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Cluster.Famicom.Mappers
{
    public class MMC1 : IMapper
    {
        public string Name
        {
            get { return "MMC1"; }
        }

        public int Number
        {
            get { return 1; }
        }

        public int DefaultPrgSize
        {
            get { return 256 * 1024; }
        }

        public int DefaultChrSize
        {
            get { return 128 * 1024; }
        }

        void WriteMmc1(FamicomDumperConnection dumper, UInt16 address, byte data)
        {
            var buffer = new byte[5];
            for (int i = 0; i < 5; i++)
            {
                buffer[i] = (byte)(data >> i);
            }
            dumper.WritePrg(address, buffer);
        }

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            dumper.WritePrg(0x8000, 0x80);
            WriteMmc1(dumper, 0x8000, 0x0C);

            byte banks = (byte)(size / 0x4000);

            for (byte bank = 0; bank < banks-1; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                WriteMmc1(dumper, 0xE000, bank);
                data.AddRange(dumper.ReadPrg(0x8000, 0x4000));
                Console.WriteLine("OK");
            }
            if (banks > 0)
            {
                Console.Write("Reading last PRG bank #{0}... ", banks - 1);
                data.AddRange(dumper.ReadPrg(0xC000, 0x4000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            byte banks = (byte)(size / 0x1000);

            for (int bank = 0; bank < banks; bank += 2)
            {
                Console.Write("Reading CHR banks #{0} and #{1}... ", bank, bank + 1);
                WriteMmc1(dumper, 0xA000, (byte)bank);
                WriteMmc1(dumper, 0xC000, (byte)(bank | 1));
                data.AddRange(dumper.ReadChr(0x0000, 0x2000));
                Console.WriteLine("OK");
            }
        }
    }
}
