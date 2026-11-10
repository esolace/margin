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
    static Inspector? inspector;
    static ButtonMenuItem? menu;
    static ButtonMenuItem? displayMenu;
    static CheckMenuItem? toggle;
    public static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Margin", "theme.json");
    public static string PreferencesPath => Path.Combine(Path.GetDirectoryName(SettingsPath)!, "preferences.json");
    static readonly LabelSession<Canvas> session = new(PreferencesPath, canvas => new Labels(canvas), Groups.Start, Groups.Stop);
    public static Theme Current => session.Theme;
    public static bool Enabled => session.EffectiveEnabled;
    public static bool DesiredEnabled => session.DesiredEnabled;
    public static LabelSessionState SessionState => session.State;
    public static string? LastError => session.LastError;
    public static Window Owner => Editor.ThisOrRhino;
    public static void Start()
    {
        if (session.Started) return;
        var theme = new Theme();
        try { if (File.Exists(SettingsPath)) theme = ThemeStore.Load(SettingsPath); }
        catch (Exception ex) { Rhino.RhinoApp.WriteLine("Margin: using Paper. " + ex.Message); }
        session.Start(InstalledFonts.Resolve(theme));
        if (session.LastError is not null) Rhino.RhinoApp.WriteLine("Margin preferences: " + session.LastError);
        Rhino.RhinoApp.Idle += Ready;
    }
    static void Ready(object? sender, EventArgs e)
    {
        var editor = Editor.Instance;
        Groups.WatchCanvas(editor?.Canvas);
        session.Attach(editor?.Canvas);
        if (editor?.Canvas is null || editor.Menu is null) return;
        var display = editor.Menu.Items.OfType<ButtonMenuItem>()
            .FirstOrDefault(i => i.Text.Replace("&", "").Equals("Display", StringComparison.OrdinalIgnoreCase));
        if (display is null) return;
        if (menu is not null && !ReferenceEquals(displayMenu, display)) RemoveMenu();
        if (menu is not null) { if (toggle is not null) toggle.Checked = DesiredEnabled; return; }
        displayMenu = display;
        menu = new ButtonMenuItem { Text = "Margin", ID = "MarginMenu" };
        var open = new ButtonMenuItem { Text = "Open Studio…" };
        open.Click += (_, _) => Show();
        toggle = new CheckMenuItem { Text = "Show component labels", Checked = DesiredEnabled };
        toggle.Click += (_, _) =>
        {
            try { SetEnabled(toggle.Checked); }
            catch (Exception ex) { toggle.Checked = DesiredEnabled; Rhino.RhinoApp.WriteLine("Margin preferences: " + ex.Message); }
        };
        menu.Items.Add(open); menu.Items.Add(toggle);
        displayMenu.Items.Add(menu);
    }
    public static void SetEnabled(bool value)
    {
        session.SetEnabled(value);
        if (toggle is not null) toggle.Checked = DesiredEnabled;
        Editor.Instance?.Canvas?.Invalidate();
    }
    public static void Apply(Theme theme)
    {
        session.Apply(InstalledFonts.Resolve(theme.Normalize()));
        Editor.Instance?.Canvas?.Invalidate();
    }
    public static void Save() => ThemeStore.Save(SettingsPath, Current);
    public static void Show()
    {
        if (inspector is null) { inspector = new Inspector(); inspector.Closed += (_, _) => inspector = null; }
        inspector.Present();
    }
    static void RemoveMenu()
    {
        if (menu is not null) { displayMenu?.Items.Remove(menu); menu.Dispose(); }
        menu = null; toggle = null; displayMenu = null;
    }
    public static void Stop()
    {
        Rhino.RhinoApp.Idle -= Ready;
        try { session.Stop(); }
        finally { inspector?.Close(); inspector = null; RemoveMenu(); }
    }
}
