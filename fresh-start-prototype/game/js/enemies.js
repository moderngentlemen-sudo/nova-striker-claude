// Enemy archetypes and behaviour. Every attack announces its category through the world's
// telegraph events: 'standard' (parryable), 'heavy' (perfect parry to fully negate), 'unblockable'.
import { DT, GRAVITY, MAX_FALL, HUNTER, SCARF } from './config.js';
import { moveBody, segmentBlocked, groundBelow, KILL_Y, killYAt } from './level.js';

export const ENEMY_TYPES = {
  swarmer: { w: 0.7, h: 0.8, hp: 3, poise: 18, speed: 5.2, flinch: true, light: true },
  shield:  { w: 0.95, h: 1.85, hp: 8, poise: 55, speed: 2.6, flinch: true, light: true },
  sniper:  { w: 0.8, h: 1.7, hp: 4, poise: 25, speed: 0, flinch: true, light: true, stationary: true },
  brute:   { w: 1.5, h: 2.5, hp: 45, poise: 100, speed: 2.1, flinch: false, light: false, armor: 3 },
  post:    { w: 0.9, h: 2.1, hp: Infinity, poise: 60, speed: 0, flinch: false, light: false, stationary: true },
  turret:  { w: 0.8, h: 0.8, hp: Infinity, poise: 999, speed: 0, flinch: false, light: false, stationary: true, noGravity: true },
  // Skyline Relay additions
  drone:   { w: 0.8, h: 0.6, hp: 3, poise: 16, speed: 5, flinch: true, light: true, flier: true },          // flies, shoots from above
  mortar:  { w: 1.1, h: 1.2, hp: 8, poise: 40, speed: 0, flinch: false, light: false, stationary: true },  // lobs shells that burst
  charger: { w: 1.2, h: 1.5, hp: 12, poise: 60, speed: 3.2, flinch: false, light: false, armor: 1 },       // armored rushing charge
};
export const MORTAR = { range: 24, minRange: 3, wind: 36, gravity: 26, blast: { r: 1.9, dmg: 14 }, cd: 170 };
export const CHARGER = { wind: 34, speed: 15, maxTicks: 48, dmg: 16, daze: 80, cd: 100, minRange: 3, maxRange: 13 };

let nextId = 1;
export function createEnemy(type, x, y, extra = {}) {
  const T = ENEMY_TYPES[type];
  return {
    kind: 'enemy', id: nextId++, type, x, y, vx: 0, vy: 0, w: T.w, h: T.h, prevX: x, prevY: y,
    facing: -1, shieldDir: -1, onGround: false, hp: T.hp, maxHp: T.hp, poise: 0, poiseMax: T.poise,
    armor: T.armor || 0, armorMax: T.armor || 0,
    state: 'idle', st: 0, atk: null, token: null, target: null, cd: 30 + Math.floor(Math.random() * 40),
    hitstop: 0, flash: 0, dead: false, deathT: 0, tagged: 0, stun: 0, slamCd: 120, cycle: 0,
    aimX: 0, aimY: 0, label: '', light: T.light, flier: !!T.flier, boss: !!T.boss, homeX: x, homeY: y, ...extra,
  };
}

// Enemies cannot see a player hidden by Veil. A Challenge taunt holds their attention, and a
// flaring Echo in range draws them away from nearer teammates.
export function canTarget(p) { return p.state !== 'downed' && p.state !== 'dead' && !p.veiled; }

export function nearestPlayer(e, world, maxD = 40) {
  if (e.tauntT > 0 && e.taunter && canTarget(e.taunter)) return e.taunter;
  let best = null, bd = maxD, flare = null, fd = Math.min(maxD, SCARF.flareRange);
  for (const p of world.players) {
    if (!canTarget(p)) continue;
    const d = Math.hypot(p.x - e.x, (p.y + 0.9) - (e.y + e.h / 2));
    if (d < bd) { bd = d; best = p; }
    if (p.char === 'echo' && p.scarfMode === 'flare' && d < fd) { fd = d; flare = p; }
  }
  return flare || best;
}

function setState(e, s) { e.state = s; e.st = 0; }

function releaseToken(e, world) { if (e.token) { world.director.release(e); } }

export function enemyPhysics(e) { physics(e); }
function physics(e) {
  const T = ENEMY_TYPES[e.type];
  if (T.flier) { e.vx *= 0.93; e.vy *= 0.93; moveBody(e, DT); return; }   // drones drift to a stop
  if (T.noGravity) return;
  e.vy -= GRAVITY * DT;
  if (e.vy < -MAX_FALL) e.vy = -MAX_FALL;
  moveBody(e, DT);
}

export function updateEnemy(e, world) {
  e.prevX = e.x; e.prevY = e.y;
  if (e.flash > 0) e.flash--;
  if (e.dropT > 0) e.dropT--;   // lets grappled enemies fall through one-way platforms
  if (e.tagged > 0) e.tagged--;
  if (e.tauntT > 0) e.tauntT--;
  if (e.dead) {
    e.deathT++;
    if (e.boss && !e.onGround) { e.vy = Math.max(e.vy - GRAVITY * DT, -14); e.vx *= 0.95; moveBody(e, DT); }   // a downed gunship falls onto the pad
    return;
  }
  if (e.y < killYAt(e.x)) {   // fell out of the level (a charge off a ledge, a knockback over the edge)
    e.dead = true; e.deathT = 0; e.hp = 0; world.director.release(e);
    world.emit('kill', { x: e.x, y: e.y, e, owner: null });
    return;
  }
  if (e.shockT > 0) e.shockT--;
  if (e.wellT > 0) e.wellT--;
  // Slowed by Nova's perfect dodge: it only acts every other tick
  if (e.slowT > 0) { e.slowT--; if (e.slowT % 2) return; }
  if (e.hitstop > 0) { e.hitstop--; return; }
  e.st++;
  if (e.cd > 0) e.cd--;
  if (e.slamCd > 0) e.slamCd--;

  // Shared interrupt states
  if (e.state === 'stagger' || e.state === 'hitstun') {
    if (e.onGround) e.vx *= 0.85;
    physics(e);
    if (e.st >= e.stun) { setState(e, 'idle'); e.dizzy = false; }
    return;
  }
  e.dizzy = false;
  if (e.state === 'launched') {
    physics(e);
    if ((e.onGround || e.flier) && e.st > 6) { e.stun = 20; setState(e, 'hitstun'); }
    return;
  }
  if (e.state === 'caught') {
    const p = e.catcher;
    const leashed = p && p.leash && p.leash.e === e;
    if (e.st <= 8 && p) {
      const tx = p.x + (e.catchSide || p.facing) * (p.w / 2 + e.w / 2 + 0.3), ty = p.y;
      e.vx = (tx - e.x) * 14; e.vy = (ty - e.y) * 14;
      moveBody(e, DT);
    } else if (leashed) {
      // Reeled in on the tether: dragged along, never more than the leash length away
      const dx = e.x - p.x, d = Math.abs(dx), L = HUNTER.leashLen;
      if (d > L) e.vx = p.vx + (p.x + Math.sign(dx) * L - e.x) * 20; else e.vx *= 0.8;
      physics(e);
      e.st = Math.min(e.st, 12);
    } else { e.vx *= 0.8; physics(e); }
    if (e.st >= 28) setState(e, 'idle');
    return;
  }
  if (e.state === 'snared') {
    e.vx = 0; physics(e);
    if (e.st >= e.stun) setState(e, 'idle');
    return;
  }
  if (e.state === 'plowed') {
    // Scooped up by RAM's charge: he carries it (world.ramPlow); if he lets go without throwing it, it drops
    const p = e.plowBy;
    if (!p || (p.state !== 'rush' && p.state !== 'ult') || e.st > 150) { e.plowBy = null; setState(e, 'hitstun'); e.stun = 16; physics(e); }
    return;
  }

  const fn = BEHAVIOUR[e.type];
  const before = e.target;
  if (fn) fn(e, world);
  if (before && before.kind === 'player' && before.veiled && e.target !== before) world.emit('lostTrack', { e, p: before });
}

// ---- Behaviours ------------------------------------------------------------------------

function walkToward(e, target, speed, stopDist) {
  const dx = target.x - e.x;
  e.facing = dx >= 0 ? 1 : -1;
  if (Math.abs(dx) > stopDist) e.vx = e.facing * speed;
  else e.vx *= 0.7;
}

const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const approach = (v, t, d) => (v < t ? Math.min(v + d, t) : Math.max(v - d, t));

export const BEHAVIOUR = {
  swarmer(e, world) {
    const p = nearestPlayer(e, world, 18);
    e.target = p;
    if (e.state === 'idle' || e.state === 'approach') {
      if (!p) { e.vx *= 0.8; physics(e); return; }
      walkToward(e, p, ENEMY_TYPES.swarmer.speed, 1.7);
      const close = Math.abs(p.x - e.x) < 2.1 && Math.abs(p.y - e.y) < 1.4;
      if (close && e.cd === 0 && world.director.request(e, 'melee')) {
        setState(e, 'windup'); e.vx = 0; world.telegraph(e, 'standard', 20);
      } else e.state = 'approach';
    } else if (e.state === 'windup') {
      e.vx *= 0.7;
      if (p) e.facing = p.x >= e.x ? 1 : -1;
      if (e.st >= 20) { setState(e, 'attack'); e.atk = { inst: world.newInstance() }; }
    } else if (e.state === 'attack') {
      e.vx = e.facing * 10;
      const x0 = e.facing > 0 ? e.x : e.x - 1.1;
      world.spawnHitbox({ owner: e, team: 'e', x0, x1: x0 + 1.1, y0: e.y, y1: e.y + 0.9, dmg: 8, kb: [e.facing * 5, 3], instance: e.atk.inst, cat: 'standard' });
      if (e.st >= 10) setState(e, 'recover');
    } else if (e.state === 'recover') {
      e.vx *= 0.8;
      if (e.st >= 26) { releaseToken(e, world); e.cd = 40 + Math.floor(Math.random() * 40); setState(e, 'idle'); }
    }
    physics(e);
  },

  shield(e, world) {
    const p = nearestPlayer(e, world, 22);
    e.target = p;
    // The shield turns slowly, so flanking and attacking from above work
    if (p && e.st % 36 === 0 && e.state !== 'attack') e.shieldDir = p.x >= e.x ? 1 : -1;
    if (e.state === 'idle' || e.state === 'approach') {
      if (!p) { e.vx *= 0.8; physics(e); return; }
      const dx = p.x - e.x;
      e.facing = e.shieldDir;
      if (Math.abs(dx) > 2.2 && Math.sign(dx) === e.shieldDir) e.vx = e.shieldDir * ENEMY_TYPES.shield.speed;
      else e.vx *= 0.7;
      const close = Math.abs(dx) < 2.5 && Math.abs(p.y - e.y) < 1.6 && Math.sign(dx) === e.shieldDir;
      if (close && e.cd === 0 && world.director.request(e, 'melee')) {
        setState(e, 'windup'); world.telegraph(e, 'standard', 22);
      }
    } else if (e.state === 'windup') {
      e.vx *= 0.6;
      if (e.st >= 22) { setState(e, 'attack'); e.atk = { inst: world.newInstance() }; }
    } else if (e.state === 'attack') {
      e.vx = e.shieldDir * 8;
      const x0 = e.shieldDir > 0 ? e.x : e.x - 1.3;
      world.spawnHitbox({ owner: e, team: 'e', x0, x1: x0 + 1.3, y0: e.y + 0.2, y1: e.y + 1.8, dmg: 12, kb: [e.shieldDir * 7, 3], instance: e.atk.inst, cat: 'standard' });
      if (e.st >= 8) setState(e, 'recover');
    } else if (e.state === 'recover') {
      e.vx *= 0.75;
      if (e.st >= 34) { releaseToken(e, world); e.cd = 60 + Math.floor(Math.random() * 40); setState(e, 'idle'); }
    }
    physics(e);
  },

  sniper(e, world) {
    const p = nearestPlayer(e, world, 24);
    if (e.state === 'idle') {
      e.target = p;
      if (p && e.cd === 0) {
        const c = { x: e.x, y: e.y + 1.4 };
        if (!segmentBlocked(c.x, c.y, p.x, p.y + 1) && world.director.request(e, 'ranged')) {
          setState(e, 'aim'); world.telegraph(e, 'heavy', 64);
        }
      }
    } else if (e.state === 'aim') {
      const t = e.target;
      if (!t || !canTarget(t)) { releaseToken(e, world); e.target = null; setState(e, 'idle'); physics(e); return; }
      e.aimX = t.x; e.aimY = t.y + 1.0;
      e.facing = t.x >= e.x ? 1 : -1;
      if (e.st >= 48) { setState(e, 'lock'); world.emit('lock', { e }); }
    } else if (e.state === 'lock') {
      if (e.st >= 16) {
        const sx = e.x + e.facing * 0.5, sy = e.y + 1.4;
        const dx = e.aimX - sx, dy = e.aimY - sy, d = Math.hypot(dx, dy) || 1;
        world.spawnProjectile({ team: 'e', owner: e, x: sx, y: sy, vx: dx / d * 24, vy: dy / d * 24, r: 0.28, dmg: 18, heavy: true, kind: 'heavy', ttl: 90 });
        world.emit('enemyShot', { e, heavy: true });
        setState(e, 'recover');
      }
    } else if (e.state === 'recover') {
      if (e.st >= 30) { releaseToken(e, world); e.cd = 140; setState(e, 'idle'); }
    }
    physics(e);
  },

  brute(e, world) {
    const p = nearestPlayer(e, world, 30);
    e.target = p;
    if (e.state === 'idle' || e.state === 'approach') {
      if (!p) { e.vx *= 0.8; physics(e); return; }
      const dist = Math.abs(p.x - e.x);
      walkToward(e, p, ENEMY_TYPES.brute.speed, 2.0);
      if (e.cd === 0) {
        if (dist > 3.2 && dist < 8.5 && e.slamCd === 0 && world.director.request(e, 'melee')) {
          setState(e, 'slamWindup'); e.vx = 0; world.telegraph(e, 'unblockable', 44);
        } else if (dist < 2.8 && Math.abs(p.y - e.y) < 2 && world.director.request(e, 'melee')) {
          setState(e, 'windup'); e.vx = 0; world.telegraph(e, 'heavy', 26);
        }
      }
    } else if (e.state === 'windup') {
      e.vx *= 0.6;
      if (e.st >= 26) { setState(e, 'attack'); e.atk = { inst: world.newInstance() }; }
    } else if (e.state === 'attack') {
      const x0 = e.facing > 0 ? e.x + 0.2 : e.x - 2.8;
      world.spawnHitbox({ owner: e, team: 'e', x0, x1: x0 + 2.6, y0: e.y + 0.3, y1: e.y + 2.2, dmg: 20, kb: [e.facing * 10, 5], heavy: true, instance: e.atk.inst, cat: 'heavy' });
      if (e.st >= 6) setState(e, 'recover');
    } else if (e.state === 'slamWindup') {
      if (e.st >= 44) {
        world.spawnShockwave(e, 1, 22); world.spawnShockwave(e, -1, 22);
        world.emit('slam', { e }); setState(e, 'slamRecover');
      }
    } else if (e.state === 'recover' || e.state === 'slamRecover') {
      e.vx *= 0.8;
      const len = e.state === 'recover' ? 38 : 50;
      if (e.st >= len) {
        releaseToken(e, world);
        if (e.state === 'slamRecover') e.slamCd = 300;
        e.cd = 30 + Math.floor(Math.random() * 30); setState(e, 'idle');
      }
    }
    physics(e);
  },

  post(e, world) {
    const p = nearestPlayer(e, world, 3.6);
    const pattern = ['standard', 'heavy', 'unblockable'];
    if (e.state === 'idle') {
      e.label = '';
      if (p && e.cd === 0) {
        e.facing = p.x >= e.x ? 1 : -1;
        const cat = pattern[e.cycle % 3]; e.cycle++;
        const wind = cat === 'standard' ? 24 : cat === 'heavy' ? 30 : 44;
        e.atk = { cat, wind, inst: world.newInstance() };
        e.label = cat === 'standard' ? 'Standard: parry it' : cat === 'heavy' ? 'Heavy: perfect-parry it' : 'Unblockable: jump it';
        setState(e, 'windup'); world.telegraph(e, cat, wind);
      }
    } else if (e.state === 'windup') {
      if (e.st >= e.atk.wind) {
        if (e.atk.cat === 'unblockable') { world.spawnShockwave(e, 1, 20, 0.6); world.spawnShockwave(e, -1, 20, 0.6); world.emit('slam', { e }); }
        setState(e, 'attack');
      }
    } else if (e.state === 'attack') {
      if (e.atk.cat !== 'unblockable') {
        const x0 = e.facing > 0 ? e.x : e.x - 2.0;
        world.spawnHitbox({ owner: e, team: 'e', x0, x1: x0 + 2.0, y0: e.y + 0.4, y1: e.y + 1.9, dmg: e.atk.cat === 'heavy' ? 16 : 8,
          kb: [e.facing * 6, 3], heavy: e.atk.cat === 'heavy', instance: e.atk.inst, cat: e.atk.cat });
      }
      if (e.st >= 5) setState(e, 'recover');
    } else if (e.state === 'recover') {
      if (e.st >= 48) { e.cd = 20; setState(e, 'idle'); }
    }
    physics(e);
  },

  // Drone: hovers above and to one side of its target, bobbing, and fires parryable shots down at it
  drone(e, world) {
    const p = nearestPlayer(e, world, 26);
    e.target = p;
    const sp = ENEMY_TYPES.drone.speed, bob = Math.sin((world.tick + e.id * 37) * 0.05) * 0.5;
    let tx = e.homeX, ty = e.homeY + bob;
    if (p) { const side = e.x >= p.x ? 1 : -1; tx = p.x + side * 5.5; ty = p.y + 3.4 + bob; e.facing = p.x >= e.x ? 1 : -1; }
    if (e.state === 'idle') {
      e.vx = approach(e.vx, clamp((tx - e.x) * 1.6, -sp, sp), 0.35);
      e.vy = approach(e.vy, clamp((ty - e.y) * 1.8, -sp, sp), 0.35);
      if (p && e.cd === 0 && Math.abs(p.x - e.x) < 11 && !segmentBlocked(e.x, e.y + 0.3, p.x, p.y + 1) && world.director.request(e, 'ranged')) {
        setState(e, 'windup'); world.telegraph(e, 'standard', 26);
      }
    } else if (e.state === 'windup') {
      e.vx *= 0.85; e.vy *= 0.85;
      if (e.st >= 26) {
        if (p && canTarget(p)) {
          const sx = e.x + e.facing * 0.4, sy = e.y + 0.25, dx = p.x - sx, dy = p.y + 1 - sy, d = Math.hypot(dx, dy) || 1;
          world.spawnProjectile({ team: 'e', owner: e, x: sx, y: sy, vx: dx / d * 13, vy: dy / d * 13, r: 0.2, dmg: 6, kind: 'std', ttl: 110 });
          world.emit('enemyShot', { e, heavy: false });
        }
        setState(e, 'recover');
      }
    } else if (e.state === 'recover') {
      e.vx *= 0.9; e.vy *= 0.9;
      if (e.st >= 24) { releaseToken(e, world); e.cd = 70 + (e.id * 13) % 50; setState(e, 'idle'); }
    } else setState(e, 'idle');
    physics(e);
  },

  // Mortar: stays put and lobs a heavy shell that bursts where a marker shows on the ground. The burst
  // can't be parried; move out, or shoot the shell down with a charged shot (a Bulwark barrier blocks it).
  mortar(e, world) {
    const p = nearestPlayer(e, world, MORTAR.range);
    if (e.state === 'idle') {
      e.target = p;
      if (p && e.cd === 0 && Math.abs(p.x - e.x) > MORTAR.minRange && world.director.request(e, 'ranged')) {
        e.facing = p.x >= e.x ? 1 : -1;
        setState(e, 'windup'); world.telegraph(e, 'unblockable', MORTAR.wind);
      }
    } else if (e.state === 'windup') {
      const t = e.target;
      if (t) e.facing = t.x >= e.x ? 1 : -1;
      if (e.st >= MORTAR.wind) {
        if (t && canTarget(t)) {
          // Aim where the target stands now; flight time grows with distance
          const sx = e.x + e.facing * 0.5, sy = e.y + 1.35, tx = t.x, ty = Math.max(groundBelow(t.x, t.y + 0.3), t.y - 6);
          const T = clamp(0.9 + Math.abs(tx - sx) / 25, 0.9, 1.5), g = MORTAR.gravity;
          const vx = (tx - sx) / T, vy = (ty + 0.2 - sy + 0.5 * g * T * T) / T;
          world.spawnProjectile({ team: 'e', owner: e, x: sx, y: sy, vx, vy, gravity: g, r: 0.3, dmg: 0, heavy: true, kind: 'mortar',
            ttl: 300, blast: { ...MORTAR.blast } });
          world.emit('mortarShot', { e, x: tx, y: ty, r: MORTAR.blast.r, ticks: Math.round(T * 60) });
        }
        setState(e, 'recover');
      }
    } else if (e.state === 'recover') {
      if (e.st >= 40) { releaseToken(e, world); e.cd = MORTAR.cd; setState(e, 'idle'); }
    } else setState(e, 'idle');
    physics(e);
  },

  // Charger: an armored rusher. It paws the ground (heavy telegraph: a perfect parry negates it), then
  // charges in a straight line. Hitting a wall, or being perfect-parried, leaves it dazed and open.
  charger(e, world) {
    const p = nearestPlayer(e, world, 26);
    e.target = p;
    if (e.state === 'idle' || e.state === 'approach') {
      if (!p) { e.vx *= 0.8; physics(e); return; }
      const dx = p.x - e.x, dist = Math.abs(dx), level = Math.abs(p.y - e.y) < 1.8;
      e.facing = dx >= 0 ? 1 : -1;
      if (dist < CHARGER.minRange + 1) e.vx = -e.facing * ENEMY_TYPES.charger.speed;   // backs off to get a run-up
      else if (dist > CHARGER.maxRange - 2) e.vx = e.facing * ENEMY_TYPES.charger.speed;
      else e.vx *= 0.7;
      e.state = 'approach';
      if (e.cd === 0 && level && dist >= CHARGER.minRange && dist <= CHARGER.maxRange && world.director.request(e, 'melee')) {
        setState(e, 'windup'); e.vx = 0; world.telegraph(e, 'heavy', CHARGER.wind);
      }
    } else if (e.state === 'windup') {
      e.vx *= 0.5;
      if (p) e.facing = p.x >= e.x ? 1 : -1;
      if (e.st >= CHARGER.wind) { setState(e, 'charge'); e.atk = { inst: world.newInstance(), x0: e.x }; world.emit('chargeStart', { e }); }
    } else if (e.state === 'charge') {
      e.vx = e.facing * CHARGER.speed;
      const x0 = e.facing > 0 ? e.x + 0.1 : e.x - 1.4;
      world.spawnHitbox({ owner: e, team: 'e', x0, x1: x0 + 1.3, y0: e.y + 0.1, y1: e.y + 1.4, dmg: CHARGER.dmg, heavy: true,
        kb: [e.facing * 12, 5], instance: e.atk.inst, cat: 'heavy' });
      if (e.hitWall) { setState(e, 'dazed'); e.vx = -e.facing * 3; world.emit('chargeCrash', { e }); }
      else if (e.st >= CHARGER.maxTicks) setState(e, 'recover');
    } else if (e.state === 'dazed') {
      e.vx *= 0.85;
      if (e.st >= CHARGER.daze) { releaseToken(e, world); e.cd = CHARGER.cd; setState(e, 'idle'); }
    } else if (e.state === 'recover') {
      e.vx *= 0.85;
      if (e.st >= 30) { releaseToken(e, world); e.cd = CHARGER.cd; setState(e, 'idle'); }
    }
    physics(e);
  },

  turret(e, world) {
    let target = null, bd = 13;
    for (const p of world.players) {
      if (!canTarget(p) || p.x > e.x + 0.5 || p.x < 44) continue;
      const d = Math.hypot(p.x - e.x, p.y + 1 - e.y);
      if (d < bd) { bd = d; target = p; }
    }
    if (e.state === 'idle') {
      if (target && e.cd === 0) {
        const heavy = e.cycle % 3 === 2; e.cycle++;
        e.atk = { heavy }; e.target = target;
        setState(e, 'windup'); world.telegraph(e, heavy ? 'heavy' : 'standard', 24);
      }
    } else if (e.state === 'windup') {
      if (e.st >= 24) {
        const t = e.target;
        if (t && canTarget(t)) {
          const dx = t.x - e.x, dy = t.y + 1.0 - e.y, d = Math.hypot(dx, dy) || 1;
          const sp = e.atk.heavy ? 14 : 12;
          world.spawnProjectile({ team: 'e', owner: e, x: e.x - 0.5, y: e.y + 0.4, vx: dx / d * sp, vy: dy / d * sp, r: e.atk.heavy ? 0.28 : 0.2,
            dmg: e.atk.heavy ? 14 : 6, heavy: e.atk.heavy, kind: e.atk.heavy ? 'heavy' : 'std', ttl: 120 });
          world.emit('enemyShot', { e, heavy: e.atk.heavy });
        }
        e.cd = 70; setState(e, 'idle');
      }
    }
  },
};
