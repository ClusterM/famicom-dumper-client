/* CHR RAM test script
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
 * Usage: famicom-dumper script --cs-file ChrRamTest.cs - [number of repetitions]
 */

using System.IO;
using System.Linq;
using System;

class TestChrRam
{
    void Run(IFamicomDumperConnection dumper, string[] args)
    {
        int count = -1;
        if (args.Any())
            count = int.Parse(args.First());

        var rnd = new Random();
        while (count != 0)
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
                    Console.WriteLine($"Mismatch at {b:X4}: {rdata[b]:X2} != {data[b]:X2}");
                    ok = false;
                }
            }
            if (!ok)
            {
                DetectProblems(data, rdata, "PPU");
                File.WriteAllBytes("chrramgood.bin", data);
                Console.WriteLine("chrramgood.bin writed");
                File.WriteAllBytes("chrrambad.bin", rdata);
                Console.WriteLine("chrrambad.bin writed");
                throw new InvalidDataException("Failed!");
            }
            Console.WriteLine("OK!");
            count--;
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
