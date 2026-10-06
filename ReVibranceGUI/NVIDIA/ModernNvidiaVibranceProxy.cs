using System;
using System.Linq;
using NvAPIWrapper;
using NvAPIWrapper.Display;

namespace ReVibranceGUI.Nvidia
{
    public class ModernNvidiaVibranceProxy
    {
        public bool IsInitialized { get; private set; }

        public ModernNvidiaVibranceProxy()
        {
            try
            {
                // Initialize the NVIDIA API
                NvAPIWrapper.NVIDIA.Initialize();
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                IsInitialized = false;
                Console.WriteLine($"Failed to initialize NvAPIWrapper: {ex.Message}");
            }
        }

        public string GetGpuNames()
        {
            if (!IsInitialized) return "NVIDIA GPU";
            try
            {
                var gpus = NvAPIWrapper.GPU.PhysicalGPU.GetPhysicalGPUs();
                if (gpus != null && gpus.Length > 0)
                {
                    var names = gpus.Select(g => g.FullName).Distinct().ToList();
                    return string.Join(" / ", names);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting NVIDIA GPU names: {ex.Message}");
            }
            return "NVIDIA GPU";
        }

        public int GetCurrentVibranceLevel()
        {
            if (!IsInitialized) return 50;

            try
            {
                var displays = Display.GetDisplays();
                var primary = displays.FirstOrDefault();
                if (primary != null)
                {
                    var dvcInfo = primary.DigitalVibranceControl;
                    int nativeLevel = dvcInfo.CurrentLevel;
                    
                    // Map Native (0-63) back to UI (50-100)
                    return 50 + (int)Math.Round((nativeLevel / 63.0) * 50.0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting vibrance level: {ex.Message}");
            }
            return 50;
        }

        public void SetVibranceLevel(int level)
        {
            if (!IsInitialized) return;

            try
            {
                // UI gives us 50-100. Map to Native 0-63.
                // 50 -> 0, 100 -> 63
                int nativeLevel = (int)Math.Round(((level - 50) / 50.0) * 63.0);
                
                // Clamp
                if (nativeLevel < 0) nativeLevel = 0;
                if (nativeLevel > 63) nativeLevel = 63;

                var displays = Display.GetDisplays();
                foreach (var display in displays)
                {
                    var dvcInfo = display.DigitalVibranceControl;
                    dvcInfo.CurrentLevel = nativeLevel;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting vibrance level: {ex.Message}");
            }
        }
    }
}
