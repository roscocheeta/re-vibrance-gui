using System.Runtime.InteropServices;

namespace ReVibranceGUI.Services
{
    public class DisplayResolution
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshRate { get; set; }

        public override string ToString() => $"{Width} x {Height} @ {RefreshRate}Hz";
    }

    public class DisplayDevice
    {
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceString { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }

        public override string ToString() => IsPrimary ? $"{DeviceString} (Primary)" : DeviceString;
    }

    public static class DisplayManager
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int ENUM_REGISTRY_SETTINGS = -2;
        private const int CDS_UPDATEREGISTRY = 0x01;
        private const int CDS_TEST = 0x02;
        private const int DISP_CHANGE_SUCCESSFUL = 0;
        private const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;
        private const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x4;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);


        public static List<DisplayDevice> GetDisplays()
        {
            var displays = new List<DisplayDevice>();
            uint i = 0;
            DISPLAY_DEVICE d = new();
            d.cb = Marshal.SizeOf(d);

            while (EnumDisplayDevices(null, i, ref d, 0))
            {
                if ((d.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == DISPLAY_DEVICE_ATTACHED_TO_DESKTOP)
                {
                    displays.Add(new DisplayDevice
                    {
                        DeviceName = d.DeviceName,
                        DeviceString = d.DeviceString,
                        IsPrimary = (d.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) == DISPLAY_DEVICE_PRIMARY_DEVICE
                    });
                }
                i++;
            }
            return displays;
        }

        public static List<DisplayResolution> GetSupportedResolutions(string? deviceName = null)
        {
            var resolutions = new List<DisplayResolution>();
            DEVMODE vDevMode = new();
            vDevMode.dmSize = (short)Marshal.SizeOf(vDevMode);

            int i = 0;
            var seen = new HashSet<string>();
            while (EnumDisplaySettings(deviceName, i, ref vDevMode))
            {
                if (vDevMode.dmBitsPerPel == 32)
                {
                    var res = new DisplayResolution
                    {
                        Width = vDevMode.dmPelsWidth,
                        Height = vDevMode.dmPelsHeight,
                        RefreshRate = vDevMode.dmDisplayFrequency
                    };
                    string key = $"{res.Width}x{res.Height}@{res.RefreshRate}";
                    if (seen.Add(key))
                    {
                        resolutions.Add(res);
                    }
                }
                i++;
            }
            return resolutions.OrderByDescending(r => r.Width).ThenByDescending(r => r.Height).ThenByDescending(r => r.RefreshRate).ToList();
        }

        public static DisplayResolution? GetCurrentResolution(string? deviceName = null)
        {
            DEVMODE vDevMode = new();
            vDevMode.dmSize = (short)Marshal.SizeOf(vDevMode);

            if (EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref vDevMode))
            {
                return new DisplayResolution
                {
                    Width = vDevMode.dmPelsWidth,
                    Height = vDevMode.dmPelsHeight,
                    RefreshRate = vDevMode.dmDisplayFrequency
                };
            }
            return null;
        }

        public static bool SetResolution(string? deviceName, int width, int height, int refreshRate)
        {
            DEVMODE vDevMode = new();
            vDevMode.dmSize = (short)Marshal.SizeOf(vDevMode);

            if (EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref vDevMode))
            {
                vDevMode.dmPelsWidth = width;
                vDevMode.dmPelsHeight = height;
                if (refreshRate > 0)
                {
                    vDevMode.dmDisplayFrequency = refreshRate;
                }
                vDevMode.dmFields = 0x00080000 | 0x00100000; // DM_PELSWIDTH | DM_PELSHEIGHT
                if (refreshRate > 0) vDevMode.dmFields |= 0x00400000; // DM_DISPLAYFREQUENCY

                int result = ChangeDisplaySettingsEx(deviceName, ref vDevMode, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
                return result == DISP_CHANGE_SUCCESSFUL;
            }
            return false;
        }

        public static void RestoreResolution(string? deviceName = null)
        {
            // Passing a default DEVMODE with 0 size or IntPtr.Zero resets to registry settings.
            // A common trick is to use EnumDisplaySettings with ENUM_REGISTRY_SETTINGS.
            DEVMODE vDevMode = new();
            vDevMode.dmSize = (short)Marshal.SizeOf(vDevMode);

            if (EnumDisplaySettings(deviceName, ENUM_REGISTRY_SETTINGS, ref vDevMode))
            {
                ChangeDisplaySettingsEx(deviceName, ref vDevMode, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
            }
        }
    }
}
