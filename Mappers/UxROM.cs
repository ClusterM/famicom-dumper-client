using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cluster.Famicom.Mappers
{
    public class UxROM : IMapper
    {
        public string Name
        {
            get { return "UxROM"; }
        }

        public int Number
        {
            get { return 2; }
        }

        public int DefaultPrgSize
        {
            get { return 256 * 1024; }
        }

        public int DefaultChrSize
        {
            get { return 0; }
        }

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            byte banks = (byte)(size / 0x4000);
            Console.Write("Reading last PRG bank... ");
            var lastBank = dumper.ReadPrg(0xC000, 0x4000);
            Console.WriteLine("OK");
            for (int bank = 0; bank < banks-1; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                // Avoiding bus conflicts
                for (int i = 0; i < lastBank.Length; i++)
                {
                    if (lastBank[i] == bank)
                    {
                        dumper.WritePrg((ushort)(0xC000 + i), (byte)bank);
                        break;
                    }
                }
                data.AddRange(dumper.ReadPrg(0x8000, 0x4000));
                Console.WriteLine("OK");
            }
            data.AddRange(lastBank);            
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            Console.Write("Dumping CHR... ");
            data.AddRange(dumper.ReadChr(0x0000, size));
            Console.WriteLine("OK");
        }
    }
}
