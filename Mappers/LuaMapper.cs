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

        public LuaMapper(string fileName)
        {
            script = new Script();
            script.DoFile(fileName);
            script.Globals["ReadPrg"] = script.Globals["ReadCpu"] = (Func<UInt16, int, List<byte>>)readPrg;
            script.Globals["WritePrg"] = script.Globals["WriteCpu"] = (Action<UInt16, List<byte>>)writePrg;
            script.Globals["AddPrg"] = script.Globals["AddPrgResult"] = (Action<List<byte>>)addResultPrg;
            script.Globals["ReadAddPrg"] = script.Globals["ReadAddCpu"] = (Action<UInt16, int>)readAddPrg;
            script.Globals["ReadChr"] = script.Globals["ReadPpu"] = (Func<UInt16, int, List<byte>>)readChr;
            script.Globals["WriteChr"] = script.Globals["WritePpu"] = (Action<UInt16, List<byte>>)writeChr;
            script.Globals["ReadAddChr"] = script.Globals["ReadAddPpu"] = (Action<UInt16, int>)readAddChr;
            script.Globals["AddChr"] = script.Globals["AddChrResult"] = (Action<List<byte>>)addResultChr;
            script.Globals["Reset"] = (Action)reset;
            script.Globals["Error"] = (Action<string>)error;
        }

        public string Name
        {
            get { return (string) script.Globals["MapperName"]; }
        }

        public int Number
        {
            get
            {
                try
                {
                    return Convert.ToInt32(script.Globals["MapperNumber"]);
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

        private void writePrg(UInt16 address, List<byte> data)
        {
            dumper.WriteCpu(address, data.ToArray());
        }

        private List<byte> readPrg(UInt16 address, int length)
        {
            var result = new List<byte>();
            result.AddRange(dumper.ReadCpu(address, length));
            return result;
        }

        private void readAddPrg(UInt16 address, int length)
        {
            resultPrg.AddRange(dumper.ReadCpu(address, length));
        }

        private void writeChr(UInt16 address, List<byte> data)
        {
            dumper.WritePpu(address, data.ToArray());
        }

        private List<byte> readChr(UInt16 address, int length)
        {
            var result = new List<byte>();
            result.AddRange(dumper.ReadPpu(address, length));
            return result;
        }

        private void readAddChr(UInt16 address, int length)
        {
            resultChr.AddRange(dumper.ReadPpu(address, length));
        }

        private void addResultPrg(List<byte> r)
        {
            resultPrg.AddRange(r);
        }

        private void addResultChr(List<byte> r)
        {
            resultChr.AddRange(r);
        }

        private void reset()
        {
            dumper.Reset();
        }

        private void error(string e)
        {
            throw new Exception(e);
        }

        public static void Execute(FamicomDumperConnection dumper, string lua)
        {
            Script script = new Script();
            script.Globals["WritePrg"] = script.Globals["WriteCpu"] = (Action<UInt16, List<byte>>)delegate(UInt16 address, List<byte> data)
            {
                var a = address;
                foreach (var v in data)
                {
                    Console.WriteLine("CPU write ${0:X2} => ${1:X4}", v, a);
                    a++;
                }
                dumper.WriteCpu(address, data.ToArray());
            };
            script.Globals["WriteChr"] = script.Globals["WritePpu"] = (Action<UInt16, List<byte>>)delegate(UInt16 address, List<byte> data)
            {
                var a = address;
                foreach (var v in data)
                {
                    Console.WriteLine("PPU write ${0:X2} => ${1:X4}", v, a);
                    a++;
                }
                dumper.WritePpu(address, data.ToArray());
            };
            script.Globals["Reset"] = (Action)delegate
            {
                Console.Write("Reset... ");
                dumper.Reset();
                Console.WriteLine("OK");
            };
            Console.WriteLine("Executing LUA script...");
            script.DoString(lua);
        }
    }
}
