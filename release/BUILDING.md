# Building Margin

Requires Python 3 and the .NET 10 SDK. Mac builds also require the installed
Rhino 9 SDK. Yak packages require Rhino's Yak executable.

```sh
python3 tools/build_release.py --platform win
python3 tools/build_release.py --platform mac
python3 tools/build_release.py --platform all --package
```

The command builds both GH1 and GH2 adapters, stages a clean public payload,
runs the shared checks, and optionally creates Yak archives. It never publishes.
Windows can be cross-compiled on Mac; running it requires Windows Rhino.

Set `MARGIN_DOTNET` or `MARGIN_YAK` for custom tool paths. For direct Mac builds,
override `RhinoResources` if Rhino is not in `/Applications/Rhino 9.app`.
For GH2-only development use `python3 tools/build_gh2.py --platform mac`.

`net10.0/` contains Margin.gha, Margin.GH2.rhp, its grasshopper-only loader marker,
and Margin.Core.dll. Rhino/Eto/OS assemblies are never bundled. The package also
contains MIT licensing, documentation, the icon and an allowlisted source ZIP.
Staging refuses unexpected files and never truncates a loaded assembly in place.

For tests without Rhino:

```sh
dotnet run --project tests -c Release
python3 tools/test_gh2_staging.py
```

Native host checks and remaining limits are documented in `docs/TESTING.md`.
Do not publish a previously used version number. Public Yak versions are immutable.
