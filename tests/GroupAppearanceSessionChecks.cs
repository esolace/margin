using Margin.Core;
using Margin.Native;
using System.Text.Json;

static class GroupAppearanceSessionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "margin-group-session-" + Guid.NewGuid());
        var path = Path.Combine(directory, "preferences.json");
        try
        {
            var startup = new GroupAppearanceSession(path);
            var subscribed = false;
            try
            {
                startup.Start(() => { subscribed = true; throw new InvalidOperationException("host unavailable"); }, () => subscribed = false);
                throw new Exception("Failed host attachment was accepted");
            }
            catch (InvalidOperationException)
            {
                check(!startup.Started && !subscribed, "failed startup releases partial host subscriptions before a retry");
            }
            startup.Start(() => subscribed = true, () => subscribed = false);
            startup.Start(() => throw new Exception("Repeated startup subscribed again"), () => { });
            startup.Stop(); startup.Stop();
            check(!subscribed && !startup.Started, "repeated start and stop release the active host subscription once");
            var expected = new GroupStyle("#D9B7CA", 37, false)
            { Appearance = new() { ShowTitle = false, TitleCase = "Uppercase", Heading = "Band", Radius = 12 } };
            PreferencesStore.Update(path, p => p with
            {
                UseGroupColour = true, GroupFill = expected.Fill, GroupOpacity = expected.Opacity,
                StyleGroups = expected.Designed, GroupAppearance = expected.Appearance
            });
            var session = new GroupAppearanceSession(path);
            var document = new object();
            var native = new GroupStyle("#123456", 65);
            GroupInitializationResult? opened = null;
            session.Start(() =>
            {
                session.Attach(document);
                opened = session.Initialize(document, "ordinary", "", "", () => native, GroupInitialization.OpenedDocument);
            }, () => { });
            check(opened?.AppliedStyle == expected && GroupStyle.Parse(opened.Metadata) == expected,
                "already-open ordinary groups adopt complete saved defaults before their first copy");
            var nativeCommand = session.Initialize(document, "new", "", "", () => native, GroupInitialization.CreatedGroup);
            check(nativeCommand.AppliedStyle == expected,
                "a newly created native group uses the same complete saved defaults");
            var observedBeforeCreate = session.Initialize(document, "observed-before-create", "", "", () => native, GroupInitialization.Observed);
            var createdAfterObservation = session.Initialize(document, "observed-before-create", observedBeforeCreate.Metadata, "", () => native, GroupInitialization.CreatedGroup);
            check(createdAfterObservation.AppliedStyle == expected,
                "native creation still receives defaults when a document scan precedes its creation notification");
            var copiedObservation = session.Initialize(document, "copied-observation", observedBeforeCreate.Metadata, "", () => native, GroupInitialization.CreatedGroup);
            session.Initialize(document, "edited-before-create", "", "", () => native, GroupInitialization.Observed);
            var editedMetadata = JsonSerializer.Serialize(native with { Fill = "#AABBCC" });
            var editedBeforeCreate = session.Initialize(document, "edited-before-create", editedMetadata, "", () => native, GroupInitialization.CreatedGroup);
            check(copiedObservation.AppliedStyle is null && copiedObservation.Metadata == observedBeforeCreate.Metadata &&
                editedBeforeCreate.AppliedStyle is null && editedBeforeCreate.Metadata == editedMetadata,
                "creation notifications cannot overwrite a copied snapshot or styling edited after observation");
            var unsaved = new GroupAppearanceSession(Path.Combine(directory, "missing.json"));
            unsaved.Start(() => { }, () => { });
            var oldNative = unsaved.Initialize(document, "old", "", "", () => native, GroupInitialization.OpenedDocument);
            check(oldNative.AppliedStyle is null && GroupStyle.Parse(oldNative.Metadata) == native,
                "ordinary groups retain native appearance when no group defaults were saved");
            var inactivePath = Path.Combine(directory, "inactive.json");
            PreferencesStore.Update(inactivePath, p => p with { UseGroupColour = false, GroupFill = "#EEDDCC", GroupOpacity = 20 });
            var inactive = new GroupAppearanceSession(inactivePath);
            inactive.Start(() => { }, () => { });
            var inactiveChoice = inactive.Initialize(document, "inactive-choice", "", "", () => native, GroupInitialization.OpenedDocument);
            check(inactiveChoice.AppliedStyle is null && GroupStyle.Parse(inactiveChoice.Metadata) == native,
                "stored color fields without the explicit UseGroupColour choice do not restyle an opened group");
            var standardNative = native with { Designed = false, Appearance = new() { ShowTitle = false } };
            var newStandard = unsaved.Initialize(document, "new-standard", "", "", () => standardNative, GroupInitialization.AddedWithDefaultColour);
            check(newStandard.AppliedStyle is { Fill: "#123456", Opacity: 65, Designed: true, Appearance.ShowTitle: true },
                "GH1 new standard-color groups retain native color while receiving the prior default design policy");
            var custom = JsonSerializer.Serialize(native with { Appearance = new() { TitleSize = 24 } });
            foreach (var value in new[] { custom, "{broken", "{\"FutureSchema\":true}", " " })
            {
                var protectedPortable = session.Initialize(document, "portable-" + value, value, custom, () => native, GroupInitialization.OpenedDocument);
                var protectedLegacy = session.Initialize(document, "legacy-" + value, "", value, () => native, GroupInitialization.OpenedDocument);
                check(protectedPortable.Metadata == value && protectedPortable.AppliedStyle is null &&
                    protectedLegacy.Metadata == value && protectedLegacy.AppliedStyle is null,
                    "nonempty portable and legacy appearance is protected and promoted verbatim: " + value);
            }
            session.SaveDefaults(expected with { Fill = "#ABCDEF", Appearance = new() { ShowTitle = true } });
            var secondDocument = new object();
            session.Attach(secondDocument);
            var copy = session.Initialize(secondDocument, "copied", opened!.Metadata, "", () => native, GroupInitialization.OpenedDocument);
            check(copy.AppliedStyle is null && GroupStyle.Parse(copy.Metadata) == expected,
                "the first cross-definition copy retains its adopted portable appearance after defaults change");
            var repeated = session.Initialize(document, "ordinary", "", "", () => native, GroupInitialization.OpenedDocument);
            var restored = session.Initialize(document, "ordinary", "", "", () => native, GroupInitialization.AddedWithDefaultColour);
            check(!session.Attach(document) && repeated.AppliedStyle is null && restored.AppliedStyle is null,
                "repeated attachment and undo restoration never apply newer defaults to an initialized group");
            var late = session.Initialize(document, "late", "", "", () => native, GroupInitialization.Observed);
            check(late.AppliedStyle is null && GroupStyle.Parse(late.Metadata) == native,
                "a late native-colored arrival is pinned without applying opened-file defaults");
            var protectedScan = session.Initialize(document, "late", late.Metadata, "",
                () => throw new Exception("Native appearance was unnecessarily resolved"), GroupInitialization.Observed);
            check(protectedScan.Metadata == late.Metadata && protectedScan.AppliedStyle is null,
                "idle scans preserve portable metadata without resolving or serializing native appearance again");
            var delayed = new GroupAppearanceSession(path);
            delayed.Start(() => { }, () => { });
            check(delayed.Defaults.Fill == "#ABCDEF", "saved preferences are ready while the canvas is still absent");
            check(delayed.Attach(new object()), "a canvas arriving later can attach its first definition");
            session.Stop();
            session.Start(() => { }, () => { });
            var reopening = session.Attach(document);
            var restarted = session.Initialize(document, "ordinary", "", "", () => native, GroupInitialization.OpenedDocument);
            check(restarted.AppliedStyle is null,
                "stop and restart preserve document encounter history for live definitions");
            var whileStopped = session.Initialize(document, "added-while-stopped", "", "", () => native,
                reopening ? GroupInitialization.OpenedDocument : GroupInitialization.Observed);
            check(whileStopped.AppliedStyle?.Fill == "#ABCDEF",
                "groups added while stopped adopt saved defaults when their already-open definition reattaches");
            session.Stop();
            File.WriteAllText(path, "broken preferences");
            session.Start(() => { }, () => { });
            var corrupt = session.Initialize(new object(), "ordinary", "", "", () => native, GroupInitialization.OpenedDocument);
            check(session.LastError is not null && !session.HasSavedDefaults && corrupt.AppliedStyle is null &&
                File.ReadAllText(path) == "broken preferences",
                "invalid preferences cannot reuse stale defaults or overwrite the damaged settings file");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
