using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReVibranceGUI.Helpers
{
    public static class IconHelper
    {
        public static ImageSource? GetIcon(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                using var sysicon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
                if (sysicon == null) return null;

                var source = Imaging.CreateBitmapSourceFromHIcon(
                    sysicon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze(); // Allows safe use across threads and lets WPF release the HICON copy.
                return source;
            }
            catch (Exception ex)
            {
                Services.Logger.Warn($"Icon extraction failed for {filePath}", ex);
                return null;
            }
        }
    }
}
