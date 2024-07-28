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
using com.clusterrr.Famicom.Dumper.FlashWriters;
using com.clusterrr.Famicom.DumperConnection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Versioning;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace com.clusterrr.Famicom.Dumper
{
    public class Program
    {
        public const string APP_NAME = "Famicom Dumper Client";
        public const string REPO_PATH = "https://github.com/ClusterM/famicom-dumper-client";
        public const int DEFAULT_GRPC_PORT = 26673;
        public static DateTime BUILD_TIME = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(long.Parse(Properties.Resources.buildtime.Trim()));

        static int Main(string[] args)
        {
            var version = Assembly.GetExecutingAssembly()?.GetName()?.Version;
            var versionStr = $"{version?.Major}.{version?.Minor}{((version?.Build ?? 0) > 0 ? $"{(char)((byte)'a' + version!.Build)}" : "")}";
            Console.WriteLine($"{APP_NAME} " +
#if !INTERIM
                $"v{versionStr}"
#else
                "interim version"
#endif
#if DEBUG
                + " (debug)"
#endif
            );
#if INTERIM || DEBUG
            Console.WriteLine($"  Commit: {Properties.Resources.gitCommit} @ {REPO_PATH}");
            Console.WriteLine($"  Build time: {BUILD_TIME.ToLocalTime()}");
#endif
            Console.WriteLine("  (c) Alexey 'Cluster' Avdyukhin / https://cluster.wtf / cluster@cluster.wtf");
            Console.WriteLine("");
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            string port = "auto";
            string? mapperName = null;
            string? psize = null;
            string? csize = null;
            string? filename = null;
            bool battery = false;
            string? csFile = null;
            string[] csArgs = Array.Empty<string>();

            string? unifName = null;
            string? unifAuthor = null;

            string? prgRamSize = null;
            string? prgNvRamSize = null;
            string? chrRamSize = null;
            string? chrNvRamSize = null;

            byte fdsSides = 1;
            byte fdsSkipSides = 0;
            bool fdsUseHeader = true;
            bool fdsDumpHiddenFiles = false;

            bool reset = false;
            bool silent = true;
            bool needCheck = false;
            bool writePBBs = false;
            List<int> badSectors = new();
            bool ignoreBadSectors = false;

            int coolboySubmapper = -1;

            int tcpPort = DEFAULT_GRPC_PORT;
            string? remoteHost = null;

            try
            {
                if (args.Length == 0 || args[0] == "help" || args[0] == "--help" || args[0] == "/?")
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
                    while (param.StartsWith("-")) param = param[1..];
                    string value = i < args.Length - 1 ? args[i + 1] : "";
                    switch (param.ToLower())
                    {
                        case "p":
                        case "port":
                            port = value;
                            i++;
                            break;
                        case "mappers":
                            // Meh... using static field
                            Scripting.MappersSearchDirectories = new string[] { value };
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
                        case "fds-skip-sides":
                            fdsSkipSides = byte.Parse(value);
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
                        case "prg-ram-size":
                            prgRamSize = value;
                            i++;
                            break;
                        case "prg-nvram-size":
                            prgNvRamSize = value;
                            i++;
                            break;
                        case "chr-ram-size":
                            chrRamSize = value;
                            i++;
                            break;
                        case "chr-nvram-size":
                            chrNvRamSize = value;
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
                        case "coolboy-submapper":
                            coolboySubmapper = byte.Parse(value);
                            i++;
                            break;
                        default:
                            Console.WriteLine("Unknown option: " + param);
                            PrintHelp();
                            return 2;
                    }
                }

                if (command == "list-mappers")
                {
                    Scripting.ListMappers();
                    return 0;
                }

                IFamicomDumperConnectionExt dumper;
                if (string.IsNullOrEmpty(remoteHost))
                {
                    // Using local dumper
                    var localDumper = new FamicomDumperLocal();
                    localDumper.Open(port);
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
#if DEBUG
                Console.WriteLine($"Protocol version: {dumper.ProtocolVersion}");
#endif
                if (dumper.HardwareVersion != null)
                    Console.WriteLine($"Dumper hardware version: {dumper.HardwareVersion.Major}.{dumper.HardwareVersion.Minor}" +
                        $"{((dumper.HardwareVersion.Build != 0) ? new string((char)dumper.HardwareVersion.Build, 1) : "")}");
                if (dumper.FirmwareVersion != null)
                    Console.WriteLine($"Dumper firmware version: {dumper.FirmwareVersion.Major}.{dumper.FirmwareVersion.Minor}" +
                        $"{((dumper.FirmwareVersion.Build != 0) ? new string((char)dumper.FirmwareVersion.Build, 1) : "")}");

                try
                {
                    if (reset)
                        Reset(dumper);
                    if (dumper.ProtocolVersion >= 4)
                        dumper.SetCoolboyGpioMode(false);

                    if (!string.IsNullOrEmpty(csFile))
                    {
                        Scripting.CompileAndExecute(csFile, dumper, filename, mapperName,
                            ParseSize(psize), ParseSize(csize),
                            ParseSize(prgRamSize), ParseSize(chrRamSize),
                            ParseSize(prgNvRamSize), ParseSize(chrNvRamSize),
                            unifName, unifAuthor, battery, csArgs);
                    }

                    switch (command)
                    {
                        case "reset":
                            if (!reset)
                                Reset(dumper);
                            break;
                        case "list-mappers":
                            Scripting.ListMappers();
                            break;
                        case "dump":
                            Dump(dumper, filename ?? "output.nes", mapperName,
                                ParseSize(psize), ParseSize(csize),
                                ParseSize(prgRamSize), ParseSize(prgNvRamSize),
                                ParseSize(chrRamSize), ParseSize(chrNvRamSize),
                                unifName, unifAuthor, battery);
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
                                throw new ArgumentException("Please specify ROM filename using --file argument");
                            FDS.WriteFDS(dumper, filename, needCheck, fdsSkipSides);
                            break;
                        case "write-prg-ram":
                        case "write-sram":
                            if (string.IsNullOrEmpty(filename))
                                throw new ArgumentException("Please specify ROM filename using --file argument");
                            WritePrgRam(dumper, filename, mapperName);
                            break;
                        case "write-coolboy":
                        case "write-coolboy-direct":
                            if (string.IsNullOrEmpty(filename))
                                throw new ArgumentException("Please specify ROM filename using --file argument");
                            new CoolboyWriter(dumper, coolboyGpioMode: false, coolboySubmapper).Write(filename, badSectors, silent, needCheck, writePBBs, ignoreBadSectors);
                            break;
                        case "write-coolboy-gpio":
                            if (string.IsNullOrEmpty(filename))
                                throw new ArgumentException("Please specify ROM filename using --file argument");
                            new CoolboyWriter(dumper, coolboyGpioMode: true, coolboySubmapper).Write(filename, badSectors, silent, needCheck, writePBBs, ignoreBadSectors);
                            break;
                        case "write-coolgirl":
                            if (string.IsNullOrEmpty(filename))
                                throw new ArgumentException("Please specify ROM filename using --file argument");
                            new CoolgirlWriter(dumper).Write(filename, badSectors, silent, needCheck, writePBBs, ignoreBadSectors);
                            break;
                        case "write-unrom512":
                            if (string.IsNullOrEmpty(filename))
                                throw new ArgumentException("Please specify ROM filename using --file argument");
                            new Unrom512Writer(dumper).Write(filename, badSectors, silent, needCheck, writePBBs, ignoreBadSectors);
                            break;
                        case "info-coolboy":
                            new CoolboyWriter(dumper, coolboyGpioMode: false, coolboySubmapper).PrintFlashInfo();
                            break;
                        case "info-coolboy-gpio":
                            new CoolboyWriter(dumper, coolboyGpioMode: true, coolboySubmapper).PrintFlashInfo();
                            break;
                        case "info-coolgirl":
                            new CoolgirlWriter(dumper).PrintFlashInfo();
                            break;
                        case "info-unrom512":
                            new Unrom512Writer(dumper).PrintFlashInfo();
                            break;
                        case "script":
                            if (string.IsNullOrEmpty(csFile))
                                throw new ArgumentException("Please specify C# script using --cs-file argument");
                            break;
                        case "server":
                            if (dumper is FamicomDumperLocal d)
                                StartServer(d, tcpPort);
                            break;
                        default:
                            Console.WriteLine("Unknown command: " + command);
                            PrintHelp();
                            return 2;
                    }
#if DEBUG
                    var timePassed = stopwatch.Elapsed;
                    if (timePassed.TotalMinutes >= 60)
                        Console.WriteLine($"Done in {timePassed.Hours}:{timePassed.Minutes:D2}:{timePassed.Seconds:D2}");
                    else if (timePassed.TotalSeconds >= 10)
                        Console.WriteLine($"Done in {timePassed.Minutes:D2}:{timePassed.Seconds:D2}");
                    else
                        Console.WriteLine($"Done in {(int)timePassed.TotalMilliseconds}ms");
#else
                    Console.WriteLine("Done.");
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
#if DEBUG
                Console.WriteLine($"ERROR {ex.GetType()}: {ex.Message}{ex.StackTrace}");
#else
                Console.WriteLine($"ERROR: {ex.Message}");
#endif
                if (!silent) PlayErrorSound();
                return 1;
            }
            return 0;
        }

        static int ParseSize(string? size)
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

        static void PlayBeepSound()
        {
            Console.Beep();
        }

        public static void PlayErrorSound()
        {
            if (OperatingSystem.IsWindows())
                PlayErrorSoundWav();
            else
                PlayBeepSound();
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
            {
                PlayDoneSoundWav();
            }
            else
            {
                PlayBeepSound();
                PlayBeepSound();
                PlayBeepSound();
            }
        }
        #endregion

        static void PrintHelp()
        {
            Console.WriteLine($"Usage: {Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName)} <command> [<options>] [- <cs_script_arguments>]");
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
            Console.WriteLine(" {0,-30}{1}", "write-coolboy-gpio", "write COOLBOY cartridge using dumper's GPIO pins");
            Console.WriteLine(" {0,-30}{1}", "write-coolgirl", "write COOLGIRL cartridge");
            Console.WriteLine(" {0,-30}{1}", "write-unrom512", "write UNROM-512 cartridge");
            Console.WriteLine(" {0,-30}{1}", "info-coolboy", "show information about COOLBOY cartridge's flash memory");
            Console.WriteLine(" {0,-30}{1}", "info-coolboy-gpio", "show information about COOLBOY cartridge's flash memory using dumper's GPIO pins");
            Console.WriteLine(" {0,-30}{1}", "info-coolgirl", "show information about COOLGIRL cartridge's flash memory");
            Console.WriteLine(" {0,-30}{1}", "info-unrom512", "show information about UNROM-512 cartridge's flash memory");
            Console.WriteLine();
            Console.WriteLine("Available options:");
            Console.WriteLine(" {0,-30}{1}", "--port <com>", "serial port of dumper or serial number of dumper device, default - auto");
            Console.WriteLine(" {0,-30}{1}", "--tcp-port <port>", $"TCP port for gRPC communication, default - {DEFAULT_GRPC_PORT}");
            Console.WriteLine(" {0,-30}{1}", "--host <host>", "enable gRPC client and connect to specified host");
            Console.WriteLine(" {0,-30}{1}", "--mappers <directory>", "directory to search mapper scripts");
            Console.WriteLine(" {0,-30}{1}", "--mapper <mapper>", "number, name or path to C# script of mapper for dumping, default - 0 (NROM)");
            Console.WriteLine(" {0,-30}{1}", "--file <output.nes>", "output/input filename (.nes, .fds, .sav, etc.)");
            Console.WriteLine(" {0,-30}{1}", "--prg-size <size>", "size of PRG memory to dump, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-30}{1}", "--chr-size <size>", "size of CHR memory to dump, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-30}{1}", "--prg-ram-size <size>", "size of PRG RAM memory for NES 2.0 header, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-30}{1}", "--chr-ram-size <size>", "size of CHR RAM memory for NES 2.0 header, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-30}{1}", "--prg-nvram-size <size>", "size of PRG NVRAM memory for NES 2.0 header, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-30}{1}", "--chr-nvram-size <size>", "size of CHR NVRAM memory for NES 2.0 header, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-30}{1}", "--battery", "set \"battery\" flag in ROM header after dumping");
            Console.WriteLine(" {0,-30}{1}", "--unif-name <name>", "internal ROM name for UNIF dumps");
            Console.WriteLine(" {0,-30}{1}", "--unif-author <name>", "author of dump name for UNIF dumps");
            Console.WriteLine(" {0,-30}{1}", "--fds-sides <sides>", "number of FDS sides to dump (default - 1)");
            Console.WriteLine(" {0,-30}{1}", "--fds-skip-sides <sides>", "number of FDS sides to skip while writing (default - 0)");
            Console.WriteLine(" {0,-30}{1}", "--fds-no-header", "do not add header to output file during FDS dumping");
            Console.WriteLine(" {0,-30}{1}", "--fds-dump-hidden", "try to dump hidden files during FDS dumping (used for some copy-protected games)");
            Console.WriteLine(" {0,-30}{1}", "--coolboy-submapper <submapper>", "submapper number to use while writing COOLBOY (default - auto, based on a ROM header)");
            Console.WriteLine(" {0,-30}{1}", "--reset", "simulate reset first");
            Console.WriteLine(" {0,-30}{1}", "--cs-file <C#_file>", "execute C# script from file");
            Console.WriteLine(" {0,-30}{1}", "--bad-sectors <bad_sectors>", "comma separated list of bad sectors for COOLBOY/COOLGIRL writing");
            Console.WriteLine(" {0,-30}{1}", "--ignore-bad-sectors", "ignore bad sectors while writing COOLBOY/COOLGIRL");
            Console.WriteLine(" {0,-30}{1}", "--verify", "verify COOLBOY/COOLGIRL/UNROM-512/FDS after writing");
            Console.WriteLine(" {0,-30}{1}", "--lock", "write-protect COOLBOY/COOLGIRL sectors after writing");
            Console.WriteLine(" {0,-30}{1}", "--sound", "play sound when done or error occured");
        }

        static public void Reset(IFamicomDumperConnectionExt dumper)
        {
            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
        }

        static void Dump(IFamicomDumperConnectionExt dumper, string fileName, string? mapperName, int prgSize, int chrSize, int
            prgRamSize, int prgNvRamSize, int chrRamSize, int chrNvRamSize, string? unifName, string? unifAuthor, bool battery)
        {
            var mapper = Scripting.GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine($"Using mapper: " +
                    $"#{mapper.Number}{(mapper.Submapper > 0 ? $".{mapper.Submapper}" : "" )}" +
                    $"{(!string.IsNullOrEmpty(mapper.Name) ? $" ({mapper.Name})" : "")}");
            else
                Console.WriteLine($"Using UNIF mapper: {mapper.Name}");

            // Get optional properties
            // int DefaultPrgRamSize -> PRG RAM size
            if (prgRamSize < 0) prgRamSize = mapper.DefaultPrgRamSize;
            // int DefaultChrRamSize -> CHR RAM size
            if (chrRamSize < 0) chrRamSize = mapper.DefaultChrRamSize;
            // int DefaultPrgNvramSize -> PRG NVRAM size
            if (prgNvRamSize < 0) prgNvRamSize = mapper.DefaultPrgNvramSize;
            // int DefaultChrNvramSize -> CHR NVRAM size
            if (chrNvRamSize < 0) chrNvRamSize = mapper.DefaultChrNvramSize;

            Console.WriteLine("Dumping...");
            var prg = new List<byte>();
            var chr = new List<byte>();
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
            var mirroring = mapper.GetMirroring(dumper);
            Console.WriteLine($"Mirroring: {mirroring}");
            var ext = Path.GetExtension(fileName);
            switch (ext.ToLower())
            {
                case ".nes":
                    // Using iNES or NES 2.0 container
                    var nesFile = new NesFile();
                    nesFile.Version = ((mapper.Number > 255) || (mapper.Submapper > 0)
                            || (prgRamSize >= 0) || (prgNvRamSize >= 0)
                            || (chrRamSize >= 0) || (chrNvRamSize >= 0))
                        ? NesFile.iNesVersion.NES20 : NesFile.iNesVersion.iNES;
                    Console.Write($"Saving ROM as {(nesFile.Version switch { NesFile.iNesVersion.NES20 => "NES 2.0", _ => "iNES" })} file: {fileName}... ");
                    if (mapper.Number < 0)
                        throw new NotSupportedException("Can't save ROM as .nes file: mapper number unknown");
                    nesFile.Mapper = (ushort)mapper.Number;
                    nesFile.Submapper = mapper.Submapper;
                    nesFile.Mirroring = mirroring switch
                    {
                        MirroringType.Horizontal => MirroringType.Horizontal,
                        MirroringType.Vertical => MirroringType.Vertical,
                        MirroringType.FourScreenVram => MirroringType.FourScreenVram,
                        _ => MirroringType.Horizontal
                    };
                    nesFile.PRG = prg.ToArray();
                    nesFile.CHR = chr.ToArray();
                    nesFile.PrgRamSize = (uint)Math.Max(0, prgRamSize);
                    nesFile.PrgNvRamSize = (uint)Math.Max(0, prgNvRamSize);
                    nesFile.ChrRamSize = (uint)Math.Max(0, chrRamSize);
                    nesFile.ChrNvRamSize = (uint)Math.Max(0, chrNvRamSize);
                    nesFile.Battery = battery || nesFile.PrgNvRamSize > 0 || nesFile.ChrNvRamSize > 0;
                    nesFile.Save(fileName);
                    break;
                case ".unf":
                case ".unif":
                    // Using UNIF container
                    Console.Write($"Saving ROM as UNIF file: {fileName}... ");
                    if (string.IsNullOrEmpty(mapper.UnifName))
                        throw new NotSupportedException("Can't save ROM as UNIF file - mapper code name unknown");
                    var unifFile = new UnifFile
                    {
                        Version = 5,
                        Mapper = mapper.UnifName
                    };
                    if (unifName != null)
                        unifFile.GameName = unifName;
                    unifFile.PRG0 = prg.ToArray();
                    if (chr.Count > 0)
                        unifFile.CHR0 = chr.ToArray();
                    unifFile.Mirroring = mirroring;
                    unifFile.Battery = battery;
                    if (!string.IsNullOrEmpty(unifAuthor))
                        unifFile.DumperName = unifAuthor;
                    unifFile.DumpingSoftware = $"Famicom Dumper by Cluster / {REPO_PATH}";
                    unifFile.Save(fileName);
                    break;
                case ".bin":
                    // Save as raw binary file
                    Console.Write($"Saving as raw binary file: {fileName}... ");
                    File.WriteAllBytes(fileName, prg.ToArray());
                    if (chr.Count > 0) Console.Write("WARNING! CHR is not saved! PRG only. ");
                    break;
                default:
                    throw new NotSupportedException($"Unknown extention {ext}, can't determine container type");
            }
            Console.WriteLine("OK");
        }

        static void ReadPrgRam(IFamicomDumperConnectionExt dumper, string fileName, string? mapperName)
        {
            var mapper = Scripting.GetMapper(mapperName);
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
            dumper.ReadCpu(0); // to avoid corruption
            Reset(dumper);
        }

        static void WritePrgRam(IFamicomDumperConnectionExt dumper, string fileName, string? mapperName)
        {
            var mapper = Scripting.GetMapper(mapperName);
            if (mapper.Number >= 0)
                Console.WriteLine($"Using mapper: #{mapper.Number} ({mapper.Name})");
            else
                Console.WriteLine($"Using mapper: {mapper.Name}");
            mapper.EnablePrgRam(dumper);
            Console.Write("Writing PRG RAM... ");
            var prgram = File.ReadAllBytes(fileName);
            dumper.WriteCpu(0x6000, prgram);
            Console.WriteLine("OK");
            dumper.ReadCpu(0); // to avoid corruption
            Reset(dumper);
        }

        static void StartServer(FamicomDumperLocal dumper, int tcpPort)
        {
            Console.WriteLine($"Listening port {tcpPort}, press Ctrl-C to stop");
            FamicomDumperService.StartServer(dumper, $"http://0.0.0.0:{tcpPort}");
        }
    }
}
