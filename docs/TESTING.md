# Testing status

This is a preview. Compilation, package checks and live-host testing are separate
levels of evidence; none is a claim that every host configuration is supported.

## Mac

Rhino 9.0.26300.9402, Apple Silicon, .NET 10. GH1 and GH2 were exercised in the
actual host. The GH2 test used an isolated canvas and checked that the existing
working document's object IDs remained unchanged.

Verified: native plugin loading, a single Display menu, all five themes,
installed-font enumeration and selection, label toggling, zoom cutoff,
above/below placement, category captions, nicknames, selected-only mode,
larger text, Studio close/reopen, and personal preset save/delete through the
native browser bridge. Mac font sizing and viewport processing were corrected
based on these checks.

A development rebuild once overwrote a file that was already loaded by Rhino.
A crash occurred during that session. Staging now replaces files atomically,
with a regression check that preserves a memory-mapped original or refuses the
replacement safely. Subsequent tests passed. This does not establish long-term
stability.

## Known Rhino / GH2 crash during computation

On the tested Mac Rhino build, restoring and computing a large migrated GH2
definition reproduced a native OpenNURBS font-list crash. The crash stacks
involve `ON_FontList::Internal_UpdateSortedLists`, PostScript font-name
comparison and font-characteristic hashing. The same definition reproduced
this fault after the entire Margin package was removed from plugin discovery.
This establishes that Margin is not required to trigger this failure; it does
not establish the exact underlying defect or rule out other host/plugin factors.

Fresh startup with an empty GH2 document loaded both packaged Margin adapters
once, with labels initially disabled, as intended. The working definition was
recovered with its object IDs intact and computation paused. This is a recovery
workaround, not a fix for the native crash. Save work before testing GH2, and
avoid repeatedly reopening a definition that crashes during computation.

## 0.5.0-alpha.2 local preview

GH1 checks ran in a separate empty `Margin-QA.3dm` session with disposable
definitions. Native Cmd/Ctrl+G, Studio's actual group-creation button, its preset
Apply button, and its future-defaults button were exercised. Heading double-click
opened the native editor; entering a name and clicking Save changed the group.
Native names/colours survived save/reopen and undo/redo. Defaults changed new
groups without recolouring existing ones. The original preset frame, accent edge
and internal heading were visually inspected with independent component labels.

The label switch retained ON and OFF across canvas replacement. Separate fresh
Rhino processes restored saved ON and saved OFF; ON was restored after testing.
These live restart checks cover GH1. Both adapters use the tested preference store,
but GH2 has not received another live restart test for this preview.

A movement regression reproduced the frozen frame: custom drawing bypassed the
native renderer's outline invalidation. The fix compares current member bounds
before using that outline, including during picking. The reusable host test
`tools/test_gh1_group_movement.py` checks movement in different directions,
expansion/contraction, nested groups, whole-group translation, membership changes
and save. Run it through RhinoCode only in an empty `Margin-QA.3dm` session.
It creates its own definition and restores the previous active definition.
Actual native mouse-handler drags were also checked separately for both a member
and the group heading. Local reports and native canvas exports are in `artifacts/`.

The new native-group defaults and heading editor are GH1 features. GH2 retains
its previous group editor. No personal workspace code was changed.

## 0.5.0-alpha.3 local preview

Both platform builds and assembly audits pass; the shared suite now has 38 checks,
including complete appearance persistence, legacy settings, automatic text contrast
and invalid-value rejection. Browser checks cover all eight new presets, bridge
payloads, editable title colour, automatic contrast, Classics and a 900px layout.

In a separate Mac GH1 QA document, all eight styles were drawn and inspected.
Text/accent colours, heading treatments and typography survive save/reopen;
custom appearance changes undo/redo together and propagate to new native groups
when saved as defaults. An actual Studio apply changed native text colour,
heading treatment and corner radius. The group movement regression still passes.
GH2 and Windows renderers compile; these new appearance controls have not had
interactive Windows or GH2 host verification. This remains a local preview.

## 0.5.0-alpha.4 group editing

The group-editing regression checks run through `GroupEditSession.Save`, with an
in-memory Grasshopper document adapter and real temporary preference files.
Run only these checks with `dotnet run --project tests -c Release -- --group-editing`.
They cover partial saves, retry, stale edits, finishing without defaults and
invalid appearance. The full shared suite has 43 checks.

Mac GH1 verification used a separately named verification assembly and a disposable
GH document that was never assigned to the user's canvas. A failed preferences
save was followed by a successful retry. One native undo restored the original
name and full appearance; redo restored the edit. Membership, stale-edit guards
and document identity guards passed. The original active canvas and its object
identities remained unchanged. The installed running plugin was not overwritten.

Studio browser checks also confirm that saving future defaults and changing
canvas selection preserve an unfinished name draft. Revert then follows the
latest selection. Windows and GH2 builds are checked, but this workflow has not
had interactive verification in those hosts.

## Windows

Both adapters compile against the pinned Rhino 9 SDK. Static assembly checks
reject Apple dependencies in Windows payloads. CI builds on a Windows runner
and runs the shared checks. No interactive Windows Rhino session has been
verified. Rendering, WebView2, fonts, installation and display scaling need
host testing.

## Remaining coverage

Multi-monitor behavior, GH2 full-screen Split View, exhaustive font coverage,
long-running use and varied definitions need broader testing. Snapshot exports
made by GH2 itself can omit third-party canvas overlays; the visible canvas is
the reference for Margin's label appearance.

For a host smoke test: use a disposable definition, open Studio, apply each
preset, switch fonts, toggle labels, zoom, move nodes, test visibility settings,
save/rename/update/delete a preset, close/reopen Studio, then save work and
restart Rhino. Confirm that the plugin loads once and presets persist.

## Current development: appearance/session boundaries and pasted groups

The appearance rules now live in `design/group-appearance-policy.json`. Studio's
small colour adapter and the C# resolver are compared over 384 combinations,
including opacity, both backgrounds, heading treatment and manual overrides.
`tools/sync_studio_policy.py --check` rejects stale embedded Studio assets.
`tools/test_studio_appearance.mjs` compares actual C# outputs with the browser
resolver. CI and the release build run these checks.

Label session checks cover desired versus effective visibility, saved ON/OFF,
canvas replacement, failed rendering/attachment, explicit recovery, failed
preferences saves and repeated cleanup. These portable tests do not stand in
for native host restart or Windows UI verification.

The pasted-group regression uses GH_Document.DuplicateDocument (native local
Copy/Paste serialization), ID mutation and document merge. The previous build
fails to pick the pasted heading: the document's attribute table still contains
the replaced native attributes. Margin now invalidates that table after
replacement and restoration. Title editing computes its current hit rectangle
rather than relying on a previous paint.

`tools/test_gh1_group_workflow.py` runs on disposable definitions without assigning
the user's active canvas or changing the system clipboard. A separately named
Mac verification assembly passes 17 checks: heading picking/selection/menu,
independent rename, undo/redo, member movement, the actual native Ungroup handler,
component preservation, undo, save/reopen and active-document identity checks.
The test invokes the native Ungroup handler after verifying picking; it does not
simulate physical mouse or keyboard input. Local reports are under
`artifacts/group-workflow/`. This does not claim the updated build is loaded in
the user's running session or that Windows received interactive testing.

Release evidence must distinguish completed checks from missing ones. A native
restart, interactive Windows tests and copying custom group typography are not
covered by the workflow above. Group fill/name/membership travel with native
copy/paste; GH1 custom appearance is document-keyed and its transfer to a new
copied identity remains a known limitation in alpha.5/alpha.6. The alpha.7 section below supersedes this limitation for the new implementation.


## 0.5.0-alpha.6 title controls and completion checks

Group titles support As typed (the backward-compatible default), UPPERCASE,
Title Case, Sentence case and lowercase. This transforms display text only;
native group names are unchanged. Unicode simple casing is checked across Studio and GH1/GH2: 60 title scenarios
and all 2,987 mapped Unicode scalars agree. The browser compatibility table
covers the 27 Greek mappings where JavaScript full casing differs from simple casing. As typed preserves intentional acronyms. Colour remains
editable in Auto mode: selecting a colour creates a custom override, and Auto
restores calculated contrast. Studio labels the control explicitly as Title colour.

The committed browser suite in `tools/browser/studio.mjs` reproduces the disabled
picker before the change and passes afterward. It covers casing preview/payload,
custom/automatic colour, defaults and draft preservation, selection/revert,
paused-label retry/off/save rollback, and minimum window width. CI runs it in a
separate job with pinned dependencies. Browser bridge replies are simulated;
this does not replace real native save tests. Screenshots at 1080x760 and 900x600
were inspected locally.

`tools/test_gh1_group_editor.py` exercises actual Eto controls without displaying
a window. Thirteen native checks pass in a separate verification assembly: custom
colour, restoration of Auto, casing, Save, undo/redo, reopening the editor and
saving/reopening the GH definition. The previous 17 copy/paste/rename/ungroup
checks also pass against the new implementation. Neither test changes the working
canvas or its objects. Test files are disposable and excluded from public source.

An earlier fresh-process attempt was blocked by Rhino's native other-instance
check. Later verification on 2026-11-08 superseded that result: the restarted Mac
GH1 process loaded one installed alpha.6 native assembly and passed 13 editor
and 17 group workflow checks, with saved preferences restored. Local evidence is
in `artifacts/restart-alpha6/VERIFIED.md`. Windows and new GH2 interactions remain
unverified; alpha.6 still has the copied-custom-appearance limitation.

## 0.5.0-alpha.7 outlines and portable appearance

The enhanced native workflow checks run on disposable definitions without replacing
the working canvas or changing the system clipboard. A separately named assembly
built from the new source passes 43 checks: complete custom appearance through
same-definition and cross-definition copies, independent edits, undo/redo, multiple
and nested groups, identical-name groups with distinct member relationships, unnamed groups, save/reopen and first-copy promotion of legacy
styles. Contour picking and crossing selection are compared against native GH1
attributes for Blob and Rectangles. Box, None and movement are also exercised.

Fourteen native editor checks pass, including automatic title contrast against the
canvas for external headings. Studio's browser suite now also checks blank-name
fallbacks and the distinction between a selected unnamed group and future defaults.
The original copy-appearance regression and rectangular Blob pick region both failed
before their respective fixes. The editor contrast regression also failed before its fix.

Native light/dark renders were inspected locally under `artifacts/native-outlines/`.
They show the retained Box frame, native freeform contours and borderless Group title.
A combined QA script stalled while switching global canvas backgrounds during export;
the disposable process was terminated, and workflow/editor checks and image export
were rerun separately in a fresh test session. No working definition was replaced.
This is recorded as a QA-session stall, not a proven Margin runtime crash or a clean
long-running stability result.

These checks exercise actual native APIs and editor controls. Real Cmd+G, Cmd+Z
and Cmd+Shift+Z were also exercised through the native UI on two fixture components
in an otherwise empty, disposable test session; screenshots confirmed creation,
undo and redo. The active definition was not replaced, and only the three fixture
objects were removed afterward. Full mouse interaction, Windows Rhino, new GH2
interactions and prolonged use remain unverified. Separate QA assembly results are not proof that the packaged alpha.7 assembly
is installed in a user's session. Fresh installed-candidate verification on 2026-11-09 loaded exactly one
Margin.Native 0.5.0.7 from the installed alpha.7 package in a new Mac GH1 process.
The installed assembly passed all 43 workflow and 14 editor checks; the active
canvas remained empty. Labels remained enabled with Petal, runtime/group errors
were null, and all three preference JSON files retained their original hashes.
Local evidence: `artifacts/alpha7-loaded.json`, `artifacts/alpha7-runtime.json`,
`artifacts/group-workflow/alpha7-installed.json` and `artifacts/group-editor/result.json`.

The all-platform release build passed 71 portable checks, 384 appearance comparisons,
60 casing scenarios, 2,987 Unicode mappings and exact-content audits of both Yak
packages. The Studio browser workflow passed using the installed Chrome executable.
Mac alpha.7 is installed locally; no package was pushed or published. Keep untested
combinations experimental.

Review follow-up: same-named copied groups now have distinct member identities in
the fixture, and recursive membership as well as appearance is checked after copy
and reopen. Selected None groups show a title underline without an enclosing frame.
