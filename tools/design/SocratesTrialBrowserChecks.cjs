const assert=require('node:assert/strict');
const path=require('node:path');
const fs=require('node:fs');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.argv[2]||'playwright');
const output=path.resolve('bin/design');fs.mkdirSync(output,{recursive:true});
(async()=>{
  const browser=await chromium.launch({headless:true,channel:process.argv[3]||'msedge'});
  try{
    const page=await browser.newPage({viewport:{width:860,height:1000}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
    await page.goto(pathToFileURL(path.resolve('docs/design/socrates_trial.html')).href);
    const b=name=>page.getByRole('button',{name,exact:true});
    await b('说明并试授').click();assert.match(await page.locator('#error').innerText(),/没有改变/);assert.match(await page.locator('#state').innerText(),/尚未使用/);
    await page.getByLabel('你想在下一场检查什么？可留空').fill('我想看看两次命中这场是否合用。');await b('说明并试授').click();
    assert.match(await page.locator('#locked').innerText(),/两次命中/);assert.equal(await b('跳过奖励').isVisible(),false);
    await b('进入下一场固定战斗').click();await b('使用连击（1费）').click();assert.match(await page.locator('#evidence').innerText(),/实际完成2次攻击/);
    assert.equal(await b('收入试授副本').isEnabled(),true);assert.equal(await b('放弃试授副本').isEnabled(),true);
    await b('暂不回应').click();await b('放弃试授副本').click();assert.match(await page.locator('#result').innerText(),/不能换另两张或退机会/);assert.equal(await page.locator('#deck').getByText('连击',{exact:true}).count(),0);
    await b('查看本幕下一份模拟奖励').click();assert.equal(await b('不表态，直接试授').isDisabled(),true);await b('普通收入所选牌').click();assert.match(await page.locator('#result').innerText(),/仍已用尽/);
    await b('推演下一幕').click();assert.equal(await b('不表态，直接试授').isEnabled(),true);
    await page.getByLabel('纸面条件').selectOption('absent');await b('不表态，直接试授').click();await b('进入下一场固定战斗').click();
    assert.equal(await b('使用连击（1费）').count(),0);await b('使用基础攻击（1费）').click();await b('使用基础防御（1费）').click();await b('结束回合').click();await b('使用基础攻击（1费）').click();
    assert.match(await page.locator('#oldReason').innerText(),/没有提交过/);assert.match(await page.locator('#evidence').innerText(),/未抽到.*不能推出无用/);await b('收入试授副本').click();
    await page.getByLabel('纸面条件').selectOption('one');await b('不表态，直接试授').click();await b('进入下一场固定战斗').click();
    await b('使用基础攻击（1费）').click();await b('使用基础防御（1费）').click();await b('结束回合').click();await b('使用基础攻击（1费）').click();assert.match(await page.locator('#evidence').innerText(),/出现在首手/);assert.match(await page.locator('#evidence').innerText(),/没有使用.*不是自动反驳/);
    await page.getByLabel('若要限定或提出新的局部理由，请写出新句子').fill('<script>不能被执行</script>');await b('提交局部新句').click();assert.match(await page.locator('#responseState').innerText(),/<script>/);assert.equal(await page.locator('#responseState script').count(),0);await b('收入试授副本').click();
    await page.getByLabel('纸面条件').selectOption('two');await b('不表态，直接试授').click();await b('进入下一场固定战斗').click();await b('使用连击（1费）').click();await b('使用基础攻击（1费）').click();assert.match(await page.locator('#evidence').innerText(),/实际完成1次攻击/);
    for(const [width,theme]of[[860,'light'],[360,'dark'],[320,'light']]){
      await page.setViewportSize({width,height:1000});await page.emulateMedia({colorScheme:theme});const size=await page.locator('body').evaluate(e=>({scroll:e.scrollWidth,client:e.clientWidth}));assert.ok(size.scroll<=size.client+1,JSON.stringify({width,...size}));await page.screenshot({path:path.join(output,`socrates_trial_${width}_${theme}.png`),fullPage:true});
    }
    await b('重置整次推演').click();await b('不表态，直接试授').click();await b('进入下一场固定战斗').click();await b('结束回合').click();await b('结束回合').click();assert.match(await page.locator('#result').innerText(),/本局死亡/);assert.equal(await b('推演下一幕').isDisabled(),true);
    assert.deepEqual(errors,[]);console.log('PASS browser: reward lock, actual use, no draw, unused, neutral replies, exact rejection, spent opportunity, next act, death, safe text and responsive themes.');
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
