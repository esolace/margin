import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';
import '../design/group-appearance.js';

const root = fileURLToPath(new URL('../', import.meta.url));
const dotnet = process.env.MARGIN_DOTNET || 'dotnet';
const run = spawnSync(dotnet, ['run', '--project', resolve(root, 'tests'), '-c', 'Release', '--no-build', '--', '--appearance-vectors'], {encoding: 'utf8'});
assert.equal(run.status, 0, run.stderr);
const vectors = JSON.parse(run.stdout);
const policy = JSON.parse(readFileSync(resolve(root, 'design/group-appearance-policy.json'), 'utf8'));
for (const {style, dark, resolved} of vectors) {
  const actual = globalThis.MarginGroupAppearance.resolve(style, dark ? '#171C22' : '#D4D0C8', policy);
  assert.deepEqual({Text: actual.ink, Accent: actual.accent, Border: actual.border, Band: actual.band, Divider: actual.divider}, resolved);
}
console.log(`PASS ${vectors.length} Studio/native appearance comparisons`);
