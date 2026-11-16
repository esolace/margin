#!/usr/bin/env python3
"""Prepare the private Rhino 7 Mac candidate. Never launches, installs or publishes."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile
from build_gh2 import atomic_copy
from build_release import ROOT, public_files

VERSION = '0.5.0-alpha.9-rhino7.1'
DEPENDENCIES = {
    'Microsoft.Bcl.AsyncInterfaces', 'System.Buffers', 'System.Memory',
    'System.Numerics.Vectors', 'System.Runtime.CompilerServices.Unsafe',
    'System.Text.Encodings.Web', 'System.Text.Json',
    'System.Threading.Tasks.Extensions', 'System.ValueTuple',
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', action='store_true', help='Also build a local Yak archive')
    args = parser.parse_args()
    dotnet = os.environ.get('MARGIN_DOTNET') or shutil.which('dotnet') or str(Path.home() / '.local/share/margin-dotnet/dotnet')
    sdk = Path(os.environ.get('MARGIN_RHINO7_RESOURCES', '/Applications/Rhino 7.app/Contents/Frameworks/RhCore.framework/Versions/A/Resources'))
    if not (sdk / 'RhinoCommon.dll').is_file():
        raise SystemExit('Rhino 7 Mac SDK missing. Set MARGIN_RHINO7_RESOURCES.')
    yak = os.environ.get('MARGIN_YAK') or shutil.which('yak') or '/Applications/Rhino 9.app/Contents/Resources/bin/yak'
    subprocess.run([sys.executable, str(ROOT / 'tools/sync_locales.py'), '--check'], check=True)
    project = ROOT / 'src/Margin.Grasshopper'
    subprocess.run([dotnet, 'build', str(project), '-c', 'Release', '-p:RhinoMajor=7', '-p:MarginPlatform=mac', f'-p:Rhino7Resources={sdk}', f'-p:PathMap={ROOT}=/_/margin'], check=True)
    stage = ROOT / 'dist' / f'Margin-{VERSION}-rh7-mac'
    stage.mkdir(parents=True, exist_ok=True)
    payload = {'Margin.gha', 'Margin.Core.dll'} | {name + '.dll' for name in DEPENDENCIES}
    allowed = payload | {'manifest.yml', 'README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.txt', 'icon.png', 'Margin-source.zip'}
    if any(p.name not in allowed and p.suffix != '.yak' for p in stage.iterdir()):
        raise SystemExit(f'Unexpected staging content in {stage}')
    for name in payload:
        atomic_copy(project / 'bin/Release/net48' / name, stage / name)
    for source, name in [(ROOT / 'LICENSE', 'LICENSE'), (ROOT / 'release/icon.png', 'icon.png'), (ROOT / 'docs/RHINO7.md', 'README.md')]:
        atomic_copy(source, stage / name)
    (stage / 'manifest.yml').write_text(f'''name: Margin
version: {VERSION}
authors:
  - Solace
description: >
  Private Rhino 7.38 Mac compatibility candidate for Grasshopper 1.
  Not host-tested or published. Native Studio, rendering and group workflows
  still require verification. Windows Rhino 7 is not supported by this candidate.
url: https://github.com/esolace/margin
icon: icon.png
''')
    assets = json.loads((project / 'obj/project.assets.json').read_text())
    notices = ['Runtime dependencies distributed under their included licenses.\n']
    for name in sorted(DEPENDENCIES):
        key = next(key for key in assets['libraries'] if key.split('/')[0] == name)
        relative = assets['libraries'][key]['path']
        folder = next(Path(root) / relative for root in assets['packageFolders'] if (Path(root) / relative).is_dir())
        license_file = next((p for p in folder.iterdir() if p.name.lower() in {'license.txt', 'license.md', 'license'}), None)
        if not license_file:
            raise SystemExit(f'Missing dependency license: {key}')
        notices.append(f'\n--- {key} ---\n{license_file.read_text()}')
        third_party = folder / 'THIRD-PARTY-NOTICES.TXT'
        if third_party.exists(): notices.append(third_party.read_text())
    (stage / 'THIRD-PARTY-NOTICES.txt').write_text('\n'.join(notices))
    temporary = stage / '.Margin-source.zip.tmp'
    with zipfile.ZipFile(temporary, 'w', zipfile.ZIP_DEFLATED) as archive:
        for source in public_files(): archive.write(source, source.relative_to(ROOT))
    os.replace(temporary, stage / 'Margin-source.zip')
    audit = [dotnet, 'run', '--project', str(ROOT / 'tools/PackageAudit'), '-c', 'Release', '--', '--rhino7', str(stage), str(sdk / 'ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll')]
    subprocess.run(audit, check=True)
    if args.package:
        for previous in stage.glob('*.yak'): previous.unlink()
        subprocess.run([yak, 'build', '--platform', 'mac'], cwd=stage, check=True)
        packages = list(stage.glob('*-rh7_38-mac.yak'))
        if len(packages) != 1: raise SystemExit('Expected one Rhino 7.38 Mac distribution')
        subprocess.run([*audit[:audit.index('--rhino7') + 1], str(packages[0]), audit[-1]], check=True)
        print('PRIVATE CANDIDATE:', packages[0])
    print('Native Rhino 7 testing pending. Nothing installed or published.')


if __name__ == '__main__': main()
