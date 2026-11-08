# Margin

**Labels, with character.**

A visual label studio for Grasshopper: installed fonts, considered colour,
backgrounds, borders and spacing, with personal presets you can keep and reuse.
No extra component is added to your definition.

![Margin Label Studio](https://raw.githubusercontent.com/esolace/margin/main/docs/images/studio.png)

## Install

In Rhino 9, open **PackageManager**, enable **Include prereleases**, search for
**Margin**, and install **0.4.0-alpha.2**. Save your work before restarting Rhino.
In GH1 or GH2, choose **Display → Margin → Open Studio…** and turn labels on.
The 0.5.0 local preview remembers your label switch across files and Rhino restarts.

Rhino chooses the matching Mac or Windows package. Both Grasshopper adapters
are included; GH2 must also be available in your Rhino installation.

## Groups (0.5.0 preview)

In GH1, create groups normally with **Cmd/Ctrl+G**, or use **Group selected
components** in Studio. Groups receive the designed frame automatically: a
larger internal heading, accent edge, and divider, distinct from component labels.
**Double-click the heading on the canvas** to edit its native name and colour.
Edits are undoable and saved in the Grasshopper file.

Studio offers eight vivid group presets: Cobalt, Orchid, Citrus, Lagoon, Ember,
Rose Pop, Blueprint and Ultraviolet. The previous eight colours remain in **Classics**.
Edit title colour (or use automatic contrast), size and weight, choose an editorial
rule, colour band or clean title, then adjust the fill, opacity, accent, border and
corners. Double-click editing also exposes title colour. Full group appearances
persist with the definition, undo/redo, and future-group defaults. Choose a preset and **Apply to group**
to update an existing group. **Use appearance for future groups** saves the colour,
opacity and styling choice on this computer, including for native Cmd/Ctrl+G groups.
Changing defaults does not recolour existing groups; pasted groups with explicit
colours keep them. Native background colours work with any canvas or workspace.
Component label presets, including Petal, remain independent.

GH2 retains its existing group editor. This update adds remembered label visibility
to both GH1 and GH2; new native-group defaults and on-canvas heading editing are
currently GH1 features. Names and membership stay native. GH1 retains exact native
fill colours without Margin; the designed heading requires Margin. GH1 style
metadata is document-scoped, so copied groups retain native colour but not custom
Margin metadata.

0.5.0-alpha.3 is a local preview. The public Package Manager version remains
0.4.0-alpha.2 until separately approved for publication. Windows packages compile;
interactive Windows Rhino testing remains outstanding.

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
See [testing status](https://github.com/esolace/margin/blob/main/docs/TESTING.md) for coverage and remaining limits.

Your presets stay in your user account:

- Mac: `~/Library/Application Support/Margin/`
- Windows: `%APPDATA%\Margin\`

Uninstalling Margin does not remove `presets.json`, `theme.json` or `preferences.json`.
Disable other component-label overlays to avoid duplicated labels.

## Build and contribute

[Build instructions](https://github.com/esolace/margin/blob/main/release/BUILDING.md) · [Contributing](https://github.com/esolace/margin/blob/main/CONTRIBUTING.md) ·
[Report a bug](https://github.com/esolace/margin/issues) · [Changelog](https://github.com/esolace/margin/blob/main/CHANGELOG.md)

MIT licensed. Source, Studio design and the Margin icon are included under the
[MIT license](LICENSE). Rhino, Eto and operating-system libraries are supplied
by the host and are not redistributed. The Yak package includes a source archive.
