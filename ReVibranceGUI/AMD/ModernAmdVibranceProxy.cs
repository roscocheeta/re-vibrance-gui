using System;
using vibrance.GUI.AMD.vendor;

namespace ReVibranceGUI.AMD
{
    public class ModernAmdVibranceProxy
    {
        private IAmdAdapter _amdAdapter;
        public bool IsInitialized { get; private set; }

        public ModernAmdVibranceProxy()
        {
            try
            {
                if (Environment.Is64BitProcess)
                {
                    _amdAdapter = new AmdAdapter64();
                }
                else
                {
                    _amdAdapter = new AmdAdapter32();
                }
                
                IsInitialized = _amdAdapter.IsAvailable();
                if (IsInitialized)
                {
                    _amdAdapter.Init();
                }
            }
            catch (Exception ex)
            {
                IsInitialized = false;
                Console.WriteLine($"Failed to initialize ADL: {ex.Message}");
            }
        }

        public string GetGpuNames()
        {
            if (!IsInitialized || _amdAdapter == null) return "AMD GPU";
            
            try
            {
                var names = _amdAdapter.GetAdapterNames();
                if (names != null && names.Count > 0)
                {
                    return string.Join(" / ", names);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting AMD GPU names: {ex.Message}");
            }
            return "AMD GPU";
        }

        public int GetCurrentVibranceLevel()
        {
            if (!IsInitialized) return 50;
            // The original vibranceGUI ADL wrapper does not expose a getter for saturation.
            // We assume default Windows saturation is 50%
            return 50; 
        }

        public void SetVibranceLevel(int level)
        {
            if (!IsInitialized) return;
            
            // UI gives us 50-100%. AMD native is typically 100 to 200 (where 100 is default, 200 is max)
            // 50% -> 100
            // 100% -> 200
            int nativeLevel = level * 2;
            
            if (nativeLevel < 100) nativeLevel = 100;
            if (nativeLevel > 300) nativeLevel = 300; // Some AMD drivers allow up to 300

            _amdAdapter.SetSaturationOnAllDisplays(nativeLevel);
        }
    }
}
