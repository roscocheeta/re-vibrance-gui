using System;
using System.Collections.Generic;
using System.Linq;
using IGCLWrapper;
using ReVibranceGUI.Services;

namespace ReVibranceGUI.Intel
{
    public class ModernIntelVibranceProxy : IDisposable
    {
        public bool IsInitialized { get; private set; }
        private IGCLApiHelper? _api;

        // Adapter/media helpers are cached: enumerating devices through IGCL on every
        // read/write (the UI polls every few seconds) is needlessly expensive.
        private readonly object _cacheLock = new();
        private List<(IGCLAdapterHelper Adapter, IGCLMediaHelper Media)>? _cache;

        public ModernIntelVibranceProxy()
        {
            try
            {
                _api = IGCLApiHelper.Initialize();
                IsInitialized = true;
                Logger.Info("IGCL initialized.");
            }
            catch (Exception ex)
            {
                IsInitialized = false;
                Logger.Info($"IGCL not available ({ex.GetType().Name}); Intel support disabled.");
            }
        }

        /// <summary>Returns the cached adapters, enumerating once on first use.</summary>
        private List<(IGCLAdapterHelper Adapter, IGCLMediaHelper Media)> GetAdapters()
        {
            lock (_cacheLock)
            {
                if (_cache != null) return _cache;

                var list = new List<(IGCLAdapterHelper, IGCLMediaHelper)>();
                foreach (var adapter in _api!.EnumerateAdapters())
                {
                    list.Add((adapter, _api.GetMediaHelper(adapter)));
                }
                _cache = list;
                return _cache;
            }
        }

        private void InvalidateCache()
        {
            lock (_cacheLock)
            {
                if (_cache == null) return;
                foreach (var (adapter, media) in _cache)
                {
                    media.Dispose();
                    adapter.Dispose();
                }
                _cache = null;
            }
        }

        public string GetGpuNames()
        {
            if (!IsInitialized || _api == null) return "Intel GPU";
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

        public int GetCurrentVibranceLevel()
        {
            if (!IsInitialized || _api == null) return 50;

            try
            {
                var primary = GetAdapters().FirstOrDefault();
                if (primary.Media != null)
                {
                    var currentSettings = primary.Media.GetStandardColorCorrection();
                    if (currentSettings.HasValue)
                    {
                        return VibranceMath.IntelToUi(currentSettings.Value.Saturation);
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

        /// <remarks>
        /// IGCL color correction is adapter-wide, so <paramref name="targetDisplay"/> cannot be honoured;
        /// the change always applies to every Intel adapter.
        /// </remarks>
        public void SetVibranceLevel(int level, string targetDisplay = "All")
        {
            if (!IsInitialized || _api == null) return;

            try
            {
                float saturation = VibranceMath.UiToIntel(level);
                foreach (var (_, media) in GetAdapters())
                {
                    var currentSettings = media.GetStandardColorCorrection();

                    if (currentSettings.HasValue)
                    {
                        var newSettings = currentSettings.Value;
                        newSettings.Enable = true;
                        newSettings.Saturation = saturation;

                        media.SetStandardColorCorrection(newSettings);
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
            _api?.Dispose();
            _api = null;
        }
    }
}
