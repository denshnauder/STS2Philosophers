// The fictional fixed-hand D113 experiment, not real battle/saves/philosophy.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const html=fs.readFileSync(path.resolve(__dirname,'../../docs/design/plato_guard_duty.html'),'utf8');
for(const script of html.matchAll(/<script>([\s\S]*?)<\/script>/g))new vm.Script(script[1]);
const context={};vm.runInNewContext(html.match(/\/\/ BEGIN PLATO GUARD DUTY PAPER MODEL([\s\S]*?)\/\/ END PLATO GUARD DUTY PAPER MODEL/)[1]+'\nglobalThis.paper=PlatoGuardDutyPaper;',context);
const m=context.paper,json=x=>JSON.stringify(x);let count=0;
const check=(name,run)=>{run();count++;console.log('PASS '+name);};
function unchanged(s,run){const before=json(s);assert.equal(run(),false);assert.equal(json(s),before);}
function open(fixture='normal',stance='unspoken'){const s=m.create(fixture);assert.equal(m.start(s,stance),true);return s;}
function camp(choice='none',fixture='normal'){const s=open(fixture);assert.equal(m.step(s,choice),true);assert.equal(m.step(s),true);assert.equal(s.phase,'maintenance');return s;}
function done(first,maintenance,second='none',fixture='normal'){const s=camp(first,fixture);assert.equal(m.maintain(s,maintenance),true);assert.equal(m.step(s,second),true);while(s.phase==='battle')assert.equal(m.step(s),true);return s;}
check('First attack plans have distinct actual bodies and a finite shared target',()=>{
 for(const [choice,hp,site]of [['none',11,3],['to-player',5,9],['to-site',12,0]]){const s=open();const before=json(s),f=m.forecast(s,choice);assert.equal(json(s),before);assert.equal(f.after.p,hp);assert.equal(f.after.site,site);assert.equal(f.hits.reduce((n,h)=>n+(h.target==='site'?h.loss:0),0),9-site);assert.equal(f.hits.reduce((n,h)=>n+(h.target==='player'?h.loss:0),0),12-hp);assert.equal(m.step(s,choice),true);assert.equal(s.p,hp);assert.equal(s.site,site);}
});
check('Early kill performs only actual strikes and no invented protection or arrival',()=>{
 const s=open();m.step(s);const f=m.forecast(s);assert.equal(f.attacks.length,1);assert.equal(f.blockGranted,0);assert.equal(f.hits.length,0);assert.equal(json(m.options(s)),json(['none']));unchanged(s,()=>m.step(s,'to-player'));m.step(s);assert.equal(s.records[1].attacks[0].loss,5);assert.equal(s.travelers[0].status,'crossed');assert.equal(s.travelers.length,1);
});
check('Refusal/unspoken/attempt changes recorded response without changing permissions or resources',()=>{
 const states=['unspoken','attempt','refuse'].map(x=>open('normal',x));for(const s of states)m.step(s,'to-player');const projected=s=>json({p:s.p,site:s.site,enemy:s.enemy,records:s.records,options:m.options(s)});assert.equal(projected(states[0]),projected(states[1]));assert.equal(projected(states[0]),projected(states[2]));assert.match(m.question(states[2]),/没有接受/);assert.match(m.question(states[1]),/愿意尝试/);
});
check('Normal maintenance matrix has real worker effort, caps, and private rest opportunity',()=>{
 for(const [first,choice,p,site,finalP,finalSite]of [['none','rest',12,5,9,1],['none','assist',11,9,8,5],['to-player','rest',9,9,6,5],['to-site','rest',12,0,5,0],['to-site','assist',12,6,9,2]]){const s=camp(first),before=json(s),f=m.maintenancePreview(s,choice);assert.equal(json(s),before);assert.equal(f.after.p,p);assert.equal(f.after.site,site);assert.equal(m.maintain(s,choice),true);assert.equal(s.p,p);assert.equal(s.site,site);m.step(s);m.step(s);assert.equal(s.p,finalP);assert.equal(s.site,finalSite);assert.equal(s.ending,'victory');}
});
check('Full site has no fictitious helper job; worker-alone cap does not create one either',()=>{
 const s=camp('to-player');assert.equal(m.maintenancePreview(s,'assist'),null);unchanged(s,()=>m.maintain(s,'assist'));for(const hp of [7,8]){const edge=camp();edge.site=hp;assert.equal(m.maintenancePreview(edge,'rest').after.site,9);unchanged(edge,()=>m.maintain(edge,'assist'));}
});
check('Full-health rebuilding forgone healing is zero and future helpers have not repaired anything',()=>{
 const s=camp('to-site');const f=m.maintenancePreview(s,'assist');assert.equal(f.restAvailable,0);assert.equal(f.restForgone,0);assert.equal(f.ownWork,0);assert.equal(f.helperWork,6);const rest=done('to-site','rest');assert.equal(rest.site,0);assert.equal(rest.travelers.filter(t=>t.status==='crossed').length,0);
});
check('Later rebuilding preserves an original waiting arrival and records actual delayed crossing',()=>{
 const s=camp('to-site'),original=json(s.records);assert.equal(s.records[2].traveler.status,'waiting');m.maintain(s,'assist');assert.equal(json(s.records.slice(0,3)),original);assert.equal(s.travelers[0].status,'crossed');assert.equal(s.travelers[0].waitPeriods,1);assert.equal(s.records[3].travelers[0].status,'crossed');assert.equal(s.records[2].traveler.waitPeriods,0);const snapshot=json(s.records[3]);m.step(s);m.step(s);assert.equal(json(s.records[3]),snapshot);assert.equal(s.travelers.length,2);assert.equal(s.travelers[1].waitPeriods,0);
});
check('Broken site has no redirection and next actual plan discloses both self attacks',()=>{
 const s=camp('to-site');m.maintain(s,'rest');assert.equal(json(m.plans(s).map(h=>h.original)),json(['player','player']));assert.equal(json(m.options(s)),json(['none']));unchanged(s,()=>m.step(s,'to-site'));unchanged(s,()=>m.step(s,'to-player'));
});
check('Destroyed-in-phase target does not silently retarget later hits; next phase does',()=>{
 const s=camp('none','hard');m.maintain(s,'rest');m.step(s);assert.equal(s.site,1);const f=m.forecast(s,'to-site');assert.equal(f.hits[0].loss,1);assert.equal(f.hits[1].executed,false);assert.equal(f.hits[1].reason,'site-lost');assert.equal(f.after.p,9);m.step(s,'to-site');assert.equal(s.site,0);assert.equal(s.p,9);assert.equal(s.enemy,6);
});
check('Hard second context retains a live facility or loses it with distinct actual journeys',()=>{
 for(const first of ['none','to-site']){const s=camp(first,'hard');m.maintain(s,first==='none'?'rest':'assist');m.step(s);m.step(s,'to-player');m.step(s);assert.equal(s.p,2);assert.equal(s.site,first==='none'?1:2);assert.equal(s.ending,'victory');assert.equal(s.travelers[0].waitPeriods,first==='none'?0:1);assert.equal(s.travelers[1].status,'crossed');}
});
check('Death clips actual loss, halts play, and never turns planned later arrival into observation',()=>{
 const s=camp('to-site','hard');m.maintain(s,'rest');m.step(s);assert.equal(s.p,5);m.step(s);assert.equal(s.p,0);assert.equal(s.phase,'dead');assert.equal(s.travelers.length,1);const last=s.records.at(-1);assert.equal(last.hits[1].loss,2);assert.equal(last.hits[1].hpAfter,0);assert.match(m.question(s),/后续计划到场未观测/);unchanged(s,()=>m.step(s));unchanged(s,()=>m.maintain(s,'assist'));
});
check('Detached previews cannot rewrite actual history or present travelers',()=>{
 const s=open(),before=json(s),f=m.forecast(s,'to-site');f.after.site=99;f.plan[0].target='elsewhere';assert.equal(json(s),before);const c=camp('to-site'),snap=json(c),v=m.maintenancePreview(c,'assist');v.travelers[0].status='imagined';v.before.site=999;assert.equal(json(c),snap);
});
check('Premature, invalid, duplicate, or post-ending actions preserve the whole state',()=>{
 const entry=m.create();unchanged(entry,()=>m.step(entry));unchanged(entry,()=>m.maintain(entry,'rest'));unchanged(entry,()=>m.start(entry,'good-person'));unchanged(entry,()=>m.finish(entry));const s=open();unchanged(s,()=>m.start(s));unchanged(s,()=>m.skip(s));unchanged(s,()=>m.step(s,'imagined'));const c=camp();unchanged(c,()=>m.maintain(c,'both'));const outcome=done('none','rest');unchanged(outcome,()=>m.step(outcome));assert.equal(m.finish(outcome),true);unchanged(outcome,()=>m.finish(outcome));unchanged(outcome,()=>m.maintain(outcome,'rest'));assert.throws(()=>m.create('unknown'),/Unsupported/);
});
check('Leaving before practice changes no resources/history and a free sentence has no material gate',()=>{
 const s=m.create();m.skip(s);assert.equal(s.p,12);assert.equal(s.site,9);assert.equal(s.records.length,0);assert.equal(s.stance,null);assert.match(m.question(s),/没有观察到/);const a=done('none','rest'),b=done('none','rest');const records=json(a.records);m.finish(a,'');m.finish(b,'<script>bad()</script>');assert.equal(a.note,null);assert.equal(b.note,'<script>bad()</script>');assert.equal(a.p,b.p);assert.equal(a.site,b.site);assert.equal(json(a.records),records);assert.equal(json(a.records),json(b.records));
});
check('All reachable decisions finish finitely and keep actual HP/blocked loss in bounds',()=>{
 let leaves=0;function walk(s){assert.ok(s.p>=0&&s.p<=12);assert.ok(s.site>=0&&s.site<=9);assert.ok(s.turn<=4);for(const r of s.records)if(r.kind==='turn')for(const h of r.hits)if(h.executed){assert.equal(h.hpBefore-h.hpAfter,h.loss);assert.ok(h.loss>=0);assert.ok(h.blocked>=0&&h.blocked<=5);}if(s.phase==='battle')for(const option of m.options(s)){const n=JSON.parse(json(s));m.step(n,option);walk(n);}else if(s.phase==='maintenance')for(const option of m.maintenanceOptions(s)){const n=JSON.parse(json(s));m.maintain(n,option);walk(n);}else {assert.ok(['outcome','dead'].includes(s.phase));leaves++;}}walk(open());walk(open('hard'));assert.ok(leaves>30);console.log('  '+leaves+' complete decision paths');
});
console.log(count+' fictional paper groups passed; no native game or design/fun acceptance.');
