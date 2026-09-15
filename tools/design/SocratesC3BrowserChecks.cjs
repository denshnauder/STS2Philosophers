const assert=require('node:assert/strict');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.argv[3] || 'playwright');
const preview=path.resolve(process.argv[2] || 'bin/design/socrates_commitment_preview.html');
(async()=>{
  const browser=await chromium.launch({headless:true,channel:process.argv[4] || 'msedge'});
  try {
    const page=await browser.newPage({viewport:{width:760,height:1000}}),errors=[];
    page.on('pageerror',e=>errors.push(e.message));await page.goto(pathToFileURL(preview).href);
    const f=page.frameLocator('iframe'),c=f.locator('#socrates-c3');
    const b=name=>c.getByRole('button',{name,exact:true});
    const result=()=>c.locator('[data-c3-result]').innerText();
    const goal=()=>c.locator('[data-c3-goal]').innerText();
    assert.equal(await f.locator('#socrates-paper').isVisible(),false);
    assert.equal(await b('结束这一回合').isDisabled(),true);
    await b('试行解围').click();await b('攻 · 1费 / 6伤害').click();await b('防一 · 1费 / 6格挡').click();
    await b('结束这一回合').click();assert.match(await result(),/指标达成 · 实际失血 2 · 当时待发抽牌 1/);
    await b('进入下一回合').click();assert.match(await goal(),/沿用解围.*无需确认/);
    assert.equal(await b('防一 · 1费 / 8格挡').isEnabled(),true);
    assert.match(await c.locator('[data-c3-state]').innerText(),/本轮抽牌请求 1/);
    await b('修订为防护（本轮无新收益）').click();assert.equal(await b('修订为解围（本轮无新收益）').isDisabled(),true);
    await b('防一 · 1费 / 8格挡').click();await b('防二 · 1费 / 8格挡').click();await b('结束这一回合').click();
    assert.match(await result(),/指标达成 · 实际失血 0 · 当时待发抽牌 0/);
    await b('进入下一回合').click();await b('修订为解围（本轮无新收益）').click();
    await b('攻 · 1费 / 6伤害').click();await b('防一 · 1费 / 6格挡').click();await b('结束这一回合').click();
    assert.match(await result(),/指标达成.*当时待发抽牌 0/);
    await b('进入下一回合').click();await b('修订为防护（本轮无新收益）').click();
    await b('防一 · 1费 / 6格挡').click();await b('结束这一回合').click();assert.match(await result(),/记录不足/);
    await c.getByLabel('C3纸面情境').selectOption('hidden');assert.match(await goal(),/尚无公开攻击/);
    assert.equal(await b('结束这一回合').isEnabled(),true);assert.equal(await b('试行防护').count(),0);
    await b('结束这一回合').click();await b('进入下一回合').click();assert.equal(await b('试行防护').isEnabled(),true);
    await c.getByLabel('C3纸面情境').selectOption('prepared');await b('试行防护').click();
    await b('结束这一回合').click();assert.match(await result(),/条件未出现/);
    await c.getByLabel('C3纸面情境').selectOption('short');await b('试行解围').click();await b('攻 · 1费 / 6伤害').click();
    assert.match(await result(),/战斗结束，未领收益清空/);assert.equal(await b('进入下一回合').isDisabled(),true);
    await c.getByLabel('C3纸面情境').selectOption('revisions');await b('本战不参与').click();
    await b('防一 · 1费 / 6格挡').click();await b('结束这一回合').click();await b('进入下一回合').click();
    assert.match(await goal(),/本战不参与/);assert.equal(await c.locator('[data-c3-choices] button').count(),0);
    await b('重新推演').click();await b('试行防护').click();await b('防一 · 1费 / 6格挡').click();await b('结束这一回合').click();
    for(const [width,theme] of [[760,'light'],[360,'dark'],[320,'light']]) {
      await page.setViewportSize({width,height:1000});await page.emulateMedia({colorScheme:theme});
      const sizes=await f.locator('body').evaluate(el=>({scroll:el.scrollWidth,client:el.clientWidth}));
      assert.ok(sizes.scroll<=sizes.client+1,JSON.stringify({width,...sizes}));
      await page.screenshot({path:path.join(path.dirname(preview),`socrates_c3_${width}_${theme}.png`),fullPage:true});
    }
    assert.deepEqual(errors,[]);
    console.log('PASS C3 browser: default view, no Keep click, repeated revision, hidden facts, initial block, decline, victory, responsive themes.');
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
