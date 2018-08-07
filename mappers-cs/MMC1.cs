using System;
using System.Collections.Generic;

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
            get { return 128 * 1024; }
        }

        void WriteMMC1(FamicomDumperConnection dumper, UInt16 address, byte data)
        {
            byte[] buffer = new byte[5];
            for (int i = 0; i < 5; i++)
            {
                buffer[i] = (byte)(data >> i);
            }
            dumper.WriteCpu(address, buffer);
        }

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            dumper.WriteCpu(0x8000, 0x80);
            WriteMMC1(dumper, 0x8000, 0x0C);    

            byte banks = (byte)(size / 0x4000);

            for (byte bank = 0; bank < banks-1; bank++)
            {
                Console.Write("Reading PRG bank #{0}... ", bank);
                WriteMMC1(dumper, 0xE000, bank);
                data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
                Console.WriteLine("OK");
            }
            if (banks > 0)
            {
                Console.Write("Reading last PRG bank #{0}... ", banks - 1);
                data.AddRange(dumper.ReadCpu(0xC000, 0x4000));
                Console.WriteLine("OK");
            }
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size)
        {
            dumper.WriteCpu(0x8000, 0x80);
            WriteMMC1(dumper, 0x8000, 0x0C);
            
            byte banks = (byte)(size / 0x1000);

            for (int bank = 0; bank < banks; bank += 2)
            {
                Console.Write("Reading CHR banks #{0} and #{1}... ", bank, bank + 1);
                WriteMMC1(dumper, 0xA000, (byte)bank);
                data.AddRange(dumper.ReadPpu(0x0000, 0x2000));
                Console.WriteLine("OK");
            }
        }

        public void EnablePrgRam(FamicomDumperConnection dumper)
        {
            dumper.WriteCpu(0x8000, 0x80);
            WriteMMC1(dumper, 0xE000, 0x00);
        }
    }
}
