/* Famicom Dumper/Programmer
 *
 * Copyright notice for this file:
 *  Copyright (C) 2016 Cluster
 *  http://clusterrr.com
 *  clusterrr@clusterrr.com
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
 *
 */

using com.clusterrr.Famicom.Mappers;
using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Text;
using System.Threading;

namespace com.clusterrr.Famicom
{
    class Program
    {
        static DateTime startTime;
        public static string MappersSearchDirectory = "mappers-cs";
        public static SoundPlayer doneSound = new SoundPlayer(Properties.Resources.DoneSound);
        public static SoundPlayer errorSound = new SoundPlayer(Properties.Resources.ErrorSound);

        static int Main(string[] args)
        {
            Console.WriteLine("Famicom Dumper Client v{0}.{1}",
                Assembly.GetExecutingAssembly().GetName().Version.Major,
                Assembly.GetExecutingAssembly().GetName().Version.Minor);              
            Console.WriteLine("  Commit {0} @ https://github.com/ClusterM/famicom-dumper-client",
                 Properties.Resources.gitCommit);
            Console.WriteLine("  (c) Alexey 'Cluster' Avdyukhin / https://clusterrr.com / clusterrr@clusterrr.com");
            Console.WriteLine();
            startTime = DateTime.Now;
            string port = "auto";
            string mapper = "0";
            string psize = null;
            string csize = null;
            string filename = null;
            string luaCode = null;
            string luaFile = null;
            string unifName = null;
            string unifAuthor = null;
            bool reset = false;
            bool silent = true;
            bool needCheck = false;
            bool writePBBs = false;
            List<int> badSectors = new List<int>();
            int testCount = -1;
            try
            {
                if (args.Length == 0)
                {
                    PrintHelp();
                    return 0;
                }

                string command = args[0].ToLower();

                for (int i = 1; i < args.Length; i++)
                {
                    string param = args[i];
                    while (param.StartsWith("-") || param.StartsWith("—")) param = param.Substring(1);
                    string value = i < args.Length - 1 ? args[i + 1] : "";
                    switch (param.ToLower())
                    {
                        case "p":
                        case "port":
                            port = value;
                            i++;
                            break;
                        case "m":
                        case "mapper":
                            mapper = value;
                            i++;
                            break;
                        case "f":
                        case "file":
                            filename = value;
                            i++;
                            break;
                        case "lua":
                        case "script":
                            luaCode = value;
                            i++;
                            break;
                        case "luafile":
                        case "scriptfile":
                            luaFile = value;
                            i++;
                            break;
                        case "psize":
                            psize = value;
                            i++;
                            break;
                        case "csize":
                            csize = value;
                            i++;
                            break;
                        case "unifname":
                            unifName = value;
                            i++;
                            break;
                        case "unifauthor":
                            unifAuthor = value;
                            i++;
                            break;
                        case "reset":
                            reset = true;
                            break;
                        case "sound":
                            silent = false;
                            break;
                        case "check":
                            needCheck = true;
                            break;
                        case "lock":
                            writePBBs = true;
                            break;
                        case "testcount":
                            testCount = int.Parse(value);
                            i++;
                            break;
                        case "badsectors":
                            foreach (var v in value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                                badSectors.Add(int.Parse(v));
                            i++;
                            break;
                        default:
                            Console.WriteLine("Unknown parameter: " + param);
                            PrintHelp();
                            return 2;
                    }
                }

                if (command == "list-mappers")
                {
                    ListMappers();
                    return 0;
                }

                using (var dumper = new FamicomDumperConnection(port))
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
                    if (reset)
                        Reset(dumper);

                    LuaMapper luaMapper = null;
                    if (!string.IsNullOrEmpty(luaFile) || !string.IsNullOrEmpty(luaCode) || command.ToLower() == "console")
                        luaMapper = new LuaMapper();
                    if (!string.IsNullOrEmpty(luaFile))
                    {
                        Console.WriteLine("Executing Lua script \"{0}\"...", Path.GetFileName(luaFile));
                        luaMapper.Verbose = true;
                        luaMapper.Execute(dumper, luaFile, true);
                        luaMapper.Verbose = false;
                    }
                    if (!string.IsNullOrEmpty(luaCode))
                    {
                        Console.WriteLine("Executing Lua code: \"{0}\"", luaCode);
                        luaMapper.Verbose = true;
                        luaMapper.Execute(dumper, luaCode, false);
                        luaMapper.Verbose = false;
                    }

                    switch (command)
                    {
                        case "reset":
                            if (!reset)
                                Reset(dumper);
                            break;
                        case "list-mappers":
                            ListMappers();
                            break;
                        case "dump":
                            Dump(dumper, filename ?? "output.nes", mapper, parseSize(psize), parseSize(csize), unifName, unifAuthor);
                            break;
                        case "read-prg-ram":
                        case "dump-prg-ram":
                        case "dump-sram":
                            ReadPrgRam(dumper, filename ?? "savegame.sav", mapper);
                            break;
                        case "write-prg-ram":
                        case "write-sram":
                            WritePrgRam(dumper, filename ?? "savegame.sav", mapper);
                            break;
                        case "test-prg-ram":
                        case "test-sram":
                            TestPrgRam(dumper, mapper);
                            break;
                        case "test-prg-coolgirl":
                        case "test-prg-ram-coolgirl":
                        case "test-sram-coolgirl":
                            CoolgirlWriter.TestPrgRam(dumper);
                            break;
                        case "test-battery":
                            TestBattery(dumper, mapper);
                            break;
                        case "test-chr-ram":
                            TestChrRam(dumper);
                            break;
                        case "test-chr-coolgirl":
                        case "test-chr-ram-coolgirl":
                            CoolgirlWriter.TestChrRam(dumper);
                            break;
                        case "test-coolgirl":
                            CoolgirlWriter.FullTest(dumper, testCount);
                            break;
                        case "test-bads-coolgirl":
                            CoolgirlWriter.FindBads(dumper, silent);
                            break;
                        case "read-crc-coolgirl":
                            CoolgirlWriter.ReadCrc(dumper);
                            break;
                        case "dump-tiles":
                            DumpTiles(dumper, filename ?? "output.png", mapper, parseSize(csize));
                            break;
                        case "write-coolboy-gpio":
                            CoolboyWriter.WriteWithGPIO(dumper, filename ?? "game.nes");
                            break;
                        case "write-coolboy":
                        case "write-coolboy-direct":
                            CoolboyWriter.Write(dumper, filename ?? "game.nes", badSectors, silent, needCheck, writePBBs);
                            break;
                        case "write-coolgirl":
                            CoolgirlWriter.Write(dumper, filename ?? "game.nes", badSectors, silent, needCheck, writePBBs);
                            break;
                        case "write-eeprom":
                            WriteEeprom(dumper, filename ?? "game.nes");
                            break;
                        case "info-coolboy":
                            CoolboyWriter.GetInfo(dumper);
                            break;
                        case "info-coolgirl":
                            CoolgirlWriter.GetInfo(dumper);
                            break;
                        case "bootloader":
                            Bootloader(dumper);
                            break;
                        case "console":
                            LuaConsole(dumper, luaMapper);
                            break;
                        case "nop":
                        case "none":
                        case "-":
                            break;
                        default:
                            Console.WriteLine("Unknown command: " + command);
                            PrintHelp();
                            return 2;

                    }
                    Console.WriteLine("Done in {0} seconds", (int)(DateTime.Now - startTime).TotalSeconds);
                    if (!silent) doneSound.PlaySync();
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                if (!silent)
                    errorSound.PlaySync();
                return 1;
            }
        }

        static int parseSize(string size)
        {
            if (string.IsNullOrEmpty(size)) return -1;
            size = size.ToUpper();
            int mul = 1;
            while (size.Contains("K"))
            {
                size = size.Replace("K", "");
                mul *= 1024;
            }
            while (size.Contains("M"))
            {
                size = size.Replace("M", "");
                mul *= 1024 * 1024;
            }
            return int.Parse(size) * mul;
        }

        static void PrintHelp()
        {
            Console.WriteLine("Usage: famicom-dumper.exe <command> [options]");
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            Console.WriteLine(" {0,-25}{1}", "list-mappers", "list built in mappers");
            Console.WriteLine(" {0,-25}{1}", "dump", "dump cartridge");
            Console.WriteLine(" {0,-25}{1}", "dump-tiles", "dump CHR data to PNG file");
            Console.WriteLine(" {0,-25}{1}", "reset", "simulate reset (M2 goes low for a second)");
            Console.WriteLine(" {0,-25}{1}", "read-prg-ram", "read PRG RAM (battery backed save if exists)");
            Console.WriteLine(" {0,-25}{1}", "write-prg-ram", "write PRG RAM");
            Console.WriteLine(" {0,-25}{1}", "write-coolboy-gpio", "write COOLBOY cartridge using GPIO");
            Console.WriteLine(" {0,-25}{1}", "write-coolboy-direct", "write COOLBOY cartridge directly");
            Console.WriteLine(" {0,-25}{1}", "write-coolgirl", "write COOLGIRL cartridge");
            Console.WriteLine(" {0,-25}{1}", "write-eeprom", "write EEPROM-based cartridge");
            Console.WriteLine(" {0,-25}{1}", "console", "start interactive Lua console");
            Console.WriteLine(" {0,-25}{1}", "test-prg-ram", "run PRG RAM test");
            Console.WriteLine(" {0,-25}{1}", "test-chr-ram", "run CHR RAM test");
            Console.WriteLine(" {0,-25}{1}", "test-battery", "test battery-backed PRG RAM");
            Console.WriteLine(" {0,-25}{1}", "test-prg-ram-coolgirl", "run PRG RAM test for COOLGIRL cartridge");
            Console.WriteLine(" {0,-25}{1}", "test-chr-ram-coolgirl", "run CHR RAM test for COOLGIRL cartridge");
            Console.WriteLine(" {0,-25}{1}", "test-coolgirl", "run all RAM tests for COOLGIRL cartridge");
            Console.WriteLine(" {0,-25}{1}", "test-bads-coolgirl", "find bad sectors on COOLGIRL cartridge");
            Console.WriteLine(" {0,-25}{1}", "read-crc-coolgirl", "shows CRC checksum for COOLGIRL");
            Console.WriteLine(" {0,-25}{1}", "info-coolboy", "show information about COOLBOY's flash memory");
            Console.WriteLine(" {0,-25}{1}", "info-coolgirl", "show information about COOLGIRL's flash memory");
            Console.WriteLine();
            Console.WriteLine("Available options:");
            Console.WriteLine(" {0,-25}{1}", "--port <com>", "serial port of dumper or serial number of FTDI device, default - auto");
            Console.WriteLine(" {0,-25}{1}", "--mapper <mapper>", "number, name or path to LUA script of mapper for dumping, default is 0 (NROM)");
            Console.WriteLine(" {0,-25}{1}", "--file <output.nes>", "output filename (.nes, .png or .sav)");
            Console.WriteLine(" {0,-25}{1}", "--psize <size>", "size of PRG memory to dump, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-25}{1}", "--csize <size>", "size of CHR memory to dump, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-25}{1}", "--luafile \"<lua_code>\"", "execute Lua code from file first");
            Console.WriteLine(" {0,-25}{1}", "--lua \"<lua_code>\"", "execute this Lua code first");
            Console.WriteLine(" {0,-25}{1}", "--unifname <name>", "internal ROM name for UNIF dumps");
            Console.WriteLine(" {0,-25}{1}", "--unifauthor <name>", "author of dump for UNIF dumps");
            Console.WriteLine(" {0,-25}{1}", "--reset", "do reset first");
            Console.WriteLine(" {0,-25}{1}", "--badsectors", "comma separated list of bad sectors for COOLBOY/COOLGIRL flashing");
            Console.WriteLine(" {0,-25}{1}", "--sound", "play sound when done or error occured");
            Console.WriteLine(" {0,-25}{1}", "--check", "verify COOLBOY/COOLGIRL checksum after writing");
            Console.WriteLine(" {0,-25}{1}", "--lock", "write-protect COOLBOY/COOLGIRL sectors after writing");
        }

        static void Reset(FamicomDumperConnection dumper)
        {
            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
        }

        static IMapper CompileMapperCS(string path)
        {
            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters();
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Data.dll");
            parameters.ReferencedAssemblies.Add(Assembly.GetEntryAssembly().Location);
            parameters.GenerateInMemory = true;
            parameters.GenerateExecutable = false;
            parameters.IncludeDebugInformation = true;

            CompilerResults results = provider.CompileAssemblyFromFile(parameters, path);
            if (results.Errors.HasErrors)
            {
                StringBuilder sb = new StringBuilder();
                foreach (CompilerError error in results.Errors)
                {
                    sb.AppendLine(String.Format("Error ({0}): {1}", error.ErrorNumber, error.ErrorText));
                }

                throw new InvalidOperationException(sb.ToString());
            }

            Assembly assembly = results.CompiledAssembly;
            var programs = assembly.GetTypes();
            if (programs.Count() == 0)
                throw new Exception("There is no assemblies");
            Type program = programs.First();
            var constructor = program.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, CallingConventions.Any, new Type[0], new ParameterModifier[0]);
            if (constructor == null)
                throw new Exception("There is no default constructor");
            return (IMapper)constructor.Invoke(new object[0]);
        }

        static Dictionary<string, IMapper> CompileAllMappers()
        {
            var result = new Dictionary<string, IMapper>();
            var directory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), MappersSearchDirectory);
            foreach (var f in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                //try
                {
                    result[f] = CompileMapperCS(f);
                }
                //catch { }
            }
            return result;
        }

        static void ListMappers()
        {
            var directory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), MappersSearchDirectory);
            Console.WriteLine("Searching mappers in {0}", directory); ;
            Console.WriteLine("Supported mappers:");
            Console.WriteLine(" {0,-30}{1,-9}{2}", "File", "Number", "Name");
            Console.WriteLine("----------------------------- -------- -----------------------");
            var mappers = CompileAllMappers();
            foreach (var mapperFile in mappers
                .Where(m => m.Value.Number >= 0)
                .OrderBy(m => m.Value.Number)
                .Union(mappers.Where(m => m.Value.Number < 0)
                .OrderBy(m => m.Value.Name)))
            {
                Console.WriteLine(" {0,-30}{1,-9}{2}", Path.GetFileName(mapperFile.Key), mapperFile.Value.Number >= 0 ? mapperFile.Value.Number.ToString() : "None", mapperFile.Value.Name);
            }
        }

        static IMapper GetMapper(string mapperName)
        {
            if (File.Exists(mapperName)) // LUA or CS script?
            {
                switch (Path.GetExtension(mapperName).ToLower())
                {
                    case ".cs":
                        return CompileMapperCS(mapperName);
                    case ".lua":
                        var luaMapper = new LuaMapper();
                        luaMapper.Execute(null, mapperName, true);
                        return luaMapper;
                    default:
                        throw new Exception("Unknown mapper extention");
                }
            }

            if (string.IsNullOrEmpty(mapperName))
                mapperName = "0";
            var mapperList = CompileAllMappers()
                .Where(m => m.Value.Name.ToLower() == mapperName.ToLower() 
                || (m.Value.Number >= 0 && m.Value.Number.ToString() == mapperName));
            if (mapperList.Count() == 0) throw new Exception("can't find mapper");
            var mapper = mapperList.First();
            Console.WriteLine("Using {0} as mapper file", mapper.Key);
            return mapper.Value;
        }

        static void Dump(FamicomDumperConnection dumper, string fileName, string mapperName, int prgSize, int chrSize, string unifName, string unifAuthor)
        {
            var mapper = GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine("Using mapper: #{0} ({1})", mapper.Number, mapper.Name);
            else
                Console.WriteLine("Using mapper: {0}", mapper.Name);
            Console.WriteLine("Dumping...");
            List<byte> prg = new List<byte>();
            List<byte> chr = new List<byte>();
            prgSize = prgSize >= 0 ? prgSize : mapper.DefaultPrgSize;
            chrSize = chrSize >= 0 ? chrSize : mapper.DefaultChrSize;
            Console.WriteLine("PRG memory size: {0}K", prgSize / 1024);
            mapper.DumpPrg(dumper, prg, prgSize);
            while (prg.Count % 0x4000 != 0) prg.Add(0);
            Console.WriteLine("CHR memory size: {0}K", chrSize / 1024);
            mapper.DumpChr(dumper, chr, chrSize);
            while (chr.Count % 0x2000 != 0) chr.Add(0);
            byte[] mirroringRaw = dumper.GetMirroring();
            NesFile.MirroringType mirroring = NesFile.MirroringType.Unknown_none;
            if (mirroringRaw.Length == 1)
            {
                mirroring = (NesFile.MirroringType)mirroringRaw[0];
                Console.WriteLine("Mirroring: " + mirroring);
            }
            else if (mirroringRaw.Length == 4)
            {
                switch (string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", mirroringRaw[0], mirroringRaw[1], mirroringRaw[2], mirroringRaw[3]))
                {
                    case "00000101":
                        mirroring = NesFile.MirroringType.Horizontal; // Horizontal
                        break;
                    case "00010001":
                        mirroring = NesFile.MirroringType.Vertical; // Vertical
                        break;
                    case "00000000":
                        mirroring = NesFile.MirroringType.OneScreenA; // One-screen A
                        break;
                    case "01010101":
                        mirroring = NesFile.MirroringType.OneScreenB; // One-screen B
                        break;
                    default:
                        mirroring = NesFile.MirroringType.Unknown_none; // Unknown
                        break;
                }
                Console.WriteLine("Mirroring: {0} ({1:X2} {2:X2} {3:X2} {4:X2})", mirroring, mirroringRaw[0], mirroringRaw[1], mirroringRaw[2], mirroringRaw[3]);
            }
            Console.WriteLine("Saving to {0}...", fileName);
            if (mapper.Number >= 0)
            {
                var nesFile = new NesFile();
                nesFile.Mapper = (byte)mapper.Number;
                nesFile.Mirroring = mirroring;
                nesFile.TvSystem = NesFile.TvSystemType.Ntsc;
                nesFile.PRG = prg.ToArray();
                nesFile.CHR = chr.ToArray();
                nesFile.Save(fileName);
            }
            else
            {
                var unifFile = new UnifFile();
                unifFile.Version = 4;
                unifFile.Mapper = mapper.UnifName;
                if (unifName != null)
                    unifFile.Fields["NAME"] = UnifFile.StringToUTF8N(unifName);
                unifFile.Fields["PRG0"] = prg.ToArray();
                if (chr.Count > 0)
                    unifFile.Fields["CHR0"] = chr.ToArray();
                unifFile.Fields["MIRR"] = new byte[] { 5 }; // mapper controlled
                unifFile.Save(fileName, unifAuthor);
            }
        }

        static void ReadPrgRam(FamicomDumperConnection dumper, string fileName, string mapperName)
        {
            var mapper = GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine("Using mapper: #{0} ({1})", mapper.Number, mapper.Name);
            else
                Console.WriteLine("Using mapper: {0}", mapper.Name);
            mapper.EnablePrgRam(dumper);
            Console.Write("Reading PRG-RAM... ");
            var sram = dumper.ReadCpu(0x6000, 0x2000);
            File.WriteAllBytes(fileName, sram);
            dumper.ReadCpu(0x0, 1); // to avoid corruption
            dumper.Reset();
        }

        static void WritePrgRam(FamicomDumperConnection dumper, string fileName, string mapperName)
        {
            var mapper = GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine("Using mapper: #{0} ({1})", mapper.Number, mapper.Name);
            else
                Console.WriteLine("Using mapper: {0}", mapper.Name);
            mapper.EnablePrgRam(dumper);
            Console.Write("Writing PRG-RAM... ");
            var sram = File.ReadAllBytes(fileName);
            dumper.WriteCpu(0x6000, sram);
            dumper.ReadCpu(0x0, 1); // to avoid corruption
            dumper.Reset();
        }

        static void TestPrgRam(FamicomDumperConnection dumper, string mapperName, int count = -1)
        {
            var mapper = GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine("Using mapper: #{0} ({1})", mapper.Number, mapper.Name);
            else
                Console.WriteLine("Using mapper: {0}", mapper.Name);
            mapper.EnablePrgRam(dumper);
            var rnd = new Random();
            while (count != 0)
            {
                var data = new byte[0x2000];
                rnd.NextBytes(data);
                Console.Write("Writing SRAM... ");
                dumper.WriteCpu(0x6000, data);
                Console.Write("Reading SRAM... ");
                var rdata = dumper.ReadCpu(0x6000, 0x2000);
                bool ok = true;
                for (int b = 0; b < 0x2000; b++)
                {
                    if (data[b] != rdata[b])
                    {
                        Console.WriteLine("Mismatch at {0:X4}: {1:X2} != {2:X2}", b, rdata[b], data[b]);
                        ok = false;
                    }
                }
                if (!ok)
                {
                    File.WriteAllBytes("sramgood.bin", data);
                    Console.WriteLine("sramgood.bin writed");
                    File.WriteAllBytes("srambad.bin", rdata);
                    Console.WriteLine("srambad.bin writed");
                    throw new Exception("Test failed");
                }
                Console.WriteLine("OK!");
                count--;
            }
        }

        static void TestBattery(FamicomDumperConnection dumper, string mapperName)
        {
            var mapper = GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine("Using mapper: #{0} ({1})", mapper.Number, mapper.Name);
            else
                Console.WriteLine("Using mapper: {0}", mapper.Name);
            mapper.EnablePrgRam(dumper);
            var rnd = new Random();
            var data = new byte[0x2000];
            rnd.NextBytes(data);
            Console.Write("Writing SRAM... ");
            dumper.WriteCpu(0x6000, data);
            dumper.Reset();
            Console.WriteLine("Replug cartridge and press enter");
            Console.ReadLine();
            mapper.EnablePrgRam(dumper);
            Console.Write("Reading SRAM... ");
            var rdata = dumper.ReadCpu(0x6000, 0x2000);
            bool ok = true;
            for (int b = 0; b < 0x2000; b++)
            {
                if (data[b] != rdata[b])
                {
                    Console.WriteLine("Mismatch at {0:X4}: {1:X2} != {2:X2}", b, rdata[b], data[b]);
                    ok = false;
                }
            }
            if (ok)
                Console.WriteLine("OK!");
            else
                throw new Exception("Failed!");
        }

        static void TestChrRam(FamicomDumperConnection dumper)
        {
            var rnd = new Random();
            while (true)
            {
                var data = new byte[0x2000];
                rnd.NextBytes(data);
                Console.Write("Writing CHR RAM... ");
                dumper.WritePpu(0x0000, data);
                Console.Write("Reading CHR RAM... ");
                var rdata = dumper.ReadPpu(0x0000, 0x2000);
                bool ok = true;
                for (int b = 0; b < 0x2000; b++)
                {
                    if (data[b] != rdata[b])
                    {
                        Console.WriteLine("Mismatch at {0:X4}: {1:X2} != {2:X2}", b, rdata[b], data[b]);
                        ok = false;
                    }
                }
                if (!ok)
                {
                    File.WriteAllBytes("chrgood.bin", data);
                    Console.WriteLine("chrgood.bin writed");
                    File.WriteAllBytes("chrbad.bin", rdata);
                    Console.WriteLine("chrbad.bin writed");
                    throw new Exception("Test failed");
                }
                Console.WriteLine("OK!");
            }
        }

        static void WriteEeprom(FamicomDumperConnection dumper, string fileName)
        {
            var nesFile = new NesFile(fileName);
            var prg = new byte[0x8000];
            int s = 0;
            while (s < prg.Length)
            {
                var n = Math.Min(nesFile.PRG.Length, prg.Length - s);
                Array.Copy(nesFile.PRG, s % nesFile.PRG.Length, prg, s, n);
                s += n;
            }
            var chr = new byte[0x2000];
            s = 0;
            while (s < chr.Length)
            {
                var n = Math.Min(nesFile.CHR.Length, chr.Length - s);
                Array.Copy(nesFile.CHR, s % nesFile.CHR.Length, chr, s, n);
                s += n;
            }

            dumper.Timeout = 1000;
            var buff = new byte[64];
            Console.Write("Writing PRG EEPROM");
            for (UInt16 a = 0; a < prg.Length; a += 64)
            {
                Array.Copy(prg, a, buff, 0, buff.Length);
                dumper.WriteCpu((UInt16)(0x8000 + a), buff);
                Thread.Sleep(3);
                Console.Write(".");
            }
            Console.WriteLine(" OK");
            Console.Write("Writing CHR EEPROM");
            for (UInt16 a = 0; a < chr.Length; a += 64)
            {
                Array.Copy(chr, a, buff, 0, buff.Length);
                dumper.WritePpu(a, buff);
                Thread.Sleep(3);
                Console.Write(".");
            }
            Console.WriteLine(" OK");
        }

        static void DumpTiles(FamicomDumperConnection dumper, string fileName, string mapperName, int chrSize, int tilesPerLine = 16)
        {
            var mapper = GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine("Using mapper: #{0} ({1})", mapper.Number, mapper.Name);
            else
                Console.WriteLine("Using mapper: {0}", mapper.Name);
            Console.WriteLine("Dumping...");
            List<byte> chr = new List<byte>();
            chrSize = chrSize >= 0 ? chrSize : mapper.DefaultChrSize;
            Console.WriteLine("CHR memory size: {0}K", chrSize / 1024);
            mapper.DumpChr(dumper, chr, chrSize);
            var tiles = new TilesExtractor(chr.ToArray());
            var allTiles = tiles.GetAllTiles();
            Console.WriteLine("Saving to {0}...", fileName);
            allTiles.Save(fileName, ImageFormat.Png);
        }

        static void Bootloader(FamicomDumperConnection dumper)
        {
            Console.WriteLine("Rebooting to bootloader...");
            dumper.Bootloader();
        }

        static void LuaConsole(FamicomDumperConnection dumper, LuaMapper luaMapper)
        {
            luaMapper.Verbose = true;
            Console.WriteLine("Starting interactive Lua console, type \"exit\" to exit.");
            while (true)
            {
                Console.Write("> ");
                var line = Console.ReadLine();
                if (line.ToLower().Trim() == "exit") break;
                try
                {
                    luaMapper.Execute(dumper, line, false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
        }
    }
}
