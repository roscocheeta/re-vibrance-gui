using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace ReVibranceGUI.Models
{
    public class GameProfile : INotifyPropertyChanged
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ExeName { get; set; } = string.Empty;
        public string ExePath { get; set; } = string.Empty;

        private int _vibranceLevel = 100;
        public int VibranceLevel
        {
            get => _vibranceLevel;
            set { _vibranceLevel = value; OnPropertyChanged(); }
        }

        private string _targetDisplay = "Primary";
        public string TargetDisplay
        {
            get => _targetDisplay;
            set { _targetDisplay = value; OnPropertyChanged(); }
        }

        private bool _changeResolution = false;
        public bool ChangeResolution
        {
            get => _changeResolution;
            set { _changeResolution = value; OnPropertyChanged(); }
        }

        private string _targetResolution = string.Empty;
        public string TargetResolution
        {
            get => _targetResolution;
            set { _targetResolution = value; OnPropertyChanged(); }
        }

        private bool _showAdvanced = false;
        [JsonIgnore]
        public bool ShowAdvanced
        {
            get => _showAdvanced;
            set { _showAdvanced = value; OnPropertyChanged(); }
        }

        private ImageSource? _iconImage;
        [JsonIgnore]
        public ImageSource? IconImage
        {
            get => _iconImage;
            set { _iconImage = value; OnPropertyChanged(); }
        }

        private bool _isRunning = false;
        [JsonIgnore]
        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
