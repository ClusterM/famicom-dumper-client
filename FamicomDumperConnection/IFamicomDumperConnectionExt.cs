using com.clusterrr.Famicom.Containers;
using System;
using System.Collections.Generic;

namespace com.clusterrr.Famicom.DumperConnection
{
    public interface IFamicomDumperConnectionExt : IFamicomDumperConnection
    {
        /// <summary>
        /// Init dumper (flush queud data, check connection)
        /// </summary>
        /// <returns></returns>
        public bool Init();

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
        /// Read CRC16 checksum of data at CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Checksum</returns>
        ushort ReadCpuCrc(ushort address, int length);

        /// <summary>
        /// Read CRC16 checksum of data at PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Checksum</returns>
        ushort ReadPpuCrc(ushort address, int length);

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
        public (byte[] Data, bool CrcOk, bool EndOfHeadMeet)[] ReadFdsBlocks(byte startBlock = 0, byte maxBlockCount = byte.MaxValue);


        /// <summary>
        /// Set maximum number of bytes in multi-byte flash program
        /// </summary>
        /// <param name="pageSize"></param>
        void SetMaximumNumberOfBytesInMultiProgram(uint pageSize);
    }
}