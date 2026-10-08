using Xunit;
using ReVibranceGUI.Services;

namespace ReVibranceGUI.Tests
{
    public class StartupManagerTests
    {
        [Theory]
        [InlineData(@"C:\Program Files\ReVibranceGUI\ReVibranceGUI.exe", "\"C:\\Program Files\\ReVibranceGUI\\ReVibranceGUI.exe\" -startup")]
        [InlineData(@"C:\NoSpaces\App.exe", "\"C:\\NoSpaces\\App.exe\" -startup")]
        public void BuildCommand_FormatsPathCorrectly_AndAppendsStartupFlag(string inputPath, string expectedCommand)
        {
            // Act
            string actualCommand = StartupManager.BuildCommand(inputPath);

            // Assert
            Assert.Equal(expectedCommand, actualCommand);
        }
    }
}
