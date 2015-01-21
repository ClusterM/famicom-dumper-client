using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace Cluster.Famicom
{
    public class FamicomDumperConnection
    {
        const int PortBaudRate = 250000;
        const int MaxReadPacketSize = 2048;
        const int MaxWritePacketSize = 512;

        public string PortName { get; set; }
        public bool Blocking { get; set; }
        SerialPort port = null;
        Thread readingThread;

        int comm_recv_pos;
        byte comm_recv_command;
        byte comm_recv_crc;
        bool comm_recv_error;

        int comm_recv_length;
        List<byte> recv_buffer = new List<byte>();

        volatile bool prgInitOk = false;
        volatile bool chrInitOk = false;
        volatile bool prgReadDone = false;
        volatile bool prgWriteDone = false;
        volatile bool chrReadDone = false;
        volatile bool chrWriteDone = false;
        volatile byte[] prgRecvData, chrRecvData;
        volatile int mirroring = -1;
        volatile bool resetAck = false;

        public delegate void OnReadResultDelegate(byte[] data);
        public delegate void OnWriteDoneDelegate();
        public delegate void OnMirroringDelegate(int mirroring);
        public delegate void OnResetAckDelegate();
        public delegate void OnErrorDelegate();
        public event OnReadResultDelegate OnPrgReadResult;
        public event OnWriteDoneDelegate OnPrgWriteDone;
        public event OnReadResultDelegate OnChrReadResult;
        public event OnWriteDoneDelegate OnChrWriteDone;
        public event OnMirroringDelegate OnMirroring;
        public event OnErrorDelegate OnError;
        public event OnResetAckDelegate OnResetAck;

        public int Timeout { get; set; }

        enum Command
        {
            COMMAND_PRG_STARTED = 0,
            COMMAND_CHR_STARTED = 1,
            COMMAND_ERROR_INVALID = 2,
            COMMAND_ERROR_CRC = 3,
            COMMAND_ERROR_OVERFLOW = 4,
            COMMAND_PRG_INIT = 5,
            COMMAND_CHR_INIT = 6,
            COMMAND_PRG_READ_REQUEST = 7,
            COMMAND_PRG_READ_RESULT = 8,
            COMMAND_PRG_WRITE_REQUEST = 9,
            COMMAND_PRG_WRITE_DONE = 10,
            COMMAND_CHR_READ_REQUEST = 11,
            COMMAND_CHR_READ_RESULT = 12,
            COMMAND_CHR_WRITE_REQUEST = 13,
            COMMAND_CHR_WRITE_DONE = 14,
            COMMAND_PHI2_INIT = 15,
            COMMAND_PHI2_INIT_DONE = 16,
            COMMAND_MIRRORING_REQUEST = 17,
            COMMAND_MIRRORING_RESULT = 18,
            COMMAND_RESET = 19,
            COMMAND_RESET_ACK = 20,
            COMMAND_PRG_EPROM_WRITE_REQUEST = 21,
            COMMAND_CHR_EPROM_WRITE_REQUEST = 22,
            COMMAND_EPROM_PREPARE = 23,
            COMMAND_PRG_FLASH_ERASE_REQUEST = 24,
            COMMAND_PRG_FLASH_WRITE_REQUEST = 25,
            COMMAND_CHR_FLASH_ERASE_REQUEST = 26,
            COMMAND_CHR_FLASH_WRITE_REQUEST = 27
        }

        public FamicomDumperConnection(string portName = null)
        {
            this.PortName = portName;
            OnPrgReadResult += FamicomDumperConnection_OnPrgReadResult;
            OnPrgWriteDone += FamicomDumperConnection_OnPrgWriteDone;
            OnChrReadResult += FamicomDumperConnection_OnChrReadResult;
            OnChrWriteDone += FamicomDumperConnection_OnChrWriteDone;
            OnMirroring += FamicomDumperConnection_OnMirroring;
            OnResetAck += FamicomDumperConnection_OnResetAck;
            Timeout = 10000;
            Blocking = true;
        }


        public void Open()
        {
            SerialPort sPort;
            sPort = new SerialPort();
            sPort.PortName = PortName;
            sPort.WriteTimeout = 5000; sPort.ReadTimeout = 300000;
            sPort.BaudRate = PortBaudRate;
            sPort.Parity = Parity.None;
            sPort.DataBits = 8;
            sPort.StopBits = StopBits.One;
            sPort.Handshake = Handshake.None;
            sPort.DtrEnable = false;
            sPort.RtsEnable = false;
            sPort.NewLine = System.Environment.NewLine;
            sPort.Open();
            port = sPort;

            if (readingThread != null)
                readingThread.Abort();
            readingThread = new Thread(readThread);
            readingThread.Start();

            prgInitOk = chrInitOk = false;
        }

        public void Close()
        {
            if (port != null)
            {
                port.Close();
                port = null;
            }
            if (readingThread != null)
            {
                readingThread.Abort();
                readingThread = null;
            }
        }

        void readThread()
        {
            try
            {
                while (port != null)
                {
                    try
                    {
                        int c = port.ReadByte();
                        if (c >= 0)
                        {
#if DEBUG
                            Console.Write("{0:X2} ", c);
#endif
                            recvProceed((byte)c);
                        }
                    }
                    catch (TimeoutException) { }
                }
            }
            catch (ThreadAbortException) { }
            catch (IOException)
            {
                if (port == null || !port.IsOpen) return;
                Close();
                //Console.WriteLine("Port closed: " + ex.Message);
            }
            finally
            {
                readingThread = null;
            }
        }

        void recvInit()
        {
            comm_recv_pos = 0;
            comm_recv_error = false;
        }

        void comm_calc_recv_crc(byte inbyte)
        {
            int j;
            for (j = 0; j < 8; j++)
            {
                byte mix = (byte)((comm_recv_crc ^ inbyte) & 0x01);
                comm_recv_crc >>= 1;
                if (mix != 0)
                    comm_recv_crc ^= 0x8C;
                inbyte >>= 1;
            }
        }

        void recvProceed(byte data)
        {
            if (comm_recv_error && data != 0x46) return;
            comm_recv_error = false;
            if (comm_recv_pos == 0)
            {
                comm_recv_crc = 0;
                recv_buffer.Clear();
            }

            comm_calc_recv_crc(data);
            int l = comm_recv_pos - 4;
            switch (comm_recv_pos)
            {
                case 0:
                    if (data != 0x46)
                    {
                        //comm_start(COMMAND_ERROR_INVALID, 0);
                        if (OnError != null)
                            OnError();
                    }
                    break;
                case 1:
                    comm_recv_command = data;
                    break;
                case 2:
                    comm_recv_length = data;
                    break;
                case 3:
                    comm_recv_length |= data << 8;
                    break;
                default:
                    if (l < comm_recv_length)
                    {
                        recv_buffer.Add(data);
                    }
                    else if (l == comm_recv_length)
                    {
                        if (comm_recv_crc == 0)
                        {
                            dataReceived((Command)comm_recv_command, recv_buffer.ToArray());
                        }
                        else
                        {
                            comm_recv_error = true;
                            if (OnError != null)
                                OnError();
                            //comm_start(COMMAND_ERROR_CRC, 0);
                        }
                        comm_recv_pos = 0;
                        return;
                    }
                    break;
            }
            comm_recv_pos++;
        }

        void dataReceived(Command command, byte[] data)
        {
#if DEBUG
            Console.WriteLine("Received command: " + command);
#endif
            switch (command)
            {
                case Command.COMMAND_PRG_STARTED:
                    prgInitOk = true;
                    break;
                case Command.COMMAND_CHR_STARTED:
                    chrInitOk = true;
                    break;
                case Command.COMMAND_PRG_READ_RESULT:
                    if (OnPrgReadResult != null)
                        OnPrgReadResult(data);
                    break;
                case Command.COMMAND_PRG_WRITE_DONE:
                    if (OnPrgWriteDone != null)
                        OnPrgWriteDone();
                    break;
                case Command.COMMAND_CHR_READ_RESULT:
                    if (OnChrReadResult != null)
                        OnChrReadResult(data);
                    break;
                case Command.COMMAND_CHR_WRITE_DONE:
                    if (OnChrWriteDone != null)
                        OnChrWriteDone();
                    break;
                case Command.COMMAND_MIRRORING_RESULT:
                    if (OnMirroring != null)
                        OnMirroring(data[0]);
                    break;
                case Command.COMMAND_RESET_ACK:
                    if (OnResetAck != null)
                        OnResetAck();
                    break;
            }
        }

        void sendData(Command command, byte[] data)
        {
            byte[] buffer = new byte[data.Length + 5];
            buffer[0] = 0x46;
            buffer[1] = (byte)command;
            buffer[2] = (byte)(data.Length & 0xFF);
            buffer[3] = (byte)((data.Length >> 8) & 0xFF);
            Array.Copy(data, 0, buffer, 4, data.Length);

            byte crc = 0;
            for (var i = 0; i < buffer.Length - 1; i++)
            {
                byte inbyte = buffer[i];
                for (int j = 0; j < 8; j++)
                {
                    byte mix = (byte)((crc ^ inbyte) & 0x01);
                    crc >>= 1;
                    if (mix != 0)
                        crc ^= 0x8C;
                    inbyte >>= 1;
                }
            }
            buffer[buffer.Length - 1] = crc;
            port.Write(buffer, 0, buffer.Length);
        }

        public bool PrgReaderInit()
        {
            prgInitOk = false;
            for (int i = 0; i < 3; i++)
            {
                sendData(Command.COMMAND_PRG_INIT, new byte[0]);
                for (int t = 0; t < 10; t++)
                {
                    Thread.Sleep(100);
                    if (prgInitOk) return true;
                }
            }
            return false;
        }


        public bool ChrReaderInit()
        {
            chrInitOk = false;
            for (int i = 0; i < 3; i++)
            {
                sendData(Command.COMMAND_CHR_INIT, new byte[0]);
                for (int t = 0; t < 10; t++)
                {
                    Thread.Sleep(100);
                    if (chrInitOk) return true;
                }
            }
            return false;
        }

        public byte[] ReadPrg(UInt16 address, int length)
        {
            if (length > MaxReadPacketSize) // Split packets;
            {
                var result = new List<byte>();
                while (length > 0)
                {
                    result.AddRange(ReadPrg(address, Math.Min(MaxReadPacketSize, length)));
                    address += MaxReadPacketSize;
                    length -= MaxReadPacketSize;
                }
                return result.ToArray();
            }

            var buffer = new byte[4];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            prgReadDone = false;
            sendData(Command.COMMAND_PRG_READ_REQUEST, buffer);
            if (Blocking)
            {
                for (int t = 0; t < Timeout; t += 50)
                {
                    Thread.Sleep(50);
                    if (prgReadDone) return prgRecvData;
                }
                throw new IOException("Read timeout");
            }
            return null;
        }

        public void WritePrg(UInt16 address, byte[] data)
        {
            if (data.Length > MaxWritePacketSize) // Split packets
            {
                int wlength = data.Length;
                int pos = 0;
                while (wlength > 0)
                {
                    var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                    Array.Copy(data, pos, wdata, 0, wdata.Length);
                    WritePrg(address, wdata);
                    address += MaxWritePacketSize;
                    pos += MaxWritePacketSize;
                    wlength -= MaxWritePacketSize;
                }
                return;
            }

            int length = data.Length;
            var buffer = new byte[4 + length];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            Array.Copy(data, 0, buffer, 4, length);
            prgWriteDone = false;
            sendData(Command.COMMAND_PRG_WRITE_REQUEST, buffer);
            if (Blocking)
            {
                for (int t = 0; t < Timeout; t += 50)
                {
                    Thread.Sleep(50);
                    if (prgWriteDone) return;
                }
                throw new IOException("Write timeout");
            }
        }

        public void WritePrg(UInt16 address, byte data)
        {
            WritePrg(address, new byte[] { data });
        }


        public byte[] ReadChr(UInt16 address, int length)
        {
            if (length > MaxReadPacketSize) // Split packets
            {
                var result = new List<byte>();
                while (length > 0)
                {
                    result.AddRange(ReadChr(address, Math.Min(MaxReadPacketSize, length)));
                    address += MaxReadPacketSize;
                    length -= MaxReadPacketSize;
                }
                return result.ToArray();
            }

            var buffer = new byte[4];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            chrReadDone = false;
            sendData(Command.COMMAND_CHR_READ_REQUEST, buffer);
            if (Blocking)
            {
                for (int t = 0; t < Timeout; t += 50)
                {
                    Thread.Sleep(50);
                    if (chrReadDone) return chrRecvData;
                }
                throw new IOException("Read timeout");
            }
            return null;
        }

        public void WriteChr(UInt16 address, byte[] data)
        {
            if (data.Length > MaxWritePacketSize) // Split packets
            {
                int wlength = data.Length;
                int pos = 0;
                while (wlength > 0)
                {
                    var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                    Array.Copy(data, pos, wdata, 0, wdata.Length);
                    WriteChr(address, wdata);
                    address += MaxWritePacketSize;
                    pos += MaxWritePacketSize;
                    wlength -= MaxWritePacketSize;
                }
                return;
            }

            int length = data.Length;
            var buffer = new byte[4 + length];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            Array.Copy(data, 0, buffer, 4, length);
            chrWriteDone = false;
            sendData(Command.COMMAND_CHR_WRITE_REQUEST, buffer);
            if (Blocking)
            {
                for (int t = 0; t < Timeout; t += 50)
                {
                    Thread.Sleep(50);
                    if (chrWriteDone) return;
                }
                throw new IOException("Write timeout");
            }
        }

        public int GetMirroring(bool wait = true)
        {
            mirroring = -1;
            sendData(Command.COMMAND_MIRRORING_REQUEST, new byte[0]);
            if (wait)
            {
                for (int t = 0; t < Timeout; t += 50)
                {
                    Thread.Sleep(50);
                    if (mirroring >= 0) return mirroring;
                }
                throw new IOException("Read timeout");
            }
            return -1;
        }

        public void Reset(bool wait = true)
        {
            resetAck = false;
            sendData(Command.COMMAND_RESET, new byte[0]);
            if (wait)
            {
                for (int t = 0; t < Timeout; t += 50)
                {
                    Thread.Sleep(50);
                    if (resetAck) return;
                }
                throw new IOException("Read timeout");
            }
        }

        void FamicomDumperConnection_OnPrgReadResult(byte[] data)
        {
            prgRecvData = data;
            prgReadDone = true;
        }
        void FamicomDumperConnection_OnPrgWriteDone()
        {
            prgWriteDone = true;
        }
        void FamicomDumperConnection_OnChrReadResult(byte[] data)
        {
            chrRecvData = data;
            chrReadDone = true;
        }
        void FamicomDumperConnection_OnChrWriteDone()
        {
            chrWriteDone = true;
        }
        void FamicomDumperConnection_OnMirroring(int mirroring)
        {
            this.mirroring = mirroring;
        }
        void FamicomDumperConnection_OnResetAck()
        {
            resetAck = true;
        }
    }
}
