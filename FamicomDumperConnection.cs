using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace Cluster.Famicom
{
    public class FamicomDumperConnection : IDisposable
    {
        const int PortBaudRate = 250000;
        const int MaxReadPacketSize = 1024;
        const int MaxWritePacketSize = 1024;
        const string DeviceName = "Famicom Dumper/Programmer";

        public string PortName { get; set; }
        SerialPort serialPort = null;
        FTDI d2xxPort = null;

        Thread readingThread;

        int comm_recv_pos;
        byte comm_recv_command;
        byte comm_recv_crc;
        bool comm_recv_error;

        int comm_recv_length;
        List<byte> recv_buffer = new List<byte>();

        bool cpuInitOk = false;
        bool ppuInitOk = false;
        bool cpuReadDone = false;
        //bool prgWriteDone = false;
        int cpuWriteDoneCounter = 0;
        bool ppuReadDone = false;
        bool ppuWriteDone = false;
        byte[] prgRecvData, chrRecvData;
        byte[] mirroring;
        bool resetAck = false;
        bool? jtagResult = null;

        private delegate void OnReadResultDelegate(byte[] data);
        private delegate void OnWriteDoneDelegate();
        private delegate void OnMirroringDelegate(byte[] mirroring);
        private delegate void OnResetAckDelegate();
        private delegate void OnErrorDelegate();
        private delegate void OnJtagResultDelegate(bool success);
        private event OnReadResultDelegate OnCpuReadResult;
        private event OnWriteDoneDelegate OnCpuWriteDone;
        private event OnReadResultDelegate OnPpuReadResult;
        private event OnWriteDoneDelegate OnPpuWriteDone;
        private event OnMirroringDelegate OnMirroring;
        private event OnErrorDelegate OnError;
        private event OnResetAckDelegate OnResetAck;
        private event OnJtagResultDelegate OnJtagResult;

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
            COMMAND_CHR_FLASH_WRITE_REQUEST = 27,
            COMMAND_JTAG_SETUP = 28,
            COMMAND_JTAG_SHUTDOWN = 29,
            COMMAND_JTAG_EXECUTE = 30,
            COMMAND_JTAG_RESULT = 31,
            COMMAND_TEST_SET = 32,
            COMMAND_TEST_RESULT = 33,
            COMMAND_COOLBOY_READ_REQUEST = 34,
            COMMAND_COOLBOY_ERASE_REQUEST = 35,
            COMMAND_COOLBOY_WRITE_REQUEST = 36,
            COMMAND_COOLGIRL_ERASE_SECTOR_REQUEST = 37,
            COMMAND_COOLGIRL_WRITE_REQUEST = 38,
            COMMAND_BOOTLOADER = 0xFE,
            COMMAND_DEBUG = 0xFF
        }

        public enum FlashType
        {
            FirstFlash,
            Coolboy,
            Coolgirl
        }

        public FamicomDumperConnection(string portName = null)
        {
            this.PortName = portName;
            OnCpuReadResult += FamicomDumperConnection_OnPrgReadResult;
            OnCpuWriteDone += FamicomDumperConnection_OnPrgWriteDone;
            OnPpuReadResult += FamicomDumperConnection_OnChrReadResult;
            OnPpuWriteDone += FamicomDumperConnection_OnChrWriteDone;
            OnMirroring += FamicomDumperConnection_OnMirroring;
            OnResetAck += FamicomDumperConnection_OnResetAck;
            OnJtagResult += FamicomDumperConnection_OnJtagResult;
            Timeout = 10000;
        }

        public void Open()
        {
            if (PortName.ToUpper().StartsWith("COM"))
            {
                SerialPort sPort;
                sPort = new SerialPort();
                sPort.PortName = PortName;
                sPort.WriteTimeout = 5000; sPort.ReadTimeout = -1;
                sPort.BaudRate = PortBaudRate;
                sPort.Parity = Parity.None;
                sPort.DataBits = 8;
                sPort.StopBits = StopBits.One;
                sPort.Handshake = Handshake.None;
                sPort.DtrEnable = false;
                sPort.RtsEnable = false;
                sPort.NewLine = System.Environment.NewLine;
                sPort.Open();
                serialPort = sPort;
            }
            else
            {
                UInt32 ftdiDeviceCount = 0;
                FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;
                // Create new instance of the FTDI device class
                FTDI myFtdiDevice = new FTDI();
                // Determine the number of FTDI devices connected to the machine
                if (string.IsNullOrEmpty(PortName) || PortName.ToLower() == "auto")
                {
                    Console.WriteLine("Searhing for dumper (FTDI device with name \"{0}\")...", DeviceName);
                    ftStatus = myFtdiDevice.GetNumberOfDevices(ref ftdiDeviceCount);
                    // Check status
                    if (ftStatus == FTDI.FT_STATUS.FT_OK)
                    {
                        Console.WriteLine("Number of FTDI devices: " + ftdiDeviceCount.ToString());
                        Console.WriteLine("");
                    }
                    else
                        throw new IOException("Failed to get number of devices (error " + ftStatus.ToString() + ")");

                    // If no devices available, return
                    if (ftdiDeviceCount == 0)
                        throw new IOException("Failed to get number of devices (error " + ftStatus.ToString() + ")");

                    // Allocate storage for device info list
                    FTDI.FT_DEVICE_INFO_NODE[] ftdiDeviceList = new FTDI.FT_DEVICE_INFO_NODE[ftdiDeviceCount];

                    // Populate our device list
                    ftStatus = myFtdiDevice.GetDeviceList(ftdiDeviceList);

                    PortName = null;
                    if (ftStatus == FTDI.FT_STATUS.FT_OK)
                    {
                        for (UInt32 i = 0; i < ftdiDeviceCount; i++)
                        {
                            Console.WriteLine("Device Index: " + i.ToString());
                            Console.WriteLine("Flags: " + String.Format("{0:x}", ftdiDeviceList[i].Flags));
                            Console.WriteLine("Type: " + ftdiDeviceList[i].Type.ToString());
                            Console.WriteLine("ID: " + String.Format("{0:x}", ftdiDeviceList[i].ID));
                            Console.WriteLine("Location ID: " + String.Format("{0:x}", ftdiDeviceList[i].LocId));
                            Console.WriteLine("Serial Number: " + ftdiDeviceList[i].SerialNumber.ToString());
                            Console.WriteLine("Description: " + ftdiDeviceList[i].Description.ToString());
                            Console.WriteLine("");
                            if (ftdiDeviceList[i].Description == DeviceName)
                                PortName = ftdiDeviceList[i].SerialNumber;
                        }
                    }
                    if (PortName == null)
                        throw new IOException("Famicom Dumper/Programmer not found");
                }

                // Open first device in our list by serial number
                ftStatus = myFtdiDevice.OpenBySerialNumber(PortName);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    throw new IOException("Failed to open device (error " + ftStatus.ToString() + ")");
                // Set data characteristics - Data bits, Stop bits, Parity
                ftStatus = myFtdiDevice.SetTimeouts(300000, 5000);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    throw new IOException("Failed to set timeouts (error " + ftStatus.ToString() + ")");
                ftStatus = myFtdiDevice.SetDataCharacteristics(FTDI.FT_DATA_BITS.FT_BITS_8, FTDI.FT_STOP_BITS.FT_STOP_BITS_1, FTDI.FT_PARITY.FT_PARITY_NONE);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    throw new IOException("Failed to set data characteristics (error " + ftStatus.ToString() + ")");
                // Set flow control
                ftStatus = myFtdiDevice.SetFlowControl(FTDI.FT_FLOW_CONTROL.FT_FLOW_NONE, 0x11, 0x13);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    throw new IOException("Failed to set flow control (error " + ftStatus.ToString() + ")");
                // Set up device data parameters
                ftStatus = myFtdiDevice.SetBaudRate(PortBaudRate);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    throw new IOException("Failed to set Baud rate (error " + ftStatus.ToString() + ")");
                d2xxPort = myFtdiDevice;
            }

            if (readingThread != null)
                readingThread.Abort();
            readingThread = new Thread(readThread);
            readingThread.Start();

            cpuInitOk = ppuInitOk = false;
        }

        public void Close()
        {
            if (serialPort != null)
            {
                if (serialPort.IsOpen)
                    serialPort.Close();
                serialPort = null;
            }
            if (d2xxPort != null)
            {
                if (d2xxPort.IsOpen)
                    d2xxPort.Close();
                d2xxPort = null;
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
                while (serialPort != null)
                {
                    try
                    {
                        int c = serialPort.ReadByte();
                        if (c >= 0)
                        {
                            //Console.Write((char)c);
#if DEBUG
//                            Console.Write("{0:X2} ", c);
//                            Thread.Sleep(10);
#endif
                            recvProceed((byte)c);
                        }
                    }
                    catch (TimeoutException) { }
                }
                while (d2xxPort != null)
                {
                    try
                    {
                        UInt32 numBytesAvailable = 0;
                        FTDI.FT_STATUS ftStatus;
                        do
                        {
                            ftStatus = d2xxPort.GetRxBytesAvailable(ref numBytesAvailable);
                            if (ftStatus != FTDI.FT_STATUS.FT_OK)
                                throw new IOException("Failed to get number of bytes available to read (error " + ftStatus.ToString() + ")");
                            Thread.Sleep(10);
                        } while (numBytesAvailable == 0);
                        var data = new byte[numBytesAvailable];
                        UInt32 numBytesRead = 0;
                        ftStatus = d2xxPort.Read(data, numBytesAvailable, ref numBytesRead);
                        if (ftStatus != FTDI.FT_STATUS.FT_OK)
                            throw new IOException("Failed to read data (error " + ftStatus.ToString() + ")");
                        foreach (var b in data)
                            recvProceed(b);
                    }
                    catch (TimeoutException) { }
                }
            }
            catch (ThreadAbortException) { }
            catch (IOException)
            {
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
            //Console.WriteLine("Received command: " + command);
            switch (command)
            {
                case Command.COMMAND_PRG_STARTED:
                    cpuInitOk = true;
                    break;
                case Command.COMMAND_CHR_STARTED:
                    ppuInitOk = true;
                    break;
                case Command.COMMAND_PRG_READ_RESULT:
                    if (OnCpuReadResult != null)
                        OnCpuReadResult(data);
                    break;
                case Command.COMMAND_PRG_WRITE_DONE:
                    if (OnCpuWriteDone != null)
                        OnCpuWriteDone();
                    break;
                case Command.COMMAND_CHR_READ_RESULT:
                    if (OnPpuReadResult != null)
                        OnPpuReadResult(data);
                    break;
                case Command.COMMAND_CHR_WRITE_DONE:
                    if (OnPpuWriteDone != null)
                        OnPpuWriteDone();
                    break;
                case Command.COMMAND_MIRRORING_RESULT:
                    if (OnMirroring != null)
                        OnMirroring(data);
                    break;
                case Command.COMMAND_RESET_ACK:
                    if (OnResetAck != null)
                        OnResetAck();
                    break;
                case Command.COMMAND_JTAG_RESULT:
                    if (OnJtagResult != null)
                        OnJtagResult(data[0] != 0);
                    break;
                case Command.COMMAND_DEBUG:
                    showDebugInfo(data);
                    break;
            }
        }

        void showDebugInfo(byte[] data)
        {
            Console.Write("Debug info:");
            foreach (var b in data)
                Console.Write(" {0:X2}", b);
            Console.WriteLine();
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
            //foreach (var b in buffer) Console.Write(", 0x{0:X2}", b);
            if (serialPort != null)
                serialPort.Write(buffer, 0, buffer.Length);
            if (d2xxPort != null)
            {
                UInt32 numBytesWritten = 0;
                var ftStatus = d2xxPort.Write(buffer, buffer.Length, ref numBytesWritten);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    throw new IOException("Failed to write to device (error " + ftStatus.ToString() + ")");
            }
        }

        public bool PrgReaderInit()
        {
            cpuInitOk = false;
            for (int i = 0; i < 50; i++)
            {
                sendData(Command.COMMAND_PRG_INIT, new byte[0]);
                Thread.Sleep(100);
                if (cpuInitOk) return true;
            }
            return false;
        }

        public bool ChrReaderInit()
        {
            ppuInitOk = false;
            for (int i = 0; i < 50; i++)
            {
                sendData(Command.COMMAND_CHR_INIT, new byte[0]);
                Thread.Sleep(100);
                if (ppuInitOk) return true;
            }
            return false;
        }

        public byte[] ReadCpu(UInt16 address, int length, FlashType flashType = FlashType.FirstFlash)
        {
            var result = new List<byte>();
            while (length > 0)
            {
                result.AddRange(readCpuBlock(address, Math.Min(MaxReadPacketSize, length), flashType));
                address += MaxReadPacketSize;
                length -= MaxReadPacketSize;
            }
            return result.ToArray();
        }

        private byte[] readCpuBlock(UInt16 address, int length, FlashType flashType = FlashType.FirstFlash)
        {
            var buffer = new byte[4];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            cpuReadDone = false;
            switch (flashType)
            {
                case FlashType.FirstFlash:
                case FlashType.Coolgirl:
                    sendData(Command.COMMAND_PRG_READ_REQUEST, buffer);
                    break;
                case FlashType.Coolboy:
                    sendData(Command.COMMAND_COOLBOY_READ_REQUEST, buffer);
                    break;
            }
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (cpuReadDone) return prgRecvData;
            }
            throw new IOException("Read timeout");
        }

        public void WriteCpu(UInt16 address, byte data)
        {
            WriteCpu(address, new byte[] { data });
        }

        public void WriteCpu(UInt16 address, byte[] data)
        {
            int wlength = data.Length;
            int pos = 0;
            while (wlength > 0)
            {
                var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                Array.Copy(data, pos, wdata, 0, wdata.Length);
                writeCpuBlock(address, wdata);
                address += MaxWritePacketSize;
                pos += MaxWritePacketSize;
                wlength -= MaxWritePacketSize;
            }
            return;
        }

        private void writeCpuBlock(UInt16 address, byte[] data)
        {
            int length = data.Length;
            var buffer = new byte[4 + length];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            Array.Copy(data, 0, buffer, 4, length);
            cpuWriteDoneCounter = 0;
            sendData(Command.COMMAND_PRG_WRITE_REQUEST, buffer);
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (cpuWriteDoneCounter != 0) return;
            }
            throw new IOException("Write timeout");
        }

        public void ErasePrgFlash(FlashType flashType = FlashType.FirstFlash)
        {
            switch (flashType)
            {
                case FlashType.FirstFlash:
                    sendData(Command.COMMAND_PRG_FLASH_ERASE_REQUEST, new byte[0]);
                    break;
                case FlashType.Coolboy:
                    sendData(Command.COMMAND_COOLBOY_ERASE_REQUEST, new byte[0]);
                    break;
                case FlashType.Coolgirl:
                    sendData(Command.COMMAND_COOLGIRL_ERASE_SECTOR_REQUEST, new byte[0]);
                    break;
            }
            cpuWriteDoneCounter = 0;
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (cpuWriteDoneCounter != 0) return;
            }
            throw new IOException("Write timeout");
        }

        public void WritePrgFlash(UInt16 address, byte[] data, FlashType flashType = FlashType.FirstFlash, bool accelerated = false)
        {
            int wlength = data.Length;
            int pos = 0;
            int writeCounter = 0;
            cpuWriteDoneCounter = 0;
            while (wlength > 0)
            {
                var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                Array.Copy(data, pos, wdata, 0, wdata.Length);
                if (containsNotFF(data))
                {
                    writePrgFlashBlock(address, wdata, !accelerated, flashType);
                    writeCounter++;
                    if (accelerated)
                        Thread.Sleep(40);
                }
                address += MaxWritePacketSize;
                pos += MaxWritePacketSize;
                wlength -= MaxWritePacketSize;
                //Console.WriteLine("{0} / {1}", writeCounter, prgWriteDoneCounter);
            }
            if (accelerated)
            {
                for (int t = 0; t < Timeout; t += 5)
                {
                    Thread.Sleep(5);
                    if (cpuWriteDoneCounter >= writeCounter)
                        return;
                }
                throw new IOException("Write timeout");
            }
        }

        private static bool containsNotFF(byte[] data)
        {
            foreach (var b in data)
                if (b != 0xFF) return true;
            return false;
        }

        private void writePrgFlashBlock(UInt16 address, byte[] data, bool wait = true, FlashType flashType = FlashType.FirstFlash)
        {
            //Console.WriteLine("{0:X8}", address);
            int length = data.Length;
            var buffer = new byte[4 + length];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            Array.Copy(data, 0, buffer, 4, length);
            if (wait)
                cpuWriteDoneCounter = 0;
            switch (flashType)
            {
                case FlashType.FirstFlash:
                    sendData(Command.COMMAND_PRG_FLASH_WRITE_REQUEST, buffer);
                    break;
                case FlashType.Coolboy:
                    sendData(Command.COMMAND_COOLBOY_WRITE_REQUEST, buffer);
                    break;
                case FlashType.Coolgirl:
                    sendData(Command.COMMAND_COOLGIRL_WRITE_REQUEST, buffer);
                    break;
            }
            if (wait)
            {
                for (int t = 0; t < Timeout; t += 5)
                {
                    Thread.Sleep(5);
                    if (cpuWriteDoneCounter != 0) return;
                }
                throw new IOException("Write timeout");
            }
        }

        public void EraseChrFlash()
        {
            sendData(Command.COMMAND_CHR_FLASH_ERASE_REQUEST, new byte[0]);
            ppuWriteDone = false;
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (ppuWriteDone) return;
            }
            throw new IOException("Write timeout");
        }

        public void WriteChrFlash(UInt16 address, byte[] data)
        {
            int wlength = data.Length;
            int pos = 0;
            while (wlength > 0)
            {
                var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                Array.Copy(data, pos, wdata, 0, wdata.Length);
                writeChrFlashBlock(address, wdata);
                address += MaxWritePacketSize;
                pos += MaxWritePacketSize;
                wlength -= MaxWritePacketSize;
            }
            return;
        }

        private void writeChrFlashBlock(UInt16 address, byte[] data)
        {
            int length = data.Length;
            var buffer = new byte[4 + length];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            Array.Copy(data, 0, buffer, 4, length);
            ppuWriteDone = false;
            sendData(Command.COMMAND_CHR_FLASH_WRITE_REQUEST, buffer);
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (ppuWriteDone) return;
            }
            throw new IOException("Write timeout");
        }

        public byte[] ReadPpu(UInt16 address, int length, bool wait = true)
        {
            if (length > MaxReadPacketSize) // Split packets;
            {
                var result = new List<byte>();
                while (length > 0)
                {
                    result.AddRange(ReadPpu(address, Math.Min(MaxReadPacketSize, length)));
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
            ppuReadDone = false;
            sendData(Command.COMMAND_CHR_READ_REQUEST, buffer);
            if (wait)
            {
                for (int t = 0; t < Timeout; t += 5)
                {
                    Thread.Sleep(5);
                    if (ppuReadDone) return chrRecvData;
                }
                throw new IOException("Read timeout");
            }
            return null;
        }

        public void WritePpu(UInt16 address, byte[] data, bool wait = true)
        {
            if (data.Length > MaxWritePacketSize) // Split packets
            {
                int wlength = data.Length;
                int pos = 0;
                while (wlength > 0)
                {
                    var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                    Array.Copy(data, pos, wdata, 0, wdata.Length);
                    WritePpu(address, wdata);
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
            ppuWriteDone = false;
            sendData(Command.COMMAND_CHR_WRITE_REQUEST, buffer);
            if (wait)
            {
                for (int t = 0; t < Timeout; t += 5)
                {
                    Thread.Sleep(5);
                    if (ppuWriteDone) return;
                }
                throw new IOException("Write timeout");
            }
        }

        public void WriteChrEprom(UInt16 address, byte[] data, bool wait = true)
        {
            if (data.Length > MaxWritePacketSize) // Split packets
            {
                int wlength = data.Length;
                int pos = 0;
                while (wlength > 0)
                {
                    var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                    Array.Copy(data, pos, wdata, 0, wdata.Length);
                    WriteChrEprom(address, wdata);
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
            ppuWriteDone = false;
            sendData(Command.COMMAND_CHR_EPROM_WRITE_REQUEST, buffer);
            if (wait)
            {
                for (int t = 0; t < Timeout; t += 5)
                {
                    Thread.Sleep(5);
                    if (ppuWriteDone) return;
                }
                throw new IOException("Write timeout");
            }
        }

        public void PrepareEprom()
        {
            sendData(Command.COMMAND_EPROM_PREPARE, new byte[0]);
        }

        public byte[] GetMirroring()
        {
            mirroring = null;
            sendData(Command.COMMAND_MIRRORING_REQUEST, new byte[0]);
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (mirroring != null)
                    return mirroring;
            }
            throw new IOException("Read timeout");
        }

        public void Reset(bool wait = true)
        {
            resetAck = false;
            sendData(Command.COMMAND_RESET, new byte[0]);
            if (wait)
            {
                for (int t = 0; t < Timeout; t += 5)
                {
                    Thread.Sleep(5);
                    if (resetAck) return;
                }
                throw new IOException("Read timeout");
            }
        }

        public void JtagSetup(bool wait = true)
        {
            jtagResult = null;
            sendData(Command.COMMAND_JTAG_SETUP, new byte[0]);
            if (wait)
            {
                for (int t = 0; t < Timeout; t += 50)
                {
                    Thread.Sleep(5);
                    if (jtagResult == true) return;
                    if (jtagResult == false)
                        throw new IOException("JTAG setup error");
                }
                throw new IOException("JTAG setup timeout");
            }
        }

        public void JtagShutdown(bool wait = true)
        {
            jtagResult = null;
            sendData(Command.COMMAND_JTAG_SHUTDOWN, new byte[0]);
            if (wait)
            {
                for (int t = 0; t < Timeout; t += 50)
                {
                    Thread.Sleep(5);
                    if (jtagResult == true) return;
                    if (jtagResult == false)
                        throw new IOException("JTAG shutdown error");
                }
                throw new IOException("JTAG shutdown timeout");
            }
        }

        public void WriteJtag(byte[] data, bool wait = true)
        {
            if (data.Length > MaxWritePacketSize) // Split packets
            {
                int wlength = data.Length;
                int pos = 0;
                while (wlength > 0)
                {
                    var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                    Array.Copy(data, pos, wdata, 0, wdata.Length);
                    WriteJtag(wdata);
                    pos += MaxWritePacketSize;
                    wlength -= MaxWritePacketSize;
                }
                return;
            }

            int length = data.Length;
            var buffer = new byte[2 + length];
            buffer[0] = (byte)(length & 0xFF);
            buffer[1] = (byte)((length >> 8) & 0xFF);
            Array.Copy(data, 0, buffer, 2, length);

            jtagResult = null;
            sendData(Command.COMMAND_JTAG_EXECUTE, buffer);
            //Console.Write("{0:X2} ", buffer[2]);
            if (wait)
            {
                for (int t = 0; t < Timeout; t += 5)
                {
                    Thread.Sleep(5);
                    if (jtagResult == true) return;
                    if (jtagResult == false)
                        throw new IOException("JTAG error");
                }
                throw new IOException("JTAG write timeout");
            }
        }

        public void Bootloader()
        {
            sendData(Command.COMMAND_BOOTLOADER, new byte[0]);
        }

        void FamicomDumperConnection_OnPrgReadResult(byte[] data)
        {
            prgRecvData = data;
            cpuReadDone = true;
        }
        void FamicomDumperConnection_OnPrgWriteDone()
        {
            cpuWriteDoneCounter++;
        }
        void FamicomDumperConnection_OnChrReadResult(byte[] data)
        {
            chrRecvData = data;
            ppuReadDone = true;
        }
        void FamicomDumperConnection_OnChrWriteDone()
        {
            ppuWriteDone = true;
        }
        void FamicomDumperConnection_OnMirroring(byte[] mirroring)
        {
            this.mirroring = mirroring;
        }
        void FamicomDumperConnection_OnResetAck()
        {
            resetAck = true;
        }
        void FamicomDumperConnection_OnJtagResult(bool success)
        {
            jtagResult = success;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
