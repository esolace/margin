using System.Reflection;
using System.Text.Json;
using Eto.Drawing;
using Eto.Forms;
using Margin.Core;
using Margin.Presets;
using Theme = Margin.Core.Theme;

#if MARGIN_GH2
using Runtime = Margin.GH2.Runtime;
#endif

namespace Margin.Native;

// A single HTML source powers both the standalone studio and both desktop platforms.
public sealed class Inspector : Form
{
    readonly WebView web = new() { BrowserContextMenuEnabled = false };
    bool closed;
    bool groupsVisible;
    bool syncingGroups;
    string? lastGroupSnapshot;
    string? lastSessionSnapshot;
    readonly UITimer groupSelectionTimer = new() { Interval = .4 };
    static string PresetsPath => Path.Combine(Path.GetDirectoryName(Runtime.SettingsPath)!, "presets.json");
    public bool Ready { get; private set; }
    public int FontCount { get; private set; }
    public string? LastError { get; private set; }
    public Inspector()
    {
        Title = "Margin · Studio";
        ClientSize = new Size(1080, 760);
        MinimumSize = new Size(900, 600);
        BackgroundColor = Color.FromArgb(248, 250, 252);
        Content = web;
        web.MessageReceived += OnMessage;
        web.DocumentLoaded += OnLoaded;
        web.DocumentLoading += (_, e) => { if (e.Uri is not null && e.Uri.Scheme != "about") e.Cancel = true; };
        groupSelectionTimer.Elapsed += SyncGroupSelection;
        groupSelectionTimer.Start();
        Closed += (_, _) => { closed = true; groupSelectionTimer.Stop(); groupSelectionTimer.Dispose(); };
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Margin.Studio.html")
            ?? throw new InvalidOperationException("The Margin studio resource is missing.");
        using var reader = new StreamReader(stream);
        web.LoadHtml(reader.ReadToEnd());
    }
    string[] ReadInstalledFonts()
    {
        var fonts = InstalledFonts.Names();
        FontCount = fonts.Length;
        return fonts;
    }
    async void OnLoaded(object? sender, WebViewLoadedEventArgs e)
    {
        try
        {
            string? libraryError = null;
            List<SavedPreset> userPresets;
            try { userPresets = PresetStore.Load(PresetsPath); }
            catch (Exception ex) { userPresets = new(); libraryError = ex.Message; }
            var data = new { userPresets, libraryError, theme = Runtime.Current, presets = Theme.Presets.Select(InstalledFonts.Resolve), fonts = ReadInstalledFonts(), enabled = Runtime.DesiredEnabled, session = SessionSnapshot(), groups = Groups.Snapshot(), native = true };
            await web.ExecuteScriptAsync("window.MarginStudio.connect(" + JsonSerializer.Serialize(data) + ")");
            Ready = true;
        }
        catch (Exception ex) { LastError = ex.Message; Rhino.RhinoApp.WriteLine("Margin studio: " + ex.Message); }
    }
    async Task Reply(string message)
    {
        if (!closed) await web.ExecuteScriptAsync("window.MarginStudio.status(" + JsonSerializer.Serialize(message) + ")");
    }
    async void OnMessage(object? sender, WebViewMessageEventArgs e)
    {
        if (closed) return;
        try
        {
            if (e.Message.Length > 40_000) throw new InvalidDataException("This theme is too large.");
            using var data = JsonDocument.Parse(e.Message);
            var root = data.RootElement;
            var action = root.GetProperty("action").GetString();
            if (action is "apply" or "save" or "export") Runtime.Apply(Theme.FromJson(root.GetProperty("theme").GetRawText()));
            switch (action)
            {
                case "apply":
                    if (root.GetProperty("theme").GetProperty("FontFamily").GetString() != Runtime.Current.FontFamily)
                    {
                        await web.ExecuteScriptAsync("window.MarginStudio.setTheme(" + JsonSerializer.Serialize(Runtime.Current) + ")");
                        await Reply("That font is not installed here. Using " + Runtime.Current.FontFamily + ".");
                    }
                    break;
                case "studio-section": groupsVisible = root.GetProperty("groups").GetBoolean(); break;
                case "groups-refresh": await GroupReply(); break;
                case "group-create":
                    Groups.Create(); await GroupReply("Selection grouped. Edit its appearance here in Studio."); break;
                case "group-defaults":
                    Groups.SaveDefaults(JsonSerializer.Deserialize<GroupStyle>(root.GetProperty("style").GetRawText()) ?? throw new InvalidDataException("Missing group style."));
                    await GroupReply("Appearance saved for future groups, including Cmd/Ctrl+G.", preserveDraft: true); break;
                case "group-apply":
                    Groups.BeginEdit(root.GetProperty("document").GetString()!, root.GetProperty("id").GetString()!,
                        root.GetProperty("revision").GetString()!).Save(root.GetProperty("name").GetString() ?? "",
                        JsonSerializer.Deserialize<GroupStyle>(root.GetProperty("style").GetRawText()) ?? throw new InvalidDataException("Missing group style."));
                    await GroupReply("Group updated. Save your Grasshopper file to keep it.");
                    break;
                case "enabled": Runtime.SetEnabled(root.GetProperty("enabled").GetBoolean()); await SessionReply(true); break;
                case "save": Runtime.Save(); await Reply("Theme saved on this computer."); break;
                case "preset-save":
                    var saved = PresetStore.Save(PresetsPath, root.GetProperty("name").GetString() ?? "",
                        Theme.FromJson(root.GetProperty("theme").GetRawText()),
                        root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null);
                    Runtime.Apply(saved.Theme);
                    await PresetReply(saved.Id, saved.Theme, "Preset saved on this computer.");
                    break;
                case "preset-rename":
                    var renameId = root.GetProperty("id").GetString();
                    var original = PresetStore.Load(PresetsPath).FirstOrDefault(p => p.Id == renameId)
                        ?? throw new InvalidDataException("That preset no longer exists.");
                    var renamed = PresetStore.Save(PresetsPath, root.GetProperty("name").GetString() ?? "", original.Theme, renameId);
                    Theme? renamedCurrent = (Runtime.Current with { Name = original.Theme.Name }) == original.Theme ? renamed.Theme : null;
                    if (renamedCurrent is not null) Runtime.Apply(renamedCurrent);
                    await PresetReply(renamed.Id, renamedCurrent, "Preset renamed.");
                    break;
                case "preset-delete":
                    PresetStore.Delete(PresetsPath, root.GetProperty("id").GetString() ?? "");
                    await PresetReply(null, null, "Preset deleted. Your current style is unchanged.");
                    break;
                case "fonts": await web.ExecuteScriptAsync("window.MarginStudio.setFonts(" + JsonSerializer.Serialize(ReadInstalledFonts()) + ")"); break;
                case "export":
                    var save = new SaveFileDialog { FileName = "Margin-theme.json" };
                    save.Filters.Add(new FileFilter("Margin theme", ".json"));
                    if (save.ShowDialog(this) == DialogResult.Ok) { ThemeStore.Save(save.FileName, Runtime.Current); await Reply("Theme exported."); }
                    break;
                case "import":
                    var open = new OpenFileDialog();
                    open.Filters.Add(new FileFilter("Margin theme", ".json"));
                    if (open.ShowDialog(this) == DialogResult.Ok)
                    {
                        if (new FileInfo(open.FileName).Length > 32_768) throw new InvalidDataException("Choose a theme smaller than 32 KB.");
                        var theme = ThemeStore.Load(open.FileName);
                        Runtime.Apply(theme);
                        await web.ExecuteScriptAsync("window.MarginStudio.setTheme(" + JsonSerializer.Serialize(Runtime.Current) + ")");
                        await Reply("Theme imported. Save to keep it.");
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            await SessionReply(true);
            if (!closed) await web.ExecuteScriptAsync("window.MarginStudio.reportError(" + JsonSerializer.Serialize(ex.Message) + ")");
            await Reply("Couldn’t apply that change: " + ex.Message);
        }
    }
    async void SyncGroupSelection(object? sender, EventArgs e)
    {
        if (closed || !Ready || !Visible || syncingGroups) return;
        syncingGroups = true;
        try
        {
            await SessionReply();
            if (!groupsVisible) return;
            var snapshot = JsonSerializer.Serialize(Groups.Snapshot());
            if (snapshot == lastGroupSnapshot) return;
            await web.ExecuteScriptAsync("window.MarginStudio.setGroups(" + snapshot + ",null,true)");
            lastGroupSnapshot = snapshot;
        }
        catch (Exception ex) { LastError = ex.Message; }
        finally { syncingGroups = false; }
    }
    static object SessionSnapshot() => new { desired = Runtime.DesiredEnabled, effective = Runtime.Enabled,
        state = Runtime.SessionState.ToString(), error = Runtime.LastError };
    async Task SessionReply(bool force = false)
    {
        if (closed) return;
        var snapshot = JsonSerializer.Serialize(SessionSnapshot());
        if (!force && snapshot == lastSessionSnapshot) return;
        await web.ExecuteScriptAsync("window.MarginStudio.setSession(" + snapshot + ")");
        lastSessionSnapshot = snapshot;
    }
    async Task GroupReply(string message = "Groups refreshed.", bool preserveDraft = false)
    {
        if (closed) return;
        var snapshot = JsonSerializer.Serialize(Groups.Snapshot());
        await web.ExecuteScriptAsync("window.MarginStudio.setGroups(" + snapshot + "," + JsonSerializer.Serialize(message) + "," + (preserveDraft ? "true" : "false") + ")");
        lastGroupSnapshot = snapshot;
    }
    async Task PresetReply(string? selectedId, Theme? theme, string message)
    {
        if (!closed) await web.ExecuteScriptAsync("window.MarginStudio.setUserPresets(" +
            JsonSerializer.Serialize(new { items = PresetStore.Load(PresetsPath), selectedId, theme, message }) + ")");
    }
    public Task<string> InspectAsync(string script) => web.ExecuteScriptAsync(script);

    public void Present()
    {
        Owner = Runtime.Owner;
        StudioWindow.Prepare(this);
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        if (!Visible) Show();
        StudioWindow.KeepOnScreen(this);
        BringToFront();
    }
}
