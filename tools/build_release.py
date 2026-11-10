#!/usr/bin/env python3
"""Build audited GH1 + GH2 Yak payloads. Never installs or publishes."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile
from build_gh2 import atomic_copy

ROOT = Path(__file__).resolve().parent.parent
VERSION = '0.5.0-alpha.7'

def public_files():
    fixed = ['GLOSSARY.md', 'docs/NEXT-RELEASE.md', 'docs/adr/0001-portable-native-group-appearance.md', '.gitignore', 'LICENSE', 'README.md', 'CHANGELOG.md', 'CONTRIBUTING.md',
             'tools/browser/package.json', 'tools/browser/bun.lock', 'tools/browser/studio.mjs',
             'design/Studio.html', 'design/group-appearance-policy.json', 'design/group-appearance.js', 'tools/sync_studio_policy.py', 'tools/test_studio_appearance.mjs', 'docs/TESTING.md', 'docs/images/studio.png',
             'release/BUILDING.md', 'release/README.md', 'release/manifest.yml',
             'release/icon.png', 'release/icon.svg', 'tools/build_release.py',
             'tools/build_gh2.py', 'tools/test_gh2_staging.py', 'tools/test_gh2_group_editor.py',
             'tools/test_gh2_group_outline.py', 'tools/test_gh2_group_copy.py',
             'tools/test_gh1_group_movement.py', 'tools/test_gh1_group_workflow.py', 'tools/test_gh1_group_editor.py',
             '.github/workflows/ci.yml', '.github/ISSUE_TEMPLATE/bug_report.yml']
    result = [ROOT / name for name in fixed]
    for folder in ['src', 'tests', 'tools/PackageAudit']:
        result += [p for p in (ROOT / folder).rglob('*') if p.is_file()
                   and not {'bin', 'obj'}.intersection(p.relative_to(ROOT).parts)
                   and p.suffix in {'.cs', '.csproj'}]
    return sorted(result)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--platform', choices=['mac', 'win', 'all'], default='win' if sys.platform == 'win32' else 'mac')
    parser.add_argument('--package', action='store_true')
    args = parser.parse_args()
    dotnet = os.environ.get('MARGIN_DOTNET') or shutil.which('dotnet')
    if not dotnet:
        candidate = Path.home() / '.local/share/margin-dotnet/dotnet'
        if candidate.is_file(): dotnet = str(candidate)
    if not dotnet: raise SystemExit('Install .NET 10 or set MARGIN_DOTNET.')
    yak = os.environ.get('MARGIN_YAK') or shutil.which('yak')
    if not yak:
        candidate = Path('/Applications/Rhino 9.app/Contents/Resources/bin/yak') if sys.platform != 'win32' else Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Rhino 9/System/Yak.exe'
        if candidate.is_file(): yak = str(candidate)
    if args.package and not yak: raise SystemExit('Install Rhino 9 or set MARGIN_YAK.')
    subprocess.run([sys.executable, str(ROOT / 'tools/sync_studio_policy.py'), '--check'], check=True)
    subprocess.run([sys.executable, str(ROOT / 'tools/test_gh2_staging.py')], check=True)
    packages = []
    platforms = ['win', 'mac'] if args.platform == 'all' else [args.platform]
    for platform in platforms:
        for project in ['Margin.Grasshopper', 'Margin.Grasshopper2']:
            subprocess.run([dotnet, 'build', str(ROOT / 'src' / project), '-c', 'Release', f'-p:MarginPlatform={platform}', f'-p:PathMap={ROOT}=/_/margin', '--nologo'], check=True)
        tfm = 'net10.0' if platform == 'mac' else 'net10.0-windows'
        stage = ROOT / 'dist' / f'Margin-{VERSION}-{platform}'
        stage.mkdir(parents=True, exist_ok=True)
        allowed = {'LICENSE', 'Margin-source.zip', 'manifest.yml', 'icon.png', 'README.md', 'net10.0'}
        if any(p.name not in allowed and p.suffix != '.yak' for p in stage.iterdir()):
            raise SystemExit(f'Unexpected staging content in {stage}')
        payload = stage / 'net10.0'; payload.mkdir(exist_ok=True)
        payload_names = {'Margin.gha', 'Margin.GH2.rhp', 'Margin.GH2.rhp.grasshopper-only', 'Margin.Core.dll'}
        if any(p.name not in payload_names for p in payload.iterdir()):
            raise SystemExit(f'Unexpected binary payload in {payload}')
        for project, name in [('Margin.Grasshopper', 'Margin.gha'), ('Margin.Grasshopper2', 'Margin.GH2.rhp'), ('Margin.Grasshopper2', 'Margin.Core.dll')]:
            atomic_copy(ROOT / 'src' / project / 'bin/Release' / tfm / name, payload / name)
        (payload / 'Margin.GH2.rhp.grasshopper-only').touch()
        for source, name in [(ROOT / 'LICENSE', 'LICENSE'), *[(ROOT / 'release' / n, n) for n in ['manifest.yml', 'icon.png', 'README.md']]]:
            atomic_copy(source, stage / name)
        source_zip = stage / 'Margin-source.zip'
        temporary = stage / '.Margin-source.zip.tmp'
        with zipfile.ZipFile(temporary, 'w', zipfile.ZIP_DEFLATED) as archive:
            for source in public_files(): archive.write(source, source.relative_to(ROOT))
        os.replace(temporary, source_zip)
        if args.package:
            for package in stage.glob('*.yak'): package.unlink()
            subprocess.run([yak, 'build', '--platform', platform], cwd=stage, check=True)
            packages += [str(p) for p in stage.glob('*.yak')]
        else:
            subprocess.run([dotnet, 'run', '--project', str(ROOT / 'tools/PackageAudit'), '-c', 'Release', '--', '--gh2', str(payload / 'Margin.GH2.rhp')], check=True)
    subprocess.run([dotnet, 'run', '--project', str(ROOT / 'tests'), '-c', 'Release', '--no-launch-profile'], check=True)
    node = os.environ.get('MARGIN_NODE') or shutil.which('node') or shutil.which('bun')
    if not node and (Path.home() / '.bun/bin/bun').is_file(): node = str(Path.home() / '.bun/bin/bun')
    if not node: raise SystemExit('Install Node.js to verify Studio appearance parity.')
    subprocess.run([node, str(ROOT / 'tools/test_studio_appearance.mjs')], env={**os.environ, 'MARGIN_DOTNET': dotnet}, check=True)
    if packages:
        subprocess.run([dotnet, 'run', '--project', str(ROOT / 'tools/PackageAudit'), '-c', 'Release', '--', *packages], check=True)
        for package in packages: print('PACKAGE:', package)

if __name__ == '__main__': main()
