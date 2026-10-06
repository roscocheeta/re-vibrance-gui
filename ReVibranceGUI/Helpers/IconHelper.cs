using System;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReVibranceGUI.Helpers
{
    public static class IconHelper
    {
        public static ImageSource GetIcon(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                return null;

            try
            {
                using (Icon sysicon = Icon.ExtractAssociatedIcon(filePath))
                {
                    if (sysicon != null)
                    {
                        return Imaging.CreateBitmapSourceFromHIcon(
                            sysicon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                    }
                }
            }
            catch
            {
                // Ignore extraction errors
            }
            return null;
        }
    }
}
