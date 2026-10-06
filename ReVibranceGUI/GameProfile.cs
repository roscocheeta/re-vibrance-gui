using System.Windows.Media;
using System.Text.Json.Serialization;

namespace ReVibranceGUI
{
    public class GameProfile
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ExeName { get; set; } = string.Empty;
        public string ExePath { get; set; } = string.Empty;
        public int VibranceLevel { get; set; } = 100;

        [JsonIgnore]
        public ImageSource IconImage { get; set; }
    }
}
