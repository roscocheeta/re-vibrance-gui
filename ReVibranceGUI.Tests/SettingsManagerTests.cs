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
                PauseHotkey = "Ctrl+Shift+P"
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
            Assert.Single(deserialized.GameProfiles);

            var profile = deserialized.GameProfiles.First();
            Assert.Equal("Test Game", profile.DisplayName);
            Assert.Equal("test.exe", profile.ExeName);
            Assert.Equal(75, profile.VibranceLevel);
        }
    }
}
