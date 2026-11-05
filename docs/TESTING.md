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
