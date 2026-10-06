using System;
using System.Drawing;
using System.IO;
using Svg;

namespace SvgToIco
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var svgDoc = SvgDocument.Open(@"..\icon\gemini-svg.svg");
                // Convert to a 256x256 bitmap
                svgDoc.Width = 256;
                svgDoc.Height = 256;
                using (var bitmap = svgDoc.Draw())
                {
                    using (var fs = new FileStream(@"..\icon\app_icon.ico", FileMode.Create))
                    {
                        // Write ICO header
                        fs.WriteByte(0);
                        fs.WriteByte(0);
                        fs.WriteByte(1); // 1 = ICO
                        fs.WriteByte(0);
                        fs.WriteByte(1); // 1 image
                        fs.WriteByte(0);
                        
                        // Image details
                        fs.WriteByte(0); // Width 256 (0 means 256)
                        fs.WriteByte(0); // Height 256 (0 means 256)
                        fs.WriteByte(0); // Color palette
                        fs.WriteByte(0); // Reserved
                        fs.WriteByte(0); // Color planes
                        fs.WriteByte(0);
                        fs.WriteByte(32); // Bits per pixel
                        fs.WriteByte(0);
                        
                        // Size of image data in bytes (placeholder, will seek back)
                        long sizePos = fs.Position;
                        fs.Write(new byte[4], 0, 4);
                        
                        // Offset of image data
                        fs.Write(BitConverter.GetBytes((int)fs.Position + 4), 0, 4);
                        
                        // Write image as PNG format (valid in ICO for 256x256)
                        long imgStart = fs.Position;
                        bitmap.Save(fs, System.Drawing.Imaging.ImageFormat.Png);
                        long imgEnd = fs.Position;
                        
                        // Seek back and write size
                        fs.Seek(sizePos, SeekOrigin.Begin);
                        fs.Write(BitConverter.GetBytes((int)(imgEnd - imgStart)), 0, 4);
                    }
                }
                Console.WriteLine("Successfully created app_icon.ico");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
