using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace com.clusterrr.Famicom.DumperConnection
{
    public class Win32DeviceMgmt
    {
        [Flags]
        public enum DiGetClassFlags : uint
        {
            DIGCF_DEFAULT = 0x00000001,  // only valid with DIGCF_DEVICEINTERFACE
            DIGCF_PRESENT = 0x00000002,
            DIGCF_ALLCLASSES = 0x00000004,
            DIGCF_PROFILE = 0x00000008,
            DIGCF_DEVICEINTERFACE = 0x00000010,
        }
        /// <summary>
        /// Device registry property codes
        /// </summary>
        public enum SPDRP : uint
        {
            /// <summary>
            /// DeviceDesc (R/W)
            /// </summary>
            SPDRP_DEVICEDESC = 0x00000000,

            /// <summary>
            /// HardwareID (R/W)
            /// </summary>
            SPDRP_HARDWAREID = 0x00000001,

            /// <summary>
            /// CompatibleIDs (R/W)
            /// </summary>
            SPDRP_COMPATIBLEIDS = 0x00000002,

            /// <summary>
            /// unused
            /// </summary>
            SPDRP_UNUSED0 = 0x00000003,

            /// <summary>
            /// Service (R/W)
            /// </summary>
            SPDRP_SERVICE = 0x00000004,

            /// <summary>
            /// unused
            /// </summary>
            SPDRP_UNUSED1 = 0x00000005,

            /// <summary>
            /// unused
            /// </summary>
            SPDRP_UNUSED2 = 0x00000006,

            /// <summary>
            /// Class (R--tied to ClassGUID)
            /// </summary>
            SPDRP_CLASS = 0x00000007,

            /// <summary>
            /// ClassGUID (R/W)
            /// </summary>
            SPDRP_CLASSGUID = 0x00000008,

            /// <summary>
            /// Driver (R/W)
            /// </summary>
            SPDRP_DRIVER = 0x00000009,

            /// <summary>
            /// ConfigFlags (R/W)
            /// </summary>
            SPDRP_CONFIGFLAGS = 0x0000000A,

            /// <summary>
            /// Mfg (R/W)
            /// </summary>
            SPDRP_MFG = 0x0000000B,

            /// <summary>
            /// FriendlyName (R/W)
            /// </summary>
            SPDRP_FRIENDLYNAME = 0x0000000C,

            /// <summary>
            /// LocationInformation (R/W)
            /// </summary>
            SPDRP_LOCATION_INFORMATION = 0x0000000D,

            /// <summary>
            /// PhysicalDeviceObjectName (R)
            /// </summary>
            SPDRP_PHYSICAL_DEVICE_OBJECT_NAME = 0x0000000E,

            /// <summary>
            /// Capabilities (R)
            /// </summary>
            SPDRP_CAPABILITIES = 0x0000000F,

            /// <summary>
            /// UiNumber (R)
            /// </summary>
            SPDRP_UI_NUMBER = 0x00000010,

            /// <summary>
            /// UpperFilters (R/W)
            /// </summary>
            SPDRP_UPPERFILTERS = 0x00000011,

            /// <summary>
            /// LowerFilters (R/W)
            /// </summary>
            SPDRP_LOWERFILTERS = 0x00000012,

            /// <summary>
            /// BusTypeGUID (R)
            /// </summary>
            SPDRP_BUSTYPEGUID = 0x00000013,

            /// <summary>
            /// LegacyBusType (R)
            /// </summary>
            SPDRP_LEGACYBUSTYPE = 0x00000014,

            /// <summary>
            /// BusNumber (R)
            /// </summary>
            SPDRP_BUSNUMBER = 0x00000015,

            /// <summary>
            /// Enumerator Name (R)
            /// </summary>
            SPDRP_ENUMERATOR_NAME = 0x00000016,

            /// <summary>
            /// Security (R/W, binary form)
            /// </summary>
            SPDRP_SECURITY = 0x00000017,

            /// <summary>
            /// Security (W, SDS form)
            /// </summary>
            SPDRP_SECURITY_SDS = 0x00000018,

            /// <summary>
            /// Device Type (R/W)
            /// </summary>
            SPDRP_DEVTYPE = 0x00000019,

            /// <summary>
            /// Device is exclusive-access (R/W)
            /// </summary>
            SPDRP_EXCLUSIVE = 0x0000001A,

            /// <summary>
            /// Device Characteristics (R/W)
            /// </summary>
            SPDRP_CHARACTERISTICS = 0x0000001B,

            /// <summary>
            /// Device Address (R)
            /// </summary>
            SPDRP_ADDRESS = 0x0000001C,

            /// <summary>
            /// UiNumberDescFormat (R/W)
            /// </summary>
            SPDRP_UI_NUMBER_DESC_FORMAT = 0X0000001D,

            /// <summary>
            /// Device Power Data (R)
            /// </summary>
            SPDRP_DEVICE_POWER_DATA = 0x0000001E,

            /// <summary>
            /// Removal Policy (R)
            /// </summary>
            SPDRP_REMOVAL_POLICY = 0x0000001F,

            /// <summary>
            /// Hardware Removal Policy (R)
            /// </summary>
            SPDRP_REMOVAL_POLICY_HW_DEFAULT = 0x00000020,

            /// <summary>
            /// Removal Policy Override (RW)
            /// </summary>
            SPDRP_REMOVAL_POLICY_OVERRIDE = 0x00000021,

            /// <summary>
            /// Device Install State (R)
            /// </summary>
            SPDRP_INSTALL_STATE = 0x00000022,

            /// <summary>
            /// Device Location Paths (R)
            /// </summary>
            SPDRP_LOCATION_PATHS = 0x00000023,
        }
        private const UInt32 DICS_FLAG_GLOBAL = 0x00000001;
        private const UInt32 DIREG_DEV = 0x00000001;
        private const UInt32 KEY_QUERY_VALUE = 0x0001;

        /// <summary>
        /// The SP_DEVINFO_DATA structure defines a device instance that is a member of a device information set.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public UInt32 cbSize;
            public Guid ClassGuid;
            public UInt32 DevInst;
            public UIntPtr Reserved;
        };


        [StructLayout(LayoutKind.Sequential)]
        struct DEVPROPKEY
        {
            public Guid fmtid;
            public UInt32 pid;
        }


        [DllImport("setupapi.dll")]
        private static extern Int32 SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, UInt32 MemberIndex, ref SP_DEVINFO_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid gClass, UInt32 iEnumerator, UInt32 hParent, DiGetClassFlags nFlags);

        [DllImport("Setupapi", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetupDiOpenDevRegKey(IntPtr hDeviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint scope,
            uint hwProfile, uint parameterRegistryValueKind, uint samDesired);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryValueExW", SetLastError = true)]
        private static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, int lpReserved, out uint lpType,
            byte[] lpData, ref uint lpcbData);

        [DllImport("advapi32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int RegCloseKey(IntPtr hKey);

        [DllImport("kernel32.dll")]
        private static extern Int32 GetLastError();

        const int BUFFER_SIZE = 1024;

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiClassGuidsFromName(string ClassName,
            ref Guid ClassGuidArray1stItem, UInt32 ClassGuidArraySize,
            out UInt32 RequiredSize);

        [DllImport("setupapi.dll")]
        private static extern Int32 SetupDiClassNameFromGuid(ref Guid ClassGuid,
            StringBuilder className, Int32 ClassNameSize, ref Int32 RequiredSize);

        /// <summary>
        /// The SetupDiGetDeviceRegistryProperty function retrieves the specified device property.
        /// This handle is typically returned by the SetupDiGetClassDevs or SetupDiGetClassDevsEx function.
        /// </summary>
        /// <param Name="DeviceInfoSet">Handle to the device information set that contains the interface and its underlying device.</param>
        /// <param Name="DeviceInfoData">Pointer to an SP_DEVINFO_DATA structure that defines the device instance.</param>
        /// <param Name="Property">Device property to be retrieved. SEE MSDN</param>
        /// <param Name="PropertyRegDataType">Pointer to a variable that receives the registry data Type. This parameter can be NULL.</param>
        /// <param Name="PropertyBuffer">Pointer to a buffer that receives the requested device property.</param>
        /// <param Name="PropertyBufferSize">Size of the buffer, in bytes.</param>
        /// <param Name="RequiredSize">Pointer to a variable that receives the required buffer size, in bytes. This parameter can be NULL.</param>
        /// <returns>If the function succeeds, the return value is nonzero.</returns>
        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            SPDRP Property,
            out UInt32 PropertyRegDataType,
            byte[] PropertyBuffer,
            uint PropertyBufferSize,
            out UInt32 RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiGetDevicePropertyW(
            IntPtr deviceInfoSet,
            [In] ref SP_DEVINFO_DATA DeviceInfoData,
            [In] ref DEVPROPKEY propertyKey,
            [Out] out UInt32 propertyType,
            byte[] propertyBuffer,
            UInt32 propertyBufferSize,
            out UInt32 requiredSize,
            UInt32 flags);

        const int utf16terminatorSize_bytes = 2;

        public struct DeviceInfo
        {
            public string name;
            public string description;
            public string bus_description;
        }

        static DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc;

        static Win32DeviceMgmt()
        {
            DEVPKEY_Device_BusReportedDeviceDesc = new DEVPROPKEY();
            DEVPKEY_Device_BusReportedDeviceDesc.fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2);
            DEVPKEY_Device_BusReportedDeviceDesc.pid = 4;
        }

        public static List<DeviceInfo> GetAllCOMPorts()
        {
            Guid[] guids = GetClassGUIDs("Ports");
            List<DeviceInfo> devices = new List<DeviceInfo>();
            for (int index = 0; index < guids.Length; index++)
            {
                IntPtr hDeviceInfoSet = SetupDiGetClassDevs(ref guids[index], 0, 0, DiGetClassFlags.DIGCF_PRESENT);
                if (hDeviceInfoSet == IntPtr.Zero)
                {
                    throw new Exception("Failed to get device information set for the COM ports");
                }

                try
                {
                    UInt32 iMemberIndex = 0;
                    while (true)
                    {
                        SP_DEVINFO_DATA deviceInfoData = new SP_DEVINFO_DATA();
                        deviceInfoData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                        bool success = SetupDiEnumDeviceInfo(hDeviceInfoSet, iMemberIndex, ref deviceInfoData);
                        if (!success)
                        {
                            // No more devices in the device information set
                            break;
                        }

                        DeviceInfo deviceInfo = new DeviceInfo();
                        deviceInfo.name = GetDeviceName(hDeviceInfoSet, deviceInfoData);
                        deviceInfo.description = GetDeviceDescription(hDeviceInfoSet, deviceInfoData);
                        deviceInfo.bus_description = GetDeviceBusDescription(hDeviceInfoSet, deviceInfoData);
                        devices.Add(deviceInfo);

                        iMemberIndex++;
                    }
                }
                finally
                {
                    SetupDiDestroyDeviceInfoList(hDeviceInfoSet);
                }
            }
            return devices;
        }

        private static string GetDeviceName(IntPtr pDevInfoSet, SP_DEVINFO_DATA deviceInfoData)
        {
            IntPtr hDeviceRegistryKey = SetupDiOpenDevRegKey(pDevInfoSet, ref deviceInfoData,
                DICS_FLAG_GLOBAL, 0, DIREG_DEV, KEY_QUERY_VALUE);
            if (hDeviceRegistryKey == IntPtr.Zero)
            {
                throw new Exception("Failed to open a registry key for device-specific configuration information");
            }

            byte[] ptrBuf = new byte[BUFFER_SIZE];
            uint length = (uint)ptrBuf.Length;
            try
            {
                uint lpRegKeyType;
                int result = RegQueryValueEx(hDeviceRegistryKey, "PortName", 0, out lpRegKeyType, ptrBuf, ref length);
                if (result != 0)
                {
                    //throw new Exception("Can not read registry value PortName for device " + deviceInfoData.ClassGuid);
                    return null;
                }
            }
            finally
            {
                RegCloseKey(hDeviceRegistryKey);
            }

            return Encoding.Unicode.GetString(ptrBuf, 0, (int)length - utf16terminatorSize_bytes);
        }

        private static string GetDeviceDescription(IntPtr hDeviceInfoSet, SP_DEVINFO_DATA deviceInfoData)
        {
            byte[] ptrBuf = new byte[BUFFER_SIZE];
            uint propRegDataType;
            uint RequiredSize;
            bool success = SetupDiGetDeviceRegistryProperty(hDeviceInfoSet, ref deviceInfoData, SPDRP.SPDRP_DEVICEDESC,
                out propRegDataType, ptrBuf, BUFFER_SIZE, out RequiredSize);
            if (!success)
            {
                //throw new Exception("Can not read registry value PortName for device " + deviceInfoData.ClassGuid);
                return null;
            }
            return Encoding.Unicode.GetString(ptrBuf, 0, (int)RequiredSize - utf16terminatorSize_bytes);
        }

        private static string GetDeviceBusDescription(IntPtr hDeviceInfoSet, SP_DEVINFO_DATA deviceInfoData)
        {
            byte[] ptrBuf = new byte[BUFFER_SIZE];
            uint propRegDataType;
            uint RequiredSize;
            bool success = SetupDiGetDevicePropertyW(hDeviceInfoSet, ref deviceInfoData, ref DEVPKEY_Device_BusReportedDeviceDesc,
                out propRegDataType, ptrBuf, BUFFER_SIZE, out RequiredSize, 0);
            if (!success)
            {
                //throw new Exception("Can not read Bus provided device description device " + deviceInfoData.ClassGuid);
                return null;
            }
            return System.Text.UnicodeEncoding.Unicode.GetString(ptrBuf, 0, (int)RequiredSize - utf16terminatorSize_bytes);
        }

        private static Guid[] GetClassGUIDs(string className)
        {
            UInt32 requiredSize = 0;
            Guid[] guidArray = new Guid[1];

            bool status = SetupDiClassGuidsFromName(className, ref guidArray[0], 1, out requiredSize);
            if (true == status)
            {
                if (1 < requiredSize)
                {
                    guidArray = new Guid[requiredSize];
                    SetupDiClassGuidsFromName(className, ref guidArray[0], requiredSize, out requiredSize);
                }
            }
            else
                throw new System.ComponentModel.Win32Exception();

            return guidArray;
        }
    }
}