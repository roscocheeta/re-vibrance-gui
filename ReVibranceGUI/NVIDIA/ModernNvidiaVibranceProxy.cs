using NvAPIWrapper.Display;
using ReVibranceGUI.Services;

namespace ReVibranceGUI.Nvidia
{
    public class ModernNvidiaVibranceProxy
    {
        public bool IsInitialized { get; private set; }

        public ModernNvidiaVibranceProxy()
        {
            try
            {
                NvAPIWrapper.NVIDIA.Initialize();
                IsInitialized = true;
                Logger.Info("NVAPI initialized.");
            }
            catch (Exception ex)
            {
                IsInitialized = false;
                Logger.Info($"NVAPI not available ({ex.GetType().Name}); NVIDIA support disabled.");
            }
        }

        public string GetGpuNames()
        {
            if (!IsInitialized) return "NVIDIA GPU";
            try
            {
                var gpus = NvAPIWrapper.GPU.PhysicalGPU.GetPhysicalGPUs();
                if (gpus is { Length: > 0 })
                {
                    return string.Join(" / ", gpus.Select(g => g.FullName).Distinct());
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Error getting NVIDIA GPU names", ex);
            }
            return "NVIDIA GPU";
        }

        public int GetCurrentVibranceLevel()
        {
            if (!IsInitialized) return 50;

            try
            {
                string? primaryDeviceName = DisplayManager.GetDisplays().FirstOrDefault(d => d.IsPrimary)?.DeviceName;
                var displays = Display.GetDisplays();
                var primary = displays.FirstOrDefault(d => d.Name == primaryDeviceName) ?? displays.FirstOrDefault();

                if (primary != null)
                {
                    return VibranceMath.NvidiaToUi(primary.DigitalVibranceControl.CurrentLevel);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Error reading NVIDIA vibrance level", ex);
            }
            return 50;
        }

        public void SetVibranceLevel(int level, string targetDisplay = "All")
        {
            if (!IsInitialized) return;

            try
            {
                int nativeLevel = VibranceMath.UiToNvidia(level);
                string? primaryDeviceName = targetDisplay == "Primary" ? DisplayManager.GetDisplays().FirstOrDefault(d => d.IsPrimary)?.DeviceName : null;

                foreach (var display in Display.GetDisplays())
                {
                    if (targetDisplay == "All" || (targetDisplay == "Primary" && display.Name == primaryDeviceName))
                    {
                        display.DigitalVibranceControl.CurrentLevel = nativeLevel;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting NVIDIA vibrance to {level}", ex);
            }
        }
    }
}
