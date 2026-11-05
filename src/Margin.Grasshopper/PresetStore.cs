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
        if (new FileInfo(path).Length > 1_048_576) throw new InvalidDataException("The preset library is too large.");
        var items = JsonSerializer.Deserialize<List<SavedPreset>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The preset library is empty or damaged.");
        if (items.Count > 500 || items.Any(p => p is null || !Guid.TryParse(p.Id, out _) || p.Theme is null)
            || items.Select(p => p.Id).Distinct().Count() != items.Count)
            throw new InvalidDataException("The preset library is damaged. Your file has been preserved.");
        return items.Select(p => p with { Theme = p.Theme.Normalize() }).ToList();
    }
    public static SavedPreset Save(string path, string name, Theme theme, string? id = null)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 48 || name.Any(char.IsControl))
            throw new InvalidDataException("Use a preset name between 1 and 48 characters.");
        var items = Load(path);
        var index = id is null ? -1 : items.FindIndex(p => p.Id == id);
        if (id is not null && index < 0) throw new InvalidDataException("That preset no longer exists. Save a new preset.");
        if (items.Any(p => p.Id != id && string.Equals(p.Theme.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("You already have a preset with that name. Choose another name or update the existing preset.");
        if (id is null && items.Count >= 500) throw new InvalidDataException("Your library is full. Remove an unused preset first.");
        var item = new SavedPreset(id ?? Guid.NewGuid().ToString("N"), (theme with { Name = name }).Normalize());
        if (index < 0) items.Add(item); else items[index] = item;
        Write(path, items);
        return item;
    }
    public static void Delete(string path, string id)
    {
        var items = Load(path);
        if (items.RemoveAll(p => p.Id == id) == 0) throw new InvalidDataException("That preset no longer exists.");
        Write(path, items);
    }
    static void Write(string path, List<SavedPreset> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
