﻿using com.clusterrr.Famicom.Containers;
using Google.Protobuf;
using Grpc.Net.Client;
using RemoteDumper;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace com.clusterrr.Famicom.DumperConnection
{
    public class FamicomDumperClient : IDisposable, IFamicomDumperConnectionExt
    {
        private readonly Dumper.DumperClient client;
        private readonly GrpcChannel channel;

        public FamicomDumperClient(string url)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var httpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            channel = GrpcChannel.ForAddress(url,
                new GrpcChannelOptions { HttpHandler = httpHandler });
            client = new Dumper.DumperClient(channel);
        }

        public void Dispose()
        {
            if (channel != null)
                channel.Dispose();
            GC.SuppressFinalize(this);
        }

        private static void ThrowIfNotSuccess(ErrorInfo errorInfo)
        {
            if (errorInfo == null) return;
            var enBase = Regex.Replace(errorInfo.ExceptionName, @".*\.", "");
            throw enBase switch
            {
                nameof(IOException) => new IOException(errorInfo.ExceptionMessage),
                nameof(TimeoutException) => new TimeoutException(errorInfo.ExceptionMessage),
                nameof(InvalidDataException) => new InvalidDataException(errorInfo.ExceptionMessage),
                nameof(NotSupportedException) => new NotSupportedException(errorInfo.ExceptionMessage),
                nameof(ArgumentException) => new ArgumentException(errorInfo.ExceptionMessage),
                nameof(InvalidOperationException) => new InvalidOperationException(errorInfo.ExceptionMessage),
                _ => new Exception($"{errorInfo.ExceptionName}: {errorInfo.ExceptionMessage}"),
            };
        }

        /// <summary>
        /// Init dumper (flush queud data, check connection)
        /// </summary>
        /// <returns></returns>
        public bool Init()
        {
            var r = client.Init(new EmptyRequest());
            return r.Success;
        }

        /// <summary>
        /// Famicom Dumper serial protocol version (depends on firmware version)
        /// </summary>
        public byte ProtocolVersion
        {
            get
            {
                var r = client.GetProtocolVersion(new EmptyRequest());
                ThrowIfNotSuccess(r.ErrorInfo);
                return (byte)r.ProtocolVersion;
            }
        }

        public Version FirmwareVersion
        {
            get
            {
                var r = client.GetFirmwareVersion(new EmptyRequest());
                ThrowIfNotSuccess(r.ErrorInfo);
                return new Version(r.Major, r.Minor);
            }
        }

        public Version HardwareVersion
        {
            get
            {
                var r = client.GetHardwareVersion(new EmptyRequest());
                ThrowIfNotSuccess(r.ErrorInfo);
                return new Version(r.Major, r.Minor);
            }
        }

        /// <summary>
        /// Famicom Dumper maximum read packet size (depends on firmware and hardware)
        /// </summary>
        public ushort MaxReadPacketSize
        {
            get
            {
                var r = client.GetMaxReadPacketSize(new EmptyRequest());
                ThrowIfNotSuccess(r.ErrorInfo);
                return (ushort)r.MaxPacketSize;
            }
        }

        /// <summary>
        /// Famicom Dumper maximum write packet size (depends on firmware and hardware)
        /// </summary>
        public ushort MaxWritePacketSize
        {
            get
            {
                var r = client.GetMaxWritePacketSize(new EmptyRequest());
                ThrowIfNotSuccess(r.ErrorInfo);
                return (ushort)r.MaxPacketSize;
            }
        }

        /// <summary>
        /// Timeout for all read/write operations (in milliseconds)
        /// </summary>
        public uint Timeout
        {
            get
            {
                var r = client.GetTimeout(new EmptyRequest());
                ThrowIfNotSuccess(r.ErrorInfo);
                return (uint)r.Timeout;
            }
            set
            {
                var r = client.SetTimeout(new SetTimeoutRequest() { Timeout = (int)value });
                ThrowIfNotSuccess(r.ErrorInfo);
            }
        }

        /// <summary>
        /// Simulate reset (M2 goes to Z-state for a second)
        /// </summary>
        public void Reset()
        {
            var r = client.Reset(new EmptyRequest());
            ThrowIfNotSuccess(r.ErrorInfo);
        }

        /// <summary>
        /// Read single byte from CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <returns>Data from CPU (PRG) bus</returns>
        public byte ReadCpu(ushort address)
        {
            var r = client.ReadCpu(new ReadRequest()
            {
                Address = address
            });
            ThrowIfNotSuccess(r.ErrorInfo);
            return r.Data.ToByteArray()[0];
        }

        /// <summary>
        /// Read data from CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Data from CPU (PRG) bus</returns>
        public byte[] ReadCpu(ushort address, int length)
        {
            var r = client.ReadCpu(new ReadRequest()
            {
                Address = address,
                Length = length
            });
            ThrowIfNotSuccess(r.ErrorInfo);
            return r.Data.ToByteArray();
        }

        /// <summary>
        /// Read single byte from PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <returns>Data from PPU (CHR) bus</returns>
        public byte ReadPpu(ushort address)
        {
            var r = client.ReadPpu(new ReadRequest()
            {
                Address = address
            });
            ThrowIfNotSuccess(r.ErrorInfo);
            return r.Data.ToByteArray()[0];
        }

        /// <summary>
        /// Read data from PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Data from PPU (CHR) bus</returns>
        public byte[] ReadPpu(ushort address, int length)
        {
            var r = client.ReadPpu(new ReadRequest()
            {
                Address = address,
                Length = length
            });
            ThrowIfNotSuccess(r.ErrorInfo);
            return r.Data.ToByteArray();
        }

        /// <summary>
        /// Write data to CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        public void WriteCpu(ushort address, params byte[] data)
        {
            var r = client.WriteCpu(new WriteRequest()
            {
                Address = address,
                Data = ByteString.CopyFrom(data)
            });
            ThrowIfNotSuccess(r.ErrorInfo);
        }

        /// <summary>
        /// Write data to PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        public void WritePpu(ushort address, params byte[] data)
        {
            var r = client.WritePpu(new WriteRequest()
            {
                Address = address,
                Data = ByteString.CopyFrom(data)
            });
            ThrowIfNotSuccess(r.ErrorInfo);
        }

        /// <summary>
        /// Read CRC16 checksum of data at CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Checksum</returns>
        public ushort ReadCpuCrc(ushort address, int length)
        {
            var r = client.ReadCpuCrc(new ReadRequest()
            {
                Address = address,
                Length = length
            });
            ThrowIfNotSuccess(r.ErrorInfo);
            return (ushort)r.Crc;
        }

        /// <summary>
        /// Read CRC16 checksum of data at PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Checksum</returns>
        public ushort ReadPpuCrc(ushort address, int length)
        {
            var r = client.ReadPpuCrc(new ReadRequest()
            {
                Address = address,
                Length = length
            });
            ThrowIfNotSuccess(r.ErrorInfo);
            return (ushort)r.Crc;
        }

        /// <summary>
        /// Erase current flash sector
        /// </summary>
        public void EraseFlashSector()
        {
            var r = client.EraseFlashSector(new EmptyRequest());
            ThrowIfNotSuccess(r.ErrorInfo);
        }

        /// <summary>
        /// Write flash
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        public void WriteFlash(ushort address, byte[] data)
        {
            var r = client.WriteFlash(new WriteRequest()
            {
                Address = address,
                Data = ByteString.CopyFrom(data)
            });
            ThrowIfNotSuccess(r.ErrorInfo);
        }

        /// <summary>
        /// Erase UNROM512
        /// </summary>
        public void EraseUnrom512()
        {
            var r = client.EraseUnrom512(new EmptyRequest());
            ThrowIfNotSuccess(r.ErrorInfo);
        }

        /// <summary>
        /// Write UNROM512 flash memory
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        public void WriteUnrom512(uint address, byte[] data)
        {
            var r = client.WriteUnrom512(new WriteRequest()
            {
                Address = address,
                Data = ByteString.CopyFrom(data)
            });
        }

        /// <summary>
        /// Read Famicom Disk System blocks
        /// </summary>
        /// <param name="startBlock">First block number to read (zero-based)</param>
        /// <param name="maxBlockCount">Maximum number of blocks to read</param>
        /// <returns>Array of Famicom Disk System blocks</returns>
        public (byte[] Data, bool CrcOk, bool EndOfHeadMeet)[] ReadFdsBlocks(byte startBlock = 0, byte maxBlockCount = byte.MaxValue)
        {
            var r = client.ReadFdsBlocks(new ReadFdsRequest()
            {
                StartBlock = startBlock,
                MaxBlockCount = maxBlockCount
            });
            ThrowIfNotSuccess(r.ErrorInfo);
            return r.FdsBlocks.Select(block => (block.BlockData.ToByteArray(), block.CrcOk, block.EndOfHeadMeet)).ToArray();
        }

        /// <summary>
        /// Write blocks to Famicom Disk System card
        /// </summary>
        /// <param name="blockNumbers">Block numbers to write (zero-based)</param>
        /// <param name="blocks">Raw blocks data</param>
        public void WriteFdsBlocks(byte[] blockNumbers, byte[][] blocks)
        {
            var request = new WriteFdsRequest();
            request.BlockNumbers.AddRange(blockNumbers.Select(b => (uint)b));
            request.BlocksData.AddRange(blocks.Select(block => ByteString.CopyFrom(block)));
            var r = client.WriteFdsBlocks(request);
            ThrowIfNotSuccess(r.ErrorInfo);
        }

        /// <summary>
        /// Write single block to Famicom Disk System card
        /// </summary>
        /// <param name="blockNumbers">Block numbers to write (zero-based)</param>
        /// <param name="block">Raw block data</param>
        public void WriteFdsBlocks(byte blockNumber, byte[] block)
        {
            var request = new WriteFdsRequest();
            request.BlockNumbers.Add(blockNumber);
            request.BlocksData.Add(ByteString.CopyFrom(block));
            var r = client.WriteFdsBlocks(request);
            ThrowIfNotSuccess(r.ErrorInfo);
        }

        /// <summary>
        /// Read raw mirroring values (CIRAM A10 pin states for different states of PPU A10 and A11)
        /// </summary>
        /// <returns>Values of CIRAM A10 pin for $2000-$23FF, $2400-$27FF, $2800-$2BFF and $2C00-$2FFF</returns>
        public bool[] GetMirroringRaw()
        {
            var r = client.GetMirroringRaw(new EmptyRequest());
            ThrowIfNotSuccess(r.ErrorInfo);
            return r.Mirroring.ToArray();
        }

        /// <summary>
        /// Read decoded current mirroring mode
        /// </summary>
        /// <returns>Current mirroring</returns>
        public MirroringType GetMirroring()
        {
            var r = client.GetMirroring(new EmptyRequest());
            ThrowIfNotSuccess(r.ErrorInfo);
            return (MirroringType)r.Mirroring;
        }

        /// <summary>
        /// Set maximum number of bytes in multi-byte flash program
        /// </summary>
        /// <param name="pageSize"></param>
        public void SetMaximumNumberOfBytesInMultiProgram(uint pageSize)
        {
            var r = client.SetMaximumNumberOfBytesInMultiProgram(new SetMaximumNumberOfBytesInMultiProgramRequest() { PageSize = (int)pageSize });
            ThrowIfNotSuccess(r.ErrorInfo);
        }

        public void SetCoolboyGpioMode(bool coolboyGpioMode)
        {
            var r = client.SetCoolboyGpioMode(new CoolboyGpioModeRequest()
            {
                CoolboyGpioMode = coolboyGpioMode
            });
            ThrowIfNotSuccess(r.ErrorInfo);
        }
    }
}
