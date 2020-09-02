using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Management;

namespace com.clusterrr.Famicom.DumperConnection
{
    public class FamicomDumperConnection : MarshalByRefObject, IDisposable, IFamicomDumperConnection
    {
        const int PortBaudRate = 250000;
        const ushort DefaultmaxReadPacketSize = 1024;
        const ushort DefaultmaxWritePacketSize = 1024;
        const byte Magic = 0x46;
        const string DeviceName = "Famicom Dumper/Programmer";

        public string PortName { get; set; }
        public byte ProtocolVersion { get; private set; } = 0;
        public bool Verbose { get; set; } = false;
        public uint Timeout
        {
            get
            {
                return timeout;
            }
            set
            {
                timeout = value;
                if (serialPort != null)
                {
                    serialPort.ReadTimeout = (int)timeout;
                    serialPort.WriteTimeout = (int)timeout;
                }
            }
        }

        private SerialPort serialPort = null;
        private FTDI d2xxPort = null;
        private ushort maxReadPacketSize = DefaultmaxReadPacketSize;
        private ushort maxWritePacketSize = DefaultmaxWritePacketSize;
        private uint timeout;

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
            //PHI2_INIT = 15,
            //PHI2_INIT_DONE = 16,
            MIRRORING_REQUEST = 17,
            MIRRORING_RESULT = 18,
            RESET = 19,
            RESET_ACK = 20,
            //PRG_EPROM_WRITE_REQUEST = 21,
            //CHR_EPROM_WRITE_REQUEST = 22,
            //EPROM_PREPARE = 23,
            //PRG_FLASH_ERASE_REQUEST = 24,
            //PRG_FLASH_WRITE_REQUEST = 25,
            //CHR_FLASH_ERASE_REQUEST = 26,
            //CHR_FLASH_WRITE_REQUEST = 27,
            //TEST_SET = 32,
            //TEST_RESULT = 33,
            COOLBOY_READ_REQUEST = 34,
            COOLBOY_ERASE_SECTOR_REQUEST = 35,
            COOLBOY_WRITE_REQUEST = 36,
            FLASH_ERASE_SECTOR_REQUEST = 37,
            FLASH_WRITE_REQUEST = 38,
            PRG_CRC_READ_REQUEST = 39,
            CHR_CRC_READ_REQUEST = 40,
            FLASH_WRITE_ERROR = 41,
            FLASH_WRITE_TIMEOUT = 42,
            FLASH_ERASE_ERROR = 43,
            FLASH_ERASE_TIMEOUT = 44,

            BOOTLOADER = 0xFE,
            DEBUG = 0xFF
        }

        public enum MemoryAccessMethod
        {
            CoolboyGPIO,
            Direct
        }

        public FamicomDumperConnection(string portName = null)
        {
            this.PortName = portName;
            Timeout = 5000;
        }

        /// <summary>
        /// Method to obtain list of Linux USB devices
        /// </summary>
        /// <returns>Array of usb devices </returns>
        private static string[] GetLinuxUsbDevices()
        {
            return Directory.GetDirectories("/sys/bus/usb/devices").Where(d => File.Exists(Path.Combine(d, "dev"))).ToArray();
        }

        /// <summary>
        /// Method to get serial port path for specified USB converter
        /// </summary>
        /// <param name="deviceSerial">Serial number of USB to serial converter</param>
        /// <returns>Path of serial port</returns>
        private static string LinuxDeviceToPort(string device)
        {
            var subdirectories = Directory.GetDirectories(device);
            foreach (var subdir in subdirectories)
            {
                var subsubdirectories = Directory.GetDirectories(subdir);
                var ports = subsubdirectories.Where(d => Path.GetFileName(d).StartsWith("tty"));
                if (ports.Any())
                    return $"/dev/{Path.GetFileName(ports.First())}";
            }
            return null;
        }

        /// <summary>
        /// Method to get serial port path for specified USB converter
        /// </summary>
        /// <param name="deviceSerial">Serial number of USB to serial converter</param>
        /// <returns>Path of serial port</returns>
        private static string LinuxDeviceSerialToPort(string deviceSerial)
        {
            var devices = GetLinuxUsbDevices().Where(d =>
            {
                var serialFile = Path.Combine(d, "serial");
                return File.Exists(serialFile) && File.ReadAllText(serialFile).Trim() == deviceSerial;
            });
            if (!devices.Any()) return null;
            var device = devices.First();
            return LinuxDeviceToPort(device);
        }

        public void Open()
        {
            ProtocolVersion = 0;
            maxReadPacketSize = DefaultmaxReadPacketSize;
            maxWritePacketSize = DefaultmaxWritePacketSize;

            string portName = PortName;
            if (string.IsNullOrEmpty(portName) || portName.ToLower() == "auto")
            {
                if (!IsRunningOnMono()) // Is it running on Windows?
                {
                    // Using Windows FTDI driver to determine serial number
                    FTDI myFtdiDevice = new FTDI();
                    uint ftdiDeviceCount = 0;
                    FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;
                    // FTDI serial number autodetect
                    ftStatus = myFtdiDevice.GetNumberOfDevices(ref ftdiDeviceCount);
                    // Check status
                    if (ftStatus != FTDI.FT_STATUS.FT_OK)
                        throw new IOException("Failed to get number of devices (error " + ftStatus.ToString() + ")");

                    // If no devices available, return
                    if (ftdiDeviceCount == 0)
                        throw new IOException("Failed to get number of devices (error " + ftStatus.ToString() + ")");

                    // Allocate storage for device info list
                    FTDI.FT_DEVICE_INFO_NODE[] ftdiDeviceList = new FTDI.FT_DEVICE_INFO_NODE[ftdiDeviceCount];

                    // Populate our device list
                    ftStatus = myFtdiDevice.GetDeviceList(ftdiDeviceList);

                    portName = null;
                    if (ftStatus == FTDI.FT_STATUS.FT_OK)
                    {
                        var dumpers = ftdiDeviceList.Where(d => d.Description == DeviceName);
                        if (!dumpers.Any())
                            throw new IOException($"{DeviceName} not found");
                        portName = dumpers.First().SerialNumber;
                        Console.WriteLine($"Autodetected USB device serial number: {portName}");
                    }
                    if (ftStatus != FTDI.FT_STATUS.FT_OK)
                        throw new IOException("Failed to get FTDI devices (error " + ftStatus.ToString() + ")");
                }
                else
                {
                    // Linux?
                    var devices = GetLinuxUsbDevices();
                    var dumpers = devices.Where(d =>
                    {
                        var productFile = Path.Combine(d, "product");
                        return File.Exists(productFile) && File.ReadAllText(productFile).Trim() == DeviceName;
                    });
                    if (!dumpers.Any())
                        throw new IOException($"{DeviceName} not found");
                    portName = LinuxDeviceToPort(dumpers.First());
                    Console.WriteLine($"Autodetected USB device path: {portName}");
                }
            }

            if (portName.ToUpper().StartsWith("COM") || IsRunningOnMono())
            {
                if (IsRunningOnMono() && !portName.StartsWith("/dev/tty"))
                {
                    // Need to convert serial number to port address
                    var ttyPath = LinuxDeviceSerialToPort(portName);
                    if (string.IsNullOrEmpty(ttyPath))
                        throw new IOException($"Device with serial number {portName} not found");
                    portName = ttyPath;
                    Console.WriteLine($"Autodetected USB device path: {portName}");
                }
                // Port specified 
                SerialPort sPort;
                sPort = new SerialPort();
                sPort.PortName = portName;
                sPort.WriteTimeout = (int)Timeout; sPort.ReadTimeout = (int)Timeout;
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
                // It's Windows and serial number specified
                // Using Windows FTDI driver
                FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;
                // Create new instance of the FTDI device class
                FTDI myFtdiDevice = new FTDI();
                // Open first device in our list by serial number
                ftStatus = myFtdiDevice.OpenBySerialNumber(portName);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    throw new IOException("Failed to open device (error " + ftStatus.ToString() + ")");
                // Set data characteristics - Data bits, Stop bits, Parity
                ftStatus = myFtdiDevice.SetTimeouts(Timeout, Timeout);
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
        }

        private byte[] ReadPort()
        {
            var buffer = new byte[maxReadPacketSize + 8];
            if (serialPort != null)
            {
                var l = serialPort.Read(buffer, 0, buffer.Length);
                var result = new byte[l];
                Array.Copy(buffer, result, l);
                return result;
            }
            else if (d2xxPort != null)
            {
                UInt32 numBytesAvailable = 0;
                FTDI.FT_STATUS ftStatus;
                int t = 0;
                do
                {
                    ftStatus = d2xxPort.GetRxBytesAvailable(ref numBytesAvailable);
                    if (ftStatus != FTDI.FT_STATUS.FT_OK)
                        throw new IOException("Failed to get number of bytes available to read (error " + ftStatus.ToString() + ")");
                    if (numBytesAvailable > 0)
                        break;
                    Thread.Sleep(10);
                    t += 10;
                    if (t >= Timeout)
                        throw new TimeoutException("Read timeout");
                } while (numBytesAvailable == 0);
                uint numBytesRead = 0;
                ftStatus = d2xxPort.Read(buffer, Math.Min(numBytesAvailable, (uint)maxReadPacketSize + 8), ref numBytesRead);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    throw new IOException("Failed to read data (error " + ftStatus.ToString() + ")");
                var result = new byte[numBytesRead];
                Array.Copy(buffer, result, numBytesRead);
                return result;
            }
            return null;
        }

        void SendCommand(DumperCommand command, byte[] data)
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

        (DumperCommand Command, byte[] Data) RecvCommand()
        {
            int commRecvPos = 0;
            DumperCommand commRecvCommand = 0;
            int commRecvLength = 0;
            List<byte> recvBuffer = new List<byte>();
            while (true)
            {
                var data = ReadPort();
                foreach (var b in data)
                {
                    recvBuffer.Add(b);
                    switch (commRecvPos)
                    {
                        case 0:
                            if (b == Magic)
                                commRecvPos++;
                            else
                            {
                                recvBuffer.Clear();
                                continue;
                                //throw new InvalidDataException("Received invalid magic");
                            }
                            break;
                        case 1:
                            commRecvCommand = (DumperCommand)b;
                            commRecvPos++;
                            break;
                        case 2:
                            commRecvLength = b;
                            commRecvPos++;
                            break;
                        case 3:
                            commRecvLength |= b << 8;
                            commRecvPos++;
                            break;
                        default:
                            if (recvBuffer.Count == commRecvLength + 5)
                            {
                                // CRC
                                var calculatecCRC = CRC(recvBuffer);
                                if (calculatecCRC == 0)
                                {
                                    // CRC OK
                                    if (commRecvCommand == DumperCommand.ERROR_CRC)
                                        throw new InvalidDataException("Dumper reported CRC error");
                                    else if (commRecvCommand == DumperCommand.ERROR_INVALID)
                                        throw new InvalidDataException("Dumper reported invalid magic");
                                    else if (commRecvCommand == DumperCommand.ERROR_OVERFLOW)
                                        throw new InvalidDataException("Dumper reported overflow error");
                                    else
                                        return (commRecvCommand, recvBuffer.Skip(4).Take(commRecvLength).ToArray());
                                }
                                else
                                {
                                    // CRC NOT OK
                                    throw new InvalidDataException("Received data CRC error");
                                }
                            }
                            break;
                    }
                }
            }
        }

        byte CRC(IEnumerable<byte> data)
        {
            byte commRecvCrc = 0;
            foreach (var b in data)
            {
                var inbyte = b;
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
            return commRecvCrc;
        }

        public bool DumperInit()
        {
            if (Verbose)
                Console.Write("Dumper initialization... ");

            var oldTimeout = Timeout;
            try
            {
                Timeout = 100;
                for (int i = 0; i < 30; i++)
                {
                    try
                    {
                        SendCommand(DumperCommand.PRG_INIT, new byte[0]);
                        var recv = RecvCommand();
                        if (recv.Command == DumperCommand.STARTED)
                        {
                            if (recv.Data.Length >= 1)
                                ProtocolVersion = recv.Data[0];
                            if (recv.Data.Length >= 3)
                                maxReadPacketSize = (ushort)(recv.Data[1] | (recv.Data[2] << 8));
                            if (recv.Data.Length >= 5)
                                maxWritePacketSize = (ushort)(recv.Data[3] | (recv.Data[4] << 8));
                            return true;
                        }
                    }
                    catch { }
                }
            }
            finally
            {
                Timeout = oldTimeout;
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
                result.AddRange(ReadCpuBlock(address, Math.Min(maxReadPacketSize, length), flashType));
                address += maxReadPacketSize;
                length -= maxReadPacketSize;
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
            switch (flashType)
            {
                case MemoryAccessMethod.Direct:
                    SendCommand(DumperCommand.PRG_READ_REQUEST, buffer);
                    break;
                case MemoryAccessMethod.CoolboyGPIO:
                    SendCommand(DumperCommand.COOLBOY_READ_REQUEST, buffer);
                    break;
            }
            var recv = RecvCommand();
            if (recv.Command != DumperCommand.PRG_READ_RESULT)
                throw new IOException($"Invalid data received: {recv.Command}");
            return recv.Data;
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
            SendCommand(DumperCommand.PRG_CRC_READ_REQUEST, buffer);
            var recv = RecvCommand();
            if (recv.Command != DumperCommand.PRG_READ_RESULT)
                throw new IOException($"Invalid data received: {recv.Command}");
            return (ushort)(recv.Data[0] | (recv.Data[1] << 8));
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
                var wdata = new byte[Math.Min(maxWritePacketSize, wlength)];
                Array.Copy(data, pos, wdata, 0, wdata.Length);
                WriteCpuBlock(address, wdata);
                address += maxWritePacketSize;
                pos += maxWritePacketSize;
                wlength -= maxWritePacketSize;
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
            SendCommand(DumperCommand.PRG_WRITE_REQUEST, buffer);
            var recv = RecvCommand();
            if (recv.Command != DumperCommand.PRG_WRITE_DONE)
                throw new IOException($"Invalid data received: {recv.Command}");
        }

        public void EraseCpuFlashSector(MemoryAccessMethod flashType)
        {
            switch (flashType)
            {
                case MemoryAccessMethod.CoolboyGPIO:
                    SendCommand(DumperCommand.COOLBOY_ERASE_SECTOR_REQUEST, new byte[0]);
                    break;
                case MemoryAccessMethod.Direct:
                    SendCommand(DumperCommand.FLASH_ERASE_SECTOR_REQUEST, new byte[0]);
                    break;
            }
            var recv = RecvCommand();
            if (recv.Command == DumperCommand.FLASH_ERASE_ERROR)
                throw new IOException($"Flash erase error (0x{recv.Data[0]:X2})");
            else if (recv.Command == DumperCommand.FLASH_ERASE_TIMEOUT)
                throw new TimeoutException($"Flash erase timeout");
            else if (recv.Command != DumperCommand.PRG_WRITE_DONE)
                throw new IOException($"Invalid data received: {recv.Command}");
        }

        public void WriteCpuFlash(ushort address, byte[] data, MemoryAccessMethod flashType = MemoryAccessMethod.Direct)
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
            while (wlength > 0)
            {
                var wdata = new byte[Math.Min(maxWritePacketSize, wlength)];
                Array.Copy(data, pos, wdata, 0, wdata.Length);
                if (data.Select(b => b != 0xFF).Any()) // if there is any not FF byte
                    WriteCpuFlashBlock(address, wdata, flashType);
                address += maxWritePacketSize;
                pos += maxWritePacketSize;
                wlength -= maxWritePacketSize;
            }
            if (Verbose)
                Console.WriteLine(" OK");
        }

        private void WriteCpuFlashBlock(ushort address, byte[] data, MemoryAccessMethod flashType)
        {
            int length = data.Length;
            var buffer = new byte[4 + length];
            buffer[0] = (byte)(address & 0xFF);
            buffer[1] = (byte)((address >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);
            Array.Copy(data, 0, buffer, 4, length);
            switch (flashType)
            {
                case MemoryAccessMethod.CoolboyGPIO:
                    SendCommand(DumperCommand.COOLBOY_WRITE_REQUEST, buffer);
                    break;
                case MemoryAccessMethod.Direct:
                    SendCommand(DumperCommand.FLASH_WRITE_REQUEST, buffer);
                    break;
            }
            var recv = RecvCommand();
            if (recv.Command == DumperCommand.FLASH_WRITE_ERROR)
                throw new IOException($"Flash write error");
            else if (recv.Command == DumperCommand.FLASH_WRITE_TIMEOUT)
                throw new TimeoutException($"Flash write timeout");
            else if (recv.Command != DumperCommand.PRG_WRITE_DONE)
                throw new IOException($"Invalid data received: {recv.Command}");
        }

        public byte[] ReadPpu(ushort address, int length)
        {
            if (Verbose)
                Console.Write($"Reading 0x{length:X4}B <= 0x{address:X4} @ PPU...");
            var result = new List<byte>();
            while (length > 0)
            {
                result.AddRange(ReadPpuBlock(address, Math.Min(maxReadPacketSize, length)));
                address += maxReadPacketSize;
                length -= maxReadPacketSize;
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
            SendCommand(DumperCommand.CHR_READ_REQUEST, buffer);
            var recv = RecvCommand();
            if (recv.Command != DumperCommand.CHR_READ_RESULT)
                throw new IOException($"Invalid data received: {recv.Command}");
            return recv.Data;
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
            SendCommand(DumperCommand.CHR_CRC_READ_REQUEST, buffer);
            var recv = RecvCommand();
            if (recv.Command != DumperCommand.CHR_READ_RESULT)
                throw new IOException($"Invalid data received: {recv.Command}");
            return (ushort)(recv.Data[0] | (recv.Data[1] << 8));
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
            if (data.Length > maxWritePacketSize) // Split packets
            {
                int wlength = data.Length;
                int pos = 0;
                while (wlength > 0)
                {
                    var wdata = new byte[Math.Min(maxWritePacketSize, wlength)];
                    Array.Copy(data, pos, wdata, 0, wdata.Length);
                    WritePpu(address, wdata);
                    address += maxWritePacketSize;
                    pos += maxWritePacketSize;
                    wlength -= maxWritePacketSize;
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
            SendCommand(DumperCommand.CHR_WRITE_REQUEST, buffer);
            var recv = RecvCommand();
            if (recv.Command != DumperCommand.CHR_WRITE_DONE)
                throw new IOException($"Invalid data received: {recv.Command}");
        }

        public bool[] GetMirroring()
        {
            if (Verbose)
                Console.Write("Reading mirroring... ");
            SendCommand(DumperCommand.MIRRORING_REQUEST, new byte[0]);
            var recv = RecvCommand();
            if (recv.Command != DumperCommand.MIRRORING_RESULT)
                throw new IOException($"Invalid data received: {recv.Command}");
            var mirroring = recv.Data;
            foreach (var b in mirroring)
                Console.Write($"{b} ");
            Console.WriteLine();
            return mirroring.Select(v => v != 0).ToArray();
        }

        /// <summary>
        /// Simulate reset (M2 goes to Z-state for a second)
        /// </summary>
        public void Reset()
        {
            SendCommand(DumperCommand.RESET, new byte[0]);
            var recv = RecvCommand();
            if (recv.Command != DumperCommand.RESET_ACK)
                throw new IOException($"Invalid data received: {recv.Command}");
        }

        public void Bootloader()
        {
            SendCommand(DumperCommand.BOOTLOADER, new byte[0]);
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
