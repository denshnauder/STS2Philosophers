// Enumerates labelled initial hands under the declared clean-deck assumptions.
// Measures direct Block capacity, not survival, player skill, game RNG or win rate.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const facts = JSON.parse(fs.readFileSync(path.resolve(__dirname, '../../docs/design/native_starting_facts.json'), 'utf8'));
assert.equal(facts.schemaVersion, 1);
assert.match(facts.source.sha256, /^[A-F0-9]{64}$/);
assert.equal(facts.baseEnergy, 3);
assert.equal(facts.baseHandDraw, 5);
assert.equal(facts.characters.length, 5);
assert.equal(Object.keys(facts.cards).length, 19);
const thresholds = [5,8,11,16];
function hands(deck, count, index=0, selected=[], result=[]) {
  if(selected.length===count) { result.push([...selected]); return result; }
  for(let i=index;i<=deck.length-(count-selected.length);i++) {
    selected.push(deck[i]); hands(deck,count,i+1,selected,result); selected.pop();
  }
  return result;
}
function blockCeiling(hand) {
  // All direct Block cards here cost 1; Survivor's mandatory discard cannot
  // remove already-played Block, and >=5 initial cards leave discard choices
  // after at most 3 energy-consuming Block plays. No resource/draw engine runs.
  const direct=hand.map(id=>facts.cards[id]).filter(c=>c.directBlock>0);
  for(const card of direct) assert.equal(card.energyCost,1);
  return direct.map(c=>c.directBlock).sort((a,b)=>b-a).slice(0,facts.baseEnergy).reduce((a,b)=>a+b,0);
}
const rows=facts.characters.map(c=>{
  const deck=Object.entries(c.deck).flatMap(([id,count])=>{
    assert.ok(facts.cards[id],id); assert.ok(Number.isInteger(count)&&count>0);
    return Array(count).fill(id);
  });
  const options=hands(deck,c.draw);
  const ceilings=options.map(blockCeiling);
  const distribution={}; for(const ceiling of ceilings) distribution[ceiling]=(distribution[ceiling]||0)+1;
  return {character:c.id,deck:deck.length,draw:c.draw,hands:options.length,
    thresholds:Object.fromEntries(thresholds.map(t=>[t,ceilings.filter(v=>v>=t).length])),
    maxDirectBlock:Math.max(...ceilings),distribution};
});
for(const row of rows) {
  if(row.character==='Silent') {
    assert.equal(row.deck,12); assert.equal(row.draw,7); assert.equal(row.hands,792);
    assert.deepEqual(row.thresholds,{'5':792,'8':787,'11':726,'16':431});
    assert.equal(row.maxDirectBlock,18);
  } else {
    assert.equal(row.deck,10); assert.equal(row.draw,5); assert.equal(row.hands,252);
    assert.deepEqual(row.distribution,{'0':6,'5':60,'10':120,'15':66});
    assert.deepEqual(row.thresholds,{'5':246,'8':186,'11':66,'16':0});
    assert.equal(row.maxDirectBlock,15);
  }
}
assert.equal(rows.reduce((sum,row)=>sum+row.hands,0),1800);
assert.equal(facts.cards.FallingStar.starCost,2,'Zero energy is not zero resource cost');
assert.equal(facts.cards.Bodyguard.directBlock,0,'Summon is not direct Block');
assert.equal(facts.cards.Unleash.directDamage,null,'Osty damage is not a fixed attack');
assert.equal(facts.starterRelicFacts.BoundPhylactery.excludesTurnOneFromLateSummon,true);
console.log(JSON.stringify({scope:'Direct Block only, clean-deck hand combinations; not game playtest',rows},null,2));
console.log('Starting-hand checks passed: 1800 labelled hands; native resource distinctions preserved.');
