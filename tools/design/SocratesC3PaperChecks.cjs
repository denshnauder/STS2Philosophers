// Limited authored paper experiment only; no native deck, draw or enemy AI claims.
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const html=fs.readFileSync(path.resolve(__dirname,'../../docs/design/socrates_commitment.html'),'utf8');
for(const match of html.matchAll(/<script>([\s\S]*?)<\/script>/g)) new vm.Script(match[1]);
const code=html.match(/\/\/ BEGIN C3 PAPER MODEL([\s\S]*?)\/\/ END C3 PAPER MODEL/)[1];
const context={}; vm.runInNewContext(code+'\nglobalThis.paper=SocratesC3Paper;',context);
const p=context.paper;
let total=0;
function check(name,run){run();total++;console.log('PASS '+name);}
function initial(mode='clear') {
  const s=p.create(); assert.equal(p.select(s,mode),true);
  assert.equal(p.play(s,0,0),true); assert.equal(p.play(s,1),true); assert.equal(p.finish(s),true); return s;
}
check('First public invitation precedes action; invalid choices have no effect',()=>{
  const s=p.create();assert.equal(p.play(s,0,0),false);assert.equal(p.finish(s),false);
  assert.equal(p.select(s,'skip'),false);assert.equal(p.select(s,'guard'),true);
  assert.equal(p.select(s,'clear'),false);assert.equal(s.revised,false);
});
check('Symmetric reward and achieved metric can coexist with injury',()=>{
  for(const mode of ['guard','clear']) {
    const s=initial(mode);assert.equal(s.result.metric,'met');assert.equal(s.pending,1);assert.equal(s.result.loss,2);
  }
});
check('Existing criterion does not need a Keep confirmation',()=>{
  const s=initial();assert.equal(p.next(s),true);assert.equal(s.mode,'clear');assert.equal(s.changed,false);
  assert.equal(p.play(s,1),true);assert.equal(p.select(s,'guard'),false);
});
check('Repeated revisions recover after another counterexample and never mint that turn reward',()=>{
  const s=initial();p.next(s);assert.equal(s.received,1);assert.equal(s.requests,1);
  assert.equal(p.select(s,'guard'),true);p.play(s,1);p.play(s,2);p.finish(s);
  assert.equal(s.result.metric,'met');assert.equal(s.pending,0);assert.equal(s.received,1);
  p.next(s);assert.equal(p.select(s,'clear'),true);assert.equal(p.select(s,'guard'),false);
  p.play(s,0,1);p.play(s,2);p.finish(s);assert.equal(s.result.metric,'met');assert.equal(s.pending,0);
  p.next(s);assert.equal(s.incoming,null);assert.equal(p.select(s,'guard'),true);
  p.play(s,1);p.play(s,2);p.finish(s);assert.equal(s.result.metric,'incomplete');assert.equal(s.pending,0);
});
check('Invalid card or target does not consume the revision window',()=>{
  const s=initial();p.next(s);
  assert.equal(p.play(s,0,0),false);assert.equal(p.play(s,99),false);assert.equal(s.locked,false);
  assert.equal(p.select(s,'guard'),true);assert.equal(p.play(s,1),true);assert.equal(p.play(s,1),false);
});
check('Ending the turn also locks declarations and duplicate settlement',()=>{
  const s=p.create();p.select(s,'guard');p.finish(s);
  const saved=JSON.stringify(s);assert.equal(p.finish(s),false);assert.equal(p.select(s,'clear'),false);
  assert.equal(JSON.stringify(s),saved);
});
check('Opening consumes pending once before new decisions',()=>{
  const s=initial();assert.equal(s.requests,0);p.next(s);
  assert.equal(s.received,1);assert.equal(s.pending,0);assert.equal(p.next(s),false);assert.equal(s.requests,1);
});
check('Declining lasts for the battle and does not block ordinary play',()=>{
  const s=p.create();assert.equal(p.decline(s),true);assert.equal(p.select(s,'guard'),false);
  p.play(s,1);p.finish(s);p.next(s);
  assert.equal(p.invitation(s),false);assert.equal(p.canAct(s),true);assert.equal(p.select(s,'clear'),false);
  assert.equal(p.create().declined,false);
});
check('Only hidden intents neither reveal the damage nor trigger an invitation',()=>{
  const s=p.create('hidden');assert.equal(s.incoming,null);assert.equal(s.frozen.length,0);
  assert.equal(p.select(s,'guard'),false);assert.equal(p.canAct(s),true);
  p.play(s,1);p.finish(s);assert.equal(s.result.loss,2);assert.equal(s.result.metric,'none');
  p.next(s);assert.equal(p.invitation(s),true);assert.equal(p.select(s,'guard'),true);
});
check('Already sufficient initial block permits a criterion but does not mint reward',()=>{
  const s=p.create('prepared');assert.equal(p.select(s,'guard'),true);p.play(s,1);p.finish(s);
  assert.equal(s.result.metric,'no_condition');assert.equal(s.pending,0);
});
check('Frozen attacking identities exclude a nonattacking kill',()=>{
  const s=p.create();p.select(s,'clear');p.play(s,0,1);p.play(s,1);p.finish(s);
  assert.equal(s.result.metric,'unmet');assert.equal(s.pending,0);
});
check('A later intent change does not rewrite the original scope',()=>{
  const s=p.create();p.select(s,'clear');s.enemies[0].publicDamage=0;s.enemies[0].actualDamage=0;
  p.play(s,0,0);p.finish(s);assert.equal(s.result.metric,'met');
});
check('Final kill clears reward and prohibits another turn',()=>{
  const s=p.create('short');p.select(s,'clear');p.play(s,0,0);
  assert.equal(s.result.metric,'met');assert.equal(s.result.won,true);assert.equal(s.pending,0);
  assert.equal(p.next(s),false);assert.equal(p.finish(s),false);
});
check('Death clears otherwise earned reward',()=>{
  const s=p.create();s.hp=1;p.select(s,'guard');p.play(s,1);p.finish(s);
  assert.equal(s.result.metric,'met');assert.equal(s.result.dead,true);assert.equal(s.pending,0);assert.equal(p.next(s),false);
});
check('Six turn excerpt is not falsely reported as a won or finished battle',()=>{
  const s=p.create();p.select(s,'guard');
  for(let round=1;round<=6;round++) {
    for(let i=0;i<s.cards.length;i++)if(s.cards[i][3])p.play(s,i);
    assert.equal(p.finish(s),true);
    if(round<6)assert.equal(p.next(s),true);
  }
  assert.equal(s.terminal,false);assert.equal(s.pending,1);assert.equal(p.next(s),false);
  assert.equal(s.enemies[2].hp,60);assert.equal(s.history.length,6);
});
console.log(`PASS ${total} C3 paper checks; not engine or gameplay acceptance.`);
