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

using Cluster.Famicom.Mappers;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;

namespace Cluster.Famicom
{
    class Program
    {
        static DateTime startTime;
        static SoundPlayer doneSound = new SoundPlayer(Properties.Resources.DoneSound);
        static SoundPlayer errorSound = new SoundPlayer(Properties.Resources.ErrorSound);

        static int Main(string[] args)
        {
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

                string command = args[0];

                for (int i = 1; i < args.Length; i++)
                {
                    string param = args[i];
                    while (param.StartsWith("-")) param = param.Substring(1);
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
                        case "silent":
                            silent = true;
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

                    switch (command.ToLower())
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
                        case "test-prg-ram-coolgirl":
                        case "test-sram-coolgirl":
                            TestPrgRamCoolgirl(dumper);
                            break;
                        case "test-battery":
                            TestBattery(dumper, mapper);
                            break;
                        case "test-chr-ram":
                            TestChrRam(dumper);
                            break;
                        case "test-chr-coolgirl":
                            TestChrRamCoolgirl(dumper);
                            break;
                        case "test-coolgirl":
                            TestCoolgirlFull(dumper, testCount);
                            break;
                        case "test-bads-coolgirl":
                            FindBadsCoolgirl(dumper, silent);
                            break;
                        case "read-crc-coolgirl":
                            ReadCrcCoolgirl(dumper);
                            break;
                        case "dump-tiles":
                            DumpTiles(dumper, filename ?? "output.png", mapper, parseSize(csize));
                            break;
                        case "write-flash":
                            WriteFlash(dumper, filename ?? "game.nes");
                            break;
                        case "write-coolboy":
                            WriteCoolboy(dumper, filename ?? "game.nes");
                            break;
                        case "write-coolgirl":
                            WriteCoolgirl(dumper, filename ?? "game.nes", badSectors, silent, needCheck, writePBBs);
                            break;
                        case "write-eeprom":
                            WriteEeprom(dumper, filename ?? "game.nes");
                            break;
                        case "info-coolgirl":
                            GetCoolgirlInfoOnly(dumper);
                            break;
                        case "jtag":
                            WriteJtag(dumper, filename ?? "mapper.fmp");
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
            Console.WriteLine(" {0,-20}{1}", "list-mappers", "list built in mappers");
            Console.WriteLine(" {0,-20}{1}", "dump", "dump cartridge");
            Console.WriteLine(" {0,-20}{1}", "reset", "simulate reset (M2 goes low for a second)");
            Console.WriteLine(" {0,-20}{1}", "read-prg-ram", "read PRG RAM (battery backed save if exists)");
            Console.WriteLine(" {0,-20}{1}", "write-prg-ram", "write PRG RAM");
            //Console.WriteLine(" {0,-20}{1}", "write-flash", "write special flash cartridge");
            Console.WriteLine(" {0,-20}{1}", "write-coolboy", "write COOLBOY cartridge");
            Console.WriteLine(" {0,-20}{1}", "write-coolgirl", "write COOLGIRL cartridge");
            Console.WriteLine(" {0,-20}{1}", "console", "start interactive Lua console");
            Console.WriteLine(" {0,-20}{1}", "dump-tiles", "dump CHR data to PNG file");
            Console.WriteLine(" {0,-20}{1}", "test-prg-ram", "run PRG RAM test");
            Console.WriteLine(" {0,-20}{1}", "test-chr-ram", "run CHR RAM test");
            Console.WriteLine(" {0,-20}{1}", "test-battery", "test battery-backed PRG RAM");
            Console.WriteLine(" {0,-20}{1}", "test-prg-ram-coolgirl", "run PRG RAM test for COOLGIRL cartridge");
            Console.WriteLine(" {0,-20}{1}", "test-chr-ram-coolgirl", "run CHR RAM test for COOLGIRL cartridge");
            Console.WriteLine(" {0,-20}{1}", "test-coolgirl", "run all RAM tests for COOLGIRL cartridge");
            Console.WriteLine(" {0,-20}{1}", "test-bads-coolgirl", "find bad sectors on COOLGIRL cartridge");
            Console.WriteLine(" {0,-20}{1}", "info-coolgirl", "show information abou COOLGIRL's flash memory");


            Console.WriteLine(" {0,-20}{1}", "dump-tiles", "dump CHR data to PNG file");
            Console.WriteLine();
            Console.WriteLine("Available options:");
            Console.WriteLine(" {0,-20}{1}", "--port <com>", "serial port of dumper or serial number of FTDI device, default - auto");
            Console.WriteLine(" {0,-20}{1}", "--mapper <mapper>", "number, name or path to LUA script of mapper for dumping, default is 0 (NROM)");
            Console.WriteLine(" {0,-20}{1}", "--file <output.nes>", "output filename (.nes, .png or .sav)");
            Console.WriteLine(" {0,-20}{1}", "--psize <size>", "size of PRG memory to dump, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-20}{1}", "--csize <size>", "size of CHR memory to dump, you can use \"K\" or \"M\" suffixes");
            Console.WriteLine(" {0,-20}{1}", "--luafile \"<lua_code>\"", "execute Lua code from file first");
            Console.WriteLine(" {0,-20}{1}", "--lua \"<lua_code>\"", "execute this Lua code first");
            Console.WriteLine(" {0,-20}{1}", "--unifname <name>", "internal ROM name for UNIF dumps");
            Console.WriteLine(" {0,-20}{1}", "--unifauthor <name>", "author of dump for UNIF dumps");
            Console.WriteLine(" {0,-20}{1}", "--reset", "do reset first");
            //Console.WriteLine(" {0,-20}{1}", "--silent", "silent mode (without sounds)");
            Console.WriteLine(" {0,-20}{1}", "--badsectors", "comma separated list of bad sectors for COOLGIRL flashing");
            Console.WriteLine(" {0,-20}{1}", "--sound", "play sounds");
        }

        static void Reset(FamicomDumperConnection dumper)
        {
            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
        }

        static void ListMappers()
        {
            Console.WriteLine("Supported mappers:");
            Console.WriteLine(" {0,-10}{1}", "Number", "Name");
            Console.WriteLine("----------------------");
            foreach (var mapper in MappersContainer.Mappers)
            {
                Console.WriteLine(" {0,-10}{1}", mapper.Number, mapper.Name);
            }
        }

        static IMapper GetMapper(string mapperName)
        {
            if (File.Exists(mapperName)) // LUA script?
            {
                var luaMapper = new LuaMapper();
                luaMapper.Execute(null, mapperName, true);
                return luaMapper;
            }
            var mapper = MappersContainer.GetMapper(mapperName ?? "0");
            if (mapper == null) throw new Exception("can't find mapper");
            return mapper;
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

        static void TestPrgRamCoolgirl(FamicomDumperConnection dumper, int count = -1)
        {
            TestPrgRam(dumper, "coolgirl", 1);
            dumper.Reset();
            dumper.WriteCpu(0x5007, 0x01); // enable SRAM
            var rnd = new Random();
            while (count != 0)
            {
                var data = new byte[][] { new byte[0x2000], new byte[0x2000], new byte[0x2000], new byte[0x2000] };
                for (byte bank = 0; bank < 4; bank++)
                {
                    Console.WriteLine("Writing SRAM, bank #{0}... ", bank);
                    rnd.NextBytes(data[bank]);
                    dumper.WriteCpu(0x5005, bank);
                    dumper.WriteCpu(0x6000, data[bank]);
                }
                for (byte bank = 0; bank < 4; bank++)
                {
                    Console.Write("Reading SRAM, bank #{0}... ", bank);
                    dumper.WriteCpu(0x5005, bank);
                    var rdata = dumper.ReadCpu(0x6000, 0x2000);
                    bool ok = true;
                    for (int b = 0; b < 0x2000; b++)
                    {
                        if (data[bank][b] != rdata[b])
                        {
                            Console.WriteLine("Mismatch at {0:X4}: {1:X2} != {2:X2}", b, rdata[b], data[bank][b]);
                            ok = false;
                        }
                    }
                    if (!ok)
                    {
                        File.WriteAllBytes("sramgood.bin", data[bank]);
                        Console.WriteLine("sramgood.bin writed");
                        File.WriteAllBytes("srambad.bin", rdata);
                        Console.WriteLine("srambad.bin writed");
                        throw new Exception("Test failed");
                    }
                    Console.WriteLine("OK!");
                }
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

        static void TestChrRamCoolgirl(FamicomDumperConnection dumper, int count = -1)
        {
            dumper.Reset();
            dumper.WriteCpu(0x5007, 0x2); // enable CHR writing
            var rnd = new Random();
            var data = new byte[0x2000];
            rnd.NextBytes(data);
            Console.WriteLine("Basic test.");
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

            Console.WriteLine("Global test.");
            data = new byte[256 * 1024];
            while (count != 0)
            {
                dumper.Reset();
                dumper.WriteCpu(0x5007, 0x2); // enable CHR writing
                rnd.NextBytes(data);
                for (byte bank = 0; bank < 32; bank++)
                {
                    Console.WriteLine("Writing CHR RAM bank #{0}...", bank);
                    dumper.WriteCpu(0x5003, bank); // select bank
                    var d = new byte[0x2000];
                    Array.Copy(data, bank * 0x2000, d, 0, 0x2000);
                    dumper.WritePpu(0x0000, d);
                }
                for (byte bank = 0; bank < 32; bank++)
                {
                    Console.Write("Reading CHR RAM bank #{0}... ", bank);
                    dumper.WriteCpu(0x5003, bank); // select bank
                    rdata = dumper.ReadPpu(0x0000, 0x2000);
                    ok = true;
                    for (int b = 0; b < 0x2000; b++)
                    {
                        if (data[b + bank * 0x2000] != rdata[b])
                        {
                            Console.WriteLine("Mismatch at {0:X4}: {1:X2} != {2:X2}", b, rdata[b], data[b + bank * 0x2000]);
                            ok = false;
                        }
                    }
                    if (!ok)
                    {
                        File.WriteAllBytes("chrgoodf.bin", data);
                        Console.WriteLine("chrgoodf.bin writed");
                        File.WriteAllBytes("chrbad.bin", rdata);
                        Console.WriteLine("chrbad.bin writed");
                        throw new Exception("Test failed");
                    }
                    Console.WriteLine("OK!");
                }
                count--;
            }
        }

        static void TestCoolgirlFull(FamicomDumperConnection dumper, int count = -1)
        {
            while (count != 0)
            {
                TestChrRamCoolgirl(dumper, 1);
                TestPrgRamCoolgirl(dumper, 1);
                if (count > 0) count--;
            }
        }

        enum WriteFlashRotMode { None, LastBanksOnly, Full }
        static void WriteFlash(FamicomDumperConnection dumper, string fileName, WriteFlashRotMode rotMode = WriteFlashRotMode.None)
        {
            var nesFile = new NesFile(fileName);
            int prgBanks = nesFile.PRG.Length / 0x8000;
            if (prgBanks * 0x8000 < nesFile.PRG.Length) prgBanks++;
            int chrBanks = nesFile.CHR.Length / 0x2000;
            if (chrBanks * 0x2000 < nesFile.CHR.Length) chrBanks++;

            Console.Write("Erasing PRG FLASH... ");
            dumper.Timeout = 3000000;
            dumper.ErasePrgFlash();
            Console.WriteLine("Done!");
            Console.Write("Writing PRG FLASH... ");
            dumper.Timeout = 10000;
            int writed = 0;
            for (byte bank = 0; bank < 16; bank++)
            {
                int pos;
                if (bank < prgBanks)
                {
                    pos = bank * 0x8000;
                }
                else if (rotMode == WriteFlashRotMode.LastBanksOnly)
                {
                    if (bank >= prgBanks && bank < 15) continue;
                    pos = (prgBanks - 1) * 0x8000; // last bank
                }
                else if (rotMode == WriteFlashRotMode.Full)
                {
                    pos = (bank % prgBanks) * 0x8000;
                }
                else continue;
                var data = new byte[0x8000];
                Array.Copy(nesFile.PRG, pos, data, 0, Math.Min(0x8000, nesFile.PRG.Length - pos));
                if (bank == prgBanks - 1 && nesFile.PRG.Length - pos < 0x8000)
                    Array.Copy(nesFile.PRG, pos, data, 0x8000 - (nesFile.PRG.Length - pos), nesFile.PRG.Length - pos);
                dumper.WriteCpu(0x0000, (byte)(bank));
                dumper.WritePrgFlash(0x0000, data);
                writed++;
                switch (rotMode)
                {
                    case WriteFlashRotMode.None:
                        Console.Write("{0}% ", 100 * writed / prgBanks);
                        break;
                    case WriteFlashRotMode.LastBanksOnly:
                        Console.Write("{0}% ", 100 * writed / (prgBanks + 1));
                        break;
                    case WriteFlashRotMode.Full:
                        Console.Write("{0}% ", 100 * writed / 16);
                        break;
                }
            }

            Console.WriteLine("Done! {0} banks writed.", writed);

            Console.Write("Erasing CHR FLASH... ");
            dumper.EraseChrFlash();
            Console.WriteLine("Done!");
            Console.Write("Writing CHR FLASH... ");
            for (byte bank = 0; bank < chrBanks; bank++)
            {
                int pos = bank * 0x2000;
                var data = new byte[0x2000];
                Array.Copy(nesFile.CHR, pos, data, 0, Math.Min(0x2000, nesFile.CHR.Length - pos));
                dumper.WriteCpu(0x4000, bank);
                dumper.WriteChrFlash(0x0000, data);
                if (chrBanks > 1)
                    Console.Write("{0}% ", 100 * (bank + 1) / chrBanks);
            }

            Console.WriteLine("Done! {0} banks writed.", chrBanks);
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

        static void WriteCoolboy(FamicomDumperConnection dumper, string fileName)
        {
            byte[] PRG;
            try
            {
                var nesFile = new NesFile(fileName);
                PRG = nesFile.PRG;
            }
            catch
            {
                var nesFile = new UnifFile(fileName);
                PRG = nesFile.Fields["PRG0"];
            }
            while (PRG.Length < 512 * 1024)
            {
                var PRGbig = new byte[PRG.Length * 2];
                Array.Copy(PRG, 0, PRGbig, 0, PRG.Length);
                Array.Copy(PRG, 0, PRGbig, PRG.Length, PRG.Length);
                PRG = PRGbig;
            }

            int prgBanks = PRG.Length / 0x2000;

            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
            dumper.WriteCpu(0xA001, 0x00); // RAM protect
            DateTime lastSectorTime = DateTime.Now;
            TimeSpan timeTotal = new TimeSpan();
            for (int bank = 0; bank < prgBanks; bank += 2)
            {
                int outbank = bank / 16;
                byte r0 = (byte)((outbank & 0x07) | ((outbank & 0xc0) >> 2));
                byte r1 = (byte)(((outbank & 0x30) >> 2) | ((outbank << 1) & 0x10));
                byte r2 = 0;
                byte r3 = 0;
                dumper.WriteCpu(0x6000, new byte[] { r0 });
                dumper.WriteCpu(0x6001, new byte[] { r1 });
                dumper.WriteCpu(0x6002, new byte[] { r2 });
                dumper.WriteCpu(0x6003, new byte[] { r3 });

                int inbank = bank % 64;
                dumper.WriteCpu(0x8000, new byte[] { 6, (byte)(inbank) });
                dumper.WriteCpu(0x8000, new byte[] { 7, (byte)(inbank | 1) });

                var data = new byte[0x4000];
                int pos = bank * 0x2000;
                if (pos % (128 * 1024) == 0)
                {
                    timeTotal = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (prgBanks - bank) / 16);
                    timeTotal = timeTotal.Add(DateTime.Now - startTime);
                    lastSectorTime = DateTime.Now;
                    Console.Write("Erasing sector... ");
                    dumper.ErasePrgFlash(FamicomDumperConnection.FlashType.Coolboy);
                    Console.WriteLine("OK");
                }
                Array.Copy(PRG, pos, data, 0, data.Length);
                var timePassed = DateTime.Now - startTime;
                Console.Write("Writing {0}/{1} ({2}%, {3:D2}:{4:D2}:{5:D2}/{6:D2}:{7:D2}:{8:D2})... ", bank / 2 + 1, prgBanks / 2, (int)(100 * bank / prgBanks),
                    timePassed.Hours, timePassed.Minutes, timePassed.Seconds, timeTotal.Hours, timeTotal.Minutes, timeTotal.Seconds);
                dumper.WritePrgFlash(0x0000, data, FamicomDumperConnection.FlashType.Coolboy, true);
                Console.WriteLine("OK");
            }
        }

        static int GetCoolgirlSize(FamicomDumperConnection dumper)
        {
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0x90);
            var autoselect = dumper.ReadCpu(0x8000, 0x100);
            byte manufacturer = autoselect[0];
            var device = new byte[] { autoselect[2], autoselect[0x1C], autoselect[0x1E] };
            dumper.WriteCpu(0x8000, 0xF0); // Reset            
            Console.WriteLine("Chip manufacturer ID: {0:X2}", manufacturer);
            Console.WriteLine("Chip device ID: {0:X2} {1:X2} {2:X2}", device[0], device[1], device[2]);
            string deviceName;
            int size;
            switch ((UInt32)((device[0] << 16) | (device[1] << 8) | (device[2])))
            {
                case 0x7E2801:
                    deviceName = "S29GL01GP";
                    size = 128 * 1024 * 1024;
                    break;
                case 0x7E2301:
                    deviceName = "S29GL512GP";
                    size = 64 * 1024 * 1024;
                    break;
                case 0x7E2201:
                    deviceName = "S29GL256GP";
                    size = 32 * 1024 * 1024;
                    break;
                case 0x7E2101:
                    deviceName = "S29GL128GP";
                    size = 16 * 1024 * 1024;
                    break;
                default:
                    throw new Exception("Unknown device ID");
            }
            Console.WriteLine("Device name: {0}", deviceName);
            Console.WriteLine("Device size: {0} MBytes / {1} Mbit", size / 1024 / 1024, size / 1024 / 1024 * 8);
            return size;
        }

        static void GetCoolgirlInfoOnly(FamicomDumperConnection dumper)
        {
            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 8K
            GetCoolgirlSize(dumper);
        }

        static byte PPBReadCoolgirl(FamicomDumperConnection dumper, uint sector)
        {
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 8K
            // Select sector
            byte r0 = (byte)((sector * 4) >> 7);
            byte r1 = (byte)((sector * 4) << 1);
            dumper.WriteCpu(0x5000, r0);
            dumper.WriteCpu(0x5001, r1);
            // PPB Command Set Entry
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0xC0);
            // PPB Status Read
            var result = dumper.ReadCpu(0x8000, 1)[0];
            // PPB Command Set Exit
            dumper.WriteCpu(0x8000, 0x90);
            dumper.WriteCpu(0x8000, 0x00);
            return result;
        }

        static void PPBWriteCoolgirl(FamicomDumperConnection dumper, uint sector)
        {
            Console.Write($"Writing PPB for sector #{sector}... ");
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 8K
            // Select sector
            byte r0 = (byte)((sector * 4) >> 7);
            byte r1 = (byte)((sector * 4) << 1);
            dumper.WriteCpu(0x5000, r0);
            dumper.WriteCpu(0x5001, r1);
            // PPB Command Set Entry
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0xC0);
            // PPB Program
            dumper.WriteCpu(0x8000, 0xA0);
            dumper.WriteCpu(0x8000, 0x00);
            // Sector 0
            dumper.WriteCpu(0x5000, 0);
            dumper.WriteCpu(0x5001, 0);
            // Check
            while (true)
            {
                var b0 = dumper.ReadCpu(0x8000, 1)[0];
                //dumper.ReadCpu(0x0000, 1);
                var b1 = dumper.ReadCpu(0x8000, 1)[0];
                var tg = b0 ^ b1;
                if ((tg & (1 << 6)) == 0) // DQ6 = not toggle
                {
                    Thread.Sleep(1);
                    break;
                }
                else// DQ6 = toggle
                {
                    if ((b0 & (1 << 5)) != 0) // DQ5 = 1
                    {
                        b0 = dumper.ReadCpu(0x8000, 1)[0];
                        //dumper.ReadCpu(0x0000, 1);
                        b1 = dumper.ReadCpu(0x8000, 1)[0];
                        tg = b0 ^ b1;
                        if ((tg & (1 << 6)) == 0) // DQ6 = not toggle
                            break;
                        else
                            throw new Exception("PPB write failed");
                    }
                }
            }
            var r = dumper.ReadCpu(0x8000, 1)[0];
            if ((r & 1) != 0) // DQ0 = 1
                throw new Exception("PPB write failed");
            // PPB Command Set Exit
            dumper.WriteCpu(0x8000, 0x90);
            dumper.WriteCpu(0x8000, 0x00);
            Console.WriteLine("OK");
        }

        static void PPBEraseCoolgirl(FamicomDumperConnection dumper)
        {
            Console.Write($"Erasing all PBBs... ");
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 8K
            // Sector 0
            dumper.WriteCpu(0x5000, 0);
            dumper.WriteCpu(0x5001, 0);
            // PPB Command Set Entry
            dumper.WriteCpu(0x8AAA, 0xAA);
            dumper.WriteCpu(0x8555, 0x55);
            dumper.WriteCpu(0x8AAA, 0xC0);
            // All PPB Erase
            dumper.WriteCpu(0x8000, 0x80);
            dumper.WriteCpu(0x8000, 0x30);
            // Check
            while (true)
            {
                var b0 = dumper.ReadCpu(0x8000, 1)[0];
                //dumper.ReadCpu(0x0000, 1);
                var b1 = dumper.ReadCpu(0x8000, 1)[0];
                var tg = b0 ^ b1;
                if ((tg & (1 << 6)) == 0) // DQ6 = not toggle
                {
                    Thread.Sleep(1);
                    break;
                }
                else// DQ6 = toggle
                {
                    if ((b0 & (1 << 5)) != 0) // DQ5 = 1
                    {
                        b0 = dumper.ReadCpu(0x8000, 1)[0];
                        //dumper.ReadCpu(0x0000, 1);
                        b1 = dumper.ReadCpu(0x8000, 1)[0];
                        tg = b0 ^ b1;
                        if ((tg & (1 << 6)) == 0) // DQ6 = not toggle
                            break;
                        else
                            throw new Exception("PPB erase failed");
                    }
                }
            }
            var r = dumper.ReadCpu(0x8000, 1)[0];
            if ((r & 1) != 1) // DQ0 = 0
                throw new Exception("PPB erase failed");
            // PPB Command Set Exit
            dumper.WriteCpu(0x8000, 0x90);
            dumper.WriteCpu(0x8000, 0x00);
            Console.WriteLine("OK");
        }

        static void WriteCoolgirl(FamicomDumperConnection dumper, string fileName, IEnumerable<int> badSectors, bool silent, bool needCheck = false, bool writePBBs = false)
        {
            byte[] PRG;
            if (Path.GetExtension(fileName).ToLower() == ".bin")
            {
                PRG = File.ReadAllBytes(fileName);
            }
            else
            {
                try
                {
                    var nesFile = new NesFile(fileName);
                    PRG = nesFile.PRG;
                }
                catch
                {
                    var nesFile = new UnifFile(fileName);
                    PRG = nesFile.Fields["PRG0"];
                }
            }

            int prgBanks = PRG.Length / 0x8000;

            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 8K
            int flashSize = GetCoolgirlSize(dumper);
            if (PRG.Length > flashSize)
                throw new Exception("This ROM is too big for this cartridge");
            PPBEraseCoolgirl(dumper);

            DateTime lastSectorTime = DateTime.Now;
            TimeSpan timeTotal = new TimeSpan();
            int errorCount = 0;
            for (int bank = 0; bank < prgBanks; bank++)
            {
                if (badSectors.Contains(bank / 4)) bank += 4; // bad sector :(
                try
                {
                    byte r0 = (byte)(bank >> 7);
                    byte r1 = (byte)(bank << 1);
                    dumper.WriteCpu(0x5000, r0);
                    dumper.WriteCpu(0x5001, r1);

                    var data = new byte[0x8000];
                    int pos = bank * 0x8000;
                    if (pos % (128 * 1024) == 0)
                    {
                        timeTotal = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (prgBanks - bank) / 4);
                        timeTotal = timeTotal.Add(DateTime.Now - startTime);
                        lastSectorTime = DateTime.Now;
                        Console.Write("Erasing sector... ");
                        dumper.ErasePrgFlash(FamicomDumperConnection.FlashType.Coolgirl);
                        Console.WriteLine("OK");
                    }
                    Array.Copy(PRG, pos, data, 0, data.Length);
                    var timePassed = DateTime.Now - startTime;
                    //var timeTotal = new TimeSpan((DateTime.Now - startTime).Ticks * prgBanks / (bank + 1));
                    Console.Write("Writing {0}/{1} ({2}%, {3:D2}:{4:D2}:{5:D2}/{6:D2}:{7:D2}:{8:D2})... ", bank + 1, prgBanks, (int)(100 * bank / prgBanks),
                        timePassed.Hours, timePassed.Minutes, timePassed.Seconds, timeTotal.Hours, timeTotal.Minutes, timeTotal.Seconds);
                    dumper.WritePrgFlash(0x0000, data, FamicomDumperConnection.FlashType.Coolgirl, true);
                    Console.WriteLine("OK");
                    if ((bank % 4 == 3) || (bank == prgBanks - 1))
                        PPBWriteCoolgirl(dumper, (uint)bank / 4);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    if (errorCount >= 3)
                        throw ex;
                    if (!silent) errorSound.PlaySync();
                    Console.WriteLine("Error: " + ex.Message);
                    bank = (bank & ~3) - 1;
                    Console.WriteLine("Lets try again");
                    Console.Write("Reset... ");
                    dumper.Reset();
                    Console.WriteLine("OK");
                    dumper.WriteCpu(0x5007, 0x04); // enable PRG write
                    dumper.WriteCpu(0x5002, 0xFE); // mask = 8K
                    continue;
                }
            }
            if (errorCount > 0)
                Console.WriteLine("Warning! Error count: {0}", errorCount);

            if (needCheck)
            {
                Console.WriteLine("Starting check process");
                Console.Write("Reset... ");
                dumper.Reset();
                Console.WriteLine("OK");

                var readStartTime = DateTime.Now;
                lastSectorTime = DateTime.Now;
                timeTotal = new TimeSpan();
                dumper.WriteCpu(0x5002, 0xFE); // mask = 8K
                for (int bank = 0; bank < prgBanks; bank++)
                {
                    if (badSectors.Contains(bank / 4)) bank += 4; // bad sector :(
                    byte r0 = (byte)(bank >> 7);
                    byte r1 = (byte)(bank << 1);
                    dumper.WriteCpu(0x5000, r0);
                    dumper.WriteCpu(0x5001, r1);

                    var data = new byte[0x8000];
                    int pos = bank * 0x8000;
                    if (pos % (128 * 1024) == 0)
                    {
                        timeTotal = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (prgBanks - bank) / 4);
                        timeTotal = timeTotal.Add(DateTime.Now - readStartTime);
                        lastSectorTime = DateTime.Now;
                    }
                    Array.Copy(PRG, pos, data, 0, data.Length);
                    UInt16 crc = 0;
                    foreach (var a in data)
                    {
                        crc ^= a;
                        for (int i = 0; i < 8; ++i)
                        {
                            if ((crc & 1) != 0)
                                crc = (UInt16)((crc >> 1) ^ 0xA001);
                            else
                                crc = (UInt16)(crc >> 1);
                        }
                    }
                    var timePassed = DateTime.Now - readStartTime;
                    Console.Write("Reading CRC {0}/{1} ({2}%, {3:D2}:{4:D2}:{5:D2}/{6:D2}:{7:D2}:{8:D2})... ", bank + 1, prgBanks, (int)(100 * bank / prgBanks),
                        timePassed.Hours, timePassed.Minutes, timePassed.Seconds, timeTotal.Hours, timeTotal.Minutes, timeTotal.Seconds);
                    //var datar = dumper.ReadCpu(0x8000, 0x8000);
                    var crcr = dumper.ReadCpuCrc(0x8000, 0x8000);
                    if (crcr != crc)
                        throw new Exception(string.Format("Check failed: {0:X4} != {1:X4}", crcr, crc));
                    else
                        Console.WriteLine("OK (CRC = {0:X4})", crcr);
                }
                if (errorCount > 0)
                {
                    Console.WriteLine("Warning! Error count: {0}", errorCount);
                    return;
                }
            }
        }

        static void FindBadsCoolgirl(FamicomDumperConnection dumper, bool silent)
        {
            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 8K
            PPBEraseCoolgirl(dumper);
            dumper.WriteCpu(0x5000, 0);
            dumper.WriteCpu(0x5001, 0);
            var flashSize = GetCoolgirlSize(dumper);
            int prgBanks = flashSize / 0x8000;

            Console.Write("Erasing sector #0... ");
            dumper.ErasePrgFlash(FamicomDumperConnection.FlashType.Coolgirl);
            Console.WriteLine("OK");
            var data = new byte[0x8000];
            new Random().NextBytes(data);
            Console.Write("Writing sector #0 for test... ");
            dumper.WritePrgFlash(0x0000, data, FamicomDumperConnection.FlashType.Coolgirl, true);
            Console.WriteLine("OK");
            Console.Write("Reading sector #0 for test... ");
            var datar = dumper.ReadCpu(0x8000, 0x8000);
            for (int i = 0; i < data.Length; i++)
                if (data[i] != datar[i])
                {
                    throw new Exception("Check failed");
                }
            Console.WriteLine("OK");

            DateTime lastSectorTime = DateTime.Now;
            TimeSpan timeTotal = new TimeSpan();
            var badSectors = new List<int>();

            for (int bank = 0; bank < prgBanks; bank += 4)
            {
                byte r0 = (byte)(bank >> 7);
                byte r1 = (byte)(bank << 1);
                dumper.WriteCpu(0x5000, r0);
                dumper.WriteCpu(0x5001, r1);

                timeTotal = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (prgBanks - bank) / 4);
                timeTotal = timeTotal.Add(DateTime.Now - startTime);
                lastSectorTime = DateTime.Now;
                var timePassed = DateTime.Now - startTime;
                Console.Write("Erasing sector {0}/{1} ({2}%, {3:D2}:{4:D2}:{5:D2}/{6:D2}:{7:D2}:{8:D2})... ", bank / 4 + 1, prgBanks / 4, (int)(100 * bank / prgBanks),
                    timePassed.Hours, timePassed.Minutes, timePassed.Seconds, timeTotal.Hours, timeTotal.Minutes, timeTotal.Seconds);
                try
                {
                    dumper.ErasePrgFlash(FamicomDumperConnection.FlashType.Coolgirl);
                    Console.WriteLine("OK");
                }
                catch
                {
                    Console.WriteLine("ERROR!");
                    if (!silent) errorSound.PlaySync();
                    Console.Write("Trying again... ");
                    dumper.Reset();
                    dumper.WriteCpu(0x5007, 0x04); // enable PRG write
                    dumper.WriteCpu(0x5002, 0xFE); // mask = 8K
                    dumper.WriteCpu(0x5000, r0);
                    dumper.WriteCpu(0x5001, r1);
                    try
                    {
                        dumper.ErasePrgFlash(FamicomDumperConnection.FlashType.Coolgirl);
                        Console.WriteLine("OK");
                    }
                    catch
                    {
                        Console.WriteLine("ERROR! Sector #{0} is bad.", bank / 4);
                        if (!silent) errorSound.PlaySync();
                        badSectors.Add(bank / 4);
                    }
                }
            }
            if (badSectors.Count > 0)
            {
                foreach (var bad in badSectors)
                    Console.WriteLine("Bad sector: {0}", bad);
                throw new Exception("Bad sectors found");
            }
            else Console.WriteLine("There is no bad sectors");
        }

        static void ReadCrcCoolgirl(FamicomDumperConnection dumper)
        {
            Console.Write("Reset... ");
            dumper.Reset();
            Console.WriteLine("OK");
            dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            dumper.WriteCpu(0x5002, 0xFE); // mask = 8K
            var flashSize = GetCoolgirlSize(dumper);
            int prgBanks = flashSize / 0x8000;

            var readStartTime = DateTime.Now;
            var lastSectorTime = DateTime.Now;
            var timeTotal = new TimeSpan();
            UInt16 crc = 0;
            for (int bank = 0; bank < /*16*/prgBanks; bank++)
            {
                byte r0 = (byte)(bank >> 7);
                byte r1 = (byte)(bank << 1);
                dumper.WriteCpu(0x5000, r0);
                dumper.WriteCpu(0x5001, r1);

                var data = new byte[0x8000];
                int pos = bank * 0x8000;
                if (pos % (128 * 1024) == 0)
                {
                    timeTotal = new TimeSpan((DateTime.Now - lastSectorTime).Ticks * (prgBanks - bank) / 4);
                    timeTotal = timeTotal.Add(DateTime.Now - readStartTime);
                    lastSectorTime = DateTime.Now;
                }
                var timePassed = DateTime.Now - readStartTime;
                Console.Write("Reading CRC {0}/{1} ({2}%, {3:D2}:{4:D2}:{5:D2}/{6:D2}:{7:D2}:{8:D2})... ", bank + 1, prgBanks, (int)(100 * bank / prgBanks),
                    timePassed.Hours, timePassed.Minutes, timePassed.Seconds, timeTotal.Hours, timeTotal.Minutes, timeTotal.Seconds);
                //var datar = dumper.ReadCpu(0x8000, 0x8000);
                var crcr = dumper.ReadCpuCrc(0x8000, 0x8000);
                Console.WriteLine("CRC = {0:X4}", crcr);
                crc ^= crcr;
            }
            Console.WriteLine("Total CRC = {0:X4}", crc);
        }

        static void WriteJtag(FamicomDumperConnection dumper, string fileName)
        {
            Console.Write("JTAG setup... ");
            dumper.JtagSetup();
            Console.WriteLine("OK");
            Console.Write("JTAG programming... ");
            dumper.WriteJtag(File.ReadAllBytes(fileName));
            Console.WriteLine("Done!");
            Console.Write("JTAG shutdown... ");
            dumper.JtagShutdown();
            Console.WriteLine("OK");
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
