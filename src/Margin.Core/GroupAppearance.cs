using System.Text.RegularExpressions;

namespace Margin.Core;

// Null colours retain automatic contrast for existing group styles.
public sealed record GroupAppearance
{
    public string? Text { get; init; }
    public string? Accent { get; init; }
    public string? Border { get; init; }
    public double TitleSize { get; init; } = 15;
    public bool Bold { get; init; } = true;
    public double Radius { get; init; } = 8;
    public double BorderWidth { get; init; } = 1;
    public bool ShowAccent { get; init; } = true;
    public bool ShowDivider { get; init; } = true;
    public string Heading { get; init; } = "Rule";

    public GroupAppearance Validate()
    {
        static string? Colour(string? value) => value is null ? null
            : Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$") ? value.ToUpperInvariant()
            : throw new InvalidDataException("Choose a valid six-digit colour.");
        static bool Range(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
        if (!Range(TitleSize, 10, 30) || !Range(Radius, 0, 24) || !Range(BorderWidth, 0, 4)
            || !GroupAppearancePolicy.IsHeading(Heading))
            throw new InvalidDataException("Choose a valid group title and frame appearance.");
        return this with { Text = Colour(Text), Accent = Colour(Accent), Border = Colour(Border) };
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public double ExtraHeaderSpace => GroupAppearancePolicy.ExtraHeaderSpace(this);
}
