// Fictional teaching/allocation loop, not human learning, virtue, saves or balance.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const html=fs.readFileSync(path.resolve(__dirname,'../../docs/design/protagoras_teaching.html'),'utf8');
for(const m of html.matchAll(/<script>([\s\S]*?)<\/script>/g))new vm.Script(m[1]);
const context={};vm.runInNewContext(html.match(/\/\/ BEGIN PROTAGORAS TEACHING PAPER MODEL([\s\S]*?)\/\/ END PROTAGORAS TEACHING PAPER MODEL/)[1]+'\nglobalThis.paper=ProtagorasTeachingPaper;',context);
const p=context.paper,json=x=>JSON.stringify(x);let total=0;
const check=(name,run)=>{run();total++;console.log('PASS '+name);};
function unchanged(s,run){const before=json(s);assert.equal(run(),false);assert.equal(json(s),before);}
function open(rule='lowest',kind='method',fixture='newcomer'){const s=p.create(fixture);assert.equal(p.first(s,rule),true);assert.equal(p.teach(s,kind),true);return s;}
check('Three first allocations conserve supplies and show actual capped healing',()=>{
  for(const [key,a,hp]of[['contribution',[2,0,1],[10,2,6]],['equal',[1,1,1],[8,4,6]],['lowest',[0,2,1],[6,6,6]]]){const s=p.create();assert.equal(p.first(s,key),true);assert.equal(json(s.records[0].allocation),json(a));assert.equal(json(s.party.map(x=>x.hp)),json(hp));assert.equal(s.records[0].allocation.reduce((n,x)=>n+x,0),3);}
});
check('Sample and method produce distinct second allocations from the same first result',()=>{
  const a=open('lowest','sample'),b=open('lowest','method');assert.equal(json(a.records),json(b.records));assert.equal(json(a.party.map(x=>x.hp)),json([2,1,4]));
  assert.equal(json(p.preview(a).allocation),json([0,2,1]));assert.equal(json(p.preview(b).allocation),json([1,2,0]));assert.equal(p.preview(a).finalHp,4);assert.equal(p.preview(b).finalHp,6);
  assert.equal(p.execute(a),true);assert.equal(p.execute(b),true);assert.equal(a.party[0].hp,4);assert.equal(b.party[0].hp,6);assert.equal(a.records[1].actor,'learner');
});
check('Taking back control gives actual allocation rights and forgoes capped rest',()=>{
  const s=open('lowest','sample');assert.equal(p.preview(s,'contribution').finalHp,6);assert.equal(p.preview(s,'contribution').restForgone,2);assert.equal(p.execute(s,'contribution'),true);assert.equal(s.party[0].hp,6);assert.equal(s.records[1].actor,'player');assert.equal(s.records[1].restGain,0);assert.match(p.question(s),/你自己的分配/);
  const capped=open('contribution','method');assert.equal(p.preview(capped).restGain,0);assert.equal(p.preview(capped,'contribution').restForgone,0);assert.equal(p.execute(capped,'contribution'),true);assert.equal(capped.party[0].hp,10);
});
check('Equal shares have identical material with distinct explicit instructions',()=>{
  const a=open('equal','sample'),b=open('equal','method');assert.equal(json(p.preview(a)),json(p.preview(b)));assert.notEqual(json(a.teaching),json(b.teaching));assert.equal(p.preview(a).available,false);p.adopt(a);p.adopt(b);assert.equal(a.party[0].hp,b.party[0].hp);assert.notEqual(json(a.records[1].teaching),json(b.records[1].teaching));
});
check('Rotating contributors changes method, not a remembered seat example',()=>{
  const a=open('contribution','sample','rotation'),b=open('contribution','method','rotation');assert.equal(json(p.preview(a).allocation),json([2,0,1]));assert.equal(json(p.preview(b).allocation),json([0,2,1]));assert.equal(p.preview(a).after[0].hp,10);assert.equal(p.preview(a).finalHp,null);unchanged(a,()=>p.execute(a));assert.equal(p.preview(b).finalHp,8);assert.equal(p.execute(b),true);
});
check('Self use compares identical inputs, travel and rest without recorded instructions or moral rewards',()=>{
  for(const fixture of ['newcomer','rotation']){const a=p.create(fixture),b=open('contribution','method',fixture);assert.equal(p.baseline(a),true);assert.equal(p.execute(b,p.preview(b).available?null:'contribution'),true);assert.equal(json(a.party),json(b.party));assert.equal(a.teaching,null);assert.equal(json(a.travel),json(b.travel));assert.match(p.question(a),/没有指定后续操作指令/);assert.equal('virtueScore' in a,false);assert.equal('learnedVirtue' in a,false);}
});
check('First party remains a snapshot; newcomer cannot overwrite departed participant facts',()=>{
  const s=p.create();p.first(s,'lowest');const first=json(s.records[0]);p.teach(s,'method');assert.equal(s.travel.departed.id,'wounded');assert.equal(s.travel.departed.hp,6);assert.equal(s.party[1].id,'arrival');assert.equal(s.party[1].hp,1);p.execute(s);assert.equal(json(s.records[0]),first);s.party[1].hp=10;assert.equal(s.records[1].after[1].hp,5);assert.equal(s.travel.departed.hp,6);
});
check('Previews are read only, including detached arrays returned to callers',()=>{
  const s=open(),before=json(s);for(let n=0;n<3;n++)p.preview(s);assert.equal(json(s),before);const r=p.preview(s);r.before[0].hp=10;r.after[1].hp=10;r.allocation[0]=3;assert.equal(json(s),before);
});
check('Premature, invalid and repeated confirmations leave all records unchanged',()=>{
  const s=p.create();unchanged(s,()=>p.teach(s,'method'));unchanged(s,()=>p.execute(s));unchanged(s,()=>p.adopt(s));unchanged(s,()=>p.endShared(s));unchanged(s,()=>p.finish(s));unchanged(s,()=>p.first(s,'__proto__'));p.first(s,'equal');unchanged(s,()=>p.first(s,'lowest'));unchanged(s,()=>p.baseline(s));unchanged(s,()=>p.skip(s));unchanged(s,()=>p.teach(s,'other'));p.teach(s,'method');unchanged(s,()=>p.teach(s,'sample'));unchanged(s,()=>p.execute(s,'other'));p.adopt(s);unchanged(s,()=>p.execute(s));unchanged(s,()=>p.adopt(s));unchanged(s,()=>p.endShared(s));unchanged(s,()=>p.finish(s,42));unchanged(s,()=>p.finish(s,'x'.repeat(301)));p.finish(s);unchanged(s,()=>p.finish(s,'改写已结束的记录'));
});
check('Unsupported parties and allocations do not swallow supplies or fabricate recovery',()=>{
  for(const a of [[3,0,1],[-1,3,1],[1,1,0],[1.5,0.5,1],null])assert.equal(p.distribute(p.create().party,a),null);
  for(const bad of [0,-1,11,2.5]){const s=p.create();s.party[0].hp=bad;unchanged(s,()=>p.first(s,'equal'));unchanged(s,()=>p.baseline(s));}
  const s=p.create();s.party[0].contribution=3;unchanged(s,()=>p.first(s,'equal'));
});
check('Leaving records no explicit trial; blank or expressed notes do not affect settled resources',()=>{
  const a=p.create();assert.equal(p.skip(a),true);assert.equal(a.party[0].hp,6);assert.equal(a.records.length,0);assert.equal(p.question(a),'');unchanged(a,()=>p.baseline(a));
  const b=open(),c=open();p.execute(b);p.execute(c);assert.equal(p.finish(b),true);assert.equal(p.finish(c,'<script>未执行</script>'),true);assert.equal(b.note,null);assert.equal(c.note,'<script>未执行</script>');assert.equal(json(b.party),json(c.party));assert.equal(json(b.records),json(c.records));
});
check('No explicit instruction is not a claim that everyday education is absent',()=>{
  const s=p.create();p.baseline(s);assert.match(p.question(s),/没有指定后续操作指令/);assert.match(p.question(s),/日常相处仍可能有教育/);assert.match(p.question(s),/没追踪那些过程/);assert.equal(s.teaching,null);assert.equal('everydayLearning' in s,false);assert.equal('virtueImproved' in s,false);
});
check('All fixture/instruction/control paths spend six supplies once and retain valid life',()=>{
  for(const fixture of ['newcomer','rotation'])for(const key of Object.keys(p.rules))for(const kind of ['sample','method'])for(const override of [null,...Object.keys(p.rules)]){
    const s=open(key,kind,fixture);if(override===null&&!p.preview(s).available){unchanged(s,()=>p.execute(s));assert.equal(p.adopt(s),true);}else assert.equal(p.execute(s,override),true);assert.equal(s.records.reduce((n,r)=>n+r.allocation.reduce((m,x)=>m+x,0),0),6);assert.equal(s.records.length,2);assert.ok(s.party.every(x=>x.hp>0&&x.hp<=10));unchanged(s,()=>p.execute(s,override));assert.equal(p.finish(s),true);
  }
});
check('Can compute an instruction but refuse to execute it; refusal never invents observed healing',()=>{
  const s=open('equal','method'),r=p.preview(s);assert.equal(json(r.allocation),json([1,1,1]));assert.equal(r.available,false);assert.equal(r.finalHp,null);assert.equal(s.discussion.originalWilling,false);unchanged(s,()=>p.execute(s));assert.equal(s.records.length,1);assert.equal(json(s.party.map(x=>x.hp)),json([4,1,4]));assert.equal(p.execute(s,'equal'),true);assert.equal(s.records[1].actor,'player');assert.equal(s.records[1].restGain,0);
});
check('Adopting a local counterproposal preserves original instructions and frozen voices',()=>{
  const s=open('contribution','sample'),teaching=json(s.teaching);assert.equal(s.discussion.originalWilling,false);assert.equal(p.adopt(s),true);assert.equal(s.records[1].selection,'counterproposal');assert.equal(s.records[1].actor,'learner');assert.equal(s.records[1].allocation[1],2);assert.equal(json(s.teaching),teaching);assert.equal(json(s.records[1].teaching),teaching);const voices=json(s.records[1].discussion);s.discussion.originalAllocation[0]=0;s.discussion.arrival='后来不同的话';assert.equal(json(s.records[1].discussion),voices);assert.match(p.question(s),/原教学指令没有被改写/);assert.equal('agreedJustice' in s,false);
});
check('Already suitable original proposals remain executable without forced norm change',()=>{
  const s=open('contribution','method','rotation'),teaching=json(s.teaching);assert.equal(p.preview(s).available,true);assert.equal(p.execute(s),true);assert.equal(s.records[1].selection,'original');assert.equal(json(s.teaching),teaching);assert.equal(s.records[1].restGain,2);assert.equal(s.party[0].hp,8);
});
check('Ending shared distribution matches same-input self use without resetting prior costs',()=>{
  for(const fixture of ['newcomer','rotation'])for(const kind of ['sample','method']){const a=open('contribution',kind,fixture),b=p.create(fixture),old=json(a.records[0]);p.baseline(b);assert.equal(p.endShared(a),true);assert.equal(json(a.party),json(b.party));assert.equal(json(a.records[0]),old);assert.equal(json(a.travel),json(b.travel));assert.equal(a.records[1].selection,'self-use');assert.equal(a.records[1].actor,'each');assert.ok(a.teaching);assert.match(p.question(a),/不等于没人能学会/);unchanged(a,()=>p.endShared(a));}
});
console.log(`PASS ${total} paper checks; no conclusion about human learning, virtue, native safety or fun.`);
