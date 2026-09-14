// Checks only the fictional paper model in docs/design. This is not the STS2 engine.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = path.resolve(__dirname, '../../docs/design/socrates_commitment.html');
const html = fs.readFileSync(source, 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
new vm.Script(script);
const model = script.match(/\/\/ BEGIN PAPER MODEL([\s\S]*?)\/\/ END PAPER MODEL/)[1];
const context = {};
vm.runInNewContext(model + '\nglobalThis.paper = SocratesPaper;', context);
const p = context.paper;
let checks = 0;
function check(name, run) { run(); checks++; console.log('PASS ' + name); }
function act(id, mode, cards, target=0) {
  const s=p.create(id); assert.equal(p.commit(s, mode, target), true);
  for(const i of cards) assert.equal(p.play(s,i,target), true);
  if(!s.ended) assert.equal(p.finish(s),true);
  return s;
}
check('Two nonidentical decisions in C03', () => {
  const g=act('C03','guard',[1,2]), c=act('C03','clear',[0,1]);
  assert.equal(g.result.loss,0); assert.equal(g.result.draw,1); assert.equal(g.result.enemyHp,36);
  assert.equal(c.result.loss,2); assert.equal(c.result.draw,2); assert.equal(c.result.enemyHp,30);
});
check('No retroactive declaration or target change', () => {
  const s=p.create('C03'); assert.equal(p.play(s,0,0),false);
  assert.equal(p.commit(s,'clear',0),true); assert.equal(p.commit(s,'guard'),false);
  assert.equal(p.commit(s,'clear',1),false); assert.equal(s.target,0);
});
check('Costs, card identity and legal targets', () => {
  const s=p.create('C03'); p.commit(s,'clear',0);
  assert.equal(p.play(s,0,8),false); assert.equal(s.energy,2);
  assert.equal(p.play(s,0,0),true); assert.equal(p.play(s,0,1),false);
  assert.equal(p.play(s,1,0),true); assert.equal(p.play(s,2,0),false);
  assert.equal(s.energy,0); assert.equal(p.play(s,NaN,0),false);
});
check('Ending is idempotent', () => {
  const s=act('C03','guard',[1,2]); const saved=JSON.stringify(s);
  assert.equal(p.finish(s),false); assert.equal(p.play(s,0,0),false);
  assert.equal(JSON.stringify(s),saved);
});
check('No next turn after victory', () => {
  const s=act('C05','clear',[0]); assert.equal(s.ended,true);
  assert.equal(s.result.clearMet,true); assert.equal(s.result.won,true); assert.equal(s.result.draw,0);
});
check('Absent public condition and already sufficient block', () => {
  const none=p.create('C06'); assert.equal(p.commit(none,'guard'),false);
  assert.equal(p.commit(none,'clear',0),false); assert.equal(p.commit(none,'skip'),true);
  const covered=p.create('C07'); assert.equal(p.commit(covered,'guard'),false);
});
check('Unachievable displayed conditions are not philosophical failure', () => {
  const s=act('C09','guard',[1]); assert.equal(s.result.draw,0);
  assert.equal('BrokenTurns' in s,false); assert.equal('philosophyScore' in s,false);
});
check('A different defeated enemy cannot satisfy frozen target', () => {
  const s=p.create('C10'); p.commit(s,'clear',0); p.play(s,0,1); p.play(s,1,0); p.finish(s);
  assert.equal(s.enemies[1].hp,0); assert.equal(s.result.clearMet,false); assert.equal(s.result.draw,0);
});
check('Criterion achievement is not no damage', () => {
  const s=act('C11','guard',[1]); assert.equal(s.result.guardMet,true);
  assert.equal(s.result.loss,8); assert.equal(s.result.draw,1);
});
check('Changing intent does not silently lower frozen threshold', () => {
  const s=act('C12','guard',[0]); assert.equal(s.threshold,8);
  assert.equal(s.result.loss,0); assert.equal(s.result.guardMet,false);
});
check('Revision forfeits this round only in this single round model', () => {
  const c=act('C13','clear',[0,1]); assert.equal(c.revised,true);
  assert.equal(c.result.clearDraw,2); assert.equal(c.result.draw,0);
  const g=act('C14','guard',[1,2]); assert.equal(g.revised,true);
  assert.equal(g.result.guardDraw,1); assert.equal(g.result.draw,0);
  const keep=act('C13','guard',[1,2]); assert.equal(keep.revised,false); assert.equal(keep.result.draw,1);
});
check('No future reward after player death', () => {
  const s=act('C15','guard',[1]); assert.equal(s.result.guardMet,true);
  assert.equal(s.result.survived,false); assert.equal(s.result.draw,0);
});
check('Invalid modes and targets cannot commit', () => {
  const s=p.create('C03'); for(const [m,t] of [['bad',0],['clear',9],['clear',NaN],['clear',0.5]])
    assert.equal(p.commit(s,m,t),false);
  assert.equal(s.committed,false);
});
const expected=[10,20,15,27,3,5,4,22,3,8,3,3];
const result=[];
check('All 123 legal action prefixes across 12 fixed fixtures', () => {
  expected.forEach((count,i)=>{
    const id='C'+String(i+1).padStart(2,'0'), rows=p.enumerate(id);
    assert.equal(rows.length,count,id);
    result.push({case:id,legalPrefixes:rows.length,
      maxGuardDraw:Math.max(...rows.map(r=>r.guardDraw)),maxClearDraw:Math.max(...rows.map(r=>r.clearDraw))});
  });
  assert.equal(result.reduce((n,r)=>n+r.legalPrefixes,0),123);
  assert.equal(result[0].maxGuardDraw,1); assert.equal(result[0].maxClearDraw,0);
  assert.equal(result[1].maxGuardDraw,0); assert.equal(result[1].maxClearDraw,2);
});
console.table(result);
console.log(`${checks} paper model checks passed. No game build, balance, or playtest claim.`);
