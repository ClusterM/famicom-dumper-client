using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Cluster.Famicom
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    printHelp();
                    return 0;
                }
                string command = args[0];
                string port = "COM13";
                string mapper = "0";
                string psize = null;
                string csize = null;
                string filename = null;

                for (int a = 1; a < args.Length - 1; a += 2)
                {
                    switch (args[a])
                    {
                        case "-p":
                            port = args[a + 1];
                            break;
                        case "-m":
                            mapper = args[a + 1];
                            break;
                        case "-f":
                            filename = args[a + 1];
                            break;
                        case "-psize":
                            psize = args[a + 1];
                            break;
                        case "-csize":
                            csize = args[a + 1];
                            break;
                        default:
                            printHelp();
                            return 2;
                    }
                }

                switch (command)
                {
                    case "reset":
                        resetOnly(port);
                        break;
                    case "list-mappers":
                        listMappers();
                        break;
                    case "dump":
                        dump(port, filename ?? "output.nes", mapper, parseSize(psize), parseSize(csize));
                        break;
                    case "dump-sram":
                        dumpSram(port, filename ?? "savegame.sav");
                        break;
                    case "write-sram":
                        writeSram(port, filename ?? "savegame.sav");
                        break;
                    case "dump-tiles":
                        DumpTiles(port, filename ?? "output.png", mapper, parseSize(csize));
                        break;
                    default:
                        printHelp();
                        return 2;
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return 1;
            }
        }

        static int parseSize(string size)
        {
            if (string.IsNullOrEmpty(size)) return -1;
            size = size.ToUpper();
            if (size.Contains("K"))
            {
                size = size.Replace("K", "");
                return int.Parse(size) * 1024;
            }
            return int.Parse(size);
        }

        static void printHelp()
        {
            Console.WriteLine("Usage: famicom-dumper.exe <command> [options]");
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            Console.WriteLine(" {0,-20}{1}", "list-mappers", "show supported mappers");
            Console.WriteLine(" {0,-20}{1}", "dump", "dump cartridge");
            Console.WriteLine(" {0,-20}{1}", "reset", "simulate reset to change games in multicards");
            Console.WriteLine(" {0,-20}{1}", "dump-sram", "read SRAM (battery backed save)");
            Console.WriteLine(" {0,-20}{1}", "write-sram", "write SRAM");
            Console.WriteLine(" {0,-20}{1}", "dump-tiles", "dump only tiles to PNG file");
            Console.WriteLine();
            Console.WriteLine("Available options:");
            Console.WriteLine(" {0,-20}{1}", "-p <com>", "serial port of dumper, default is \"COM13\"");
            Console.WriteLine(" {0,-20}{1}", "-m <mapper>", "number or name of mapper for dumping, default is 0");
            Console.WriteLine(" {0,-20}{1}", "-f <output.nes>", "output filename (.nes, .png or .sav)");
            Console.WriteLine(" {0,-20}{1}", "-psize <sile>", "size of PRG memory to dump, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-20}{1}", "-csize <sile>", "size of CHR memory to dump, you can use \"K\" or \"M\" suffixes");
        }

        static void resetOnly(string portName)
        {
            var dumper = new FamicomDumperConnection(portName);
            try
            {
                dumper.Open();
                Console.Write("PRG reader initialization... ");
                bool prgInit = dumper.PrgReaderInit();
                if (!prgInit) throw new Exception("can't init PRG reader");
                Console.WriteLine("OK");
                Console.Write("CHR reader initialization... ");
                bool chrInit = dumper.ChrReaderInit();
                if (!chrInit) throw new Exception("can't init CHR reader");
                Console.WriteLine("OK");
                dumper.Timeout = 5000;
                Console.Write("Reset... ");
                dumper.Reset();
                Console.WriteLine("OK");
                //NesFile.MirroringType mirroring = (NesFile.MirroringType)dumper.GetMirroring();
                //Console.WriteLine("Mirroring: " + mirroring);
                dumper.Close();
            }
            finally
            {
                dumper.Close();
            }
        }

        static void listMappers()
        {
            Console.WriteLine("Supported mappers:");
            Console.WriteLine(" {0,-10}{1}", "Number", "Name");
            Console.WriteLine("----------------------");
            foreach (var mapper in MappersContainer.Mappers)
            {
                Console.WriteLine(" {0,-10}{1}", mapper.Number, mapper.Name);
            }
        }

        static void dump(string portName, string fileName, string mapperName, int prgSize, int chrSize)
        {
            var mapper = MappersContainer.GetMapper(mapperName ?? "0");
            if (mapper == null) throw new Exception("can't find mapper");
            Console.WriteLine("Using mapper: #{0} ({1})", mapper.Number, mapper.Name);
            var dumper = new FamicomDumperConnection(portName);
            try
            {
                dumper.Open();
                Console.Write("PRG reader initialization... ");
                bool prgInit = dumper.PrgReaderInit();
                if (!prgInit) throw new Exception("can't init PRG reader");
                Console.WriteLine("OK");
                Console.Write("CHR reader initialization... ");
                bool chrInit = dumper.ChrReaderInit();
                if (!chrInit) throw new Exception("can't init CHR reader");
                Console.WriteLine("OK");
                dumper.Timeout = 5000;
                Console.WriteLine("Dumping...");
                List<byte> prg = new List<byte>();
                List<byte> chr = new List<byte>();
                prgSize = prgSize >= 0 ? prgSize : mapper.DefaultPrgSize;
                chrSize = chrSize >= 0 ? chrSize : mapper.DefaultChrSize;
                Console.WriteLine("PRG memory size: {0}", prgSize);
                mapper.DumpPrg(dumper, prg, prgSize);
                Console.WriteLine("CHR memory size: {0}", chrSize);
                mapper.DumpChr(dumper, chr, chrSize);
                NesFile.MirroringType mirroring = (NesFile.MirroringType)dumper.GetMirroring();
                Console.WriteLine("Mirroring: " + mirroring);
                dumper.Close();

                Console.WriteLine("Saving to {0}...", fileName);
                var nesFile = new NesFile();
                nesFile.Mapper = (byte)mapper.Number;
                nesFile.Mirroring = mirroring;
                nesFile.TvSystem = NesFile.TvSystemType.Ntsc;
                nesFile.PRG = prg.ToArray();
                nesFile.CHR = chr.ToArray();
                nesFile.Save(fileName);
                Console.WriteLine("Done!");
            }
            finally
            {
                dumper.Close();
            }
        }

        static void dumpSram(string portName, string fileName)
        {
            var dumper = new FamicomDumperConnection(portName);
            try
            {
                dumper.Open();
                Console.Write("PRG reader initialization... ");
                bool prgInit = dumper.PrgReaderInit();
                if (!prgInit) throw new Exception("can't init PRG reader");
                Console.WriteLine("OK");
                Console.Write("CHR reader initialization... ");
                bool chrInit = dumper.ChrReaderInit();
                if (!chrInit) throw new Exception("can't init CHR reader");
                Console.WriteLine("OK");
                dumper.Timeout = 5000;
                Console.Write("Dumping SRAM... ");
                var sram = dumper.ReadPrg(0x6000, 0x2000);
                File.WriteAllBytes(fileName, sram);
                dumper.ReadPrg(0x0, 1); // to avoid corruption
                Console.WriteLine("Done!");
            }
            finally
            {
                dumper.Close();
            }
        }

        static void writeSram(string portName, string fileName)
        {
            var dumper = new FamicomDumperConnection(portName);
            try
            {
                dumper.Open();
                Console.Write("PRG reader initialization... ");
                bool prgInit = dumper.PrgReaderInit();
                if (!prgInit) throw new Exception("can't init PRG reader");
                Console.WriteLine("OK");
                Console.Write("CHR reader initialization... ");
                bool chrInit = dumper.ChrReaderInit();
                if (!chrInit) throw new Exception("can't init CHR reader");
                Console.WriteLine("OK");
                dumper.Timeout = 5000;
                Console.Write("Writing SRAM... ");
                var sram = File.ReadAllBytes(fileName);
                dumper.WritePrg(0x6000, sram);
                dumper.ReadPrg(0x0, 1); // to avoid corruption
                Console.WriteLine("Done!");
            }
            finally
            {
                dumper.Close();
            }
        }


        static void DumpTiles(string portName, string fileName, string mapperName, int chrSize, int tilesPerLine = 16)
        {
            var mapper = MappersContainer.GetMapper(mapperName ?? "0");
            if (mapper == null) throw new Exception("can't find mapper");
            Console.WriteLine("Using mapper: #{0} ({1})", mapper.Number, mapper.Name);
            var dumper = new FamicomDumperConnection(portName);
            try
            {
                dumper.Open();
                Console.Write("PRG reader initialization... ");
                bool prgInit = dumper.PrgReaderInit();
                if (!prgInit) throw new Exception("can't init PRG reader");
                Console.WriteLine("OK");
                Console.Write("CHR reader initialization... ");
                bool chrInit = dumper.ChrReaderInit();
                if (!chrInit) throw new Exception("can't init CHR reader");
                Console.WriteLine("OK");
                dumper.Timeout = 5000;
                Console.WriteLine("Dumping...");
                List<byte> chr = new List<byte>();
                chrSize = chrSize >= 0 ? chrSize : mapper.DefaultChrSize;
                Console.WriteLine("CHR memory size: {0}", chrSize);
                mapper.DumpChr(dumper, chr, chrSize);
                dumper.Close();
                var tiles = new TilesExtractor(chr.ToArray());
                var allTiles = tiles.GetAllTiles();
                Console.WriteLine("Saving to {0}...", fileName);
                allTiles.Save(fileName, ImageFormat.Png);
                Console.WriteLine("Done!");
            }
            finally
            {
                dumper.Close();
            }
        }

#if DEBUGf
        static void Tests()
        {
            var dumper = new FamicomDumperConnection("COM13");
            dumper.Open();
            Console.Write("PRG reader initialization... ");
            bool prgInit = dumper.PrgReaderInit();
            if (!prgInit) throw new Exception("can't init PRG reader");
            Console.WriteLine("OK");
            Console.Write("CHR reader initialization... ");
            bool chrInit = dumper.ChrReaderInit();
            if (!chrInit) throw new Exception("can't init CHR reader");
            Console.WriteLine("OK");

            while (true)
            {
//                dumper.WritePrgEprom(0x8000, new byte[] { 1 ,2,3,4,5,6,7,8});
//                var chr = dumper.ReadPrg(0x8000, 8);

                var testdata = new byte[256];
                for (int i = 0; i < 256; i++) testdata[i] = (byte)(255-i);
                dumper.WritePrgEprom(0x8000, testdata);
                var chr = dumper.ReadPrg(0x8000 + 0x400, 256);

                
                foreach(var b in chr)
                {
                    Console.Write("{0:X2} ", b);
                }
                Console.WriteLine();
            }


        }
#endif
    }
}
