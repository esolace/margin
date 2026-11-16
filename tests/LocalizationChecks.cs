using Margin.Core;

static class LocalizationChecks
{
    public sealed record LocaleCase(string Choice, string? Host, string Expected);
    public static void Run(Action<bool,string> check)
    {
        using var casesStream = typeof(LocalizationChecks).Assembly.GetManifestResourceStream("Margin.LocaleCases.json")!;
        var cases = System.Text.Json.JsonSerializer.Deserialize<LocaleCase[]>(casesStream,new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        foreach(var item in cases) check(Localization.Resolve(item.Choice,item.Host)==item.Expected,"shared locale case: "+item.Choice+"/"+item.Host);
        check(Localization.Resolve("auto", "de-DE") == "de", "automatic language follows the host");
        check(Localization.Resolve("fr", "de-DE") == "fr", "manual language overrides the host");
        check(Localization.Resolve("auto", "zh-HK") == "zh-Hant" && Localization.Resolve("auto", "zh-CN") == "zh-Hans", "Chinese regions resolve to the correct script");
        check(Localization.Resolve("auto", "pt-BR") == "pt" && Localization.Resolve("auto", "xx") == "en", "regional languages and unsupported hosts have safe fallbacks");
        check(Localization.Languages.Count == 13, "all thirteen Rhino languages are available");
        check(Localization.Translate("de", "ui.show_title") == "Titel anzeigen"
            && Localization.Translate("ja", "ui.show_title") == "タイトルを表示", "native controls use the requested catalog");
        check(Localization.Translate("unknown", "ui.show_title") == "Show title", "missing catalog falls back to English");
        var supplied = "<img src=x> {error} 日本語";
        check(Localization.Translate("en", "preset.use", ("name", supplied)) == "Use " + supplied, "substitution preserves literal user content without recursive expansion");
        foreach (var exception in new Exception[] {
            Localization.InvalidData("preset.use", ("name", supplied)),
            Localization.InvalidOperation("preset.use", ("name", supplied)) })
        {
            var descriptor = System.Text.Json.JsonSerializer.SerializeToElement(Localization.Describe(exception));
            check(descriptor.GetProperty("key").GetString() == "preset.use"
                && descriptor.GetProperty("args").GetProperty("name").GetString() == supplied,
                "validation errors preserve translatable messages across runtimes");
        }
        check((string)Localization.Describe(new Exception(supplied)) == supplied, "external errors preserve their original detail");
        var english = Localization.Catalog("en");
        foreach (var language in Localization.Languages.Keys)
        {
            var catalog = Localization.Catalog(language);
            check(catalog.Count == english.Count && english.Keys.All(catalog.ContainsKey), language + " has every interface message");
            foreach (var key in english.Keys)
            {
                string[] Tokens(string value) => System.Text.RegularExpressions.Regex.Matches(value, @"\{[a-zA-Z][a-zA-Z0-9]*\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).OrderBy(v => v).ToArray();
                if (string.IsNullOrWhiteSpace(catalog[key]) || !Tokens(english[key]).SequenceEqual(Tokens(catalog[key])))
                    throw new Exception("Incomplete translation: " + language + " " + key);
            }
        }
        Localization.Use("en", "en-US");
        var revision = Localization.Revision;
        Localization.Use("auto", "en-US");
        check(Localization.Choice == "auto" && Localization.Revision > revision, "same-language choice changes are acknowledged across host adapters");
        check(!new Theme().ToJson().Contains("Language"), "language is not exported into shared themes");
        var path = Path.Combine(Path.GetTempPath(), "margin-language-" + Guid.NewGuid(), "preferences.json");
        try
        {
            PreferencesStore.Update(path, p => p with { Language = "ja", LabelsEnabled = true, GroupFill = "#123456" });
            var saved = PreferencesStore.Load(path);
            check(saved.Language == "ja" && saved.LabelsEnabled && saved.GroupFill == "#123456", "language survives reopen without changing appearance");
            check(System.Text.Json.JsonSerializer.Deserialize<Preferences>("{}")!.Language == "auto", "old preferences follow Rhino automatically");
            File.WriteAllText(path, "{\"Language\":\"future-locale\",\"LabelsEnabled\":true,\"GroupFill\":\"#123456\"}");
            check(PreferencesStore.Load(path) is { Language: "auto", LabelsEnabled: true, GroupFill: "#123456" }, "unknown saved language preserves unrelated preferences");
            PreferencesStore.Update(path, p => p with { Language = "fr" });
            check(PreferencesStore.Load(path) is { Language: "fr", LabelsEnabled: true, GroupFill: "#123456" }, "a saved unknown language can be repaired");
            var original = File.ReadAllText(path);
            try { PreferencesStore.Update(path, p => p with { Language = "invalid" }); throw new Exception("Invalid language accepted"); }
            catch (InvalidDataException) { check(File.ReadAllText(path) == original, "invalid language cannot replace saved preferences"); }
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
}
