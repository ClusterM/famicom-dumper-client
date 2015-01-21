using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cluster.Famicom.Mappers
{
    public class CNROM : IMapper
    {
        public string Name
        {
            get { return "CNROM"; }
        }

        public int Number
        {
            get { return 3; }
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

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            Console.Write("Dumping PRG... ");
            prg = dumper.ReadPrg(0x8000, size);
            data.AddRange(prg);
            Console.WriteLine("OK");
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            if (prg == null)
            {
                prg = dumper.ReadPrg(0x8000, DefaultPrgSize);
            }
            byte banks = (byte)(size / 0x2000);

            for (int bank = 0; bank < banks; bank++)
            {
                Console.Write("Dumping CHR bank #{0}... ", bank);
                for (int i = 0; i < prg.Length; i++)
                {
                    if (prg[i] == bank)
                    {
                        dumper.WritePrg((ushort)(0x8000 + i), (byte)bank);
                        break;
                    }
                }
                var d = dumper.ReadChr(0x0000, 0x2000);
                data.AddRange(d);
                Console.WriteLine("OK");
            }
        }
    }
}
