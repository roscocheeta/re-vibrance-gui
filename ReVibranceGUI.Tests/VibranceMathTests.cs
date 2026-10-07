using ReVibranceGUI.Services;
using Xunit;

namespace ReVibranceGUI.Tests
{
    public class VibranceMathTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(50, 50)]
        [InlineData(100, 100)]
        [InlineData(150, 100)] // Clamp
        [InlineData(-50, 0)] // Clamp
        public void UiToNvidia_ShouldMapProperly(int uiLevel, int expectedNative)
        {
            Assert.Equal(expectedNative, VibranceMath.UiToNvidia(uiLevel));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(50, 50)]
        [InlineData(100, 100)]
        [InlineData(150, 100)] // Clamp
        [InlineData(-50, 0)] // Clamp
        public void NvidiaToUi_ShouldMapProperly(int nativeLevel, int expectedUi)
        {
            Assert.Equal(expectedUi, VibranceMath.NvidiaToUi(nativeLevel));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(50, 100)]
        [InlineData(100, 200)]
        [InlineData(150, 200)] // Clamp UI first
        [InlineData(-50, 0)] // Clamp UI first
        public void UiToAmd_ShouldMapProperly(int uiLevel, int expectedNative)
        {
            Assert.Equal(expectedNative, VibranceMath.UiToAmd(uiLevel));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(100, 50)]
        [InlineData(200, 100)]
        [InlineData(250, 100)] // Native clamping might be needed or handled in Math
        public void AmdToUi_ShouldMapProperly(int nativeLevel, int expectedUi)
        {
            Assert.Equal(expectedUi, VibranceMath.AmdToUi(nativeLevel));
        }
    }
}
