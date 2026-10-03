// Nova's Marksman kit: bracer attachments, three charge levels, Perfect Release, Focus, the secondary
// blaster (Recoil Burst), splash, rocket jumps, light boosters, full-level range and skate glide.
// Runs the real simulation headless. Enemies are frozen in place (huge hitstop) unless a test needs them to move.
import { World } from '../game/js/world.js';
import { createEnemy } from '../game/js/enemies.js';
import { SETTINGS, MARKSMAN, NOVA } from '../game/js/config.js';
import { chargeStage, burstStage, focusMult } from '../game/js/player.js';
SETTINGS.novaKit = 'marksman'; SETTINGS.echoKit = 'hunter';
const BT = ['jump', 'dash', 'melee', 'fire', 'parry', 'sig', 'mode'];
function mk(prev, o = {}) {
  const held = {}; for (const b of BT) held[b] = !!(o.held && o.held[b]);
  const pressed = {}, released = {};
  for (const b of BT) { pressed[b] = held[b] && !prev.held[b]; released[b] = !held[b] && !!prev.held[b]; }
  return { mx: o.mx || 0, my: o.my || 0, aimFree: !!o.aim, ax: o.aim ? o.aim[0] : 0, ay: o.aim ? o.aim[1] : 0, held, pressed, released };
}
function setup(x = 99) {
  const w = new World(); w.enemies = [];
  const p = w.addPlayer('test', 'nova'); p.x = x; p.y = 0;
  let prev = { held: {} }; const log = [];
  const run = (o = {}, n = 1) => {
    for (let i = 0; i < n; i++) { const c = mk(prev, o); prev = c; w.step({ 0: c }); log.push(...w.events); w.events.length = 0; }
  };
  run({}, 10); p.mercy = 0;
  return { w, p, run, log };
}
const assert = (c, m) => { console.log((c ? 'PASS ' : 'FAIL ') + m); if (!c) process.exitCode = 1; };
const count = (log, t, f = () => true) => log.filter(e => e.type === t && f(e)).length;
const tap = (run, b, extra = {}) => { run({ ...extra, held: { [b]: true } }, 1); run(extra, 1); };
const enemy = (w, type, x, y = 0, o = {}) => { const e = createEnemy(type, x, y, { cd: 9999, slamCd: 9999, ...o }); e.hitstop = 1e9; w.enemies.push(e); return e; };
// Hold fire for n ticks (the first tick is the press, which also fires a basic round), then let go
const charge = (run, n, aim = [1, 0]) => { run({ aim, held: { fire: true } }, n); run({ aim }, 1); };
const select = (run, p, name) => { for (let i = 0; i < 4 && p.attachment !== name; i++) { tap(run, 'mode'); run({}, MARKSMAN.switchCd); } };
const aimAt = (p, e) => { const dx = e.x - p.x, dy = e.y + e.h / 2 - (p.y + p.h * 0.62), m = Math.hypot(dx, dy); return [dx / m, dy / m]; };
const hitsOn = (log, e) => count(log, 'hit', h => h.e === e);
const M = MARKSMAN, C = M.charge, W = M.perfectWindow, BC = M.burst.charge;

{ // The mode button cycles the bracer attachments
  const { p, run, log } = setup();
  const seen = [p.attachment];
  for (let i = 0; i < 4; i++) { tap(run, 'mode'); run({}, M.switchCd); seen.push(p.attachment); }
  assert(seen.join(' > ') === 'lance > volley > arc > prism > lance' && count(log, 'attach') === 4, `Mode button cycles ${seen.join(' > ')}`);
}
{ // Charge stages, including the Perfect Release window
  const { p, run } = setup();
  const stages = [];
  const held = n => { run({ aim: [1, 0], held: { fire: true } }, n); stages.push(chargeStage(p)); };
  held(1); held(C[0] - 1); held(C[1] - C[0]); held(C[2] - C[1]); held(W);
  run({ aim: [1, 0] }, 1); stages.push(chargeStage(p) || 'idle');
  assert(stages.join(' > ') === 'charging > L1 > L2 > perfect > L3 > idle', `Three charge levels: ${stages.join(' > ')}`);
}
{ // Tap fires a basic round; a partial charge fires a Lance that pierces two Swarmers
  const { w, p, run, log } = setup(99);
  const a = enemy(w, 'swarmer', 103), b = enemy(w, 'swarmer', 105);
  const aim = aimAt(p, a);
  charge(run, C[0] + 2, aim);
  const lance = w.projectiles.find(pr => pr.kind === 'lance');
  run({}, 40);
  assert(count(log, 'shot', s => s.level === 0) === 1 && lance && lance.pierce && a.dead && b.dead,
    `Basic round on press, then a level 1 Lance pierces both Swarmers (dead: ${a.dead}, ${b.dead})`);
}
{ // Letting go inside the window after level 3 is a Perfect Release; letting go late is a normal level 3 shot
  const t1 = setup(99); charge(t1.run, C[2] + 2);
  const perf = t1.w.projectiles.find(pr => pr.kind === 'rail');
  const t2 = setup(99); charge(t2.run, C[2] + W + 3);
  const late = t2.w.projectiles.find(pr => pr.kind === 'rail');
  const t3 = setup(99); charge(t3.run, C[1] + 2);
  const two = t3.w.projectiles.find(pr => pr.kind === 'lance');
  assert(count(t1.log, 'perfectRelease') === 1 && perf && perf.perfect && Math.abs(perf.dmg - M.lance[3].dmg * M.perfectMult) < 1e-9 &&
    count(t2.log, 'perfectRelease') === 0 && late && !late.perfect && Math.abs(late.dmg - M.lance[3].dmg) < 1e-9 &&
    two && Math.abs(two.dmg - M.lance[2].dmg) < 1e-9,
    `Level 2 Lance deals ${two && two.dmg}; Perfect Release inside ${W} ticks of level 3 deals ${perf && perf.dmg}; a late release deals ${late && late.dmg}`);
}
{ // A level 2 Lance breaks a layer of Brute armor; a Perfect one breaks a Shieldbearer's guard head-on
  const { w, p, run, log } = setup(99); const br = enemy(w, 'brute', 104);
  charge(run, C[1] + 2, aimAt(p, br)); run({}, 20);
  const t = setup(99); const sh = enemy(t.w, 'shield', 104); sh.shieldDir = -1;
  charge(t.run, C[0] + 2, aimAt(t.p, sh)); t.run({}, 20);
  const blockedL1 = count(t.log, 'blocked');
  const t3 = setup(99); const sh3 = enemy(t3.w, 'shield', 104); sh3.shieldDir = -1;
  charge(t3.run, C[2] + 2, aimAt(t3.p, sh3)); t3.run({}, 20);
  assert(br.armor === 2 && blockedL1 >= 1 && count(t3.log, 'guardBreak') === 1,
    `Level 2 Lance strips Brute armor (${br.armor} left); a level 1 Lance is blocked by a shield (${blockedL1}); a Perfect one breaks the guard`);
}
{ // Volley: five darts spread over two enemies at different heights, and both are hit
  const { w, p, run, log } = setup(99); select(run, p, 'volley');
  const a = enemy(w, 'swarmer', 105), b = enemy(w, 'sniper', 106, 4);
  charge(run, C[1] + 2);
  const darts = w.projectiles.filter(pr => pr.kind === 'dart');
  run({}, 60);
  assert(darts.length === M.volley.darts[2] && hitsOn(log, a) >= 1 && hitsOn(log, b) >= 1,
    `Level 2 Volley fires ${darts.length} darts that home onto both targets (hits ${hitsOn(log, a)} + ${hitsOn(log, b)})`);
}
{ // Dart counts: level 1 fires 3, level 3 fires 7, a Perfect Release fires 9
  const darts = n => { const t = setup(99); select(t.run, t.p, 'volley'); charge(t.run, n); return t.w.projectiles.filter(pr => pr.kind === 'dart').length; };
  const d1 = darts(C[0] + 1), d3 = darts(C[2] + W + 2), dp = darts(C[2] + 1);
  assert(d1 === 3 && d3 === 7 && dp === 9, `Volley darts: level 1 ${d1}, level 3 ${d3}, Perfect ${dp}`);
}
{ // Arc: the shell arcs, bursts on the Shieldbearer (not blocked, even from the front) and spares an enemy outside the radius
  const { w, p, run, log } = setup(99); select(run, p, 'arc');
  const sh = enemy(w, 'shield', 109.2); sh.shieldDir = -1;
  const far = enemy(w, 'swarmer', 112.3);
  run({ aim: [1, 0], held: { fire: true } }, C[0] + 2);
  run({ aim: [1, 0] }, 1);
  const shell = w.projectiles.find(pr => pr.kind === 'shell');
  const y0 = shell ? shell.y : 0; let top = y0;
  for (let i = 0; i < 90 && !count(log, 'blast'); i++) { run({}, 1); if (shell && !shell.dead) top = Math.max(top, shell.y); }
  const blast = log.find(e => e.type === 'blast');
  // (the basic round fired on the press is blocked by the shield; the blast is not)
  const blastHits = count(log, 'hit', h => h.e === sh && h.source === 'blast');
  assert(shell && top > y0 + 1 && blast && blastHits === 1 && hitsOn(log, far) === 0,
    `Arc shell climbs ${(top - y0).toFixed(2)} m, bursts at x=${blast && blast.x.toFixed(1)} (r ${blast && blast.r}) and hits the shield head-on (${blastHits}); the far Swarmer is untouched`);
}
{ // Prism: the round splits on the first enemy and a shard hits the one behind it
  const { w, p, run, log } = setup(99); select(run, p, 'prism');
  const a = enemy(w, 'swarmer', 103), b = enemy(w, 'sniper', 106);
  charge(run, C[1] + 2, aimAt(p, a)); run({}, 40);
  const split = log.find(e => e.type === 'split');
  assert(split && split.n === 3 && hitsOn(log, a) >= 1 && hitsOn(log, b) >= 1,
    `Prism splits into ${split && split.n} shards on impact and a shard hits the Sniper behind (hits ${hitsOn(log, b)})`);
}
{ // Prism into a wall: it splits off the surface and the shards ricochet
  const { w, p, run, log } = setup(105); select(run, p, 'prism');
  charge(run, C[0] + 2); run({}, 60);
  assert(count(log, 'split') === 1 && count(log, 'ricochet') >= 1, `Prism splits off the wall and shards ricochet (${count(log, 'ricochet')} bounces)`);
}
{ // Scatter: pellets hit and push an enemy just outside melee reach, Nova stays put (no recoil since
  // Version 9), and a cooldown applies (closer in, the melee button is his bracer combo instead)
  const { w, p, run, log } = setup(99); const e = enemy(w, 'swarmer', 101.6);
  const aim = aimAt(p, e);
  run({ aim }, 2);
  tap(run, 'melee', { aim });
  const vxAfter = p.vx; run({ aim }, 6);   // the pellets need a few ticks to cover the distance
  const hits = hitsOn(log, e), push = e.vx;
  tap(run, 'melee', { aim });
  const quick = count(log, 'burst');
  run({ aim }, M.burst.cd - 6); tap(run, 'melee', { aim });
  assert(count(log, 'burst') === 2 && quick === 1 && hits >= 3 && push > 5 && Math.abs(vxAfter) < 0.5 && p.moveId === null,
    `Scatter lands ${hits} pellets, pushes the enemy (vx ${push.toFixed(1)}), no recoil on Nova (vx ${vxAfter.toFixed(1)}); a second press inside the cooldown does nothing`);
}
{ // Secondary blaster charge levels: pressing fires a quick burst; holding passes levels 1-3; letting go
  // right after level 3 is a Perfect level 3 burst with a muzzle blast, and its centre pellet breaks armor
  const { w, p, run, log } = setup(99); const br = enemy(w, 'brute', 102.2);
  const aim = aimAt(p, br), stages = [];
  run({ aim, held: { melee: true } }, BC[0]); stages.push(burstStage(p));
  run({ aim, held: { melee: true } }, BC[1] - BC[0]); stages.push(burstStage(p));
  run({ aim, held: { melee: true } }, BC[2] - BC[1] + 2); stages.push(burstStage(p));
  const before = new Set(w.projectiles);
  run({ aim }, 1);
  const b = log.filter(e => e.type === 'burst'), fresh = w.projectiles.filter(pr => pr.kind === 'pellet' && !before.has(pr)).length;
  run({ aim }, 10);
  assert(stages.join(' > ') === 'L1 > L2 > perfect' && count(log, 'burstLevel') === 3 && b.length === 2 && b[0].level === 0 && b[1].level === 3 && b[1].perfect &&
    fresh === M.burst[3].pellets && count(log, 'blast') === 1 && br.armor <= 2,
    `Burst stages ${stages.join(' > ')}; release fires a Perfect level 3 burst (${fresh} pellets + blast); Brute armor ${br.armor}`);
}
{ // A level 1 secondary burst fires 7 pellets
  const { w, p, run, log } = setup(99);
  run({ aim: [1, 0], held: { melee: true } }, BC[0] + 2);
  const before = new Set(w.projectiles); run({ aim: [1, 0] }, 1);
  const fresh = w.projectiles.filter(pr => pr.kind === 'pellet' && !before.has(pr)).length;
  assert(fresh === M.burst[1].pellets && log.filter(e => e.type === 'burst').pop().level === 1, `Level 1 secondary burst: ${fresh} pellets`);
}
{ // Pellets fly on but fall off with distance
  const hitDmg = x => { const t = setup(99); const post = enemy(t.w, 'post', x); tap(t.run, 'melee', { aim: aimAt(t.p, post) }); t.run({}, 50); const h = t.log.find(e => e.type === 'hit' && e.e === post); return h ? h.dmg : 0; };
  const near = hitDmg(100.8), far = hitDmg(109);
  assert(near > 0 && far > 0 && far < near * 0.6, `Pellet damage ${near.toFixed(2)} up close, ${far.toFixed(2)} at 10 m`);
}
{ // No recoil in the air either: a charged Scatter aimed diagonally down neither lifts nor pushes him
  const { p, run } = setup(99), aim = [0.55, -0.83];
  p.y = 14; p.vy = 0; p.onGround = false; run({}, 4);
  run({ aim, held: { melee: true } }, BC[2] + 2);
  const vy0 = p.vy, vx0 = p.vx; run({ aim }, 1); const vy1 = p.vy, vx1 = p.vx;
  assert(vy1 <= vy0 && Math.abs(vx1 - vx0) < 0.5, `Charged Scatter aimed down in the air: no lift (vy ${vy0.toFixed(1)} -> ${vy1.toFixed(1)}) and no push (vx ${vx0.toFixed(1)} -> ${vx1.toFixed(1)})`);
}
{ // Skates: higher top speed, a longer glide than the Sentinel kit, and no backpedal slowdown while aiming behind
  const glide = kit => {
    SETTINGS.novaKit = kit; const { p, run } = setup(98);
    run({ mx: 1 }, 40); const top = p.vx, x0 = p.x;
    run({}, 60); const dist = p.x - x0;
    const t = setup(98); t.run({ mx: 1, aim: [-1, 0], held: { fire: true } }, 50); const back = t.p.vx;
    return { top, dist, back };
  };
  const m = glide('marksman'), s = glide('sentinel'); SETTINGS.novaKit = 'marksman';
  assert(Math.abs(m.top - M.skate.top) < 0.05 && m.dist > s.dist * 3 && m.back > 7.5 && s.back < 6.5,
    `Top speed ${m.top.toFixed(2)} vs ${s.top.toFixed(2)}; glide ${m.dist.toFixed(2)} m vs ${s.dist.toFixed(2)} m; moving while aiming behind ${m.back.toFixed(1)} vs ${s.back.toFixed(1)}`);
}
{ // Reversing at speed carves to a quick stop
  const { p, run, log } = setup(98);
  run({ mx: 1 }, 40); let t = 0;
  while (p.vx > 0 && t < 30) { run({ mx: -1 }, 1); t++; }
  assert(count(log, 'carve') >= 1 && t <= 7, `Carve event and a stop from ${M.skate.top} m/s in ${t} ticks`);
}
{ // Focus: charged hits build it (2 for a Perfect Release), it boosts damage, and taking a hit clears it
  const { w, p, run, log } = setup(99); const post = enemy(w, 'post', 104);
  const aim = aimAt(p, post);
  charge(run, C[0] + 2, aim); run({}, 20);
  const f1 = p.focus;
  charge(run, C[2] + 2, aim); run({}, 20);
  const f2 = p.focus;
  charge(run, C[0] + 2, aim);
  const lance = w.projectiles.find(pr => pr.kind === 'lance');
  const expected = M.lance[1].dmg * (1 + M.focus.dmgPer * Math.floor(p.focus));
  run({}, 20);
  w.spawnHitbox({ owner: post, team: 'e', x0: p.x - 0.8, x1: p.x + 0.8, y0: p.y, y1: p.y + 1.8, dmg: 5, kb: [0, 0], instance: w.newInstance(), cat: 'standard' });
  run({}, 1);
  assert(Math.floor(f1) === 1 && Math.floor(f2) === 3 && lance && Math.abs(lance.dmg - expected) < 1e-9 && p.focus === 0 && count(log, 'focusLost') === 1,
    `Focus ${f1.toFixed(2)} after a hit, ${f2.toFixed(2)} after a Perfect Release; Lance damage ${lance && lance.dmg.toFixed(2)} (x${focusMult({ char: 'nova', focus: 3 }).toFixed(2)}); cleared when hit`);
}
{ // Focus drains when Nova stops landing shots
  const { p, run } = setup(99); p.focus = 3; p.focusT = 5;
  run({}, 5 + M.focus.decayStep + 1);
  assert(p.focus === 1, `Focus drains one level after a quiet stretch, then one more every ${M.focus.decayStep} ticks (now ${p.focus})`);
}
{ // The Sentinel kit is untouched: mode does nothing, melee starts the jab chain, a full charge fires the rail
  SETTINGS.novaKit = 'sentinel';
  const { w, p, run, log } = setup(99);
  tap(run, 'mode'); tap(run, 'melee');
  const jab = p.moveId;
  run({}, 40);
  charge(run, NOVA.charge2 + 1);
  const rail = w.projectiles.find(pr => pr.kind === 'rail');
  SETTINGS.novaKit = 'marksman';
  assert(count(log, 'attach') === 0 && jab === 'nova_jab1' && rail && !rail.perfect && count(log, 'burst') === 0,
    `Sentinel kit: no attachment switch, melee starts ${jab}, full charge fires the rail`);
}
{ // Splash: a basic round that hits one Swarmer also hurts the one right behind it
  const { w, p, run, log } = setup(99); const a = enemy(w, 'swarmer', 104), b = enemy(w, 'swarmer', 104.8);
  tap(run, 'fire', { aim: aimAt(p, a) }); run({}, 30);
  const splashB = count(log, 'hit', h => h.e === b && h.source === 'blast');
  assert(hitsOn(log, a) >= 1 && splashB === 1 && count(log, 'splash') >= 1, `Direct hit on the first Swarmer, splash on the second (${splashB})`);
}
{ // Splash on terrain: a Lance fired into the floor beside a Swarmer still hurts it
  const { w, p, run, log } = setup(99); const e = enemy(w, 'swarmer', 102.4);
  const dx = 101.9 - p.x, dy = 0 - (p.y + p.h * 0.62), m = Math.hypot(dx, dy);
  charge(run, C[0] + 2, [dx / m, dy / m]); run({}, 20);
  assert(count(log, 'hit', h => h.e === e && h.source === 'blast') >= 1, `Lance into the floor splashes the Swarmer beside the impact`);
}
{ // Range: a basic round flies past the old 20 m limit until it hits the ledge wall 31 m away
  const { w, p, run, log } = setup(0);
  tap(run, 'fire', { aim: [1, 0] }); run({}, 90);
  const wall = log.find(e => e.type === 'projWall' && e.pr.kind === 'shot');
  assert(wall && wall.x > 31, `Round travelled to x=${wall && wall.x.toFixed(1)} (the ledge wall), well past 20 m`);
}
{ // Rocket jump: a level 3 shot at his feet launches Nova well above a normal jump; basic rounds do not
  const height = n => {
    const t = setup(99); const y0 = t.p.y; let top = y0;
    t.run({ aim: [0, -1], held: { fire: true } }, n); t.run({ aim: [0, -1] }, 1);
    for (let i = 0; i < 80; i++) { t.run({ held: { jump: false } }, 1); top = Math.max(top, t.p.y); }
    return { h: top - y0, rj: count(t.log, 'rocketJump') };
  };
  const l3 = height(C[2] + W + 2), l1 = height(C[0] + 2), perf = height(C[2] + 2);
  const t = setup(99); tap(t.run, 'fire', { aim: [0, -1] }); t.run({}, 20);
  assert(l3.rj === 1 && l3.h > 3.2 && perf.h > l3.h && l1.h > 0.8 && l1.h < l3.h && count(t.log, 'rocketJump') === 0,
    `Rocket jump heights: level 1 ${l1.h.toFixed(2)} m, level 3 ${l3.h.toFixed(2)} m, Perfect ${perf.h.toFixed(2)} m (a normal jump is 3.2 m); basic rounds don't launch`);
}
{ // A second rocket jump in the same airtime is weaker
  const { w, p, run, log } = setup(99);
  p.y = 6; p.onGround = false; p.vy = 0;
  const spec = { r: 1.9, rocket: 21 };
  w.rocketPush(p, p.x, p.y - 0.2, p.z, spec, null, false); p.vy = 0;
  w.rocketPush(p, p.x, p.y - 0.2, p.z, spec, null, false);
  const ks = w.events.filter(e => e.type === 'rocketJump').map(e => e.k);
  assert(ks.length === 2 && Math.abs(ks[1] / ks[0] - M.rocket.air[1]) < 1e-9, `Second air rocket jump strength x${(ks[1] / ks[0]).toFixed(2)}`);
}
{ // Light boosters: after the double jump, press and hold jump to hover; fuel runs out, then refills on landing
  const { p, run, log } = setup(99);
  run({ held: { jump: true } }, 12); run({}, 1);                  // jump
  run({ held: { jump: true } }, 1); run({}, 1);                   // double jump
  while (p.vy > 1) run({}, 1);
  const y0 = p.y; run({ held: { jump: true } }, 30);              // press again and hold: boosters
  const hover = p.y - y0, fuelMid = p.fuel, on = count(log, 'thrustOn');
  run({ held: { jump: true } }, 60);
  const empty = p.fuel;
  for (let i = 0; i < 120 && !p.onGround; i++) run({}, 1);
  run({}, 30);
  assert(on === 1 && hover > 0.5 && fuelMid < M.boost.fuel && empty === 0 && count(log, 'thrustOff') >= 1 && p.fuel === M.boost.fuel,
    `Boosters lift Nova ${hover.toFixed(2)} m in half a second after the apex, run dry, and refill on landing`);
}
{ // Holding jump through an ordinary jump never fires the boosters; letting go cuts them
  const { p, run, log } = setup(99);
  for (let i = 0; i < 80 && !(i > 5 && p.onGround); i++) run({ held: { jump: true } }, 1);
  const plain = count(log, 'thrustOn');
  run({}, 5); run({ held: { jump: true } }, 12); run({}, 1); run({ held: { jump: true } }, 1); run({}, 1);
  run({ held: { jump: true } }, 5); run({}, 2);
  assert(plain === 0 && count(log, 'thrustOn') === 1 && count(log, 'thrustOff') === 1 && !p.thrusting,
    `A held ordinary jump never boosts; a press after the double jump does, and letting go stops it`);
}
