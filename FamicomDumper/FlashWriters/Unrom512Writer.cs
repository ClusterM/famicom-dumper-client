using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.Dumper.FlashWriters;
using com.clusterrr.Famicom.DumperConnection;
using RemoteDumper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;

namespace com.clusterrr.Famicom.Dumper.FlashWriters
{
    public class Unrom512Writer : FlashWriter
    {
        static int[] MAPPER_NUMBERS = { 2, 30 };
        static string[] MAPPER_STRINGS = { "UNROM", "UNROM-512", "UNROM-512-8", "UNROM-512-16", "UNROM-512-32" };

        protected override IFamicomDumperConnectionExt dumper { get; }
        protected override int BankSize => 0x4000;
        protected override FlashEraseMode EraseMode => FlashEraseMode.Chip;
        protected override bool NeedEnlarge => true;

        public Unrom512Writer(IFamicomDumperConnectionExt dumper)
        {
            this.dumper = dumper;
        }

        protected override void Init()
        {
            ResetFlash();
        }

        protected override bool CheckMapper(ushort mapper, byte submapper)
        {
            return MAPPER_NUMBERS.Contains(mapper);
        }

        protected override bool CheckMapper(string mapper)
        {
            return MAPPER_STRINGS.Contains(mapper);
        }

        protected override FlashInfo GetFlashInfo()
        {
            WriteFlashCmd(0x5555, 0xAA);
            WriteFlashCmd(0x2AAA, 0x55);
            WriteFlashCmd(0x5555, 0x90);

            var id = dumper.ReadCpu(0x8000, 2);
            int flashSize;
            switch (id[0])
            {
                case 0xBF: // Microchip
                    flashSize = id[1] switch
                    {
                        0xB5 => 128 * 1024, // SST39SF010A
                        0xB6 => 256 * 1024, // SST39SF020A
                        0xB7 => 512 * 1024, // SST39SF040
                        _ => 0,
                    };
                    break;
                case 0x01: // AMD
                    flashSize = id[1] switch
                    {
                        0xD9 => 128 * 1024, // Am29F100 (top)
                        0xDF => 128 * 1024, // Am29F100 (botton)
                        0x20 => 128 * 1024, // Am29F010 
                        0x51 => 256 * 1024, // Am29F200 (too)
                        0x57 => 256 * 1024, // Am29F200 (bottom)
                        0xB0 => 256 * 1024, // Am29F002 (top)
                        0x34 => 256 * 1024, // Am29F002 (bottom)
                        0xA4 => 512 * 1024, // Am29F040
                        0xD5 => 1024 * 1024, // Am29F080
                        0xAD => 2 * 1024 * 1024, // Am29F016
                        _ => 0,
                    };
                    break;
                case 0x9D: // PMC
                    flashSize = id[1] switch
                    {
                        0x1B => 64 * 1024,  // Pm39LV512
                        0x1C => 128 * 1024, // Pm39LV010
                        0x3D => 256 * 1024, // Pm39LV020
                        0x3E => 512 * 1024, // Pm39LV040
                        _ => 0,
                    };
                    break;
                default:
                    flashSize = 0;
                    break;
            }
            ResetFlash();
            return new FlashInfo()
            {
                ManufactorerId = id[0],
                DeviceId = id[1],
                DeviceSize = flashSize,
                MaximumNumberOfBytesInMultiProgram = 0,
                Regions = null
            };
        }

        protected override void Erase(int offset)
        {
            dumper.EraseUnrom512();
        }

        protected override void Write(byte[] data, int offset)
        {
            dumper.WriteUnrom512((uint)offset, data);
        }

        protected override ushort ReadCrc(int offset)
        {
            SelectBank((byte)(offset / BankSize));
            return dumper.ReadCpuCrc(0x8000, 0x4000);
        }

        public override void PrintFlashInfo()
        {
            ResetFlash();
            var flash = GetFlashInfo();
            if (flash.ManufactorerId != null)
                Console.WriteLine($"Manufactorer ID: {flash.ManufactorerId:X2}");
            if (flash.DeviceId != null)
                Console.WriteLine($"Device ID: {flash.DeviceId:X2}");
            var deviceSize = flash.DeviceSize;
            Console.WriteLine($"Device size: " + (deviceSize > 0 ? $"{deviceSize / 1024} KByte / {deviceSize / 1024 * 8} Kbit" : "unknown"));
        }

        void WriteFlashCmd(uint address, byte value)
        {
            dumper.WriteCpu(0xC000, (byte)(address >> 14));
            dumper.WriteCpu((ushort)(0x8000 | (address & 0x3FFF)), value);
        }

        void ResetFlash()
        {
            dumper.WriteCpu(0x8000, 0xF0);
        }

        void SelectBank(byte bank)
        {
            dumper.WriteCpu(0xC000, bank);
        }
    }
}
