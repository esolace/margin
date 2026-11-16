using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Margin.Core;

public static class Localization
{
    public static IReadOnlyDictionary<string,string> Languages { get; } = new Dictionary<string,string>
    {
        ["en"]="English", ["cs"]="Čeština", ["de"]="Deutsch", ["es"]="Español", ["fr"]="Français",
        ["it"]="Italiano", ["ja"]="日本語", ["ko"]="한국어", ["pl"]="Polski", ["pt"]="Português",
        ["ru"]="Русский", ["zh-Hans"]="简体中文", ["zh-Hant"]="繁體中文"
    };
    static readonly Lazy<Dictionary<string,Dictionary<string,string>>> catalogs = new(Load);
    static readonly Regex placeholder = new(@"\{([a-zA-Z][a-zA-Z0-9]*)\}");
    public static string Choice { get; private set; } = "auto";
    public static string Language { get; private set; } = "en";
    public static int Revision { get; private set; }
    public static bool IsChoice(string? choice) => choice == "auto" || (choice is not null && Languages.ContainsKey(choice));
    public static string Resolve(string? choice, string? host)
    {
        if (choice != "auto" && choice is not null && Languages.ContainsKey(choice)) return choice;
        var tag = (host ?? "").Replace('_','-').ToLowerInvariant();
        if (tag.StartsWith("zh"))
            return tag.Contains("hans") ? "zh-Hans" : tag.Contains("hant") || tag.EndsWith("-tw") || tag.EndsWith("-hk") || tag.EndsWith("-mo") ? "zh-Hant" : "zh-Hans";
        var language = tag.Split('-')[0];
        return Languages.ContainsKey(language) ? language : "en";
    }
    public static void Use(string choice, string host)
    {
        if (!IsChoice(choice)) throw new InvalidDataException(Text("language.invalid"));
        var next = Resolve(choice,host);
        if (Language == next && Choice == choice) return;
        Language = next; Choice = choice; Revision++;
    }
    public static IReadOnlyDictionary<string,string> Catalog(string language) =>
        catalogs.Value.TryGetValue(language, out var value) ? value : catalogs.Value["en"];
    public static string Text(string key, params (string Name, object? Value)[] values) => Translate(Language,key,values);
    public static string Translate(string language, string key, params (string Name, object? Value)[] values)
    {
        var catalog = Catalog(language);
        var template = catalog.TryGetValue(key,out var translated) ? translated
            : catalogs.Value["en"].TryGetValue(key,out var english) ? english : key;
        return placeholder.Replace(template, match =>
        {
            foreach (var value in values)
                if (value.Name == match.Groups[1].Value) return Convert.ToString(value.Value,CultureInfo.InvariantCulture) ?? "";
            return match.Value;
        });
    }
    static Dictionary<string,Dictionary<string,string>> Load()
    {
        using var stream = typeof(Localization).Assembly.GetManifestResourceStream("Margin.Locales.json")
            ?? throw new InvalidOperationException("The language catalog is missing.");
        return JsonSerializer.Deserialize<Dictionary<string,Dictionary<string,string>>>(stream)!;
    }
}
