namespace com.clusterrr.Demo // You can use any namespace
{
    class DemoScript // Class name also doesn't matter
    {
        // But method signature must be like this. Also you can make this method static.
        void Run(IFamicomDumperConnection dumper, string[] args)
        {
            // You can parse additional command line arguments if need
            // Specify arguments this way: >FamicomDumper.exe script --csfile DemoScript.cs - argument1 argument2 argument3
            if (args.Any())
                Console.WriteLine("Command line arguments: " + string.Join(", ", args));

            Console.WriteLine("Please insert MMC3 cartridge and press enter");
            Console.ReadLine();

            Console.WriteLine("Let's check - how many PRG banks on this MMC3 cartridge");
            byte[] firstBank = null;
            for (var bank = 0; ; bank = bank == 0 ? 1 : bank * 2)
            {
                if (bank > 256) throw new InvalidDataException("Bank number out of range, did you actually insert the MMC3 cartridge?");
                Console.Write("Reading PRG bank #{0}... ", bank);
                dumper.WriteCpu(0x8000, new byte[] { 6, (byte)bank });
                var data = dumper.ReadCpu(0x8000, 0x2000);
                Console.WriteLine("OK");
                if (bank == 0)
                    firstBank = data;
                else if (Enumerable.SequenceEqual(data, firstBank))
                {
                    Console.WriteLine("There are {0} PRG banks on this cartridge, {1} KBytes in total", bank, bank * 0x2000 / 1024);
                    break;
                }
            }

            Console.WriteLine("Let's check - how many CHR banks on this MMC3 cartridge");
            for (var bank = 0; ; bank = bank == 0 ? 1 : bank * 2)
            {
                if (bank > 256) throw new InvalidDataException("Bank number out of range, did you actually insert the MMC3 cartridge?");
                Console.Write("Reading CHR bank #{0}... ", bank);
                dumper.WriteCpu(0x8000, new byte[] { 2, (byte)bank });
                var data = dumper.ReadPpu(0x1000, 0x0400);
                Console.WriteLine("OK");
                if (bank == 0)
                    firstBank = data;
                else if (Enumerable.SequenceEqual(data, firstBank))
                {
                    Console.WriteLine("There are {0} CHR banks on this cartridge, {1} KBytes in total", bank, bank * 0x0400 / 1024);
                    break;
                }
            }
        }
    }
}
