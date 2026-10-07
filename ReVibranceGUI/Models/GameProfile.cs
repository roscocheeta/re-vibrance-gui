using System.Text.Json.Serialization;
using System.Windows.Media;

namespace ReVibranceGUI.Models
{
    public class GameProfile
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ExeName { get; set; } = string.Empty;
        public string ExePath { get; set; } = string.Empty;
        public int VibranceLevel { get; set; } = 100;

        public string TargetDisplay { get; set; } = "Primary";
        public bool ChangeResolution { get; set; } = false;
        public string TargetResolution { get; set; } = string.Empty;

        [JsonIgnore]
        public bool ShowAdvanced { get; set; } = false;

        [JsonIgnore]
        public ImageSource? IconImage { get; set; }
    }
}
