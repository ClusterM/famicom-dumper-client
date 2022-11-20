using System.Collections.Generic;
using System;

class UNROM512 : IMapper
{
    public string Name
    {
        get { return "UNROM-512"; }
    }

    public int Number
    {
        get { return 30; }
    }

    public byte Submapper
    {
        get { return 0; }
    }

    public string UnifName
    {
        get { return "UNROM-512"; }
    }

    public int DefaultPrgSize
    {
        get { return 512 * 1024; }
    }

    public int DefaultChrSize
    {
        get { return 0; }
    }

    public void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        var banks = (byte)(size / 0x4000);
        Console.Write("Reading last PRG bank... ");
        var lastBank = dumper.ReadCpu(0xC000, 0x4000);
        Console.WriteLine("OK");
        for (int bank = 0; bank < banks - 1; bank++)
        {
            Console.Write($"Reading PRG bank #{bank}/{banks}... ");
            // Avoiding bus conflicts
            var noBusConflict = false;
            for (var i = 0; i < lastBank.Length; i++)
            {
                if (lastBank[i] == bank)
                {
                    dumper.WriteCpu((ushort)(0xC000 + i), (byte)bank);
                    noBusConflict = true;
                    break;
                }
            }
            if (!noBusConflict) // Whatever...
                dumper.WriteCpu(0xC000, (byte)bank);
            data.AddRange(dumper.ReadCpu(0x8000, 0x4000));
            Console.WriteLine("OK");
        }
        data.AddRange(lastBank);
    }

    public void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size)
    {
        throw new NotSupportedException("This mapper doesn't have a CHR ROM");
    }

    public void EnablePrgRam(IFamicomDumperConnection dumper)
    {
        throw new NotSupportedException("PRG RAM is not supported by this mapper");
    }

    public MirroringType GetMirroring(IFamicomDumperConnection dumper)
    {
        return dumper.GetMirroring();
    }
}
