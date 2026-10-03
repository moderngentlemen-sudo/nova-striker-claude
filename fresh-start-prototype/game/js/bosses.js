// Level bosses. The Lockwarden holds the Concourse Lock (its last wave); the Stormcaller guards the relay beacon
// at the end of the Skyline Relay. Both have two phases: at half health they roar (invulnerable for a moment),
// re-arm and speed up, and gain a new attack. Every attack uses the usual telegraph categories (a glint for
// standard, a double glint for heavy, the magenta strip and rising tone for unblockable), and each boss opens
// real punish windows: a Lockwarden that charges into a wall or has its hammer perfect-parried is dazed; a
// Stormcaller that dives into the pad, or is perfect-parried out of the dive, crashes and lies open. They are
// never knocked back, hit-stop on them is kept short, poise decays between hits, and after a stagger they
// cannot be staggered again for a while (combat.js). Registered into the enemy tables at import.
import { DT, GRAVITY } from './config.js';
import { ENEMY_TYPES, BEHAVIOUR, nearestPlayer, canTarget, enemyPhysics, createEnemy, faceTo } from './enemies.js';
import { rayCast, groundBelow, ARENA_HW } from './level.js';
import { away, hdist, setFacing, fz, fwdBox, boxAt, spreadDir } from './geom.js';

export const BOSS = {
  warden: {
    name: 'Lockwarden', title: 'The lock’s last guard',
    hp: 240, armor: 4, rearm: 2, poise: 420, poiseDecay: 0.6, stagger: 150, staggerCd: 420, daze: 110, walk: 2.4, cd: [46, 28],
    sweep: { wind: 22, active: 8, rec: 30, reach: 3.6, dmg: 14, kb: [9, 4] },
    hammer: { wind: 34, active: 6, rec: 44, reach: 3.9, dmg: 24, kb: [12, 6] },
    stomp: { wind: 40, jump: 13, hop: 6, dmg: 22, rec: 40 },
    missiles: { wind: 26, n: [3, 5], dmg: 10, gravity: 16, rec: 34 },
    charge: { wind: 36, speed: 16, maxTicks: 120, dmg: 26, rec: 30 },
    laser: { wind: 50, ticks: 42, dmg: 20, low: [0.3, 0.8], high: [1.15, 1.55], range: 40, rec: 36, turn: 1.15 },   // turn: how many times round it sweeps
  },
  stormcaller: {
    name: 'Stormcaller', title: 'It keeps the relay beacon',
    hp: 260, armor: 0, rearm: 2, poise: 380, poiseDecay: 0.6, stagger: 140, staggerCd: 420, hover: 6.6, speed: 7, cd: [40, 26],
    padX: [300.5, 313.5], floor: 18.6,
    volley: { wind: 24, bursts: [3, 4], every: 10, speed: 14, dmg: 8, spread: 0.12, rec: 26 },
    rain: { wind: 40, n: [4, 6], blast: { r: 1.7, dmg: 16 }, gravity: 26, rec: 30 },
    sweep: { wind: 50, ticks: 44, low: [0.3, 0.8], high: [1.15, 1.55], hoverY: 2.4, dmg: 20, rec: 60 },
    dive: { wind: 36, speed: 22, maxTicks: 60, dmg: 24, crash: 70, parried: 115, rise: 40 },
    drones: 2,
  },
};
Object.assign(ENEMY_TYPES, {
  warden: { w: 2.2, h: 3.4, hp: BOSS.warden.hp, poise: BOSS.warden.poise, speed: BOSS.warden.walk, flinch: false, light: false, armor: BOSS.warden.armor, boss: true },
  stormcaller: { w: 3.0, h: 1.5, hp: BOSS.stormcaller.hp, poise: BOSS.stormcaller.poise, speed: BOSS.stormcaller.speed, flinch: false, light: false, flier: true, boss: true },
});

const approach = (v, t, d) => (v < t ? Math.min(v + d, t) : Math.max(v - d, t));
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
function setState(e, s) { e.state = s; e.st = 0; }
function pick(e, opts) {
  const pool = opts.filter(o => o[0] !== e.lastAtk), sum = pool.reduce((s, o) => s + o[1], 0);
  let r = Math.random() * sum;
  for (const o of pool) { r -= o[1]; if (r <= 0) return o[0]; }
  return pool[0][0];
}

// Shared: poise decays, stagger immunity, and the half-health phase change (a roar that re-arms it)
function bossTick(e, world, B) {
  e.poise = e.staggerCd > 0 ? 0 : Math.max(0, e.poise - B.poiseDecay);
  if (e.staggerCd > 0) e.staggerCd--;
  if (e.invuln > 0) e.invuln--;
  if (e.phase === 1 && e.hp <= e.maxHp / 2 && e.state !== 'intro') {
    e.phase = 2; e.armor = e.armorMax = B.rearm; e.invuln = 80; e.atk = null;
    setState(e, 'roar');
    world.emit('bossPhase', { e, x: e.x, y: e.y + e.h * 0.6, z: e.z });
    return true;
  }
  return false;
}

// Starts an attack: telegraphs it and remembers what it is
function begin(e, world, kind, cat, wind, extra = {}) {
  e.atk = { kind, wind, inst: world.newInstance(), ...extra }; e.lastAtk = kind;
  setState(e, 'windup'); world.telegraph(e, cat, wind);
}

// Where a laser runs: from the boss along the horizontal direction (dx, dz) in the height band [y0, y1] above
// the floor, to the first wall. A thick segment (r: half the band), the shape its hitbox takes (combat.js seg).
function laserSpan(e, dx, dz, fy, band, range) {
  const y = fy + (band[0] + band[1]) / 2, x0 = e.x + dx * (e.w / 2 + 0.1), z0 = e.z + dz * (e.w / 2 + 0.1), h = rayCast(x0, y, z0, dx, 0, dz, range);
  return { x0, y0: y, z0, x1: x0 + dx * h.t, y1: y, z1: z0 + dz * h.t, r: (band[1] - band[0]) / 2, y, band: [fy + band[0], fy + band[1]] };
}
// The Stormcaller's sweep: a sheet of light across the whole pad at one height, from where it hangs along the
// pad (jump it or duck under it; there is no side-stepping a wall of light)
function sheetSpan(e, dir, fy, band, range) {
  const y = fy + (band[0] + band[1]) / 2, x0 = e.x + dir * (e.w / 2 + 0.1), h = rayCast(x0, y, 0, dir, 0, 0, range);
  return { x0: Math.min(x0, x0 + dir * h.t), x1: Math.max(x0, x0 + dir * h.t), y0: fy + band[0], y1: fy + band[1], z0: -ARENA_HW, z1: ARENA_HW, y, sheet: true };
}

// ---- Lockwarden ----------------------------------------------------------------------------------
const damp = (e, k) => { e.vx *= k; e.vz *= k; };
BEHAVIOUR.warden = function (e, world) {
  const B = BOSS.warden, fast = e.phase === 2 ? 0.8 : 1;
  if (bossTick(e, world, B)) { enemyPhysics(e); return; }
  const p = nearestPlayer(e, world, 60); e.target = p;
  const s = e.state, A = e.atk;
  if (s === 'intro') {
    // Dropped into the lock from above: the landing throws a shockwave ring
    enemyPhysics(e);
    if (e.onGround && !e.landed) { e.landed = true; world.spawnShockwave(e, 12); world.emit('bossSlam', { e, x: e.x, y: e.y, z: e.z, big: true }); }
    if (e.landed && e.st >= 110) { e.invuln = 0; setState(e, 'idle'); e.cd = 30; }
    return;
  }
  if (s === 'roar') { damp(e, 0.8); enemyPhysics(e); if (e.st >= 80) { setState(e, 'idle'); e.cd = 16; } return; }
  if (s === 'dazed') { damp(e, 0.85); enemyPhysics(e); if (e.st >= B.daze) { setState(e, 'idle'); e.cd = 20; } return; }
  if (s === 'idle' || s === 'approach') {
    if (!p) { damp(e, 0.8); enemyPhysics(e); return; }
    const dist = hdist(e, p), u = faceTo(e, p), w = B.walk * (e.phase === 2 ? 1.3 : 1);
    if (dist > 3.2) { e.vx = approach(e.vx, u.x * w, 0.35); e.vz = approach(e.vz, u.z * w, 0.35); } else damp(e, 0.7);
    if (e.cd === 0 && e.onGround) {
      const opts = dist < 4.4 ? [['sweep', 4], ['hammer', 3], ['stomp', 2]] : dist < 11 ? [['charge', 3], ['missiles', 3], ['stomp', 2]] : [['missiles', 4], ['charge', 3]];
      if (e.phase === 2) opts.push(['laser', 3]);
      startWarden(e, world, pick(e, opts));
    }
    enemyPhysics(e); return;
  }
  if (!A) { setState(e, 'idle'); enemyPhysics(e); return; }
  if (s === 'windup') {
    damp(e, 0.6);
    if (p && A.kind !== 'laser' && A.kind !== 'charge') faceTo(e, p);
    if (A.kind === 'laser') { A.ang = Math.atan2(fz(e), e.facing); A.span = laserSpan(e, e.facing, fz(e), e.y, A.high ? B.laser.high : B.laser.low, B.laser.range); }
    if (e.st >= A.wind) {
      if (A.kind === 'stomp') { hop(e, p, B); setState(e, 'jump'); }
      else if (A.kind === 'missiles') { fireMissiles(e, world, B.missiles); setState(e, 'recover'); A.rec = B.missiles.rec; }
      else if (A.kind === 'charge') { setState(e, 'charge'); world.emit('chargeStart', { e }); }
      else if (A.kind === 'laser') { setState(e, 'laser'); world.emit('bossLaser', { e, ticks: B.laser.ticks, high: A.high }); }
      else setState(e, 'attack');
    }
    enemyPhysics(e); return;
  }
  if (s === 'attack') {
    const S = A.kind === 'hammer' ? B.hammer : B.sweep;
    world.spawnHitbox({ owner: e, team: 'e', ...fwdBox(e, 0.3 + S.reach / 2, S.reach, A.kind === 'hammer' ? 0 : 0.2, A.kind === 'hammer' ? 3.0 : 2.6, S.reach + 0.6),
      dmg: S.dmg, kb: [e.facing * S.kb[0], S.kb[1], fz(e) * S.kb[0]], heavy: A.kind === 'hammer', instance: A.inst, cat: A.kind === 'hammer' ? 'heavy' : 'standard' });
    if (A.kind === 'hammer' && e.st === 2) world.emit('bossSlam', { e, x: e.x + e.facing * 2.6, y: e.y, z: e.z + fz(e) * 2.6 });
    if (e.st >= S.active) { setState(e, 'recover'); A.rec = S.rec; }
    damp(e, 0.5); enemyPhysics(e); return;
  }
  if (s === 'jump') {
    enemyPhysics(e);
    if (e.onGround && e.st > 3) {
      world.spawnShockwave(e, B.stomp.dmg, 1.3);
      world.emit('bossSlam', { e, x: e.x, y: e.y, z: e.z, big: true });
      if (--A.hops > 0) { hop(e, p, B); e.st = 0; }
      else { setState(e, 'recover'); A.rec = B.stomp.rec; e.vx = 0; e.vz = 0; }
    }
    return;
  }
  if (s === 'charge') {
    e.vx = e.facing * B.charge.speed; e.vz = fz(e) * B.charge.speed;
    world.spawnHitbox({ owner: e, team: 'e', ...fwdBox(e, 1.15, 1.5, 0.1, 2.8, 2.4), dmg: B.charge.dmg, heavy: true,
      kb: [e.facing * 13, 6, fz(e) * 13], instance: A.inst, cat: 'heavy' });
    enemyPhysics(e);
    if (e.hitWall) { setState(e, 'dazed'); e.vx = -e.facing * 3; e.vz = -fz(e) * 3; world.emit('chargeCrash', { e }); world.emit('bossSlam', { e, x: e.x + e.facing * 1.1, y: e.y, z: e.z + fz(e) * 1.1 }); }
    else if (e.st >= B.charge.maxTicks) { setState(e, 'recover'); A.rec = B.charge.rec; }
    return;
  }
  if (s === 'laser') {
    // The beam sweeps round it at ankle (jump it) or chest height (crouch or slide under it). It is swept in
    // three steps a tick so it can't pass over anyone between ticks.
    const L = B.laser, band = A.high ? L.high : L.low, step = L.turn * Math.PI * 2 / L.ticks;
    for (let k = 1; k <= 3; k++) {
      const a = A.ang + step * k / 3, span = laserSpan(e, Math.cos(a), Math.sin(a), e.y, band, L.range);
      world.spawnHitbox({ owner: e, team: 'e', seg: span, x0: 0, x1: 0, y0: 0, y1: 0, dmg: L.dmg,
        kb: [Math.cos(a) * 6, 5, Math.sin(a) * 6], unblockable: true, cat: 'unblockable', instance: A.inst });
      if (k === 3) A.span = span;
    }
    A.ang += step; setFacing(e, Math.cos(A.ang), Math.sin(A.ang));
    e.vx = 0; e.vz = 0; enemyPhysics(e);
    if (e.st >= L.ticks) { setState(e, 'recover'); A.rec = L.rec; }
    return;
  }
  if (s === 'recover') {
    damp(e, 0.8); enemyPhysics(e);
    // A perfect parry of the hammer (or any heavy blow) leaves it dazed
    if (e.parried === 2) { e.parried = 0; setState(e, 'dazed'); world.emit('bossDazed', { e, x: e.x, y: e.y + e.h * 0.7, z: e.z }); return; }
    if (e.st >= (A.rec || 30) * fast) { e.atk = null; e.parried = 0; setState(e, 'idle'); e.cd = B.cd[e.phase - 1]; }
    return;
  }
  setState(e, 'idle'); enemyPhysics(e);
};
// The stomp's hop: up, and toward the target as far as it can carry
function hop(e, p, B) {
  e.vy = B.stomp.jump; e.onGround = false;
  if (!p) { e.vx = 0; e.vz = 0; return; }
  let vx = (p.x - e.x) * 0.9, vz = (p.z - e.z) * 0.9; const m = Math.hypot(vx, vz);
  if (m > B.stomp.hop) { vx *= B.stomp.hop / m; vz *= B.stomp.hop / m; }
  e.vx = vx; e.vz = vz;
}

function startWarden(e, world, k) {
  const B = BOSS.warden, fast = e.phase === 2 ? 0.8 : 1;
  e.vx = 0; e.vz = 0;
  if (k === 'sweep') begin(e, world, k, 'standard', Math.round(B.sweep.wind * fast));
  else if (k === 'hammer') begin(e, world, k, 'heavy', Math.round(B.hammer.wind * fast));
  else if (k === 'stomp') begin(e, world, k, 'unblockable', Math.round(B.stomp.wind * fast), { hops: e.phase === 2 ? 2 : 1 });
  else if (k === 'missiles') begin(e, world, k, 'standard', Math.round(B.missiles.wind * fast));
  else if (k === 'charge') begin(e, world, k, 'heavy', Math.round(B.charge.wind * fast));
  else { e.laserHigh = !e.laserHigh; begin(e, world, k, 'unblockable', Math.round(B.laser.wind * fast), { high: e.laserHigh }); }
}

// Missiles lob up off its back and come down on the players (with some spread): standard shots, so they
// can be parried, deflected, shot down or erased by the beam
function fireMissiles(e, world, M) {
  const n = M.n[e.phase - 1], targets = world.players.filter(canTarget);
  for (let i = 0; i < n; i++) {
    const t = targets.length ? targets[i % targets.length] : null, k = i - (n - 1) / 2;
    const sx = e.x - e.facing * 0.5 + k * 0.25, sy = e.y + e.h + 0.1, sz = e.z - fz(e) * 0.5;
    const ox = (t ? t.x : e.x + e.facing * 6), oz = (t ? t.z : e.z + fz(e) * 6);
    const tx = ox + k * 1.1, tz = oz + ((i % 2) ? 1 : -1) * Math.min(1.6, Math.abs(k) * 0.9);
    const ty = Math.max(groundBelow(tx, (t ? t.y : e.y) + 1, tz), (t ? t.y : e.y) - 6) + 0.6;
    const T = clamp(1.0 + Math.hypot(tx - sx, tz - sz) / 24 + i * 0.07, 1.0, 1.7), g = M.gravity;
    world.spawnProjectile({ team: 'e', owner: e, x: sx, y: sy, z: sz, vx: (tx - sx) / T, vy: (ty - sy + 0.5 * g * T * T) / T, vz: (tz - sz) / T, gravity: g, r: 0.26, dmg: M.dmg,
      kind: 'missile', ttl: 240 });
  }
  world.emit('bossMissiles', { e, n });
}

// ---- Stormcaller ---------------------------------------------------------------------------------
const PAD_Z = ARENA_HW - 1.5;   // how far across the pad it hovers
BEHAVIOUR.stormcaller = function (e, world) {
  const B = BOSS.stormcaller, fast = e.phase === 2 ? 0.8 : 1, floor = B.floor;
  if (bossTick(e, world, B)) { e.vx *= 0.9; e.vy *= 0.9; e.vz *= 0.9; enemyPhysics(e); return; }
  const p = nearestPlayer(e, world, 60); e.target = p;
  const s = e.state, A = e.atk, bob = Math.sin(world.tick * 0.04) * 0.4;
  const home = (tx, ty, tz, k = 1.6, max = B.speed) => {
    e.vx = approach(e.vx, clamp((tx - e.x) * k, -max, max), 0.45); e.vy = approach(e.vy, clamp((ty - e.y) * k, -max, max), 0.45);
    e.vz = approach(e.vz, clamp((tz - e.z) * k, -max, max), 0.45);
  };
  if (s === 'intro') {
    home(e.homeX, floor + B.hover, e.homeZ || 0, 1.2, 5);
    if (e.st >= 110) { e.invuln = 0; setState(e, 'idle'); e.cd = 30; }
    enemyPhysics(e); return;
  }
  if (s === 'roar') {
    e.vx *= 0.9; e.vy *= 0.9; e.vz *= 0.9;
    if (e.st === 30) {
      // Phase two: it calls in drones
      for (let i = 0; i < B.drones; i++) {
        const d = createEnemy('drone', e.x + (i ? 4 : -4), e.y + 1.5, { z: e.z + (i ? -2 : 2), zone: e.zone, enc: e.enc, cd: 60 + i * 30, add: true });
        world.enemies.push(d);
      }
      world.emit('bossCall', { e });
    }
    if (e.st >= 80) { setState(e, 'idle'); e.cd = 16; }
    enemyPhysics(e); return;
  }
  if (s === 'crashed') {
    // Down on the pad: open to everything until it lifts off
    damp(e, 0.85); e.vy = Math.max(e.vy - GRAVITY * DT, -12);
    enemyPhysics(e);
    if (e.st >= e.crashFor) { setState(e, 'rise'); }
    return;
  }
  if (s === 'rise') { home(e.x, floor + B.hover, e.z, 1.4, 6); enemyPhysics(e); if (e.st >= B.dive.rise) { setState(e, 'idle'); e.cd = 16; } return; }
  if (s === 'idle') {
    if (!p) { home(e.homeX, floor + B.hover + bob, e.homeZ || 0); enemyPhysics(e); return; }
    faceTo(e, p);
    // Hover above the pad off to one side of the target
    const u = away(p, e, e), tx = clamp(p.x + u.x * 4.5, B.padX[0], B.padX[1]), tz = clamp(p.z + u.z * 4.5, -PAD_Z, PAD_Z);
    home(tx, floor + B.hover + bob, tz);
    if (e.cd === 0) startStorm(e, world, pick(e, [['volley', 4], ['rain', 3], ['sweep', 3], ['dive', 3]]));
    enemyPhysics(e); return;
  }
  if (!A) { setState(e, 'idle'); enemyPhysics(e); return; }
  if (s === 'reposition') {
    const tx = B.padX[A.edge], ty = floor + B.sweep.hoverY;
    home(tx, ty, 0, 2.2, 9);
    setFacing(e, A.edge === 0 ? 1 : -1, 0);
    if ((Math.abs(e.x - tx) < 0.4 && Math.abs(e.y - ty) < 0.4) || e.st > 90) { setState(e, 'windup'); world.telegraph(e, 'unblockable', A.wind); }
    enemyPhysics(e); return;
  }
  if (s === 'windup') {
    if (A.kind === 'sweep') { home(B.padX[A.edge], floor + B.sweep.hoverY, 0, 2, 4); A.span = sheetSpan(e, e.facing > 0 ? 1 : -1, floor, A.high ? B.sweep.high : B.sweep.low, 30); }
    else { e.vx *= 0.9; e.vy *= 0.9; e.vz *= 0.9; if (p) faceTo(e, p); }
    if (A.kind === 'dive' && p) { A.tx = p.x; A.ty = p.y + 0.6; A.tz = p.z; }
    if (e.st >= A.wind) {
      if (A.kind === 'volley') { setState(e, 'volley'); A.n = 0; }
      else if (A.kind === 'rain') { fireRain(e, world, B.rain); setState(e, 'recover'); A.rec = B.rain.rec; }
      else if (A.kind === 'sweep') { setState(e, 'laser'); world.emit('bossLaser', { e, ticks: B.sweep.ticks, high: A.high }); }
      else {
        const dx = A.tx - e.x, dy = A.ty - (e.y + e.h / 2), dz = (A.tz ?? e.z) - e.z, m = Math.hypot(dx, dy, dz) || 1;
        A.dx = dx / m; A.dy = dy / m; A.dz = dz / m; setState(e, 'dive'); world.emit('bossDive', { e });
      }
    }
    enemyPhysics(e); return;
  }
  if (s === 'volley') {
    e.vx *= 0.9; e.vy *= 0.9; e.vz *= 0.9;
    if (e.st % B.volley.every === 1 && p && canTarget(p)) {
      const sx = e.x + e.facing * 1.2, sy = e.y + 0.3, sz = e.z + fz(e) * 1.2;
      const dx = p.x - sx, dy = p.y + 1 - sy, dz = p.z - sz, m = Math.hypot(dx, dy, dz) || 1;
      for (const d of [-1, 1]) {
        const [vx, vy, vz] = spreadDir(dx / m, dy / m, dz / m, d * B.volley.spread);
        world.spawnProjectile({ team: 'e', owner: e, x: sx, y: sy + d * 0.25, z: sz, vx: vx * B.volley.speed, vy: vy * B.volley.speed, vz: vz * B.volley.speed, r: 0.2, dmg: B.volley.dmg, kind: 'std', ttl: 150 });
      }
      world.emit('enemyShot', { e, heavy: false });
      if (++A.n >= A.shots) { setState(e, 'recover'); A.rec = B.volley.rec; }
    }
    enemyPhysics(e); return;
  }
  if (s === 'laser') {
    home(B.padX[A.edge], floor + B.sweep.hoverY, 0, 2, 3);
    A.span = sheetSpan(e, e.facing > 0 ? 1 : -1, floor, A.high ? B.sweep.high : B.sweep.low, 30);
    const L = A.span;
    world.spawnHitbox({ owner: e, team: 'e', x0: L.x0, x1: L.x1, y0: L.y0, y1: L.y1, z0: L.z0, z1: L.z1, dmg: B.sweep.dmg,
      kb: [e.facing * 6, 5, 0], unblockable: true, cat: 'unblockable', instance: A.inst });
    enemyPhysics(e);
    if (e.st >= B.sweep.ticks) { setState(e, 'recover'); A.rec = B.sweep.rec; A.low = true; }
    return;
  }
  if (s === 'dive') {
    e.vx = A.dx * B.dive.speed; e.vy = A.dy * B.dive.speed; e.vz = (A.dz || 0) * B.dive.speed;
    const hm = Math.hypot(A.dx, A.dz || 0), kx = hm > 0.05 ? A.dx / hm : e.facing, kz = hm > 0.05 ? (A.dz || 0) / hm : fz(e);
    world.spawnHitbox({ owner: e, team: 'e', ...boxAt(e.x, e.z, e.w / 2 + 0.2, e.y - 0.2, e.y + e.h), dmg: B.dive.dmg, heavy: true,
      kb: [kx * 12, 6, kz * 12], instance: A.inst, cat: 'heavy' });
    enemyPhysics(e);
    const hitFloor = e.onGround || e.hitWall || e.y <= floor + 0.05;   // the pad, a perch or a wall
    if (e.parried === 2 || hitFloor || e.st >= B.dive.maxTicks) {
      const parried = e.parried === 2; e.parried = 0;
      if (parried || hitFloor) { e.crashFor = parried ? B.dive.parried : B.dive.crash; setState(e, 'crashed'); world.emit('bossCrash', { e, x: e.x, y: e.y, z: e.z, parried }); }
      else { setState(e, 'rise'); }
    }
    return;
  }
  if (s === 'recover') {
    if (A.low) { e.vx *= 0.85; e.vy *= 0.85; e.vz *= 0.85; } else home(e.x, floor + B.hover + bob, e.z, 1, 3);
    enemyPhysics(e);
    if (e.parried === 2) { e.parried = 0; e.crashFor = B.dive.parried; setState(e, 'crashed'); world.emit('bossCrash', { e, x: e.x, y: e.y, z: e.z, parried: true }); return; }
    if (e.st >= (A.rec || 30) * fast) { e.atk = null; e.parried = 0; setState(e, A.low ? 'rise' : 'idle'); e.cd = B.cd[e.phase - 1]; }
    return;
  }
  setState(e, 'idle'); enemyPhysics(e);
};

function startStorm(e, world, k) {
  const B = BOSS.stormcaller, fast = e.phase === 2 ? 0.8 : 1;
  if (k === 'volley') begin(e, world, k, 'standard', Math.round(B.volley.wind * fast), { shots: B.volley.bursts[e.phase - 1] });
  else if (k === 'rain') begin(e, world, k, 'unblockable', Math.round(B.rain.wind * fast));
  else if (k === 'dive') begin(e, world, k, 'heavy', Math.round(B.dive.wind * fast));
  else {
    // The sweep: it drops low at one edge of the pad, then fires a sheet of light across it at ankle height (jump
    // it) or, in phase two, alternately at chest height (crouch or slide under it)
    const edge = Math.abs(e.x - B.padX[0]) < Math.abs(e.x - B.padX[1]) ? 0 : 1;
    e.laserHigh = e.phase === 2 ? !e.laserHigh : false;
    e.atk = { kind: 'sweep', inst: world.newInstance(), edge, high: e.laserHigh, wind: Math.round(B.sweep.wind * fast) }; e.lastAtk = 'sweep';
    setState(e, 'reposition');
  }
}
// Tests and tools: start a named attack now, the same way the boss would choose it
export function forceBossAttack(world, e, kind) { e.cd = 0; if (e.type === 'warden') startWarden(e, world, kind); else startStorm(e, world, kind); }

// Shells rained across the pad (unblockable bursts, with landing markers): one on each player, the rest spread
function fireRain(e, world, R) {
  const n = R.n[e.phase - 1], B = BOSS.stormcaller, targets = world.players.filter(canTarget);
  for (let i = 0; i < n; i++) {
    const t = targets[i];
    const tx = t && i < targets.length ? t.x : B.padX[0] + (B.padX[1] - B.padX[0]) * ((i + 0.5) / n) + (Math.random() - 0.5);
    const tz = t && i < targets.length ? t.z : (Math.random() * 2 - 1) * PAD_Z;
    const ty = Math.max(groundBelow(tx, B.floor + 2, tz), B.floor - 6), sx = e.x + (i - n / 2) * 0.3, sy = e.y + e.h * 0.2, sz = e.z;
    const T = 0.95 + i * 0.12, g = R.gravity;
    world.spawnProjectile({ team: 'e', owner: e, x: sx, y: sy, z: sz, vx: (tx - sx) / T, vy: (ty + 0.2 - sy + 0.5 * g * T * T) / T, vz: (tz - sz) / T, gravity: g, r: 0.3, dmg: 0, heavy: true,
      kind: 'mortar', ttl: 300, blast: { ...R.blast } });
    world.emit('mortarShot', { e, x: tx, y: ty, z: tz, r: R.blast.r, ticks: Math.round(T * 60) });
  }
}

// A boss for an encounter: scaled for the number of players, arriving on its intro
export function spawnBoss(world, type, x, y, extra = {}) {
  const B = BOSS[type], n = Math.max(1, world.players.length);
  const e = createEnemy(type, x, y, { ...extra, boss: true, phase: 1, invuln: 999, staggerCd: 0, parried: 0, cd: 60, facing: -1, homeX: x, homeY: y, homeZ: extra.z || 0 });
  e.hp = e.maxHp = Math.round(B.hp * (1 + 0.6 * (n - 1)));
  e.state = 'intro'; e.st = 0;
  world.enemies.push(e);
  world.emit('bossIntro', { e, name: B.name, title: B.title });
  return e;
}
