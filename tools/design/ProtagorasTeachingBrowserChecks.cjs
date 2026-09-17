const assert=require('node:assert/strict'),path=require('node:path'),fs=require('node:fs'),{pathToFileURL}=require('node:url');
const {chromium}=require(process.argv[2]||'playwright');
const output=path.resolve('bin/design');fs.mkdirSync(output,{recursive:true});
(async()=>{
  const browser=await chromium.launch({headless:true,channel:process.argv[3]||'msedge'});
  try{
    const page=await browser.newPage({viewport:{width:860,height:1100}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
    await page.goto(pathToFileURL(path.resolve('docs/design/protagoras_teaching.html')).href);
    const b=name=>page.getByRole('button',{name,exact:true}),reset=()=>b('重置整次推演').click();
    assert.match(await page.locator('#party1').innerText(),/生命6\/10/);
    await b('这次采用：每次给生命最低者').click();assert.match(await page.locator('#firstResult').innerText(),/伤者：2份，生命2→6/);
    await b('记住本次份数').click();assert.match(await page.locator('#proposal').innerText(),/玩家：0份，生命2→2/);assert.match(await page.locator('#proposal').innerText(),/最终4\/10/);
    await page.screenshot({path:path.join(output,'protagoras_teaching_sample.png'),fullPage:true});
    await b('交给他执行，然后休整').click();assert.match(await page.locator('#records').innerText(),/所教示例：席位份数0\/2\/1/);assert.match(await page.locator('#records').innerText(),/同行者执行/);assert.match(await page.locator('#status').innerText(),/生命4\/10/);
    await page.getByLabel('你留下的看法，可留空').fill('<script>window.paperInjected=true</script>');await b('离开这次共同实践').click();assert.match(await page.locator('#savedNote').innerText(),/<script>/);assert.equal(await page.locator('#savedNote script').count(),0);assert.equal(await page.evaluate(()=>window.paperInjected),undefined);
    await reset();await b('这次采用：每次给生命最低者').click();await b('再按这个办法算').click();assert.match(await page.locator('#proposal').innerText(),/玩家：1份，生命2→4/);assert.match(await page.locator('#proposal').innerText(),/最终6\/10/);await b('交给他执行，然后休整').click();assert.match(await page.locator('#question').innerText(),/值得遵守/);await b('离开这次共同实践').click();assert.match(await page.locator('#savedNote').innerText(),/没有提交看法/);
    await reset();await b('这次采用：每次给生命最低者').click();await b('记住本次份数').click();await b('亲自改分：按贡献份数').click();assert.match(await page.locator('#records').innerText(),/休整实际回复0，玩家最终6/);assert.match(await page.locator('#question').innerText(),/你自己的分配/);assert.equal(await b('交给他执行，然后休整').isVisible(),false);
    await page.getByLabel('换场条件').selectOption('rotation');await b('这次采用：按贡献份数').click();await b('再按这个办法算').click();assert.match(await page.locator('#proposal').innerText(),/玩家：0份，生命6→6/);await b('交给他执行，然后休整').click();assert.match(await page.locator('#status').innerText(),/生命8\/10/);
    await reset();await b('对照：各用自己的份数').click();assert.match(await page.locator('#status').innerText(),/生命8\/10/);assert.match(await page.locator('#records').innerText(),/没有教学指令/);assert.match(await page.locator('#question').innerText(),/没有教学材料/);
    await reset();await b('不参与，直接离开').click();assert.match(await page.locator('#ending').innerText(),/没有参与.*生命仍6/);assert.equal(await b('离开这次共同实践').isVisible(),false);
    for(const [width,theme]of[[860,'light'],[360,'light'],[360,'dark'],[320,'dark']]){
      await page.setViewportSize({width,height:1100});await page.emulateMedia({colorScheme:theme});await reset();
      for(const phase of ['initial','second','outcome','done']){
        if(phase==='second'){await b('这次采用：每人一份').click();await b('再按这个办法算').click();}
        if(phase==='outcome')await b('交给他执行，然后休整').click();
        if(phase==='done')await b('离开这次共同实践').click();
        assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true,`${width}/${theme}/${phase} overflow`);
        await page.screenshot({path:path.join(output,`protagoras_teaching_${phase}_${width}_${theme}.png`),fullPage:true});
      }
    }
    assert.equal(errors.length,0,errors.join('\n'));console.log('PASS teaching sample/method/control/self-use/exit, safe text and 860/360/320 light/dark browser checks; no native game or fun verdict.');
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
