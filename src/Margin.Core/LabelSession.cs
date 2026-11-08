namespace Margin.Core;

public interface ILabelRenderer : IDisposable
{
    Theme Theme { get; set; }
    bool Enabled { get; set; }
    string? LastError { get; }
}

public enum LabelSessionState { Stopped, WaitingForCanvas, Off, On, Paused }

// Owns the saved choice and renderer lease. Native adapters own host events and menus.
public sealed class LabelSession<TCanvas> where TCanvas : class
{
    readonly string preferencesPath;
    readonly Func<TCanvas, ILabelRenderer> createRenderer;
    readonly Action startGroups, stopGroups;
    TCanvas? canvas;
    ILabelRenderer? renderer;
    string? error;
    public bool Started { get; private set; }
    public bool DesiredEnabled { get; private set; }
    public bool EffectiveEnabled => Started && renderer?.Enabled == true;
    public Theme Theme { get; private set; } = new();
    public string? LastError => renderer?.LastError ?? error;
    public LabelSessionState State => !Started ? LabelSessionState.Stopped : canvas is null ? LabelSessionState.WaitingForCanvas
        : !DesiredEnabled ? LabelSessionState.Off : EffectiveEnabled ? LabelSessionState.On : LabelSessionState.Paused;

    public LabelSession(string preferencesPath, Func<TCanvas, ILabelRenderer> createRenderer, Action startGroups, Action stopGroups)
    { this.preferencesPath = preferencesPath; this.createRenderer = createRenderer; this.startGroups = startGroups; this.stopGroups = stopGroups; }

    public void Start(Theme theme)
    {
        if (Started) return;
        Theme = theme.Normalize(); error = null;
        try { DesiredEnabled = PreferencesStore.Load(preferencesPath).LabelsEnabled; }
        catch (Exception ex) { DesiredEnabled = false; error = ex.Message; }
        Started = true;
        try { startGroups(); }
        catch { Started = false; stopGroups(); throw; }
    }
    public void Attach(TCanvas? next)
    {
        if (!Started || ReferenceEquals(canvas, next)) return;
        Release(); canvas = next;
        if (canvas is not null) Create();
    }
    void Create()
    {
        try
        {
            renderer = createRenderer(canvas!);
            renderer.Theme = Theme;
            renderer.Enabled = DesiredEnabled;
            error = null;
        }
        catch (Exception ex) { Release(); error = ex.Message; }
    }
    void Release() { var old = renderer; renderer = null; old?.Dispose(); }
    public void SetEnabled(bool enabled)
    {
        PreferencesStore.Update(preferencesPath, p => p with { LabelsEnabled = enabled });
        DesiredEnabled = enabled; error = null;
        if (!Started || canvas is null) return;
        if (enabled && (renderer is null || renderer.LastError is not null)) { Release(); Create(); }
        if (renderer is not null) renderer.Enabled = enabled;
    }
    public void Apply(Theme theme) { Theme = theme.Normalize(); if (renderer is not null) renderer.Theme = Theme; }
    public void Stop()
    {
        if (!Started) return;
        Started = false;
        try { Release(); }
        finally { canvas = null; stopGroups(); }
    }
}
