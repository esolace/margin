# Testing status

This is a preview. Compilation, package checks and live-host testing are separate
levels of evidence; none is a claim that every host configuration is supported.

## Rhino 8 compatibility candidate, 2026-11-16

Alpha.8's installed Rhino 8 Mac package was tested in Rhino 8.37.26307.15002
on Apple Silicon. Its GH1 module ID is `5f4ceb07-576b-41fb-96d9-035a699370bc`.
The package targets .NET 8 and the Rhino 8.21.25188.17001 SDK. This installed
Rhino 8 host uses .NET 10.0.8; older Rhino 8 hosts were not tested interactively.
The portable suite also passed all 77 checks on .NET 8.0.34.

- The installed GH1 adapter passed 63 group workflow, 21 editor and 19 movement
  checks. These cover native outlines, independent same/cross-definition copies,
  undo/redo, title visibility and saving/reopening.
- Studio's real WebKit bridge saved a purple visible-title appearance and a teal
  hidden-title appearance. The host's actual Group menu command and Studio button
  both used those defaults, preserving member identity and blank native names.
  Changing defaults preserved existing groups. Studio recovered an unnamed hidden
  heading without changing defaults. All 11 before-restart checks passed.
- A new Rhino process loaded the same installed module. Labels remained enabled,
  Studio displayed the saved hidden-title default, and both creation routes used
  it. All seven after-restart checks passed.
- Package audits reject Rhino 9 code disguised as a Rhino 8 distribution, a
  .NET 10 core library inside the .NET 8 package, and an incorrect minimum Rhino
  minor version. Windows builds remain experimental pending native testing.

The repeatable Studio/restart test is `tools/test_gh1_group_defaults.py`; it
requires backed-up preferences and disposable Rhino processes. Detailed local
reports are under `artifacts/rhino8/`. The visible group and component labels
were inspected in Rhino 8. The mouse tool could read the window and send keyboard
input, but coordinate clicks failed with `noWindowsAvailable`; physical title
double-click/drag acceptance remains unverified on Rhino 8. The user was working
in class, so testing stopped without interrupting that work. The native editor
and picking checks above are separate evidence, not a substitute for those gestures.

The original theme, presets and preferences were restored byte for byte, and
the disposable Rhino 8 process was closed. The user's Rhino 9 process was not
restarted. Alpha.8 Rhino 9 builds passed host/runtime audits and CI; their native
coverage remains the historical alpha.7 record below, not a fresh alpha.8 test.
The local Rhino 9 installation remains on alpha.7. CI passed Windows builds for
both hosts, .NET 8 and .NET 10 portable checks, and the Studio browser workflows.

## Installed Mac group candidate, 2026-11-10

The local alpha.7 candidate was exercised on Apple Silicon, macOS 27.0.1
(26A434), Rhino 9.0.26300.9402 and Grasshopper 2 9.0.26300.9402. The Rhino
package loaded Margin.GH2 0.5.0.7. The final GH2 assembly module ID was
`488d5fbe-d1ea-4976-b40d-5431804de3af`. Public Package Manager alpha.4
predates these changes.

- **Studio and creation.** In a disposable definition, Studio saved a purple
  fill at 72% with a 22 px uppercase gold title. Both the native Group command
  implementation and Studio’s WebKit button produced native groups with that
  precise style, empty name and selected member. Saving a teal 48% default with
  the title hidden left the selected purple group intact. Both routes produced
  the new style, and the original retained its visible title and member
  relationships when pasted in the same file and in a second definition.
  Physical Cmd+G also grouped two selected components with the hidden teal style.
  Empty Studio selection displayed “Select components on the canvas first.”
  Studio’s document picker found the hidden unnamed group, restored its heading
  and left its name, members and computer-wide defaults intact. One undo hid
  the heading again. A fresh process loaded the same module, displayed the
  saved hidden default in Studio and created matching groups through both
  routes. Closing and reopening the destination preserved the original purple
  design under the teal default. Canvas captures of both routes were inspected.
- **Title editing and mouse.** On the final installed build, double-clicking
  a heading opened Margin’s editor. Saving a rename and cancelling a second
  rename retained the first name. Clearing the name and switching off Show
  title hid the entire heading. Cmd+Z restored the name and heading together.
  Redo restored the blank, hidden state. Right-click opened GH2’s own group
  menu with its name, colour, member and state controls.
- **GH2 geometry.** The tested host offers a rectangular group. Native light
  and dark renders show readable automatic titles on their respective
  backgrounds, including nested frames. A member drag of 70 by 20
  canvas units expanded the frame. Dragging the hidden-title group’s edge by
  30 by 30 moved both members without resizing the frame. The native picking
  and rendering checks cover interior window selection, nested bounds and the
  space reclaimed by a hidden heading. All 20 native picking, rendering and editor-colour checks passed on the reviewed build.
  The selected edge reached 7.9:1 contrast on the dark-fill fixture.
  Both mouse drags were repeated on the final installed build.
- **Native appearance.** Show title is available in both hosts with Margin styling off. Hiding a named group removes its heading and restores the native frame used for a blank name. Turning the title back on restores the host’s original presentation. A blank native name follows the host’s own display rules in this mode. A blank Margin-styled title displays “Group” when shown. The GH2 editor’s automatic colour swatch matches the title drawn over a nested group on both canvas skins.
- **Copy and reopen.** The host’s app-local Copy/Paste commands transferred
  independently styled, identically named siblings and a hidden unnamed parent.
  Checks resolved copied member IDs, edited source and copies independently,
  exercised undo/redo, closed and reopened the saved destination, and opened
  an existing unstyled native group with defaults active. No system clipboard
  was used.
  This GH2 Edit menu has no separate Ungroup command. Deleting just the
  group through native selection preserved both members and their positions.
  Undo restored its original membership and appearance. Redo removed only
  the group.
- **Mac GH1.** The installed Margin.gha (module ID
  `28697b4d-5372-477e-9e55-ec50eb9ec02c`) passed 63 group workflow, 21 Eto
  editor and 19 movement checks. Hiding a heading preserved the native name,
  removed the former pick area, recovered the space and survived undo/redo.
- **Portable and package checks.** The shared suite passed 77 checks. Studio
  and C# agreed on 1,536 appearance combinations, 60 casing scenarios and
  2,987 Unicode mappings. The Playwright Studio workflow passed in Chrome.
  Both Mac and Windows Yak archives passed the binary and exact-content
  audits. Interactive Windows testing remains outstanding.

The installed defaults test is in `tools/test_gh2_group_defaults.py`. Its
before/after phases require two disposable Rhino processes with the same
installed candidate and a backup of the original Margin preferences. Native
copy, editor and outline checks use the adjacent GH2 host scripts. Detailed
local reports and images are under `artifacts/gh2-groups/`. The paired
restart results are `defaults-before-restart.json` and
`defaults-after-restart.json`. The before phase also exercises reopening the saved .ghz file. Native light and dark renders are in `outline/`. After verification the original GH2 session, autosave, theme, presets and
preferences were restored byte for byte. These checks cover the plugin binaries packaged for alpha.7.

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


## Alpha.7 review fixes and future-default regression — 2026-11-09

Grasshopper's native Group command assigns `GH_Skin.group_back` before adding
its group. The previous empty-colour check therefore skipped saved typography.
The command reproduced the user's green/20px/uppercase defaults being replaced
by automatic/15px/as-typed. The corrected installed build applies the saved
appearance; the visible canvas and native state both confirmed it after restart.
Existing native groups are pinned before their first copy so destination defaults
do not replace their appearance. Metadata-free imports from sources never opened
with Margin remain indistinguishable from new standard-colour groups; see the ADR.

The outline fixture now explicitly enables Designed styling. The native workflow
passes 58 checks on the installed Mac GH1 adapter, including distinctive temporary
in-memory defaults, both colour preference modes, explicit-colour preservation,
existing-native copy preservation and every outline. Test state is restored in
`finally`; all three user preference JSON hashes remained unchanged. Fourteen
native editor checks and 19 movement checks also pass on the final adapter.

Physical mouse testing on disposable definitions verified Blob, Rectangles, Box
and None title/contour selection, native context menus and double-click editing.
Blob title dragging moved both members; Cmd+Z restored them. Box member dragging
expanded its frame. Blob/Rectangles gaps and None empty space did not capture
group menus; crossing a wholly empty None region selected nothing. Clearing a
name in the editor displayed Group (with the chosen uppercase casing). None was
set through the native API because this GH1 menu does not expose None.

Mouse testing also exposed a component label covering a None heading. Label
placement now reserves designed group title regions using its existing collision
policy. A fresh installed process visibly moved that label below its component.
Final native light/dark exports of all four outlines were inspected with labels.
Studio now uses canvas contrast for external headings and suppresses inapplicable
bands/dividers in its preview. Browser checks cover Auto/manual colours across
light/dark previews and each external outline.

The release build passes 74 portable checks, 768 native/Studio appearance
comparisons, 60 title-casing scenarios, 2,987 Unicode mappings and both platform
package audits. The Studio browser workflow passes. Local detailed evidence is
in `artifacts/alpha7-review-fixes/`, including mouse actions, final native reports,
preference hashes, the native Group-command reproduction and light/dark renders.
This completes the targeted Mac GH1 mouse pass, not an exhaustive interaction or
stability certification. Windows, new GH2 interactions, multi-monitor behavior
and prolonged use remain unverified. The package remains a local experimental
alpha; nothing was published.
## Background native multilingual check — 2026-11-16

A disposable Rhino **8.37.26307.15002 Mac / GH1** process exercised the
multilingual candidate with a private Margin settings directory. It passed
**197 assertions**: all 13 manual language choices persisted; real unshown Eto
group editors displayed translated controls; multilingual literal names,
title visibility and casing survived native save, undo/redo, all four outline
copies, and GH definition save/reopen. Automatic selection was checked against
this host's English locale. This is not 13 different Rhino interface locales,
a system-clipboard test, a mouse test or native visual/layout verification.

Studio loaded inside Rhino's actual WebView, connected and read 197 installed
fonts without showing its window. The Studio language acknowledgement test
stopped on a test-helper Boolean comparison; the helper was corrected, but
its rerun could not connect because the test process had crashed during cleanup.
The crash report identified **ObjectDisposedException in
Inspector.SyncGroupSelection**: a queued timer accessed `Visible` on a disposed
Studio. Cleanup previously depended only on `Closed`, which direct disposal
of an unshown window did not raise.

The fix stops and detaches the timer on both close and disposal, unsubscribes
browser callbacks, rejects callbacks after disposal, and safely finishes browser
operations already in flight. A standalone regression using the **actual
compiled Inspector and Eto disposed state** reproduced the exact exception
before the fix and passes afterwards for the Rhino 8 GH1 and Rhino 9 GH2
assemblies. It creates no native window and does not establish that native
timer teardown or post-fix close/reopen was tested in Rhino. The Windows build
job also runs this regression. Local Mac Rhino 7/8/9 GH1 and Rhino 9 GH2 builds
pass; 124 shared checks and the existing appearance/casing parity checks pass.

Isolation: no tool targeted the user's working Rhino 9 process, no working
definition was opened or edited, and checksums of the personal Margin files
were unchanged. The test launch briefly activated despite macOS background
flags; it was hidden, then set to prohibit activation and its visible test
window was ordered out. No further Rhino instance was launched after that.
The final editor-suite visibility assertion used macOS `Hidden`, which becomes
false under the prohibited activation policy; separate inspection confirmed
`Active=false`. Its 197 functional assertions had already passed. The test now
checks activation policy instead of interpreting `Hidden` as foreground state.

Remaining: post-fix native Studio language acknowledgement, close/reopen,
direct disposal during pending browser work, actual Rhino restart persistence,
native menu discovery, screenshots/glyph and layout review, Rhino 9 GH1/GH2
interaction, and Rhino 7 host validation. Windows host validation and independent
native-speaker review remain pending. No candidate was installed or published.
Raw reports, the crash evidence, personal-file checksum comparison and red/green
regression logs are under `artifacts/multilingual-native/`.

Repeatable checks: `tools/test_native_localization.py` has `verify(output)` and
`verify_studio(output)` for a disposable, activation-prohibited Rhino UI thread.
Set `MARGIN_SETTINGS_DIRECTORY` to an absolute private directory **before**
loading either Margin adapter. This process-local override covers themes,
preferences and presets; the normal application-data location is unchanged.
The Studio helper requires a fresh private preset library. Its runtime
stop/start check is not a Rhino process restart. Never run these helpers in a
working Rhino process.

The standalone crash regression takes the built adapter followed by dependency
directories, for example:

```sh
dotnet run --project tools/NativeLifecycleChecks -c Release -- \
  src/Margin.Grasshopper/bin/Release/net8.0/Margin.gha \
  '/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Versions/A/Resources' \
  '/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Versions/A/Resources/ManagedPlugIns/GrasshopperPlugin.rhp'
```

## Multilingual candidate — 2026-11-16

Implemented all 13 Rhino languages using 218 shared offline messages. Language
choice is an application preference, separate from themes and group metadata.
The language-only test slice verifies locale resolution, old preferences,
invalid-choice protection, complete catalogs, placeholder integrity and literal
user content. Browser workflows pass all 13 languages at 900 × 600, including
host language selection, manual acknowledgement, saved/reopened selection,
failed saving, typed preset names, group drafts, hidden/blank names and visible
actions. All languages exercise save/rename/update/delete preset dialogs,
font-picker keyboard controls, Tab navigation and translated current feedback,
including nested native validation errors. Screenshots are in
`artifacts/multilingual/screenshots/`.

Local verification passes 121 shared checks, Mac Rhino 7/8/9 GH1 builds and
Rhino 9 GH2 builds. Studio/native parity passes 1,536 appearance comparisons,
60 title-case scenarios and 2,987 Unicode mappings. The Rhino 7 private Yak
package builds and passes its payload/source audit; its Mono dependency binding
checks remain pending in the native host.

Spec and standards reviews are clear after fixing language preference recovery,
feedback translation, .NET Framework exception metadata and dependency-free
package discovery. Shared locale fixtures now check both native and browser
resolution, including explicit Chinese script precedence.

No live Rhino process, installed plugin, working definition or personal
preference file was changed. No native workflow claim is made for this build.
Rhino 7's Intel launch limitation and the Windows Rhino 7 exclusion remain.
Independent native-speaker review is pending; completeness tests do not certify
translation quality. Local build/verification logs are under
`artifacts/multilingual/`.

Final code commit `68e424d` passed all four jobs in
[GitHub run 43926248469](https://github.com/esolace/margin/actions/runs/43926248469):
legacy-core (net48), portable-checks (net8/net10 and compatibility/parity checks),
studio-workflows (existing workflows plus all 13 languages), and windows-build
(Rhino 8/9 GH1 and Rhino 9 GH2). The legacy error-descriptor failure from the
previous run is fixed and covered by regression checks. Windows build success
does not constitute interactive Windows host validation.

## Rhino 7 background preparation — 2026-11-16

Private candidate **0.5.0-alpha.9-rhino7.1**, Rhino 7.38 Mac/GH1, prepared
on `compatibility/rhino7`. See `docs/RHINO7.md` for scope and the remaining
native verification checklist. This section does not change the public alpha.8
support claims below.

Background verification:

- Mac adapter and test executable compile against net48; zero warnings/errors.
- Rhino 8 and Rhino 9 Mac adapters still compile with zero warnings/errors;
  all 77 portable checks pass on both .NET 8 and .NET 10.
- Shared compatibility path passes 77 portable checks on .NET 10, including
  preference/preset persistence, saved defaults and optional group titles.
- Studio/native parity passes 1,536 appearance comparisons, 60 title-case
  scenarios and 2,987 Unicode mappings; generated category data is checked.
- Legacy Studio transport test verifies ordered commands, Unicode payloads,
  acknowledgement and reuse after draining the queue. This is a JavaScript
  simulation, not a native WebView test.
- Standards and spec reviews found no actionable issues. GitHub run
  [43915589372](https://github.com/esolace/margin/actions/runs/43915589372) passed
  all four jobs on commit `26b1933`: portable checks, Studio browser workflows,
  Windows Rhino 8/9 builds and the shared net48 runtime suite. This verifies the
  older framework on Windows, not Mono or Rhino 7 host behavior.
- Candidate audit checks root-layout payload, dependency presence, Framework
  target, embedded Studio/Unicode data, SDK private group-method signatures,
  dependency licenses and source-archive contents. Two dependencies request
  Unsafe 4.0.4.1 while the package includes 6.0.0.0; actual Mono binding remains
  a native verification requirement.

During initial preparation no Rhino session was launched or manipulated, no package was installed/published,
and no live preferences or working definitions were changed. The bundled Mono
CLI could not run here (“bad CPU type in executable”). Rendering, Studio commands,
mouse gestures, persistence/restart and loader behavior inside Rhino 7 remain
unverified. Windows Rhino 7 is excluded; shared net48 CI is not a host-support
claim. Local logs and the private package are under `artifacts/rhino7/` and
`dist/Margin-0.5.0-alpha.9-rhino7.1-rh7-mac/` respectively.

Follow-up background native check, 2026-11-16: directly launching the Rhino 7
executable failed before process creation with macOS errno 86, “Bad CPU type in
executable.” The app and bundled Mono are x86_64; an independent Intel execution
probe failed too. No host tests ran. No package was installed and no working
Rhino session was touched. Evidence: `artifacts/rhino7/native-launch-check.json`.
