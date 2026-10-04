// Echo's deflect spin stuns: enemies the twirling staff touches are stunned (light ones longer than heavy),
// once per spin; enemies out of reach, enemies already mid-blow (left to the parry), bosses and other characters' parries are left alone.
import { World, spinStunTicks } from '../game/js/world.js';
import { createEnemy } from '../game/js/enemies.js';
import { SETTINGS, DEFLECT } from '../game/js/config.js';
SETTINGS.novaKit = 'sentinel'; SETTINGS.echoKit = 'hunter'; SETTINGS.lockOn = false; SETTINGS.difficulty = 'normal';

const BT = ['jump', 'dash', 'melee', 'fire', 'parry', 'sig', 'mode', 'lock', 'sub', 'ult'];
const assert = (c, m) => { console.log((c ? 'PASS ' : 'FAIL ') + m); if (!c) process.exitCode = 1; };
function setup(char = 'echo') {
  const w = new World(); w.enemies = []; w.towerSpawned = true; w.arena.state = 'cleared';
  const p = w.addPlayer('t0', char); p.x = 30; p.y = 0; p.prevX = 30; p.prevY = 0;
  let prev = {}; const log = [];
  const run = (o = {}, n = 1) => {
    for (let i = 0; i < n; i++) {
      const held = {}, pressed = {}, released = {};
      for (const b of BT) { held[b] = !!(o.held && o.held[b]); pressed[b] = held[b] && !prev[b]; released[b] = !held[b] && !!prev[b]; }
      prev = held; w.step({ [p.slot]: { mx: 0, my: 0, aimFree: false, ax: 0, ay: 0, held, pressed, released } });
      log.push(...w.events); w.events.length = 0;
    }
  };
  run({}, 10);
  const add = (type, dx, extra = {}) => { const e = createEnemy(type, p.x + dx, 0, { cd: 999, ...extra }); w.enemies.push(e); return e; };
  return { w, p, run, log, add };
}
const spin = run => { run({ held: { parry: true } }, 1); run({}, 4); };

{ // Light and heavy enemies beside him are stunned; one out of reach is not
  const { p, run, log, add } = setup();
  const near = add('swarmer', 1.2), behind = add('shield', -1.3), far = add('swarmer', 4);
  spin(run);
  const stunned = log.filter(e => e.type === 'spinStun').map(e => e.e);
  assert(p.state === 'parry', 'Echo is spinning (parry)');
  assert(near.state === 'hitstun' && near.stun === 51, `A light enemy in front is stunned for ${near.stun} ticks`);
  assert(behind.state === 'hitstun' && stunned.includes(behind), 'An enemy behind him is stunned too (the spin goes all round)');
  assert(far.state !== 'hitstun' && !stunned.includes(far), 'An enemy 4 m away is not touched');
}
{ // Heavy enemies for less; once per spin; the stun holds them still, then wears off
  const { run, log, add } = setup();
  const brute = add('brute', 1.6);
  spin(run); run({}, 10);
  const n = log.filter(e => e.type === 'spinStun').length;
  assert(n === 1 && brute.stun === 27, `A brute is stunned once per spin, for ${brute.stun} ticks`);
  run({}, 27);
  assert(brute.state !== 'hitstun', 'The stun wears off');
}
{ // Bosses shrug it off; a snared enemy keeps its root; Nova's parry doesn't stun
  const { run, log, add } = setup();
  const boss = add('swarmer', 1.2, { boss: true }), rooted = add('swarmer', -1.2), post = add('post', 0.9);
  rooted.state = 'snared'; rooted.st = 0; rooted.stun = 96;
  spin(run);
  assert(!log.some(e => e.type === 'spinStun'), 'A boss is not stunned, a snared enemy stays snared, and the drill post keeps swinging');
  const A = setup(), mid = A.add('swarmer', 1.2); mid.state = 'attack'; mid.st = 0; mid.atk = { inst: 1 };
  spin(A.run);
  assert(!A.log.some(q => q.type === 'spinStun' && q.e === mid), 'An enemy whose blow is already coming is left to the parry');
  const N = setup('nova'), s = N.add('swarmer', 1.2);
  spin(N.run);
  assert(N.p.state === 'parry' && s.state !== 'hitstun', "Nova's parry does not stun");
}
{ // The setting sets the length (seconds, light; heavy about half), clamped to 0.25-3 s; the stun marks the enemy dizzy until it ends
  const light = { light: true }, heavy = { light: false };
  SETTINGS.echoSpinStun = 2; const a = [spinStunTicks(light), spinStunTicks(heavy)];
  SETTINGS.echoSpinStun = 9; const b = spinStunTicks(light); SETTINGS.echoSpinStun = 0.1; const c = spinStunTicks(light);
  assert(a[0] === 120 && a[1] === 62 && b === 180 && c === 15, `Stun setting: 2 s is ${a[0]}/${a[1]} ticks (light/heavy), clamped to ${c}-${b}`);
  SETTINGS.echoSpinStun = 1;
  const { run, add } = setup(); const e = add('swarmer', 1.2);
  spin(run); const mid = e.dizzy && e.stun === 60;
  run({}, 60);
  assert(mid && !e.dizzy && e.state !== 'hitstun', 'At 1 s a swarmer is stunned 60 ticks, marked dizzy, then recovers unmarked');
  // follow-up staff hits on a stunned enemy don't cut the stun short
  SETTINGS.echoSpinStun = 2; const R = setup(); const f = R.add('shield', 1.2);
  spin(R.run); R.run({}, 30); const before = f.stun - f.st; R.log.length = 0;
  R.run({ held: { melee: true } }, 1); R.run({}, 8);
  assert(R.log.some(q => q.type === 'hit' && q.e === f) && before > 60 && f.state === 'hitstun' && f.dizzy && f.stun - f.st >= before - 10, `A stunned shield's guard is down, and a follow-up hit keeps the stun (${before} ticks left before, ${f.stun - f.st} after)`);
  SETTINGS.echoSpinStun = 0.85;
}
