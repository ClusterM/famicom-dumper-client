/* COOLGIRL cartridge tests script
 *
 * Copyright notice for this file:
 *  Copyright (C) 2021 Cluster
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
 * Usage: famicom-dumper script --cs-script CoolgirlTests.cs --chr-size <size> - full|prg-ram|chr-ram [number of repetitions]
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
            case "":
                Console.WriteLine("Please specify one of the test modes: full, prg-ram or chr-ram");
                break;
            default:
                break;
        }
        Console.WriteLine("Usage: famicom-dumper script --cs-script CoolgirlTests.cs --chr-size <size> - full|prg-ram|chr-ram [number of repetitions]");
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
        while (count != 0)
        {
            var data = new byte[][] { new byte[0x2000], new byte[0x2000], new byte[0x2000], new byte[0x2000] };
            for (byte bank = 0; bank < 4; bank++)
            {
                Console.WriteLine($"Writing PRG RAM, bank #{bank}/{4}... ");
                rnd.NextBytes(data[bank]);
                dumper.WriteCpu(0x5005, bank);
                dumper.WriteCpu(0x6000, data[bank]);
            }
            for (byte bank = 0; bank < 4; bank++)
            {
                Console.Write($"Reading PRG RAM, bank #{bank}/{4}... ");
                dumper.WriteCpu(0x5005, bank);
                var rdata = dumper.ReadCpu(0x6000, 0x2000);
                bool ok = true;
                for (int b = 0; b < 0x2000; b++)
                {
                    if (data[bank][b] != rdata[b])
                    {
                        Console.WriteLine($"Mismatch at {b:X4}: {rdata[b]:X2} != {data[bank][b]:X2}");
                        ok = false;
                    }
                }
                if (!ok)
                {
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
        var data = new byte[0x2000];
        rnd.NextBytes(data);
        Console.WriteLine("Single bank test.");
        Console.Write("Writing CHR RAM... ");
        dumper.WritePpu(0x0000, data);
        Console.Write("Reading CHR RAM... ");
        var rdata = dumper.ReadPpu(0x0000, 0x2000);
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
            File.WriteAllBytes("chrramgood.bin", data);
            Console.WriteLine("chrramgood.bin writed");
            File.WriteAllBytes("chrrambad.bin", rdata);
            Console.WriteLine("chrrambad.bin writed");
            throw new IOException("Test failed");
        }
        Console.WriteLine("OK");

        Console.WriteLine("Multibank test.");
        data = new byte[chrSize];
        for (; count != 0; count--)
        {
            dumper.WriteCpu(0x5007, 0x2); // enable CHR writing
            rnd.NextBytes(data);
            for (byte bank = 0; bank < data.Length / 0x2000; bank++)
            {
                Console.WriteLine($"Writing CHR RAM bank #{bank}/{data.Length / 0x2000}...");
                dumper.WriteCpu(0x5003, (byte)(bank & 0b00011111)); // select bank, low 5 bits
                dumper.WriteCpu(0x5005, (byte)((bank & 0b00100000) << 2)); // select bank, 6th bit
                var d = new byte[0x2000];
                Array.Copy(data, bank * 0x2000, d, 0, 0x2000);
                dumper.WritePpu(0x0000, d);
            }
            for (byte bank = 0; bank < data.Length / 0x2000; bank++)
            {
                Console.Write($"Reading CHR RAM bank #{bank}/{data.Length / 0x2000}... ");
                dumper.WriteCpu(0x5003, (byte)(bank & 0b00011111)); // select bank, low 5 bits
                dumper.WriteCpu(0x5005, (byte)((bank & 0b00100000) << 2)); // select bank, 6th bit
                rdata = dumper.ReadPpu(0x0000, 0x2000);
                ok = true;
                for (int b = 0; b < 0x2000; b++)
                {
                    if (data[b + bank * 0x2000] != rdata[b])
                    {
                        Console.WriteLine($"Mismatch at {b:X4}: {rdata[b]:X2} != {data[b + bank * 0x2000]:X2}");
                        ok = false;
                    }
                }
                if (!ok)
                {
                    File.WriteAllBytes("chrramgoodf.bin", data);
                    Console.WriteLine("chrramgoodf.bin writed");
                    File.WriteAllBytes("chrrambad.bin", rdata);
                    Console.WriteLine("chrrambad.bin writed");
                    throw new InvalidDataException("Test failed");
                }
                Console.WriteLine("OK");
            }
        }
    }
}