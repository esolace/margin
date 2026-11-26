using Margin.Core;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Margin.Native;

public enum GroupInitialization { OpenedDocument, Observed, AddedWithDefaultColour, CreatedGroup }

public sealed record GroupInitializationResult(string Metadata, GroupStyle? AppliedStyle);

// Owns readiness and appearance adoption. Native adapters retain storage and host events.
public sealed class GroupAppearanceSession
{
    sealed class DocumentState
    {
        public long AttachmentGeneration;
        public readonly HashSet<string> Initialized = new();
        public readonly Dictionary<string, (long Generation, string Metadata)> Observed = new();
    }
    readonly string preferencesPath;
    readonly ConditionalWeakTable<object, DocumentState> documents = new();
    Preferences preferences = new();
    Action? release;
    long generation;
    public bool Started { get; private set; }
    public string? LastError { get; private set; }
    public bool HasSavedDefaults => preferences.UseGroupColour;
    public GroupStyle Defaults => new(preferences.GroupFill, preferences.GroupOpacity, preferences.StyleGroups) { Appearance = preferences.GroupAppearance };

    public GroupAppearanceSession(string preferencesPath) => this.preferencesPath = preferencesPath;

    public void Start(Action attach, Action detach)
    {
        if (Started) return;
        preferences = new(); LastError = null;
        try { preferences = PreferencesStore.Load(preferencesPath); }
        catch (Exception ex) { LastError = ex.Message; }
        Started = true; release = detach; generation++;
        try { attach(); }
        catch { Stop(); throw; }
    }
    public void Stop()
    {
        if (!Started) return;
        Started = false;
        var detach = release; release = null;
        detach?.Invoke();
    }
    public void SaveDefaults(GroupStyle style)
    {
        style = style.Validate();
        preferences = PreferencesStore.Update(preferencesPath, p => p with
        {
            GroupFill = style.Fill, GroupOpacity = style.Opacity, StyleGroups = style.Designed,
            UseGroupColour = true, GroupAppearance = style.Appearance
        });
    }
    public bool Attach(object document)
    {
        RequireReady();
        // Start cycles allow groups added while stopped to initialize; the
        // identity set survives so restored groups never adopt newer defaults.
        var state = documents.GetOrCreateValue(document);
        var first = state.AttachmentGeneration != generation;
        state.AttachmentGeneration = generation;
        return first;
    }
    public static string Read(string portable, string legacy) => portable.Length > 0 ? portable : legacy;

    public GroupInitializationResult Initialize(object document, string id, string portable, string legacy,
        Func<GroupStyle> nativeStyle, GroupInitialization initialization)
    {
        RequireReady();
        var state = documents.GetOrCreateValue(document);
        var first = state.Initialized.Add(id);
        var saved = Read(portable, legacy);
        // A host can announce Create after a scan saw the inserted object. Only
        // this session's exact untouched snapshot may be upgraded by that event.
        var observedCreation = initialization == GroupInitialization.CreatedGroup && legacy.Length == 0 &&
            state.Observed.TryGetValue(id, out var observed) && observed.Generation == generation && observed.Metadata == saved;
        if (initialization == GroupInitialization.CreatedGroup) state.Observed.Remove(id);
        if (saved.Length > 0 && !observedCreation) return new(saved, null);
        if ((first || observedCreation) && HasSavedDefaults && initialization != GroupInitialization.Observed)
            return new(JsonSerializer.Serialize(Defaults), Defaults);
        if (first && initialization == GroupInitialization.AddedWithDefaultColour)
        {
            var native = nativeStyle();
            var style = Defaults with { Fill = native.Fill, Opacity = native.Opacity };
            return new(JsonSerializer.Serialize(style), style);
        }
        var metadata = saved.Length > 0 ? saved : JsonSerializer.Serialize(nativeStyle());
        if (first && initialization == GroupInitialization.Observed)
            state.Observed[id] = (generation, metadata);
        return new(metadata, null);
    }
    void RequireReady()
    {
        if (!Started) throw new InvalidOperationException("Start the group appearance session before attaching a definition.");
    }
}
