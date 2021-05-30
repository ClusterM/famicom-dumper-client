using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RemoteDumper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.clusterrr.Famicom.DumperConnection
{
    public class FamicomDumperService : Dumper.DumperBase
    {
        private static IFamicomDumperConnectionExt dumper;

        public static void StartServer(IFamicomDumperConnectionExt dumper, string url)
        {
            FamicomDumperService.dumper = dumper;
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
#if !DEBUG
                    webBuilder.ConfigureLogging((context, logging) => logging.ClearProviders());
#endif
                    webBuilder.UseUrls(url);
                    webBuilder.ConfigureKestrel((options) =>
                    {
                        options.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http2);
                    });
                    webBuilder.UseStartup<GrpcStartup>();
                })
                .Build()
                .Run();
        }

        public override Task<InitResponse> Init(EmptyRequest request, ServerCallContext context)
        {
            Console.Write("Dumper initialization... ");
            bool initResult = dumper.Init();
            Console.WriteLine(initResult ? "OK" : "faied");
            return Task.FromResult(new InitResponse() { Success = initResult });
        }

        public override Task<ProtocolVersionResponse> GetProtocolVersion(EmptyRequest request, ServerCallContext context)
        {
            var result = new ProtocolVersionResponse();
            try
            {
                result.ProtocolVersion = dumper.ProtocolVersion;
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<PacketSizeResponse> GetMaxReadPacketSize(EmptyRequest request, ServerCallContext context)
        {
            var result = new PacketSizeResponse();
            try
            {
                result.MaxPacketSize = dumper.MaxReadPacketSize;
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<PacketSizeResponse> GetMaxWritePacketSize(EmptyRequest request, ServerCallContext context)
        {
            var result = new PacketSizeResponse();
            try
            {
                result.MaxPacketSize = dumper.MaxWritePacketSize;
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<GetTimeoutResponse> GetTimeout(EmptyRequest request, ServerCallContext context)
        {
            var result = new GetTimeoutResponse();
            try
            {
                result.Timeout = (int)dumper.Timeout;
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<EmptyResponse> SetTimeout(SetTimeoutRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();
            try
            {
                dumper.Timeout = (uint)request.Timeout;
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<EmptyResponse> Reset(EmptyRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();
            try
            {
                Console.Write("Reset... ");
                dumper.Reset();
                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<ReadResponse> ReadCpu(ReadRequest request, ServerCallContext context)
        {
            var result = new ReadResponse();
            try
            {
                if (request.Length > 1)
                    Console.Write($"Reading 0x{request.Address:X4}-0x{request.Address + request.Length - 1:X4} @ CPU... ");
                else
                    Console.Write($"Reading 0x{request.Address:X4} @ CPU... ");
                byte[] data;
                if (request.HasLength)
                    data = dumper.ReadCpu((ushort)request.Address, (ushort)request.Length);
                else
                    data = new byte[] { dumper.ReadCpu((ushort)request.Address) };
                if (data.Length <= 32)
                {
                    foreach (var b in data)
                        Console.Write($"{b:X2} ");
                    Console.WriteLine();
                }
                else Console.WriteLine("OK");
                result.Data = ByteString.CopyFrom(data);
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<ReadResponse> ReadPpu(ReadRequest request, ServerCallContext context)
        {
            var result = new ReadResponse();
            try
            {
                if (request.Length > 1)
                    Console.Write($"Reading 0x{request.Address:X4}-0x{request.Address + request.Length - 1:X4} @ PPU... ");
                else
                    Console.Write($"Reading 0x{request.Address:X4} @ PPU... ");
                byte[] data;
                if (request.HasLength)
                    data = dumper.ReadPpu((ushort)request.Address, (ushort)request.Length);
                else
                    data = new byte[] { dumper.ReadPpu((ushort)request.Address) };
                if (data.Length <= 32)
                {
                    foreach (var b in data)
                        Console.Write($"{b:X2} ");
                    Console.WriteLine();
                }
                else Console.WriteLine("OK");
                result.Data = ByteString.CopyFrom(data);
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<EmptyResponse> WriteCpu(WriteRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();
            try
            {
                if (request.Data.Length <= 32)
                {
                    Console.Write($"Writing ");
                    foreach (var b in request.Data)
                        Console.Write($"0x{b:X2} ");
                    if (request.Data.Length > 1)
                        Console.Write($"=> 0x{request.Address:X4}-0x{request.Address + request.Data.Length - 1:X4} @ CPU... ");
                    else
                        Console.Write($"=> 0x{request.Address:X4} @ CPU... ");
                }
                else
                {
                    Console.Write($"Writing to 0x{request.Address:X4}-0x{request.Address + request.Data.Length - 1:X4} @ CPU... ");
                }
                dumper.WriteCpu((ushort)request.Address, request.Data.ToByteArray());
                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<EmptyResponse> WritePpu(WriteRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();
            try
            {
                if (request.Data.Length <= 32)
                {
                    Console.Write($"Writing ");
                    foreach (var b in request.Data)
                        Console.Write($"0x{b:X2} ");
                    if (request.Data.Length > 1)
                        Console.Write($"=> 0x{request.Address:X4}-0x{request.Address + request.Data.Length - 1:X4} @ PPU... ");
                    else
                        Console.Write($"=> 0x{request.Address:X4} @ PPU... ");
                }
                else
                {
                    Console.Write($"Writing to 0x{request.Address:X4}-0x{request.Address + request.Data.Length - 1:X4} @ PPU... ");
                }
                dumper.WritePpu((ushort)request.Address, request.Data.ToByteArray());
                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<ReadCrcResponse> ReadCpuCrc(ReadRequest request, ServerCallContext context)
        {
            var result = new ReadCrcResponse();
            try
            {
                if (request.Length > 1)
                    Console.Write($"Reading CRC of 0x{request.Address:X4}-0x{request.Address + request.Length - 1:X4} @ CPU... ");
                else
                    Console.Write($"Reading CRC of 0x{request.Address:X4} @ CPU... ");
                result.Crc = dumper.ReadCpuCrc((ushort)request.Address, (ushort)request.Length);
                Console.WriteLine($"{result.Crc:X4}");
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<ReadCrcResponse> ReadPpuCrc(ReadRequest request, ServerCallContext context)
        {
            var result = new ReadCrcResponse();
            try
            {
                if (request.Length > 1)
                    Console.Write($"Reading CRC of 0x{request.Address:X4}-0x{request.Address + request.Length - 1:X4} @ PPU... ");
                else
                    Console.Write($"Reading CRC of 0x{request.Address:X4} @ PPU... ");
                result.Crc = dumper.ReadPpuCrc((ushort)request.Address, (ushort)request.Length);
                Console.WriteLine($"{result.Crc:X4}");
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<EmptyResponse> WriteFlash(WriteRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();
            try
            {
                if (request.Data.Length <= 32)
                {
                    Console.Write($"Writing ");
                    foreach (var b in request.Data)
                        Console.Write($"0x{b:X2} ");
                    if (request.Data.Length > 1)
                        Console.Write($"=> 0x{request.Address:X4}-0x{request.Address + request.Data.Length - 1:X4} @ flash... ");
                    else
                        Console.Write($"=> 0x{request.Address:X4} @ flash... ");
                }
                else
                {
                    Console.Write($"Writing to 0x{request.Address:X4}-0x{request.Address + request.Data.Length - 1:X4} @ flash... ");
                }
                dumper.WriteFlash((ushort)request.Address, request.Data.ToByteArray());
                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<ReadFdsResponse> ReadFdsBlocks(ReadFdsRequest request, ServerCallContext context)
        {
            var result = new ReadFdsResponse();
            try
            {
                Console.Write($"Reading FDS block(s) {request.StartBlock}-{((request.MaxBlockCount < byte.MaxValue) ? $"{request.StartBlock + request.MaxBlockCount - 1}" : "*")}... ");
                (byte[] Data, bool CrcOk, bool EndOfHeadMeet)[] blocks;
                if (request.HasMaxBlockCount)
                    blocks = dumper.ReadFdsBlocks((byte)request.StartBlock, (byte)request.MaxBlockCount);
                else
                    blocks = dumper.ReadFdsBlocks((byte)request.StartBlock);
                result.FdsBlocks.AddRange(blocks.Select(block => new ReceivedFdsBlock()
                {
                    BlockData = ByteString.CopyFrom(block.Data),
                    CrcOk = block.CrcOk,
                    EndOfHeadMeet = block.EndOfHeadMeet
                }));
                Console.WriteLine($"received {blocks.Length} blocks");
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<EmptyResponse> WriteFdsBlocks(WriteFdsRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();
            try
            {
                Console.Write($"Writing FDS block(s) {string.Join(", ", request.BlockNumbers)}... ");
                dumper.WriteFdsBlocks(
                    request.BlockNumbers.Select(b => (byte)b).ToArray(),
                    request.BlocksData.Select(block => block.ToByteArray()).ToArray());
                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<MirroringRawResponse> GetMirroringRaw(EmptyRequest request, ServerCallContext context)
        {
            var result = new MirroringRawResponse();
            try
            {
                Console.Write("Reading mirroring... ");
                result.Mirroring.AddRange(dumper.GetMirroringRaw());
                foreach (var b in result.Mirroring)
                    Console.Write($"{b} ");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<MirroringResponse> GetMirroring(EmptyRequest request, ServerCallContext context)
        {
            var result = new MirroringResponse();
            try
            {
                Console.Write("Reading mirroring... ");
                var mirroring = dumper.GetMirroring();
                Console.WriteLine(mirroring);
                result.Mirroring = (MirroringResponse.Types.Mirroring)mirroring;
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<EmptyResponse> SetMaximumNumberOfBytesInMultiProgram(SetMaximumNumberOfBytesInMultiProgramRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();
            try
            {
                Console.Write($"Setting maximum number of bytes in multi program mode to {request.PageSize}... ");
                dumper.SetMaximumNumberOfBytesInMultiProgram((uint)request.PageSize);
                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        public override Task<EmptyResponse> EraseFlashSector(EmptyRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();
            try
            {
                Console.Write("Erasing flash sector... ");
                dumper.EraseFlashSector();
                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                PrintError(ex);
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }

        private static void PrintError(Exception ex)
        {
            Console.WriteLine($"ERROR {ex.GetType()}: " + ex.Message
#if DEBUG
                    + ex.StackTrace
#endif
                    );
        }
    }
}
