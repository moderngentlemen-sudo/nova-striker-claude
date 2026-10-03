// Version 10: two new characters. RAM (Vanguard, the tank): the Rampart guard that blocks, covers the team and
// stores Kinetic, the Kinetic Release, the Ram Charge and Battering Ram, Stalwart, the Hydraulic Uplift, the
// Seismic Slam, the Bulwark Wall, Guardian Link, Provoke and Siege Breaker. Fix (Mechanic, the support): the
// Patch Beam (heals, Tune-Up, revives from range), faster revives, gadgets (Patch Pylon, Sentry, Amp Coil) and
// wrench upgrades, power-ups, the Rivet Gun, Jack-Up, Scrap and Overhaul. Runs the real simulation headless.
import { World } from '../game/js/world.js';
import { createEnemy } from '../game/js/enemies.js';
import { SETTINGS, CHARS, RAM, FIX, ULT, POUND, DASH_CHARGE, ROSTER, nextChar, MOVES, NOVA } from '../game/js/config.js';
import { boostRate } from '../game/js/player.js';
import { hasHeadroom } from '../game/js/level.js';
SETTINGS.novaKit = 'marksman'; SETTINGS.echoKit = 'hunter'; SETTINGS.lockOn = true; SETTINGS.lockMode = 'manual'; SETTINGS.dashCharge = true;
SETTINGS.dashIframes = false; SETTINGS.difficulty = 'normal';
const BT = ['jump', 'dash', 'melee', 'fire', 'parry', 'sig', 'mode', 'lock', 'sub', 'ult'];
function mk(prev, o = {}) {
  const held = {}; for (const b of BT) held[b] = !!(o.held && o.held[b]);
  const pressed = {}, released = {};
  for (const b of BT) { pressed[b] = held[b] && !prev.held[b]; released[b] = !held[b] && !!prev.held[b]; }
  return { mx: o.mx || 0, my: o.my || 0, aimFree: !!o.aim, ax: o.aim ? o.aim[0] : 0, ay: o.aim ? o.aim[1] : 0, held, pressed, released };
}
function setup(x = 99, chars = ['ram'], y = 0, gap = 1.2) {
  const w = new World(); w.enemies = []; w.towerSpawned = true; w.arena.state = 'cleared';
  const ps = chars.map((c, i) => { const p = w.addPlayer('t' + i, c); p.x = x + i * gap; p.y = y; p.prevX = p.x; p.prevY = y; return p; });
  const prev = ps.map(() => ({ held: {} })); const log = [];
  const run = (o = {}, n = 1, each = null) => {
    for (let i = 0; i < n; i++) {
      const spec = typeof o === 'function' ? o(i) : o, multi = Object.keys(spec).some(k => /^\d$/.test(k));
      const cmds = {};
      ps.forEach((p, j) => { const c = mk(prev[j], multi ? spec[j] || {} : j === 0 ? spec : {}); prev[j] = c; cmds[p.slot] = c; });
      w.step(cmds); log.push(...w.events); w.events.length = 0;
      if (each && each(i)) return true;
    }
    return false;
  };
  run({}, 10); for (const p of ps) p.mercy = 0;
  return { w, p: ps[0], ps, run, log };
}
const assert = (c, m) => { console.log((c ? 'PASS ' : 'FAIL ') + m); if (!c) process.exitCode = 1; };
const count = (log, t, f = () => true) => log.filter(e => e.type === t && f(e)).length;
const enemy = (w, type, x, y = 0, o = {}) => { const e = createEnemy(type, x, y, { cd: 9999, slamCd: 9999, ...o }); w.enemies.push(e); return e; };
const still = e => { e.hitstop = 1e9; return e; };
const hits = (log, e) => log.filter(h => h.type === 'hit' && h.e === e);
const tap = (run, b, o = {}) => { run({ ...o, held: { ...(o.held || {}), [b]: true } }, 1); run(o, 1); };
// An enemy shot fired from (x, y) at a point, from an enemy owner
const shoot = (w, owner, x, y, tx, ty, speed = 14, o = {}) => {
  const d = Math.hypot(tx - x, ty - y); w.spawnProjectile({ team: 'e', owner, x, y, vx: (tx - x) / d * speed, vy: (ty - y) / d * speed, r: 0.2, dmg: 6, kind: 'std', ttl: 200, ...o });
  return w.projectiles[w.projectiles.length - 1];
};
// An enemy strike landing on a box in front of `e`
const strike = (w, e, x0, x1, dmg = 8, o = {}) => w.spawnHitbox({ owner: e, team: 'e', x0, x1, y0: e.y, y1: e.y + 1.8, dmg, kb: [e.facing * 5, 3], instance: w.newInstance(), cat: 'standard', ...o });
const GUARD = { held: { parry: true }, aim: [1, 0] };

// ================================================================ RAM
{ // Size: much bigger than Nova and Echo, yet he walks under the arena's floating columns and crawls the gym tunnel
  const c = CHARS.ram, { w, p, run } = setup(65.5, ['ram']);
  run({ mx: 1 }, 120);
  const underColumns = p.x > 69.5 && Math.abs(p.y) < 0.01;
  const t = setup(41, ['ram']); t.run({ mx: 1, my: -1 }, 360);
  assert(c.height / CHARS.nova.height > 1.3 && c.width > 1 && c.hp > 150 && underColumns && t.p.x > 50.6 && hasHeadroom(65, 0, 0, c.width, c.height),
    `RAM: ${c.height} m tall (Nova ${CHARS.nova.height}), ${c.hp} health; walks under the columns (x ${p.x.toFixed(1)}) and crawls through the tunnel (x ${t.p.x.toFixed(1)})`);
}
{ // The Rampart blocks a shot from in front: no health lost, Integrity spent, Kinetic stored
  const { w, p, run, log } = setup(100, ['ram']);
  const sn = still(enemy(w, 'sniper', 110, 0));
  run(GUARD, 4);
  const pr = shoot(w, sn, 104, p.y + 1.4, p.x, p.y + 1.4, 14, { dmg: 10 });
  run(GUARD, 30);
  assert(p.state === 'guard' && p.hp === p.maxHp && pr.dead && Math.abs(p.integrity - 90) < 0.01 && Math.abs(p.kinetic - 10) < 0.01 && count(log, 'guardBlock') === 1,
    `Rampart blocks a shot: health ${p.hp}/${p.maxHp}, Integrity ${p.integrity.toFixed(0)}, Kinetic ${p.kinetic.toFixed(0)}`);
}
{ // It covers the team: a shot at a teammate behind him stops at the shield; a shot from behind still hits him
  const { w, p, ps, run } = setup(100, ['ram', 'fix'], 0, -1.6);
  const fix = ps[1], sn = still(enemy(w, 'sniper', 112, 0));
  run({ 0: GUARD }, 4);
  const pr = shoot(w, sn, 106, fix.y + 1.0, fix.x, fix.y + 1.0, 16, { dmg: 10 });
  run({ 0: GUARD }, 40);
  const covered = fix.hp === fix.maxHp && pr.dead && p.integrity < RAM.guard.integrity;
  shoot(w, sn, 96, p.y + 2.0, p.x, p.y + 2.0, 14, { dmg: 10 });   // over Fix's head
  run({ 0: GUARD }, 40);
  assert(covered && p.hp === p.maxHp - 10 && p.state === 'guard',
    `Teammate behind the Rampart covered (Fix ${fix.hp}/${fix.maxHp}); a shot from behind hits RAM (${p.hp}/${p.maxHp}) and he keeps guarding (Stalwart)`);
}
{ // Perfect Guard: raised just before the shot lands, it goes back at the shooter, and costs no Integrity
  const { w, p, run, log } = setup(100, ['ram']);
  const sn = still(enemy(w, 'sniper', 108, 0)); sn.hp = 60;
  const pr = shoot(w, sn, 102.2, p.y + 1.4, p.x, p.y + 1.4, 24, { dmg: 18, heavy: true, kind: 'heavy' });
  run(GUARD, 30);
  assert(pr.team === 'p' && pr.reflected && hits(log, sn).length === 1 && p.integrity === RAM.guard.integrity && count(log, 'perfectGuard') === 1 && p.kinetic >= RAM.guard.perfectKinetic,
    `Perfect Guard sends the heavy shot back into the sniper (${hits(log, sn).length} hit, ${(60 - sn.hp).toFixed(1)} damage); Integrity ${p.integrity}, Kinetic ${p.kinetic}`);
}
{ // Strikes from in front are blocked too, and a guard broken by damage puts him off balance until it regrows
  const { w, p, run, log } = setup(100, ['ram']);
  const sw = still(enemy(w, 'swarmer', 101.4, 0)); sw.facing = -1;
  run(GUARD, 12);
  strike(w, sw, 100.2, 101.4, 8); run(GUARD, 2);
  const blocked = p.hp === p.maxHp && Math.abs(p.integrity - 92) < 0.01;
  for (let i = 0; i < 3; i++) { strike(w, sw, 100.2, 101.4, 40, { heavy: true }); run(GUARD, 2); }
  const broken = p.guardBroken && count(log, 'rampartBreak') === 1 && p.hp === p.maxHp;
  run(GUARD, 60); const stillDown = p.state !== 'guard';
  let back = 0; run(GUARD, 400, i => { if (p.state === 'guard') { back = i; return true; } });
  assert(blocked && broken && stillDown && back > 0 && p.integrity >= RAM.guard.recover - 1,
    `Strike blocked (Integrity 92); the guard breaks under heavy blows (no health lost) and comes back ${(back / 60 + 1).toFixed(1)} s later at ${p.integrity.toFixed(0)}`);
}
{ // Kinetic Release: fire while guarding dumps the stored Kinetic as a cone of force in front
  const { w, p, run, log } = setup(100, ['ram']);
  const front = still(enemy(w, 'swarmer', 102.6, 0)), back = still(enemy(w, 'swarmer', 97.4, 0)); front.hp = back.hp = 60;
  const shot = shoot(w, front, 104, 1.4, 106, 1.4, 1, { ttl: 999 });
  run(GUARD, 12); p.kinetic = 100;
  run({ ...GUARD, held: { parry: true, fire: true } }, 1); run(GUARD, 2);
  const ev = log.find(e => e.type === 'kineticRelease');
  assert(ev && Math.abs(60 - front.hp - RAM.release.dmg[1]) < 0.01 && back.hp === 60 && p.kinetic === 0 && shot.dead,
    `Kinetic Release (full): ${(60 - front.hp).toFixed(1)} damage in front, reach ${ev && ev.r.toFixed(1)} m, nothing behind, the shot in its way erased`);
}
{ // Ram Charge: light enemies in front are scooped up and carried, and slammed into the wall at the end
  const { w, p, run, log } = setup(108.5, ['ram']);
  const a = still(enemy(w, 'swarmer', 110.2, 0)), b = still(enemy(w, 'swarmer', 111.4, 0)); a.hp = b.hp = 60;
  run({ mx: 1, held: { dash: true } }, 1); run({}, 24);
  const R = RAM.rush;
  assert(count(log, 'plowCatch') === 2 && count(log, 'ramSplat') === 1 && a.state === 'stagger' && b.state === 'stagger' &&
    Math.abs(60 - a.hp - (R.catchDmg[0] + R.splat.dmg[0])) < 0.01 && a.x < 113 && b.x < 113,
    `Ram Charge carries 2 Swarmers and slams them into the wall (${(60 - a.hp).toFixed(0)} damage each, both reeling)`);
}
{ // A heavy enemy stops an ordinary Ram Charge; the Battering Ram (level 2+) scoops it up too
  const { w, p, run, log } = setup(100, ['ram']);
  const br = still(enemy(w, 'brute', 102.6, 0));
  run({ mx: 1, held: { dash: true } }, 1); run({}, 20);
  const bonk = count(log, 'ramBonk') === 1 && br.state !== 'plowed' && count(log, 'plowCatch') === 0;
  const t = setup(100, ['ram']); const br2 = still(enemy(t.w, 'brute', 103.5, 0));
  t.run({ held: { dash: true } }, DASH_CHARGE.charge[1] + 4); t.run({ mx: 1 }, 1); t.run({ mx: 1 }, 6);
  const carried = br2.state === 'plowed' && count(t.log, 'plowCatch') === 1 && t.p.rush && t.p.rush.level === 2;
  assert(bonk && carried, `Ordinary charge bonks off the Brute; a level 2 Battering Ram carries it (${br2.state})`);
}
{ // RAM, the tank, has the most health on the team
  const most = ROSTER.filter(c => c !== 'ram').every(c => CHARS.ram.hp > CHARS[c].hp);
  assert(most, `RAM has the most health (${CHARS.ram.hp}; ${ROSTER.filter(c => c !== 'ram').map(c => `${c} ${CHARS[c].hp}`).join(', ')})`);
}
{ // Knockback: RAM's close-range hits throw a light enemy much further than the same hit from anyone else; a
  // finisher sends it flying. Bosses and armor still don't budge.
  const thrown = ramKnock => {
    const { w, p, run } = setup(100, ['ram']);
    const sw = enemy(w, 'swarmer', 101.5, 0); sw.hp = 60; sw.cd = 9999;
    const x0 = sw.x; let flew = false, far = 0;
    w.spawnHitbox({ owner: p, team: 'p', x0: 100.5, x1: 102.5, y0: 0, y1: 2, dmg: 1, poise: 1, kb: [MOVES.ram_b3.kb[0], MOVES.ram_b3.kb[1]], instance: w.newInstance(), ramKnock });
    run({}, 60, () => { if (sw.state === 'launched') flew = true; far = Math.max(far, sw.x - x0); });   // (before it walks back)
    return { d: far, flew };
  };
  const plain = thrown(false), ram = thrown(true);
  const { w, p, run } = setup(100, ['ram']);
  const br = enemy(w, 'brute', 101.8, 0); br.cd = 9999; const bx = br.x;
  w.spawnHitbox({ owner: p, team: 'p', x0: 100.5, x1: 103, y0: 0, y1: 2.5, dmg: 1, poise: 1, kb: [14, 4], instance: w.newInstance(), ramKnock: true });
  run({}, 30);
  assert(ram.flew && ram.d > plain.d * 2 && ram.d > 4 && Math.abs(br.x - bx) < 0.5,
    `RAM's Piston Punch throws a Swarmer ${ram.d.toFixed(1)} m (flying) against ${plain.d.toFixed(1)} m without his knockback; an armored Brute holds (${(br.x - bx).toFixed(2)} m)`);
}
{ // Stalwart: ordinary hits don't knock RAM about (he keeps doing what he was doing); heavy ones do
  const { w, p, run } = setup(100, ['ram']);
  const sw = still(enemy(w, 'swarmer', 101.3, 0)); sw.facing = -1;
  strike(w, sw, 100, 101.3, 8); run({}, 2);
  const kept = p.state === 'normal' && p.hp === p.maxHp - 8;
  p.mercy = 0; strike(w, sw, 100, 101.3, 20, { heavy: true, cat: 'heavy' }); run({}, 2);
  assert(kept && p.state === 'hitstun', `Stalwart: a standard hit costs health but not his footing (${kept}); a heavy one staggers him (${p.state})`);
}
{ // Hydraulic Uplift (up + melee): launches what is in front, lifts him, sweeps shots out of the air over him
  const { w, p, run, log } = setup(100, ['ram']);
  const sw = still(enemy(w, 'swarmer', 101.6, 0)); sw.hp = 60; sw.hitstop = 0; sw.cd = 9999;
  let top = 0, launched = 0, high = 0;
  run({ my: 1, held: { melee: true } }, 1);
  const pr = shoot(w, sw, 101, 4.0, 99, 4.0, 2, { ttl: 999 });
  run({ my: 1 }, 45, () => { top = Math.max(top, p.y); high = Math.max(high, sw.y); if (sw.state === 'launched') launched = Math.max(launched, sw.vy); });
  assert(launched >= RAM.rush.speed[0] && high > 2 && top > 1.0 && pr.dead && count(log, 'upliftBlast') === 1,
    `Hydraulic Uplift launches the Swarmer (vy ${launched.toFixed(1)}, ${high.toFixed(1)} m up), lifts RAM ${top.toFixed(2)} m, sweeps a shot away and bursts at the top`);
}
{ // Seismic Slam (hold melee, let go): shockwaves run out both ways along the floor
  const { w, p, run, log } = setup(100, ['ram']);
  const l = still(enemy(w, 'swarmer', 96, 0)), r = still(enemy(w, 'swarmer', 104.4, 0)); l.hp = r.hp = 60;
  run({ held: { melee: true } }, 3); p.state = 'normal'; p.move = null;   // (the press's own bash is beside the point here)
  run({ held: { melee: true } }, 34); run({}, 70);
  assert(count(log, 'quake') === 1 && hits(log, l).length >= 1 && hits(log, r).length >= 1,
    `Seismic Slam sends shockwaves both ways: hits at 4 m left (${(60 - l.hp).toFixed(1)}) and right (${(60 - r.hp).toFixed(1)})`);
}
{ // Bulwark Wall: enemy shots stop at it, a teammate's shot through it is boosted, enemies can't walk through it
  const { w, p, ps, run, log } = setup(100, ['ram', 'nova'], 0, -1.2);
  run({ 0: { held: { sig: true } } }, 1); run({}, 2);
  const wall = w.barriers.find(b => b.kind === 'rampart');
  const sn = still(enemy(w, 'sniper', 112, 0));
  const pr = shoot(w, sn, 106, 1.4, 96, 1.4, 14, { dmg: 10 });
  run({}, 30);
  const stopped = pr.dead && p.hp === p.maxHp && wall.hp === RAM.wall.hp - 10;
  w.fireShot(ps[1], 0); const mine = w.projectiles[w.projectiles.length - 1]; run({}, 12);
  const sw = enemy(w, 'swarmer', 105, 0); sw.cd = 9999;
  run({}, 150);
  assert(wall && stopped && mine.amplified && sw.x > wall.x + 0.3,
    `Bulwark Wall at ${(wall.x - p.x).toFixed(1)} m: an enemy shot stops (wall ${wall.hp}/${RAM.wall.hp}); Nova's shot passes boosted; the Swarmer is held at x ${sw.x.toFixed(2)}`);
}
{ // Guardian Link: he leaps to a far teammate's side and links; part of their damage comes to him, and they get Plating
  const { w, p, ps, run, log } = setup(100, ['ram', 'nova'], 0, 7);
  const nova = ps[1];
  tap(run, 'mode');
  run({}, 60, () => !!p.link);
  const landed = count(log, 'leap') === 1 && count(log, 'leapLand') === 1 && Math.abs(p.x - nova.x) < 2.2 && p.link && p.link.q === nova && nova.plate === RAM.link.plate;
  const sw = still(enemy(w, 'swarmer', nova.x + 1.2, 0)); sw.facing = -1;
  nova.mercy = 0; p.mercy = 0; const h0 = p.hp;
  strike(w, sw, nova.x - 0.3, nova.x + 1.2, 20); run({}, 2);
  assert(landed && Math.abs(h0 - p.hp - 12) < 0.01 && nova.hp === nova.maxHp && Math.abs(nova.plate - (RAM.link.plate - 8)) < 0.01,
    `Guardian Link: leaps 7 m to Nova and links (Plating ${RAM.link.plate}); a 20 hit puts 12 on RAM and 8 into Nova's Plating`);
}
{ // Provoke (LB): enemies close by turn on him, and he braces (takes less)
  const { w, p, ps, run, log } = setup(104, ['ram', 'nova'], 0, -6);
  const es = [enemy(w, 'swarmer', 106, 0), enemy(w, 'swarmer', 108, 0), enemy(w, 'shield', 110, 0)];
  run({}, 5);
  tap(run, 'sub'); run({}, 3);
  const taunted = es.every(e => e.taunter === p && e.tauntT > 0) && count(log, 'taunted') === 3;
  for (const e of es) still(e);
  p.mercy = 0; strike(w, es[0], p.x, p.x + 2, 10); run({}, 2);
  assert(taunted && Math.abs(p.maxHp - p.hp - 10 * RAM.provoke.brace) < 0.01, `Provoke turns 3 enemies on RAM; braced, a 10 hit costs ${(p.maxHp - p.hp).toFixed(1)}`);
}
{ // Meteor Drop: RAM's ground pound lands wider and harder
  const r = {};
  for (const c of ['nova', 'ram']) {
    const { p, run, log } = setup(100, [c], 6);
    p.onGround = false; run({ my: -1, held: { melee: true } }, 1); run({ my: -1 }, 60);
    const ev = log.find(e => e.type === 'poundLand'); r[c] = ev ? ev.r : 0;
  }
  assert(Math.abs(r.ram / r.nova - RAM.pound) < 0.01, `Meteor Drop reaches ${r.ram.toFixed(2)} m (Nova's pound ${r.nova.toFixed(2)} m)`);
}
{ // Siege Breaker: the team is Fortified, the charge scoops up everyone in the way, and the pile is slammed
  const { w, p, ps, run, log } = setup(98, ['ram', 'fix'], 0, -1.2);
  const es = [enemy(w, 'swarmer', 101.5, 0), enemy(w, 'swarmer', 103, 0), enemy(w, 'shield', 105, 0)].map(e => { e.hp = 200; return e; });
  p.ult = 100; tap(run, 'ult');
  run({}, ULT.cast + 4);
  run({}, ULT.ram.brace + ULT.ram.charge + ULT.ram.end + 10);
  assert(count(log, 'fortify') === 1 && ps[1].plate >= ULT.ram.fortify && count(log, 'plowCatch') === 3 && count(log, 'ramSlam') === 1 && es.every(e => e.hp < 200 - ULT.ram.slam.dmg * ULT.boss) && p.state === 'normal',
    `Siege Breaker: Fortify (${ps[1].plate} Plating on Fix), 3 enemies carried, then slammed (${es.map(e => (200 - e.hp).toFixed(0)).join(', ')} damage)`);
}

// ================================================================ Fix
{ // Patch Beam: it goes to the teammate who needs it most and heals them fast; it never heals the healthy one first
  const { w, p, ps, run } = setup(100, ['fix', 'nova', 'echo'], 0, 0);
  const nova = ps[1], echo = ps[2]; nova.x = 103; echo.x = 96; nova.hp = 40; echo.hp = 90;
  run({ held: { parry: true } }, 60);
  assert(p.state === 'patch' && p.patch.target === nova && Math.abs(nova.hp - 40 - FIX.beam.heal) < 1 && echo.hp === 90,
    `Patch Beam picks the most hurt (Nova) and heals ${(nova.hp - 40).toFixed(1)} in a second; Echo untouched (${echo.hp})`);
}
{ // Tune-Up: whoever the beam holds charges 1.5x as fast and fills their ultimate 1.5x as fast
  const { w, p, ps, run } = setup(100, ['fix', 'nova'], 0, 2);
  const nova = ps[1];
  run({ 1: { held: { fire: true }, aim: [1, 0] } }, 40); const alone = nova.chargeT;
  run({}, 30);
  run({ 0: { held: { parry: true } } }, 5);
  run({ 0: { held: { parry: true } }, 1: { held: { fire: true }, aim: [1, 0] } }, 40); const tuned = nova.chargeT;
  assert(Math.abs(tuned / alone - FIX.beam.tune) < 0.06 && boostRate(nova) === FIX.beam.tune,
    `Tune-Up: Nova's charge after 40 ticks ${alone.toFixed(0)} alone, ${tuned.toFixed(0)} while beamed (x${(tuned / alone).toFixed(2)})`);
}
{ // Revives: Fix beside a downed teammate brings them back 3x as fast, with more health; her beam does it from range
  const t = {};
  for (const [who, gap] of [['echo', 1], ['fix', 1], ['beam', 6]]) {
    const chars = who === 'echo' ? ['echo', 'nova'] : ['fix', 'nova'];
    const { w, p, ps, run } = setup(100, [...chars, 'ram'], 0, gap);
    ps[2].x = 90; const nova = ps[1]; w.downPlayer(nova);
    let n = 0; run({ held: { parry: who === 'beam' } }, 200, i => { if (nova.state !== 'downed') { n = i + 1; return true; } });
    t[who] = { n, hp: nova.hp };
  }
  assert(t.echo.n >= 119 && t.fix.n <= 42 && t.beam.n <= 42 && t.echo.hp === 40 && t.fix.hp === Math.round(100 * FIX.reviveHp) && t.beam.hp === 60,
    `Revive: ${t.echo.n} ticks beside Echo (40 health), ${t.fix.n} beside Fix and ${t.beam.n} by her beam from 6 m (${t.fix.hp} health)`);
}
{ // Gadgets: the Patch Pylon heals everyone in its field (a wrench hit pair upgrades it), costing Scrap
  const { w, p, ps, run, log } = setup(100, ['fix', 'nova'], 0, 2);
  const nova = ps[1]; nova.hp = 50; p.scrap = 100;
  tap(run, 'sig'); run({}, 2);
  const g = w.gadgets.find(q => q.kind === 'pylon');
  run({}, 120);
  const healed = nova.hp - 50;
  // Two wrench hits raise it a level
  tap(run, 'melee'); run({}, 20); tap(run, 'melee'); run({}, 30);
  const spent = count(log, 'gadgetDeploy') === 1 && p.scrap < 100 - FIX.gadget.pylon.cost + FIX.scrap.regen * 4;
  assert(g && spent && Math.abs(healed - FIX.gadget.pylon.heal[0] * 2) < 1.5 && g.level === 2 && count(log, 'gadgetUp') === 1,
    `Patch Pylon: heals Nova ${healed.toFixed(1)} in 2 s (cost ${FIX.gadget.pylon.cost} Scrap); two wrench hits make it level ${g && g.level}`);
}
{ // A downed teammate in a Patch Pylon's field gets back up on their own, slowly (much slower than Fix beside them)
  const { w, p, ps, run } = setup(100, ['fix', 'nova'], 0, 2);
  const nova = ps[1]; p.scrap = 100;
  tap(run, 'sig'); run({}, 2);
  p.x = 88; p.prevX = 88; w.downPlayer(nova);
  let n = 0; run({}, 600, i => { if (nova.state !== 'downed') { n = i + 1; return true; } });
  const want = 120 / FIX.gadget.pylon.revive[0];
  assert(n > 0 && Math.abs(n - want) < 6 && n > 4 * 40 && nova.hp === Math.round(100 * FIX.reviveHp),
    `Patch Pylon revive: Nova back up in ${n} ticks with nobody beside her (Fix beside her: 40), ${nova.hp} health`);
}
{ // Sentry: shoots the nearest enemy in sight until it falls; Amp Coil: teammates inside charge faster
  const { w, p, ps, run, log } = setup(100, ['fix', 'nova'], 0, -2);
  const sw = still(enemy(w, 'swarmer', 106, 0)); p.scrap = 100;
  tap(run, 'mode'); run({}, 12); tap(run, 'sig'); run({}, 240);
  const sentry = w.gadgets.find(q => q.kind === 'sentry');
  const shots = count(log, 'sentryShot'), dead = sw.dead;
  tap(run, 'mode'); run({}, 12); p.scrap = 100; tap(run, 'sig'); run({}, 4);
  const nova = ps[1]; nova.x = p.x + 0.5;
  run({ 1: { held: { fire: true }, aim: [1, 0] } }, 30); const k = boostRate(nova);
  assert(sentry && shots >= 3 && dead && Math.abs(k - FIX.gadget.coil.rate[0]) < 1e-9,
    `Sentry fires ${shots} bolts and drops the Swarmer; Nova in the Amp Coil's field charges x${k}`);
}
{ // Power-ups: melee with no enemy close tosses the selected one to the nearest teammate (it homes in)
  const { w, p, ps, run, log } = setup(100, ['fix', 'nova'], 0, 5);
  const nova = ps[1]; nova.hp = 30; p.scrap = 100;
  tap(run, 'sub'); run({}, 12); tap(run, 'sub'); run({}, 12);   // Overclock > Plating > Medkit
  tap(run, 'melee'); run({}, 60);
  const med = nova.hp === 70 && count(log, 'powerUp', e => e.kind === 'medkit' && e.p === nova) === 1;
  tap(run, 'sub'); run({}, 12); tap(run, 'melee'); run({}, 60);   // Overclock
  tap(run, 'sub'); run({}, 12); tap(run, 'melee'); run({}, 60);   // Plating
  assert(med && nova.overclockT > 0 && nova.plate === FIX.power.plating.plate && p.scrap < 100 - 3 * FIX.power.cost + 10,
    `Power-ups reach Nova: Medkit (+40, ${nova.hp}), Overclock (${(nova.overclockT / 60).toFixed(1)} s left), Plating (${nova.plate})`);
}
{ // With no one to toss to, it drops at her feet, and she can pick it up herself
  const { w, p, run, log } = setup(100, ['fix']);
  p.scrap = 100; tap(run, 'melee'); run({}, 45);
  const mine = w.pickups.filter(k => !k.level), dropped = mine.length === 1 && mine[0].rest;   // (not the level's own power-ups)
  run({ mx: 1 }, 6); run({ mx: -1 }, 20);
  assert(dropped && count(log, 'powerUp', e => e.p === p) === 1 && p.overclockT > 0, `Alone, the power-up drops at her feet and she picks it up (Overclock)`);
}
{ // Hot Rivet: charged, it sticks into the enemy and bursts after its fuse
  const { w, p, run, log } = setup(100, ['fix']);
  const sw = still(enemy(w, 'swarmer', 105, 0)); sw.hp = 60;
  run({ held: { fire: true }, aim: [1, -0.05] }, FIX.rivet.charge[0] + 4); run({ aim: [1, -0.05] }, 8);
  const stuck = count(log, 'rivetStick') === 1;
  run({}, FIX.rivet.hot.fuse + 4);
  assert(stuck && count(log, 'rivetBlast') === 1 && 60 - sw.hp >= FIX.rivet.hot.dmg[0] + FIX.rivet.hot.blast[0].dmg - 0.6,
    `Hot Rivet sticks, then bursts (${(60 - sw.hp).toFixed(1)} damage in all, with the burst)`);
}
{ // Jack-Up (up + melee): she rises behind a wrench uppercut and leaves a spring pad that bounces a teammate high
  const { w, p, ps, run, log } = setup(100, ['fix', 'nova'], 0, 4);
  let top = 0; run({ 0: { my: 1, held: { melee: true } } }, 1); run({}, 40, () => { top = Math.max(top, p.y); });
  const pad = w.gadgets.find(g => g.kind === 'pad'), nova = ps[1];
  nova.x = pad.x; nova.y = 3; nova.prevX = nova.x; nova.prevY = 3; nova.onGround = false; nova.vy = 0;
  let peak = 0; run({}, 60, () => { peak = Math.max(peak, nova.y); });
  assert(pad && top > 2 && count(log, 'padBounce', e => e.p === nova) === 1 && peak > 4,
    `Jack-Up lifts Fix ${top.toFixed(2)} m and leaves a pad: Nova bounces off it to ${peak.toFixed(1)} m`);
}
{ // Scrap: it trickles in, and an enemy falling near her drops some
  const { w, p, run, log } = setup(100, ['fix', 'nova'], 0, 2);
  p.scrap = 10; run({}, 60); const trickle = p.scrap - 10;
  const sn = still(enemy(w, 'sniper', 104.6, 0)); sn.hp = 0.5; w.fireShot(w.players[1], 0); run({}, 20);
  assert(Math.abs(trickle - FIX.scrap.regen) < 0.01 && count(log, 'scrap') === 1, `Scrap: +${trickle.toFixed(1)} a second, +${FIX.scrap.kill} from a kill close by`);
}
{ // Overhaul: a downed teammate is back up, the team healed, enemies on screen hurt; then Overclock, Plating, level 3 gadgets
  const { w, p, ps, run, log } = setup(100, ['fix', 'nova', 'echo'], 0, 1.5);
  const nova = ps[1], echo = ps[2]; w.downPlayer(nova); echo.hp = 30; p.scrap = 100;
  tap(run, 'sig'); run({}, 4);
  const sw = still(enemy(w, 'swarmer', 106, 0)); sw.hp = 60;
  p.ult = 100; tap(run, 'ult'); run({}, ULT.cast + ULT.fix.end + 10);
  const g = w.gadgets.find(q => q.kind === 'pylon');
  assert(nova.state === 'normal' && echo.hp > 30 + 2 * 0.35 * echo.maxHp - 1 && hits(log, sw).length >= 3 && echo.overclockT > 0 && echo.plate >= ULT.fix.plate && g && g.level === 3,
    `Overhaul: Nova revived, Echo healed to ${echo.hp.toFixed(0)}, the Swarmer hit ${hits(log, sw).length} times, team Overclocked and Plated, Pylon level 3`);
}

// ================================================================ Both
{ // Team ultimates have their own names; the roster cycles through all four
  const { w, p, ps, run } = setup(100, ['ram', 'fix'], 0, 1.5);
  p.ult = 100; ps[1].ult = 100;
  run({ 0: { held: { ult: true } } }, 1); run({}, 5); run({ 1: { held: { ult: true } } }, 1); run({}, ULT.cast);
  const name = w.ultCast && w.ultCast.name;
  const order = [ROSTER[0]]; for (let i = 0; i < 4; i++) order.push(nextChar(order[order.length - 1]));
  assert(name === 'Heavy Metal' && order.join(' > ') === 'nova > echo > ram > fix > nova' && nextChar('nova', -1) === 'fix',
    `RAM + Fix team ultimate: ${name}; swapping cycles ${order.join(' > ')}`);
}
{ // An ultimate that ends in a pit (Siege Breaker charging off a ledge) finishes there: the world never stays frozen
  const { w, p, run, log } = setup(9, ['ram', 'nova'], 0, -2);
  enemy(w, 'swarmer', 12.5, 0, { hp: 99 });
  p.ult = 100; tap(run, 'ult', { aim: [1, 0] });
  let n = 0; run({}, 500, i => { if (!w.ultCast) { n = i + 1; return true; } });
  assert(n > 0 && p.state === 'normal' && !p.ultRun && count(log, 'recall') >= 1 && !w.enemies.some(e => e.state === 'plowed'),
    `Siege Breaker off the gym's ledge: RAM recalled from the pit, the ultimate over after ${n} ticks (${p.state}), nothing left plowed`);
}
