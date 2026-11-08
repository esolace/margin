#!/usr/bin/env python3
"""Build isolated GH2 development payloads. Does not install or publish."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import sys

ROOT = Path(__file__).resolve().parent.parent
VERSION = '0.5.0-alpha.6'

def atomic_copy(source, destination):
    # A running host can memory-map this file. Truncating it in place can corrupt
    # the loaded assembly; replace the directory entry and leave its old inode intact.
    handle, temporary = tempfile.mkstemp(prefix='.margin-', dir=destination.parent)
    os.close(handle)
    try:
        shutil.copy2(source, temporary)
        os.replace(temporary, destination)
    finally:
        Path(temporary).unlink(missing_ok=True)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--platform', choices=['mac', 'win', 'all'], default='all')
    args = parser.parse_args()
    dotnet = os.environ.get('MARGIN_DOTNET') or shutil.which('dotnet')
    if not dotnet:
        candidate = Path.home() / '.local/share/margin-dotnet/dotnet'
        if candidate.is_file():
            dotnet = str(candidate)
    if not dotnet:
        raise SystemExit('Install the .NET 10 SDK or set MARGIN_DOTNET.')

    subprocess.run([sys.executable, str(ROOT / 'tools/test_gh2_staging.py')], check=True)

    payloads = []
    for platform in (['win', 'mac'] if args.platform == 'all' else [args.platform]):
        subprocess.run([dotnet, 'build', str(ROOT / 'src/Margin.Grasshopper2'),
                        '-c', 'Release', f'-p:MarginPlatform={platform}', f'-p:PathMap={ROOT}=/_/margin', '--nologo'], check=True)
        tfm = 'net10.0' if platform == 'mac' else 'net10.0-windows'
        source = ROOT / 'src/Margin.Grasshopper2/bin/Release' / tfm
        stage = ROOT / 'dist' / f'Margin-GH2-{VERSION}-{platform}'
        stage.mkdir(parents=True, exist_ok=True)
        allowed = {'Margin.GH2.rhp', 'Margin.GH2.rhp.grasshopper-only',
                   'Margin.Core.dll', 'LICENSE', 'README.md'}
        if any(p.name not in allowed for p in stage.iterdir()):
            raise SystemExit(f'Unexpected files in {stage}; inspect before staging.')
        for name in ['Margin.GH2.rhp', 'Margin.Core.dll']:
            atomic_copy(source / name, stage / name)
        (stage / 'Margin.GH2.rhp.grasshopper-only').touch()
        shutil.copy2(ROOT / 'LICENSE', stage / 'LICENSE')
        shutil.copy2(ROOT / 'docs/TESTING.md', stage / 'README.md')
        payloads.append(str(stage / 'Margin.GH2.rhp'))

    subprocess.run([dotnet, 'run', '--project', str(ROOT / 'tools/PackageAudit'),
                    '-c', 'Release', '--', '--gh2', *payloads], check=True)
    subprocess.run([dotnet, 'run', '--project', str(ROOT / 'tests'),
                    '-c', 'Release', '--no-launch-profile'], check=True)

if __name__ == '__main__':
    main()
