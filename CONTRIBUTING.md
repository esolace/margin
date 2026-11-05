# Contributing to Margin

Start with a small, reproducible issue. Include the operating system, Rhino
build, GH1 or GH2, Margin version, display scaling and the steps that failed.
Use a minimal definition with no confidential project data.

Build instructions are in `release/BUILDING.md`. Keep host adapters thin and
share the Studio, themes and presets. Preserve the approved visual language:
quiet surfaces, legible typography, restrained colour and consistent spacing.

Test changes in the affected Grasshopper host. A successful compilation does
not verify native rendering, window ownership or browser interaction. Windows
host testing is especially welcome.

Never rebuild over a loaded plugin in place. Use a new path or atomic staging,
then restart the test host when required. Keep working definitions separate
from test fixtures. Do not commit personal presets, recovery files, access
credentials, course files or host assemblies.

Contributions are accepted under the repository's MIT license.
