const assert=require('node:assert/strict'),path=require('node:path'),fs=require('node:fs'),{pathToFileURL}=require('node:url');
const {chromium}=require(process.argv[2]||'playwright');
const output=path.resolve('bin/design');fs.mkdirSync(output,{recursive:true});
(async()=>{
 const browser=await chromium.launch({headless:true,channel:process.argv[3]||'msedge'});
 try{
  const page=await browser.newPage({viewport:{width:860,height:1100}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.resolve('docs/design/plato_guard_duty.html')).href);
  const click=id=>page.locator('#'+id).click(),choice=x=>page.locator('[data-choice="'+x+'"]').click(),step=()=>click('step'),reset=()=>click('reset'),historyText=()=>page.locator('#records').textContent();
  assert.match(await page.locator('#invitation').innerText(),/维护者.*旅人/);assert.match(await page.locator('#entry').innerText(),/不是原典引文/);
  await page.locator('#stance').selectOption('refuse');await click('start');
  assert.match(await page.locator('#playerStatus').innerText(),/12\/12/);assert.equal(await historyText(),'');
  await choice('to-player');assert.match(await page.locator('#forecast').innerText(),/预测，尚未执行/);assert.match(await page.locator('#forecast').innerText(),/第2段从灯火改向你/);assert.equal((await page.locator('#forecast').innerText()).includes('实际'),false);assert.match(await page.locator('#playerStatus').innerText(),/12\/12/);assert.equal(await page.locator('#calculation').evaluate(x=>x.open),false);await page.locator('#calculation summary').focus();await page.keyboard.press('Enter');assert.equal(await page.locator('#calculation').evaluate(x=>x.open),true);assert.match(await page.locator('#calculationText').innerText(),/基础攻击预计命中/);
  await step();assert.match(await page.locator('#playerStatus').innerText(),/5\/12/);assert.match(await page.locator('#siteStatus').innerText(),/9\/9/);await page.locator('#history summary').click();assert.match(await page.locator('#records').innerText(),/实际失6/);assert.equal(await page.locator('#choices').isVisible(),false);
  await step();assert.equal(await page.locator('#assist').isDisabled(),true);assert.match(await page.locator('#assistForecast').innerText(),/没有进一步协助/);
  await click('rest');await step();await step();assert.match(await page.locator('#question').innerText(),/没有接受这份要求/);assert.match(await page.locator('#arrivals').innerText(),/旅人乙.*立即通行/);await click('finish');assert.match(await page.locator('#savedNote').innerText(),/没有提交句子/);
  await reset();await click('start');const self=page.locator('[data-choice="to-site"]');await self.focus();await page.keyboard.press('Enter');assert.equal(await self.getAttribute('aria-pressed'),'true');assert.match(await page.locator('#siteStatus').innerText(),/9\/9/);await step();await step();
  assert.match(await page.locator('#assistForecast').innerText(),/放弃休整可回复0/);assert.match(await page.locator('#arrivals').innerText(),/甲.*等待/);await click('assist');assert.match(await page.locator('#arrivals').innerText(),/等待1个观察时段后通行/);assert.match(await historyText(),/实际到场，灯生命0.*仍在等待/);await step();await step();
  const history=await historyText();await page.locator('#note').fill('<script>window.paperInjected=true</script>');await click('finish');assert.equal(await page.locator('#savedNote script').count(),0);assert.equal(await page.evaluate(()=>window.paperInjected),undefined);assert.match(await page.locator('#savedNote').innerText(),/<script>/);assert.equal(await historyText(),history);
  await reset();await page.locator('#fixture').selectOption('hard');await click('start');await choice('to-site');await step();await step();await click('rest');assert.equal(await page.locator('[data-choice="to-site"]').isDisabled(),true);assert.match(await page.locator('#intents').innerText(),/8打你.*4打你/);await step();await step();assert.match(await page.locator('#endingTitle').innerText(),/死亡/);assert.match(await page.locator('#question').innerText(),/后续计划到场未观测/);assert.equal((await page.locator('#arrivals').innerText()).includes('旅人乙'),false);assert.match(await historyText(),/实际失2，生命2→0/);
  await reset();await page.locator('#stance').selectOption('attempt');await click('start');await step();await step();await click('rest');await step();await choice('to-player');await step();await step();assert.match(await page.locator('#playerStatus').innerText(),/2\/12/);assert.match(await page.locator('#siteStatus').innerText(),/1\/9/);assert.match(await page.locator('#question').innerText(),/愿意尝试/);
  await reset();await click('skip');assert.match(await page.locator('#question').innerText(),/没有观察到/);assert.equal(await historyText(),'');
  for(const [width,theme]of [[860,'light'],[360,'light'],[360,'dark'],[320,'dark']]){
   await page.setViewportSize({width,height:1100});await page.emulateMedia({colorScheme:theme});
   for(const phase of ['entry','battle','maintenance','outcome']){
    await reset();await page.locator('#fixture').selectOption('normal');
    if(phase!=='entry')await click('start');
    if(['maintenance','outcome'].includes(phase)){await choice('to-site');await step();await step();}
    if(phase==='outcome'){await click('assist');await step();await step();await page.locator('#note').fill('重复观察'.repeat(100));await click('finish');}
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true,`${width}/${theme}/${phase} overflow`);
    await page.screenshot({path:path.join(output,`plato_guard_duty_${phase}_${width}_${theme}.png`),fullPage:true});
    if(phase==='battle'){assert.equal(await page.locator('#calculation').evaluate(x=>x.open),false);await page.locator('#calculation summary').focus();await page.keyboard.press('Enter');assert.equal(await page.locator('#calculation').evaluate(x=>x.open),true);assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true,`${width}/${theme}/calculation overflow`);}
    if(phase!=='entry'){await page.locator('#history summary').focus();await page.keyboard.press('Enter');assert.equal(await page.locator('#history').evaluate(x=>x.open),true);assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true,`${width}/${theme}/history overflow`);}
   }
  }
  assert.equal(errors.length,0,errors.join('\n'));console.log('PASS actual/forecast, refusal, keyboard choices/history, delayed crossing, zero healing cost, death/unobserved arrival, safe optional text and 860/360/320 light/dark browser checks.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
