using com.clusterrr.Communication;
using com.clusterrr.Famicom.Containers;
using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace com.clusterrr.Famicom.DumperConnection
{
    public class FamicomDumperLocal : IDisposable, IFamicomDumperConnectionExt
    {
        readonly string[] DEVICE_NAMES = new string[] { "Famicom Dumper/Programmer", "Famicom Dumper/Writer" };
        const uint TIMEOUT = 10000;
        const uint BAUDRATE = 250000;

        public byte ProtocolVersion { get; private set; } = 0;
        public Version FirmwareVersion { get; private set; } = null;
        public Version HardwareVersion { get; private set; } = null;
        public uint Timeout { get => connection.Timeout; set => connection.Timeout = value; }
        public ushort MaxReadPacketSize { get => connection.MaxReadPacketSize; set => connection.MaxReadPacketSize = value; }
        public ushort MaxWritePacketSize { get => connection.MaxWritePacketSize; set => connection.MaxWritePacketSize = value; }
        private SerialClient connection = new SerialClient();

        enum DumperCommand
        {
            STARTED = 0,
            CHR_STARTED = 1, // deprecated
            ERROR_INVALID = 2,
            ERROR_CRC = 3,
            ERROR_OVERFLOW = 4,
            PRG_INIT = 5,
            CHR_INIT = 6,
            PRG_READ_REQUEST = 7,
            PRG_READ_RESULT = 8,
            PRG_WRITE_REQUEST = 9,
            PRG_WRITE_DONE = 10,
            CHR_READ_REQUEST = 11,
            CHR_READ_RESULT = 12,
            CHR_WRITE_REQUEST = 13,
            CHR_WRITE_DONE = 14,
            MIRRORING_REQUEST = 17,
            MIRRORING_RESULT = 18,
            RESET = 19,
            RESET_ACK = 20,
            FLASH_ERASE_SECTOR_REQUEST = 37,
            FLASH_WRITE_REQUEST = 38,
            PRG_CRC_READ_REQUEST = 39,
            CHR_CRC_READ_REQUEST = 40,
            FLASH_WRITE_ERROR = 41,
            FLASH_WRITE_TIMEOUT = 42,
            FLASH_ERASE_ERROR = 43,
            FLASH_ERASE_TIMEOUT = 44,
            FDS_READ_REQUEST = 45,
            FDS_READ_RESULT_BLOCK = 46,
            FDS_READ_RESULT_END = 47,
            FDS_TIMEOUT = 48,
            FDS_NOT_CONNECTED = 49,
            FDS_BATTERY_LOW = 50,
            FDS_DISK_NOT_INSERTED = 51,
            FDS_END_OF_HEAD = 52,
            FDS_WRITE_REQUEST = 53,
            FDS_WRITE_DONE = 54,
            SET_FLASH_BUFFER_SIZE = 55,
            SET_VALUE_DONE = 56,
            FDS_DISK_WRITE_PROTECTED = 57,
            FDS_BLOCK_CRC_ERROR = 58,
            COOLBOY_GPIO_MODE = 59,
            UNROM512_ERASE_REQUEST = 60,
            UNROM512_WRITE_REQUEST = 61,

            DEBUG = 0xFF
        }

        public FamicomDumperLocal()
        {
        }

        public void Open(string portName = null)
        {
            ProtocolVersion = 0;
            FirmwareVersion = null;
            HardwareVersion = null;
            connection.Open(portName, BAUDRATE, TIMEOUT, DEVICE_NAMES);
        }

        public void Close()
        {
            connection.Close();
        }

        private void SendCommand(DumperCommand command, byte[] data) => connection.SendCommand((byte)command, data);

        private (DumperCommand Command, byte[] Data) RecvCommand()
        {
            byte command;
            byte[] data;
            bool debugging = false;
            do
            {
                (command, data) = connection.RecvCommand();
#if DEBUG
                if (command == (byte)DumperCommand.DEBUG)
                {
                    if (!debugging)
                        Console.Write("Debug data: ");
                    foreach (var b in data)
                    {
                        Console.Write($"{b:X02} ");
                    }
                    debugging = true;
                } else if (debugging)
                {
                    Console.WriteLine();
                }
#endif
            }
            while (command == (byte)DumperCommand.DEBUG);
            return ((DumperCommand)command, data);
        }

        /// <summary>
        /// Init dumper (flush queud data, check connection)
        /// </summary>
        /// <returns></returns>
        public bool Init()
        {
            bool result = false;
            var oldTimeout = Timeout;
            try
            {
                Timeout = 250;
                // Flush all queud data
                while (true)
                {
                    try
                    {
                        RecvCommand();
                    }
                    catch
                    {
                        break;
                    }
                }
                for (int i = 0; i < 300 && !result; i++)
                {
                    try
                    {
                        SendCommand(DumperCommand.PRG_INIT, Array.Empty<byte>());
                        (var command, var data) = RecvCommand();
                        if (command == DumperCommand.STARTED)
                        {
                            if (data.Length >= 1)
                                ProtocolVersion = data[0];
                            if (data.Length >= 3)
                                MaxReadPacketSize = (ushort)(data[1] | (data[2] << 8));
                            if (data.Length >= 5)
                                MaxWritePacketSize = (ushort)(data[3] | (data[4] << 8));
                            if (data.Length >= 9)
                                FirmwareVersion = new Version(data[5] | (data[6] << 8), data[7], data[8]);
                            if (data.Length >= 13)
                                HardwareVersion = new Version(data[9] | (data[10] << 8), data[11], data[12]);
                            result = true;
                        }
                    }
                    catch { }
                }
            }
            finally
            {
                Timeout = oldTimeout;
            }
            return result;
        }

        /// <summary>
        /// Simulate reset (M2 goes to Z-state for a second)
        /// </summary>
        public void Reset()
        {
            SendCommand(DumperCommand.RESET, Array.Empty<byte>());
            (var command, _) = RecvCommand();
            if (command != DumperCommand.RESET_ACK)
                throw new IOException($"Invalid data received: {command}");
        }

        /// <summary>
        /// Read single byte from CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <returns>Data from CPU (PRG) bus</returns>
        public byte ReadCpu(ushort address) => ReadCpu(address, 1)[0];

        /// <summary>
        /// Read data from CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Data from CPU (PRG) bus</returns>
        public byte[] ReadCpu(ushort address, int length)
        {
            var result = new List<byte>();
            while (length > 0)
            {
                result.AddRange(ReadCpuBlock(address, Math.Min(MaxReadPacketSize, length)));
                address += MaxReadPacketSize;
                length -= MaxReadPacketSize;
            }
            return result.ToArray();
        }

        private byte[] ReadCpuBlock(ushort address, int length)
        {
            var buffer = new byte[4];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            SendCommand(DumperCommand.PRG_READ_REQUEST, buffer);
            (var command, var data) = RecvCommand();
            if (command != DumperCommand.PRG_READ_RESULT)
                throw new IOException($"Invalid data received: {command}");
            return data;
        }

        /// <summary>
        /// Read CRC16 checksum of data at CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Checksum</returns>
        public ushort ReadCpuCrc(ushort address, int length)
        {
            var buffer = new byte[4];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            SendCommand(DumperCommand.PRG_CRC_READ_REQUEST, buffer);
            (var command, var data) = RecvCommand();
            if (command != DumperCommand.PRG_READ_RESULT)
                throw new IOException($"Invalid data received: {command}");
            var crc = (ushort)(data[0] | (data[1] << 8));
            return crc;
        }

        /// <summary>
        /// Write data to CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        public void WriteCpu(ushort address, params byte[] data)
        {
            int wlength = data.Length;
            int pos = 0;
            while (wlength > 0)
            {
                var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                Array.Copy(data, pos, wdata, 0, wdata.Length);
                WriteCpuBlock(address, wdata);
                address += (ushort)wdata.Length;
                pos += wdata.Length;
                wlength -= wdata.Length;
            }
            return;
        }

        private void WriteCpuBlock(ushort address, byte[] data)
        {
            int length = data.Length;
            var buffer = new byte[4 + length];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            Array.Copy(data, 0, buffer, 4, length);
            SendCommand(DumperCommand.PRG_WRITE_REQUEST, buffer);
            (var command, _) = RecvCommand();
            if (command != DumperCommand.PRG_WRITE_DONE)
                throw new IOException($"Invalid data received: {command}");
        }

        /// <summary>
        /// Erase COOLBOY/GOOLGIRL current flash sector
        /// </summary>
        public void EraseFlashSector()
        {
            SendCommand(DumperCommand.FLASH_ERASE_SECTOR_REQUEST, Array.Empty<byte>());
            (var command, var data) = RecvCommand();
            if (command == DumperCommand.FLASH_ERASE_ERROR)
                throw new IOException($"Flash erase error (0x{data[0]:X2})");
            else if (command == DumperCommand.FLASH_ERASE_TIMEOUT)
                throw new TimeoutException($"Flash erase timeout");
            else if (command != DumperCommand.PRG_WRITE_DONE)
                throw new IOException($"Invalid data received: {command}");
        }

        /// <summary>
        /// Write COOLBOY/GOOLGIRL flash memory
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        public void WriteFlash(ushort address, byte[] data)
        {
            int wlength = data.Length;
            int pos = 0;
            while (wlength > 0)
            {
                var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                Array.Copy(data, pos, wdata, 0, wdata.Length);
                if (data.Where(b => b != 0xFF).Any()) // if there is any not FF byte
                    WriteCpuFlashBlock(address, wdata);
                address += (ushort)wdata.Length;
                pos += wdata.Length;
                wlength -= wdata.Length;
            }
        }

        private void WriteCpuFlashBlock(ushort address, byte[] data)
        {
            int length = data.Length;
            var buffer = new byte[4 + length];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            Array.Copy(data, 0, buffer, 4, length);
            SendCommand(DumperCommand.FLASH_WRITE_REQUEST, buffer);
            (var command, _) = RecvCommand();
            if (command == DumperCommand.FLASH_WRITE_ERROR)
                throw new IOException($"Flash write error");
            else if (command == DumperCommand.FLASH_WRITE_TIMEOUT)
                throw new IOException($"Flash write timeout");
            else if (command != DumperCommand.PRG_WRITE_DONE)
                throw new IOException($"Invalid data received: {command}");
        }

        /// <summary>
        /// Erase UNROM512
        /// </summary>
        public void EraseUnrom512()
        {
            if (ProtocolVersion < 5)
                throw new NotSupportedException("Dumper firmware version is too old, update it to write UNROM-512 cartridges");
            SendCommand(DumperCommand.UNROM512_ERASE_REQUEST, Array.Empty<byte>());
            (var command, var data) = RecvCommand();
            if (command == DumperCommand.FLASH_ERASE_ERROR)
                throw new IOException($"Flash erase error (0x{data[0]:X2})");
            else if (command == DumperCommand.FLASH_ERASE_TIMEOUT)
                throw new TimeoutException($"Flash erase timeout");
            else if (command != DumperCommand.PRG_WRITE_DONE)
                throw new IOException($"Invalid data received: {command}");
        }

        /// <summary>
        /// Write UNROM512 flash memory
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        public void WriteUnrom512(uint address, byte[] data)
        {
            if (ProtocolVersion < 5)
                throw new NotSupportedException("Dumper firmware version is too old, update it to write UNROM-512 cartridges");
            int wlength = data.Length;
            int pos = 0;
            while (wlength > 0)
            {
                var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                Array.Copy(data, pos, wdata, 0, wdata.Length);
                if (data.Where(b => b != 0xFF).Any()) // if there is any not FF byte
                    WriteUnrom512Block(address, wdata);
                address += (ushort)wdata.Length;
                pos += wdata.Length;
                wlength -= wdata.Length;
            }
        }

        private void WriteUnrom512Block(uint address, byte[] data)
        {
            int length = data.Length;
            var buffer = new byte[6 + length];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)((address >> 16) & 0xFF);
            buffer[3] = (byte)((address >> 24) & 0xFF);
            buffer[4] = (byte)(length & 0xFF);
            buffer[5] = (byte)((length >> 8) & 0xFF);
            Array.Copy(data, 0, buffer, 6, length);
            SendCommand(DumperCommand.UNROM512_WRITE_REQUEST, buffer);
            (var command, _) = RecvCommand();
            if (command == DumperCommand.FLASH_WRITE_ERROR)
                throw new IOException($"Flash write error");
            else if (command == DumperCommand.FLASH_WRITE_TIMEOUT)
                throw new IOException($"Flash write timeout");
            else if (command != DumperCommand.PRG_WRITE_DONE)
                throw new IOException($"Invalid data received: {command}");
        }

        /// <summary>
        /// Read single byte from PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <returns>Data from PPU (CHR) bus</returns>
        public byte ReadPpu(ushort address) => ReadPpu(address, 1)[0];

        /// <summary>
        /// Read data from PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Data from PPU (CHR) bus</returns>
        public byte[] ReadPpu(ushort address, int length)
        {
            var result = new List<byte>();
            while (length > 0)
            {
                result.AddRange(ReadPpuBlock(address, Math.Min(MaxReadPacketSize, length)));
                address += MaxReadPacketSize;
                length -= MaxReadPacketSize;
            }
            return result.ToArray();
        }

        private byte[] ReadPpuBlock(ushort address, int length)
        {
            var buffer = new byte[4];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            SendCommand(DumperCommand.CHR_READ_REQUEST, buffer);
            (var command, var data) = RecvCommand();
            if (command != DumperCommand.CHR_READ_RESULT)
                throw new IOException($"Invalid data received: {command}");
            return data;
        }

        /// <summary>
        /// Read CRC16 checksum of data at PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Checksum</returns>
        public ushort ReadPpuCrc(ushort address, int length)
        {
            var buffer = new byte[4];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            SendCommand(DumperCommand.CHR_CRC_READ_REQUEST, buffer);
            (var command, var data) = RecvCommand();
            if (command != DumperCommand.CHR_READ_RESULT)
                throw new IOException($"Invalid data received: {command}");
            var crc = (ushort)(data[0] | (data[1] << 8));
            return crc;
        }

        /// <summary>
        /// Write data to PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        public void WritePpu(ushort address, params byte[] data)
        {
            int wlength = data.Length;
            int pos = 0;
            while (wlength > 0)
            {
                var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                Array.Copy(data, pos, wdata, 0, wdata.Length);
                WritePpuBlock(address, wdata);
                address += (ushort)wdata.Length;
                pos += wdata.Length;
                wlength -= wdata.Length;
            }
            return;
        }

        private void WritePpuBlock(ushort address, byte[] data)
        {
            int length = data.Length;
            var buffer = new byte[4 + length];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            Array.Copy(data, 0, buffer, 4, length);
            SendCommand(DumperCommand.CHR_WRITE_REQUEST, buffer);
            (var command, _) = RecvCommand();
            if (command != DumperCommand.CHR_WRITE_DONE)
                throw new IOException($"Invalid data received: {command}");
        }

        /// <summary>
        /// Read Famicom Disk System blocks
        /// </summary>
        /// <param name="startBlock">First block number to read (zero-based)</param>
        /// <param name="maxBlockCount">Maximum number of blocks to read</param>
        /// <returns>Array of Famicom Disk System blocks</returns>
        public (byte[] Data, bool CrcOk, bool EndOfHeadMeet)[] ReadFdsBlocks(byte startBlock = 0, byte maxBlockCount = byte.MaxValue)
        {
            if (ProtocolVersion < 3)
                throw new NotSupportedException("Dumper firmware version is too old, update it to read/write FDS cards");
            var blocks = new List<(byte[] Data, bool CrcOk, bool EndOfHeadMeet)>();
            var buffer = new byte[2];
            buffer[0] = startBlock;
            buffer[1] = maxBlockCount;
            SendCommand(DumperCommand.FDS_READ_REQUEST, buffer);
            bool receiving = true;
            int currentBlock = startBlock;
            while (receiving)
            {
                (var command, var data) = RecvCommand();
                switch (command)
                {
                    case DumperCommand.FDS_READ_RESULT_BLOCK:
                        {
                            blocks.Add((Data: data[0..^2], CrcOk: data[^2] != 0, EndOfHeadMeet: data[^1] != 0));
                            currentBlock++;
                        }
                        break;
                    case DumperCommand.FDS_READ_RESULT_END:
                        receiving = false;
                        break;
                    case DumperCommand.FDS_NOT_CONNECTED:
                        throw new IOException("RAM adapter IO error, is it connected?");
                    case DumperCommand.FDS_DISK_NOT_INSERTED:
                        throw new IOException("Disk card is not set");
                    case DumperCommand.FDS_BATTERY_LOW:
                        throw new IOException("Battery voltage is low or power supply is not connected");
                    case DumperCommand.FDS_TIMEOUT:
                        throw new IOException("FDS read timeout");
                    case DumperCommand.FDS_END_OF_HEAD:
                        throw new IOException("End of head");
                    case DumperCommand.FDS_BLOCK_CRC_ERROR:
                        throw new IOException("Block CRC error");
                    default:
                        throw new IOException($"Invalid data received: {command}");
                }
            }
            return blocks.ToArray();
        }

        /// <summary>
        /// Write blocks to Famicom Disk System card
        /// </summary>
        /// <param name="blockNumbers">Block numbers to write (zero-based)</param>
        /// <param name="blocks">Raw blocks data</param>
        public void WriteFdsBlocks(byte[] blockNumbers, params byte[][] blocks)
        {
            if (ProtocolVersion < 3)
                throw new NotSupportedException("Dumper firmware version is too old, update it to read/write FDS cards");
            if (blockNumbers.Length != blocks.Length)
                throw new ArgumentException("blockNumbers.Length != blocks.Length");
            var buffer = new byte[1 + blocks.Length + blocks.Length * 2 + blocks.Sum(e => e.Length)];
            buffer[0] = (byte)(blocks.Length);
            for (int i = 0; i < blocks.Length; i++)
            {
                buffer[1 + i] = blockNumbers[i];
            }
            for (int i = 0; i < blocks.Length; i++)
            {
                buffer[1 + blocks.Length + i * 2] = (byte)(blocks[i].Length & 0xFF);
                buffer[1 + blocks.Length + i * 2 + 1] = (byte)((blocks[i].Length >> 8) & 0xFF);
            }
            int pos = 1 + blocks.Length + blocks.Length * 2;
            foreach (var block in blocks)
            {
                Array.Copy(block, 0, buffer, pos, block.Length);
                pos += block.Length;
            }
            SendCommand(DumperCommand.FDS_WRITE_REQUEST, buffer);
            (var command, var data) = RecvCommand();
            switch (command)
            {
                case DumperCommand.FDS_WRITE_DONE:
                    return;
                case DumperCommand.FDS_NOT_CONNECTED:
                    throw new IOException("RAM adapter IO error, is it connected?");
                case DumperCommand.FDS_DISK_NOT_INSERTED:
                    throw new IOException("Disk card is not set");
                case DumperCommand.FDS_DISK_WRITE_PROTECTED:
                    throw new IOException("Disk card is write protected");
                case DumperCommand.FDS_BATTERY_LOW:
                    throw new IOException("Battery voltage is low or power supply is not connected");
                case DumperCommand.FDS_TIMEOUT:
                    throw new IOException("FDS read timeout");
                case DumperCommand.FDS_END_OF_HEAD:
                    throw new IOException("End of head");
                case DumperCommand.FDS_BLOCK_CRC_ERROR:
                    throw new IOException("Block CRC error");
                case DumperCommand.FDS_READ_RESULT_END:
                    throw new IOException("Unexpected end of data");
                default:
                    throw new IOException($"Invalid data received: {command}");
            }
        }

        /// <summary>
        /// Write single block to Famicom Disk System card
        /// </summary>
        /// <param name="blockNumbers">Block number to write (zero-based)</param>
        /// <param name="block">Raw block data</param>
        public void WriteFdsBlocks(byte blockNumber, byte[] block)
            => WriteFdsBlocks(new byte[] { blockNumber }, block);
 
        /// <summary>
        /// Read raw mirroring values (CIRAM A10 pin states for different states of PPU A10 and A11)
        /// </summary>
        /// <returns>Values of CIRAM A10 pin for $2000-$23FF, $2400-$27FF, $2800-$2BFF and $2C00-$2FFF</returns>
        public bool[] GetMirroringRaw()
        {
            SendCommand(DumperCommand.MIRRORING_REQUEST, Array.Empty<byte>());
            (var command, var data) = RecvCommand();
            if (command != DumperCommand.MIRRORING_RESULT)
                throw new IOException($"Invalid data received: {command}");
            var mirroringRaw = data;
            return mirroringRaw.Select(v => v != 0).ToArray();
        }

        /// <summary>
        /// Read decoded current mirroring mode
        /// </summary>
        /// <returns>Current mirroring</returns>
        public MirroringType GetMirroring()
        {
            var mirroringRaw = GetMirroringRaw();
            if (mirroringRaw.Length == 1)
            {
                // Backward compatibility with old firmwares
                return mirroringRaw[0] ? MirroringType.Vertical : MirroringType.Horizontal;
            }
            else if (mirroringRaw.Length == 4)
            {
                var mirrstr = $"{(mirroringRaw[0] ? 1 : 0)}{(mirroringRaw[1] ? 1 : 0)}{(mirroringRaw[2] ? 1 : 0)}{(mirroringRaw[3] ? 1 : 0)}";
                switch (mirrstr)
                {
                    case "0011":
                        return MirroringType.Horizontal; // Horizontal
                    case "0101":
                        return MirroringType.Vertical; // Vertical
                    case "0000":
                        return MirroringType.OneScreenA; // One-screen A
                    case "1111":
                        return MirroringType.OneScreenB; // One-screen B
                }
            }
            return MirroringType.Unknown; // Unknown
        }

        /// <summary>
        /// Set maximum number of bytes in multi-byte flash program
        /// </summary>
        /// <param name="pageSize"></param>
        public void SetMaximumNumberOfBytesInMultiProgram(uint pageSize)
        {
            if (ProtocolVersion < 3)
                throw new NotSupportedException("Dumper firmware version is too old");
            var buffer = new byte[2];
            buffer[0] = (byte)(pageSize & 0xFF);
            buffer[1] = (byte)((pageSize >> 8) & 0xFF);
            SendCommand(DumperCommand.SET_FLASH_BUFFER_SIZE, buffer);
            (var command, _) = RecvCommand();
            if (command != DumperCommand.SET_VALUE_DONE)
                throw new IOException($"Invalid data received: {command}");
        }

        public void SetCoolboyGpioMode(bool coolboyGpioMode)
        {
            if (ProtocolVersion < 4)
            {
                if (coolboyGpioMode)
                    throw new NotSupportedException("Dumper firmware version is too old");
                else
                    return;
            }
            if ((HardwareVersion == null) || (HardwareVersion.Major < 3))
            {
                if (coolboyGpioMode)
                    throw new NotSupportedException("Not supported by this dumper hardware");
                else
                    return;
            }
            SendCommand(DumperCommand.COOLBOY_GPIO_MODE, new byte[] { (byte)(coolboyGpioMode ? 1 : 0) });
            (var command, _) = RecvCommand();
            if (command != DumperCommand.SET_VALUE_DONE)
                throw new IOException($"Invalid data received: {command}");
        }

        public void Dispose()
        {
            Close();
        }
    }
}
