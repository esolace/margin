using Eto.Drawing;
using Margin.Core;

namespace Margin.Native;

internal static class InstalledFonts
{
    internal static string[] Names() => Fonts.AvailableFontFamilies.Select(f => f.Name)
        .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();

    internal static Theme Resolve(Theme theme)
    {
        var names = Names();
        var chosen = names.FirstOrDefault(n => string.Equals(n, theme.FontFamily, StringComparison.OrdinalIgnoreCase));
        if (chosen is null)
        {
            var preferred = OperatingSystem.IsWindows() ? "Segoe UI" : "Helvetica Neue";
            chosen = names.FirstOrDefault(n => n == preferred) ?? names.FirstOrDefault(n => n == "Arial")
                ?? names.FirstOrDefault() ?? theme.FontFamily;
        }
        return theme with { FontFamily = chosen };
    }
}
