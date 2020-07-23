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

using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;

namespace com.clusterrr.Famicom
{
    class Program
    {
        static DateTime startTime;
        public static string MappersSearchDirectory = "mappers";
        private const string ScriptStartMethod = "Run";
        public static SoundPlayer doneSound = new SoundPlayer(FamicomDumper.Properties.Resources.DoneSound);
        public static SoundPlayer errorSound = new SoundPlayer(FamicomDumper.Properties.Resources.ErrorSound);

        static int Main(string[] args)
        {
            Console.WriteLine($"Famicom Dumper Client v{Assembly.GetExecutingAssembly().GetName().Version.Major}.{Assembly.GetExecutingAssembly().GetName().Version.Minor}");
            Console.WriteLine($"  Commit {FamicomDumper.Properties.Resources.gitCommit} @ https://github.com/ClusterM/famicom-dumper-client");
            Console.WriteLine("  (c) Alexey 'Cluster' Avdyukhin / https://clusterrr.com / clusterrr@clusterrr.com");
            Console.WriteLine();
            startTime = DateTime.Now;
            string port = "auto";
            string mapper = "0";
            string psize = null;
            string csize = null;
            string filename = null;
            string csFile = null;
            string unifName = null;
            string unifAuthor = null;
            bool reset = false;
            bool silent = true;
            bool needCheck = false;
            bool writePBBs = false;
            List<int> badSectors = new List<int>();
            int testCount = -1;
            uint tcpPort = 26672;
            string remoteHost = null;
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
                        case "csfile":
                        case "scriptfile":
                            csFile = value;
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
                        case "tcpport":
                            tcpPort = uint.Parse(value);
                            i++;
                            break;
                        case "host":
                            remoteHost = value;
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

                FamicomDumperConnection dumper;
                if (string.IsNullOrEmpty(remoteHost))
                {
                    if ((string.IsNullOrEmpty(port) || port == "auto") && IsRunningOnMono())
                        throw new NotSupportedException("Port autodetect is not supported on Linux, please specify dumper port using --port parameter");
                    dumper = new FamicomDumperConnection(port);
                    dumper.Open();
                }
                else
                {
                    BinaryServerFormatterSinkProvider binaryServerFormatterSinkProvider
                        = new BinaryServerFormatterSinkProvider();
                    BinaryClientFormatterSinkProvider binaryClientFormatterSinkProvider
                        = new BinaryClientFormatterSinkProvider();
                    binaryServerFormatterSinkProvider.TypeFilterLevel
                        = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
                    var dict = new System.Collections.Hashtable();
                    dict["name"] = "FamicomDumperClient";
                    dict["secure"] = false;
                    var channel = new TcpChannel(dict, binaryClientFormatterSinkProvider, binaryServerFormatterSinkProvider);
                    ChannelServices.RegisterChannel(channel, false);
                    dumper = (FamicomDumperConnection)Activator.GetObject(typeof(FamicomDumperConnection), $"tcp://{remoteHost}:{tcpPort}/dumper");
                    var lifetime = dumper.GetLifetimeService();
                }
                try
                {
                    Console.Write("Dumper initialization... ");
                    bool prgInit = dumper.DumperInit();
                    if (!prgInit) throw new IOException("Can't init dumper");
                    Console.WriteLine("OK");

                    if (reset)
                        Reset(dumper);

                    if (!string.IsNullOrEmpty(csFile))
                    {
                        CompileAndExecute(csFile, dumper);
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
                            Dump(dumper, filename ?? "output.nes", mapper, ParseSize(psize), ParseSize(csize), unifName, unifAuthor);
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
                            DumpTiles(dumper, filename ?? "output.png", mapper, ParseSize(csize));
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
                        case "script":
                            if (string.IsNullOrEmpty(csFile))
                                throw new ArgumentNullException("Please specify C# script using --csfile argument");
                            break;
                        case "server":
                            StartServer(dumper, tcpPort);
                            break;
                        default:
                            Console.WriteLine("Unknown command: " + command);
                            PrintHelp();
                            return 2;

                    }
                    Console.WriteLine("Done in {0} seconds", (int)(DateTime.Now - startTime).TotalSeconds);
                    if (!silent) doneSound.PlaySync();
                }
                finally
                {
                    if (string.IsNullOrEmpty(remoteHost))
                        dumper.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error {ex.GetType()}: " + ex.Message
#if DEBUG
                    + ex.StackTrace
#endif
                    );
                if (!silent)
                    errorSound.PlaySync();
#if DEBUG
                Console.WriteLine("Press any key");
                Console.ReadKey();
#endif
                return 1;
            }
#if DEBUG
            Console.WriteLine("Press any key");
            Console.ReadKey();
#endif
            return 0;
        }

        static int ParseSize(string size)
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
            Console.WriteLine(" {0,-25}{1}", "list-mappers", "list available mappers to dump");
            Console.WriteLine(" {0,-25}{1}", "dump", "dump cartridge");
            Console.WriteLine(" {0,-25}{1}", "server", "start server for remote dumping");
            Console.WriteLine(" {0,-25}{1}", "script", "execute C# script specified by --csfile option");
            Console.WriteLine(" {0,-25}{1}", "reset", "simulate reset (M2 goes to Z-state for a second)");
            Console.WriteLine(" {0,-25}{1}", "dump-tiles", "dump CHR data to PNG file");
            Console.WriteLine(" {0,-25}{1}", "read-prg-ram", "read PRG RAM (battery backed save if exists)");
            Console.WriteLine(" {0,-25}{1}", "write-prg-ram", "write PRG RAM");
            Console.WriteLine(" {0,-25}{1}", "write-coolboy-gpio", "write COOLBOY cartridge using GPIO");
            Console.WriteLine(" {0,-25}{1}", "write-coolboy-direct", "write COOLBOY cartridge directly");
            Console.WriteLine(" {0,-25}{1}", "write-coolgirl", "write COOLGIRL cartridge");
            Console.WriteLine(" {0,-25}{1}", "write-eeprom", "write EEPROM-based cartridge");
            Console.WriteLine(" {0,-25}{1}", "test-prg-ram", "run PRG RAM test");
            Console.WriteLine(" {0,-25}{1}", "test-chr-ram", "run CHR RAM test");
            Console.WriteLine(" {0,-25}{1}", "test-battery", "test battery-backed PRG RAM");
            Console.WriteLine(" {0,-25}{1}", "test-prg-ram-coolgirl", "run PRG RAM test for COOLGIRL cartridge");
            Console.WriteLine(" {0,-25}{1}", "test-chr-ram-coolgirl", "run CHR RAM test for COOLGIRL cartridge");
            Console.WriteLine(" {0,-25}{1}", "test-coolgirl", "run all RAM tests for COOLGIRL cartridge");
            Console.WriteLine(" {0,-25}{1}", "test-bads-coolgirl", "find bad sectors on COOLGIRL cartridge");
            Console.WriteLine(" {0,-25}{1}", "read-crc-coolgirl", "show CRC checksum for COOLGIRL");
            Console.WriteLine(" {0,-25}{1}", "info-coolboy", "show information about COOLBOY's flash memory");
            Console.WriteLine(" {0,-25}{1}", "info-coolgirl", "show information about COOLGIRL's flash memory");
            Console.WriteLine();
            Console.WriteLine("Available options:");
            Console.WriteLine(" {0,-25}{1}", "--port <com>", "serial port of dumper or serial number of FTDI device, default - auto");
            Console.WriteLine(" {0,-25}{1}", "--tcpport <port>", "TCP port for client/server communication, default - 26672");
            Console.WriteLine(" {0,-25}{1}", "--host <host>", "enable network client and connect to specified host");
            Console.WriteLine(" {0,-25}{1}", "--mapper <mapper>", "number, name or path to C# script of mapper for dumping, default is 0 (NROM)");
            Console.WriteLine(" {0,-25}{1}", "--file <output.nes>", "output filename (.nes, .png or .sav)");
            Console.WriteLine(" {0,-25}{1}", "--psize <size>", "size of PRG memory to dump, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-25}{1}", "--csize <size>", "size of CHR memory to dump, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-25}{1}", "--csfile <C#_file>", "execute C# script from file");
            Console.WriteLine(" {0,-25}{1}", "--reset", "simulate reset first");
            Console.WriteLine(" {0,-25}{1}", "--unifname <name>", "internal ROM name for UNIF dumps");
            Console.WriteLine(" {0,-25}{1}", "--unifauthor <name>", "author of dump for UNIF dumps");
            Console.WriteLine(" {0,-25}{1}", "--badsectors", "comma separated list of bad sectors for COOLBOY/COOLGIRL writing");
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

        static Assembly Compile(string path)
        {
            var provider = new CSharpCodeProvider();
            CompilerParameters options = new CompilerParameters();
            var entryAssemblyLocation = Assembly.GetEntryAssembly().Location;
            // Automatically add references
            options.ReferencedAssemblies.Add("System.dll");
            options.ReferencedAssemblies.Add("System.Data.dll");
            options.ReferencedAssemblies.Add("System.Core.dll");
            options.ReferencedAssemblies.Add(entryAssemblyLocation);
            options.ReferencedAssemblies.Add(Path.Combine(Path.GetDirectoryName(entryAssemblyLocation), "FamicomDumperConnection.dll"));
            options.GenerateInMemory = true;
            options.GenerateExecutable = false;
            options.IncludeDebugInformation = true;


            var source = File.ReadAllText(path);
            // And usings
            int linesOffset = 0;
            if (!new Regex(@"^using\s+System\s*;").IsMatch(source))
            {
                source = "using System;\r\n" + source;
                linesOffset++;
            }
            if (!new Regex(@"^using\s+System\.Collections\.Generic\s*;").IsMatch(source))
            {
                source = "using System.Collections.Generic;\r\n" + source;
                linesOffset++;
            }
            if (!new Regex(@"^using\s+System\.Linq\s*;").IsMatch(source))
            {
                source = "using System.Linq;\r\n" + source;
                linesOffset++;
            }
            if (!new Regex(@"^using\s+com\.clusterrr\.Famicom\.DumperConnection\s*;").IsMatch(source))
            {
                source = "using com.clusterrr.Famicom.DumperConnection;\r\n" + source;
                linesOffset++;
            }

            CompilerResults results = provider.CompileAssemblyFromSource(options, source);
            if (results.Errors.HasErrors)
            {
                foreach (CompilerError error in results.Errors)
                    Console.WriteLine($"In {Path.GetFileName(path)} on line {error.Line - linesOffset}: ({error.ErrorNumber}) {error.ErrorText}");
                throw new InvalidProgramException();
            }

            return results.CompiledAssembly;
        }

        static IMapper CompileMapper(string path)
        {
            Assembly assembly = Compile(path);
            var programs = assembly.GetTypes();
            if (!programs.Any())
                throw new InvalidProgramException("There is no assemblies");
            Type program = programs.First();
            var constructor = program.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, CallingConventions.Any, new Type[0], new ParameterModifier[0]);
            if (constructor == null)
                throw new InvalidProgramException("There is no valid default constructor");
            var mapper = constructor.Invoke(new object[0]);
            if (!(mapper is IMapper))
                throw new InvalidProgramException("Class doesn't implement IMapper interface");
            return mapper as IMapper;
        }

        static Dictionary<string, IMapper> CompileAllMappers()
        {
            var result = new Dictionary<string, IMapper>();
            var directory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), MappersSearchDirectory);
            Console.WriteLine($"Compiling mappers in {directory}...");
            foreach (var f in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                result[f] = CompileMapper(f);
            }
            return result;
        }

        static void CompileAndExecute(string path, FamicomDumperConnection dumper)
        {
            Console.WriteLine($"Compiling {path}...");
            Assembly assembly = Compile(path);
            var programs = assembly.GetTypes();
            if (!programs.Any())
                throw new InvalidProgramException("There is no assemblies");
            Type program = programs.First();

            // Is it static method?
            var staticMethod = program.GetMethod(ScriptStartMethod, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, new Type[] { typeof(FamicomDumperConnection) }, new ParameterModifier[0]);
            if (staticMethod != null)
            {
                Console.WriteLine($"Running {program.Name}.{ScriptStartMethod}()...");
                staticMethod.Invoke(program, new object[] { dumper });
                return;
            }

            // Let's try instance method, need to call constructor first
            var constructor = program.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, CallingConventions.Any, new Type[0], new ParameterModifier[0]);
            if (constructor == null)
                throw new InvalidProgramException("There is no valid default constructor");
            var obj = constructor.Invoke(new object[0]);
            var instanceMethod = obj.GetType().GetMethod(ScriptStartMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, new Type[] { typeof(FamicomDumperConnection) }, new ParameterModifier[0]);
            if (instanceMethod == null)
                throw new InvalidProgramException($"There is no {ScriptStartMethod} method");
            Console.WriteLine($"Running {program.Name}.{ScriptStartMethod}()...");
            instanceMethod.Invoke(obj, new object[] { dumper });
        }

        static void ListMappers()
        {
            var directory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), MappersSearchDirectory);

            var mappers = CompileAllMappers();
            Console.WriteLine("Supported mappers:");
            Console.WriteLine(" {0,-30}{1,-24}{2,-9}{3,-24}", "File", "Name", "Number", "UNIF name");
            Console.WriteLine("----------------------------- ----------------------- -------- -----------------------");
            foreach (var mapperFile in mappers
                .Where(m => m.Value.Number >= 0)
                .OrderBy(m => m.Value.Number)
                .Union(mappers.Where(m => m.Value.Number < 0)
                .OrderBy(m => m.Value.Name)))
            {
                Console.WriteLine(" {0,-30}{1,-24}{2,-9}{3,-24}",
                    Path.GetFileName(mapperFile.Key),
                    mapperFile.Value.Name,
                    mapperFile.Value.Number >= 0 ? mapperFile.Value.Number.ToString() : "None",
                    mapperFile.Value.UnifName ?? "None");
            }
        }

        static IMapper GetMapper(string mapperName)
        {
            if (File.Exists(mapperName)) // CS script?
            {
                Console.WriteLine($"Compiling {mapperName}...");
                return CompileMapper(mapperName);
            }

            if (string.IsNullOrEmpty(mapperName))
                mapperName = "0";
            var mapperList = CompileAllMappers()
                .Where(m => m.Value.Name.ToLower() == mapperName.ToLower()
                || (m.Value.Number >= 0 && m.Value.Number.ToString() == mapperName));
            if (mapperList.Count() == 0) throw new KeyNotFoundException("Can't find mapper");
            var mapper = mapperList.First();
            Console.WriteLine($"Using {Path.GetFileName(mapper.Key)} as mapper file");
            return mapper.Value;
        }

        static void Dump(FamicomDumperConnection dumper, string fileName, string mapperName, int prgSize, int chrSize, string unifName, string unifAuthor)
        {
            var mapper = GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine($"Using mapper: #{mapper.Number} ({mapper.Name})");
            else
                Console.WriteLine($"Using mapper: {mapper.Name}");
            Console.WriteLine("Dumping...");
            List<byte> prg = new List<byte>();
            List<byte> chr = new List<byte>();
            prgSize = prgSize >= 0 ? prgSize : mapper.DefaultPrgSize;
            chrSize = chrSize >= 0 ? chrSize : mapper.DefaultChrSize;
            if (prgSize > 0)
            {
                Console.WriteLine("PRG memory size: {0}K", prgSize / 1024);
                mapper.DumpPrg(dumper, prg, prgSize);
                while (prg.Count % 0x4000 != 0) prg.Add(0);
            }
            if (chrSize > 0)
            {
                Console.WriteLine("CHR memory size: {0}K", chrSize / 1024);
                mapper.DumpChr(dumper, chr, chrSize);
                while (chr.Count % 0x2000 != 0) chr.Add(0);
            }
            bool[] mirroringRaw = dumper.GetMirroring();
            NesFile.MirroringType mirroring = NesFile.MirroringType.Unknown_none;
            if (mirroringRaw.Length == 1)
            {
                // Backward compatibility with old firmwares
                mirroring = mirroringRaw[0] ? NesFile.MirroringType.Vertical : NesFile.MirroringType.Horizontal;
                Console.WriteLine("Mirroring: " + mirroring);
            }
            else if (mirroringRaw.Length == 4)
            {
                switch (string.Format("{0}{1}{2}{3}", mirroringRaw[0] ? 1 : 0, mirroringRaw[1] ? 1 : 0, mirroringRaw[2] ? 1 : 0, mirroringRaw[3] ? 1 : 0))
                {
                    case "0011":
                        mirroring = NesFile.MirroringType.Horizontal; // Horizontal
                        break;
                    case "0101":
                        mirroring = NesFile.MirroringType.Vertical; // Vertical
                        break;
                    case "0000":
                        mirroring = NesFile.MirroringType.OneScreenA; // One-screen A
                        break;
                    case "1111":
                        mirroring = NesFile.MirroringType.OneScreenB; // One-screen B
                        break;
                    default:
                        mirroring = NesFile.MirroringType.Unknown_none; // Unknown
                        break;
                }
                Console.WriteLine("Mirroring: {0} ({1} {2} {3} {4})", mirroring, mirroringRaw[0] ? 1 : 0, mirroringRaw[1] ? 1 : 0, mirroringRaw[2] ? 1 : 0, mirroringRaw[3] ? 1 : 0);
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
                    throw new VerificationException("Failed!");
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
            Console.WriteLine("Replug cartridge and press any key");
            Console.ReadKey();
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
                throw new VerificationException("Failed!");
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
                    throw new VerificationException("Failed!");
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

        static void StartServer(FamicomDumperConnection dumper, uint tcpPort)
        {
            BinaryServerFormatterSinkProvider binaryServerFormatterSinkProvider
                = new BinaryServerFormatterSinkProvider();
            BinaryClientFormatterSinkProvider binaryClientFormatterSinkProvider
                = new BinaryClientFormatterSinkProvider();
            binaryServerFormatterSinkProvider.TypeFilterLevel
                = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
            var dict = new System.Collections.Hashtable();
            dict["name"] = "FamicomDumperServer";
            dict["port"] = tcpPort;
            dict["secure"] = false;
            var channel = new TcpChannel(dict, binaryClientFormatterSinkProvider, binaryServerFormatterSinkProvider);
            ChannelServices.RegisterChannel(channel, false);
            dumper.Verbose = true;
            RemotingServices.Marshal(dumper, "dumper");
            Console.WriteLine($"Listening port {tcpPort}, press any key to stop");
            Console.ReadKey();
            ChannelServices.UnregisterChannel(channel);
            channel.StopListening(null);
        }

        private static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }
    }
}
