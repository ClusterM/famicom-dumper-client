using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cluster.Famicom.Mappers
{
    public class AxROM : IMapper
    {
        public string Name
        {
            get { return "AxROM"; }
        }

        public int Number
        {
            get { return 7; }
        }

        public string UnifName
        {
            get { return null; }
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
            dumper.WriteCpu((ushort)(0x5000), (byte)0x00);
            dumper.WriteCpu((ushort)(0x5001), (byte)0x10);
            dumper.WriteCpu((ushort)(0x5002), (byte)0x10);
            dumper.WriteCpu((ushort)(0x5006), (byte)0x07);
            dumper.WriteCpu((ushort)(0x5007), (byte)0x03);
            byte banks = (byte)(size / 0x8000);
            for (int bank = 0; bank < banks; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                // TODO: избежать конфликтов шины
                // Avoiding bus conflicts
                /*
                for (int i = 0; i < lastBank.Length; i++)
                {
                    if (lastBank[i] == bank)
                    {
                        break;
                    }
                }
                 */
                dumper.WriteCpu((ushort)(0x8000), (byte)bank);
                data.AddRange(dumper.ReadCpu(0x8000, 0x8000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            Console.Write("Dumping CHR... ");
            data.AddRange(dumper.ReadPpu(0x0000, size));
            Console.WriteLine("OK");
        }
        public void EnablePrgRam(FamicomDumperConnection dumper)
        {
            Console.WriteLine("Warning: SRAM is not supported by this mapper");
        }
    }
}
