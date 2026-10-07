using System.Windows.Media;
using System.Text.Json.Serialization;

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
        public int ResolutionWidth { get; set; } = 0;
        public int ResolutionHeight { get; set; } = 0;
        public int RefreshRate { get; set; } = 0;

        [JsonIgnore]
        public ImageSource? IconImage { get; set; }
    }
}
