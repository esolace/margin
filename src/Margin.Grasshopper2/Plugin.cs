using Eto.Forms;
using Grasshopper2.UI;
using Grasshopper2.UI.Canvas;
using Margin.Core;
using Theme = Margin.Core.Theme;
using Margin.Native;

[assembly: System.Runtime.InteropServices.Guid("d0b6f846-09b8-4b80-9ab6-27728ba6f193")]

namespace Margin.GH2;

public sealed class PluginInfo : Grasshopper2.Framework.Plugin
{
    public override string Name => "Margin";
    public override string Info => "Designed component labels and a personal style studio for Grasshopper 2.";
    public override string Author => "Solace";
    public override string LicenceDescription => "MIT";
    public override void OnLoaded() => Runtime.Start();
}
public static class Runtime
{
    static bool started;
    static Canvas? attached;
    static Labels? labels;
    static Inspector? inspector;
    static ButtonMenuItem? menu;
    static ButtonMenuItem? displayMenu;
    static CheckMenuItem? toggle;
    public static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Margin", "theme.json");
    public static Theme Current { get; private set; } = new();
    public static bool Enabled => labels?.Enabled ?? false;
    public static Window Owner => Editor.ThisOrRhino;
    public static void Start()
    {
        if (started) return;
        started = true;
        try { if (File.Exists(SettingsPath)) Current = ThemeStore.Load(SettingsPath); }
        catch (Exception ex) { Rhino.RhinoApp.WriteLine("Margin: using Paper. " + ex.Message); }
        Current = InstalledFonts.Resolve(Current);
        Rhino.RhinoApp.Idle += Ready;
    }
    static void Ready(object? sender, EventArgs e)
    {
        var editor = Editor.Instance;
        if (editor?.Canvas is null || editor.Menu is null) return;
        if (!ReferenceEquals(attached, editor.Canvas))
        {
            labels?.Dispose(); attached = editor.Canvas;
            labels = new Labels(attached) { Theme = Current };
        }
        if (menu is not null) return;
        displayMenu = editor.Menu.Items.OfType<ButtonMenuItem>()
            .FirstOrDefault(i => i.Text.Replace("&", "").Equals("Display", StringComparison.OrdinalIgnoreCase));
        if (displayMenu is null) return;
        menu = new ButtonMenuItem { Text = "Margin", ID = "MarginMenu" };
        var open = new ButtonMenuItem { Text = "Open Label Studio…" };
        open.Click += (_, _) => Show();
        toggle = new CheckMenuItem { Text = "Show component labels", Checked = Enabled };
        toggle.Click += (_, _) => SetEnabled(toggle.Checked);
        menu.Items.Add(open); menu.Items.Add(toggle);
        displayMenu.Items.Add(menu);
    }
    public static void SetEnabled(bool value)
    {
        if (labels is null || attached is null) return;
        if (labels.LastError is not null) { labels.Dispose(); labels = new Labels(attached) { Theme = Current }; }
        labels.Enabled = value;
        if (toggle is not null) toggle.Checked = value;
        attached.Invalidate();
    }
    public static void Apply(Theme theme)
    {
        Current = InstalledFonts.Resolve(theme.Normalize());
        if (labels is not null) labels.Theme = Current;
        attached?.Invalidate();
    }
    public static void Save() => ThemeStore.Save(SettingsPath, Current);
    public static void Show()
    {
        if (inspector is null) { inspector = new Inspector(); inspector.Closed += (_, _) => inspector = null; }
        inspector.Present();
    }
    public static void Stop()
    {
        Rhino.RhinoApp.Idle -= Ready;
        labels?.Dispose(); labels = null; attached = null;
        inspector?.Close(); inspector = null;
        if (menu is not null) { displayMenu?.Items.Remove(menu); menu.Dispose(); }
        menu = null; toggle = null; displayMenu = null; started = false;
    }
}
