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
        private readonly bool coolboyGpioMode;
        private int submapper;

        protected override IFamicomDumperConnectionExt dumper { get; }
        protected override int BankSize => 0x4000;
        protected override FlashEraseMode EraseMode => FlashEraseMode.Sector;
        protected override bool UseSubmappers => true;
        protected override bool CanUsePpbs => true;

        public CoolboyWriter(IFamicomDumperConnectionExt dumper, bool coolboyGpioMode, int submapper)
        {
            this.dumper = dumper;
            this.coolboyGpioMode = coolboyGpioMode;
            this.submapper = submapper;
        }

        protected override void Init()
        {
            if (coolboyGpioMode) dumper.SetCoolboyGpioMode(coolboyGpioMode);
        }

        protected override byte[] LoadPrg(string filename)
        {
            byte[] PRG;
            var extension = Path.GetExtension(filename).ToLower();
            switch (extension)
            {
                case ".bin":
                    if (submapper < 0)
                        throw new InvalidDataException("Can't autodetect submapper when using binary file, please use \"--coolboy-submapper\" option");
                    PRG = File.ReadAllBytes(filename);
                    break;
                case ".nes":
                    var nes = NesFile.FromFile(filename);
                    if (submapper < 0) submapper = nes.Submapper;
                    if (!CheckMapper(nes.Mapper, nes.Submapper))
                        Console.WriteLine($"WARNING! Invalid mapper: {nes.Mapper}{(UseSubmappers ? $".{nes.Submapper}" : "")}, most likely it will not work after writing.");
                    PRG = nes.PRG;
                    break;
                case ".unf":
                    var unif = UnifFile.FromFile(filename);
                    var mapper = unif.Mapper!;
                    if (mapper.StartsWith("NES-") || mapper.StartsWith("UNL-") || mapper.StartsWith("HVC-") || mapper.StartsWith("BTL-") || mapper.StartsWith("BMC-"))
                        mapper = mapper[4..];
                    if (submapper < 0)
                    {
                        switch (mapper)
                        {
                            case MAPPER_STRING_COOLBOY:
                                submapper = SUBMAPPER_NUMBER_COOLBOY;
                                break;
                            case MAPPER_STRING_MINDKIDS:
                                submapper = SUBMAPPER_NUMBER_MINDKIDS;
                                break;
                            default:
                                throw new InvalidDataException($"Invalid mapper: {unif.Mapper}");
                        }
                    }
                    if (!CheckMapper(mapper))
                        Console.WriteLine($"WARNING! Invalid mapper: {unif.Mapper}, most likely it will not work after writing.");
                    PRG = unif.PRG0!;
                    break;
                default:
                    throw new InvalidDataException($"Unknown file extension: {extension}, can't detect file format");
            }
            return PRG;
        }

        protected override bool CheckMapper(ushort mapper, byte submapper)
        {
            return ((mapper == MAPPER_NUMBER) && (submapper == this.submapper))
                || this.submapper < 0;
        }

        protected override bool CheckMapper(string mapper)
        {
            return ((mapper == MAPPER_STRING_COOLBOY) && (submapper == SUBMAPPER_NUMBER_COOLBOY))
                || ((mapper == MAPPER_STRING_MINDKIDS) && (submapper == SUBMAPPER_NUMBER_MINDKIDS))
                || submapper < 0;
        }

        protected override FlashInfo GetFlashInfo()
        {
            SelectBank(0);
            var cfi = FlashHelper.GetCFIInfo(dumper);
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
            dumper.EraseFlashSector();
        }

        protected override void Write(byte[] data, int offset)
        {
            SelectBank(offset / BankSize);
            dumper.WriteFlash(0x8000, data);
        }

        protected override ushort ReadCrc(int offset)
        {
            SelectBank(offset / BankSize);
            return dumper.ReadCpuCrc(0x8000, BankSize);
        }

        protected override void PPBSet(int offset)
        {
            SelectBank(offset / BankSize);
            FlashHelper.PPBSet(dumper);
        }

        protected override void PPBClear()
        {
            SelectBank(0);
            FlashHelper.PPBClear(dumper);
        }

        public override void PrintFlashInfo()
        {
            if (submapper < 0) throw new InvalidDataException("Can't autodetect submapper, please use \"--coolboy-submapper\" option");
            Program.Reset(dumper);
            Init();
            SelectBank(0);
            var cfi = FlashHelper.GetCFIInfo(dumper);
            FlashHelper.PrintCFIInfo(cfi);
            FlashHelper.LockBitsCheckPrint(dumper);
            FlashHelper.PPBLockBitCheckPrint(dumper);
        }

        private void SelectBank(int bank)
        {
            ushort baseRegOffset = submapper switch
            {
                0 => 0x6000,
                1 => 0x5000,
                2 => 0x7000,
                3 => 0x5000,
                4 => 0x6000,
                5 => 0x5000,
                6 => 0x6000,
                7 => 0x5000,
                _ => 0x6000,
            };
            byte r0, r1, r2, r3;
            switch (submapper)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 8:
                case 9:
                    r0 = (byte)(
                          ((bank >> 3) & 0b111) // 5(19), 4(18), 3(17) bits
                        | (((bank >> 9) & 0b11) << 4) // 10(24), 9(23) bits
                        | (1 << 6)); // PRG mask 256KB
                    break;
                case 4:
                case 5:
                    r0 = (byte)(
                          ((bank >> 3) & 0b111) // 5(19), 4(18), 3(17) bits
                        | (((bank >> 6) & 0b11) << 4) // 7(21), 6(20) bits
                        | (1 << 6)); // PRG mask 256KB
                    break;
                // TODO: submappers 6 and 7
                default:
                    throw new NotSupportedException($"Submapper {submapper} is not supported");
            }
            switch (submapper)
            {
                case 0:
                case 1:
                case 6:
                case 7:
                    r1 = (byte)(
                          (((bank >> 7) & 0b11) << 2) // 8(22), 7(21)
                        | (((bank >> 6) & 1) << 4) // 6(20)
                        | (1 << 7)); // PRG mask 512KB
                    break;
                case 2:
                case 3:
                    r1 = (byte)(
                          (((bank >> 8) & 1) << 1) // 8(22)
                        | (((bank >> 7) & 1) << 2) // 7(21)
                        | (((bank >> 6) & 1) << 3) // 6(20)
                        | (1 << 7)); // PRG mask 512KB
                    break;
                case 4:
                case 5:
                    r1 = 1 << 7; // PRG mask 512KB
                    break;
                default:
                    throw new NotSupportedException($"Submapper {submapper} is not supported");
            }
            r2 = 0;
            r3 = (byte)((1 << 4) // NROM mode
                | ((bank & 0b111) << 1)); // 2(16), 1(15), 0(14) bits
            dumper.WriteCpu(baseRegOffset, r0, r1, r2, r3);
        }
    }
}
