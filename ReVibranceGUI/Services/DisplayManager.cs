using System.Runtime.InteropServices;

namespace ReVibranceGUI.Services
{
    public class DisplayResolution
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshRate { get; set; }

        public override string ToString() => $"{Width} x {Height} @ {RefreshRate} Hz";

        public static DisplayResolution? Parse(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            var parts = val.Split(new[] { 'x', '@', 'H', 'z', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
            {
                int r = parts.Length > 2 && int.TryParse(parts[2], out int rate) ? rate : 0;
                return new DisplayResolution { Width = w, Height = h, RefreshRate = r };
            }
            return null;
        }

        /// <summary>
        /// Cheap sanity bounds for a requested mode. Not a substitute for <c>CDS_TEST</c>, but stops
        /// absurd values (e.g. from a hand-edited settings file) before they reach the driver.
        /// </summary>
        public static bool IsPlausible(int width, int height, int refreshRate) =>
            width is >= 320 and <= 16384 &&
            height is >= 200 and <= 16384 &&
            refreshRate is >= 0 and <= 1000;
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
        // CDS_FULLSCREEN makes the mode change temporary: it is NOT written to the registry, and Windows
        // reverts it automatically if this process exits or crashes. (CDS_UPDATEREGISTRY would persist it.)
        private const int CDS_FULLSCREEN = 0x04;
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

        // Passing a NULL DEVMODE tells Windows to restore the registry (user-configured) mode.
        [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsExReset(string? lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        private static readonly object ChangeLock = new();

        // Devices (null device name stored as "") whose mode WE changed and therefore must restore.
        private static readonly HashSet<string> ChangedDevices = new(StringComparer.OrdinalIgnoreCase);

        private static string DeviceKey(string? deviceName) => deviceName ?? string.Empty;


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
            if (!DisplayResolution.IsPlausible(width, height, refreshRate))
            {
                Logger.Warn($"Ignoring implausible resolution request {width}x{height}@{refreshRate}");
                return false;
            }

            lock (ChangeLock)
            {
                DEVMODE vDevMode = new();
                vDevMode.dmSize = (short)Marshal.SizeOf(vDevMode);

                if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref vDevMode))
                {
                    return false;
                }

                // Already at the requested mode: nothing to change (and nothing for us to restore).
                if (vDevMode.dmPelsWidth == width && vDevMode.dmPelsHeight == height &&
                    (refreshRate <= 0 || vDevMode.dmDisplayFrequency == refreshRate))
                {
                    return true;
                }

                vDevMode.dmPelsWidth = width;
                vDevMode.dmPelsHeight = height;
                if (refreshRate > 0)
                {
                    vDevMode.dmDisplayFrequency = refreshRate;
                }
                vDevMode.dmFields = 0x00080000 | 0x00100000; // DM_PELSWIDTH | DM_PELSHEIGHT
                if (refreshRate > 0) vDevMode.dmFields |= 0x00400000; // DM_DISPLAYFREQUENCY

                // Validate first so an unsupported mode can never blank the display.
                int test = ChangeDisplaySettingsEx(deviceName, ref vDevMode, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
                if (test != DISP_CHANGE_SUCCESSFUL)
                {
                    Logger.Warn($"Display mode {width}x{height}@{refreshRate} rejected by driver (code {test}).");
                    return false;
                }

                int result = ChangeDisplaySettingsEx(deviceName, ref vDevMode, IntPtr.Zero, CDS_FULLSCREEN, IntPtr.Zero);
                if (result == DISP_CHANGE_SUCCESSFUL)
                {
                    ChangedDevices.Add(DeviceKey(deviceName));
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Restores the user's configured (registry) mode for one device, but only if this app changed it.
        /// </summary>
        public static void RestoreResolution(string? deviceName = null)
        {
            lock (ChangeLock)
            {
                if (!ChangedDevices.Remove(DeviceKey(deviceName))) return;
                ChangeDisplaySettingsExReset(deviceName, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            }
        }

        /// <summary>
        /// Restores every device this app changed. Cheap no-op when nothing was changed, so it is safe
        /// to call on every profile switch and on exit.
        /// </summary>
        public static void RestoreAll()
        {
            lock (ChangeLock)
            {
                foreach (string key in ChangedDevices.ToList())
                {
                    ChangeDisplaySettingsExReset(key.Length == 0 ? null : key, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
                }
                ChangedDevices.Clear();
            }
        }
    }
}
