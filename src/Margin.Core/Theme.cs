using System.Text.Json;
using System.Text.RegularExpressions;

namespace Margin.Core;

public sealed record Theme
{
    public string Name { get; init; } = "Paper";
    public string FontFamily { get; init; } = "Helvetica Neue";
    public double FontSize { get; init; } = 12;
    public bool Bold { get; init; } = true;
    public string Text { get; init; } = "#28313B";
    public string Fill { get; init; } = "#F8FAFC";
    public string Border { get; init; } = "#BCC6D2";
    public string Accent { get; init; } = "#547DA6";
    public double Opacity { get; init; } = 96;
    public double Radius { get; init; } = 6;
    public double BorderWidth { get; init; } = 1;
    public double PaddingX { get; init; } = 10;
    public double PaddingY { get; init; } = 6;
    public double Gap { get; init; } = 12;
    public double MinZoom { get; init; } = .55;
    public bool Below { get; init; }
    public bool ShowCategory { get; init; }
    public bool ShowAccent { get; init; } = true;
    public bool UseNickname { get; init; }
    public bool SelectedOnly { get; init; }
    public bool AvoidOverlap { get; init; } = true;

    public static IReadOnlyList<Theme> Presets { get; } = new[] {
        new Theme(),
        new Theme { Name="Ink", Fill="#273644", Text="#F1F5F9", Border="#43576A", Accent="#97BADC" },
        new Theme { Name="Outline", Opacity=0, Text="#334C62", Border="#6986A1", Radius=3, ShowAccent=false },
        new Theme { Name="Signal", Fill="#DCECF7", Text="#173C57", Border="#84AFCB", Accent="#286A9B", Radius=12 },
        new Theme { Name="Petal", Fill="#F5E5EF", Text="#642D4C", Border="#D6B2C6", Accent="#AF6187", Radius=10 }
    };

    public Theme Normalize() => this with
    {
        Name = Clean(Name, "Custom", 48),
        FontFamily = Clean(FontFamily, "Helvetica Neue", 100),
        Text = Hex(Text, "#28313B"),
        Fill = Hex(Fill, "#F8FAFC"),
        Border = Hex(Border, "#BCC6D2"),
        Accent = Hex(Accent, "#547DA6"),
        FontSize = Clamp(FontSize, 8, 28, 12),
        Opacity = Clamp(Opacity, 0, 100, 96),
        Radius = Clamp(Radius, 0, 24, 6),
        BorderWidth = Clamp(BorderWidth, 0, 4, 1),
        PaddingX = Clamp(PaddingX, 2, 30, 10),
        PaddingY = Clamp(PaddingY, 2, 20, 6),
        Gap = Clamp(Gap, 4, 60, 12),
        MinZoom = Clamp(MinZoom, .2, 2, .55)
    };
    private static double Clamp(double v, double min, double max, double fallback) => double.IsFinite(v) ? Math.Clamp(v, min, max) : fallback;
    private static string Clean(string? s, string fallback, int max) => string.IsNullOrWhiteSpace(s) ? fallback : s.Trim()[..Math.Min(s.Trim().Length, max)];
    private static string Hex(string? s, string fallback) => s is not null && Regex.IsMatch(s, "^#[0-9a-fA-F]{6}$") ? s.ToUpperInvariant() : fallback;
    public string ToJson() => JsonSerializer.Serialize(Normalize(), new JsonSerializerOptions { WriteIndented = true });
    public static Theme FromJson(string json)
    {
        if (json.Length > 32_768) throw new InvalidDataException("Theme files must be smaller than 32 KB.");
        return (JsonSerializer.Deserialize<Theme>(json) ?? throw new InvalidDataException("Empty theme.")).Normalize();
    }
}

public static class ThemeStore
{
    public static Theme Load(string path) => Theme.FromJson(File.ReadAllText(path));
    public static void Save(string path, Theme theme)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, theme.ToJson()); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
