# Margin: native outlines, neutral unnamed groups and dependable copying

## Problem Statement

Margin ignores Grasshopper's native group-outline choices: choosing Blob or another outline still produces a rounded rectangular Margin frame, with rectangular interaction regions. This undermines normal group organisation.

An unnamed group displays “Untitled group”, making an intentional choice to leave it unnamed look unfinished. Studio can also misidentify a selected unnamed group as the future-group defaults target.

Copying preserves native names, colours and membership but loses custom Margin group appearance, including title colour and title casing. Users cannot reliably reuse a designed group within a definition or carry it into another definition.

## Solution

Deliver an improved experimental alpha that respects every native outline, displays “Group” for unnamed groups, and preserves group appearance through copying, independent editing, saving and reopening.

Grasshopper's native outline remains authoritative. Margin adapts rendering and interaction geometry to None, Box, Blob and Rectangles. A copied group retains its design regardless of destination defaults and remains independently editable.

Describe validation by operating system and Grasshopper host. Builds without interactive validation remain explicitly experimental; availability must not imply tested feature parity.

## Mac Grasshopper 2 release requirement

The next public alpha requires a passing group workflow in the installed Mac GH2 build. In a disposable definition, save a distinctive group appearance in Studio, create a group with the normal GH2 Group command, and verify its fill and title styling on the canvas. Edit and clear its title, then check the “Group” fallback. Copy the group within the file and into a second definition. Check that the copy keeps its appearance and membership, that edits to each copy are independent, and that saving and reopening the destination preserves the result. Check the outline choices that GH2 itself offers on light and dark canvases.

Both the normal GH2 Group command and Studio’s **Group selection** button must create a native group with the saved appearance. Double-clicking the group title must open Margin’s title editor. Check that saving a title updates the canvas and that right-clicking still opens the native GH2 group menu.

Record the tested Rhino and GH2 builds and the observed results before publishing. Windows remains experimental pending interactive validation. The November 9 verification record covers the newer workflows on Mac GH1 only.

## Optional group title in GH1 and GH2

A group can have a blank native name. With **Show title** enabled, Margin displays “Group” for a blank name. Switching **Show title** off hides the whole heading and its reserved space while preserving the group's stored name, membership, outline and chosen styling. Studio must let the author find the group and restore the title, including a borderless group with a blank name. Existing appearances default to showing the title. Verify undo/redo, future defaults, independent copies and saving/reopening on each host.

## User Stories

1. As a Grasshopper user, I want to choose Blob, so that Margin respects the organic boundary I selected.
2. As a Grasshopper user, I want to choose Box, so that I can retain a conventional native group boundary.
3. As a Grasshopper user, I want to choose Rectangles, so that Margin respects the native arrangement around the grouped contents.
4. As a Grasshopper user, I want to choose None, so that Margin does not substitute its own enclosing frame.
5. As a Grasshopper user, I want to switch outlines on an existing styled group, so that I can change its presentation without rebuilding it.
6. As a Grasshopper user, I want selection and context-menu regions to agree with the native outline and visible title, so that interaction does not depend on an invisible replacement rectangle.
7. As a Grasshopper user, I want boundaries to update when members move, so that the group continues to represent its contents.
8. As a Grasshopper user, I want nested and separated contents to retain correct outline behaviour, so that complex definitions remain understandable.
9. As a Grasshopper user, I want legible group styling on light and dark canvases, so that workspace choice does not compromise my design.
10. As a Grasshopper user, I want unnamed groups to display “Group” on the canvas, so that leaving a group unnamed feels neutral.
11. As a Grasshopper user, I want to clear a name and return to “Group”, so that a custom title is optional.
12. As a Studio user, I want a selected unnamed group to display “Group”, so that I can distinguish it from future-group defaults.
13. As a Grasshopper user, I want supplied names preserved, so that the fallback does not rewrite meaningful text.
14. As a Grasshopper user, I want multiple unnamed groups to remain independently editable, so that their shared display name causes no editing ambiguity.
15. As a Grasshopper user, I want existing canvas and Studio renaming workflows preserved, so that outline improvements retain familiar editing.
16. As a Grasshopper user, I want to copy a styled group within a definition, so that I can reuse its complete appearance.
17. As a Grasshopper user, I want to copy into another definition, so that the design travels with the group.
18. As a Grasshopper user, I want copied groups to retain title colour, typography and title casing, so that their visual hierarchy survives duplication.
19. As a Grasshopper user, I want copied groups to retain fill, frame styling and native outline, so that duplication preserves the design.
20. As a Grasshopper user, I want destination defaults to leave copied appearance intact, so that another definition does not silently restyle my work.
21. As a Grasshopper user, I want to edit a copy without changing its original, so that reuse creates no hidden links.
22. As a Grasshopper user, I want to edit an original without changing its copies, so that each group can evolve independently.
23. As a Grasshopper user, I want copied names and membership preserved, so that appearance preservation does not damage native behaviour.
24. As a Grasshopper user, I want multiple and nested copies to retain their respective appearances, so that duplication does not confuse similar or unnamed groups.
25. As a Grasshopper user, I want saved and reopened copies to retain their appearance, so that copying succeeds beyond the current session.
26. As a Grasshopper user, I want existing saved appearances to remain readable, so that the update preserves earlier work.
27. As a Grasshopper user, I want undo and redo to restore edits and membership, so that normal recovery remains dependable.
28. As a Grasshopper user, I want ungrouping to preserve member objects, so that changing organisation does not destroy my definition.
29. As a Grasshopper user, I want future defaults to affect new groups without restyling existing or copied groups, so that defaults remain predictable.
30. As a Grasshopper user, I want ordinary Cmd/Ctrl+G creation preserved, so that Margin integrates with normal use.
31. As a Margin user, I want component labels and presets to remain independent of group changes, so that this update preserves my label setup.
32. As a release user, I want platform-specific validation notes, so that I can distinguish demonstrated behaviour from an unverified build.
33. As a user with active coursework, I want disposable test definitions, so that verification does not alter my working canvas.

## Implementation Decisions

- Keep native groups as the underlying objects. Preserve native names, membership, outlines and commands rather than introducing a separate group workflow.
- Update the GH1 rendering and interaction adapter together. Drawing alone is insufficient: current picking, selection and menu regions also assume a rectangle. Preserve layout refresh when members move.
- Use “Group” as the blank-name display fallback across relevant host renderers and Studio. Do not persist it as a supplied name or replace a literal name such as “Untitled group”. Preserve existing title casing without rewriting native names.
- In Studio, distinguish a selected unnamed group from no selected group. Continue identifying editing targets by identity, not display name.
- Preserve complete appearance through native copy/paste within and between definitions. Native colour alone does not satisfy this requirement.
- Preserve independently editable copies and existing saved appearances. The GH1 document-scoped appearance lookup does not transfer with a copied group's new identity; persistence and copy integration need a coordinated fix.
- No replacement storage mechanism or migration precedence has been selected. Verify native serialisation and copy behaviour before choosing one. Do not recover appearance by guessing from names, colours or positions.
- Keep host differences explicit. GH2 has a different appearance-storage mechanism; this spec does not require cross-host group conversion or a shared storage rewrite.
- Retain the distinction between applying appearance to a group and setting future defaults. Copying must not apply destination defaults over a copied design.
- Keep the release experimental and disclose unverified combinations. Check published versions before assigning the next version identifier.

## Testing Decisions

- The user confirmed complete workflow testing in disposable Grasshopper definitions, visual inspection on light and dark canvases, and Studio unnamed-group checks while preserving the working canvas.
- Prefer one primary acceptance boundary: the native Grasshopper group workflow. Extend the existing regression covering local copy/paste, picking, rename, movement, native ungrouping, undo and save/reopen. Reuse existing movement and native-editor checks instead of creating a parallel harness.
- Good tests assert observable results: chosen outline, visible appearance, correct interaction targets, independent edits and persisted results. Do not assert private storage keys, helper-call sequences or a particular drawing implementation.
- Cover all four outlines, outline switching, nested groups, separated members, movement, selection and context menus. Check points around irregular boundaries to expose obsolete rectangular interaction regions while respecting native semantics and legitimate title hit regions.
- Start copying tests with non-default typography, title colour, title casing, fill and frame treatment. Compare appearance before copying, immediately afterward, after independent edits, and after reopening the destination.
- Cover same-definition and cross-definition copying, different destination defaults, multiple and nested groups, identical names, unnamed groups and legacy appearances. Verify native outline, names and membership alongside Margin appearance.
- Exercise create, rename, clear-name, move, ungroup, undo/redo, save/reopen, new-definition, Studio-reopen and fresh-process persistence workflows where affected. Verify ordinary Cmd/Ctrl+G.
- Check new and existing blank names, preservation of supplied names, and selected unnamed group versus future defaults. Reuse Studio browser checks for display behaviour that native document tests cannot observe. Retain shared appearance-policy parity checks where affected.
- Inspect actual native output on light and dark canvases: title placement, clipping, contour fidelity, selection feedback and member overlap. Scripted native-command checks do not substitute for inspecting rendering or exercising real mouse/keyboard interaction on the candidate.
- Keep fixtures separate from coursework. Do not replace the working canvas, change its solver or preview settings, or overwrite a loaded assembly. Follow existing safe staging procedures.
- Record build, operating system, host, runtime, evidence and remaining gaps. Compilation and package audits are not interactive Windows/GH2 validation. Distinguish historical restart evidence from verification of the new candidate.
- Run relevant portable, Studio and package checks alongside native acceptance checks. Record unexecuted checks honestly.

## Out of Scope

- A stable release or unsupported claims of Windows/GH2 feature parity.
- WorkspaceThemes changes, workspace defaults or accepted brightness adjustments.
- A new group sidebar, general Studio redesign or component-label preset changes.
- Replacing native group commands or introducing linked copies.
- Rewriting supplied names or making custom titles mandatory.
- Unrelated new features, additional platform support or broad storage redesign without demonstrated need.
- Implementing the feature, publishing packages or pushing code during this specification-writing task. Publishing the specification to the configured issue tracker is part of this task.

## Further Notes

- This specification synthesises decisions made on 2026-11-09. It records intended behaviour, not completed implementation or verification.
- One proposal was unanswered before the specification request: preserve typography and colours, adapt borders and accents to native contours, omit bands/dividers that cannot fit cleanly, retain an editable title for None without a replacement frame, and preserve settings when switching outlines. This is a recommended direction, not an approved detailed visual design. Respecting all native outlines is agreed; inspect the resulting treatment before calling it finished.
- No storage ADR exists for copy preservation. Record one if implementation selects a consequential migration or ownership tradeoff. A promising SDK API is not yet a proven solution.
- The 2026-11-09 read-only publication audit found Yak alpha.4 public, a GitHub alpha.4 tag but newest GitHub Release alpha.2, and alpha.6 local development. Recheck before choosing a version and reconcile installation instructions, release notes and assets.
- GitHub Issues is available, but the inspected label set lacks the required `ready-for-agent` label and no project-specific triage setup was found. The invoked to-spec workflow directs the user to run `/setup-matt-pocock-skills` when tracker/triage configuration has not been provided. Publication and labelling remain pending that setup; this local spec has not been published.
