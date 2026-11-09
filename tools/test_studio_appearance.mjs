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
for (const {style, dark, externalTitle, resolved} of vectors) {
  const actual = globalThis.MarginGroupAppearance.resolve(style, dark ? '#171C22' : '#D4D0C8', policy, externalTitle);
  assert.deepEqual({Text: actual.ink, Accent: actual.accent, Border: actual.border, Band: actual.band, Divider: actual.divider}, resolved);
}
console.log(`PASS ${vectors.length} Studio/native appearance comparisons`);

const titles = spawnSync(dotnet, ['run', '--project', resolve(root, 'tests'), '-c', 'Release', '--no-build', '--', '--title-vectors'], {encoding:'utf8'});
assert.equal(titles.status,0,titles.stderr);
const titleVectors=JSON.parse(titles.stdout);
for(const {name,casing,text} of titleVectors) assert.equal(globalThis.MarginGroupAppearance.formatTitle(name,casing,policy),text,`${casing}: ${name}`);
console.log(`PASS ${titleVectors.length} Studio/native title case comparisons`);

const mappingsRun=spawnSync(dotnet,['run','--project',resolve(root,'tests'),'-c','Release','--no-build','--','--case-mappings'],{encoding:'utf8'});
assert.equal(mappingsRun.status,0,mappingsRun.stderr);
const mappings=JSON.parse(mappingsRun.stdout);
for(const {letter,upper,lower} of mappings){
  assert.equal(globalThis.MarginGroupAppearance.formatTitle(letter,'Uppercase',policy),upper,`uppercase U+${letter.codePointAt(0).toString(16)}`);
  assert.equal(globalThis.MarginGroupAppearance.formatTitle(letter,'Lowercase',policy),lower,`lowercase U+${letter.codePointAt(0).toString(16)}`);
}
console.log(`PASS ${mappings.length} Unicode simple-case mappings`);
