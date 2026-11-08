using Margin.Core;
using System.Text.Json;
#if MARGIN_GH2
using Grasshopper2.Doc;
using Grasshopper2.SpecialObjects;
using Grasshopper2.UI;
using Grasshopper2.Undo;
using Grasshopper2.Undo.Actions;
using Eto.Drawing;
#else
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.GUI.Canvas;
using System.Drawing;
using System.Drawing.Drawing2D;
#endif

namespace Margin.Native;

public sealed record GroupSnapshot(string? Document, string Host, GroupEntry[] Items)
{ public GroupStyle Defaults => Groups.Defaults; }

// Native groups remain native groups: no replacement objects or changes to membership.
public static class Groups
{
    const string Key = "Margin.GroupStyle";
#if MARGIN_GH2
    static readonly List<WeakReference<Document>> documents = new();
#else
    static readonly List<WeakReference<GH_Document>> documents = new();
#endif
    static DateTime nextScan;
    static bool started;
    static Preferences preferences = new();
    public static GroupStyle Defaults => new(preferences.GroupFill, preferences.GroupOpacity, preferences.StyleGroups) { Appearance = preferences.GroupAppearance };
    public static void SaveDefaults(GroupStyle style)
    {
        style = style.Validate();
#if MARGIN_GH2
        var path = Margin.GH2.Runtime.PreferencesPath;
#else
        var path = Runtime.PreferencesPath;
#endif
        preferences = PreferencesStore.Update(path, p => p with { GroupFill = style.Fill, GroupOpacity = style.Opacity, StyleGroups = style.Designed, UseGroupColour = true, GroupAppearance = style.Appearance });
    }
    public static string? LastError { get; private set; }
#if MARGIN_GH2
    static Document? Active => Editor.Instance?.Canvas?.Document;
    static IEnumerable<GroupObject> Items(Document d) => d.Objects.Forwards.OfType<GroupObject>();
    static string Identity(Document d) => d.Identity.ToString();
    static string Read(GroupObject g) => g.CustomValues.Get(Key, "");
    public static GroupStyle Style(GroupObject g) => GroupStyle.Parse(Read(g)) ?? new(Grasshopper2.Eto.Drawing.OpenColor.Create(g.GroupColour, 6).ToHex(false), 65, false);
    static GroupEntry Entry(GroupObject g) => new(g.InstanceId.ToString(), g.UserName ?? "", g.ContentIds.Count(), g.Selected, Style(g), (g.UserName ?? "") + "|" + g.GroupColour + "|" + Read(g));
    public static GroupSnapshot Snapshot() => Active is { } d ? Snapshot(d) : new(null, "Grasshopper 2", []);
    public static GroupSnapshot Snapshot(Document d) => new(Identity(d), "Grasshopper 2", Items(d).Select(Entry).ToArray());
#else
    static GH_Document? Active => Instances.ActiveCanvas?.Document;
    static IEnumerable<GH_Group> Items(GH_Document d) => d.Objects.OfType<GH_Group>();
    static string Identity(GH_Document d) => d.DocumentID.ToString();
    static string StyleKey(GH_Group g) => Key + "." + g.InstanceGuid.ToString("N");
    static string Read(GH_Group g) => g.OnPingDocument()?.ValueTable.GetValue(StyleKey(g), "") ?? "";
    public static GroupStyle Style(GH_Group g)
    {
        // Uncoloured native groups inherit the canvas default; Empty is not transparent black.
        var colour = g.Colour.IsEmpty ? GH_Skin.group_back : g.Colour;
        var saved = GroupStyle.Parse(Read(g)) ?? new GroupStyle("#000000", 85);
        return saved with { Fill = $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}", Opacity = Math.Round(colour.A / 255d * 100, 1) };
    }
    static GroupEntry Entry(GH_Group g) => new(g.InstanceGuid.ToString(), g.NickName ?? "", g.ObjectIDs.Count, g.Attributes?.Selected ?? false, Style(g), (g.NickName ?? "") + "|" + g.Colour.ToArgb() + "|" + Read(g));
    public static GroupSnapshot Snapshot() => Active is { } d ? Snapshot(d) : new(null, "Grasshopper", []);
    public static GroupSnapshot Snapshot(GH_Document d) => new(Identity(d), "Grasshopper", Items(d).Select(Entry).ToArray());
#endif
    public static GroupEditSession BeginEdit(string document, string id, string revision)
    {
        var d = Active ?? throw new InvalidOperationException("Open a Grasshopper definition first.");
        return new(Entry(FindForEdit(d, document, id, revision)),
            (currentRevision, name, style) => ApplyEdit(Active ?? throw new InvalidOperationException("Open a Grasshopper definition first."),
                document, id, currentRevision, name, style), SaveDefaults);
    }
#if MARGIN_GH2
    public static GroupEditSession BeginEdit(Document d, string document, string id, string revision)
#else
    public static GroupEditSession BeginEdit(GH_Document d, string document, string id, string revision)
#endif
        => new(Entry(FindForEdit(d, document, id, revision)),
            (currentRevision, name, style) => ApplyEdit(d, document, id, currentRevision, name, style), SaveDefaults);

#if MARGIN_GH2
    static GroupObject FindForEdit(Document d, string document, string id, string revision)
#else
    static GH_Group FindForEdit(GH_Document d, string document, string id, string revision)
#endif
    {
        if (Identity(d) != document) throw new InvalidOperationException("The active definition changed. Reload the group before editing.");
        var group = Items(d).FirstOrDefault(g => Entry(g).Id == id)
            ?? throw new InvalidOperationException("That group was removed. Select another group.");
        if (Entry(group).Revision != revision) throw new InvalidOperationException("This group changed in Grasshopper. Reload it before applying your edit.");
        return group;
    }
    public static void Apply(string document, string id, string revision, string name, GroupStyle style)
    {
        var d = Active ?? throw new InvalidOperationException("Open a Grasshopper definition first.");
        ApplyEdit(d, document, id, revision, name, style);
    }
#if MARGIN_GH2
    public static void Apply(Document d, string document, string id, string revision, string name, GroupStyle style)
#else
    public static void Apply(GH_Document d, string document, string id, string revision, string name, GroupStyle style)
#endif
        => ApplyEdit(d, document, id, revision, name, style);

#if MARGIN_GH2
    static GroupEntry ApplyEdit(Document d, string document, string id, string revision, string name, GroupStyle style)
#else
    static GroupEntry ApplyEdit(GH_Document d, string document, string id, string revision, string name, GroupStyle style)
#endif
    {
        style = style.Validate(); name = name.Trim();
        if (name.Length > 120 || name.Any(char.IsControl)) throw new InvalidDataException("Use a single-line group name of up to 120 characters.");
        var g = FindForEdit(d, document, id, revision);
        var old = Entry(g);
#if MARGIN_GH2
        if (old.Name == name && old.Style == style) return old;
#else
        // GH1 stores alpha in a byte; compare against the value it can represent.
        var alpha = (int)Math.Round(style.Opacity * 2.55);
        var displayedStyle = style with { Opacity = Math.Round(alpha / 255d * 100, 1) };
        if (old.Name == name && old.Style == displayedStyle) return old;
#endif
#if MARGIN_GH2
        var actions = new Grasshopper2.Undo.Action[] { new RenameAction(g), new CustomValueAction(g, Key, Effects.ExpireLayout), new PropertyAction(g, nameof(GroupObject.GroupColour), Effects.ExpireLayout, false) };
        g.AddUndoRecord(new Record(new VerbNoun("Edit", "group"), actions));
        g.UserName = name;
        g.GroupColour = Grasshopper2.Eto.Drawing.OpenColor.MatchFamily(Color.Parse(style.Fill));
        g.CustomValues.Set(Key, JsonSerializer.Serialize(style));
        g.Attributes.InvalidateLayout();
        g.OnLayoutChanged();
#else
        var next = Color.FromArgb(alpha, Convert.ToInt32(style.Fill[1..3],16), Convert.ToInt32(style.Fill[3..5],16), Convert.ToInt32(style.Fill[5..7],16));
        g.RecordUndoEvent("Margin: edit group", new EditGroupAction(g, name, next, JsonSerializer.Serialize(style)));
        Set(g, name, next, JsonSerializer.Serialize(style));
#endif
        Attach(d);
        Refresh();
        return Entry(g);
    }
#if !MARGIN_GH2
    static void Set(GH_Group g, string name, Color colour, string value)
    {
        g.NickName = name; g.Colour = colour;
        var d = g.OnPingDocument();
        d?.ValueTable.SetValue(StyleKey(g), value);
        g.ExpireCaches(); g.Attributes?.ExpireLayout(); d?.Modified();
    }
    sealed class EditGroupAction : Grasshopper.Kernel.Undo.GH_UndoAction
    {
        readonly Guid id; readonly string beforeName, afterName, beforeStyle, afterStyle; readonly Color beforeColour, afterColour;
        public EditGroupAction(GH_Group g, string name, Color colour, string style)
        { id = g.InstanceGuid; beforeName = g.NickName; afterName = name; beforeColour = g.Colour; afterColour = colour; beforeStyle = Groups.Read(g); afterStyle = style; }
        public override bool ExpiresDisplay => true;
        public override bool ExpiresSolution => false;
        protected override void Internal_Undo(GH_Document d) { if (Items(d).FirstOrDefault(g => g.InstanceGuid == id) is { } g) Set(g, beforeName, beforeColour, beforeStyle); }
        protected override void Internal_Redo(GH_Document d) { if (Items(d).FirstOrDefault(g => g.InstanceGuid == id) is { } g) Set(g, afterName, afterColour, afterStyle); }
    }
#endif
    public static void Start()
    {
        if (started) return;
        started = true;
#if MARGIN_GH2
        var path = Margin.GH2.Runtime.PreferencesPath;
#else
        var path = Runtime.PreferencesPath;
        Instances.CanvasCreated += WatchCanvas;
        if (Instances.ActiveCanvas is { } canvas) WatchCanvas(canvas);
#endif
        try { preferences = PreferencesStore.Load(path); }
        catch (Exception ex) { LastError = ex.Message; }
        Rhino.RhinoApp.Idle += Scan;
    }
    static void Scan(object? sender, EventArgs e)
    {
        if (DateTime.UtcNow < nextScan) return;
        nextScan = DateTime.UtcNow.AddMilliseconds(400);
        try { if (Active is { } d) Attach(d); }
        catch (Exception ex) { LastError = ex.Message; Rhino.RhinoApp.Idle -= Scan; }
    }
#if MARGIN_GH2
    public static void Attach(Document d)
    {
        documents.RemoveAll(w => !w.TryGetTarget(out _));
        if (!documents.Any(w => w.TryGetTarget(out var target) && ReferenceEquals(target, d))) documents.Add(new(d));
        foreach (var g in Items(d)) if (g.Attributes?.GetType() == typeof(GroupAttributes)) g.OverrideAttributes(new StyledGroup(g));
    }
    static void Refresh() => Editor.Instance?.Canvas?.Invalidate();
#else
    static GH_Canvas? watchedCanvas;
    static void WatchCanvas(GH_Canvas canvas)
    {
        if (ReferenceEquals(watchedCanvas, canvas)) return;
        if (watchedCanvas is not null) watchedCanvas.DocumentChanged -= ChangedDocument;
        watchedCanvas = canvas;
        canvas.DocumentChanged += ChangedDocument;
        if (canvas.Document is { } d) Attach(d);
    }
    static void ChangedDocument(GH_Canvas canvas, GH_CanvasDocumentChangedEventArgs e)
    { if (e.NewDocument is { } d) Attach(d); }
    static void Added(object sender, GH_DocObjectEventArgs e)
    {
        try
        {
            foreach (var g in e.Objects.OfType<GH_Group>())
            {
                // Preserve pasted/restored styles and explicit native colours.
                if (Read(g).Length == 0 && g.Colour.IsEmpty)
                {
                    var style = Defaults;
                    var colour = preferences.UseGroupColour
                        ? Color.FromArgb((int)Math.Round(style.Opacity * 2.55), ColorTranslator.FromHtml(style.Fill))
                        : GH_Skin.group_back;
                    Set(g, g.NickName, colour, JsonSerializer.Serialize(style));
                }
            }
            Attach(e.Document);
        }
        catch (Exception ex) { LastError = ex.Message; Rhino.RhinoApp.WriteLine("Margin group defaults: " + ex.Message); }
    }
    public static GH_Group Create()
    {
        var d = Active ?? throw new InvalidOperationException("Open a Grasshopper definition first.");
        return Create(d);
    }
    public static GH_Group Create(GH_Document d)
    {
        var selected = d.Objects.Where(o => o.Attributes?.Selected == true).ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("Select components on the canvas first.");
        Attach(d);
        var group = new GH_Group();
        foreach (var o in selected) group.AddObject(o.InstanceGuid);
        d.AddObject(group, false);
        d.UndoUtil.RecordAddObjectEvent("Group selection", group);
        foreach (var o in selected) o.Attributes.Selected = false;
        group.Attributes.Selected = true;
        d.Modified(); Refresh();
        return group;
    }
    public static void Attach(GH_Document d)
    {
        documents.RemoveAll(w => !w.TryGetTarget(out _));
        if (!documents.Any(w => w.TryGetTarget(out var target) && ReferenceEquals(target, d)))
        { documents.Add(new(d)); d.ObjectsAdded += Added; }
        foreach (var g in Items(d)) if (g.Attributes?.GetType() == typeof(GH_GroupAttributes)) g.Attributes = new StyledGroup(g) { Selected = g.Attributes.Selected };
    }
    static void Refresh() => Instances.ActiveCanvas?.Invalidate();
#endif
    public static void Stop()
    {
        Rhino.RhinoApp.Idle -= Scan; started = false;
#if !MARGIN_GH2
        Instances.CanvasCreated -= WatchCanvas;
        if (watchedCanvas is not null) watchedCanvas.DocumentChanged -= ChangedDocument;
        watchedCanvas = null;
        foreach (var weak in documents) if (weak.TryGetTarget(out var doc)) doc.ObjectsAdded -= Added;
#endif
        // Attributes are harmless when detached; restore the active document immediately.
        foreach (var weak in documents) if (weak.TryGetTarget(out var d)) foreach (var g in Items(d)) if (g.Attributes is StyledGroup)
        {
#if MARGIN_GH2
            g.OverrideAttributes(new GroupAttributes(g));
#else
            g.Attributes = new GH_GroupAttributes(g) { Selected = g.Attributes.Selected };
#endif
        }
        documents.Clear();
        Refresh();
    }
}
