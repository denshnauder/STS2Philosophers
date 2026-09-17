// Recompute only the fictional, fixed-hand D106 design case. No game APIs.
const assert = require('node:assert/strict');
const cards = {
  strike: { cost: 1, damage: 5, block: 0 },
  defend: { cost: 1, damage: 0, block: 5 },
  cut: { cost: 2, damage: 14, block: 0 },
  jab: { cost: 1, damage: 6, block: 0 },
  sweep: { cost: 1, damage: 3, block: 0 },
  guard: { cost: 1, damage: 0, block: 7 },
  riposte: { cost: 1, damage: 4, block: 4 },
  needle: { cost: 0, damage: 1, block: 0 },
};
const courses = {
  focus: ['cut', 'jab', 'sweep'],
  mixed: ['jab', 'guard', 'riposte'],
};
const ordinary = [['sweep', 'guard', 'needle'], ['sweep', 'needle', 'jab']];
function preferable(a, b) {
  return !b || a.gold > b.gold || a.gold === b.gold &&
    (a.hp > b.hp || a.hp === b.hp && a.turn < b.turn);
}
function battle(rewards, scenario) {
  const hand = ['strike', 'strike', 'defend', ...rewards];
  let best = null;
  // Six turns suffice for the documented witnesses. This is not an unlimited
  // solver or a real draw simulation. Focus cannot survive beyond turn two.
  const seen = new Set();
  function search(hp, enemy, turn, path) {
    if (turn > 6) return;
    const state = [hp, enemy, turn].join(':');
    if (seen.has(state)) return;
    seen.add(state);
    for (let mask = 0; mask < 2 ** hand.length; mask++) {
      const plays = hand.filter((_, i) => mask & 1 << i);
      if (plays.reduce((n, k) => n + cards[k].cost, 0) > 3) continue;
      let life = hp, target = enemy;
      const block = plays.reduce((n, k) => n + cards[k].block, 0);
      for (const key of plays) {
        const damage = cards[key].damage;
        if (!damage) continue;
        target -= damage;
        if (target <= 0) break;
        if (scenario === 'armored' && turn === 1) life -= 4;
        if (life <= 0) break;
      }
      if (life <= 0) continue;
      const nextPath = [...path, plays];
      if (target <= 0) {
        const result = { hp: life, gold: 12, turn, path: nextPath };
        if (preferable(result, best)) best = result;
        continue;
      }
      life -= Math.max(0, (turn === 1 ? 12 : 16) - block);
      if (life <= 0) continue;
      if (scenario === 'flee') {
        const result = { hp: life, gold: 0, turn, path: nextPath };
        if (preferable(result, best)) best = result;
      } else search(life, target, turn + 1, nextPath);
    }
  }
  search(10, scenario === 'flee' ? 18 : 24, 1, []);
  return best;
}
function bestIncome(offers, scenario) {
  let best = null;
  for (const a of [...offers[0], null]) for (const b of [...offers[1], null]) {
    const income = [a, b].filter(Boolean);
    const result = battle(income, scenario);
    if (result && preferable(result, best?.result)) best = { income, result };
  }
  return best;
}
assert.equal(battle(['jab', 'cut'], 'flee').gold, 12);
assert.equal(battle(['jab', 'cut'], 'flee').hp, 10);
for (const a of [...courses.focus, null]) for (const b of [...courses.focus, null]) {
  assert.equal(battle([a, b].filter(Boolean), 'armored'), null);
}
const mixedEscape = bestIncome([courses.mixed, courses.mixed], 'flee');
assert.equal(mixedEscape.result.gold, 0);
assert.equal(mixedEscape.result.hp, 10);
const mixedVictory = battle(['guard', 'riposte'], 'armored');
assert.equal(mixedVictory.hp, 10);
assert.equal(mixedVictory.gold, 12);
assert.equal(mixedVictory.turn, 5);
assert.equal(bestIncome(ordinary, 'flee').result.gold, 0);
const ordinaryVictory = battle(['guard', 'jab'], 'armored');
assert.equal(ordinaryVictory.hp, 2);
assert.equal(ordinaryVictory.turn, 3);
// A different ordinary input can contain tools excluded by either curriculum.
assert.equal(battle(['guard', 'cut'], 'flee').gold, 12);
assert.equal(battle(['guard', 'cut'], 'armored').hp, 6);
console.log('D106 fictional counterexample calculations passed.');
console.log(JSON.stringify({ mixedVictory, ordinaryVictory }, null, 2));
