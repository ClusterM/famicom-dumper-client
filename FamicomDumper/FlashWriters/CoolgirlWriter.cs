using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using static com.clusterrr.Famicom.Dumper.FlashWriters.CFIInfo;

namespace com.clusterrr.Famicom.Dumper.FlashWriters
{
    public class CoolgirlWriter : FlashWriter
    {
        const int MAPPER_NUMBER = 342;
        const string MAPPER_STRING = "COOLGIRL";

        protected override IFamicomDumperConnectionExt Dumper { get; }
        protected override int BankSize => 0x8000;
        protected override FlashEraseMode EraseMode => FlashEraseMode.Sector;
        protected override bool CanUsePpbs => true;

        public CoolgirlWriter(IFamicomDumperConnectionExt dumper)
        {
            Dumper = dumper;
        }

        protected override void Init()
        {

        }

        protected override bool CheckMapper(ushort mapper, byte submapper)
        {
            return mapper == MAPPER_NUMBER;
        }

        protected override bool CheckMapper(string mapper)
        {
            return mapper == MAPPER_STRING;
        }

        protected override FlashInfo GetFlashInfo()
        {
            var cfi = FlashHelper.GetCFIInfo(Dumper);
            return new FlashInfo()
            {
                DeviceSize = (int)cfi.DeviceSize,
                MaximumNumberOfBytesInMultiProgram = cfi.MaximumNumberOfBytesInMultiProgram,
                Regions = cfi.EraseBlockRegionsInfo
            };
        }

        protected override void InitBanking()
        {
            Dumper.WriteCpu(0x5007, 0x04); // enable PRG write
            Dumper.WriteCpu(0x5002, 0xFE); // mask = 32K
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

        protected override void PPBClear()
        {
            SelectBank(0);
            FlashHelper.PPBClear(Dumper);
        }

        protected override void PPBSet(int offset)
        {
            SelectBank(offset / BankSize);
            FlashHelper.PPBSet(Dumper);
        }

        public override void PrintFlashInfo()
        {
            Program.Reset(Dumper);
            Init();
            InitBanking();
            var cfi = FlashHelper.GetCFIInfo(Dumper);
            FlashHelper.PrintCFIInfo(cfi);
            FlashHelper.LockBitsCheckPrint(Dumper);
            FlashHelper.PPBLockBitCheckPrint(Dumper);
        }

        private void SelectBank(int bank)
        {
            byte r0 = (byte)(bank >> 7);
            byte r1 = (byte)(bank << 1);
            Dumper.WriteCpu(0x5000, r0, r1);
        }   
    }
}
