/* COOLGIRL cartridge tests script
 *
 * Copyright notice for this file:
 *  Copyright (C) 2023 Cluster
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

/*
 * Usage: famicom-dumper script --cs-file CoolgirlTests.cs --chr-size <size> - full|prg-ram|chr-ram [number of repetitions]
 */

class CoolgirlTests
{
    void Run(IFamicomDumperConnection dumper, string[] args, int chrSize = 256 * 1024)
    {
        string testMode = "";
        int count = -1;
        if (args.Length > 0)
            testMode = args[0];
        if (args.Length > 1)
            count = int.Parse(args[1]);
        switch (testMode)
        {
            default:
            case "full":
                FullTest(dumper, count, chrSize);
                return;
            case "prg":
            case "prgram":
            case "prg-ram":
                TestPrgRam(dumper, count);
                return;
            case "chr":
            case "chrram":
            case "chr-ram":
                TestChrRam(dumper, count, chrSize);
                return;
        }
        Console.WriteLine("Usage: famicom-dumper script --cs-file CoolgirlTests.cs --chr-size <size> - full|prg-ram|chr-ram [number of repetitions]");
    }

    public static void FullTest(IFamicomDumperConnection dumper, int count, int chrSize)
    {
        while (count != 0)
        {
            TestPrgRam(dumper, 1);
            TestChrRam(dumper, 1, chrSize);
            if (count > 0) count--;
        }
    }

    public static void TestPrgRam(IFamicomDumperConnection dumper, int count)
    {
        Console.Write("Reset... ");
        dumper.Reset();
        Console.WriteLine("OK");
        dumper.WriteCpu(0x5007, 0x01); // enable PRG RAM
        var rnd = new Random();
        int bankSize = 0x2000;
        int banks = 4;
        while (count != 0)
        {
            var data = new byte[banks][];
            for (int i = 0; i < banks; i++)
            {
                data[i] = new byte[0x2000];
                rnd.NextBytes(data[i]);
            }
            for (byte bank = 0; bank < banks; bank++)
            {
                Console.WriteLine($"Writing PRG RAM, bank #{bank}/{4}... ");
                rnd.NextBytes(data[bank]);
                dumper.WriteCpu(0x5005, bank);
                dumper.WriteCpu(0x6000, data[bank]);
            }
            for (byte bank = 0; bank < banks; bank++)
            {
                Console.Write($"Reading PRG RAM, bank #{bank}/{4}... ");
                dumper.WriteCpu(0x5005, bank);
                var rdata = dumper.ReadCpu(0x6000, bankSize);
                bool ok = true;
                for (int b = 0; b < rdata.Length; b++)
                {
                    if (data[bank][b] != rdata[b])
                    {
                        Console.WriteLine($"Mismatch at {b:X4}: {rdata[b]:X2} != {data[bank][b]:X2}");
                        ok = false;
                    }
                }
                if (!ok)
                {
                    DetectProblems(data[bank], rdata, "CPU");
                    File.WriteAllBytes("prgramgood.bin", data[bank]);
                    Console.WriteLine("prgramgood.bin writed");
                    File.WriteAllBytes("prgrambad.bin", rdata);
                    Console.WriteLine("prgrambad.bin writed");
                    throw new InvalidDataException("Test failed");
                }
                Console.WriteLine("OK");
            }
            count--;
        }
    }

    public static void TestChrRam(IFamicomDumperConnection dumper, int count, int chrSize)
    {
        Console.WriteLine($"Testing CHR RAM, size: {chrSize / 1024}KB");
        Console.Write("Reset... ");
        dumper.Reset();
        Console.WriteLine("OK");
        dumper.WriteCpu(0x5007, 0x2); // enable CHR writing
        var rnd = new Random();
        int bankSize = 0x2000;
        int banks = chrSize / bankSize;
        for (; count != 0; count--)
        {
            var data = new byte[banks][];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = new byte[0x2000];
                rnd.NextBytes(data[i]);
            }
            for (byte bank = 0; bank < banks; bank++)
            {
                Console.WriteLine($"Writing CHR RAM bank #{bank}/{banks}...");
                dumper.WriteCpu(0x5003, (byte)(bank & 0b00011111)); // select bank, low 5 bits
                dumper.WriteCpu(0x5005, (byte)((bank & 0b00100000) << 2)); // select bank, 6th bit
                dumper.WritePpu(0x0000, data[bank]);
            }
            for (byte bank = 0; bank < banks; bank++)
            {
                Console.Write($"Reading CHR RAM bank #{bank}/{banks}... ");
                dumper.WriteCpu(0x5003, (byte)(bank & 0b00011111)); // select bank, low 5 bits
                dumper.WriteCpu(0x5005, (byte)((bank & 0b00100000) << 2)); // select bank, 6th bit
                var rdata = dumper.ReadPpu(0x0000, bankSize);
                var ok = true;
                for (int b = 0; b < rdata.Length; b++)
                {
                    if (data[bank][b] != rdata[b])
                    {
                        Console.WriteLine($"Mismatch at {b:X4}: {rdata[b]:X2} != {data[bank][b]:X2}");
                        ok = false;
                    }
                }
                if (!ok)
                {
                    DetectProblems(data[bank], rdata, "PPU");
                    File.WriteAllBytes("chrramgood.bin", data[0]);
                    Console.WriteLine("chrramgood.bin writed");
                    File.WriteAllBytes("chrrambad.bin", rdata);
                    Console.WriteLine("chrrambad.bin writed");
                    throw new InvalidDataException("Test failed");
                }
                Console.WriteLine("OK");
            }
        }
    }

    // Function to detect problem lines
    private static void DetectProblems(byte[] good, byte[] bad, string busName)
    {
        var size = Math.Min(good.Length, bad.Length);
        int problemBits = 0;
        for (int i = 0; i < size; i++)
        {
            problemBits |= (byte)(good[i] ^ bad[i]);
        }
        if (problemBits != 0xFF)
        {
            for (int i = 0; i < 8; i++)
                if ((problemBits & (1 << i)) != 0)
                    Console.WriteLine($"Problems on line D{i} @ {busName}");
        }

        int memoryTestSize = 4;
        problemBits = 0;
        for (int i = memoryTestSize; i < bad.Length; i += memoryTestSize)
        {
            bool matched = true;
            for (int j = 0; j < memoryTestSize; j++)
            {
                if (bad[i + j] != bad[j])
                {
                    matched = false;
                    break;
                }
            }
            if (matched) problemBits |= i;
        }
        for (int i = 0; i < 16; i++)
            if ((problemBits & (1 << i)) != 0)
                Console.WriteLine($"Problems on line A{i} @ {busName}");
    }
}
