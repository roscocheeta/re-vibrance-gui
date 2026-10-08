using System;
using System.IO;
using System.Linq;
using ReVibranceGUI;
using ReVibranceGUI.Models;
using ReVibranceGUI.Services;
using Xunit;

namespace ReVibranceGUI.Tests
{
    public class SettingsManagerTests
    {
        [Fact]
        public void DefaultSettings_AreCorrect()
        {
            var settings = new AppSettings();

            Assert.False(settings.MinimizeToTray);
            Assert.Equal("Auto", settings.Theme);
            Assert.False(settings.EnablePauseHotkey);
            Assert.Equal(string.Empty, settings.PauseHotkey);
            Assert.False(settings.AutoStartMonitoring);

            Assert.Empty(settings.GameProfiles);
        }

        [Fact]
        public void Serialization_MaintainsDataIntegrity()
        {
            var settings = new AppSettings
            {
                MinimizeToTray = true,
                Theme = "Dark",
                EnablePauseHotkey = true,
                PauseHotkey = "Ctrl+Shift+P",
                AutoStartMonitoring = true
            };

            settings.GameProfiles.Add(new GameProfile
            {
                DisplayName = "Test Game",
                ExeName = "test.exe",
                ExePath = @"C:\Games\test.exe",
                VibranceLevel = 75
            });

            // We mock the save path by temporarily overriding AppData in our test context if possible,
            // but SettingsManager currently hardcodes Environment.GetFolderPath.
            // For a pure unit test, we test the JSON string generation directly if we refactor,
            // or we just test serialization/deserialization logic.

            string json = System.Text.Json.JsonSerializer.Serialize(settings);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

            Assert.NotNull(deserialized);
            Assert.True(deserialized.MinimizeToTray);
            Assert.Equal("Dark", deserialized.Theme);
            Assert.True(deserialized.EnablePauseHotkey);
            Assert.Equal("Ctrl+Shift+P", deserialized.PauseHotkey);
            Assert.True(deserialized.AutoStartMonitoring);
            Assert.Single(deserialized.GameProfiles);

            var profile = deserialized.GameProfiles.First();
            Assert.Equal("Test Game", profile.DisplayName);
            Assert.Equal("test.exe", profile.ExeName);
            Assert.Equal(75, profile.VibranceLevel);
        }
        [Fact]
        public void Sanitize_RejectsUncPaths()
        {
            var settings = new AppSettings();
            settings.GameProfiles.Add(new GameProfile { ExeName = "test.exe", ExePath = @"\\192.168.1.1\share\test.exe" });
            settings.GameProfiles.Add(new GameProfile { ExeName = "test2.exe", ExePath = @"//10.0.0.1/share/test2.exe" });
            settings.GameProfiles.Add(new GameProfile { ExeName = "test3.exe", ExePath = @"C:\Games\test3.exe" });

            var sanitized = SettingsManager.Sanitize(settings);

            Assert.Equal(string.Empty, sanitized.GameProfiles[0].ExePath);
            Assert.Equal(string.Empty, sanitized.GameProfiles[1].ExePath);
            Assert.Equal(@"C:\Games\test3.exe", sanitized.GameProfiles[2].ExePath);
        }

        [Fact]
        public void Sanitize_CapsStringLengths()
        {
            var settings = new AppSettings();
            var longName = new string('A', 300);
            var longPath = new string('B', 600);
            settings.GameProfiles.Add(new GameProfile { ExeName = longName, DisplayName = longName, ExePath = longPath });

            var sanitized = SettingsManager.Sanitize(settings);

            Assert.Equal(255, sanitized.GameProfiles[0].ExeName.Length);
            Assert.Equal(255, sanitized.GameProfiles[0].DisplayName.Length);
            Assert.Equal(500, sanitized.GameProfiles[0].ExePath.Length);
        }

        [Fact]
        public void Sanitize_ValidatesTargetResolution()
        {
            var settings = new AppSettings();
            settings.GameProfiles.Add(new GameProfile { ExeName = "valid1.exe", TargetResolution = "1920x1080" });
            settings.GameProfiles.Add(new GameProfile { ExeName = "valid2.exe", TargetResolution = "2560x1440@144Hz" });
            settings.GameProfiles.Add(new GameProfile { ExeName = "invalid1.exe", TargetResolution = "1920x1080@144Hz@60Hz" });
            settings.GameProfiles.Add(new GameProfile { ExeName = "invalid2.exe", TargetResolution = "drop table users;" });
            settings.GameProfiles.Add(new GameProfile { ExeName = "invalid3.exe", TargetResolution = "1920x" });

            var sanitized = SettingsManager.Sanitize(settings);

            Assert.Equal("1920x1080", sanitized.GameProfiles[0].TargetResolution);
            Assert.Equal("2560x1440@144Hz", sanitized.GameProfiles[1].TargetResolution);
            Assert.Equal(string.Empty, sanitized.GameProfiles[2].TargetResolution);
            Assert.Equal(string.Empty, sanitized.GameProfiles[3].TargetResolution);
            Assert.Equal(string.Empty, sanitized.GameProfiles[4].TargetResolution);
        }
    }
}
