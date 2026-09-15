// Inspect the rendered local paper exercise, never Steam, Godot or STS2.
const assert=require('node:assert/strict');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.argv[3] || 'playwright');
const preview=path.resolve(process.argv[2] || 'bin/design/socrates_commitment_preview.html');
(async()=>{
  const browser=await chromium.launch({headless:true,...(process.argv[4]?{channel:process.argv[4]}:{})});
  try {
    const page=await browser.newPage({viewport:{width:760,height:1100}});
    const errors=[]; page.on('pageerror',error=>errors.push(error.message));
    await page.goto(pathToFileURL(preview).href);
    const f=page.frameLocator('iframe');
    await f.locator('#socrates-history > summary').click();
    await f.getByText('有限承诺 · 纸面试玩',{exact:true}).waitFor();
    const button=name=>f.locator('#socrates-paper').getByRole('button',{name,exact:true});
    const result=()=>f.locator('[data-result]').innerText();
    await button('试行防护').click();
    assert.equal(await button('试行解围').isDisabled(),true);
    await button('防一 · 1费 / 6格挡').click(); await button('防二 · 1费 / 6格挡').click();
    await button('结束回合').click();
    assert.match(await result(),/生命损失 0 · 次回合可领抽牌 1/);
    await button('重试本例').click(); await button('试行解围').click();
    await button('攻 · 1费 / 6伤害').click(); await button('防一 · 1费 / 6格挡').click();
    await button('结束回合').click();
    assert.match(await result(),/生命损失 2 · 次回合可领抽牌 2/);
    assert.equal(await button('结束回合').isDisabled(),true);
    await f.getByLabel('纸面局面',{exact:true}).selectOption('C11');
    await button('试行防护').click(); await button('防 · 1费 / 8格挡').click(); await button('结束回合').click();
    assert.match(await result(),/生命损失 8 · 次回合可领抽牌 1/);
    await f.getByLabel('纸面局面',{exact:true}).selectOption('C13');
    await button('试行解围').click(); await button('攻 · 1费 / 6伤害').click();
    await button('防一 · 1费 / 6格挡').click(); await button('结束回合').click();
    assert.match(await result(),/次回合可领抽牌 0/); assert.match(await result(),/修订代价已计入/);
    await f.getByLabel('纸面局面',{exact:true}).selectOption('C05');
    await button('试行解围').click(); await button('攻 · 1费 / 6伤害').click();
    assert.match(await result(),/战斗已结束，没有次回合奖励/);
    await f.getByLabel('纸面局面',{exact:true}).selectOption('C15');
    await button('试行防护').click(); await button('防 · 1费 / 8格挡').click(); await button('结束回合').click();
    assert.match(await result(),/生命损失 40 · 次回合可领抽牌 0/);
    await f.getByLabel('纸面局面',{exact:true}).selectOption('C03');
    await f.getByLabel('纸面对照规则',{exact:true}).selectOption('C2');
    assert.equal(await f.getByLabel('行动前的解围目标',{exact:true}).isVisible(),false);
    assert.match(await f.locator('[data-goal]').innerText(),/甲、乙/);
    await button('试行防护').click(); await button('攻 · 1费 / 6伤害').click();
    await button('防一 · 1费 / 6格挡').click(); await button('结束回合').click();
    assert.match(await result(),/生命损失 2 · 次回合可领抽牌 1/);
    await f.getByLabel('纸面局面',{exact:true}).selectOption('C10');
    await button('试行解围').click();
    await f.getByLabel('本次出牌的攻击目标',{exact:true}).selectOption('1');
    await button('攻 · 1费 / 6伤害').click(); await button('防 · 1费 / 5格挡').click();
    await button('结束回合').click(); assert.match(await result(),/次回合可领抽牌 2/);
    await f.getByLabel('纸面对照规则',{exact:true}).selectOption('C1');
    assert.equal(await f.getByLabel('行动前的解围目标',{exact:true}).isVisible(),true);
    assert.equal(await button('试行解围').isEnabled(),true);
    await f.getByLabel('纸面对照规则',{exact:true}).selectOption('C2');
    await f.getByLabel('纸面局面',{exact:true}).selectOption('C03');
    for(const [width,theme] of [[760,'light'],[360,'dark'],[320,'light']]) {
      await page.setViewportSize({width,height:1100}); await page.emulateMedia({colorScheme:theme});
      const sizes=await f.locator('body').evaluate(el=>({scroll:el.scrollWidth,client:el.clientWidth}));
      assert.ok(sizes.scroll<=sizes.client+1,JSON.stringify({width,...sizes}));
      await page.screenshot({path:path.join(path.dirname(preview),`socrates_${width}_${theme}.png`),fullPage:true});
    }
    assert.deepEqual(errors,[]);
    console.log('PASS C1/C2 browser interactions, variant reset, frozen scope, duplicate lock, revision, victory, death, and 760/360/320px layout.');
  } finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
