using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cluster.Famicom.Mappers
{
    public class NROM : IMapper
    {
        public string Name
        {
            get { return "NROM"; }
        }

        public int Number
        {
            get { return 0; }
        }

        public int DefaultPrgSize
        {
            get { return 0x8000; }
        }

        public int DefaultChrSize
        {
            get { return 0x2000; }
        }

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            Console.Write("Dumping PRG... ");
            data.AddRange(dumper.ReadPrg(0x8000, size));
            Console.WriteLine("OK");
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            Console.Write("Dumping CHR... ");
            data.AddRange(dumper.ReadChr(0x0000, size));
            Console.WriteLine("OK");
        }
    }
}
