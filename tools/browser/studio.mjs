import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import {mkdir} from 'node:fs/promises';
import {join} from 'node:path';

const browser = await chromium.launch(process.env.MARGIN_BROWSER_EXECUTABLE
  ? {executablePath: process.env.MARGIN_BROWSER_EXECUTABLE} : {});
try {
  const page = await browser.newPage({viewport:{width:1080,height:760}});
  const errors=[]; page.on('pageerror',error=>errors.push(error.message));
  await page.goto(new URL('../../design/Studio.html',import.meta.url).href);
  await page.evaluate(()=>{
    window.messages=[]; window.eto={postMessage:message=>messages.push(JSON.parse(message))};
    const style={Fill:'#253E56',Opacity:85,Designed:true,Appearance:{...groupAppearanceDefaults}};
    window.fixture={Document:'qa',Host:'Grasshopper',Items:[
      {Id:'a',Name:'Guide curves',Selected:true,Members:2,Revision:'a1',Style:style},
      {Id:'b',Name:'Surface',Selected:false,Members:3,Revision:'b1',Style:style}]};
    MarginStudio.connect({native:true,theme:defaults,presets:[defaults],fonts:['Arial'],enabled:true,userPresets:[],groups:fixture});
  });
  await page.locator('#groupsTab').click();
  await page.evaluate(()=>MarginStudio.setGroups({...fixture,Host:'Grasshopper 2'}));
  assert(await page.locator('#createGroup').isVisible(),'GH2 offers native grouping in Studio');
  assert(await page.locator('#saveGroupDefaults').isVisible(),'GH2 offers saved group defaults');
  await page.locator('#createGroup').click();
  assert.equal(await page.evaluate(()=>messages.at(-1).action),'group-create');
  await page.locator('#saveGroupDefaults').click();
  assert.equal(await page.evaluate(()=>messages.at(-1).action),'group-defaults');
  for(const outline of ['Blob','Rectangles','None']){
    await page.evaluate(outline=>{
      fixture.Items[0].Outline=outline;
      fixture.Items[0].Style={Fill:'#000000',Opacity:100,Designed:true,Appearance:{...groupAppearanceDefaults,Heading:'Band'}};
      MarginStudio.setGroups(fixture);
    },outline);
    assert.equal(await page.locator('#groupText').inputValue(),'#23282c',outline+' Auto title contrasts with the light canvas');
    assert.equal(await page.locator('#groupSpecimen').evaluate(n=>n.style.getPropertyValue('--g-ink')),'#23282C');
    const title=await page.locator('#groupPreviewName').boundingBox(),frame=await page.locator('#groupSpecimen').boundingBox();
    assert(title.y+title.height<=frame.y,outline+' preview title sits outside the fill');
    if(process.env.MARGIN_SCREENSHOT_DIR){await mkdir(process.env.MARGIN_SCREENSHOT_DIR,{recursive:true});await page.screenshot({path:join(process.env.MARGIN_SCREENSHOT_DIR,'studio-'+outline.toLowerCase()+'-light.png')});}
    await page.locator('#groupTone').click();
    assert.equal(await page.locator('#groupText').inputValue(),'#f4f3f0',outline+' Auto title follows the dark canvas');
    await page.locator('#groupText').evaluate(input=>{input.value='#ff35a0';input.dispatchEvent(new Event('input',{bubbles:true}));});
    await page.locator('#groupTone').click();
    assert.equal(await page.locator('#groupText').inputValue(),'#ff35a0',outline+' custom title colour stays explicit');
    await page.locator('#revertGroup').click();
  }
  await page.evaluate(()=>{fixture.Items[0].Outline='Box';MarginStudio.setGroups(fixture);});
  assert.equal(await page.locator('#groupText').inputValue(),'#f4f3f0','Box restores contrast against its dark band');
  await page.evaluate(()=>{fixture.Items[0].Name='';MarginStudio.setGroups(fixture);});
  assert.equal(await page.locator('#groupTarget').innerText(),'Group','selected unnamed group is not future defaults');
  assert.equal(await page.locator('#groupPreviewName').innerText(),'Group','blank name has a neutral canvas preview');
  assert.equal(await page.locator('#groupName').inputValue(),'','fallback does not replace the native blank name');
  for (const host of ['Grasshopper','Grasshopper 2']) {
    await page.evaluate(host=>{
      fixture.Host=host;
      Object.assign(fixture.Items[0],{Name:'',Outline:'None',Selected:false});
      fixture.Items[0].Style.Appearance.ShowTitle=false;
      MarginStudio.setGroups(fixture);
    },host);
    await page.getByLabel('Edit group from this definition').selectOption('a',{timeout:1800});
    assert.equal(await page.locator('#groupTarget').innerText(),'Group',host+' identifies the unnamed group');
    assert(await page.locator('#groupPreviewName').isHidden(),host+' suppresses the fallback');
    assert.equal(await page.locator('#groupSpecimen').evaluate(n=>getComputedStyle(n).marginTop),'0px');
    await page.getByLabel('Show title',{exact:true}).check();
    assert.equal(await page.locator('#groupPreviewName').innerText(),'Group');
    await page.locator('#applyGroup').click();
    const restore=await page.evaluate(()=>messages.at(-1));
    assert.equal(restore.id,'a'); assert.equal(restore.name,''); assert.equal(restore.style.Appearance.ShowTitle,true);
    assert.equal(restore.style.Designed,true); assert.equal(restore.style.Fill,'#000000');
    await page.evaluate(()=>{fixture.Items[0].Selected=true;fixture.Items[0].Style.Appearance.ShowTitle=true;MarginStudio.setGroups(fixture);});
  }
  await page.evaluate(()=>{fixture.Host='Grasshopper';fixture.Items[0].Outline='Box';});
  await page.evaluate(()=>{fixture.Items[0].Name='Guide curves';MarginStudio.setGroups(fixture);});
  assert(await page.locator('#groupText').isVisible(),'title colour is visible in Studio');
  assert(await page.locator('#groupText').isEnabled(),'title colour remains editable in automatic mode');
  await page.locator('#groupName').fill('Edited group');
  await page.locator('#groupTitleCase').selectOption('Uppercase');
  assert.equal(await page.locator('#groupPreviewName').innerText(),'EDITED GROUP');
  assert.equal(await page.locator('#groupName').inputValue(),'Edited group','casing leaves the saved name intact');
  await page.locator('#groupText').evaluate(input=>{input.value='#ff35a0';input.dispatchEvent(new Event('input',{bubbles:true}));});
  assert(!(await page.locator('#groupAutoText').isChecked()),'choosing title colour switches to custom');
  assert.equal(await page.locator('#groupSpecimen').evaluate(node=>node.style.getPropertyValue('--g-ink')),'#FF35A0');
  if(process.env.MARGIN_SCREENSHOT_DIR){await mkdir(process.env.MARGIN_SCREENSHOT_DIR,{recursive:true});await page.screenshot({path:join(process.env.MARGIN_SCREENSHOT_DIR,'studio-title-controls.png')});}
  await page.locator('#applyGroup').click();
  const apply=await page.evaluate(()=>messages.findLast(m=>m.action==='group-apply'));
  assert.equal(apply.name,'Edited group');assert.equal(apply.style.Appearance.TitleCase,'Uppercase');assert.equal(apply.style.Appearance.Text,'#FF35A0');
  await page.evaluate(()=>{fixture.Items[0]={...fixture.Items[0],Name:'Edited group',Revision:'a2',Style:messages.findLast(m=>m.action==='group-apply').style};MarginStudio.setGroups(fixture,'Saved');});
  await page.locator('#groupAutoText').check();
  await page.locator('#saveGroupDefaults').click();
  assert.equal(await page.evaluate(()=>messages.at(-1).style.Appearance.Text),null,'automatic mode saves without a custom override');
  await page.locator('#groupName').fill('Unsaved draft');
  await page.evaluate(()=>MarginStudio.setGroups(fixture,'Defaults saved',true));
  assert.equal(await page.locator('#groupName').inputValue(),'Unsaved draft','defaults acknowledgement preserves draft');
  await page.evaluate(()=>{fixture.Items[0].Selected=false;fixture.Items[1].Selected=true;MarginStudio.setGroups(fixture,null,true);});
  assert.equal(await page.locator('#groupName').inputValue(),'Unsaved draft','canvas selection does not discard draft');
  await page.locator('#revertGroup').click();
  assert.equal(await page.locator('#groupName').inputValue(),'Surface');
  await page.locator('#labelsTab').click();
  await page.evaluate(()=>MarginStudio.setSession({desired:true,effective:false,state:'Paused',error:'Drawing failed'}));
  assert(await page.locator('#retryLabels').isVisible());
  await page.locator('#retryLabels').click();assert.equal(await page.evaluate(()=>messages.at(-1).enabled),true);
  await page.locator('#toggle').click();assert.equal(await page.evaluate(()=>messages.at(-1).enabled),false,'paused labels can be switched off');
  await page.evaluate(()=>MarginStudio.setSession({desired:true,effective:false,state:'Paused',error:'Save failed'}));
  assert.equal(await page.locator('#toggle').getAttribute('aria-pressed'),'true','failed save restores desired choice');
  await page.evaluate(()=>MarginStudio.setSession({desired:false,effective:false,state:'Off',error:null}));
  assert(!(await page.locator('#retryLabels').isVisible()));
  await page.evaluate(()=>MarginStudio.setSession({desired:true,effective:false,state:'WaitingForCanvas',error:null}));
  assert.match(await page.locator('#connection').innerText(),/Waiting/);
  await page.evaluate(()=>MarginStudio.setSession({desired:true,effective:true,state:'On',error:null}));
  assert.match(await page.locator('#connection').innerText(),/Connected/);
  await page.setViewportSize({width:900,height:600});
  assert(!(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth)),'minimum size has no horizontal overflow');
  await page.locator('#groupsTab').click();
  if(process.env.MARGIN_SCREENSHOT_DIR)await page.screenshot({path:join(process.env.MARGIN_SCREENSHOT_DIR,'studio-title-controls-compact.png')});
  assert.deepEqual(errors,[]);
  console.log('PASS Studio title colour, automatic/custom mode, apply payload, defaults/draft/revert, paused visibility and recovery');
} finally {await browser.close();}
