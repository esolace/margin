# Building Margin

Requires Python 3, the .NET 10 SDK, and Node.js 22+ (or Bun) for Studio checks. Mac builds also require the installed
Rhino 9 SDK for Rhino 9. Rhino 8 uses the pinned NuGet 8.21 SDK. Yak packages require Rhino's Yak executable.

```sh
python3 tools/build_release.py --platform win
python3 tools/build_release.py --platform mac
python3 tools/build_release.py --rhino 8 --platform all --package
python3 tools/build_release.py --rhino all --platform all --package
```

The default target is Rhino 9 (GH1 and GH2). `--rhino 8` builds GH1 for .NET 8;
`--rhino all` builds both host distributions. The command stages a clean public payload,
runs the shared checks, and optionally creates Yak archives. It never publishes.
Windows can be cross-compiled on Mac; running it requires Windows Rhino.

Set `MARGIN_DOTNET`, `MARGIN_NODE` or `MARGIN_YAK` for custom tool paths. For direct Mac builds,
override `RhinoResources` if Rhino is not in `/Applications/Rhino 9.app`.
For repeatable Rhino 9 minimum-version targeting, use the intended SDK: alpha.8
Mac uses the installed 9.0.26293.12304 SDK, while Windows uses NuGet
9.0.26300.9401. A newer installed Mac SDK produces its corresponding Yak tag.
For GH2-only development use `python3 tools/build_gh2.py --platform mac`.

`net10.0/` contains Margin.gha, Margin.GH2.rhp, its grasshopper-only loader marker,
and Margin.Core.dll for Rhino 9. Rhino 8 uses `net8.0/` with Margin.gha and
Margin.Core.dll only. Distributions are tagged `rh8_21` or `rh9_0`, plus
`mac` or `win`, so Package Manager selects a compatible build. The audit checks
the embedded Rhino and .NET dependencies against that tag.
Rhino/Eto/OS assemblies are never bundled. The package also
contains MIT licensing, documentation, the icon and an allowlisted source ZIP.
Staging refuses unexpected files and never truncates a loaded assembly in place.

For tests without Rhino:

```sh
dotnet run --project tests -c Release
dotnet run --project tests -c Release -p:RhinoMajor=8 # requires .NET 8 runtime
python3 tools/test_gh2_staging.py
python3 tools/sync_studio_policy.py --check
node tools/test_studio_appearance.mjs
```

Native host checks and remaining limits are documented in `docs/TESTING.md`.
Do not publish a previously used version number. Public Yak versions are immutable.

Studio interaction checks use the pinned Bun/Playwright workspace:

```sh
cd tools/browser
bun install --frozen-lockfile
bunx playwright install chromium
bun studio.mjs
```

CI runs this suite in its own job. Set `MARGIN_BROWSER_EXECUTABLE` to use an
existing Chromium executable; `MARGIN_SCREENSHOT_DIR` optionally saves UI evidence.
These checks simulate the native bridge, so native-host checks remain separate.
