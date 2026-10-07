using System;

namespace ReVibranceGUI.Services
{
    /// <summary>
    /// Pure conversion functions between the UI vibrance scale (0-100%)
    /// and each vendor's native range. Kept free of driver calls so they can be unit tested.
    /// </summary>
    public static class VibranceMath
    {
        public const int UiMin = 0;
        public const int UiMax = 100;

        public static int ClampUi(int level) => Math.Clamp(level, UiMin, UiMax);

        /// <summary>NVIDIA DVC natively accepts 0-100 in NvAPIWrapper.</summary>
        public static int UiToNvidia(int uiLevel) => ClampUi(uiLevel);

        /// <summary>NVIDIA DVC 0-100 to UI 0-100.</summary>
        public static int NvidiaToUi(int nativeLevel) => ClampUi(nativeLevel);

        /// <summary>AMD ADL natively accepts 0-200 for saturation, where 100 is default. UI is 0-100 (50 default), so we multiply by 2.</summary>
        public static int UiToAmd(int uiLevel) => ClampUi(uiLevel) * 2;

        /// <summary>AMD ADL 0-200 to UI 0-100.</summary>
        public static int AmdToUi(int nativeLevel) => ClampUi(nativeLevel / 2);

        /// <summary>Intel IGCL saturation is passed as a 0-100 float; UI is 0-100, so this is a clamped identity.</summary>
        public static float UiToIntel(int uiLevel) => ClampUi(uiLevel);

        /// <summary>Intel IGCL saturation float to UI 0-100 (rounded, clamped; NaN maps to the neutral 50).</summary>
        public static int IntelToUi(float nativeLevel) =>
            float.IsNaN(nativeLevel) ? 50 : ClampUi((int)Math.Round(Math.Clamp(nativeLevel, UiMin, UiMax)));
    }
}
