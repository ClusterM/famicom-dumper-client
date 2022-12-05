using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace com.clusterrr.Famicom.Dumper.FlashWriters
{
    public class CoolboyWriter : FlashWriter
    {
        const int MAPPER_NUMBER = 268;
        const int SUBMAPPER_NUMBER_COOLBOY = 0;
        const int SUBMAPPER_NUMBER_MINDKIDS = 1;
        const string MAPPER_STRING_COOLBOY = "COOLBOY";
        const string MAPPER_STRING_MINDKIDS = "MINDKIDS";
        private enum CoolboyVersion { COOLBOY, MINDKIDS }
        private readonly bool coolboyGpioMode;
        private CoolboyVersion coolboyVersion;

        protected override IFamicomDumperConnectionExt Dumper { get; }
        protected override int BankSize => 0x4000;
        protected override FlashEraseMode EraseMode => FlashEraseMode.Sector;
        protected override bool UseSubmappers => true;
        protected override bool CanUsePpbs => true;

        public CoolboyWriter(IFamicomDumperConnectionExt dumper, bool coolboyGpioMode)
        {
            Dumper = dumper;
            this.coolboyGpioMode = coolboyGpioMode;
        }

        protected override void Init()
        {
            Dumper.SetCoolboyGpioMode(coolboyGpioMode);
            Console.Write("Detecting cartridge version... ");
            // 0th CHR bank using both methods
            Dumper.WriteCpu(0x5000, 0, 0, 0, 0x10);
            Dumper.WriteCpu(0x6000, 0, 0, 0, 0x10);
            // Writing 0
            Dumper.WritePpu(0x0000, 0);
            // First CHR bank using both methods
            Dumper.WriteCpu(0x5000, 0, 0, 1, 0x10);
            Dumper.WriteCpu(0x6000, 0, 0, 1, 0x10);
            // Writing 1
            Dumper.WritePpu(0x0000, 1);
            // 0th bank using first method
            Dumper.WriteCpu(0x6000, 0, 0, 0, 0x10);
            byte v6000 = Dumper.ReadPpu(0x0000);
            // return
            Dumper.WriteCpu(0x6000, 0, 0, 1, 0x10);
            // 0th bank using second method
            Dumper.WriteCpu(0x5000, 0, 0, 0, 0x10);
            byte v5000 = Dumper.ReadPpu(0x0000);

            if (v6000 == 0 && v5000 == 1)
                coolboyVersion = CoolboyVersion.COOLBOY;
            else if (v6000 == 1 && v5000 == 0)
                coolboyVersion = CoolboyVersion.MINDKIDS;
            else
                throw new IOException("Can't detect cartridge version");
            Console.WriteLine($"Cartridge version: {coolboyVersion}");
        }

        protected override bool CheckMapper(ushort mapper, byte submapper)
        {
            return (mapper == MAPPER_NUMBER) && (
                ((coolboyVersion == CoolboyVersion.COOLBOY) && (submapper == SUBMAPPER_NUMBER_COOLBOY))
                || ((coolboyVersion == CoolboyVersion.MINDKIDS) && (submapper == SUBMAPPER_NUMBER_MINDKIDS))
            );
        }

        protected override bool CheckMapper(string mapper)
        {
            return ((coolboyVersion == CoolboyVersion.COOLBOY) && (mapper == MAPPER_STRING_COOLBOY))
                || ((coolboyVersion == CoolboyVersion.MINDKIDS) && (mapper == MAPPER_STRING_MINDKIDS));
        }

        protected override FlashInfo GetFlashInfo()
        {
            SelectBank(0);
            var cfi = FlashHelper.GetCFIInfo(Dumper);
            return new FlashInfo()
            {
                DeviceSize = (int)cfi.DeviceSize,
                MaximumNumberOfBytesInMultiProgram = cfi.MaximumNumberOfBytesInMultiProgram,
                Regions = cfi.EraseBlockRegionsInfo
            };
        }

        protected override void Erase(int offset)
        {
            SelectBank(offset / BankSize);
            Dumper.EraseFlashSector();
        }

        protected override void Write(byte[] data, int offset)
        {
            SelectBank(offset / BankSize);
            Dumper.WriteFlash(0x8000, data);
        }

        protected override ushort ReadCrc(int offset)
        {
            SelectBank(offset / BankSize);
            return Dumper.ReadCpuCrc(0x8000, BankSize);
        }

        protected override void PPBSet(int offset)
        {
            SelectBank(offset / BankSize);
            FlashHelper.PPBSet(Dumper);
        }

        protected override void PPBClear()
        {
            SelectBank(0);
            FlashHelper.PPBClear(Dumper);
        }

        public override void PrintFlashInfo()
        {
            Program.Reset(Dumper);
            Init();
            SelectBank(0);
            var cfi = FlashHelper.GetCFIInfo(Dumper);
            FlashHelper.PrintCFIInfo(cfi);
            FlashHelper.LockBitsCheckPrint(Dumper);
            FlashHelper.PPBLockBitCheckPrint(Dumper);
        }

        private void SelectBank(int bank)
        {
            ushort coolboyReg = coolboyVersion switch
            {
                CoolboyVersion.MINDKIDS => 0x5000,
                _ => 0x6000,
            };
            byte r0 = (byte)(((bank >> 3) & 0x07) // 5, 4, 3 bits
                | (((bank >> 9) & 0x03) << 4) // 10, 9 bits
                | (1 << 6)); // resets 4th mask bit
            byte r1 = (byte)((((bank >> 7) & 0x03) << 2) // 8, 7
                | (((bank >> 6) & 1) << 4) // 6
                | (1 << 7)); // resets 5th mask bit
            byte r2 = 0;
            byte r3 = (byte)((1 << 4) // NROM mode
                | ((bank & 7) << 1)); // 2, 1, 0 bits
            Dumper.WriteCpu(coolboyReg, r0, r1, r2, r3);
        }
    }
}
