namespace ReVibranceGUI.Services
{
    /// <summary>
    /// Pure conversion functions between the UI vibrance scale (50–100%)
    /// and each vendor's native range. Kept free of driver calls so they can be unit tested.
    /// </summary>
    public static class VibranceMath
    {
        public const int UiMin = 50;
        public const int UiMax = 100;

        public const int NvidiaNativeMax = 63;

        public static int ClampUi(int level) => Math.Clamp(level, UiMin, UiMax);

        /// <summary>UI 50–100 → NVIDIA DVC 0–63.</summary>
        public static int UiToNvidia(int uiLevel)
        {
            double fraction = (ClampUi(uiLevel) - UiMin) / (double)(UiMax - UiMin);
            return (int)Math.Round(fraction * NvidiaNativeMax);
        }

        /// <summary>NVIDIA DVC 0–63 → UI 50–100.</summary>
        public static int NvidiaToUi(int nativeLevel)
        {
            int clamped = Math.Clamp(nativeLevel, 0, NvidiaNativeMax);
            return UiMin + (int)Math.Round(clamped / (double)NvidiaNativeMax * (UiMax - UiMin));
        }

        /// <summary>UI 50–100 → AMD ADL saturation 100–200 (100 = driver default).</summary>
        public static int UiToAmd(int uiLevel) => ClampUi(uiLevel) * 2;
    }
}
