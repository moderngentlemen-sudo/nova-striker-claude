// Combat resolution: melee hitboxes, projectiles, barriers, shockwaves, damage and parries. Everything here is 3D:
// x along the path, y up, z across it (geom.js).
import { DT, PARRY, MERCY_TICKS, DIFFICULTY, SETTINGS, ECHO, SCARF, MARKSMAN, DEFLECT, SUB, DODGE, ULT, RAM, POWERUPS } from './config.js';
import { onDealtDamage, addResolve, breakVeil, parryWindows, gainFocus, loseFocus, gainUlt, chest } from './player.js';
import { pointInSolid, groundBelow, BOXES, LEVEL_X0, LEVEL_X1, KILL_Y, killYAt, breakableAt, rayBoxT, DESTRUCT } from './level.js';
import { sign, away, turnToward, fz, distToSeg, nearestOnBox } from './geom.js';

export function hurtbox(ent) {
  const z = ent.z || 0;
  return { x0: ent.x - ent.w / 2, x1: ent.x + ent.w / 2, y0: ent.y, y1: ent.y + ent.h, z0: z - ent.w / 2, z1: z + ent.w / 2 };
}
// Box against box. A hitbox can also be a thick segment (seg: a laser or a beam), tested against the box it meets.
function overlap(a, b) {
  if (a.seg) return segHitsBox(a.seg, b, a.seg.r || 0);
  return a.x0 < b.x1 && a.x1 > b.x0 && a.y0 < b.y1 && a.y1 > b.y0 && (a.z0 ?? -Infinity) < (b.z1 ?? Infinity) && (a.z1 ?? Infinity) > (b.z0 ?? -Infinity);
}
function circleBox(c, b) {
  const n = nearestOnBox(b, c.x, c.y, c.z || 0);
  return (c.x - n.x) ** 2 + (c.y - n.y) ** 2 + ((c.z || 0) - n.z) ** 2 < c.r * c.r;
}
// Does a thick segment g ({ x0, y0, z0, x1, y1, z1 }) touch a box (grown by w)?
export function segHitsBox(g, b, w) {
  const vx = g.x1 - g.x0, vy = g.y1 - g.y0, vz = (g.z1 || 0) - (g.z0 || 0), L = Math.hypot(vx, vy, vz);
  if (L < 1e-6) return false;
  const h = rayBoxT(g.x0, g.y0, g.z0 || 0, vx / L, vy / L, vz / L,
    { x0: b.x0 - w, x1: b.x1 + w, y0: b.y0 - w, y1: b.y1 + w, z0: (b.z0 ?? -1e9) - w, z1: (b.z1 ?? 1e9) + w });
  return !!h && h.t <= L;
}

// A barrier is a plate: centre (x, y, z), normal (nx, ny, nz). A round one has radius `half`; a wall (b.wide) is
// `half` tall either side of its centre and `wide` across. Does a segment from a to b cross it?
export function crossesBarrier(b, x0, y0, z0, x1, y1, z1) {
  const nz = b.nz || 0, bz = b.z || 0;
  const s0 = (x0 - b.x) * b.nx + (y0 - b.y) * b.ny + (z0 - bz) * nz, s1 = (x1 - b.x) * b.nx + (y1 - b.y) * b.ny + (z1 - bz) * nz;
  if ((s0 > 0) === (s1 > 0)) return false;
  const t = s0 / (s0 - s1);
  const cx = x0 + (x1 - x0) * t - b.x, cy = y0 + (y1 - y0) * t - b.y, cz = z0 + (z1 - z0) * t - bz;
  if (b.wide) {
    // a wall standing up: across is horizontal and perpendicular to the normal
    const ax = -nz, az = b.nx, am = Math.hypot(ax, az) || 1;
    return Math.abs(cy) <= b.half && Math.abs((cx * ax + cz * az) / am) <= b.wide;
  }
  return Math.hypot(cx, cy, cz) <= b.half;
}

export function resolveHitboxes(world) {
  for (const hb of world.hitboxes) {
    if (world.strikeBoxes) world.strikeBoxes(hb);   // breakable pieces in reach (either side's strikes)
    let set = world.hitSets.get(hb.instance);
    if (!set) { set = new Set(); world.hitSets.set(hb.instance, set); }
    if (hb.team === 'p') {
      for (const e of world.enemies) {
        if (e.dead || set.has(e.id) || !overlap(hb, hurtbox(e))) continue;
        set.add(e.id);
        // Radial hits (landing shockwaves, the Aegis bursts, ground pounds) push each enemy away from their
        // centre. Bursts and pound shockwaves count as blasts (a shield cannot stop them); a pound that lands
        // a hit still counts as a connected strike for its recovery.
        let hit = hb;
        if (hb.radial) {
          const u = away({ x: hb.cx, z: hb.cz ?? e.z }, e), k = Math.abs(hb.kb[0]);
          hit = { ...hb, kb: [u.x * k, hb.kb[1], u.z * k] };
        }
        const res = hitEnemy(world, e, hit, hb.aegisBurst || hb.scatter || hb.quake ? 'blast' : 'melee');
        if (hb.scatter && hb.owner && (res === 'hit' || res === 'kill')) hb.owner.hitConfirm = true;
      }
      // Fix's wrench on one of her gadgets: an upgrade (once a swing)
      if (hb.wrench && hb.owner && world.gadgets) {
        for (const g of world.gadgets) {
          if (g.owner !== hb.owner || g.dead || g.kind === 'pad' || set.has('g' + g.id)) continue;
          if (!overlap(hb, gadgetBox(g, 0.45))) continue;
          set.add('g' + g.id); world.wrenchGadget(g, hb.owner);
        }
      }
    } else {
      for (const p of world.players) {
        if (p.state === 'dead' || p.state === 'downed' || set.has('p' + p.slot)) continue;
        if (!overlap(hb, hurtbox(p))) continue;
        set.add('p' + p.slot);
        // A Bulwark Wall between the striker and the player takes the blow instead
        const wall = hb.owner && world.wallBetween && world.wallBetween(hb.owner, p);
        if (wall) { if (!set.has('w')) { set.add('w'); world.hurtWall(wall, hb.dmg || 0, wall.x, p.y + 1, wall.z); } continue; }
        hitPlayer(world, p, hb);
      }
      if (world.gadgets) for (const g of world.gadgets) {
        if (g.dead || g.kind === 'pad' || set.has('g' + g.id)) continue;
        if (!overlap(hb, gadgetBox(g, 0.4))) continue;
        set.add('g' + g.id); world.hurtGadget(g, hb.dmg || 0);
      }
    }
  }
  world.hitboxes.length = 0;
}
const gadgetBox = (g, r) => ({ x0: g.x - r, x1: g.x + r, y0: g.y, y1: g.y + g.h, z0: (g.z || 0) - r, z1: (g.z || 0) + r });

// ---- Enemies taking hits ---------------------------------------------------------------

// Which way an enemy's shield faces (a horizontal unit vector)
export const shieldVec = e => ({ x: e.shieldDir, z: e.shieldDirZ || 0 });

export function hitEnemy(world, e, hit, source) {
  if (e.dead) return 'none';
  const owner = hit.owner;
  const cx = e.x, cy = e.y + e.h * 0.55, cz = e.z;
  // A boss arriving or roaring into its second phase shrugs everything off
  if (e.invuln > 0) { world.emit('blocked', { x: cx, y: cy, z: cz, e }); return 'blocked'; }
  // Veil ambush: the first melee hit after striking from hiding breaks guard and armor and staggers
  const ambush = source === 'melee' && owner && owner.kind === 'player' && owner.ambushT > 0;
  if (ambush) owner.ambushT = 0;

  // Arc blasts come over the top of the shield, so only direct hits are checked against it
  if (e.type === 'shield' && e.state !== 'stagger' && source !== 'blast') {
    const S = shieldVec(e);
    let fromFront;
    if (source === 'proj') { const vx = hit.vx || 0, vz = hit.vz || 0, h = Math.hypot(vx, vz); fromFront = h < 1e-3 || (-vx * S.x - vz * S.z) / h > 0.2; }
    else { const u = away(e, owner); fromFront = u.x * S.x + u.z * S.z > 0.2; }
    const breaks = hit.armorBreak || hit.bulwark || (hit.vbTier || 0) >= 2 || hit.rail || hit.amplified || hit.ram || ambush;
    if (fromFront && !breaks) {
      e.poise += (hit.poise || 10) * 0.35;
      world.emit('blocked', { x: cx + S.x * 0.5, y: cy, z: cz + S.z * 0.5, e });
      if (source === 'melee' && owner) { owner.vx = -owner.facing * 3; owner.vz = -fz(owner) * 3; owner.hitstop = 3; }
      if (e.poise >= e.poiseMax) stagger(world, e, 90);
      return 'blocked';
    }
    if (fromFront && breaks) { world.emit('guardBreak', { x: cx, y: cy, z: cz, e }); stagger(world, e, 90); }
  }

  const fury = owner && owner.kind === 'player' && owner.furyT > 0;
  if (fury && hit.kb && !hit.furied) hit = { ...hit, furied: true, kb: [hit.kb[0] * POWERUPS.fury.kb, hit.kb[1], (hit.kb[2] || 0) * POWERUPS.fury.kb] };
  let dmg = (hit.dmg || 0) * (ambush ? SCARF.ambushDmg : 1) * (fury ? POWERUPS.fury.dmg : 1), poise = hit.poise || 0;
  let armored = e.armor > 0;
  if (armored) {
    if (hit.armorBreak || ambush) {
      e.armor--; armored = e.armor > 0; dmg *= 0.6;
      world.emit('armorBreak', { x: cx, y: cy, z: cz, e, left: e.armor, owner });
    } else { dmg *= 0.3; poise *= 0.4; world.emit('armorHit', { x: cx, y: cy, z: cz, e }); }
  }
  if (e.tagged > 0) poise *= 1.25;
  if (e.hp !== Infinity) e.hp -= dmg;
  e.poise += poise;
  e.flash = 6;
  if (owner && owner.kind === 'player') {
    if (source === 'melee') owner.hitConfirm = true;
    onDealtDamage(owner, dmg, source === 'melee');
    if (!hit.ult) gainUlt(owner, dmg * ULT.gain.dealt, world);
  }
  const heavyHit = (hit.vbTier || 0) >= 2 || hit.armorBreak || hit.rail || poise >= 40;
  if (source === 'melee') {
    const stop = hit.vbTier ? 3 + hit.vbTier * 2 : heavyHit ? 6 : 3;
    if (owner) owner.hitstop = Math.max(owner.hitstop, stop);
    e.hitstop = stop + 1;
  } else e.hitstop = Math.max(e.hitstop, 2);
  if (e.boss) e.hitstop = Math.min(e.hitstop, 2);   // a combo never freezes a boss in place

  world.emit('hit', { x: cx, y: cy, z: cz, e, owner, heavy: heavyHit, tier: hit.vbTier || 0, source, dmg });
  if (hit.vbTier === 3 || (hit.rail && (e.type === 'brute' || e.boss))) world.emit('impact', { x: cx, y: cy, z: cz, big: true });

  if (ambush) { world.emit('ambush', { x: cx, y: cy, z: cz, e, owner }); world.bark(owner, 'ambush', 0.3); }
  if (e.hp <= 0) { kill(world, e, owner, hit); return 'kill'; }
  const T = e.type, kx = hit.kb ? hit.kb[0] : 0, kz = hit.kb ? hit.kb[2] || 0 : 0;
  const canMove = !['post', 'turret', 'sniper', 'mortar'].includes(T);
  if (ambush && T !== 'post' && T !== 'turret') {
    if (e.state !== 'stagger') stagger(world, e, T === 'brute' ? 120 : 90);
  } else if (hit.scatter && e.light && canMove && !e.flier && !armored) {
    // A ground pound's scatter blast throws light enemies outward in an arc
    if (e.state === 'windup' || e.state === 'aim' || e.state === 'lock') world.director.release(e);
    e.state = 'launched'; e.st = 0; e.vy = hit.kb[1]; e.vx = kx; e.vz = kz; e.poise = 0;
  } else if (e.poise >= e.poiseMax) {
    stagger(world, e, T === 'brute' ? 120 : T === 'shield' ? 90 : 60);
  } else if (!armored && e.state !== 'stagger' && !(e.state === 'snared' && !hit.launcher) && (T === 'swarmer' || T === 'shield' || T === 'sniper' || T === 'drone')) {
    if (e.state === 'windup' || e.state === 'aim' || e.state === 'lock') world.director.release(e);
    // Drones fly, so they flinch in place instead of being launched
    if (hit.launcher && e.light && canMove && !e.flier) {
      e.state = 'launched'; e.st = 0; e.vy = hit.kb[1]; e.vx = kx * 0.3; e.vz = kz * 0.3; world.director.release(e);
    } else if (!e.flier && (e.state === 'launched' || (!e.onGround && canMove))) {
      e.state = 'launched'; e.st = 0; e.vy = Math.max(e.vy, 4); e.vx = kx * 0.3; e.vz = kz * 0.3;
    } else if (e.state !== 'caught') {
      e.state = 'hitstun'; e.st = 0; e.stun = T === 'swarmer' ? 16 : 12;
    }
  }
  // Chain lightning holds a light enemy it stuns for longer; everything it touches crackles for a moment
  if (hit.shock) {
    e.shockT = Math.max(e.shockT || 0, hit.stun || 12);
    if (e.light && !armored && !e.boss && e.state === 'hitstun') e.stun = Math.max(e.stun, hit.stun || 0);
  }
  if (canMove && !armored && hit.kb && !e.boss && !hit.well) {
    if (e.state !== 'launched') { e.vx = kx; e.vz = kz; if (hit.kb[1] > 0) e.vy = Math.max(e.vy, hit.kb[1] * 0.6); }
  }
  // RAM's close-range hits throw enemies much further (RAM.knock): a hard enough push sends a light enemy flying,
  // a heavy one is shoved back. Launchers keep their own arc; bosses, armor and held enemies don't budge.
  if (hit.ramKnock && canMove && !armored && hit.kb && !e.boss && !hit.launcher && !['caught', 'snared', 'plowed'].includes(e.state)) {
    const K = RAM.knock, k = e.light ? K.light : K.heavy, vx = kx * k, vz = kz * k;
    if (e.light && !e.flier && Math.hypot(vx, vz) >= K.launchAt) {
      world.director.release(e);
      e.state = 'launched'; e.st = 0; e.vx = vx; e.vz = vz; e.vy = hit.kb[1] < 0 ? hit.kb[1] * K.light : Math.max(e.vy, hit.kb[1] * K.light, K.lift);   // (a downward swat drives it down)
    } else { e.vx = vx; e.vz = vz; }
  }
  return 'hit';
}

function stagger(world, e, ticks) {
  if (e.boss) { if (e.staggerCd > 0 || e.invuln > 0) return; ticks = 150; e.staggerCd = 420; e.atk = null; }
  world.director.release(e);
  e.state = 'stagger'; e.st = 0; e.stun = ticks; e.poise = 0;
  world.emit('stagger', { x: e.x, y: e.y + e.h * 0.6, z: e.z, e });
}

function kill(world, e, owner, hit = {}) {
  e.dead = true; e.deathT = 0; e.hp = 0;
  world.director.release(e);
  if (owner && owner.kind === 'player' && !hit.ult) gainUlt(owner, ULT.gain.kill, world);
  if (world.onKill) world.onKill(e, owner);
  world.emit('kill', { x: e.x, y: e.y + e.h / 2, z: e.z, e, owner });
  if (e.boss) world.emit('bossDown', { e, x: e.x, y: e.y + e.h / 2, z: e.z, owner });
}

// ---- Players taking hits ---------------------------------------------------------------

// Where a hit on a player comes from: the shot, the blast centre, or the attacker's middle
function hitSource(p, hit) {
  const o = hit.owner;
  if (hit.proj) return { x: hit.proj.x, y: hit.proj.y, z: hit.proj.z || 0 };
  if (hit.at) return { x: hit.at.x, y: hit.at.y, z: hit.at.z ?? p.z };
  if (o) return { x: o.x, y: o.y + (o.h || 1) * 0.5, z: o.z || 0 };
  return null;
}

export function hitPlayer(world, p, hit) {
  if (p.state === 'dead' || p.state === 'downed' || p.state === 'ult') return 'ignored';
  // Nova's Aegis blocks every attack (unblockables too) for him and anyone inside it
  const guard = world.shieldFor ? world.shieldFor(p) : null;
  if (guard) {
    const o = hit.owner, from = hitSource(p, hit) || { x: p.x + p.facing, y: p.y + 1, z: p.z + fz(p) };
    const diff = DIFFICULTY[SETTINGS.difficulty] || DIFFICULTY.normal;
    world.absorbAegis(guard, (hit.dmg || 0) * diff.dmg, from.x, from.y, from.z, hit.instance !== undefined ? 'i' + hit.instance : undefined);
    // Heavy melee rebounds off hard light: a charging Charger is dazed as if it hit a wall
    if (o && o.kind === 'enemy' && !hit.proj && hit.heavy) {
      o.poise += 30;
      if (o.state === 'charge') { o.state = 'dazed'; o.st = 0; o.vx = -o.facing * 4; o.vz = -fz(o) * 4; world.emit('chargeCrash', { e: o }); }
      if (o.boss && o.state === 'dive') o.parried = 2;   // a diving Stormcaller crashes off the hard light
    }
    return 'shielded';
  }
  const attacker = hit.owner;
  const unblockable = hit.cat === 'unblockable' || hit.unblockable;
  const pos = { x: p.x, y: p.y + p.h * 0.6, z: p.z };
  // RAM's Rampart: a strike, a blast or a shot from in front of it is blocked (shockwaves along the floor go
  // under it). The damage comes off its Integrity; a Perfect Guard costs none and makes a striker reel.
  if (p.char === 'ram' && p.state === 'guard' && !p.guardBroken && !hit.ground && world.guardFaces) {
    const src = hitSource(p, hit);
    if (src && world.guardFaces(p, src.x, src.y, src.z)) {
      const perfect = p.guardT <= RAM.guard.perfect, diff = DIFFICULTY[SETTINGS.difficulty] || DIFFICULTY.normal;
      if (attacker && attacker.kind === 'enemy' && !hit.proj) {
        if (perfect) {
          if (attacker.boss) attacker.parried = 2;
          attacker.poise += 60; attacker.hitstop = 8;
          if (attacker.poise >= attacker.poiseMax) stagger(world, attacker, attacker.type === 'brute' ? 120 : 80);
          else if (attacker.state === 'attack') { attacker.state = 'recover'; attacker.st = 0; }
        }
        // The shield stops a charge dead
        if (attacker.state === 'charge') { attacker.state = 'dazed'; attacker.st = 0; attacker.vx = -attacker.facing * 4; attacker.vz = -fz(attacker) * 4; world.emit('chargeCrash', { e: attacker }); }
      }
      world.guardResult(p, perfect ? 0 : (hit.dmg || 0) * diff.dmg, perfect, src.x, src.y, src.z, !!hit.heavy || unblockable);
      return 'guarded';
    }
  }

  const win = parryWindows(p);
  if (p.state === 'parry' && !p.parryResult && p.parryT <= win.window) {
    if (unblockable) {
      world.emit('parryFail', pos);
    } else {
      const perfect = p.parryT <= win.perfect;
      p.parryResult = perfect ? 'perfect' : 'normal'; p.st = 0;
      p.hitstop = perfect ? 6 : 4;
      if (hit.heavy && !perfect) {
        p.hp -= hit.dmg * 0.3 * (DIFFICULTY[SETTINGS.difficulty] || DIFFICULTY.normal).dmg;
        p.vx = -p.facing * 5; p.vz = -fz(p) * 5;
        if (p.hp <= 0) { world.downPlayer(p); return 'hit'; }
      }
      if (attacker && attacker.kind === 'enemy' && !hit.proj) {
        if (attacker.boss) attacker.parried = perfect ? 2 : 1;   // bosses react on their next tick (bosses.js)
        attacker.poise += perfect ? 60 : 25;
        attacker.hitstop = perfect ? 8 : 4;
        if (attacker.poise >= attacker.poiseMax) stagger(world, attacker, attacker.type === 'brute' ? 120 : 80);
        else if (perfect && attacker.state === 'attack') { attacker.state = 'recover'; attacker.st = 0; }
        else if (perfect && attacker.state === 'charge') { attacker.state = 'dazed'; attacker.st = 0; attacker.vx = -attacker.facing * 3; attacker.vz = -fz(attacker) * 3; }   // a parried Charger reels
      }
      if (perfect) {
        if (p.char === 'nova') {
          p.bulwarkCd = Math.max(0, p.bulwarkCd - 120);
          for (const e of world.enemies) {
            if (e.dead || ['post', 'turret'].includes(e.type)) continue;
            if (Math.hypot(e.x - p.x, e.z - p.z) < 2.4 && Math.abs(e.y - p.y) < 2) { const u = away(p, e); e.vx = u.x * 8; e.vz = u.z * 8; }
          }
        } else {
          p.riposteT = 20; addResolve(p, 20); p.cells = Math.min(ECHO.cellsMax, p.cells + 2);
        }
      } else addResolve(p, 10);
      if (perfect) gainUlt(p, ULT.gain.perfect, world);
      world.emit('parry', { ...pos, p, perfect, heavy: !!hit.heavy });
      if (perfect) world.bark(p, 'perfect', 0.35);
      return 'parried';
    }
  }

  // Nova's dodge: an attack that reaches him in its opening ticks is a perfect dodge
  if (p.state === 'dodge' && p.dodge && !p.dodge.perfect && p.dodge.t <= DODGE.perfect && world.perfectDodge) world.perfectDodge(p, hit);
  if (p.mercy > 0 || p.iframe) return 'ignored';
  const diff = DIFFICULTY[SETTINGS.difficulty] || DIFFICULTY.normal;
  let dmg = hit.dmg * diff.dmg;
  const armored = p.char === 'echo' && p.state === 'attack' && p.moveId === 'echo_charged' && p.resolve >= ECHO.resolveHalf;
  // RAM braced by Provoke, or mid-charge, takes less
  if (p.char === 'ram') { if (p.braceT > 0) dmg *= RAM.provoke.brace; if (p.state === 'rush' || p.state === 'leap') dmg *= RAM.rushTaken; }
  // A RAM's Guardian Link takes his share; then Plating soaks what it can
  const guardian = world.guardianOf ? world.guardianOf(p) : null;
  if (guardian) { const share = dmg * RAM.link.share; dmg -= share; world.linkHit(guardian, share, p); }
  if (p.plate > 0) { const a = Math.min(p.plate, dmg); p.plate -= a; dmg -= a; if (a > 0) world.emit('plateHit', { p, x: pos.x, y: pos.y, z: pos.z, left: p.plate }); }
  // Stalwart: ordinary hits don't knock RAM about (heavy ones and blasts do); mid-charge or mid-leap nothing does
  const stalwart = p.char === 'ram' && (p.state === 'rush' || p.state === 'leap' || (!hit.heavy && !unblockable));
  p.hp -= dmg;
  gainUlt(p, dmg * ULT.gain.taken, world);
  breakVeil(p, world, 'hit');
  if (p.char === 'echo') {
    p.strain = Math.min(p.maxHp - Math.max(0, p.hp), p.strain + dmg * 0.5); p.strainT = 300;
    if (world.nearestEnemyDist(p.x, p.y + 1, p.z) < 4 && world.tick - p.lastResolveHitT > 30) {
      addResolve(p, 8); p.lastResolveHitT = world.tick;
    }
  } else if (!stalwart) { p.chargeT = 0; if (p.focus > 0) loseFocus(p, world); }
  if (!stalwart) { p.rifleT = 0; p.dashChargeT = 0; p.burstT = 0; p.subArmed = false; p.dodge = null; p.rivetQ = 0; p.tossArmed = false; }
  p.mercy = MERCY_TICKS; p.hitstop = stalwart ? 2 : 4;
  world.emit('playerHit', { ...pos, p, dmg, heavy: !!hit.heavy, armored: armored || stalwart });
  if (p.hp <= 0) { p.hp = 0; world.downPlayer(p); return 'hit'; }
  if (!armored && !stalwart) {
    if (p.beam) world.endBeam(p, 'hit');
    p.state = 'hitstun'; p.st = 0; p.stun = hit.heavy || unblockable ? 24 : 14;
    p.vx = hit.kb ? hit.kb[0] : 0; p.vz = hit.kb ? hit.kb[2] || 0 : 0; p.vy = hit.kb ? hit.kb[1] : 3;
    p.dash = null; p.lash = null; p.zip = null; p.meleeCharged = false;
  }
  return 'hit';
}

// ---- Projectiles, barriers, shockwaves --------------------------------------------------

// Minimum distance between two points moving linearly over the same tick.
function sweptDistance(a, b) {
  const r0x = a.px - b.px, r0y = a.py - b.py, r0z = (a.pz || 0) - (b.pz || 0);
  const dx = (a.x - b.x) - r0x, dy = (a.y - b.y) - r0y, dz = ((a.z || 0) - (b.z || 0)) - r0z;
  const dd = dx * dx + dy * dy + dz * dz;
  const t = dd > 1e-9 ? Math.max(0, Math.min(1, -(r0x * dx + r0y * dy + r0z * dz) / dd)) : 0;
  return Math.hypot(r0x + dx * t, r0y + dy * t, r0z + dz * t);
}
// A knockback along a shot's flight: the horizontal way it flies, `k` hard, and `up`
function shotKb(pr, k, up) {
  const h = Math.hypot(pr.vx, pr.vz || 0);
  return h > 1e-3 ? [pr.vx / h * k, up, (pr.vz || 0) / h * k] : [0, up, 0];
}

function projectileHits(world, pr) {
  if (pr.team === 'p') {
    for (const e of world.enemies) {
      if (e.dead || pr.hitSet.has(e.id) || !circleBox(pr, hurtbox(e))) continue;
      pr.hitSet.add(e.id);
      if (pr.snare) { world.applySnare(e, pr.owner); pr.dead = true; return; }   // snares wrap around shields
      if (pr.stick) {   // a Hot Rivet: it hits, then stays stuck in the enemy until its fuse runs out
        hitEnemy(world, e, { ...pr, kb: shotKb(pr, pr.kb || 2, 1) }, 'proj');
        pr.stuck = { e, ox: pr.x - e.x, oy: pr.y - e.y, oz: pr.z - e.z, t: pr.stick.fuse }; pr.vx = 0; pr.vy = 0; pr.vz = 0;
        world.emit('rivetStick', { p: pr.owner, x: pr.x, y: pr.y, z: pr.z, e });
        return;
      }
      if (pr.blast) { detonate(world, pr, pr.x, pr.y, pr.z, null, false); pr.dead = true; return; }
      const hit = { ...pr, kb: shotKb(pr, pr.kb || 2, pr.kbY || 1) };
      if (pr.falloff) {   // secondary blaster pellets lose damage over distance
        const f = Math.max(0.25, 1 - Math.hypot(pr.x - pr.falloff.x, pr.y - pr.falloff.y, pr.z - (pr.falloff.z || 0)) / pr.falloff.d);
        hit.dmg *= f; hit.poise *= f; hit.kb[0] *= f; hit.kb[2] *= f;
      }
      const res = hitEnemy(world, e, hit, 'proj');
      if (pr.disc) {   // the disc cuts on through; a shield or a boss's guard turns it for home
        if (res === 'blocked' && pr.disc.phase === 'out') { discTurn(pr, 'back'); world.emit('ricochet', { x: pr.x, y: pr.y, z: pr.z, pr }); }
        continue;
      }
      if (res === 'hit' || res === 'kill') awardFocus(world, pr);
      if (pr.tracer || pr.mark) { e.tagged = Math.max(e.tagged, 600); world.emit('tag', { x: e.x, y: e.y + e.h, z: e.z, e }); }
      if (pr.splash) detonate(world, pr, pr.x, pr.y, pr.z, e, false);   // splash reaches the enemies around this one
      if (pr.prism) {   // splits on impact; a blocked prism glances back off the shield
        const b = res === 'blocked' ? -1 : 1;
        world.splitPrism(pr, pr.x, pr.y, pr.z, pr.vx * b, pr.vy, pr.vz * b, e);
        pr.dead = true; return;
      }
      if (!pr.pierce || res === 'blocked') { pr.dead = true; return; }
      if (pr.pierceLeft !== undefined && pr.pierceLeft-- <= 0) { pr.dead = true; return; }   // a Breach Shot goes through so many
    }
  } else {
    for (const p of world.players) {
      if (p.state === 'dead' || p.state === 'downed') continue;
      if (canDeflect(p, pr)) { world.deflect(p, pr); return; }
      if (!circleBox(pr, hurtbox(p))) continue;
      if (pr.blast) { detonate(world, pr, pr.x, pr.y, pr.z, null, false); pr.dead = true; return; }   // mortar shells burst on contact
      const res = hitPlayer(world, p, { dmg: pr.dmg, heavy: pr.heavy, cat: pr.heavy ? 'heavy' : 'standard',
        kb: shotKb(pr, 6, 3), owner: pr.owner, proj: pr });
      if (res !== 'ignored') { pr.dead = true; return; }
    }
  }
}

// Echo (Hunter kit) knocks back an enemy shot that reaches his staff: during the first DEFLECT.window ticks
// of a parry (all round him), or in front of him while a `deflect` staff swing is out. Shells that burst
// (unblockable) cannot be deflected.
function canDeflect(p, pr) {
  if (p.char !== 'echo' || SETTINGS.echoKit !== 'hunter' || pr.blast || pr.team !== 'e') return false;
  const cx = p.x, cy = p.y + p.h * 0.6, cz = p.z, d = Math.hypot(pr.x - cx, pr.y - cy, pr.z - cz);
  if (p.state === 'parry' && p.parryT <= DEFLECT.window) return d < DEFLECT.reach + pr.r;
  const m = p.move;
  if (p.state === 'attack' && m && m.deflect && p.st >= m.su - 1 && p.st <= m.su + m.ac + 1) {
    const front = m.spin || (pr.x - cx) * p.facing + (pr.z - cz) * fz(p) > -0.3;
    return front && d < DEFLECT.reach + 0.7 + pr.r;
  }
  return false;
}

// Marksman kit: the first piece of a charged release to land earns Focus for the whole shot
// (2 on a Perfect Release); every basic round that lands earns a little.
export function awardFocus(world, pr) {
  const p = pr.owner;
  if (!p || p.kind !== 'player' || p.char !== 'nova') return;
  if (pr.family) {
    if (!pr.family.focused) { pr.family.focused = true; gainFocus(p, pr.family.perfect ? 2 : 1, world); }
  } else if (pr.kind === 'shot') gainFocus(p, MARKSMAN.focus.perRound, world);
}

// Where a projectile bursts. Arc shells and mortar shells explode; Nova's other shots splash.
// A burst on terrain (onTerrain) can rocket-jump Nova; an Arc shell can wherever it bursts.
function detonate(world, pr, x, y, z, skip, onTerrain) {
  const common = { owner: pr.owner, team: pr.team, x, y, z, level: pr.level || 0, perfect: !!pr.perfect, family: pr.family || null, skip };
  // RAM's level 3 Breach Shot bursts where it ends; Fix's Hot Rivets burst when their fuse runs out
  if (pr.endBlast) world.explode({ ...common, spec: pr.endBlast, kind: 'breachBlast' });
  if (pr.stick) { world.explode({ ...common, spec: pr.stick.blast, kind: 'rivetBlast' }); return; }
  if (pr.blast) world.explode({ ...common, spec: pr.blast, rocket: true, kind: pr.kind === 'grenade' || pr.kind === 'bomblet' ? 'frag' : 'blast' });
  else if (pr.splash) world.explode({ ...common, spec: pr.splash, rocket: onTerrain, kind: 'splash' });
  if (pr.cluster && world.clusterBurst) world.clusterBurst(pr, x, y, z);   // a level 3 grenade scatters bomblets
}

// Which faces a shot crossed going from the old point into solid: [x, y, z] (all three at a corner)
function wallFaces(pr, ox, oy, oz) {
  const fx = pointInSolid(pr.x, oy, oz), fy = pointInSolid(ox, pr.y, oz), fzz = pointInSolid(ox, oy, pr.z);
  if (!fx && !fy && !fzz) return [true, true, true];
  return [fx, fy, fzz];
}

// Walls: shards ricochet while they have bounces left, shells burst, other shots splash, prisms
// split off the surface. Returns true when the projectile is gone.
function hitWall(world, pr, ox, oy, oz) {
  // A breakable piece takes the shot's damage (enemy fire wears cover down too); blasting shells do it in explode
  const bk = !pr.blast && breakableAt(pr.x, pr.y, pr.z, 0.05);
  if (bk && world.damageBox) world.damageBox(bk, Math.max(0.5, pr.dmg || 0) * (pr.heavy ? 2 : 1), pr.x, pr.y, pr.z, pr.owner);
  const [flipX, flipY, flipZ] = wallFaces(pr, ox, oy, oz);
  if (pr.disc) {
    // The disc glances off terrain on its way out and turns for home (hovering first if it would)
    pr.x = ox; pr.y = oy; pr.z = oz;
    if (pr.disc.phase === 'out') discTurn(pr, pr.disc.hover > 0 ? 'hover' : 'back');
    else if (pr.disc.phase === 'hover') { pr.vx = 0; pr.vy = 0; pr.vz = 0; }
    world.emit('ricochet', { x: ox, y: oy, z: oz, pr });
    return false;
  }
  if (pr.bouncy) { bounce(world, pr, ox, oy, oz, flipX, flipY, flipZ); return false; }
  if (pr.stick) {   // a Hot Rivet sticks in the wall where it struck
    pr.x = ox; pr.y = oy; pr.z = oz; pr.vx = 0; pr.vy = 0; pr.vz = 0; pr.stuck = { e: null, ox, oy, oz, t: pr.stick.fuse };
    world.emit('rivetStick', { p: pr.owner, x: ox, y: oy, z: oz, e: null });
    return true;
  }
  if (pr.bounces > 0) {
    pr.x = ox; pr.y = oy; pr.z = oz; if (flipX) pr.vx = -pr.vx; if (flipY) pr.vy = -pr.vy; if (flipZ) pr.vz = -pr.vz; pr.bounces--;
    world.emit('ricochet', { x: ox, y: oy, z: oz, pr });
    return false;
  }
  detonate(world, pr, ox, oy, oz, null, true);
  if (pr.prism) world.splitPrism(pr, ox, oy, oz, flipX ? -pr.vx : pr.vx, flipY ? -pr.vy : pr.vy, flipZ ? -pr.vz : pr.vz, null);
  else if (pr.snare) world.snareLanded(pr, ox, oy, oz);
  pr.dead = true; world.emit('projWall', { x: pr.x, y: pr.y, z: pr.z, pr });
  return true;
}

// End of a projectile's time (enemy shots, Echo's bolts): shells burst in the air, prisms split forward
function expire(world, pr) {
  if (pr.blast) detonate(world, pr, pr.x, pr.y, pr.z, null, false);
  else if (pr.prism) world.splitPrism(pr, pr.x, pr.y, pr.z, pr.vx, pr.vy, pr.vz, null);
  pr.dead = true;
}

// A grenade bounces off terrain, keeping `bouncy` of its speed; on a floor with little speed left it comes to
// rest and rolls to a stop
function bounce(world, pr, ox, oy, oz, flipX, flipY, flipZ) {
  pr.x = ox; pr.y = oy; pr.z = oz;
  const sp = Math.hypot(pr.vx, pr.vy, pr.vz);
  if (flipX) pr.vx = -pr.vx * pr.bouncy;
  if (flipZ) pr.vz = -pr.vz * pr.bouncy;
  if (flipY) {
    const floor = pr.vy < 0;
    pr.vy = -pr.vy * pr.bouncy; pr.vx *= SUB.grenade.roll; pr.vz *= SUB.grenade.roll;
    if (floor && pr.vy < 2.4) { pr.vy = 0; pr.rest = true; const g = groundBelow(pr.x, oy + 0.05, pr.z); if (g > -Infinity && oy - g < 0.5) pr.y = g + pr.r; }
  }
  if (sp > 3) world.emit('bounce', { x: ox, y: oy, z: oz, pr, sp });
}
// The top of a one-way platform crossed going down between two heights, if any (grenades land on them)
function oneWayTop(x, z, y0, y1) {
  for (const b of BOXES) if (b.type === 'o' && x > b.x0 && x < b.x1 && z > b.z0 && z < b.z1 && y0 >= b.y1 - 0.02 && y1 < b.y1) return b.y1;
  return null;
}

export function updateProjectiles(world, frozen = false) {
  const list = world.projectiles;
  for (const pr of list) {
    if (pr.dead) continue;
    if (pr.z === undefined) pr.z = 0;
    if (pr.vz === undefined) pr.vz = 0;
    pr.px = pr.x; pr.py = pr.y; pr.pz = pr.z;
    if (frozen && pr.team === 'e') continue;   // an ultimate holds enemy fire in the air
    if (pr.stuck) {
      // A stuck Hot Rivet rides along in its enemy (or sits in the wall) until the fuse is out
      const S = pr.stuck;
      if (S.e) { if (!S.e.dead) { pr.x = S.e.x + S.ox; pr.y = S.e.y + Math.min(S.oy, S.e.h); pr.z = S.e.z + (S.oz || 0); } }
      else { pr.x = S.ox; pr.y = S.oy; pr.z = S.oz || 0; }
      if (--S.t <= 0) { detonate(world, pr, pr.x, pr.y, pr.z, null, false); pr.dead = true; }
      continue;
    }
    // A perfect dodge slows enemy shots close by
    const k = pr.slowT > 0 ? (pr.slowT--, 0.5) : 1;
    if (pr.homing) steerToTagged(world, pr);
    if (pr.seek) steerDart(world, pr);
    if (pr.disc) { steerDisc(world, pr); if (pr.dead) continue; }
    if (pr.rest) {
      // A grenade at rest rolls to a stop, and falls again if the floor goes
      pr.vx *= 0.8; pr.vz *= 0.8; pr.vy = 0;
      if (!pointInSolid(pr.x, pr.y - pr.r - 0.08, pr.z) && oneWayTop(pr.x, pr.z, pr.y, pr.y - pr.r - 0.08) === null) pr.rest = false;
    } else if (pr.gravity) pr.vy -= pr.gravity * DT * k;
    pr.ttl--;
    if (pr.ttl <= 0) { expire(world, pr); continue; }
    // Anything that leaves the level is gone (Nova's shots otherwise fly until they hit something)
    if (pr.x < LEVEL_X0 - 2 || pr.x > LEVEL_X1 + 2 || pr.y < killYAt(pr.x) - 6 || pr.y > 90 || Math.abs(pr.z) > 60) { pr.dead = true; continue; }
    // Sub-step so fast shots cannot skip over thin targets or walls
    const steps = Math.max(1, Math.ceil(Math.hypot(pr.vx, pr.vy, pr.vz) * DT * k / 0.3));
    for (let s = 0; s < steps && !pr.dead; s++) {
      const ox = pr.x, oy = pr.y, oz = pr.z;
      pr.x += pr.vx * DT * k / steps; pr.y += pr.vy * DT * k / steps; pr.z += pr.vz * DT * k / steps;
      if (pr.bouncy && pr.vy < 0) {
        const top = oneWayTop(pr.x, pr.z, oy, pr.y);
        if (top !== null) { bounce(world, pr, pr.x, top + 0.01, pr.z, false, true, false); continue; }
      }
      if (!pr.ghost && pointInSolid(pr.x, pr.y, pr.z)) {
        if (hitWall(world, pr, ox, oy, oz)) break;
        continue;
      }
      if (pr.team === 'e' && world.aegisAt) {
        const guard = world.aegisAt(pr.x, pr.y, pr.z, pr.r);
        if (guard) {
          // Stopped at the hard light: a shell bursts on it, everything else is absorbed
          world.absorbAegis(guard, pr.blast ? pr.blast.dmg : pr.dmg, pr.x, pr.y, pr.z);
          if (pr.blast) world.emit('enemyBlast', { x: pr.x, y: pr.y, z: pr.z, r: pr.blast.r * 0.6 });
          pr.dead = true; break;
        }
      }
      if (pr.team === 'e' && world.rampartCross) {
        // RAM's Rampart stops it, covering everyone behind him (a Perfect Guard sends it back as his)
        const ram = world.rampartCross(ox, oy, oz, pr.x, pr.y, pr.z, pr.r);
        if (ram) { if (world.blockShot(ram, pr) === 'block') break; continue; }
        // Fix's gadgets stand in the way of shots too
        const g = world.gadgetAt && world.gadgetAt(pr.x, pr.y, pr.z, pr.r);
        if (g) { world.hurtGadget(g, pr.blast ? pr.blast.dmg : pr.dmg); if (pr.blast) world.emit('enemyBlast', { x: pr.x, y: pr.y, z: pr.z, r: pr.blast.r * 0.6 }); pr.dead = true; break; }
      }
      for (const b of world.barriers) {
        if (!crossesBarrier(b, ox, oy, oz, pr.x, pr.y, pr.z)) continue;
        if (pr.team === 'e') {
          pr.dead = true; world.emit('barrierBlock', { x: pr.x, y: pr.y, z: pr.z, kind: b.kind });
          if (b.kind === 'rampart') world.hurtWall(b, pr.blast ? pr.blast.dmg : pr.dmg, pr.x, pr.y, pr.z);
          break;
        }
        if (!pr.amplified) {
          pr.amplified = true; pr.pierce = true; pr.dmg *= 1.5; pr.poise = (pr.poise || 8) * 1.5; pr.r *= 1.3;
          if (pr.blast) { pr.blast.dmg *= 1.5; pr.blast.poise *= 1.5; pr.blast.r *= 1.2; }
          world.emit('amplify', { x: pr.x, y: pr.y, z: pr.z, pr });
        }
      }
      if (!pr.dead) projectileHits(world, pr);
    }
  }
  // Nova's shots intercept hostile projectiles; heavy ones need a charged shot (not darts, shards or pellets).
  for (const a of list) {
    if (a.dead || a.team !== 'p' || !a.intercept) continue;
    for (const b of list) {
      if (b.dead || b.team !== 'e') continue;
      if (sweptDistance(a, b) > a.r + b.r + 0.25) continue;
      if (b.heavy && !(a.interceptHeavy ?? !!a.level)) { a.dead = true; world.emit('interceptFail', { x: a.x, y: a.y, z: a.z }); break; }
      b.dead = true; if (!a.pierce) a.dead = true;
      const saved = world.projectileTarget(b, a.owner);
      world.emit('intercept', { x: b.x, y: b.y, z: b.z, owner: a.owner, heavy: b.heavy, saved });
      if (a.dead) break;
    }
  }
  world.projectiles = list.filter(pr => !pr.dead);
}

// Nova's disc: out along the throw (easing off toward the far end), a hover there from level 2 (cutting
// again every SUB.disc.tick ticks), then home to his chest, faster and faster and through walls. It cuts
// each enemy once per leg. It fades if he is gone.
function discTurn(pr, phase) {
  pr.disc.phase = phase; pr.disc.t = 0; pr.hitSet.clear();
  if (phase === 'back') pr.ghost = true;
}
function steerDisc(world, pr) {
  const D = SUB.disc, s = pr.disc, o = pr.owner; s.t++;
  if (!o || !world.players.includes(o) || o.state === 'dead' || o.state === 'downed' || o.char !== 'nova') {
    pr.dead = true; world.emit('discFade', { x: pr.x, y: pr.y, z: pr.z }); return;
  }
  if (s.phase === 'out') {
    const f = 1 - 0.65 * Math.max(0, (s.t - s.out * 0.55) / (s.out * 0.45));
    pr.vx = s.dx * s.speed * f; pr.vy = s.dy * s.speed * f; pr.vz = (s.dz || 0) * s.speed * f;
    if (s.t >= s.out) discTurn(pr, s.hover > 0 ? 'hover' : 'back');
  } else if (s.phase === 'hover') {
    pr.vx *= 0.6; pr.vy *= 0.6; pr.vz *= 0.6;
    if (s.t % D.tick === 0) pr.hitSet.clear();
    if (s.t >= s.hover) discTurn(pr, 'back');
  } else {
    const c = chest(o), dx = c.x - pr.x, dy = c.y - pr.y, dz = c.z - pr.z, d = Math.hypot(dx, dy, dz) || 1, sp = Math.min(D.back + s.t * 0.6, 44);
    pr.vx = dx / d * sp; pr.vy = dy / d * sp; pr.vz = dz / d * sp;
    if (d < 0.8 + sp * DT) { pr.dead = true; world.emit('discCatch', { p: o, x: c.x, y: c.y, z: c.z }); }
    else if (s.t > D.maxBack) pr.dead = true;
  }
}

// Volley darts fly straight for a moment so the fan opens, then turn toward their target.
// A dart whose target is gone picks the nearest enemy ahead it has not hit yet.
function steerDart(world, pr) {
  const s = pr.seek;
  if (++s.age < s.delay || s.age > s.until) return;   // after `until` ticks a dart flies straight on
  let t = s.target;
  if (!t || t.dead) {
    t = null; let bd = 10;
    const sp0 = Math.hypot(pr.vx, pr.vy, pr.vz) || 1;
    for (const e of world.enemies) {
      if (e.dead || pr.hitSet.has(e.id)) continue;
      const dx = e.x - pr.x, dy = e.y + e.h / 2 - pr.y, dz = e.z - pr.z, d = Math.hypot(dx, dy, dz);
      if (d < bd && (dx * pr.vx + dy * pr.vy + dz * pr.vz) / (d * sp0) > 0) { bd = d; t = e; }
    }
    s.target = t;
    if (!t) return;
  }
  [pr.vx, pr.vy, pr.vz] = turnToward(pr.vx, pr.vy, pr.vz, t.x - pr.x, t.y + t.h / 2 - pr.y, t.z - pr.z, s.turn);
}

// Homing shots curve toward tagged enemies, and toward the shooter's lock-on target
function steerToTagged(world, pr) {
  let best = null, bd = 14;
  const sp = Math.hypot(pr.vx, pr.vy, pr.vz) || 1, dx0 = pr.vx / sp, dy0 = pr.vy / sp, dz0 = pr.vz / sp, lock = pr.owner && pr.owner.lockT;
  for (const e of world.enemies) {
    if (e.dead || (e.tagged <= 0 && e !== lock)) continue;
    const dx = e.x - pr.x, dy = e.y + e.h / 2 - pr.y, dz = e.z - pr.z, d = Math.hypot(dx, dy, dz);
    if (d < bd && (dx * dx0 + dy * dy0 + dz * dz0) / d > 0.5) { bd = d; best = e; }
  }
  if (!best) return;
  [pr.vx, pr.vy, pr.vz] = turnToward(pr.vx, pr.vy, pr.vz, best.x - pr.x, best.y + best.h / 2 - pr.y, best.z - pr.z, 0.06);
}

// Shockwaves run out along the floor as an expanding ring (centre x, z at floor height y; radius r). Anything
// standing in the ring's band is hit once, thrown outward; jumping over it is the way to avoid it. The ring
// keeps going over gaps; it ends when its time runs out.
export function updateShockwaves(world) {
  for (const s of world.shockwaves) {
    s.r += s.speed * DT; s.ttl--;
    let set = world.hitSets.get(s.instance);
    if (!set) { set = new Set(); world.hitSets.set(s.instance, set); }
    const inBand = (x, z, half) => Math.abs(Math.hypot(x - s.x, z - s.z) - s.r) < 0.5 + half;
    const atFloor = ent => ent.y < s.y + s.h && ent.y + ent.h > s.y - 0.1;
    // Breakable pieces in the ring's path
    if (world.damageBox) for (const b of BOXES) {
      if (b.type !== 'd' || b.broken || set.has('b' + b.id) || b.y0 > s.y + s.h || b.y1 < s.y) continue;
      const nx = Math.max(b.x0, Math.min(s.x, b.x1)), nz = Math.max(b.z0, Math.min(s.z, b.z1));
      if (Math.abs(Math.hypot(nx - s.x, nz - s.z) - s.r) > 0.6) continue;
      set.add('b' + b.id); world.damageBox(b, (s.dmg || 4) * 2, nx, s.y + 0.4, nz, s.owner);
    }
    if (s.team === 'p') {
      for (const e of world.enemies) {
        if (e.dead || set.has(e.id) || !atFloor(e) || !inBand(e.x, e.z, e.w / 2)) continue;
        set.add(e.id);
        const u = away(s, e);
        hitEnemy(world, e, { owner: s.owner, dmg: s.dmg, poise: s.poise, kb: [u.x * 6, 9, u.z * 6], launcher: true, quake: true }, 'blast');
      }
    } else {
      for (const p of world.players) {
        if (p.state === 'dead' || p.state === 'downed' || set.has('p' + p.slot) || !atFloor(p) || !inBand(p.x, p.z, p.w / 2)) continue;
        set.add('p' + p.slot);
        const u = away(s, p);
        hitPlayer(world, p, { owner: s.owner, dmg: s.dmg, kb: [u.x * 8, 7, u.z * 8], unblockable: true, cat: 'unblockable', instance: s.instance, ground: true,
          at: { x: s.x, y: s.y, z: s.z } });
      }
      if (world.gadgets) for (const g of world.gadgets) {
        if (g.dead || g.kind === 'pad' || set.has('g' + g.id) || !inBand(g.x, g.z || 0, 0.4) || Math.abs(g.y - s.y) > s.h) continue;
        set.add('g' + g.id); world.hurtGadget(g, s.dmg || 0);
      }
    }
  }
  world.shockwaves = world.shockwaves.filter(s => s.ttl > 0);
}
