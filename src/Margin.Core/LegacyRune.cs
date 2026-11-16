#if NETFRAMEWORK || MARGIN_LEGACY_CHECK
using System.Globalization;
using System.Text.Json;

namespace Margin.Core;

internal readonly record struct LegacyRune(int Value)
{
    sealed record Data(int[][] Cases, int[][] Ranges);
    static readonly Data data = Load();
    static readonly Dictionary<int, int[]> cases = data.Cases.ToDictionary(row => row[0]);
    static Data Load()
    {
        using var stream = typeof(LegacyRune).Assembly.GetManifestResourceStream("Margin.LegacyUnicode.json")!;
        return JsonSerializer.Deserialize<Data>(stream)!;
    }
    public override string ToString() => char.ConvertFromUtf32(Value);
    public static LegacyRune ToUpperInvariant(LegacyRune rune) => new(cases.TryGetValue(rune.Value, out var row) ? row[1] : rune.Value);
    public static LegacyRune ToLowerInvariant(LegacyRune rune) => new(cases.TryGetValue(rune.Value, out var row) ? row[2] : rune.Value);
    public static UnicodeCategory GetUnicodeCategory(LegacyRune rune)
    {
        int lo = 0, hi = data.Ranges.Length - 1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            var row = data.Ranges[mid];
            if (rune.Value < row[0]) hi = mid - 1;
            else if (rune.Value > row[1]) lo = mid + 1;
            else return (UnicodeCategory)row[2];
        }
        return UnicodeCategory.OtherNotAssigned;
    }
    public static bool IsLetter(LegacyRune rune) => GetUnicodeCategory(rune) is
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
        or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter;
    public static bool IsNumber(LegacyRune rune) => GetUnicodeCategory(rune) is
        UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber;
}

internal static class LegacyScalarEnumeration
{
    public static IEnumerable<LegacyRune> EnumerateLegacyRunes(this string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char value = text[i];
            if (char.IsHighSurrogate(value) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                yield return new(char.ConvertToUtf32(value, text[++i]));
            else yield return new(char.IsSurrogate(value) ? 0xfffd : value);
        }
    }
}
#endif
