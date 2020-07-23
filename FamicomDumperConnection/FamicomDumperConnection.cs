using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace com.clusterrr.Famicom.DumperConnection
{
    public class FamicomDumperConnection : MarshalByRefObject, IDisposable, IFamicomDumperConnection
    {
        const int PortBaudRate = 250000;
        const int MaxReadPacketSize = 1024;
        const int MaxWritePacketSize = 1024;
        const byte Magic = 0x46;
        const string DeviceName = "Famicom Dumper/Programmer";

        public string PortName { get; set; }
        public bool Verbose { get; set; } = false;
        public int Timeout { get; set; }

        private SerialPort serialPort = null;
        private FTDI d2xxPort = null;
        private Thread readingThread;
        private int commRecvPos;
        private byte commRecvCommand;
        private byte commRecvCrc;
        private bool commRecvError;
        private int commRecvLength;
        private List<byte> recvBuffer = new List<byte>();
        private bool dumperInitOk = false;
        private bool cpuReadDone = false;
        private int cpuWriteDoneCounter = 0;
        private bool ppuReadDone = false;
        private bool ppuWriteDone = false;
        private byte[] prgRecvData, chrRecvData;
        private byte[] mirroring;
        private bool resetAck = false;

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
            COMMAND_TEST_SET = 32,
            COMMAND_TEST_RESULT = 33,
            COMMAND_COOLBOY_READ_REQUEST = 34,
            COMMAND_COOLBOY_ERASE_REQUEST = 35,
            COMMAND_COOLBOY_WRITE_REQUEST = 36,
            COMMAND_COOLGIRL_ERASE_SECTOR_REQUEST = 37,
            COMMAND_COOLGIRL_WRITE_REQUEST = 38,
            COMMAND_PRG_CRC_READ_REQUEST = 39,
            COMMAND_CHR_CRC_READ_REQUEST = 40,
            COMMAND_BOOTLOADER = 0xFE,
            COMMAND_DEBUG = 0xFF
        }

        public enum MemoryAccessMethod
        {
            CoolboyGPIO,
            Direct
        }

        public FamicomDumperConnection(string portName = null)
        {
            this.PortName = portName;
            Timeout = 10000;
        }

        public void Open()
        {
            if (PortName.ToUpper().StartsWith("COM") || IsRunningOnMono())
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
                sPort.NewLine = Environment.NewLine;
                sPort.Open();
                serialPort = sPort;
            }
            else
            {
                uint ftdiDeviceCount = 0;
                FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;
                // Create new instance of the FTDI device class
                FTDI myFtdiDevice = new FTDI();
                // Determine the number of FTDI devices connected to the machine
                if (string.IsNullOrEmpty(PortName) || PortName.ToLower() == "auto")
                {
                    //Console.WriteLine("Searching for dumper (FTDI device with name \"{0}\")...", DeviceName);
                    ftStatus = myFtdiDevice.GetNumberOfDevices(ref ftdiDeviceCount);
                    // Check status
                    if (ftStatus == FTDI.FT_STATUS.FT_OK)
                    {
                        //Console.WriteLine("Number of FTDI devices: " + ftdiDeviceCount.ToString());
                        //Console.WriteLine("");
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
                            //Console.WriteLine("Device Index: " + i.ToString());
                            //Console.WriteLine("Flags: " + String.Format("{0:x}", ftdiDeviceList[i].Flags));
                            //Console.WriteLine("Type: " + ftdiDeviceList[i].Type.ToString());
                            //Console.WriteLine("ID: " + String.Format("{0:x}", ftdiDeviceList[i].ID));
                            //Console.WriteLine("Location ID: " + String.Format("{0:x}", ftdiDeviceList[i].LocId));
                            //Console.WriteLine("Serial Number: " + ftdiDeviceList[i].SerialNumber.ToString());
                            //Console.WriteLine("Description: " + ftdiDeviceList[i].Description.ToString());
                            //Console.WriteLine("");
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

            dumperInitOk = false;
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
                            RecvProceed((byte)c);
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
                            RecvProceed(b);
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

        void CalcRecvCRC(byte inbyte)
        {
            int j;
            for (j = 0; j < 8; j++)
            {
                byte mix = (byte)((commRecvCrc ^ inbyte) & 0x01);
                commRecvCrc >>= 1;
                if (mix != 0)
                    commRecvCrc ^= 0x8C;
                inbyte >>= 1;
            }
        }

        void RecvProceed(byte data)
        {
            if (commRecvError && data != Magic) return;
            commRecvError = false;
            if (commRecvPos == 0)
            {
                commRecvCrc = 0;
                recvBuffer.Clear();
            }

            CalcRecvCRC(data);
            int l = commRecvPos - 4;
            switch (commRecvPos)
            {
                case 0:
                    if (data != Magic)
                    {
                        OnError();
                    }
                    break;
                case 1:
                    commRecvCommand = data;
                    break;
                case 2:
                    commRecvLength = data;
                    break;
                case 3:
                    commRecvLength |= data << 8;
                    break;
                default:
                    if (l < commRecvLength)
                    {
                        recvBuffer.Add(data);
                    }
                    else if (l == commRecvLength)
                    {
                        if (commRecvCrc == 0)
                        {
                            DataReceived((Command)commRecvCommand, recvBuffer.ToArray());
                        }
                        else
                        {
                            commRecvError = true;
                            OnError();
                            //comm_start(COMMAND_ERROR_CRC, 0);
                        }
                        commRecvPos = 0;
                        return;
                    }
                    break;
            }
            commRecvPos++;
        }

        void DataReceived(Command command, byte[] data)
        {
            //Console.WriteLine("Received command: " + command);
            switch (command)
            {
                case Command.COMMAND_PRG_STARTED:
                    dumperInitOk = true;
                    break;
                case Command.COMMAND_PRG_READ_RESULT:
                    OnCpuReadResult(data);
                    break;
                case Command.COMMAND_PRG_WRITE_DONE:
                    OnCpuWriteDone();
                    break;
                case Command.COMMAND_CHR_READ_RESULT:
                    OnPpuReadResult(data);
                    break;
                case Command.COMMAND_CHR_WRITE_DONE:
                    OnPpuWriteDone();
                    break;
                case Command.COMMAND_MIRRORING_RESULT:
                    OnMirroring(data);
                    break;
                case Command.COMMAND_RESET_ACK:
                    OnResetAck();
                    break;
                case Command.COMMAND_DEBUG:
                    ShowDebugInfo(data);
                    break;
            }
        }

        void ShowDebugInfo(byte[] data)
        {
            foreach (var b in data)
                Console.Write("{0:X2} ", b);
        }

        void SendData(Command command, byte[] data)
        {
            byte[] buffer = new byte[data.Length + 5];
            buffer[0] = Magic;
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
                uint numBytesWritten = 0;
                var ftStatus = d2xxPort.Write(buffer, buffer.Length, ref numBytesWritten);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    throw new IOException("Failed to write to device (error " + ftStatus.ToString() + ")");
            }
        }

        public bool DumperInit()
        {
            if (Verbose)
                Console.Write("Dumper initialization... ");

            dumperInitOk = false;
            for (int i = 0; i < 300; i++)
            {
                SendData(Command.COMMAND_PRG_INIT, new byte[0]);
                Thread.Sleep(50);
                if (dumperInitOk)
                {
                    if (Verbose)
                        Console.WriteLine("OK");
                    return true;
                }
            }
            if (Verbose)
                Console.WriteLine("failed");
            return false;
        }

        public byte[] ReadCpu(ushort address, int length)
            => ReadCpu(address, length, MemoryAccessMethod.Direct);

        public byte[] ReadCpu(ushort address, int length, MemoryAccessMethod flashType)
        {
            if (Verbose)
                Console.Write($"Reading 0x{length:X4}B <= 0x{address:X4} @ CPU...");
            var result = new List<byte>();
            while (length > 0)
            {
                result.AddRange(ReadCpuBlock(address, Math.Min(MaxReadPacketSize, length), flashType));
                address += MaxReadPacketSize;
                length -= MaxReadPacketSize;
            }
            if (Verbose && result.Count <= 32)
            {
                foreach (var b in result)
                    Console.Write($" {b:X2}");
            }
            else if (Verbose)
                Console.WriteLine(" OK");
            return result.ToArray();
        }

        private byte[] ReadCpuBlock(ushort address, int length, MemoryAccessMethod flashType = MemoryAccessMethod.Direct)
        {
            var buffer = new byte[4];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            cpuReadDone = false;
            switch (flashType)
            {
                case MemoryAccessMethod.Direct:
                    SendData(Command.COMMAND_PRG_READ_REQUEST, buffer);
                    break;
                case MemoryAccessMethod.CoolboyGPIO:
                    SendData(Command.COMMAND_COOLBOY_READ_REQUEST, buffer);
                    break;
            }
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (cpuReadDone) return prgRecvData;
            }
            throw new IOException("Read timeout");
        }

        public ushort ReadCpuCrc(ushort address, int length)
        {
            if (Verbose)
                Console.Write($"Reading CRC of 0x{length:X4}b of 0x{address:X4} @ CPU...");
            var buffer = new byte[4];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            cpuReadDone = false;
            SendData(Command.COMMAND_PRG_CRC_READ_REQUEST, buffer);
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (cpuReadDone)
                {
                    var crc = (ushort)(prgRecvData[0] | (prgRecvData[1] * 0x100));
                    if (Verbose)
                        Console.WriteLine($" {crc:X4}");
                    return crc;
                }
            }
            throw new IOException("Read timeout");
        }

        public void WriteCpu(ushort address, byte data)
            => WriteCpu(address, new byte[] { data });

        public void WriteCpu(ushort address, byte[] data)
        {
            if (Verbose)
            {
                if (data.Length <= 32)
                {
                    Console.Write($"Writing ");
                    foreach (var b in data)
                        Console.Write($"0x{b:X2} ");
                    Console.Write($"=> 0x{address:X4} @ CPU...");
                }
                else
                {
                    Console.Write($"Writing 0x{data.Length:X4}B => 0x{address:X4} @ CPU...");
                }
            }
            int wlength = data.Length;
            int pos = 0;
            while (wlength > 0)
            {
                var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                Array.Copy(data, pos, wdata, 0, wdata.Length);
                WriteCpuBlock(address, wdata);
                address += MaxWritePacketSize;
                pos += MaxWritePacketSize;
                wlength -= MaxWritePacketSize;
            }
            if (Verbose)
                Console.WriteLine(" OK");
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
            cpuWriteDoneCounter = 0;
            SendData(Command.COMMAND_PRG_WRITE_REQUEST, buffer);
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (cpuWriteDoneCounter != 0) return;
            }
            throw new IOException("Write timeout");
        }

        public void EraseCpuFlash(MemoryAccessMethod flashType)
        {
            switch (flashType)
            {
                case MemoryAccessMethod.CoolboyGPIO:
                    SendData(Command.COMMAND_COOLBOY_ERASE_REQUEST, new byte[0]);
                    break;
                case MemoryAccessMethod.Direct:
                    SendData(Command.COMMAND_COOLGIRL_ERASE_SECTOR_REQUEST, new byte[0]);
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

        public void WriteCpuFlash(ushort address, byte[] data, MemoryAccessMethod flashType = MemoryAccessMethod.Direct, bool accelerated = false)
        {
            if (Verbose)
            {
                if (data.Length <= 32)
                {
                    Console.Write($"Writing ");
                    foreach (var b in data)
                        Console.Write($"0x{b:X2} ");
                    Console.Write($"=> 0x{address:X4} @ CPU flash ({flashType})...");
                }
                else
                {
                    Console.Write($"Writing 0x{data.Length:X4}B => 0x{address:X4} @ CPU flash ({flashType})...");
                }
            }
            int wlength = data.Length;
            int pos = 0;
            int writeCounter = 0;
            cpuWriteDoneCounter = 0;
            while (wlength > 0)
            {
                var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                Array.Copy(data, pos, wdata, 0, wdata.Length);
                if (ContainsNotFF(data))
                {
                    WriteCpuFlashBlock(address, wdata, !accelerated, flashType);
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
            if (Verbose)
                Console.WriteLine(" OK");
        }

        private static bool ContainsNotFF(byte[] data)
        {
            foreach (var b in data)
                if (b != 0xFF) return true;
            return false;
        }

        private void WriteCpuFlashBlock(ushort address, byte[] data, bool wait, MemoryAccessMethod flashType)
        {
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
                case MemoryAccessMethod.CoolboyGPIO:
                    SendData(Command.COMMAND_COOLBOY_WRITE_REQUEST, buffer);
                    break;
                case MemoryAccessMethod.Direct:
                    SendData(Command.COMMAND_COOLGIRL_WRITE_REQUEST, buffer);
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

        public byte[] ReadPpu(ushort address, int length)
        {
            if (Verbose)
                Console.Write($"Reading 0x{length:X4}B <= 0x{address:X4} @ PPU...");
            var result = new List<byte>();
            while (length > 0)
            {
                result.AddRange(ReadPpuBlock(address, Math.Min(MaxReadPacketSize, length)));
                address += MaxReadPacketSize;
                length -= MaxReadPacketSize;
            }
            if (Verbose && result.Count <= 32)
            {
                foreach (var b in result)
                    Console.Write($" {b:X2}");
                Console.WriteLine();
            }
            else if (Verbose)
                Console.WriteLine(" OK");
            return result.ToArray();
        }

        public byte[] ReadPpuBlock(ushort address, int length)
        {
            var buffer = new byte[4];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            ppuReadDone = false;
            SendData(Command.COMMAND_CHR_READ_REQUEST, buffer);
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (ppuReadDone)
                    return chrRecvData;
            }
            throw new IOException("Read timeout");
        }

        public ushort ReadPpuCrc(ushort address, int length)
        {
            if (Verbose)
                Console.Write($"Reading CRC of 0x{length:X4}b of 0x{address:X4} @ PPU...");
            var buffer = new byte[4];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            ppuReadDone = false;
            SendData(Command.COMMAND_CHR_CRC_READ_REQUEST, buffer);
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (ppuReadDone)
                {
                    var crc = (ushort)(chrRecvData[0] | (chrRecvData[1] * 0x100));
                    if (Verbose)
                        Console.WriteLine($" {crc:X4}");
                    return crc;
                }
            }
            throw new IOException("Read timeout");
        }

        public void WritePpu(ushort address, byte data)
            => WritePpu(address, new byte[] { data });

        public void WritePpu(ushort address, byte[] data)
        {
            if (Verbose)
            {
                if (data.Length <= 32)
                {
                    Console.Write($"Writing ");
                    foreach (var b in data)
                        Console.Write($"0x{b:X2} ");
                    Console.Write($"=> 0x{address:X4} @ PPU...");
                }
                else
                {
                    Console.Write($"Writing 0x{data.Length:X4}B => 0x{address:X4} @ PPU...");
                }
            }
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
                if (Verbose)
                    Console.WriteLine(" OK");
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
            SendData(Command.COMMAND_CHR_WRITE_REQUEST, buffer);
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (ppuWriteDone) return;
            }
            throw new IOException("Write timeout");
        }

        public void WritePpuEprom(ushort address, byte[] data)
        {
            if (data.Length > MaxWritePacketSize) // Split packets
            {
                int wlength = data.Length;
                int pos = 0;
                while (wlength > 0)
                {
                    var wdata = new byte[Math.Min(MaxWritePacketSize, wlength)];
                    Array.Copy(data, pos, wdata, 0, wdata.Length);
                    WritePpuEprom(address, wdata);
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
            SendData(Command.COMMAND_CHR_EPROM_WRITE_REQUEST, buffer);
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (ppuWriteDone) return;
            }
            throw new IOException("Write timeout");
        }

        public bool[] GetMirroring()
        {
            mirroring = null;
            SendData(Command.COMMAND_MIRRORING_REQUEST, new byte[0]);
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (mirroring != null)
                    return mirroring.Select(v => v != 0).ToArray();
            }
            throw new IOException("Read timeout");
        }

        /// <summary>
        /// Simulate reset (M2 goes to Z-state for a second)
        /// </summary>
        public void Reset()
        {
            resetAck = false;
            SendData(Command.COMMAND_RESET, new byte[0]);
            for (int t = 0; t < Timeout; t += 5)
            {
                Thread.Sleep(5);
                if (resetAck) return;
            }
            throw new IOException("Read timeout");
        }

        public void Bootloader()
        {
            SendData(Command.COMMAND_BOOTLOADER, new byte[0]);
        }

        private void OnCpuReadResult(byte[] data)
        {
            prgRecvData = data;
            cpuReadDone = true;
        }

        private void OnCpuWriteDone()
        {
            cpuWriteDoneCounter++;
        }

        private void OnPpuReadResult(byte[] data)
        {
            chrRecvData = data;
            ppuReadDone = true;
        }

        private void OnPpuWriteDone()
        {
            ppuWriteDone = true;
        }

        private void OnMirroring(byte[] mirroring)
        {
            this.mirroring = mirroring;
        }

        private void OnResetAck()
        {
            resetAck = true;
        }

        private void OnError()
        {
        }

        private static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }

        public override object InitializeLifetimeService()
        {
            return null; // Infinity
        }

        public void Dispose()
        {
            Close();
        }
    }
}
