using System.Text.Json;
using System.Text.RegularExpressions;

namespace Margin.Core;

public sealed record Preferences
{
    public bool LabelsEnabled { get; init; }
    public bool StyleGroups { get; init; } = true;
    public bool UseGroupColour { get; init; }
    public string GroupFill { get; init; } = "#304737";
    public double GroupOpacity { get; init; } = 85;
    public GroupAppearance GroupAppearance { get; init; } = new();
    public Preferences Validate()
    {
        if (!Regex.IsMatch(GroupFill ?? "", "^#[0-9a-fA-F]{6}$") || !double.IsFinite(GroupOpacity) || GroupOpacity < 0 || GroupOpacity > 100)
            throw new InvalidDataException("Invalid saved group appearance.");
        return this with { GroupFill = GroupFill!.ToUpperInvariant(), GroupAppearance = (GroupAppearance ?? new()).Validate() };
    }
}

public static class PreferencesStore
{
    static readonly object gate = new();
    public static Preferences Load(string path) => File.Exists(path)
        ? (JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty Margin preferences.")).Validate()
        : new();
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
                File.Move(temporary, path, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return value;
        }
    }
}
