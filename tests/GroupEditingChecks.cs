using Margin.Core;
using Margin.Native;

static class GroupEditingChecks
{
    public static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "margin-edit-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "preferences.json");
            Directory.CreateDirectory(path);
            var host = new GroupHost();
            var edit = new GroupEditSession(host.Group, host.Apply, style =>
                PreferencesStore.Update(path, p => p with { GroupFill = style.Fill }));
            var changed = host.Group.Style with { Fill = "#E745AB" };
            var partial = edit.Save("Guide curves", changed, true);
            check(!partial.Complete && partial.Group.Name == "Guide curves", "a defaults failure reports the successful group edit");
            Directory.Delete(path);
            var retry = edit.Save("Guide curves", changed, true);
            check(retry.Complete && PreferencesStore.Load(path).GroupFill == "#E745AB" && host.UndoSteps == 1,
                "retry saves defaults without a stale revision or duplicate undo step");
            var stale = new GroupEditSession(host.Group, host.Apply, style =>
                PreferencesStore.Update(path, p => p with { GroupFill = style.Fill }));
            edit.Save("Newer edit", changed);
            try { stale.Save("Outdated edit", changed with { Fill = "#123456" }, true); throw new Exception("Stale edit accepted"); }
            catch (InvalidOperationException)
            {
                check(host.Group.Name == "Newer edit" && PreferencesStore.Load(path).GroupFill == "#E745AB",
                    "a stale edit changes neither the group nor future defaults");
            }
            File.WriteAllText(path, "damaged preferences");
            var failedAgain = edit.Save("Finish after failed defaults", changed, true);
            var finish = edit.Save("Finish after failed defaults", changed);
            check(!failedAgain.Complete && finish.Complete && host.UndoSteps == 3, "user can finish without saving future defaults after a partial save");
            try { edit.Save("Invalid appearance", changed with { Opacity = -1 }, true); throw new Exception("Invalid appearance accepted"); }
            catch (InvalidDataException)
            {
                check(host.Group.Name == "Finish after failed defaults" && host.UndoSteps == 3,
                    "invalid appearance leaves the native group unchanged");
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    // The external Grasshopper document boundary, without loading a Rhino session.
    sealed class GroupHost
    {
        public GroupEntry Group { get; private set; } = new("group-1", "Original", 2, true, new("#304737", 85), "revision-0");
        public int UndoSteps { get; private set; }
        public GroupEntry Apply(string revision, string name, GroupStyle style)
        {
            if (revision != Group.Revision) throw new InvalidOperationException("Stale group revision");
            if (name == Group.Name && style == Group.Style) return Group;
            UndoSteps++;
            return Group = Group with { Name = name, Style = style, Revision = "revision-" + UndoSteps };
        }
    }
}
