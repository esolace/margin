using System.Text.Json;
using Margin.Core;

namespace Margin.Presets;

public sealed record SavedPreset(string Id, Theme Theme);

// Keep the library separate from the last-used theme; never overwrite a bad file.
public static class PresetStore
{
    public static List<SavedPreset> Load(string path)
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 1_048_576) throw new InvalidDataException(Localization.Text("ui.the_preset_library_is_too_large"));
        var items = JsonSerializer.Deserialize<List<SavedPreset>>(File.ReadAllText(path))
            ?? throw new InvalidDataException(Localization.Text("ui.the_preset_library_is_empty_or_damaged"));
        if (items.Count > 500 || items.Any(p => p is null || !Guid.TryParse(p.Id, out _) || p.Theme is null)
            || items.Select(p => p.Id).Distinct().Count() != items.Count)
            throw new InvalidDataException(Localization.Text("ui.the_preset_library_is_damaged_your_file_has_been_preserved"));
        return items.Select(p => p with { Theme = p.Theme.Normalize() }).ToList();
    }
    public static SavedPreset Save(string path, string name, Theme theme, string? id = null)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 48 || name.Any(char.IsControl))
            throw new InvalidDataException(Localization.Text("ui.use_a_preset_name_between_1_and_48_characters"));
        var items = Load(path);
        var index = id is null ? -1 : items.FindIndex(p => p.Id == id);
        if (id is not null && index < 0) throw new InvalidDataException(Localization.Text("ui.that_preset_no_longer_exists_save_a_new_preset"));
        if (items.Any(p => p.Id != id && string.Equals(p.Theme.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException(Localization.Text("ui.you_already_have_a_preset_with_that_name_choose_another_name_or_update_the_existing_p"));
        if (id is null && items.Count >= 500) throw new InvalidDataException(Localization.Text("ui.your_library_is_full_remove_an_unused_preset_first"));
        var item = new SavedPreset(id ?? Guid.NewGuid().ToString("N"), (theme with { Name = name }).Normalize());
        if (index < 0) items.Add(item); else items[index] = item;
        Write(path, items);
        return item;
    }
    public static void Delete(string path, string id)
    {
        var items = Load(path);
        if (items.RemoveAll(p => p.Id == id) == 0) throw new InvalidDataException(Localization.Text("ui.that_preset_no_longer_exists"));
        Write(path, items);
    }
    static void Write(string path, List<SavedPreset> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
            RuntimeSupport.ReplaceFile(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
