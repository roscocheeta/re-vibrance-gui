using System;
using System.Linq;
using IGCLWrapper;
using ReVibranceGUI.Services;

namespace ReVibranceGUI.Intel
{
    public class ModernIntelVibranceProxy : IDisposable
    {
        public bool IsInitialized { get; private set; }
        private IGCLApiHelper? _api;

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

        public string GetGpuNames()
        {
            if (!IsInitialized || _api == null) return "Intel GPU";
            try
            {
                var adapters = _api.EnumerateAdapters();
                if (adapters != null && adapters.Any())
                {
                    return "Intel Graphics";
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Error getting Intel GPU names", ex);
            }
            return "Intel GPU";
        }

        public int GetCurrentVibranceLevel()
        {
            if (!IsInitialized || _api == null) return 50;

            try
            {
                var adapters = _api.EnumerateAdapters();
                var primaryAdapter = adapters.FirstOrDefault();
                if (primaryAdapter != null)
                {
                    var mediaHelper = _api.GetMediaHelper(primaryAdapter);
                    var currentSettings = mediaHelper.GetStandardColorCorrection();
                    if (currentSettings.HasValue)
                    {
                        var settings = currentSettings.Value;
                        // Map 0-100 float to 0-100 int
                        return (int)Math.Round(settings.Saturation);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Error reading Intel vibrance level", ex);
            }
            return 50;
        }

        public void SetVibranceLevel(int level, string targetDisplay = "All")
        {
            if (!IsInitialized || _api == null) return;

            try
            {
                foreach (var adapter in _api.EnumerateAdapters())
                {
                    var mediaHelper = _api.GetMediaHelper(adapter);
                    var currentSettings = mediaHelper.GetStandardColorCorrection();

                    if (currentSettings.HasValue)
                    {
                        var newSettings = currentSettings.Value;
                        newSettings.Enable = true;
                        newSettings.Saturation = (float)level; // 0.0f to 100.0f

                        mediaHelper.SetStandardColorCorrection(newSettings);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting Intel vibrance to {level}", ex);
            }
        }

        public void Dispose()
        {
            _api?.Dispose();
        }
    }
}
