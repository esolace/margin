#!/usr/bin/env python3
"""Embed the shared offline catalog and browser adapter into Studio."""
import argparse
import json
from pathlib import Path
import re
ROOT = Path(__file__).resolve().parent.parent
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--check', action='store_true')
args = p.parse_args()
page = ROOT / 'design/Studio.html'
text = page.read_text()
catalog = json.dumps(json.loads((ROOT / 'design/locales.json').read_text()), ensure_ascii=False, separators=(',', ':')).replace('<', '\\u003c')
script = (ROOT / 'design/localization.js').read_text()
block = '<!-- margin-localization:start -->\n<script id="marginLocales" type="application/json">' + catalog + '</script>\n<script>\n' + script + '</script>\n<!-- margin-localization:end -->'
if '<!-- margin-localization:start -->' in text:
    updated = re.sub(r'<!-- margin-localization:start -->.*?<!-- margin-localization:end -->', lambda _:block, text, flags=re.S)
else:
    updated = text.replace('<script id="groupAppearanceResolver">', block+'<script id="groupAppearanceResolver">')
if args.check:
    if updated != text: raise SystemExit('Studio translations are out of date. Run tools/sync_locales.py.')
else: page.write_text(updated)
