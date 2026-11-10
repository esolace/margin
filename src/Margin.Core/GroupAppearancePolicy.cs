using System.Text.Json;
using System.Text;
using System.Globalization;
using System.Collections.ObjectModel;

namespace Margin.Core;

public sealed record ResolvedGroupAppearance(string Text, string Accent, string Border, bool Band, bool Divider);

// Drawing adapters supply their actual canvas colour; all contrast/treatment
// choices use the same small policy asset as Studio's browser adapter.
public static class GroupAppearancePolicy
{
    sealed record HeadingRule(bool Band, bool Divider, double ExtraSpace);
    sealed record Rules(double[] LuminanceWeights, double LightThreshold, double LightEdgeScale,
        int DarkEdgeOffset, string DarkInk, string LightInk, Dictionary<string, HeadingRule> Headings, Dictionary<string, string> TitleCases);
    static readonly Rules rules = Load();

    public static IReadOnlyDictionary<string, string> TitleCases { get; } = new ReadOnlyDictionary<string, string>(rules.TitleCases);

    // Unicode simple casing keeps native and browser output aligned without
    // expanding a character into several letters or rewriting the stored name.
    public static string FormatTitle(string name, string casing)
    {
        if (!TitleCases.ContainsKey(casing)) throw new InvalidDataException("Choose a valid title case.");
        if (casing == "AsTyped") return name;
        var result = new StringBuilder();
        var initial = true;
        foreach (var rune in name.EnumerateRunes())
        {
            var value = casing == "Uppercase" ? Rune.ToUpperInvariant(rune) : Rune.ToLowerInvariant(rune);
            if (casing is "TitleCase" or "SentenceCase")
            {
                if (Rune.IsLetter(rune)) { if (initial) value = Rune.ToUpperInvariant(value); initial = false; }
                else if (casing == "TitleCase" && !Rune.IsNumber(rune) && rune.Value is not (39 or 8217)
                    && Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)) initial = true;
            }
            result.Append(value.ToString());
        }
        return result.ToString();
    }
    static Rules Load()
    {
        using var stream = typeof(GroupAppearancePolicy).Assembly.GetManifestResourceStream("Margin.GroupAppearancePolicy.json")!;
        return JsonSerializer.Deserialize<Rules>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
    static double[] Rgb(string hex)
    {
        if (hex is null || hex.Length != 7 || hex[0] != '#' || !hex[1..].All(Uri.IsHexDigit))
            throw new InvalidDataException("Choose a valid six-digit colour.");
        return [Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16)];
    }
    static double Luminance(double[] rgb) => rgb.Select((v, i) => v * rules.LuminanceWeights[i]).Sum() / 255;
    static string Hex(IEnumerable<double> rgb) => "#" + string.Concat(rgb.Select(v => ((int)v).ToString("X2")));
    public static bool IsHeading(string heading) => heading is not null && rules.Headings.ContainsKey(heading);
    public static double ExtraHeaderSpace(GroupAppearance appearance) => !appearance.ShowTitle ? 0 : Math.Max(0, (appearance.TitleSize - 15) * 1.5) + rules.Headings[appearance.Heading].ExtraSpace;

    public static ResolvedGroupAppearance Resolve(GroupAppearance appearance, string fill, double opacity, string canvas, bool externalTitle = false)
    {
        appearance = appearance.Validate();
        if (!double.IsFinite(opacity) || opacity < 0 || opacity > 100)
            throw new InvalidDataException("Choose an opacity between 0 and 100.");
        var rgb = Rgb(fill);
        var edge = Hex(rgb.Select(v => Luminance(rgb) > rules.LightThreshold
            ? Math.Floor(v * rules.LightEdgeScale) : Math.Min(255, v + rules.DarkEdgeOffset)));
        var accent = appearance.Accent ?? edge;
        var heading = rules.Headings[appearance.Heading];
        var background = Rgb(canvas);
        var behind = externalTitle ? background : heading.Band ? Rgb(accent) : rgb.Select((v, i) => v * opacity / 100 + background[i] * (1 - opacity / 100)).ToArray();
        var ink = appearance.Text ?? (Luminance(behind) > rules.LightThreshold ? rules.DarkInk : rules.LightInk);
        return new(ink, accent, appearance.Border ?? edge, appearance.ShowTitle && !externalTitle && heading.Band, appearance.ShowTitle && !externalTitle && heading.Divider && appearance.ShowDivider);
    }
}
