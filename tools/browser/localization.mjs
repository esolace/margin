import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import {mkdir,readFile} from 'node:fs/promises';
import {join} from 'node:path';
const catalogs=JSON.parse(await readFile(new URL('../../design/locales.json',import.meta.url),'utf8'));
const browser=await chromium.launch(process.env.MARGIN_BROWSER_EXECUTABLE?{executablePath:process.env.MARGIN_BROWSER_EXECUTABLE}:{});
try {
 const page=await browser.newPage({viewport:{width:900,height:600},locale:'en-US'});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto(new URL('../../design/Studio.html',import.meta.url).href);
 await page.evaluate(()=>{
  window.messages=[];window.eto={postMessage:raw=>messages.push(JSON.parse(raw))};
  window.fixture={Document:'locale-test',Host:'Grasshopper 2',Items:[{Id:'a',Name:'',Members:2,Selected:true,Revision:'r1',Style:{Fill:'#123456',Opacity:77,Designed:true,Appearance:{...groupAppearanceDefaults}}}]};
  MarginStudio.connect({native:true,language:{choice:'auto',host:'de-DE'},theme:{...defaults,Name:'Labels'},presets:[defaults],fonts:['Arial','日本語'],enabled:true,userPresets:[{Id:'my-style',Theme:{...defaults,Name:'Labels'}}],groups:fixture});
 });
 assert.equal(await page.locator('#groupsTab').innerText(),'Gruppen','automatic selection follows Rhino, not the browser language');
 await page.locator('#groupsTab').click();
 await page.locator('#groupName').fill('My <b>群組</b> {name}');
 await page.locator('#groupTitleSize').fill('23');
 const draft=await page.evaluate(()=>({name:document.getElementById('groupName').value,style:groupStyle(),state:MarginStudio.inspect()}));
 for(const language of Object.keys(catalogs)){
  await page.locator('#languageChoice').selectOption(language);
  assert.equal(await page.evaluate(()=>messages.at(-1).action),'language');
  assert.equal(await page.evaluate(()=>messages.at(-1).choice),language);
  assert(await page.locator('#languageChoice').isDisabled(),'wait for persisted host acknowledgement');
  await page.evaluate(choice=>MarginStudio.setLanguage({choice,host:'de-DE'}),language);
  assert.equal(await page.locator('html').getAttribute('lang'),language);
  assert.equal(await page.locator('#groupName').inputValue(),draft.name,'switch preserves authored name');
  assert.deepEqual(await page.evaluate(()=>groupStyle()),draft.style,'switch preserves unsaved appearance');
  assert.equal(await page.evaluate(()=>MarginStudio.inspect().groupId),'a');
  assert.equal(await page.evaluate(()=>MarginStudio.inspect().groupDirty),true);
  assert.equal(await page.evaluate(()=>MarginStudio.inspect().userPresets[0].Theme.Name),'Labels','user preset names are never translated');
  assert.equal(await page.locator('#groupPreviewName b').count(),0,'user names never become markup');
  assert.equal(await page.locator('#groupsTab').innerText(),catalogs[language]['ui.groups']);
  assert.equal(await page.locator('#groupShowTitle').evaluate(n=>document.querySelector('label[for="'+n.id+'"]').textContent),catalogs[language]['ui.show_title']);
  for(const id of ['languageChoice','groupsTab','labelsTab','applyGroup','revertGroup','saveGroupDefaults']){
   const box=await page.locator('#'+id).boundingBox();assert(box&&box.x>=0&&box.y>=0&&box.x+box.width<=901&&box.y+box.height<=601,language+' accessible action: '+id);
  }
  assert(!(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth)),language+' no page overflow');
  await page.locator('#labelsTab').click();
  await page.locator('#newPreset').click();
  await page.locator('#presetName').fill('Keep 私の名前');
  await page.evaluate(choice=>MarginStudio.setLanguage({choice,host:'en'}),language);
  assert.equal(await page.locator('#presetName').inputValue(),'Keep 私の名前','open dialog preserves the typed name');
  assert.equal(await page.locator('#presetTitle').innerText(),catalogs[language]['ui.save_preset']);
  await page.locator('#presetCancel').click();
  for(const id of ['save','export','import']){
   const box=await page.locator('#'+id).boundingBox();assert(box&&box.y+box.height<=601,language+' label footer remains reachable');
  }
  await page.locator('#groupsTab').click();
  if(process.env.MARGIN_SCREENSHOT_DIR){await mkdir(process.env.MARGIN_SCREENSHOT_DIR,{recursive:true});await page.screenshot({path:join(process.env.MARGIN_SCREENSHOT_DIR,'locale-'+language+'.png')});}
 }
 // Failed saving restores the acknowledged choice without losing the draft.
 await page.locator('#languageChoice').selectOption('fr');
 await page.evaluate(()=>{MarginStudio.setLanguage({choice:'zh-Hant',host:'en'});MarginStudio.reportError('Disk full');});
 assert.equal(await page.locator('#languageChoice').inputValue(),'zh-Hant');
 assert.equal(await page.locator('#groupName').inputValue(),draft.name);
 // Native blank names get a display-only fallback in the selected language.
 await page.evaluate(()=>{MarginStudio.setLanguage({choice:'ja',host:'en'});MarginStudio.setGroups(fixture);});
 assert.equal(await page.locator('#groupPreviewName').innerText(),'グループ');
 assert.equal(await page.locator('#groupName').inputValue(),'');
 // Browser-only preferences also survive a reopen, without modifying theme names.
 await page.goto(new URL('../../design/Studio.html',import.meta.url).href);
 await page.locator('#languageChoice').selectOption('fr');await page.reload();
 assert.equal(await page.locator('#languageChoice').inputValue(),'fr');
 assert.equal(await page.locator('#groupsTab').innerText(),'Groupes');
 await page.locator('#languageChoice').focus();await page.keyboard.press('Tab');
 assert.notEqual(await page.evaluate(()=>document.activeElement.id),'languageChoice','keyboard can leave language selector');
 assert.deepEqual(errors,[]);
 console.log('PASS all 13 languages: host selection, persisted override, drafts, names, dialog, failed save, placeholders and compact layout');
} finally {await browser.close();}
