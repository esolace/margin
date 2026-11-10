using Margin.Core;

static class SessionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "margin-session-" + Guid.NewGuid());
        var path = Path.Combine(directory, "preferences.json");
        try
        {
            PreferencesStore.Update(path, p => p with { LabelsEnabled = true });
            var host = new Host();
            var session = new LabelSession<Canvas>(path, host.Attach, host.StartGroups, host.StopGroups);
            session.Start(new Theme());
            check(session.DesiredEnabled && !session.EffectiveEnabled && session.State == LabelSessionState.WaitingForCanvas,
                "saved ON is retained while waiting for a canvas");
            var first = new Canvas(); session.Attach(first);
            check(session.EffectiveEnabled && first.Draw() == 1, "attaching a canvas honours saved ON");
            session.Start(new Theme()); session.Attach(first);
            check(first.Draw() == 1 && host.GroupsStarted == 1, "repeated start and attach do not duplicate paint or group handlers");
            host.Latest!.Fail();
            check(session.State == LabelSessionState.Paused && session.DesiredEnabled && PreferencesStore.Load(path).LabelsEnabled,
                "drawing failure pauses only the renderer and preserves saved ON");
            session.Attach(first);
            check(session.State == LabelSessionState.Paused && first.Draw() == 0,
                "idle attach does not silently retry a failed renderer");
            session.SetEnabled(true);
            check(session.State == LabelSessionState.On && first.Draw() == 1, "explicit retry creates one working renderer");
            var second = new Canvas(); session.Attach(second);
            check(first.Draw() == 0 && second.Draw() == 1, "canvas replacement releases the previous subscription");
            session.SetEnabled(false); session.Attach(first);
            check(session.State == LabelSessionState.Off && first.Draw() == 0 && !PreferencesStore.Load(path).LabelsEnabled,
                "saved OFF is honoured after canvas replacement");
            session.Stop(); session.Stop();
            check(first.Draw() == 0 && second.Draw() == 0 && host.GroupsStopped == 1 && session.State == LabelSessionState.Stopped,
                "repeated stop releases rendering and group lifecycle once");
            session.Start(new Theme()); session.Attach(first);
            check(!session.DesiredEnabled && !session.EffectiveEnabled, "fresh startup restores saved OFF");
            session.SetEnabled(true); session.Attach(null);
            check(first.Draw() == 0 && session.State == LabelSessionState.WaitingForCanvas && session.DesiredEnabled,
                "closing a canvas releases drawing without changing the saved choice");
            host.FailAttach = true; session.Attach(second);
            check(session.State == LabelSessionState.Paused && session.LastError is not null, "failed renderer attachment is contained");
            host.FailAttach = false; session.Attach(first);
            check(session.EffectiveEnabled && session.LastError is null && first.Draw() == 1,
                "a replacement canvas clears an earlier attachment failure");
            // A real filesystem failure must not flip the saved or active choice.
            File.Delete(path); Directory.CreateDirectory(path);
            try { session.SetEnabled(false); throw new Exception("Expected preferences failure"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                check(session.DesiredEnabled && session.EffectiveEnabled && first.Draw() == 1,
                    "failed preference save leaves desired and effective visibility unchanged");
            }
            Directory.Delete(path);
            session.SetEnabled(false);
            check(!session.DesiredEnabled && !session.EffectiveEnabled && !PreferencesStore.Load(path).LabelsEnabled,
                "retry after preference failure saves and applies OFF");
            session.Stop();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    sealed class Canvas
    {
        public event Func<bool>? Paint;
        public int Draw() => Paint?.GetInvocationList().Cast<Func<bool>>().Count(p => p()) ?? 0;
    }
    sealed class Host
    {
        public int GroupsStarted, GroupsStopped;
        public Renderer? Latest;
        public bool FailAttach;
        public ILabelRenderer Attach(Canvas canvas) => FailAttach ? throw new InvalidOperationException("Host attachment failed") : Latest = new Renderer(canvas);
        public void StartGroups() => GroupsStarted++;
        public void StopGroups() => GroupsStopped++;
    }
    sealed class Renderer : ILabelRenderer
    {
        readonly Canvas canvas;
        public Theme Theme { get; set; } = new();
        public bool Enabled { get; set; }
        public string? LastError { get; private set; }
        public Renderer(Canvas canvas) { this.canvas = canvas; canvas.Paint += Draw; }
        bool Draw() => Enabled;
        public void Fail() { LastError = "Host drawing failed"; Enabled = false; canvas.Paint -= Draw; }
        public void Dispose() { Enabled = false; canvas.Paint -= Draw; }
    }
}
