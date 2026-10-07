using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ReVibranceGUI.Services;

namespace ReVibranceGUI.Intel
{
    public class ModernIntelVibranceProxy : IDisposable
    {
        public bool IsInitialized { get; private set; }
        private IntPtr _apiHandle = IntPtr.Zero;
        private List<IntPtr>? _cache;
        private readonly object _cacheLock = new();

        public ModernIntelVibranceProxy()
        {
            try
            {
                var initArgs = new ctl_init_args_t
                {
                    Size = (uint)Marshal.SizeOf(typeof(ctl_init_args_t)),
                    Version = 0,
                    flags = 0
                };
                
                var result = ControlLib.ctlInit(ref initArgs, out _apiHandle);
                if (result == ctl_result_t.CTL_RESULT_SUCCESS && _apiHandle != IntPtr.Zero)
                {
                    IsInitialized = true;
                    Logger.Info("IGCL initialized via lightweight P/Invoke.");
                }
                else
                {
                    IsInitialized = false;
                    Logger.Info($"IGCL init failed ({result}); Intel support disabled.");
                }
            }
            catch (Exception ex)
            {
                IsInitialized = false;
                Logger.Info($"IGCL not available ({ex.GetType().Name}); Intel support disabled.");
            }
        }

        private List<IntPtr> GetAdapters()
        {
            lock (_cacheLock)
            {
                if (_cache != null) return _cache;

                var list = new List<IntPtr>();
                uint count = 0;
                if (ControlLib.ctlEnumerateDevices(_apiHandle, ref count, null) == ctl_result_t.CTL_RESULT_SUCCESS && count > 0)
                {
                    var devices = new IntPtr[count];
                    if (ControlLib.ctlEnumerateDevices(_apiHandle, ref count, devices) == ctl_result_t.CTL_RESULT_SUCCESS)
                    {
                        list.AddRange(devices);
                    }
                }
                _cache = list;
                return _cache;
            }
        }

        private void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cache = null;
            }
        }

        public string GetGpuNames()
        {
            if (!IsInitialized || _apiHandle == IntPtr.Zero) return "Intel GPU";
            try
            {
                if (GetAdapters().Count > 0)
                {
                    return "Intel Graphics";
                }
            }
            catch (Exception ex)
            {
                InvalidateCache();
                Logger.Warn("Error getting Intel GPU names", ex);
            }
            return "Intel GPU";
        }

        private ctl_video_processing_standard_color_correction_t? GetColorCorrection(IntPtr adapter)
        {
            int size = Marshal.SizeOf(typeof(ctl_video_processing_standard_color_correction_t));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                var scc = new ctl_video_processing_standard_color_correction_t
                {
                    Size = (uint)size,
                    Version = 0
                };
                Marshal.StructureToPtr(scc, ptr, false);

                var getset = new ctl_video_processing_feature_getset_t
                {
                    Size = (uint)Marshal.SizeOf(typeof(ctl_video_processing_feature_getset_t)),
                    Version = 0,
                    FeatureType = ctl_video_processing_feature_t.CTL_VIDEO_PROCESSING_FEATURE_STANDARD_COLOR_CORRECTION,
                    bSet = false,
                    ValueType = ctl_property_value_type_t.CTL_PROPERTY_VALUE_TYPE_CUSTOM,
                    CustomValueSize = size,
                    pCustomValue = ptr
                };

                if (ControlLib.ctlGetSetVideoProcessingFeature(adapter, ref getset) == ctl_result_t.CTL_RESULT_SUCCESS)
                {
                    return Marshal.PtrToStructure<ctl_video_processing_standard_color_correction_t>(ptr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return null;
        }

        private bool SetColorCorrection(IntPtr adapter, ctl_video_processing_standard_color_correction_t scc)
        {
            int size = Marshal.SizeOf(typeof(ctl_video_processing_standard_color_correction_t));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(scc, ptr, false);

                var getset = new ctl_video_processing_feature_getset_t
                {
                    Size = (uint)Marshal.SizeOf(typeof(ctl_video_processing_feature_getset_t)),
                    Version = 0,
                    FeatureType = ctl_video_processing_feature_t.CTL_VIDEO_PROCESSING_FEATURE_STANDARD_COLOR_CORRECTION,
                    bSet = true,
                    ValueType = ctl_property_value_type_t.CTL_PROPERTY_VALUE_TYPE_CUSTOM,
                    CustomValueSize = size,
                    pCustomValue = ptr
                };

                return ControlLib.ctlGetSetVideoProcessingFeature(adapter, ref getset) == ctl_result_t.CTL_RESULT_SUCCESS;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public int GetCurrentVibranceLevel()
        {
            if (!IsInitialized || _apiHandle == IntPtr.Zero) return 50;

            try
            {
                var primary = GetAdapters().FirstOrDefault();
                if (primary != IntPtr.Zero)
                {
                    var currentSettings = GetColorCorrection(primary);
                    if (currentSettings.HasValue)
                    {
                        return VibranceMath.IntelToUi(currentSettings.Value.saturation);
                    }
                }
            }
            catch (Exception ex)
            {
                InvalidateCache();
                Logger.Warn("Error reading Intel vibrance level", ex);
            }
            return 50;
        }

        public void SetVibranceLevel(int level, string targetDisplay = "All")
        {
            if (!IsInitialized || _apiHandle == IntPtr.Zero) return;

            try
            {
                float saturation = VibranceMath.UiToIntel(level);
                foreach (var adapter in GetAdapters())
                {
                    var currentSettings = GetColorCorrection(adapter);
                    if (currentSettings.HasValue)
                    {
                        var newSettings = currentSettings.Value;
                        newSettings.standard_color_correction_enable = true;
                        newSettings.saturation = saturation;

                        SetColorCorrection(adapter, newSettings);
                    }
                }
            }
            catch (Exception ex)
            {
                InvalidateCache();
                Logger.Error($"Error setting Intel vibrance to {level}", ex);
            }
        }

        public void Dispose()
        {
            InvalidateCache();
            if (_apiHandle != IntPtr.Zero)
            {
                try { ControlLib.ctlClose(_apiHandle); } catch { }
                _apiHandle = IntPtr.Zero;
            }
            IsInitialized = false;
        }
    }
}
