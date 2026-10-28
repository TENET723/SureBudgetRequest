namespace SureBudgetRequest.Application.Common;

/// <summary>
/// Utility for determining optimal contrast text color based on background hex color,
/// following standard WCAG perceived luminance calculations.
/// </summary>
public static class ColorContrastHelper
{
    public const string DefaultDarkText = "#1F2937";
    public const string DefaultLightText = "#FFFFFF";

    /// <summary>
    /// Returns the explicit font color if provided; otherwise calculates whether
    /// dark or light text provides optimal contrast against the given background color.
    /// </summary>
    public static string GetEffectiveFontColor(string? bgHex, string? explicitFontHex)
    {
        if (!string.IsNullOrWhiteSpace(explicitFontHex))
            return explicitFontHex.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(bgHex))
            return DefaultLightText;

        var hex = bgHex.Trim().TrimStart('#');
        if (hex.Length == 3)
        {
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        }

        if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
        {
            return DefaultLightText;
        }

        int r = (rgb >> 16) & 0xFF;
        int g = (rgb >> 8) & 0xFF;
        int b = rgb & 0xFF;

        // Standard sRGB relative luminance calculation (WCAG perceived brightness)
        double luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;

        // Luminance > 0.6 indicates a bright background (yellows, pastels, white)
        return luminance > 0.6 ? DefaultDarkText : DefaultLightText;
    }
}
