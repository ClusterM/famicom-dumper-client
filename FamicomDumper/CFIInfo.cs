using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace com.clusterrr.Famicom.DumperConnection
{
    public struct CFIInfo
    {
        public enum ParseMode
        {
            Every1Byte,
            Every2Bytes,
            Every4Bytes
        }

        public enum FlashDeviceInterface
        {
            x8_only = 0x0000,
            x16_only = 0x0001,
            x8_and_x16_via_byte_pin = 0x0002,
            x32_only = 0x0003,
            x8_and_x16_via_word_pin = 0x0004
        }

        public struct EraseBlockRegionInfo
        {
            public readonly ushort NumberOfBlocks;
            public readonly uint SizeOfBlocks;

            public EraseBlockRegionInfo(ushort numberOfBlocks, uint sizeOfBlocks)
            {
                NumberOfBlocks = numberOfBlocks;
                SizeOfBlocks = sizeOfBlocks;
            }
        }

        /// <summary>
        /// Primary Algorithm Command Set and Control Interface ID Code 16-bit ID code defining a specific algorithm. (Ref.JEP137)
        /// </summary>
        public readonly ushort PrimaryAlgorithmCommandSet;
        /// <summary>
        /// Alternative Algorithm Command Set and Control Interface ID Code
        /// second specific algorithm supported by the device. (Ref.JEP137)
        /// NOTE — ID Code = 0000h means that no alternate algorithm is employed.
        /// </summary>
        public readonly ushort AlternativeAlgorithmCommandSet;
        /// <summary>
        /// Vcc Logic Supply Minimum Program/Erase or Write voltage
        /// </summary>
        public readonly float VccLogicSupplyMinimumProgramErase;
        /// <summary>
        /// Vcc Logic Supply Maximum Program/Erase or Write voltage 
        /// </summary>
        public readonly float VccLogicSupplyMaximumProgramErase;
        /// <summary>
        /// Vpp [Programming] Supply Minimum Program/Erase voltage
        /// </summary>
        public readonly float VppSupplyMinimumProgramErasevoltage;
        /// <summary>
        /// Vpp [Programming] Supply Maximum Program/Erase voltage
        /// </summary>
        public readonly float VppSupplyMaximumProgramErasevoltage;
        /// <summary>
        /// Typical timeout per single byte/word/D-word program (multi-byte program count = 1), µs
        /// </summary>
        public readonly uint TypicalTimeoutPerSingleProgram;
        /// <summary>
        /// Typical timeout for maximum-size multi-byte program, µs
        /// </summary>
        public readonly uint TypicalTimeoutForMaximumSizeMultiByteProgram;
        /// <summary>
        /// Typical timeout per individual block erase, ms
        /// </summary>
        public readonly uint TypicalTimeoutPerIndividualBlockErase;
        /// <summary>
        /// Typical timeout for full chip erase, ms
        /// </summary>
        public readonly uint TypicalTimeoutForFullChipErase;
        /// <summary>
        /// Maximum timeout per single byte/word/D-word program (multi-byte program count = 1), µs
        /// </summary>
        public readonly uint MaximumTimeoutPerSingleProgram;
        /// <summary>
        /// Maximum timeout for maximum-size multi-byte program, µs
        /// </summary>
        public readonly uint MaximumTimeoutForMaximumSizeMultiByteProgram;
        /// <summary>
        /// Maximum timeout per individual block erase, ms
        /// </summary>
        public readonly uint MaximumTimeoutPerIndividualBlockErase;
        /// <summary>
        /// Maximum timeout for full chip erase, ms
        /// </summary>
        public readonly uint MaximumTimeoutForFullChipErase;
        /// <summary>
        /// Device Size
        /// </summary>
        public readonly uint DeviceSize;
        /// <summary>
        /// Flash Device Interface Code description (Ref. JEP137)
        /// </summary>
        public readonly FlashDeviceInterface FlashDeviceInterfaceCodeDescription;
        /// <summary>
        /// Maximum number of bytes in multi-byte program
        /// </summary>
        public readonly ushort MaximumNumberOfBytesInMultiProgram;
        /// <summary>
        /// Erase Block Regions Information
        /// </summary>
        public readonly ReadOnlyCollection<EraseBlockRegionInfo> EraseBlockRegionsInfo;

        public CFIInfo(byte[] data, ParseMode parseMode)
        {
            switch (parseMode)
            {
                case ParseMode.Every2Bytes:
                    {
                        var newData = new byte[data.Length / 2];
                        for (int i = 0; i < newData.Length; i++)
                            newData[i] = data[i * 2];
                        data = newData;
                        break;
                    }
                case ParseMode.Every4Bytes:
                    {
                        var newData = new byte[data.Length / 4];
                        for (int i = 0; i < newData.Length; i++)
                            newData[i] = data[i * 4];
                        data = newData;
                        break;
                    }
            }
            if (data[0x10] != 0x51 || data[0x11] != 0x52 || data[0x12] != 0x59)
            {
                throw new IOException("Can't enter CFI mode. Invalid flash memory? Broken cartridge? Is it inserted?");
            }

            PrimaryAlgorithmCommandSet = (ushort)(data[0x13] + data[0x14] * 0x100);
            //var p = (ushort)(data[0x15] + data[0x16] * 0x100);
            AlternativeAlgorithmCommandSet = (ushort)(data[0x17] + data[0x18] * 0x100);
            //var a = (ushort)(data[0x19] + data[0x20] * 0x100);
            VccLogicSupplyMinimumProgramErase = (float)((data[0x1B] >> 4) + 0.1 * (data[0x1B] & 0x0F));
            VccLogicSupplyMaximumProgramErase = (float)((data[0x1C] >> 4) + 0.1 * (data[0x1C] & 0x0F));
            VppSupplyMinimumProgramErasevoltage = (float)((data[0x1D] >> 4) + 0.1 * (data[0x1D] & 0x0F));
            VppSupplyMaximumProgramErasevoltage = (float)((data[0x1E] >> 4) + 0.1 * (data[0x1E] & 0x0F));
            TypicalTimeoutPerSingleProgram = data[0x1F] == 0 ? 0 : (1U << data[0x1F]);
            TypicalTimeoutForMaximumSizeMultiByteProgram = data[0x20] == 0 ? 0 : (1U << data[0x20]);
            TypicalTimeoutPerIndividualBlockErase = data[0x21] == 0 ? 0 : (1U << data[0x21]);
            TypicalTimeoutForFullChipErase = data[0x22] == 0 ? 0 : (1U << data[0x22]);
            MaximumTimeoutPerSingleProgram = data[0x1F] == 0 ? 0 : ((1U << data[0x1F]) * (1U << data[0x23]));
            MaximumTimeoutForMaximumSizeMultiByteProgram = data[0x20] == 0 ? 0 : ((1U << data[0x20]) * (1U << data[0x24]));
            MaximumTimeoutPerIndividualBlockErase = data[0x21] == 0 ? 0 : ((1U << data[0x21]) * (1U << data[0x25]));
            MaximumTimeoutForFullChipErase = data[0x22] == 0 ? 0 : ((1U << data[0x22]) * (1U << data[0x26]));
            DeviceSize = 1U << data[0x27];
            FlashDeviceInterfaceCodeDescription = (FlashDeviceInterface)(data[0x28] + data[0x29] * 0x100);
            MaximumNumberOfBytesInMultiProgram = (ushort)(1U << (data[0x2A] + data[0x2B] * 0x100));
            var eraseBlockRegions = data[0x2C];
            var regions = new List<EraseBlockRegionInfo>();
            for (int i = 0; i < eraseBlockRegions; i++)
            {
                ushort numberOfBlocks = (ushort)(data[0x2D + i * 4] + data[0x2E + i * 4] * 0x100 + 1);
                uint sizeOfBlocks = (ushort)(data[0x2F + i * 4] + data[0x30 + i * 4] * 0x100);
                sizeOfBlocks = sizeOfBlocks == 0 ? 128 : (256 * sizeOfBlocks);
                regions.Add(new EraseBlockRegionInfo(numberOfBlocks, sizeOfBlocks));
            }
            EraseBlockRegionsInfo = regions.AsReadOnly();
        }
    }
}
