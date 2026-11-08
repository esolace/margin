using Margin.Core;

static class AppearanceChecks
{
    public static void Run(Action<bool, string> check)
    {
        var automatic = new GroupAppearance();
        check(GroupAppearancePolicy.Resolve(automatic, "#FFFFFF", 10, "#151C22").Text == "#F4F3F0",
            "transparent light fill uses light text against the actual dark canvas");
        check(GroupAppearancePolicy.Resolve(automatic, "#000000", 10, "#FFFFFF").Text == "#23282C",
            "transparent dark fill uses dark text against the actual light canvas");
        var band = automatic with { Heading = "Band", Accent = "#FFFFFF" };
        var resolved = GroupAppearancePolicy.Resolve(band, "#000000", 10, "#151C22");
        check(resolved.Text == "#23282C" && resolved.Band && !resolved.Divider, "band title contrast follows its opaque accent instead of the group fill");
        var manual = automatic with { Text = "#123456", Accent = "#ABCDEF", Border = "#654321" };
        check(GroupAppearancePolicy.Resolve(manual, "#FFFFFF", 0, "#FFFFFF") == new ResolvedGroupAppearance("#123456", "#ABCDEF", "#654321", false, true),
            "explicit title, accent and border colours override automatic choices");
        check(GroupAppearancePolicy.Resolve(automatic with { Heading = "Plain" }, "#FFFFFF", 100, "#000000") is { Divider: false, Band: false, Accent: "#A5A5A5" },
            "plain titles suppress decoration while retaining automatic edge colour");
        try { GroupAppearancePolicy.Resolve(automatic, "#FFFFFF", double.NaN, "#000000"); throw new Exception("Invalid opacity accepted"); }
        catch (InvalidDataException) { check(true, "invalid opacity is rejected before colour resolution"); }
    }

    public static object[] Vectors() => (from fill in new[] { "#FFFFFF", "#000000", "#304737", "#ED8DC5" }
        from opacity in new[] { 0d, 10, 85.1, 100 }
        from dark in new[] { false, true }
        from heading in new[] { "Rule", "Band", "Plain" }
        from manual in new[] { false, true }
        from divider in new[] { false, true }
        select Vector(fill, opacity, dark, new GroupAppearance { Heading = heading, ShowDivider = divider,
            Text = manual ? "#123456" : null, Accent = manual ? "#ABCDEF" : null, Border = manual ? "#654321" : null })).ToArray();

    static object Vector(string fill, double opacity, bool dark, GroupAppearance appearance) => new
    {
        style = new { Fill = fill, Opacity = opacity, Appearance = appearance }, dark,
        resolved = GroupAppearancePolicy.Resolve(appearance, fill, opacity, dark ? "#171C22" : "#D4D0C8")
    };
}
