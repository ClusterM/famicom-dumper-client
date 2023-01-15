/* PRG RAM test script
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
 * Usage: famicom-dumper script --cs-file PrgRamTest.cs --mapper <mapper> - [number of repetitions]
 */

class TestPrgRam
{
    void Run(IFamicomDumperConnection dumper, IMapper mapper, string[] args)
    {
        int count = -1;
        if (args.Any())
            count = int.Parse(args.First());
        if (mapper != null)
        {
            if (mapper.Number >= 0)
                Console.WriteLine($"Using mapper: #{mapper.Number} ({mapper.Name})");
            else
                Console.WriteLine($"Using mapper: {mapper.Name}");
            mapper.EnablePrgRam(dumper);
        }
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
                DetectProblems(data, rdata, "CPU");
                File.WriteAllBytes("prgramgood.bin", data);
                Console.WriteLine("prgramgood.bin writed");
                File.WriteAllBytes("prgrambad.bin", rdata);
                Console.WriteLine("prgrambad.bin writed");
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
