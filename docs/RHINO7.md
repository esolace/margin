# Rhino 7 Mac candidate

Version: **0.5.0-alpha.9-rhino7.1**. This is a private preparation build,
not a public release or a claim of Rhino 7 support. The published Rhino 8/9
release remains alpha.8. Nothing is installed by the preparation tools.

## Scope

Prepare Margin for **Rhino 7.38 on Mac, Grasshopper 1** without opening Rhino,
changing installed packages or preferences, or interrupting a working session.
Keep Studio, saved defaults, optional group titles, native outlines, copying,
and persisted appearance consistent with the existing app.

Rhino 7 uses the older .NET Framework/Mono runtime. This candidate targets
net48, packages its JSON dependencies, uses Rhino 7's browser navigation events
for Studio commands, and supplies Unicode data so title casing matches modern
Margin. Its plugin files live at the package root for Rhino 7's loader.

Windows Rhino 7 is excluded: its legacy browser does not support the current
Studio. A modern browser host needs separate implementation and native testing.
The Windows CI job tests shared net48 logic only. Grasshopper 2 remains Rhino 9
only. Rhino 8/9 retain their existing build targets and public version.

## Reproduce background preparation

Requires .NET 10 SDK and local Rhino 7 Mac SDK files. The installed SDK used
here is **7.38.24338.17002**. No command below starts Rhino.

```sh
python3 tools/build_rhino7.py --package
dotnet build tests -c Release -p:RhinoMajor=7
dotnet run --project tests -c Release -p:MarginLegacyCheck=true
dotnet run --project tools/LegacyUnicode -- src/Margin.Core/LegacyUnicode.json --check
node tools/test_studio_appearance.mjs
node tools/test_legacy_studio.mjs
```

Set `MARGIN_DOTNET`, `MARGIN_YAK`, or `MARGIN_RHINO7_RESOURCES` to override local
tool locations. On Windows CI, run `tests/bin/Release/net48/Margin.Tests.exe`
and set `MARGIN_TEST_EXECUTABLE` to that path for the Studio appearance check.
This exercises the actual older framework without claiming Windows host support.

## Native verification still required

The local bundled Mono command currently exits with “bad CPU type in executable”;
its standalone executable is Intel-only. This does not establish whether the
Rhino app can run. Opening Rhino or changing the machine's translation/runtime
setup is deliberately deferred until it will not interrupt work.

Before installation or publication, use separate disposable definitions and
back up preferences. Verify plugin discovery, Studio opening and queued commands,
saved defaults through both Studio and native Group, editable and hidden titles,
all native outline choices, selection/movement, ungroup, undo/redo, copy/paste
within and between files, save/reopen, a new definition, Studio reopen and Rhino
restart. Inspect light and dark canvases and exercise real mouse gestures.
Check dependency loading and preservation of existing presets on that host.
Several NuGet dependencies request an older assembly version of the bundled
`System.Runtime.CompilerServices.Unsafe`. The package audit reports these
explicitly; compilation and dependency presence do not verify Mono binding.
Resolve any host binding failure before treating the candidate as installable.

Do not label this candidate “Mac tested” until those native checks pass.
Background evidence and remaining gaps are recorded in `docs/TESTING.md`.
