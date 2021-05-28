using com.clusterrr.Famicom.Containers;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
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
        private readonly IFamicomDumperConnection dumper;

        public FamicomDumperService(IFamicomDumperConnection dumper)
        {
            this.dumper = dumper;
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
                dumper.Reset();
            }
            catch (Exception ex)
            {
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
                byte[] data;
                if (request.HasLength)
                    data = dumper.ReadCpu((ushort)request.Address, (ushort)request.Length);
                else
                    data = new byte[] { dumper.ReadCpu((ushort)request.Address) };
                result.Data = ByteString.CopyFrom(data);
            }
            catch (Exception ex)
            {
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
                byte[] data;
                if (request.HasLength)
                    data = dumper.ReadPpu((ushort)request.Address, (ushort)request.Length);
                else
                    data = new byte[] { dumper.ReadPpu((ushort)request.Address) };
                result.Data = ByteString.CopyFrom(data);
            }
            catch (Exception ex)
            {
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
                dumper.WriteCpu((ushort)request.Address, request.Data.ToByteArray());
            }
            catch (Exception ex)
            {
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
                dumper.WritePpu((ushort)request.Address, request.Data.ToByteArray());
            }
            catch (Exception ex)
            {
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
                result.Crc = dumper.ReadCpuCrc((ushort)request.Address, (ushort)request.Length);
            }
            catch (Exception ex)
            {
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
                result.Crc = dumper.ReadPpuCrc((ushort)request.Address, (ushort)request.Length);
            }
            catch (Exception ex)
            {
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
                dumper.WriteFlash((ushort)request.Address, request.Data.ToByteArray());
            }
            catch (Exception ex)
            {
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
                IFdsBlock[] blocks;
                if (request.HasMaxBlockCount)
                    blocks = dumper.ReadFdsBlocks((byte)request.StartBlock, (byte)request.MaxBlockCount);
                else
                    blocks = dumper.ReadFdsBlocks((byte)request.StartBlock);
                var blocksRaw = blocks.Select(block => ByteString.CopyFrom(block.ToBytes()));
                result.FdsBlocks.AddRange(blocksRaw);
            }
            catch (Exception ex)
            {
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
                dumper.WriteFdsBlocks(
                    request.BlockNumbers.Select(b => (byte)b).ToArray(),
                    request.FdsBlocks.Select(block => block.ToByteArray()).ToArray());
            }
            catch (Exception ex)
            {
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
                result.Mirroring.AddRange(dumper.GetMirroringRaw());
            }
            catch (Exception ex)
            {
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
                result.Mirroring = (MirroringResponse.Types.Mirroring)dumper.GetMirroring();
            }
            catch (Exception ex)
            {
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
                dumper.SetMaximumNumberOfBytesInMultiProgram((uint)request.PageSize);
            }
            catch (Exception ex)
            {
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
                dumper.EraseFlashSector();
            }
            catch (Exception ex)
            {
                result.ErrorInfo = new ErrorInfo()
                {
                    ExceptionName = ex.GetType().ToString(),
                    ExceptionMessage = ex.Message
                };
            }
            return Task.FromResult(result);
        }
    }
}
