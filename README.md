# Margin

**Labels, with character.**

A visual label studio for Grasshopper: installed fonts, considered colour,
backgrounds, borders and spacing, with personal presets you can keep and reuse.
No extra component is added to your definition.

![Margin Label Studio](docs/images/studio.png)

## Install

In Rhino 9, open **PackageManager**, enable **Include prereleases**, search for
**Margin**, and install **0.4.0-alpha.2**. Save your work before restarting Rhino.
In GH1 or GH2, choose **Display → Margin → Open Label Studio…** and turn labels on.
Labels deliberately start off each session so they do not overlap other label plugins.

Rhino chooses the matching Mac or Windows package. Both Grasshopper adapters
are included; GH2 must also be available in your Rhino installation.

## Make it yours

Start with Paper, Ink, Outline, Signal or Petal. Search installed font families,
change the colour and surface treatment, or adjust spacing and visibility.
**+ Save preset** adds a style to **Your presets**, where it can be renamed,
updated or deleted. **Set as default** separately remembers the startup style.
Themes can also be exported and imported as JSON.

The Studio runs offline. Margin has no account requirement or telemetry.
No fonts are bundled. If an imported font is unavailable, an installed fallback
is used. GH1 and GH2 share the local preset library.

## Compatibility

| Platform | Grasshopper 1 | Grasshopper 2 |
| --- | --- | --- |
| macOS, Apple Silicon | Live-tested | Live-tested preview |
| Windows | Builds; Rhino runtime unverified | Builds; Rhino runtime unverified |

Use Rhino 9 with its .NET 10 runtime. The Mac build is tested against
9.0.26300.9402; Windows uses the 9.0.26300.9401 SDK. Older Rhino 9 builds may lack
required GH2 APIs. Rhino 8, .NET Framework mode and Linux are not supported.
This is an **experimental preview**, not a stable release. Windows testers are
welcome; an automated Windows build is not an interactive Rhino test.
See [testing status](docs/TESTING.md) for coverage and remaining limits.

Your presets stay in your user account:

- Mac: `~/Library/Application Support/Margin/`
- Windows: `%APPDATA%\Margin\`

Uninstalling Margin does not remove `presets.json` or `theme.json`.
Disable other component-label overlays to avoid duplicated labels.

## Build and contribute

[Build instructions](release/BUILDING.md) · [Contributing](CONTRIBUTING.md) ·
[Report a bug](https://github.com/esolace/margin/issues) · [Changelog](CHANGELOG.md)

MIT licensed. Source, Studio design and the Margin icon are included under the
[MIT license](LICENSE). Rhino, Eto and operating-system libraries are supplied
by the host and are not redistributed. The Yak package includes a source archive.
