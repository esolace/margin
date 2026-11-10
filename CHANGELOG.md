# Changelog

## 0.5.0-alpha.7 — 2026-11-10

- Create GH2 groups with the saved appearance through Studio and the native Group command. Double-click a visible group title to edit its name and styling.
- Hide or restore a group heading in GH1 and GH2. The group picker in Studio can recover a hidden unnamed group. Older appearances keep their headings visible.
- Retain the native GH2 rectangular frame for selection, movement and context menus, including on groups with separated members.

- Apply saved future-group styling to Grasshopper's native Group command, which assigns a standard colour before adding the group. Preserve existing native appearances when copying.
- Match Studio's automatic title contrast and external heading preview to Blob, Rectangles and None.
- Keep component labels clear of group titles, including borderless groups.

- Respect GH1 Box, Blob, Rectangles and None outlines in both drawing and interaction. Freeform titles sit above the native contour; None retains a title without an enclosing frame.
- Keep Box heading bands, accents and corner styling; retain those settings when switching to an outline where they do not apply. Automatic freeform title contrast uses the actual canvas background.
- Display “Group” for unnamed groups on the canvas and in Studio. Preserve blank native names and distinguish a selected unnamed group from future defaults.
- Carry complete GH1 group appearance through copying within and between definitions, independent edits, undo/redo and save/reopen. Promote legacy saved styles before copying.
- Extend native and Studio regression coverage. See the November 10 testing record for GH2 candidate coverage. Interactive Windows testing remains outstanding.

## 0.5.0-alpha.6 — local development

- Keep title colour editable in Studio and the native group editor. Picking a colour switches to custom; Auto restores calculated contrast.
- Add As typed, UPPERCASE, Title Case, Sentence case and lowercase treatments without changing native group names.
- Save casing with group appearance and future defaults. Existing appearances remain As typed.
- Add browser workflow checks to CI and native editor colour/casing persistence checks.

## 0.5.0-alpha.5 — local development

- Fix pasted native groups retaining stale click/selection attributes, restoring heading editing and native ungroup behavior.
- Resolve group colours and heading treatments from shared policy, checked against Studio across light/dark backgrounds, opacity and overrides.
- Share label-session ownership across hosts; retain saved visibility through canvas changes and failures, with explicit retry in Studio.
- Add native copy/paste/rename/ungroup/undo/reopen regression coverage and a workflow-based release checklist.


## 0.5.0-alpha.4 — 2026-11-08

- Share the group edit session between Studio and the native title editor.
- Retain the acknowledged group revision if saving future defaults fails, so Save can be retried.
- Report partial success clearly and allow finishing without saving defaults.
- Avoid duplicate undo records caused by native opacity rounding.
- Keep stale-edit guards and preserve native group membership.

## 0.5.0-alpha.3 — local preview

- Add eight vivid group presets with coordinated title, fill, accent and border colours; retain the eight classic colours.
- Add editable title colour, automatic contrast, size/weight, heading treatments, frame colours, corners and border width.
- Persist the complete group appearance through undo/redo, save/reopen and GH1 future-group defaults; retain legacy styles.
- Match Studio specimens to both native renderers; keep component labels and workspace themes independent.

## 0.5.0-alpha.2 — local preview

- Remember label visibility across files, canvas recreation and application restarts in GH1 and GH2.
- Style native GH1 groups automatically, including Cmd/Ctrl+G; preserve the original group preset frames and separate heading hierarchy.
- Double-click a group heading to edit its native name and colour with undo support.
- Add working Studio group creation and persisted appearance defaults for future native groups.
- Retain explicit existing group colours when defaults change.
- Refresh GH1 group boundaries as members move, including nested groups; keep painting and hit testing on the same current frame.


## 0.5.0-alpha.1 (local preview)

- Add a Groups section with native group naming, colour and opacity editing.
- Add eight independent group colour presets and light/dark previews.
- Add opt-in group headings, accent edges and borders in GH1 and GH2.
- Preserve group membership, persist styles with definitions, and support Undo/Redo.
- Guard against editing a stale group or a different active definition.
- Keep all five component label presets and personal presets unchanged.

Mac native editing, undo/redo, serialization and Studio interactions verified.
Mac/Windows packages built and audited; Windows runtime testing is still unverified.

## 0.4.0-alpha.2

- Add a dedicated Grasshopper 2 renderer and Display menu integration.
- Share Label Studio, installed fonts and personal presets across GH1 and GH2.
- Correct Mac GH2 font sizing to match the Studio preview.
- Restrict GH2 label processing to the visible area and nearby collision bounds.
- Include both host adapters in separate Mac and Windows Yak distributions.
- Protect loaded assemblies with atomic staging and a regression check.
- Publish MIT source, build checks and a public issue tracker.

Mac GH1 and GH2 are tested locally. Windows interactive Rhino behavior remains
unverified. This release is a preview.

## 0.3.0-alpha.1

First public GH1 preview: Label Studio, five built-in styles, installed fonts,
personal preset management, theme import/export, Mac and Windows builds.
