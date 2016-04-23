using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Cluster.Famicom.Mappers
{
    public class LuaMapper : IMapper
    {
        Script script;
        FamicomDumperConnection dumper;
        List<byte> resultPrg = new List<byte>();
        List<byte> resultChr = new List<byte>();
        public bool Verbose = false;

        private delegate void WriteNesDelegate(string filename, List<byte> prgData, List<byte> chrData, byte mapper, bool vertical);

        public LuaMapper()
        {
            script = new Script();
            script.Globals["ReadPrg"] = script.Globals["ReadCpu"] = (Func<UInt16, int, List<byte>>)delegate(UInt16 address, int length)
            {
                if (Verbose)
                    Console.WriteLine("Reading {0} bytes from CPU:${1:X4}", length, address);
                var result = new List<byte>();
                result.AddRange(dumper.ReadCpu(address, length));
                return result;
            };
            script.Globals["WritePrg"] = script.Globals["WriteCpu"] = (Action<UInt16, List<byte>>)delegate(UInt16 address, List<byte> data)
            {
                if (Verbose)
                {
                    var a = address;
                    foreach (var v in data)
                    {
                        Console.WriteLine("CPU write ${0:X2} => ${1:X4}", v, a);
                        a++;
                    }
                }
                dumper.WriteCpu(address, data.ToArray());
            };
            script.Globals["AddPrg"] = script.Globals["AddPrgResult"] = (Action<List<byte>>)delegate(List<byte> r)
            {
                resultPrg.AddRange(r);
            };
            script.Globals["ReadAddPrg"] = script.Globals["ReadAddCpu"] = (Action<UInt16, int>)delegate(UInt16 address, int length)
            {
                if (Verbose)
                    Console.WriteLine("Reading {0} bytes from CPU:${1:X4}", length, address);
                resultPrg.AddRange(dumper.ReadCpu(address, length));
            };
            script.Globals["ReadChr"] = script.Globals["ReadPpu"] = (Func<UInt16, int, List<byte>>)delegate(UInt16 address, int length)
            {
                if (Verbose)
                    Console.WriteLine("Reading {0} bytes from PPU:${1:X4}", length, address);
                var result = new List<byte>();
                result.AddRange(dumper.ReadPpu(address, length));
                return result;
            };
            script.Globals["WriteChr"] = script.Globals["WritePpu"] = (Action<UInt16, List<byte>>)delegate(UInt16 address, List<byte> data)
            {
                if (Verbose)
                {
                    var a = address;
                    foreach (var v in data)
                    {
                        Console.WriteLine("PPU write ${0:X2} => ${1:X4}", v, a);
                        a++;
                    }
                }
                dumper.WritePpu(address, data.ToArray());
            };
            script.Globals["ReadAddChr"] = script.Globals["ReadAddPpu"] = (Action<UInt16, int>)delegate(UInt16 address, int length)
            {
                if (Verbose)
                    Console.WriteLine("Reading {0} bytes from PPU:${1:$X4}", length, address);
                resultChr.AddRange(dumper.ReadPpu(address, length));
            };
            script.Globals["AddChr"] = script.Globals["AddChrResult"] = (Action<List<byte>>)delegate(List<byte> r)
            {
                resultChr.AddRange(r);
            };
            script.Globals["Reset"] = (Action)delegate
            {
                if (Verbose) Console.Write("Reset... ");
                dumper.Reset();
                if (Verbose) Console.WriteLine("OK");
            };
            script.Globals["WriteFile"] = (Action<string, List<byte>>)delegate(string filename, List<byte> data)
            {
                if (Verbose) Console.Write("Writing data to \"{0}\"... ", Path.GetFileName(filename));
                File.WriteAllBytes(filename, data.ToArray());
                if (Verbose) Console.WriteLine("OK");
            };
            script.Globals["WriteNes"] = (WriteNesDelegate)delegate(string filename, List<byte> prgData, List<byte> chrData, byte mapper, bool vertical)
            {
                if (Verbose) Console.Write("Writing data to NES file \"{0}\" (mapper={1}, mirroring={2})... ", Path.GetFileName(filename), mapper, vertical ? "vertical" : "horizontal");
                var nesFile = new NesFile();
                nesFile.PRG = prgData.ToArray();
                nesFile.CHR = chrData.ToArray();
                nesFile.Mapper = 0;
                nesFile.Mirroring = vertical ? NesFile.MirroringType.Vertical : NesFile.MirroringType.Horizontal;
                nesFile.Save(filename);
                if (Verbose) Console.WriteLine("OK");
            };
            script.Globals["Error"] = (Action<string>)delegate(string message)
            {
                throw new Exception(message);
            };
        }

        public void Execute(FamicomDumperConnection dumper, string scriptSource, bool useFile = true)
        {
            this.dumper = dumper;
            if (useFile)
                script.DoFile(scriptSource);
            else
                script.DoString(scriptSource);
        }

        public string Name
        {
            get { return (string)script.Globals["MapperName"]; }
        }

        public int Number
        {
            get
            {
                try
                {
                    return int.Parse(script.Globals["MapperNumber"].ToString());
                }
                catch
                {
                    return -1;
                }
            }
        }

        public string UnifName
        {
            get
            {
                try
                {
                    return (string)script.Globals["MapperUnifName"];
                }
                catch
                {
                    return null;
                }
            }
        }

        public int DefaultPrgSize
        {
            get { return Convert.ToInt32(script.Globals["DefaultPrgSize"]); }
        }

        public int DefaultChrSize
        {
            get { return Convert.ToInt32(script.Globals["DefaultChrSize"]); }
        }

        public void DumpPrg(FamicomDumperConnection dumper, List<byte> data, int size = 0)
        {
            this.dumper = dumper;
            resultPrg.Clear();
            script.Call(script.Globals["DumpPrg"], size);
            data.AddRange(resultPrg);
        }

        public void DumpChr(FamicomDumperConnection dumper, List<byte> data, int size = 0)
        {
            this.dumper = dumper;
            resultChr.Clear();
            script.Call(script.Globals["DumpChr"], size);
            data.AddRange(resultChr);
        }

        public void EnablePrgRam(FamicomDumperConnection dumper)
        {
            this.dumper = dumper;
            script.Call(script.Globals["EnablePrgRam"]);
        }

        private void reset()
        {
            dumper.Reset();
        }
    }
}
