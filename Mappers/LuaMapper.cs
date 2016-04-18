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
            script.Globals["AddPrg"] = script.Globals["AddResult"] = (Action<List<byte>>)addResultPrg;
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
            get { return script.Call(script.Globals["MapperName"]).CastToString(); }
        }

        public int Number
        {
            get
            {
                try
                {
                    return (int)script.Call(script.Globals["MapperNumber"]).CastToNumber();
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
                    return script.Call(script.Globals["MapperUnifName"]).CastToString();
                }
                catch
                {
                    return null;
                }
            }
        }

        public int DefaultPrgSize
        {
            get { return (int)script.Call(script.Globals["DefaultPrgSize"]).CastToNumber(); }
        }

        public int DefaultChrSize
        {
            get { return (int)script.Call(script.Globals["DefaultChrSize"]).CastToNumber(); }
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
    }
}
