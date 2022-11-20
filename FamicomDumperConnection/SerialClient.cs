using com.clusterrr.Famicom.DumperConnection;
using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace com.clusterrr.Communication
{
    public class SerialClient : IDisposable
    {
        const byte Magic = 0x46;
        public ushort MaxReadPacketSize { get; set; } = 1024;
        public ushort MaxWritePacketSize { get; set; } = 1024;
        private uint timeout = 10000;
        private SerialPort serialPort = null;
        private FTDI d2xxPort = null;

        public void Open(string portNameOrSerial, uint baudRate, uint timeout = 10000, string[] deviceNames = null)
        {
            this.timeout = timeout;

            Close(); // Close if already opened

            bool portAutodetect = (string.IsNullOrEmpty(portNameOrSerial) || portNameOrSerial.ToLower() == "auto");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // First of all lets check bus reported device descriptions
                if (portAutodetect)
                {
                    var allComPorts = Win32DeviceMgmt.GetAllCOMPorts();
                    var matched = allComPorts.Where(p => deviceNames.Contains(p.bus_description));
                    if (matched.Any())
                    {
                        var portName = matched.First().name;
                        Console.WriteLine($"Autodetected virtual serial port: {portName}");
                        OpenPortByName(portName, baudRate, timeout);
                        return;
                    }
                }
                // Let's try FTDI driver if Windows... legacy stuff
                if (!portNameOrSerial.StartsWith("COM"))
                {
                    FTDI myFtdiDevice = null;
                    FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;
                    try
                    {
                        // Using Windows FTDI driver
                        ftStatus = FTDI.FT_STATUS.FT_OK;
                        // Create new instance of the FTDI device class
                        myFtdiDevice = new();
                    }
                    catch (IOException)
                    {
                        // FTDI driver not installed, ignore it
                    }
                    if (myFtdiDevice != null)
                    {
                        if (!portAutodetect)
                        {
                            // Is is FTDI serial number?
                            // Open first device in our list by serial number
                            ftStatus = myFtdiDevice.OpenBySerialNumber(portNameOrSerial);
                            if (ftStatus == FTDI.FT_STATUS.FT_OK)
                            {
                                if (myFtdiDevice.GetCOMPort(out string portName) != FTDI.FT_STATUS.FT_OK)
                                    throw new IOException($"Failed to get FTDI serial port name (error {ftStatus})");
                                myFtdiDevice.Close();
                                Console.WriteLine($"Autodetected virtual serial port: {portName}");
                                OpenPortByName(portName, baudRate, timeout);
                                return;
                            }
                        }
                        else
                        {
                            // Autodetect by device name
                            bool found = false;
                            foreach (var name in deviceNames)
                            {
                                ftStatus = myFtdiDevice.OpenByDescription(name);
                                if (ftStatus == FTDI.FT_STATUS.FT_OK)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (!found) throw new IOException($"Can't autodetect serial port, try to specify it manually");
                            if (myFtdiDevice.GetCOMPort(out string portName) != FTDI.FT_STATUS.FT_OK)
                                throw new IOException($"Failed to get FTDI serial port name (error {ftStatus})");
                            myFtdiDevice.Close();
                            Console.WriteLine($"Autodetected virtual serial port: {portName}");
                            OpenPortByName(portName, baudRate, timeout);
                            return;
                        }
                    } // FTDI OK
                } // not COM
            } // Windows
            else
            {
                // not Windows
                if (portAutodetect)
                {
                    // Linux autodetect
                    var usbDevices = Directory.GetDirectories("/sys/bus/usb/devices").Where(d => File.Exists(Path.Combine(d, "dev"))).ToArray();
                    var portDevices = usbDevices.Where(d =>
                    {
                        var productFile = Path.Combine(d, "product");
                        return File.Exists(productFile) && deviceNames.Contains(File.ReadAllText(productFile).Trim());
                    });
                    if (portDevices.Any())
                    {
                        var portName = LinuxDeviceToPort(portDevices.First());
                        if (!string.IsNullOrEmpty(portName))
                        {
                            Console.WriteLine($"Autodetected virtual serial port: {portName}");
                            OpenPortByName(portName, baudRate, timeout);
                            return;
                        }
                    }
                    throw new IOException($"Can't autodetect serial port, try to specify it manually");
                }
            }

            // Just open port
            OpenPortByName(portNameOrSerial, baudRate, timeout);
            return;
        }

        private void OpenPortByName(string portName, uint baudRate, uint timeout)
        {
            var port = new SerialPort
            {
                PortName = portName,
                WriteTimeout = (int)timeout,
                ReadTimeout = (int)timeout
            };
            if (!portName.Contains("ttyACM"))
            {
                // Not supported by ACM devices
                port.BaudRate = (int)baudRate;
                port.Parity = Parity.None;
                port.DataBits = 8;
                port.StopBits = StopBits.One;
                port.Handshake = Handshake.None;
                port.DtrEnable = false;
                port.RtsEnable = false;
            }
            port.NewLine = Environment.NewLine;
            port.Open();
            serialPort = port;
        }

        public void Close()
        {
            if (serialPort != null)
            {
                serialPort.Close();
                serialPort = null;
            }
            if (d2xxPort != null)
            {
                d2xxPort.Close();
                d2xxPort = null;
            }
        }

        private byte[] ReadPort()
        {
            var buffer = new byte[MaxReadPacketSize + 8];
            if (serialPort != null)
            {
                var l = serialPort.Read(buffer, 0, buffer.Length);
                var result = new byte[l];
                Array.Copy(buffer, result, l);
                return result;
            }
            else if (d2xxPort != null)
            {
                uint numBytesAvailable = 0;
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
                ftStatus = d2xxPort.Read(buffer, Math.Min(numBytesAvailable, (uint)MaxReadPacketSize + 8), ref numBytesRead);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    throw new IOException("Failed to read data (error " + ftStatus.ToString() + ")");
                var result = new byte[numBytesRead];
                Array.Copy(buffer, result, numBytesRead);
                return result;
            }
            return null;
        }

        public void SendCommand(byte command, byte[] data)
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
            buffer[^1] = crc;
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

        public (byte Command, byte[] Data) RecvCommand()
        {
            int commRecvPos = 0;
            byte commRecvCommand = 0;
            int commRecvLength = 0;
            List<byte> recvBuffer = new();
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
                                // Wait for valid magic
                                recvBuffer.Clear();
                                continue;
                            }
                            break;
                        case 1:
                            commRecvCommand = b;
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
                                    return (commRecvCommand, recvBuffer.Skip(4).Take(commRecvLength).ToArray());
                                else
                                    throw new InvalidDataException("Received data CRC error");
                            }
                            break;
                    }
                }
            }
        }

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
                if (d2xxPort != null)
                {
                    d2xxPort.SetTimeouts((uint)timeout, (uint)timeout);
                }
            }
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
                // Searching for /sys/bus/usb/devices/{device}/xxx/ttyZZZ/
                var subsubdirectories = Directory.GetDirectories(subdir);
                var ports = subsubdirectories.Where(d =>
                {
                    var directory = Path.GetFileName(d);
                    return directory.Length > 3 && directory.StartsWith("tty");
                });
                if (ports.Any())
                    return $"/dev/{Path.GetFileName(ports.First())}";

                // Searching for /sys/bus/usb/devices/{device}/xxx/tty/ttyZZZ/
                var ttyDirectory = Path.Combine(subdir, "tty");
                if (Directory.Exists(ttyDirectory))
                {
                    ports = Directory.GetDirectories(ttyDirectory).Where(d =>
                    {
                        var directory = Path.GetFileName(d);
                        return directory.Length > 3 && directory.StartsWith("tty");
                    });
                    if (ports.Any())
                        return $"/dev/{Path.GetFileName(ports.First())}";
                }
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

        static byte CRC(IEnumerable<byte> data)
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

        public void Dispose()
        {
            Close();
        }
    }
}
