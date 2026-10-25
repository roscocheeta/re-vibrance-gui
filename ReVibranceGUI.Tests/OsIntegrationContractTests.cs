using System;
using System.IO;
using Microsoft.Win32;
using ReVibranceGUI.Services;
using Xunit;

namespace ReVibranceGUI.Tests
{
    /// <summary>
    /// OS boundary contract tests verifying integration with the host operating system
    /// (Windows Registry, Display settings, file system boundaries, and headless runner safety).
    /// </summary>
    public class OsIntegrationContractTests
    {
        [Fact]
        public void RegistryContract_UserRunKeyArea_CanReadAndWrite()
        {
            const string testSubKey = @"Software\ReVibranceGUI_ContractTest";
            const string testValueName = "TestStartupEntry";
            const string testValueData = "\"C:\\Test\\App.exe\" -startup";

            try
            {
                // Verify write capability in HKCU
                using (var key = Registry.CurrentUser.CreateSubKey(testSubKey))
                {
                    Assert.NotNull(key);
                    key.SetValue(testValueName, testValueData);
                }

                // Verify read-back capability
                using (var readKey = Registry.CurrentUser.OpenSubKey(testSubKey, writable: false))
                {
                    Assert.NotNull(readKey);
                    var val = readKey.GetValue(testValueName) as string;
                    Assert.Equal(testValueData, val);
                }

                // Verify deletion capability
                using (var deleteKey = Registry.CurrentUser.OpenSubKey(testSubKey, writable: true))
                {
                    Assert.NotNull(deleteKey);
                    deleteKey.DeleteValue(testValueName, throwOnMissingValue: false);
                    Assert.Null(deleteKey.GetValue(testValueName));
                }
            }
            finally
            {
                // Clean up contract test key
                try
                {
                    Registry.CurrentUser.DeleteSubKey(testSubKey, throwOnMissingSubKey: false);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void DisplayManager_HeadlessSessionGuard_DoesNotThrow()
        {
            // In headless CI runners (e.g., GitHub Actions Session 0), no physical monitor exists.
            // DisplayManager must safely return without throwing unhandled COM or Win32 exceptions.
            var resolutions = DisplayManager.GetSupportedResolutions();
            Assert.NotNull(resolutions);

            var current = DisplayManager.GetCurrentResolution();
            // In headless runners current might be null, but querying it must not throw.
            if (current != null)
            {
                Assert.True(DisplayResolution.IsPlausible(current.Width, current.Height, current.RefreshRate));
            }
        }

        [Fact]
        public void SettingsManager_AtomicFileWriteContract_RecoversFromCorruptedState()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ReVibranceGUI_Contract_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string tempSettingsFile = Path.Combine(tempDir, "settings.json");

            try
            {
                // Write corrupted / malformed JSON
                File.WriteAllText(tempSettingsFile, "{ this is invalid JSON [!@#$ ");

                // SettingsManager should log an error and safely return defaults without throwing
                var loaded = SettingsManager.Load(tempSettingsFile);
                Assert.NotNull(loaded);
                Assert.Equal("Auto", loaded.Theme);
                Assert.False(loaded.MinimizeToTray);

                // Now test atomic save
                loaded.Theme = "Dark";
                loaded.MinimizeToTray = true;
                SettingsManager.Save(loaded, tempSettingsFile);

                Assert.True(File.Exists(tempSettingsFile));
                var reloaded = SettingsManager.Load(tempSettingsFile);
                Assert.Equal("Dark", reloaded.Theme);
                Assert.True(reloaded.MinimizeToTray);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch
                {
                    // Ignore temp cleanup errors
                }
            }
        }

        [Theory]
        [InlineData("cs2.exe", "cs2")]
        [InlineData("CS2.EXE", "cs2")]
        [InlineData("Valorant", "valorant")]
        [InlineData("  game.exe  ", "game")]
        [InlineData(@"C:\Games\Game.exe", "game")]
        [InlineData(@"D:/Games/Sub/Game.exe", "game")]
        public void ProcessNormalization_HandlesEdgeCasesAndPaths(string input, string expected)
        {
            string normalized = VibranceAutomator.NormalizeProcessName(input);
            Assert.Equal(expected, normalized);
        }
    }
}
