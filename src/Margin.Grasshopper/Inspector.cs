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
    int languageRevision = -1;
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
#if MARGIN_RHINO7
        web.DocumentLoading += OnLegacyMessage;
#else
        web.MessageReceived += OnMessage;
#endif
        web.DocumentLoaded += OnLoaded;
        web.DocumentLoading += (_, e) => { if (e.Uri is not null && e.Uri.Scheme != "about") e.Cancel = true; };
        groupSelectionTimer.Elapsed += SyncGroupSelection;
        groupSelectionTimer.Start();
        Closed += (_, _) => { closed = true; groupSelectionTimer.Stop(); groupSelectionTimer.Dispose(); };
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Margin.Studio.html")
            ?? throw new InvalidOperationException(Localization.Text("ui.the_margin_studio_resource_is_missing"));
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
            var data = new { language = NativeLanguage.Snapshot(), userPresets, libraryError, theme = Runtime.Current, presets = Theme.Presets.Select(InstalledFonts.Resolve), fonts = ReadInstalledFonts(), enabled = Runtime.DesiredEnabled, session = SessionSnapshot(), groups = Groups.Snapshot(), native = true };
#if MARGIN_RHINO7
            await web.ExecuteScriptAsync(LegacyStudioBridge.Script);
#endif
            await web.ExecuteScriptAsync("window.MarginStudio.connect(" + JsonSerializer.Serialize(data) + ")");
            Ready = true;
        }
        catch (Exception ex) { LastError = ex.Message; Rhino.RhinoApp.WriteLine("Margin studio: " + ex.Message); }
    }
    async Task Reply(string message)
    {
        if (!closed) await web.ExecuteScriptAsync("window.MarginStudio.status(" + JsonSerializer.Serialize(message) + ")");
    }
#if MARGIN_RHINO7
    async void OnLegacyMessage(object? sender, WebViewLoadingEventArgs e)
    {
        if (e.Uri?.Scheme != "margin") return;
        e.Cancel = true;
        try
        {
            if (e.Uri.Host != "message") return;
            await HandleMessage(Uri.UnescapeDataString(e.Uri.AbsolutePath.TrimStart('/')));
        }
        catch (Exception ex) { LastError = ex.Message; Rhino.RhinoApp.WriteLine("Margin studio: " + ex.Message); }
        finally
        {
            try { if (!closed) await web.ExecuteScriptAsync("window.MarginLegacyAck()"); }
            catch (Exception ex) { LastError = ex.Message; Rhino.RhinoApp.WriteLine("Margin studio: " + ex.Message); }
        }
    }
#else
    async void OnMessage(object? sender, WebViewMessageEventArgs e) => await HandleMessage(e.Message);
#endif
    async Task HandleMessage(string message)
    {
        if (closed) return;
        try
        {
            if (message.Length > 40_000) throw new InvalidDataException(Localization.Text("ui.this_theme_is_too_large"));
            using var data = JsonDocument.Parse(message);
            var root = data.RootElement;
            var action = root.GetProperty("action").GetString();
            if (action is "apply" or "save" or "export") Runtime.Apply(Theme.FromJson(root.GetProperty("theme").GetRawText()));
            switch (action)
            {
                case "language":
                    NativeLanguage.Save(root.GetProperty("choice").GetString() ?? "auto");
                    await LanguageReply(true);
                    break;
                case "apply":
                    if (root.GetProperty("theme").GetProperty("FontFamily").GetString() != Runtime.Current.FontFamily)
                    {
                        await web.ExecuteScriptAsync("window.MarginStudio.setTheme(" + JsonSerializer.Serialize(Runtime.Current) + ")");
                        await Reply(Localization.Text("fonts.fallback", ("name", Runtime.Current.FontFamily)));
                    }
                    break;
                case "studio-section": groupsVisible = root.GetProperty("groups").GetBoolean(); break;
                case "groups-refresh": await GroupReply(); break;
                case "group-create":
                    Groups.Create(); await GroupReply(Localization.Text("ui.selection_grouped_edit_its_appearance_here_in_studio")); break;
                case "group-defaults":
                    Groups.SaveDefaults(JsonSerializer.Deserialize<GroupStyle>(root.GetProperty("style").GetRawText()) ?? throw new InvalidDataException(Localization.Text("ui.missing_group_style")));
                    await GroupReply(Localization.Text("ui.appearance_saved_for_future_groups_including_cmd_ctrl_g"), preserveDraft: true); break;
                case "group-apply":
                    Groups.BeginEdit(root.GetProperty("document").GetString()!, root.GetProperty("id").GetString()!,
                        root.GetProperty("revision").GetString()!).Save(root.GetProperty("name").GetString() ?? "",
                        JsonSerializer.Deserialize<GroupStyle>(root.GetProperty("style").GetRawText()) ?? throw new InvalidDataException(Localization.Text("ui.missing_group_style")));
                    await GroupReply(Localization.Text("ui.group_updated_save_your_grasshopper_file_to_keep_it"));
                    break;
                case "enabled": Runtime.SetEnabled(root.GetProperty("enabled").GetBoolean()); await SessionReply(true); break;
                case "save": Runtime.Save(); await Reply(Localization.Text("ui.theme_saved_on_this_computer")); break;
                case "preset-save":
                    var saved = PresetStore.Save(PresetsPath, root.GetProperty("name").GetString() ?? "",
                        Theme.FromJson(root.GetProperty("theme").GetRawText()),
                        root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null);
                    Runtime.Apply(saved.Theme);
                    await PresetReply(saved.Id, saved.Theme, Localization.Text("ui.preset_saved_on_this_computer"));
                    break;
                case "preset-rename":
                    var renameId = root.GetProperty("id").GetString();
                    var original = PresetStore.Load(PresetsPath).FirstOrDefault(p => p.Id == renameId)
                        ?? throw new InvalidDataException(Localization.Text("ui.that_preset_no_longer_exists"));
                    var renamed = PresetStore.Save(PresetsPath, root.GetProperty("name").GetString() ?? "", original.Theme, renameId);
                    Theme? renamedCurrent = (Runtime.Current with { Name = original.Theme.Name }) == original.Theme ? renamed.Theme : null;
                    if (renamedCurrent is not null) Runtime.Apply(renamedCurrent);
                    await PresetReply(renamed.Id, renamedCurrent, Localization.Text("preset.renamed"));
                    break;
                case "preset-delete":
                    PresetStore.Delete(PresetsPath, root.GetProperty("id").GetString() ?? "");
                    await PresetReply(null, null, Localization.Text("ui.preset_deleted_your_current_style_is_unchanged"));
                    break;
                case "fonts": await web.ExecuteScriptAsync("window.MarginStudio.setFonts(" + JsonSerializer.Serialize(ReadInstalledFonts()) + ")"); break;
                case "export":
                    var save = new SaveFileDialog { FileName = "Margin-theme.json" };
                    save.Filters.Add(new FileFilter(Localization.Text("ui.margin_theme"), ".json"));
                    if (save.ShowDialog(this) == DialogResult.Ok) { ThemeStore.Save(save.FileName, Runtime.Current); await Reply(Localization.Text("ui.theme_exported")); }
                    break;
                case "import":
                    var open = new OpenFileDialog();
                    open.Filters.Add(new FileFilter(Localization.Text("ui.margin_theme"), ".json"));
                    if (open.ShowDialog(this) == DialogResult.Ok)
                    {
                        if (new FileInfo(open.FileName).Length > 32_768) throw new InvalidDataException(Localization.Text("ui.choose_a_theme_smaller_than_32_kb"));
                        var theme = ThemeStore.Load(open.FileName);
                        Runtime.Apply(theme);
                        await web.ExecuteScriptAsync("window.MarginStudio.setTheme(" + JsonSerializer.Serialize(Runtime.Current) + ")");
                        await Reply(Localization.Text("ui.theme_imported_save_to_keep_it"));
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            if (!closed) await web.ExecuteScriptAsync("window.MarginStudio.setLanguage(" + JsonSerializer.Serialize(NativeLanguage.Snapshot()) + ")");
            await SessionReply(true);
            if (!closed) await web.ExecuteScriptAsync("window.MarginStudio.reportError(" + JsonSerializer.Serialize(ex.Message) + ")");
            await Reply(Localization.Text("change.failed", ("error", ex.Message)));
        }
    }
    async void SyncGroupSelection(object? sender, EventArgs e)
    {
        if (closed || !Ready || !Visible || syncingGroups) return;
        syncingGroups = true;
        try
        {
            await SessionReply();
            await LanguageReply();
            if (!groupsVisible) return;
            var snapshot = JsonSerializer.Serialize(Groups.Snapshot());
            if (snapshot == lastGroupSnapshot) return;
            await web.ExecuteScriptAsync("window.MarginStudio.setGroups(" + snapshot + ",null,true)");
            lastGroupSnapshot = snapshot;
        }
        catch (Exception ex) { LastError = ex.Message; }
        finally { syncingGroups = false; }
    }
    async Task LanguageReply(bool force = false)
    {
        if (closed || (!force && languageRevision == Localization.Revision)) return;
        await web.ExecuteScriptAsync("window.MarginStudio.setLanguage(" + JsonSerializer.Serialize(NativeLanguage.Snapshot()) + ")");
        languageRevision = Localization.Revision;
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
    async Task GroupReply(string? message = null, bool preserveDraft = false)
    {
        if (closed) return;
        var snapshot = JsonSerializer.Serialize(Groups.Snapshot());
        await web.ExecuteScriptAsync("window.MarginStudio.setGroups(" + snapshot + "," + JsonSerializer.Serialize(message ?? Localization.Text("group.refreshed")) + "," + (preserveDraft ? "true" : "false") + ")");
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
