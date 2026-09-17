const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const html=fs.readFileSync('docs/design/plato_rule_authority.html','utf8');
const start=html.indexOf('// BEGIN PLATO RULE AUTHORITY PAPER MODEL'),end=html.indexOf('// END PLATO RULE AUTHORITY PAPER MODEL');
assert.ok(start>=0&&end>start);
const M=vm.runInNewContext(html.slice(start,end)+'\nPlatoRuleAuthorityPaper');
const snapshot=s=>JSON.stringify(s),cards=s=>s.records.filter(r=>r.kind==='card');
function run(which,mode,actions){const s=M.create(which,mode==='baseline');if(mode!=='baseline')assert.equal(M.choose(s,mode),true);for(const [id,target]of actions)assert.equal(M.play(s,id,target),true);assert.equal(M.finish(s),true);return s;}
const defs=n=>Array.from({length:n},(_,i)=>['defend'+(i+1),'self']);
const a=run('A','delegate',defs(2));assert.equal(a.hp,8);assert.equal(a.energy,1);assert.equal(a.enemies[0].hp,5);assert.equal(cards(a)[0].paid,0);assert.equal(cards(a)[0].hpBefore,13);assert.equal(cards(a)[0].hpAfter,5);
const direct=run('A','direct',[['bash','B'],...defs(2)]);assert.equal(direct.hp,12);assert.equal(direct.energy,0);assert.equal(direct.enemies[1].hp,0);assert.equal(cards(direct)[0].paid,1);assert.equal(direct.records.filter(r=>r.kind==='hit').length,1);
const base=run('A','baseline',[['bash','B'],...defs(1)]);assert.equal(base.hp,12);assert.equal(base.energy,0);assert.equal(cards(base)[0].paid,2);assert.equal(base.block,1);
const unused=run('A','none',[['bash','B'],...defs(1)]);assert.equal(unused.hp,base.hp);assert.equal(unused.energy,base.energy);assert.equal(cards(unused)[0].paid,2);
const b=run('B','delegate',defs(3));assert.equal(b.hp,9);assert.equal(b.energy,0);assert.equal(b.records.filter(r=>r.kind==='hit')[0].loss,2);
const bd=run('B','direct',[['bash','B'],...defs(2)]);assert.equal(bd.hp,5);assert.equal(bd.hand.length,1);assert.equal(bd.enemies[0].hp,13);
const allDef=run('B','direct',defs(3));assert.equal(allDef.hp,9);assert.equal(allDef.energy,1);assert.equal(allDef.enemies[0].hp,13);assert.equal(allDef.enemies[1].hp,6);assert.equal(cards(allDef)[0].paid,0);assert.equal(cards(allDef)[1].paid,1);
const death=run('B','baseline',[['bash','A'],...defs(1)]);assert.equal(death.phase,'dead');assert.equal(death.hp,0);assert.equal(death.records.filter(r=>r.kind==='hit').length,1);assert.equal(death.records.at(-1).loss,12);
function unchanged(s,fn){const before=snapshot(s);assert.equal(fn(),false);assert.equal(snapshot(s),before);}
const invalid=M.create();unchanged(invalid,()=>M.play(invalid,'bash','B'));unchanged(invalid,()=>M.finish(invalid));unchanged(invalid,()=>M.respond(invalid,'text'));unchanged(invalid,()=>M.choose(invalid,'secret'));M.choose(invalid,'direct');unchanged(invalid,()=>M.choose(invalid,'delegate'));unchanged(invalid,()=>M.play(invalid,'bash','self'));unchanged(invalid,()=>M.play(invalid,'defend1','A'));M.play(invalid,'bash','B');unchanged(invalid,()=>M.play(invalid,'bash','A'));M.finish(invalid);unchanged(invalid,()=>M.finish(invalid));unchanged(invalid,()=>M.play(invalid,'defend1','self'));
const p=M.create();const before=snapshot(p),preview=M.preview(p);preview.card.cost=999;preview.target.hp=-1;assert.equal(snapshot(p),before);assert.equal(M.preview(p).paid,0);
const blank=M.copy(direct);assert.equal(M.respond(blank,'  '),true);assert.equal(blank.note,null);const withoutNote=snapshot(blank);assert.equal(M.respond(blank,'x'.repeat(500)),true);assert.equal(blank.note.length,400);blank.note=null;assert.equal(snapshot(blank),withoutNote);unchanged(blank,()=>M.respond(blank,42));
const original=M.create('B',true);unchanged(original,()=>M.choose(original,'delegate'));assert.equal(M.price(original,original.hand[0]),2);
const independent=M.create();independent.hand[0].cost=999;assert.equal(M.create().hand[0].cost,2);
let paths=0;
function explore(s,count){const end=M.copy(s);M.finish(end);paths++;assert.ok(end.hp>=0&&end.hp<=12);assert.ok(end.energy>=0&&end.energy<=3);assert.ok(end.enemies.every(e=>e.hp>=0));assert.equal(cards(end).reduce((v,c)=>v+c.paid,0)+end.energy,3);assert.equal(end.hand.length+cards(end).length,count);assert.ok(cards(end).filter(c=>c.automatic).length<=1);for(const r of end.records.filter(r=>r.kind==='hit'))assert.ok(r.loss>=0&&r.loss<=r.hpBefore);for(const o of M.options(s)){const next=M.copy(s);assert.equal(M.play(next,o.card,o.target),true);explore(next,count);}}
for(const which of['A','B']){for(const mode of['baseline','delegate','direct','none']){const s=M.create(which,mode==='baseline');const count=s.hand.length;if(mode!=='baseline')M.choose(s,mode);explore(s,count);}}
assert.ok(paths>100);console.log('统领方式纸面检查通过：具体目标/真实支付/基线/死亡停止/原子拒绝/权限与回应；穷举'+paths+'个合法行动前缀。不是哲学或乐趣认证。');
