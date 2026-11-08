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
copied identity remains a known limitation.
