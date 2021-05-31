/* Famicom Dumper/Programmer
 *
 * Copyright notice for this file:
 *  Copyright (C) 2021 Cluster
 *  https://clusterrr.com
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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Versioning;
using System.Security;

namespace com.clusterrr.Famicom
{
    public class Program
    {
        private static string[] MappersSearchDirectories = {
            //Path.Combine(AppContext.BaseDirectory, "mappers"),
            Path.Combine(Directory.GetCurrentDirectory(), "mappers"),
            "/usr/share/famicom-dumper/mappers"
        };
        private static readonly string[] ScriptsSearchDirectories = {
            //Path.Combine(AppContext.BaseDirectory, "scripts"),
            Path.Combine(Directory.GetCurrentDirectory(), "scripts"),
            "/usr/share/famicom-dumper/scripts"
        };

        private const string SCRIPTS_CACHE_DIRECTORY = ".dumpercache";
        private const string SCRIPT_START_METHOD = "Run";
        private const string REPO_PATH = "https://github.com/ClusterM/famicom-dumper-client";
        private const int DEFAULT_GRPC_PORT = 26673;
        private static DateTime BUILD_TIME = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(long.Parse(Properties.Resources.buildtime.Trim()));

        static int Main(string[] args)
        {
            Console.WriteLine($"Famicom Dumper Client v{Assembly.GetExecutingAssembly().GetName().Version.Major}.{Assembly.GetExecutingAssembly().GetName().Version.Minor}");
            Console.WriteLine($"  Commit {Properties.Resources.gitCommit} @ {REPO_PATH}");
#if DEBUG
            Console.WriteLine($"  Debug version, build time: {BUILD_TIME.ToLocalTime()}");
#endif
            Console.WriteLine("  (c) Alexey 'Cluster' Avdyukhin / https://clusterrr.com / clusterrr@clusterrr.com");
            Console.WriteLine("");
            var startTime = DateTime.Now;
            string port = "auto";
            string mapperName = null;
            string psize = null;
            string csize = null;
            string filename = null;
            bool battery = false;
            string csFile = null;
            string[] csArgs = Array.Empty<string>();
            string unifName = null;
            string unifAuthor = null;
            bool reset = false;
            bool silent = true;
            bool needCheck = false;
            bool writePBBs = false;
            List<int> badSectors = new();
            int tcpPort = DEFAULT_GRPC_PORT;
            bool ignoreBadSectors = false;
            string remoteHost = null;
            byte fdsSides = 1;
            bool fdsUseHeader = true;
            bool fdsDumpHiddenFiles = false;
            try
            {
                if (args.Length == 0 || args.Contains("help") || args.Contains("--help"))
                {
                    PrintHelp();
                    return 0;
                }

                string command = args[0].ToLower();

                for (int i = 1; i < args.Length; i++)
                {
                    string param = args[i];
                    if (param == "-")
                    {
                        csArgs = args.Skip(i + 1).ToArray();
                        break;
                    }
                    while (param.StartsWith("-") || param.StartsWith("—")) param = param[1..];
                    string value = i < args.Length - 1 ? args[i + 1] : "";
                    switch (param.ToLower())
                    {
                        case "p":
                        case "port":
                            port = value;
                            i++;
                            break;
                        case "mappers":
                            //MappersSearchDirectories = MappersSearchDirectories.Append(value).ToArray();
                            MappersSearchDirectories = new string[] { value };
                            i++;
                            break;
                        case "m":
                        case "mapper":
                            mapperName = value;
                            i++;
                            break;
                        case "f":
                        case "file":
                            filename = value;
                            i++;
                            break;
                        case "fds-sides":
                            fdsSides = byte.Parse(value);
                            i++;
                            break;
                        case "fds-no-header":
                            fdsUseHeader = false;
                            break;
                        case "fds-dump-hidden":
                            fdsDumpHiddenFiles = true;
                            break;
                        case "csfile":
                        case "cs-file":
                        case "scriptfile":
                        case "script-file":
                            csFile = value;
                            i++;
                            break;
                        case "psize":
                        case "prg-size":
                            psize = value;
                            i++;
                            break;
                        case "csize":
                        case "chr-size":
                            csize = value;
                            i++;
                            break;
                        case "battery":
                            battery = true;
                            break;
                        case "unifname":
                        case "unif-name":
                            unifName = value;
                            i++;
                            break;
                        case "unifauthor":
                        case "unif-author":
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
                        case "verify":
                            needCheck = true;
                            break;
                        case "lock":
                            writePBBs = true;
                            break;
                        case "badsectors":
                        case "bad-sectors":
                            foreach (var v in value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                                badSectors.Add(int.Parse(v));
                            i++;
                            break;
                        case "tcpport":
                        case "tcp-port":
                            tcpPort = int.Parse(value);
                            i++;
                            break;
                        case "host":
                            remoteHost = value;
                            i++;
                            break;
                        case "ignorebadsectors":
                        case "ignore-bad-sectors":
                            ignoreBadSectors = true;
                            break;
                        default:
                            Console.WriteLine("Unknown option: " + param);
                            PrintHelp();
                            return 2;
                    }
                }

                if (command == "list-mappers")
                {
                    ListMappers();
                    return 0;
                }

                IFamicomDumperConnectionExt dumper;
                if (string.IsNullOrEmpty(remoteHost))
                {
                    // Using local dumper
                    var localDumper = new FamicomDumperLocal(port);
                    localDumper.Open();
                    dumper = localDumper;
                }
                else
                {
                    // Using remote dumper
                    dumper = new FamicomDumperClient($"http://{remoteHost}:{tcpPort}");
                }

                Console.Write("Dumper initialization... ");
                bool initResult = dumper.Init();
                if (!initResult) throw new IOException("Can't init dumper");
                Console.WriteLine("OK");

                try
                {
                    if (reset)
                        Reset(dumper);

                    if (!string.IsNullOrEmpty(csFile))
                    {
                        CompileAndExecute(csFile, dumper, filename, mapperName, ParseSize(psize), ParseSize(csize), unifName, unifAuthor, battery, csArgs);
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
                            Dump(dumper, filename ?? "output.nes", mapperName, ParseSize(psize), ParseSize(csize), unifName, unifAuthor, battery);
                            break;
                        case "dump-fds":
                            FDS.DumpFDS(dumper, filename ?? "output.fds", fdsSides, fdsDumpHiddenFiles, fdsUseHeader);
                            break;
                        case "read-prg-ram":
                        case "dump-prg-ram":
                        case "dump-sram":
                            ReadPrgRam(dumper, filename ?? "savegame.sav", mapperName);
                            break;
                        case "write-fds":
                            if (string.IsNullOrEmpty(filename))
                                throw new ArgumentNullException("--file", "Please specify ROM filename using --file argument");
                            FDS.WriteFDS(dumper, filename, needCheck);
                            break;
                        case "write-prg-ram":
                        case "write-sram":
                            if (string.IsNullOrEmpty(filename))
                                throw new ArgumentNullException("--file", "Please specify ROM filename using --file argument");
                            WritePrgRam(dumper, filename, mapperName);
                            break;
                        case "write-coolboy":
                        case "write-coolboy-direct":
                        case "write-coolboy-gpio": // for backward compatibility
                            if (string.IsNullOrEmpty(filename))
                                throw new ArgumentNullException("--file", "Please specify ROM filename using --file argument");
                            CoolboyWriter.Write(dumper, filename, badSectors, silent, needCheck, writePBBs, ignoreBadSectors);
                            break;
                        case "write-coolgirl":
                            if (string.IsNullOrEmpty(filename))
                                throw new ArgumentNullException("--file", "Please specify ROM filename using --file argument");
                            CoolgirlWriter.Write(dumper, filename, badSectors, silent, needCheck, writePBBs, ignoreBadSectors);
                            break;
                        case "info-coolboy":
                            CoolboyWriter.PrintFlashInfo(dumper);
                            break;
                        case "info-coolgirl":
                            CoolgirlWriter.PrintFlashInfo(dumper);
                            break;
                        case "bootloader":
                            Bootloader(dumper);
                            break;
                        case "script":
                            if (string.IsNullOrEmpty(csFile))
                                throw new ArgumentNullException("--cs-file", "Please specify C# script using --cs-file argument");
                            break;
                        case "server":
                            StartServer(dumper as FamicomDumperLocal, tcpPort);
                            break;
                        default:
                            Console.WriteLine("Unknown command: " + command);
                            PrintHelp();
                            return 2;
                    }
#if DEBUG
                    var timePassed = DateTime.Now - startTime;
                    if (timePassed.TotalMinutes >= 60)
                        Console.WriteLine($"Done in {timePassed.Hours}:{timePassed.Minutes:D2}:{timePassed.Seconds:D2}");
                    else if (timePassed.TotalSeconds >= 10)
                        Console.WriteLine($"Done in {timePassed.Minutes:D2}:{timePassed.Seconds:D2}");
                    else
                        Console.WriteLine($"Done in {(int)timePassed.TotalMilliseconds}ms");
#endif
                    if (!silent) PlayDoneSound();
                }
                finally
                {
                    dumper.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR {ex.GetType()}: " + ex.Message
#if DEBUG
                    + ex.StackTrace
#endif
                    );
                if (!silent)
                    PlayErrorSound();
                return 1;
            }
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

        #region Sounds
        [SupportedOSPlatform("windows")]
        static void PlayErrorSoundWav()
        {
            var errorSound = new SoundPlayer(Properties.Resources.ErrorSound);
            errorSound.PlaySync();
        }

        static void PlayErrorSoundBeep()
        {
            Console.Beep();
        }

        public static void PlayErrorSound()
        {
            if (OperatingSystem.IsWindows())
                PlayErrorSoundWav();
            else
                PlayErrorSoundBeep();
        }

        [SupportedOSPlatform("windows")]
        static void PlayDoneSoundWav()
        {
            var doneSound = new SoundPlayer(Properties.Resources.DoneSound);
            doneSound.PlaySync();
        }

        public static void PlayDoneSound()
        {
            if (OperatingSystem.IsWindows())
                PlayDoneSoundWav();
        }
        #endregion

        static void PrintHelp()
        {
            Console.WriteLine("Usage: famicom-dumper <command> [<options>] [- <cs_script_arguments>]");
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            Console.WriteLine(" {0,-30}{1}", "list-mappers", "list available mappers to dump");
            Console.WriteLine(" {0,-30}{1}", "dump", "dump cartridge");
            Console.WriteLine(" {0,-30}{1}", "server", "start gRPC server");
            Console.WriteLine(" {0,-30}{1}", "script", "execute C# script specified by --cs-file option");
            Console.WriteLine(" {0,-30}{1}", "reset", "simulate reset (M2 goes to Z-state for a second)");
            Console.WriteLine(" {0,-30}{1}", "dump-fds", "dump FDS card using RAM adapter and FDS drive");
            Console.WriteLine(" {0,-30}{1}", "write-fds", "write FDS card using RAM adapter and FDS drive");
            Console.WriteLine(" {0,-30}{1}", "read-prg-ram", "read PRG RAM (battery backed save if exists)");
            Console.WriteLine(" {0,-30}{1}", "write-prg-ram", "write PRG RAM");
            Console.WriteLine(" {0,-30}{1}", "write-coolboy", "write COOLBOY cartridge");
            Console.WriteLine(" {0,-30}{1}", "write-coolgirl", "write COOLGIRL cartridge");
            Console.WriteLine(" {0,-30}{1}", "info-coolboy", "show information about COOLBOY's flash memory");
            Console.WriteLine(" {0,-30}{1}", "info-coolgirl", "show information about COOLGIRL's flash memory");
            Console.WriteLine();
            Console.WriteLine("Available options:");
            Console.WriteLine(" {0,-30}{1}", "--port <com>", "serial port of dumper or serial number of dumper device, default - auto");
            Console.WriteLine(" {0,-30}{1}", "--tcp-port <port>", $"TCP port for gRPC communication, default - {DEFAULT_GRPC_PORT}");
            Console.WriteLine(" {0,-30}{1}", "--host <host>", "enable gRPC client and connect to specified host");
            Console.WriteLine(" {0,-30}{1}", "--mappers <directory>", "directory to search mapper scripts");
            Console.WriteLine(" {0,-30}{1}", "--mapper <mapper>", "number, name or path to C# script of mapper for dumping, default - 0 (NROM)");
            Console.WriteLine(" {0,-30}{1}", "--file <output.nes>", "output/input filename (.nes, .fds, .png or .sav)");
            Console.WriteLine(" {0,-30}{1}", "--prg-size <size>", "size of PRG memory to dump, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-30}{1}", "--chr-size <size>", "size of CHR memory to dump, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-30}{1}", "--battery", "set \"battery\" flag in ROM header after dumping");
            Console.WriteLine(" {0,-30}{1}", "--cs-file <C#_file>", "execute C# script from file");
            Console.WriteLine(" {0,-30}{1}", "--reset", "simulate reset first");
            Console.WriteLine(" {0,-30}{1}", "--unif-name <name>", "internal ROM name for UNIF dumps");
            Console.WriteLine(" {0,-30}{1}", "--unif-author <name>", "author of dump for UNIF dumps");
            Console.WriteLine(" {0,-30}{1}", "--fds-sides", "number of FDS sides to dump (default 1)");
            Console.WriteLine(" {0,-30}{1}", "--fds-no-header", "do not add header to output file during FDS dumping");
            Console.WriteLine(" {0,-30}{1}", "--fds-dump-hidden", "try to dump hidden files during FDS dumping (used for some copy-protected games)");
            Console.WriteLine(" {0,-30}{1}", "--bad-sectors <bad_sectors>", "comma separated list of bad sectors for COOLBOY/COOLGIRL writing");
            Console.WriteLine(" {0,-30}{1}", "--ignore-bad-sectors", "ignore bad sectors while writing COOLBOY/COOLGIRL");
            Console.WriteLine(" {0,-30}{1}", "--sound", "play sound when done or error occured");
            Console.WriteLine(" {0,-30}{1}", "--verify", "verify COOLBOY/COOLGIRL/FDS after writing");
            Console.WriteLine(" {0,-30}{1}", "--lock", "write-protect COOLBOY/COOLGIRL sectors after writing");
        }

        static public void Reset(IFamicomDumperConnectionExt dumper)
        {
            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
        }

        static Assembly Compile(string path)
        {
            int linesOffset = 0;
            var source = File.ReadAllText(path);
            var cacheDirectory = Path.Combine(Path.GetDirectoryName(path), SCRIPTS_CACHE_DIRECTORY);
            var cacheFile = Path.Combine(cacheDirectory, Path.GetFileNameWithoutExtension(path)) + ".dll";

            // Try to load cached assembly
            ;
            if (File.Exists(cacheFile))
            {
                var cacheCompileTime = new FileInfo(cacheFile).LastWriteTime;
                if ((cacheCompileTime >= new FileInfo(path).LastWriteTime) // recompile if script was changed
                    && (cacheCompileTime >= BUILD_TIME.ToLocalTime())) // recompile if our app is newer
                {
                    try
                    {
                        var rawAssembly = File.ReadAllBytes(cacheFile);
                        return Assembly.Load(rawAssembly);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Can't load cached compiled script file: {ex.Message}");
                    }
                }
            }

            // And usings
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            var usings = root.Usings.Select(e => e.Name.ToString());
            var usingsToAdd = new string[]
            {
                "System",
                "System.IO",
                "System.Collections.Generic",
                "System.Linq",
                "com.clusterrr.Famicom",
                "com.clusterrr.Famicom.DumperConnection",
                "com.clusterrr.Famicom.Containers"
            };
            foreach (var @using in usingsToAdd)
            {
                if (!usings.Contains(@using))
                {
                    source = $"using {@using};\r\n" + source;
                    linesOffset++; // for correct line numbers in errors
                }
            }
            tree = CSharpSyntaxTree.ParseText(source);

            // Loading assemblies
            var domainAssemblys = AppDomain.CurrentDomain.GetAssemblies();
            var metadataReferenceList = new List<MetadataReference>();
            foreach (var assembl in domainAssemblys)
            {
                unsafe
                {
                    assembl.TryGetRawMetadata(out byte* blob, out int length);
                    var moduleMetadata = ModuleMetadata.CreateFromMetadata((IntPtr)blob, length);
                    var assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);
                    var metadataReference = assemblyMetadata.GetReference();
                    metadataReferenceList.Add(metadataReference);
                }
            }
            unsafe
            {
                // Add extra refs
                // FamicomDumperConnection.dll
                typeof(IFamicomDumperConnectionExt).Assembly.TryGetRawMetadata(out byte* blob, out int length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
                // NesContainers.dll
                typeof(NesFile).Assembly.TryGetRawMetadata(out blob, out length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
                // for image processing
                typeof(System.Drawing.Bitmap).Assembly.TryGetRawMetadata(out blob, out length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
                typeof(ImageFormat).Assembly.TryGetRawMetadata(out blob, out length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
                // wtf is it?
                typeof(System.Linq.Expressions.Expression).Assembly.TryGetRawMetadata(out blob, out length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
            }

            // Compile
            var cs = CSharpCompilation.Create("Script", new[] { tree }, metadataReferenceList,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            using var memoryStream = new MemoryStream();
            EmitResult result = cs.Emit(memoryStream);
            foreach (Diagnostic d in result.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Hidden
#if !DEBUG
                && d.Severity != DiagnosticSeverity.Warning
#endif
                ))
            {
                Console.WriteLine($"{Path.GetFileName(path)} ({d.Location.GetLineSpan().StartLinePosition.Line - linesOffset + 1}, {d.Location.GetLineSpan().StartLinePosition.Character + 1}): {d.Severity.ToString().ToLower()} {d.Descriptor.Id}: {d.GetMessage()}");
            }
            if (result.Success)
            {
                var rawAssembly = memoryStream.ToArray();
                Assembly assembly = Assembly.Load(rawAssembly);
                // Save compiled assembly to cache (at least try)
                try
                {
                    if (!Directory.Exists(cacheDirectory))
                        Directory.CreateDirectory(cacheDirectory);
                    File.WriteAllBytes(cacheFile, rawAssembly);
                }
                catch { }
                return assembly;
            }
            else throw new InvalidProgramException();
        }

        static IMapper CompileMapper(string path)
        {
            Assembly assembly = Compile(path);
            var programs = assembly.GetTypes();
            if (!programs.Any())
                throw new InvalidProgramException("There is no assemblies");
            Type program = programs.First();
            var constructor = program.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, CallingConventions.Any, Array.Empty<Type>(), Array.Empty<ParameterModifier>());
            if (constructor == null)
                throw new InvalidProgramException("There is no valid default constructor");
            var mapper = constructor.Invoke(Array.Empty<object>());
            if (!(mapper is IMapper))
                throw new InvalidProgramException("Class doesn't implement IMapper interface");
            return mapper as IMapper;
        }

        static Dictionary<string, IMapper> CompileAllMappers()
        {
            var result = new Dictionary<string, IMapper>();
            var mappersSearchDirectories = MappersSearchDirectories.Distinct().Where(d => Directory.Exists(d));
            if (!mappersSearchDirectories.Any())
            {
                Console.WriteLine("None of the listed mappers directories were found:");
                foreach (var d in MappersSearchDirectories)
                    Console.WriteLine($" {d}");
            }
            foreach (var mappersDirectory in mappersSearchDirectories)
            {
                Console.WriteLine($"Compiling mappers in {mappersDirectory}...");
                foreach (var f in Directory.GetFiles(mappersDirectory, "*.cs", SearchOption.AllDirectories))
                {
                    result[f] = CompileMapper(f);
                }
            }
            return result;
        }

        static void ListMappers()
        {
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

        public static IMapper GetMapper(string mapperName)
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
            if (!mapperList.Any()) throw new KeyNotFoundException("Can't find mapper");
            var mapper = mapperList.First();
            Console.WriteLine($"Using {Path.GetFileName(mapper.Key)} as mapper file");
            return mapper.Value;
        }

        static void CompileAndExecute(string scriptPath, IFamicomDumperConnectionExt dumper, string filename, string mapperName, int prgSize, int chrSize, string unifName, string unifAuthor, bool battery, string[] args)
        {
            if (!File.Exists(scriptPath))
            {
                var scriptsPathes = ScriptsSearchDirectories.Select(d => Path.Combine(d, scriptPath)).Where(f => File.Exists(f));
                if (!scriptsPathes.Any())
                {
                    Console.WriteLine($"{Path.Combine(Directory.GetCurrentDirectory(), scriptPath)} not found");
                    foreach (var d in ScriptsSearchDirectories)
                        Console.WriteLine($"{Path.Combine(d, scriptPath)} not found");
                    throw new FileNotFoundException($"{scriptPath} not found");
                }
                scriptPath = scriptsPathes.First();
            }
            Console.WriteLine($"Compiling {scriptPath}...");
            Assembly assembly = Compile(scriptPath);
            var programs = assembly.GetTypes();
            if (!programs.Any())
                throw new InvalidProgramException("There is no assemblies");
            Type program = programs.First();

            try
            {
                object obj;
                MethodInfo method;

                // Let's check if static method exists
                var staticMethods = program.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m.Name == SCRIPT_START_METHOD);
                if (staticMethods.Any())
                {
                    obj = program;
                    method = staticMethods.First();
                }
                else
                {
                    // Let's try instance method, need to call constructor first
                    var constructor = program.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, CallingConventions.Any, Array.Empty<Type>(), Array.Empty<ParameterModifier>());
                    if (constructor == null)
                        throw new InvalidProgramException($"There is no static {SCRIPT_START_METHOD} method and no valid default constructor");
                    obj = constructor.Invoke(Array.Empty<object>());
                    // Is it instance method with string[] parameter?
                    var instanceMethods = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m.Name == SCRIPT_START_METHOD);
                    if (!instanceMethods.Any())
                    {
                        // Seems like there are no valid methods at all
                        throw new InvalidProgramException($"There is no {SCRIPT_START_METHOD} method");
                    }
                    method = instanceMethods.First();
                }

                var parameterInfos = method.GetParameters();
                List<object> parameters = new();
                bool filenameParamExists = false;
                bool mapperParamExists = false;
                bool prgSizeParamExists = false;
                bool chrSizeParamExists = false;
                bool unifNameParamExists = false;
                bool unifAuthorParamExists = false;
                bool batteryParamExists = false;
                bool argsParamExists = false;
                foreach (var parameterInfo in parameterInfos)
                {
                    var signature = $"{parameterInfo.ParameterType.Name} {parameterInfo.Name}";
                    switch (parameterInfo.Name.ToLower())
                    {
                        case "dumper":
                            parameters.Add(dumper);
                            break;
                        case "filename":
                            filenameParamExists = true;
                            if (string.IsNullOrEmpty(filename) && !parameterInfo.HasDefaultValue)
                                throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --file is not specified");
                            if (string.IsNullOrEmpty(filename) && parameterInfo.HasDefaultValue)
                                parameters.Add(parameterInfo.DefaultValue);
                            else
                                parameters.Add(filename);
                            break;
                        case "mapper":
                            mapperParamExists = true;
                            //if (string.IsNullOrEmpty(mapperName) && !parameterInfo.HasDefaultValue)
                            //    throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --mapper is not specified");
                            parameters.Add(GetMapper(mapperName));
                            break;
                        case "prgsize":
                            prgSizeParamExists = true;
                            if ((prgSize < 0) && !parameterInfo.HasDefaultValue)
                                throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --prg-size is not specified");
                            if ((prgSize < 0) && parameterInfo.HasDefaultValue)
                                parameters.Add(parameterInfo.DefaultValue);
                            else
                                parameters.Add(prgSize);
                            break;
                        case "chrsize":
                            chrSizeParamExists = true;
                            if ((chrSize < 0) && !parameterInfo.HasDefaultValue)
                                throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --chr-size is not specified");
                            if ((chrSize < 0) && parameterInfo.HasDefaultValue)
                                parameters.Add(parameterInfo.DefaultValue);
                            else
                                parameters.Add(chrSize);
                            break;
                        case "unifname":
                            unifNameParamExists = true;
                            if (string.IsNullOrEmpty(unifName) && !parameterInfo.HasDefaultValue)
                                throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --unif-name is not specified");
                            if (string.IsNullOrEmpty(unifName) && parameterInfo.HasDefaultValue)
                                parameters.Add(parameterInfo.DefaultValue);
                            else
                                parameters.Add(unifName);
                            break;
                        case "unifauthor":
                            unifAuthorParamExists = true;
                            if (string.IsNullOrEmpty(unifAuthor) && !parameterInfo.HasDefaultValue)
                                throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --unif-author is not specified");
                            if (string.IsNullOrEmpty(unifAuthor) && parameterInfo.HasDefaultValue)
                                parameters.Add(parameterInfo.DefaultValue);
                            else
                                parameters.Add(unifAuthor);
                            break;
                        case "battery":
                            batteryParamExists = true;
                            parameters.Add(battery);
                            break;
                        case "args":
                            argsParamExists = true;
                            parameters.Add(args);
                            break;
                        default:
                            switch (parameterInfo.ParameterType.Name)
                            {
                                // For backward compatibility
                                case nameof(IFamicomDumperConnection):
                                    parameters.Add(dumper);
                                    break;
                                case "String[]":
                                    argsParamExists = true;
                                    parameters.Add(args);
                                    break;
                                case nameof(IMapper):
                                    mapperParamExists = true;
                                    if (string.IsNullOrEmpty(mapperName) && !parameterInfo.HasDefaultValue)
                                        throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --mapper is not specified");
                                    if (string.IsNullOrEmpty(mapperName) && parameterInfo.HasDefaultValue)
                                        parameters.Add(parameterInfo.DefaultValue);
                                    else
                                        parameters.Add(GetMapper(mapperName));
                                    break;
                                default:
                                    throw new ArgumentException($"Unknown parameter: {signature}");
                            }
                            break;
                    }
                }
                if (!filenameParamExists && !string.IsNullOrEmpty(filename))
                    Console.WriteLine($"WARNING: --file argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"string filename\" parameter");
                if (!mapperParamExists && !string.IsNullOrEmpty(mapperName))
                    Console.WriteLine($"WARNING: --mapper argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"IMapper mapper\" parameter");
                if (!prgSizeParamExists && prgSize >= 0)
                    Console.WriteLine($"WARNING: --prg-size argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"int prgSize\" parameter");
                if (!chrSizeParamExists && chrSize >= 0)
                    Console.WriteLine($"WARNING: --chr-size argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"int chrSize\" parameter");
                if (!unifNameParamExists && !string.IsNullOrEmpty(unifName))
                    Console.WriteLine($"WARNING: --unif-name argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"string unifName\" parameter");
                if (!unifAuthorParamExists && !string.IsNullOrEmpty(unifAuthor))
                    Console.WriteLine($"WARNING: --unif-author argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"string unifAuthor\" parameter");
                if (!batteryParamExists && battery)
                    Console.WriteLine($"WARNING: --battery argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"bool battery\" parameter");
                if (!argsParamExists && args.Any())
                    Console.WriteLine($"WARNING: command line arguments are specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"string[] args\" parameter");

                // Start it!
                Console.WriteLine($"Running {program.Name}.{SCRIPT_START_METHOD}()...");
                method.Invoke(obj, parameters.ToArray());
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                    throw ex.InnerException;
                else
                    throw;
            }
        }

        static void Dump(IFamicomDumperConnectionExt dumper, string fileName, string mapperName, int prgSize, int chrSize, string unifName, string unifAuthor, bool battery)
        {
            var mapper = GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine($"Using mapper: #{mapper.Number} ({mapper.Name})");
            else
                Console.WriteLine($"Using mapper: {mapper.Name}");
            Console.WriteLine("Dumping...");
            List<byte> prg = new();
            List<byte> chr = new();
            prgSize = prgSize >= 0 ? prgSize : mapper.DefaultPrgSize;
            chrSize = chrSize >= 0 ? chrSize : mapper.DefaultChrSize;
            if (prgSize > 0)
            {
                Console.WriteLine($"PRG memory size: {prgSize / 1024}KB");
                mapper.DumpPrg(dumper, prg, prgSize);
                while (prg.Count % 0x4000 != 0) prg.Add(0);
            }
            if (chrSize > 0)
            {
                Console.WriteLine($"CHR memory size: {chrSize / 1024}KB");
                mapper.DumpChr(dumper, chr, chrSize);
                while (chr.Count % 0x2000 != 0) chr.Add(0);
            }
            NesFile.MirroringType mirroring;
            // TODO: move GetMapper to IMapper, so it will not be optional
            mirroring = mapper.GetMirroring(dumper);
            Console.WriteLine($"Mirroring: {mirroring}");
            Console.Write($"Saving to {fileName}... ");
            if (mapper.Number >= 0)
            {
                // TODO: add RAM and NV-RAM settings for NES 2.0
                var nesFile = new NesFile();
                var submapper = mapper.Submapper;
                nesFile.Version = (mapper.Number > 255 || submapper != 0)
                    ? NesFile.iNesVersion.NES20
                    : NesFile.iNesVersion.iNES;
                nesFile.Mapper = (ushort)mapper.Number;
                nesFile.Submapper = submapper;
                nesFile.Mirroring = mirroring;
                nesFile.PRG = prg.ToArray();
                nesFile.CHR = chr.ToArray();
                nesFile.Battery = battery;
                nesFile.Save(fileName);
            }
            else
            {
                var unifFile = new UnifFile
                {
                    Version = 4,
                    Mapper = mapper.UnifName
                };
                if (unifName != null)
                    unifFile.GameName = unifName;
                unifFile.Fields["PRG0"] = prg.ToArray();
                if (chr.Count > 0)
                    unifFile.Fields["CHR0"] = chr.ToArray();
                unifFile.Mirroring = mirroring;
                unifFile.Battery = battery;
                if (!string.IsNullOrEmpty(unifAuthor))
                    unifFile.DumperName = unifAuthor;
                unifFile.DumpingSoftware = $"Famicom Dumper by Cluster / {REPO_PATH}";
                unifFile.Save(fileName);
            }
            Console.WriteLine("OK");
        }

        static void ReadPrgRam(IFamicomDumperConnectionExt dumper, string fileName, string mapperName)
        {
            var mapper = GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine($"Using mapper: #{mapper.Number} ({mapper.Name})");
            else
                Console.WriteLine($"Using mapper: {mapper.Name}");
            mapper.EnablePrgRam(dumper);
            Console.Write("Reading PRG RAM... ");
            var prgram = dumper.ReadCpu(0x6000, 0x2000);
            Console.WriteLine("OK");
            Console.Write($"Saving to {fileName}... ");
            File.WriteAllBytes(fileName, prgram);
            Console.WriteLine("OK");
            dumper.ReadCpu(0x0, 1); // to avoid corruption
            Reset(dumper);
        }

        static void WritePrgRam(IFamicomDumperConnectionExt dumper, string fileName, string mapperName)
        {
            var mapper = GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine($"Using mapper: #{mapper.Number} ({mapper.Name})");
            else
                Console.WriteLine($"Using mapper: {mapper.Name}");
            mapper.EnablePrgRam(dumper);
            Console.Write("Writing PRG RAM... ");
            var prgram = File.ReadAllBytes(fileName);
            dumper.WriteCpu(0x6000, prgram);
            Console.WriteLine("OK");
            dumper.ReadCpu(0x0, 1); // to avoid corruption
            Reset(dumper);
        }

        static void TestPrgRam(IFamicomDumperConnectionExt dumper, string mapperName, int count = -1)
        {
            var mapper = GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine($"Using mapper: #{mapper.Number} ({mapper.Name})");
            else
                Console.WriteLine($"Using mapper: {mapper.Name}");
            mapper.EnablePrgRam(dumper);
            var rnd = new Random();
            while (count != 0)
            {
                var data = new byte[0x2000];
                rnd.NextBytes(data);
                Console.Write("Writing PRG RAM... ");
                dumper.WriteCpu(0x6000, data);
                Console.Write("Reading PRG RAM... ");
                var rdata = dumper.ReadCpu(0x6000, 0x2000);
                bool ok = true;
                for (int b = 0; b < 0x2000; b++)
                {
                    if (data[b] != rdata[b])
                    {
                        Console.WriteLine($"Mismatch at {b:X4}: {rdata[b]:X2} != {data[b]:X2}");
                        ok = false;
                    }
                }
                if (!ok)
                {
                    File.WriteAllBytes("prgramgood.bin", data);
                    Console.WriteLine("prgramgood.bin writed");
                    File.WriteAllBytes("prgrambad.bin", rdata);
                    Console.WriteLine("prgrambad.bin writed");
                    throw new VerificationException("Failed!");
                }
                Console.WriteLine("OK!");
                count--;
            }
        }

        static void Bootloader(IFamicomDumperConnectionExt dumper)
        {
            Console.WriteLine("Rebooting to bootloader...");
            if (dumper is FamicomDumperLocal)
                (dumper as FamicomDumperLocal).Bootloader();
            else
                throw new IOException("'bootloader' command for local dumper only");
        }

        static void StartServer(FamicomDumperLocal dumper, int tcpPort)
        {
            Console.WriteLine($"Listening port {tcpPort}, press Ctrl-C to stop");
            FamicomDumperService.StartServer(dumper, $"http://0.0.0.0:{tcpPort}");
        }
    }
}
