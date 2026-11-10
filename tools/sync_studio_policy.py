"""Embed the shared appearance data in standalone Studio; --check detects drift."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
START = '<script type="application/json" id="groupAppearancePolicy">'
END = '</script>'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    policy = json.loads((ROOT / 'design/group-appearance-policy.json').read_text(encoding='utf-8'))
    path = ROOT / 'design/Studio.html'
    source = path.read_text(encoding='utf-8')
    start = source.index(START) + len(START)
    end = source.index(END, start)
    updated = source[:start] + '\n' + json.dumps(policy, indent=2) + '\n' + source[end:]
    marker = '<script id="groupAppearanceResolver">'
    start = updated.index(marker) + len(marker)
    end = updated.index(END, start)
    updated = updated[:start] + '\n' + (ROOT / 'design/group-appearance.js').read_text(encoding='utf-8') + updated[end:]
    if args.check:
        if updated != source:
            raise SystemExit('Studio appearance data is stale. Run python tools/sync_studio_policy.py.')
    else:
        path.write_text(updated, encoding='utf-8')


if __name__ == '__main__':
    main()
