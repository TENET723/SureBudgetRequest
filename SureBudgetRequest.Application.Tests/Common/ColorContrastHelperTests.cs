using FluentAssertions;
using SureBudgetRequest.Application.Common;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Common;

public class ColorContrastHelperTests
{
    [Theory]
    [InlineData("#FFFFFF")] // Pure White
    [InlineData("#FFFF00")] // Pure Yellow
    [InlineData("#FACC15")] // Tailwind Yellow-400
    [InlineData("#FEF08A")] // Light Yellow
    [InlineData("#86EFAC")] // Light Green
    [InlineData("#FBCFE8")] // Light Pink
    [InlineData("#E5E7EB")] // Light Gray
    public void GetEffectiveFontColor_ShouldReturnDarkText_WhenBackgroundIsBright(string bgHex)
    {
        var result = ColorContrastHelper.GetEffectiveFontColor(bgHex, explicitFontHex: null);

        result.Should().Be(ColorContrastHelper.DefaultDarkText);
    }

    [Theory]
    [InlineData("#000000")] // Black
    [InlineData("#1E3A8A")] // Navy Blue
    [InlineData("#7F1D1D")] // Dark Red
    [InlineData("#14532D")] // Dark Green
    [InlineData("#3B82F6")] // Blue
    [InlineData("#6B7280")] // Medium Gray
    public void GetEffectiveFontColor_ShouldReturnLightText_WhenBackgroundIsDark(string bgHex)
    {
        var result = ColorContrastHelper.GetEffectiveFontColor(bgHex, explicitFontHex: null);

        result.Should().Be(ColorContrastHelper.DefaultLightText);
    }

    [Fact]
    public void GetEffectiveFontColor_ShouldReturnExplicitFontColor_WhenProvided()
    {
        // Even if background is white, explicit font color is respected
        var result = ColorContrastHelper.GetEffectiveFontColor("#FFFFFF", "#123456");

        result.Should().Be("#123456");
    }

    [Fact]
    public void GetEffectiveFontColor_ShouldReturnDefaultLightText_WhenBackgroundIsNullOrEmpty()
    {
        var resultNull = ColorContrastHelper.GetEffectiveFontColor(null, null);
        var resultEmpty = ColorContrastHelper.GetEffectiveFontColor("", null);

        resultNull.Should().Be(ColorContrastHelper.DefaultLightText);
        resultEmpty.Should().Be(ColorContrastHelper.DefaultLightText);
    }

    [Fact]
    public void GetEffectiveFontColor_ShouldSupportShortHex()
    {
        // #FFF -> white -> dark text
        var resultWhite = ColorContrastHelper.GetEffectiveFontColor("#FFF", null);
        // #000 -> black -> light text
        var resultBlack = ColorContrastHelper.GetEffectiveFontColor("#000", null);

        resultWhite.Should().Be(ColorContrastHelper.DefaultDarkText);
        resultBlack.Should().Be(ColorContrastHelper.DefaultLightText);
    }
}
