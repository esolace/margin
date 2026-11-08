using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.GUI.Canvas;
using System.Windows.Forms;
using Margin.Core;

namespace Margin.Native;

public sealed class Info : GH_AssemblyInfo
{
    public override string Name => "Margin";
    public override string Description => "Designed component labels for Grasshopper.";
    public override Guid Id => new("1cbb85f4-a387-42a3-9da2-7c4a9a974790");
    public override string AuthorName => "Solace";
    public override string AuthorContact => "";
}
public sealed class Startup : GH_AssemblyPriority
{
    public override GH_LoadingInstruction PriorityLoad() { Runtime.Start(); return GH_LoadingInstruction.Proceed; }
}
public static class Runtime
{
    static Inspector? inspector;
    static ToolStripMenuItem? menu;
    static ToolStripMenuItem? labelsToggle;
    public static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Margin", "theme.json");
    public static string PreferencesPath => Path.Combine(Path.GetDirectoryName(SettingsPath)!, "preferences.json");
    static readonly LabelSession<GH_Canvas> session = new(PreferencesPath, canvas => new Labels(canvas), Groups.Start, Groups.Stop);
    public static Theme Current => session.Theme;
    public static bool DesiredEnabled => session.DesiredEnabled;
    public static LabelSessionState SessionState => session.State;
    public static string? LastError => session.LastError;
    public static Eto.Forms.Window Owner => Instances.EtoDocumentEditor ?? Rhino.UI.RhinoEtoApp.MainWindowForDocument(Rhino.RhinoDoc.ActiveDoc);
    public static bool Enabled => session.EffectiveEnabled;
    public static void Start()
    {
        if (session.Started) return;
        var theme = new Theme();
        try { if (File.Exists(SettingsPath)) theme = ThemeStore.Load(SettingsPath); }
        catch (Exception ex) { Rhino.RhinoApp.WriteLine("Margin: using Paper theme. " + ex.Message); }
        session.Start(InstalledFonts.Resolve(theme));
        if (session.LastError is not null) Rhino.RhinoApp.WriteLine("Margin preferences: " + session.LastError);
        Instances.CanvasCreated += Attach;
        Rhino.RhinoApp.Idle += Ready;
    }
    static void Ready(object? sender, EventArgs args)
    {
        var canvas = Instances.ActiveCanvas;
        var editor = Instances.DocumentEditor;
        session.Attach(canvas);
        if (canvas is null || editor?.MainMenuStrip is null) return;
        var display = editor.MainMenuStrip.Items.OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => item.Name == "mnuDisplay");
        if (display is null) return;
        if (menu is not null && !ReferenceEquals(menu.Owner, display.DropDown)) RemoveMenu();
        if (menu is null)
        {
            menu = new ToolStripMenuItem("Margin") { Name = "MarginMenu" };
            menu.DropDownItems.Add("Open Studio…", null, (_, _) => Show());
            labelsToggle = new ToolStripMenuItem("Show component labels") { CheckOnClick = true, Checked = DesiredEnabled };
            labelsToggle.Click += (_, _) =>
            {
                try { SetEnabled(labelsToggle.Checked); }
                catch (Exception ex) { labelsToggle.Checked = DesiredEnabled; Rhino.RhinoApp.WriteLine("Margin preferences: " + ex.Message); }
            };
            menu.DropDownOpening += (_, _) => labelsToggle.Checked = DesiredEnabled;
            menu.DropDownItems.Add(labelsToggle);
            display.DropDownItems.Add(menu);
        }
    }
    static void Attach(GH_Canvas canvas) => session.Attach(canvas);
    public static void SetEnabled(bool enabled)
    {
        session.SetEnabled(enabled); Instances.ActiveCanvas?.Invalidate();
        if (labelsToggle is not null) labelsToggle.Checked = DesiredEnabled;
    }
    public static void Apply(Theme theme) { session.Apply(InstalledFonts.Resolve(theme.Normalize())); Instances.ActiveCanvas?.Invalidate(); }
    public static void Save() => ThemeStore.Save(SettingsPath, Current);
    public static void Show()
    {
        if (inspector is null)
        {
            inspector = new Inspector();
            inspector.Closed += (_, _) => inspector = null;
        }
        inspector.Present();
    }
    static void RemoveMenu()
    {
        if (menu is not null) { menu.Owner?.Items.Remove(menu); menu.Dispose(); menu = null; }
        labelsToggle = null;
    }
    public static void Stop()
    {
        Instances.CanvasCreated -= Attach; Rhino.RhinoApp.Idle -= Ready;
        try { session.Stop(); }
        finally { inspector?.Close(); inspector = null; RemoveMenu(); }
    }
}
