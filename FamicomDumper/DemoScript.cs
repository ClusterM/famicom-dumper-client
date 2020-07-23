namespace com.clusterrr.Demo // You can use any namespace
{
    class DemoScript // Class name also doesn't matter
    {
        // But method signature must be like this. Also you can make this method static.
        void Run(IFamicomDumperConnection dumper)
        {
            Console.WriteLine("Please insert MMC3 cartridge and press any key");
            Console.ReadKey();

            Console.WriteLine("Let's check - how many PRG banks on this MMC3 cartridge");
            byte[] firstBank = null;
            for (var bank = 0; ; bank = bank == 0 ? 1 : bank * 2)
            {
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
