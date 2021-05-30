using com.clusterrr.Famicom.Containers;
using System;
using System.Collections.Generic;

namespace com.clusterrr.Famicom.DumperConnection
{
    public interface IFamicomDumperConnection : IDisposable
    {
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
        /// Write blocks to Famicom Disk System card
        /// </summary>
        /// <param name="blockNumbers">Block numbers to write (zero-based)</param>
        /// <param name="blocks">Raw blocks data</param>
        void WriteFdsBlocks(byte[] blockNumbers, byte[][] blocks);

        /// <summary>
        /// Write single block to Famicom Disk System card
        /// </summary>
        /// <param name="blockNumbers">Block number to write (zero-based)</param>
        /// <param name="block">Raw block data</param>
        void WriteFdsBlocks(byte blockNumber, byte[] block);

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
    }
}