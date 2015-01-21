using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cluster.Famicom
{
    public interface IMapper
    {
        string Name { get; }
        int Number { get; }

        int DefaultPrgSize { get; }
        int DefaultChrSize { get; }

        void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size = 0);

        void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size = 0);
    }
}
