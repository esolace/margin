using System.Text.Json;
using System.Text.RegularExpressions;

namespace Margin.Core;

public static class SettingsPaths
{
    // A process-local override keeps disposable native tests out of personal settings.
    public static string DirectoryPath
    {
        get
        {
            var custom = Environment.GetEnvironmentVariable("MARGIN_SETTINGS_DIRECTORY");
            if (string.IsNullOrWhiteSpace(custom))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Margin");
            if (!Path.IsPathRooted(custom)) throw new InvalidOperationException("MARGIN_SETTINGS_DIRECTORY must be an absolute path.");
            return Path.GetFullPath(custom);
        }
    }
}

public sealed record Preferences
{
    public string Language { get; init; } = "auto";
    public bool LabelsEnabled { get; init; }
    public bool StyleGroups { get; init; } = true;
    public bool UseGroupColour { get; init; }
    public string GroupFill { get; init; } = "#304737";
    public double GroupOpacity { get; init; } = 85;
    public GroupAppearance GroupAppearance { get; init; } = new();
    public Preferences Validate()
    {
        if (!Localization.IsChoice(Language)) throw Localization.InvalidData("language.invalid");
        if (!Regex.IsMatch(GroupFill ?? "", "^#[0-9a-fA-F]{6}$") || !RuntimeSupport.IsFinite(GroupOpacity) || GroupOpacity < 0 || GroupOpacity > 100)
            throw Localization.InvalidData("ui.invalid_saved_group_appearance");
        return this with { GroupFill = GroupFill!.ToUpperInvariant(), GroupAppearance = (GroupAppearance ?? new()).Validate() };
    }
}

public static class PreferencesStore
{
    static readonly object gate = new();
    public static Preferences Load(string path)
    {
        if (!File.Exists(path)) return new();
        var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path))
            ?? throw Localization.InvalidData("ui.empty_margin_preferences");
        // An unknown language must not disable labels or discard valid group defaults.
        if (!Localization.IsChoice(value.Language)) value = value with { Language = "auto" };
        return value.Validate();
    }
    public static Preferences Update(string path, Func<Preferences, Preferences> change)
    {
        lock (gate)
        {
            var value = change(Load(path)).Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
                RuntimeSupport.ReplaceFile(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return value;
        }
    }
}
