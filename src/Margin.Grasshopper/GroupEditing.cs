using Margin.Core;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Margin.Native;

public sealed record GroupStyle(string Fill, double Opacity, bool Designed = true)
{
    public GroupAppearance Appearance { get; init; } = new();
    public GroupStyle Validate()
    {
        if (!Regex.IsMatch(Fill ?? "", "^#[0-9a-fA-F]{6}$") || !double.IsFinite(Opacity) || Opacity < 0 || Opacity > 100)
            throw new InvalidDataException("Choose a valid colour and an opacity between 0 and 100.");
        return this with { Fill = Fill!.ToUpperInvariant(), Appearance = (Appearance ?? new()).Validate() };
    }
    public static GroupStyle? Parse(string json)
    {
        try { return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<GroupStyle>(json)?.Validate(); }
        catch { return null; }
    }
}
public sealed record GroupEntry(string Id, string Name, int Members, bool Selected, GroupStyle Style, string Revision);

public sealed record GroupSaveResult(GroupEntry Group, string? DefaultsError)
{
    public bool Complete => DefaultsError is null;
}

// A session retains the host's acknowledged revision across a partial save.
public sealed class GroupEditSession
{
    readonly Func<string, string, GroupStyle, GroupEntry> apply;
    readonly Action<GroupStyle> saveDefaults;
    public GroupEntry Current { get; private set; }

    public GroupEditSession(GroupEntry current, Func<string, string, GroupStyle, GroupEntry> apply, Action<GroupStyle> saveDefaults)
    { Current = current; this.apply = apply; this.saveDefaults = saveDefaults; }

    public GroupSaveResult Save(string name, GroupStyle style, bool useForFutureGroups = false)
    {
        style = style.Validate();
        var saved = apply(Current.Revision, name, style);
        Current = saved;
        try
        {
            if (useForFutureGroups) saveDefaults(style);
            return new(saved, null);
        }
        catch (Exception ex) { return new(saved, ex.Message); }
    }
}
