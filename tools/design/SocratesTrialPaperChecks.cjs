// Authored paper loop; does not test native card identity, saves, draw odds or balance.
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const html=fs.readFileSync(path.resolve(__dirname,'../../docs/design/socrates_trial.html'),'utf8');
for(const match of html.matchAll(/<script>([\s\S]*?)<\/script>/g))new vm.Script(match[1]);
const code=html.match(/\/\/ BEGIN SOCRATES TRIAL PAPER MODEL([\s\S]*?)\/\/ END SOCRATES TRIAL PAPER MODEL/)[1];
const context={};vm.runInNewContext(code+'\nglobalThis.paper=SocratesTrialPaper;',context);
const p=context.paper;
let total=0;
const check=(name,run)=>{run();total++;console.log('PASS '+name);};
const snapshot=s=>JSON.stringify(s);
function unchanged(s,run){const before=snapshot(s);assert.equal(run(),false);assert.equal(snapshot(s),before);}
function open(fixture='one',index=0,reason='只检查这场的局部用途'){
  const s=p.create(fixture);assert.equal(p.trial(s,index,reason),true);const id=s.pending;assert.equal(p.start(s),true);return {s,id};
}
function victoryWithoutTrial(s){
  let count=0;while(s.phase==='combat'){
    const alive=s.enemies.find(e=>e.hp>0);assert.equal(p.play(s,'base:strike',alive.id),true);
    if(s.phase==='combat'){assert.equal(p.play(s,'base:defend'),true);assert.equal(p.endTurn(s),true);}
    assert.ok(++count<10);
  }assert.equal(s.phase,s.battleKind==='trial'?'review':'outcome');
}
check('Ordinary accept and skip preserve unused opportunity; card reward settles once',()=>{
  for(const choice of ['accept','skip']){const s=p.create();assert.equal(choice==='accept'?p.accept(s,1):p.skip(s),true);assert.equal(s.spent.length,0);assert.equal(s.deck.length,choice==='accept'?3:2);unchanged(s,()=>p.skip(s));unchanged(s,()=>p.nextReward(s));assert.equal(p.start(s),true);victoryWithoutTrial(s);assert.equal(p.nextReward(s),true);assert.equal(p.canTry(s),true);}
});
check('Invalid reward, reason and premature actions leave the whole loop unchanged',()=>{
  const s=p.create();for(const index of [-1,3,0.5,'0'])unchanged(s,()=>p.trial(s,index));
  for(const reason of ['',42,'x'.repeat(301)])unchanged(s,()=>p.trial(s,0,reason));
  unchanged(s,()=>p.start(s));unchanged(s,()=>p.resolve(s,'keep'));unchanged(s,()=>p.nextAct(s));
});
check('Trial locks one copy and reward; no second pending trial or favourable battle swap',()=>{
  const s=p.create();assert.equal(p.trial(s,0,null),true);assert.equal(s.reason,null);assert.equal(s.deck.length,3);assert.equal(s.reward.length,0);assert.equal(s.spent.join(','),'1');
  unchanged(s,()=>p.trial(s,1));unchanged(s,()=>p.accept(s,1));unchanged(s,()=>p.nextReward(s));unchanged(s,()=>p.nextAct(s));assert.equal(p.start(s),true);unchanged(s,()=>p.start(s));
});
check('Actual use on one target supports only a local observed example',()=>{
  const {s,id}=open();assert.equal(p.play(s,id,'foe:0'),true);assert.equal(s.phase,'review');assert.equal(s.material.drawn,true);assert.equal(s.material.used,true);assert.equal(s.material.hits.length,2);assert.equal(s.material.hits.reduce((n,h)=>n+h.damage,0),8);assert.equal(s.currentReason,s.reason);
  assert.equal('knowledgeScore' in s,false);assert.equal('refuted' in s.material,false);
});
check('Two hit effect is not two enemies or two actual hits on an already dead foe',()=>{
  const {s,id}=open('two');assert.equal(p.play(s,id,'foe:0'),true);assert.equal(s.phase,'combat');assert.equal(s.material.hits.length,1);assert.equal(s.enemies[1].hp,4);unchanged(s,()=>p.play(s,id,'foe:1'));assert.equal(p.play(s,'base:strike','foe:1'),true);assert.equal(s.phase,'review');
});
check('Drawn but unused and never drawn remain distinct, without automatic refutation',()=>{
  for(const fixture of ['one','absent']){const {s}=open(fixture);victoryWithoutTrial(s);assert.equal(s.material.drawn,fixture==='one');assert.equal(s.material.used,false);assert.equal(s.material.hits.length,0);assert.equal(s.currentReason,s.reason);assert.equal(s.response,null);}
});
check('Defence evidence records grant and aggregate damage without claiming unique causality',()=>{
  const {s,id}=open('one',1);assert.equal(p.play(s,id),true);assert.equal(p.play(s,'base:defend'),true);assert.equal(p.endTurn(s),true);assert.equal(s.material.blockGranted,8);assert.equal(s.material.turns[0].blocked,6);assert.equal(s.hp,20);assert.equal('damagePreventedByTrial' in s.material,false);victoryWithoutTrial(s);
});
check('All explicit replies, no assertion and no reply have identical card decision rights',()=>{
  for(const reason of [null,'只检查这场'])for(const type of [null,'keep','limit','withdraw','unanswered'])for(const decision of ['keep','reject']){
    const {s,id}=open('one',0,reason);p.play(s,id,'foe:0');if(type!==null)assert.equal(p.reply(s,type,'限定为这一个已见局面'),true);const original=s.deck.find(c=>c.id===id);
    assert.equal(p.resolve(s,decision),true);assert.equal(s.pending,null);assert.equal(s.deck.length,decision==='keep'?3:2);assert.equal(s.spent.join(','),'1');assert.equal(s.trialHistory[0].response===null,type===null);if(decision==='keep')assert.equal(s.deck.find(c=>c.id===id),original);
  }
});
check('Reply is explicit, bounded and cannot revise history by repeated callbacks',()=>{
  const {s,id}=open();p.play(s,id,'foe:0');unchanged(s,()=>p.reply(s,'other'));unchanged(s,()=>p.reply(s,'limit',''));assert.equal(p.reply(s,'withdraw'),true);assert.equal(s.currentReason,null);assert.notEqual(s.reason,null);unchanged(s,()=>p.reply(s,'keep'));p.resolve(s,'reject');unchanged(s,()=>p.resolve(s,'keep'));unchanged(s,()=>p.resolve(s,'reject'));
});
check('Reject removes the exact simulated copy, preserves an identical extra copy, no alternatives',()=>{
  const {s,id}=open();s.deck.push({id:'extra:twin',model:'twin'});p.play(s,id,'foe:0');assert.equal(p.resolve(s,'reject'),true);assert.ok(s.deck.some(c=>c.id==='extra:twin'));assert.equal(s.deck.some(c=>c.id===id),false);assert.equal(s.deck.some(c=>['guard','heavy'].includes(c.model)),false);
});
check('Missing or ambiguous pending identity refuses auto settlement; no similar substitute',()=>{
  for(const corruption of ['missing','duplicate']){const {s,id}=open();p.play(s,id,'foe:0');s.deck.push({id:'extra:twin',model:'twin'});if(corruption==='missing')s.deck=s.deck.filter(c=>c.id!==id);else s.deck.push({...s.deck.find(c=>c.id===id)});for(const decision of ['keep','reject'])unchanged(s,()=>p.resolve(s,decision));}
});
check('Invalid, duplicate and costly combat actions do not mint evidence or spend energy',()=>{
  const {s,id}=open('two',2);unchanged(s,()=>p.play(s,id,'missing'));assert.equal(p.play(s,'base:defend'),true);assert.equal(p.play(s,'base:strike','foe:0'),true);unchanged(s,()=>p.play(s,id,'foe:0'));unchanged(s,()=>p.play(s,id,'foe:1'));unchanged(s,()=>p.play(s,'base:strike','foe:1'));assert.equal(s.material.used,false);
});
check('Normal later rewards do not refund spent inquiry; only next act renews access',()=>{
  const {s,id}=open();p.play(s,id,'foe:0');p.resolve(s,'reject');assert.equal(p.nextReward(s),true);assert.equal(p.canTry(s),false);unchanged(s,()=>p.trial(s,0));p.accept(s,0);assert.match(s.result,/仍已用尽/);assert.equal(p.start(s),true);assert.equal(p.play(s,s.focus,'foe:0'),true);assert.equal(p.nextAct(s),true);assert.equal(s.act,2);assert.equal(p.canTry(s),true);assert.equal(s.trialHistory.length,1);assert.equal(p.trial(s,1,null),true);assert.equal(s.spent.join(','),'1,2');
});
check('Death denies review, rewards, advancing and resurrection; reset is a new simulation',()=>{
  const {s}=open('two');assert.equal(p.endTurn(s),true);assert.equal(p.endTurn(s),true);assert.equal(s.phase,'dead');assert.equal(s.hp,0);for(const action of [()=>p.resolve(s,'keep'),()=>p.reply(s,'keep'),()=>p.nextReward(s),()=>p.nextAct(s),()=>p.start(s)])unchanged(s,action);assert.equal(s.trialHistory.length,0);assert.equal(p.create().pending,null);assert.equal(p.create().spent.length,0);
});
check('Matching ordinary and trial action prefixes give identical combat facts, not identical rights',()=>{
  for(const fixture of ['one','two','absent'])for(const mode of ['use','unused']){
    const ordinary=p.create(fixture),trial=p.create(fixture);p.accept(ordinary,0);p.trial(trial,0,null);
    for(const s of [ordinary,trial]){p.start(s);if(mode==='use'&&fixture!=='absent')p.play(s,s.focus,'foe:0');if(s.phase==='combat')victoryWithoutTrial(s);}
    assert.equal(ordinary.hp,trial.hp);assert.equal(ordinary.energy,trial.energy);assert.equal(snapshot(ordinary.enemies),snapshot(trial.enemies));assert.equal(snapshot(ordinary.material),snapshot(trial.material));assert.equal(ordinary.phase,'outcome');assert.equal(trial.phase,'review');assert.equal(ordinary.spent.length,0);assert.equal(trial.spent.join(','),'1');unchanged(ordinary,()=>p.resolve(ordinary,'reject'));unchanged(ordinary,()=>p.reply(ordinary,'withdraw'));assert.equal(p.resolve(trial,'keep'),true);assert.equal(snapshot(ordinary.deck),snapshot(trial.deck));
  }
});
check('Reject is an extra option after the same battle, not proof or a choice of the original alternatives',()=>{
  const ordinary=p.create(),trial=p.create();p.accept(ordinary,0);p.trial(trial,0,null);for(const s of [ordinary,trial]){p.start(s);p.play(s,s.focus,'foe:0');}assert.equal(ordinary.hp,trial.hp);p.resolve(trial,'reject');assert.equal(ordinary.deck.length,3);assert.equal(trial.deck.length,2);assert.equal(trial.reward.length,0);assert.equal(trial.trialHistory[0].response,null);
});
check('Skipped reward faces the same enemies and scripted basic hand without a new card',()=>{
  const s=p.create('two');p.skip(s);p.start(s);assert.equal(s.hand.join(','),'base:strike,base:defend');assert.equal(s.material.drawn,false);assert.equal(s.pending,null);victoryWithoutTrial(s);assert.equal(s.spent.length,0);assert.equal(s.trialHistory.length,0);assert.equal(s.deck.length,2);
});
check('An ordinary missing copy cannot be replaced by a same model; explicit mode stays consistent',()=>{
  const s=p.create();p.accept(s,0);s.deck=s.deck.filter(c=>c.id!==s.focus);s.deck.push({id:'replacement:twin',model:'twin'});unchanged(s,()=>p.start(s));const t=p.create();p.trial(t,0,null);t.pending=null;unchanged(t,()=>p.start(t));
});
check('Follow-up questions distinguish explicit handling without certifying free-text content',()=>{
  const keys=new Set();
  for(const reason of [null,'原句'])for(const response of [null,'keep','limit','withdraw','unanswered'])for(const decision of ['keep','reject']){
    const {s,id}=open('one',0,reason);p.play(s,id,'foe:0');if(response!==null)p.reply(s,response,'所有情况下都最好');p.resolve(s,decision);
    const before=snapshot(s),q=p.inquiry(s);assert.equal(snapshot(s),before);keys.add(q.key);assert.equal(q.originalReason,reason);assert.equal(q.decision,decision);
    const expected=reason===null?(response==='limit'?'introduced':'notProposed'):response==='withdraw'?'withdrawn':response==='limit'?'rewritten':response==='keep'?'kept':response==='unanswered'?'deferred':'unanswered';assert.equal(q.key,expected);
    if(response==='limit'){assert.equal(q.currentReason,'所有情况下都最好');assert.match(q.question,/新材料|没有认证/);}
  }assert.equal(keys.size,7);
});
check('Prior questions survive ordinary choices but never submit beliefs for a new copy',()=>{
  const {s,id}=open('one',0,'只指这次局部事实');p.play(s,id,'foe:0');p.reply(s,'keep');p.resolve(s,'reject');const prior=snapshot(p.inquiry(s));p.nextReward(s);assert.equal(s.reason,null);p.accept(s,1);p.start(s);victoryWithoutTrial(s);assert.equal(snapshot(p.inquiry(s)),prior);p.nextAct(s);p.trial(s,0,null);assert.notEqual(s.pending,id);assert.equal(s.reason,null);assert.equal(s.currentReason,null);assert.equal(s.trialHistory[0].decision,'reject');
});
check('Follow-up view and later materials cannot rewrite a completed observation',()=>{
  const {s,id}=open();p.play(s,id,'foe:0');p.resolve(s,'keep');const original=snapshot(s.trialHistory[0]);s.material.hits[0].damage=99;assert.equal(snapshot(s.trialHistory[0]),original);const q=p.inquiry(s);assert.equal(Object.isFrozen(q),true);assert.equal(Object.isFrozen(q.facts),true);assert.throws(()=>q.facts.push('invented'));assert.equal(snapshot(s.trialHistory[0]),original);
});
check('Continuous authored fragments change relevant facts, not claims or card decision rights',()=>{
  const s=p.create('sequence');const ids=[];
  for(let act=1;act<=3;act++){
    assert.equal(p.trial(s,0,act===1?'这张在该局面合用':null),true);ids.push(s.pending);assert.equal(p.start(s),true);assert.equal(s.material.fixture,['one','two','absent'][act-1]);
    if(act<3){p.play(s,s.pending,'foe:0');if(s.phase==='combat')p.play(s,'base:strike','foe:1');}else victoryWithoutTrial(s);
    assert.equal(s.phase,'review');assert.equal(s.material.hits.length,[2,1,0][act-1]);assert.equal(s.material.drawn,act<3);assert.equal('refuted' in s.material,false);assert.equal(s.reason,act===1?'这张在该局面合用':null);if(act===1)p.reply(s,'keep');assert.equal(p.resolve(s,'keep'),true);if(act<3)assert.equal(p.nextAct(s),true);
  }
  assert.equal(new Set(ids).size,3);assert.equal(s.trialHistory.map(r=>r.material.hits.length).join(','),'2,1,0');assert.equal(s.trialHistory[0].currentReason,'这张在该局面合用');assert.equal(s.spent.join(','),'1,2,3');unchanged(s,()=>p.nextAct(s));
});
check('Fixed sequence cannot advance with a pending copy or mint questions from ordinary choices',()=>{
  const s=p.create('sequence');p.accept(s,0);p.start(s);p.play(s,s.focus,'foe:0');assert.equal(p.inquiry(s),null);p.nextAct(s);p.trial(s,0,'第二段');unchanged(s,()=>p.nextAct(s));p.start(s);assert.equal(s.enemies.length,2);unchanged(s,()=>p.nextAct(s));
});
check('Material questions separate no draw from no use without inventing a reason',()=>{
  const questions=[];
  for(const fixture of ['one','absent']){const {s}=open(fixture);victoryWithoutTrial(s);p.resolve(s,'keep');const before=snapshot(s),q=p.inquiry(s);assert.equal(snapshot(s),before);questions.push(q.materialQuestion);assert.equal(q.key,'unanswered');assert.match(q.materialQuestion,/当前句/);}
  assert.match(questions[0],/抽到却未使用.*不能由程序代答/);assert.match(questions[1],/未抽到.*没有它的使用材料/);assert.notEqual(questions[0],questions[1]);
});
check('Actual target questions never promote printed two hits into two observed targets',()=>{
  for(const fixture of ['one','two']){const {s,id}=open(fixture,0,'只问这次对一个目标');p.play(s,id,'foe:0');if(s.phase==='combat')p.play(s,'base:strike','foe:1');p.reply(s,'keep');p.resolve(s,'reject');const q=p.inquiry(s);assert.match(q.materialQuestion,new RegExp(`实际${fixture==='one'?2:1}次命中，只涉及1个目标`));assert.match(q.materialQuestion,/不等于实际处理了两个目标/);assert.equal(q.currentReason,'只问这次对一个目标');}
});
check('No current assertion is not silently reintroduced by a concrete material question',()=>{
  for(const mode of ['silent','withdraw']){const {s,id}=open('one',0,mode==='silent'?null:'原句');p.play(s,id,'foe:0');if(mode==='withdraw')p.reply(s,'withdraw');p.resolve(s,'keep');const q=p.inquiry(s);assert.equal(q.currentReason,null);assert.match(q.materialQuestion,/若要提出一个新问题/);assert.doesNotMatch(q.materialQuestion,/当前句|你仍认为/);}
});
check('Block and attack observations keep aggregate causality and long-term comparisons open',()=>{
  const guard=open('one',1);p.play(guard.s,guard.id);victoryWithoutTrial(guard.s);p.resolve(guard.s,'keep');assert.match(p.inquiry(guard.s).materialQuestion,/给了8格挡.*不能独归/);
  const heavy=open('one',2);p.play(heavy.s,heavy.id,'foe:0');p.resolve(heavy.s,'keep');assert.match(p.inquiry(heavy.s).materialQuestion,/命中1个目标，记录伤害8.*没有给出长期比较/);
});
check('Material questions derive from the completed copy and cannot mutate its historical facts',()=>{
  const {s,id}=open();p.play(s,id,'foe:0');p.resolve(s,'keep');const before=snapshot(s.trialHistory),view=snapshot(p.inquiry(s));s.material.hits.length=0;s.material.drawn=false;assert.equal(snapshot(p.inquiry(s)),view);p.nextReward(s);p.accept(s,1);p.start(s);victoryWithoutTrial(s);assert.equal(snapshot(p.inquiry(s)),view);assert.equal(snapshot(s.trialHistory),before);assert.equal(Object.isFrozen(p.inquiry(s)),true);
});
check('Same victory in nine HP fixture is not evidence of a single-card kill or refuted local claim',()=>{
  for(const mode of ['ordinary','trial']){const s=p.create('nine');if(mode==='trial')p.trial(s,0,'仅原8生命条件合用');else p.accept(s,0);p.start(s);p.play(s,s.focus,'foe:0');assert.equal(s.enemies[0].hp,1);assert.equal(s.phase,'combat');assert.equal(s.material.hits.length,2);assert.equal(s.material.hits.reduce((n,h)=>n+h.damage,0),8);p.play(s,'base:strike','foe:0');assert.equal(s.hp,20);assert.equal(s.phase,mode==='trial'?'review':'outcome');if(mode==='trial'){p.reply(s,'keep');p.resolve(s,'keep');const q=p.inquiry(s);assert.equal(q.currentReason,'仅原8生命条件合用');assert.equal(q.fixture,'nine');assert.equal('refuted' in q,false);assert.equal('knows' in q,false);}else assert.equal(p.inquiry(s),null);}
});
console.log(`PASS ${total} authored paper checks; native save/identity and gameplay acceptance not exercised.`);
