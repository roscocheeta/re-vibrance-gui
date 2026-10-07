using ReVibranceGUI.Services;
using Xunit;

namespace ReVibranceGUI.Tests
{
    public class DisplayResolutionTests
    {
        [Theory]
        [InlineData("1920 x 1080 @ 144 Hz", 1920, 1080, 144)]
        [InlineData("2560 x 1440 @ 60 Hz", 2560, 1440, 60)]
        [InlineData("1280 x 720", 1280, 720, 0)]
        public void Parse_ShouldReadDisplayStrings(string input, int w, int h, int r)
        {
            var res = DisplayResolution.Parse(input);

            Assert.NotNull(res);
            Assert.Equal(w, res!.Width);
            Assert.Equal(h, res.Height);
            Assert.Equal(r, res.RefreshRate);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("garbage")]
        [InlineData("1920")]
        public void Parse_ShouldRejectInvalidInput(string input)
        {
            Assert.Null(DisplayResolution.Parse(input));
        }

        [Fact]
        public void ToString_ShouldRoundTripThroughParse()
        {
            var original = new DisplayResolution { Width = 3840, Height = 2160, RefreshRate = 120 };

            var parsed = DisplayResolution.Parse(original.ToString());

            Assert.NotNull(parsed);
            Assert.Equal(original.Width, parsed!.Width);
            Assert.Equal(original.Height, parsed.Height);
            Assert.Equal(original.RefreshRate, parsed.RefreshRate);
        }

        [Theory]
        [InlineData(1920, 1080, 144, true)]
        [InlineData(3840, 2160, 0, true)]
        [InlineData(0, 0, 0, false)]
        [InlineData(-1920, 1080, 60, false)]
        [InlineData(1920, 1080, -1, false)]
        [InlineData(1920, 1080, 5000, false)]
        [InlineData(99999, 1080, 60, false)]
        [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, false)]
        public void IsPlausible_ShouldBoundRequestedModes(int w, int h, int r, bool expected)
        {
            Assert.Equal(expected, DisplayResolution.IsPlausible(w, h, r));
        }
    }
}
