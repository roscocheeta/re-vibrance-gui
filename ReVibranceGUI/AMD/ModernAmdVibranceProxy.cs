using ReVibranceGUI.Services;
using vibrance.GUI.AMD.vendor;

namespace ReVibranceGUI.AMD
{
    public class ModernAmdVibranceProxy
    {
        private readonly IAmdAdapter? _amdAdapter;
        public bool IsInitialized { get; private set; }

        public ModernAmdVibranceProxy()
        {
            try
            {
                _amdAdapter = Environment.Is64BitProcess ? new AmdAdapter64() : new AmdAdapter32();

                IsInitialized = _amdAdapter.IsAvailable();
                if (IsInitialized)
                {
                    _amdAdapter.Init();
                    Logger.Info("AMD ADL initialized.");
                }
            }
            catch (Exception ex)
            {
                IsInitialized = false;
                Logger.Info($"AMD ADL not available ({ex.GetType().Name}); AMD support disabled.");
            }
        }

        public string GetGpuNames()
        {
            if (!IsInitialized || _amdAdapter == null) return "AMD GPU";

            try
            {
                var names = _amdAdapter.GetAdapterNames();
                if (names is { Count: > 0 })
                {
                    return string.Join(" / ", names);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Error getting AMD GPU names", ex);
            }
            return "AMD GPU";
        }

        public int GetCurrentVibranceLevel()
        {
            // The upstream ADL wrapper does not expose a saturation getter; assume driver default.
            return VibranceMath.UiMin;
        }

        public void SetVibranceLevel(int level)
        {
            if (!IsInitialized || _amdAdapter == null) return;

            try
            {
                _amdAdapter.SetSaturationOnAllDisplays(VibranceMath.UiToAmd(level));
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting AMD saturation for level {level}", ex);
            }
        }
    }
}
