using com.clusterrr.Famicom.Containers;
using System.Collections.Generic;

namespace com.clusterrr.Famicom.DumperConnection
{
    public interface IFamicomDumperConnection
    {
        /// <summary>
        /// Famicom Dumper serial protocol version (depends on firmware)
        /// </summary>
        byte ProtocolVersion { get; }

        /// <summary>
        /// Famicom Dumper maximum read packet size (depends on firmware and hardware)
        /// </summary>
        ushort MaxReadPacketSize { get; }

        /// <summary>
        /// Famicom Dumper maximum read packet size (depends on firmware and hardware)
        /// </summary>
        ushort MaxWritePacketSize { get; }

        /// <summary>
        /// Timeout for all read/write operations (in milliseconds)
        /// </summary>
        uint Timeout { get; set; }

        /// <summary>
        /// Simulate reset (M2 goes to Z-state for a second)
        /// </summary>
        void Reset();

        /// <summary>
        /// Read single byte from CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <returns>Data from CPU (PRG) bus</returns>
        byte ReadCpu(ushort address);
        
        /// <summary>
        /// Read data from CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Data from CPU (PRG) bus</returns>
        byte[] ReadCpu(ushort address, int length);

        /// <summary>
        /// Read CRC16 checksum of data at CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Checksum</returns>
        ushort ReadCpuCrc(ushort address, int length);

        /// <summary>
        /// Read single byte from PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <returns>Data from PPU (CHR) bus</returns>
        byte ReadPpu(ushort address);

        /// <summary>
        /// Read data from PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Data from PPU (CHR) bus</returns>
        byte[] ReadPpu(ushort address, int length);

        /// <summary>
        /// Read CRC16 checksum of data at PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Checksum</returns>
        ushort ReadPpuCrc(ushort address, int length);
 
        /// <summary>
        /// Write data to CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        void WriteCpu(ushort address, params byte[] data);

        /// <summary>
        /// Write data to PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        void WritePpu(ushort address, params byte[] data);

        /// <summary>
        /// Erase COOLBOY/GOOLGIRL current flash sector
        /// </summary>
        void EraseFlashSector();

        /// <summary>
        /// Write COOLBOY/GOOLGIRL flash memory
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        void WriteFlash(ushort address, byte[] data);

        /// <summary>
        /// Read Famicom Disk System blocks
        /// </summary>
        /// <param name="startBlock">First block number to read (zero-based)</param>
        /// <param name="maxBlockCount">Maximum number of blocks to read</param>
        /// <returns>Array of Famicom Disk System blocks</returns>
        IFdsBlock[] ReadFdsBlocks(byte startBlock = 0, byte maxBlockCount = byte.MaxValue);

        /// <summary>
        /// Write blocks to Famicom Disk System card
        /// </summary>
        /// <param name="blockNumbers">Block numbers to write (zero-based)</param>
        /// <param name="blocks">Raw blocks data</param>
        void WriteFdsBlocks(byte[] blockNumbers, byte[][] blocks);
        /// <summary>
        /// Write blocks to Famicom Disk System card
        /// </summary>
        /// <param name="blockNumbers">Block numbers to write (zero-based)</param>
        /// <param name="blocks">Blocks data</param>
        void WriteFdsBlocks(byte[] blockNumbers, IEnumerable<IFdsBlock> blocks);

        /// <summary>
        /// Write single block to Famicom Disk System card
        /// </summary>
        /// <param name="blockNumbers">Block numbers to write (zero-based)</param>
        /// <param name="block">Block data</param>
        void WriteFdsBlocks(byte blockNumber, byte[] block);

        /// <summary>
        /// Write single block to Famicom Disk System card
        /// </summary>
        /// <param name="blockNumbers">Block numbers to write (zero-based)</param>
        /// <param name="block">Block data</param>
        void WriteFdsBlocks(byte blockNumber, IFdsBlock block);

        /// <summary>
        /// Read raw mirroring values (CIRAM A10 pin states for different states of PPU A10 and A11)
        /// </summary>
        /// <returns>Values of CIRAM A10 pin for $2000-$23FF, $2400-$27FF, $2800-$2BFF and $2C00-$2FFF</returns>
        bool[] GetMirroringRaw();

        /// <summary>
        /// Read decoded current mirroring mode
        /// </summary>
        /// <returns>Current mirroring</returns>
        NesFile.MirroringType GetMirroring();

        /// <summary>
        /// Set maximum number of bytes in multi-byte flash program
        /// </summary>
        /// <param name="pageSize"></param>
        void SetMaximumNumberOfBytesInMultiProgram(uint pageSize);
    }
}