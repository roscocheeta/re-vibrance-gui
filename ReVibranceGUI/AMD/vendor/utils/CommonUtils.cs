using System;
using System.IO;

namespace vibrance.GUI.AMD.vendor.utils
{
    public static class CommonUtils
    {
        public static string GetVibrance_GUI_AppDataPath()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vibranceGUI");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        // Removed LoadUnmanagedLibraryFromResource (S4): it was unused, wrote a DLL into
        // user-writable %AppData% and then loaded it by bare name via LoadLibrary, which is
        // a DLL-planting / search-order-hijacking pattern.
    }
}