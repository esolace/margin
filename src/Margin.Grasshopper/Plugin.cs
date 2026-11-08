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
    static bool started;
    static bool preferredEnabled;
    static Labels? labels;
    static Inspector? inspector;
    static ToolStripMenuItem? menu;
    static ToolStripMenuItem? labelsToggle;
    static GH_Canvas? attached;
    public static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Margin", "theme.json");
    public static string PreferencesPath => Path.Combine(Path.GetDirectoryName(SettingsPath)!, "preferences.json");
    public static Theme Current { get; private set; } = new();
    public static Eto.Forms.Window Owner => Instances.EtoDocumentEditor ?? Rhino.UI.RhinoEtoApp.MainWindowForDocument(Rhino.RhinoDoc.ActiveDoc);
    public static bool Enabled => labels?.Enabled ?? false;
    public static void Start()
    {
        if (started) return;
        started = true; Groups.Start();
        try { if (File.Exists(SettingsPath)) Current = ThemeStore.Load(SettingsPath); } catch (Exception ex) { Rhino.RhinoApp.WriteLine("Margin: using Paper theme. " + ex.Message); }
        try { preferredEnabled = PreferencesStore.Load(PreferencesPath).LabelsEnabled; }
        catch (Exception ex) { Rhino.RhinoApp.WriteLine("Margin preferences: " + ex.Message); }
        Current = InstalledFonts.Resolve(Current);
        Instances.CanvasCreated += Attach;
        Rhino.RhinoApp.Idle += Ready;
    }
    static void Ready(object? sender, EventArgs args)
    {
        var canvas = Instances.ActiveCanvas;
        var editor = Instances.DocumentEditor;
        if (canvas is null || editor?.MainMenuStrip is null) return;
        Attach(canvas);
        if (menu is null)
        {
            var display = editor.MainMenuStrip.Items.OfType<ToolStripMenuItem>()
                .FirstOrDefault(item => item.Name == "mnuDisplay");
            if (display is null) return;
            menu = new ToolStripMenuItem("Margin") { Name = "MarginMenu" };
            menu.DropDownItems.Add("Open Studio…", null, (_, _) => Show());
            labelsToggle = new ToolStripMenuItem("Show component labels") { CheckOnClick = true, Checked = Enabled };
            labelsToggle.Click += (_, _) => SetEnabled(labelsToggle.Checked);
            menu.DropDownOpening += (_, _) => labelsToggle.Checked = Enabled;
            menu.DropDownItems.Add(labelsToggle);
            display.DropDownItems.Add(menu);
        }
        Rhino.RhinoApp.Idle -= Ready;
    }
    static void Attach(GH_Canvas canvas)
    {
        if (ReferenceEquals(attached, canvas)) return;
        labels?.Dispose(); attached = canvas;
        labels = new Labels(canvas) { Theme = Current, Enabled = preferredEnabled };
    }
    public static void SetEnabled(bool enabled)
    {
        PreferencesStore.Update(PreferencesPath, p => p with { LabelsEnabled = enabled });
        preferredEnabled = enabled;
        if (attached is null) return;
        if (labels is null || labels.LastError is not null) { labels?.Dispose(); labels = new Labels(attached) { Theme = Current, Enabled = preferredEnabled }; }
        labels.Enabled = enabled; attached.Invalidate();
        if (labelsToggle is not null) labelsToggle.Checked = enabled;
    }
    public static void Apply(Theme theme) { Current = InstalledFonts.Resolve(theme.Normalize()); if (labels is not null) labels.Theme = Current; attached?.Invalidate(); }
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
    public static void Stop()
    {
        Groups.Stop();
        Instances.CanvasCreated -= Attach; Rhino.RhinoApp.Idle -= Ready;
        labels?.Dispose(); labels = null; attached = null;
        inspector?.Close(); inspector = null;
        if (menu is not null) { menu.Owner?.Items.Remove(menu); menu.Dispose(); menu = null; }
        labelsToggle = null;
        started = false;
    }
}
