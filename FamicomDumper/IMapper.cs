using com.clusterrr.Famicom.DumperConnection;
using System.Collections.Generic;

namespace com.clusterrr.Famicom
{
    public interface IMapper
    {
        string Name { get; }
        int Number { get; }

        string UnifName { get; }

        int DefaultPrgSize { get; }
        int DefaultChrSize { get; }

        void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size = 0);

        void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size = 0);

        void EnablePrgRam(FamicomDumperConnection dumper);
    }
}
