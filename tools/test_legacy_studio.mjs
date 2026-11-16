import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../src/Margin.Grasshopper/LegacyStudioBridge.cs', import.meta.url),'utf8');
const script = source.match(/Script = """\s*([\s\S]*?)\s*""";/)[1];
const navigations = [];
const location = {set href(value) {navigations.push(value);}};
const window = {location};
vm.runInNewContext(script, {window});
const messages = [
  {action:'group-defaults', style:{Fill:'#704180', Appearance:{ShowTitle:false}}},
  {action:'group-create'},
  {action:'group-apply', name:'é / 𐐨 ? # %'}
].map(JSON.stringify);
for (const message of messages) window.eto.postMessage(message);
assert.equal(navigations.length, 1, 'Commands wait for acknowledgement instead of overwriting a navigation');
for (let i=0;i<messages.length;i++) {
  assert.equal(decodeURIComponent(new URL(navigations[i]).pathname.slice(1)),messages[i]);
  window.MarginLegacyAck();
  assert.equal(navigations.length,Math.min(messages.length,i+2));
}
window.eto.postMessage(JSON.stringify({action:'save'}));
assert.equal(navigations.length,4,'A drained queue accepts another command');
console.log('PASS legacy Studio queue preserves ordering, Unicode and subsequent commands');
