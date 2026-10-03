// Player controller: movement, actions, cancel rules. Operates on plain data; the world
// supplies spawning helpers and events. Frame data lives in config.js.
import {
  DT, GRAVITY, FALL_MULT, RISE_CUT_MULT, MAX_FALL, FAST_FALL, HIGH_VEL,
  COYOTE, JUMP_BUFFER, ACTION_BUFFER, PARRY_BUFFER, PARRY, CHARS, MOVES, VB, NOVA, MARKSMAN, ECHO, HUNTER, SCARF, SETTINGS,
  WALL, DASH_CHARGE, LOCK, AEGIS, DASH_SLASH, POUND, SUBS, SUB, DODGE, ULT, RAM, FIX, PLATE_MAX,
} from './config.js';
import { moveBody, hasHeadroom } from './level.js';
import { sign, approach, approach2, setFacing, fz, away, hdist, turnToward, fwdBox, boxAt } from './geom.js';

// Nova with the Marksman kit (bracer attachments, secondary weapons, dodge, skate glide)
export const marksman = p => p.char === 'nova' && SETTINGS.novaKit === 'marksman';

export function snap8(x, y) {
  if (Math.hypot(x, y) < 0.35) return null;
  const a = Math.round(Math.atan2(y, x) / (Math.PI / 4)) * (Math.PI / 4);
  return [Math.cos(a), Math.sin(a)];
}

export function createPlayer(slot, device, charId, x, y, z = 0) {
  const c = CHARS[charId];
  return {
    kind: 'player', slot, device, char: charId,
    x, y, z, vx: 0, vy: 0, vz: 0, w: c.width, h: c.height, prevX: x, prevY: y, prevZ: z,
    facing: 1, facingZ: 0, onGround: false, wallDir: 0, coyote: 0, jumpsUsed: 0, airDashes: 1,
    state: 'normal', st: 0, crouch: false, dropT: 0, controlLock: 0, wallSliding: false,
    dash: null, dashCd: 0, postDash: 99, dashCarry: false, fastFall: false,
    launchedT: 0, zipArriveT: 0, boostT: 0, iframe: false,
    move: null, moveId: null, queued: null, hitConfirm: false, instance: 0,
    chargeT: 0, fireCd: 0, meleeHeldT: 0, meleeCharged: false,
    hp: c.hp, maxHp: c.hp, strain: 0, strainT: 0,
    mercy: 0, hitstop: 0, stun: 0,
    parryT: 0, parryResult: null, riposteT: 0,
    bulwarkCd: 0, lashCharges: ECHO.lashCharges, lashRecharge: 0, lash: null, zip: null,
    resolve: 0, calmT: 0, lastResolveHitT: 0, cells: ECHO.cellsMax, tracerCd: 0,
    snares: HUNTER.snareCharges, snareRecharge: 0, leash: null,
    scarfMode: 'tether', modeCd: 0, veiled: false, veilCharge: 0, veilBreakT: 0, ambushT: 0, targetedBy: 0,
    attachment: 'lance', focus: 0, focusT: 0, burstCd: 0, burstT: 0, shootT: 0, carveT: 0,
    fuel: MARKSMAN.boost.fuel, thrusting: false, rockets: 0, rocketT: 0, rocketPow: 0,
    aegis: null, aegisCd: 0, overcharge: 0, overT: 0, beam: null, slash: null, pound: null,
    sub: 'scatter', subSwCd: 0, subArmed: false, dodge: null, dodgeCd: 0, airDodge: true, airRise: true, stick: [0, 0, 0],
    ult: 0, ultRun: null, chordP: 99, chordF: 99,
    // RAM: the Rampart's Integrity and stored Kinetic, the guard, the Ram Charge, and his three abilities
    integrity: RAM.guard.integrity, kinetic: 0, guardT: 99, guardOffT: 99, guardDir: [1, 0, 0], blockT: 99, guardBroken: false,
    rush: null, wallCd: 0, linkCd: 0, provokeCd: 0, link: null, leap: null, braceT: 0,
    // Fix: Scrap, the selected gadget and power-up, the Patch Beam, and a tossed power-up waiting for the button
    scrap: FIX.scrap.start, gadgetSel: 'pylon', powerSel: 'overclock', patch: null, tossArmed: false, rivetQ: 0, rivetT: 0,
    // Support anyone can carry: Plating (an overshield), Overclock, the Patch Beam's Tune-Up, an Amp Coil's field
    plate: 0, overclockT: 0, tuneT: 0, ampK: 1, fixRevive: false, padCd: 0, furyT: 0,
    aimX: 1, aimY: 0, aimZ: 0, aimFree: false, camAim: false, lastWallX: 0, lastWallZ: 0,
    wallT: 0, wallStick: 0, wallCoyote: 0, lastWallDir: 0, dashChargeT: 0, rifleT: 0, rifleCd: 0,
    lockT: null, lockHeld: 0, lockHoldDone: false, lockLost: 0, lockSuspend: false,
    buf: { jump: 99, dash: 99, melee: 99, fire: 99, parry: 99, sig: 99, mode: 99, sub: 99 },
    downedT: 0, revive: 0, respawnT: 0, secondWind: true,
    lastSafeX: x, lastSafeY: y, lastSafeZ: z, offscreenT: 0, vbTierShown: 0,
  };
}

export function setCharacter(p, charId) {
  const c = CHARS[charId];
  p.char = charId; p.w = c.width; p.h = c.height; p.maxHp = c.hp;
  p.hp = Math.min(p.hp, p.maxHp); p.chargeT = 0; p.resolve = 0; p.strain = 0;
  p.cells = ECHO.cellsMax; p.lashCharges = ECHO.lashCharges; p.state = 'normal'; p.st = 0;
  p.veiled = false; p.veilCharge = 0; p.veilBreakT = 0; p.ambushT = 0; p.targetedBy = 0;
  p.focus = 0; p.focusT = 0; p.burstCd = 0; p.burstT = 0; p.shootT = 0;
  p.fuel = MARKSMAN.boost.fuel; p.thrusting = false; p.rockets = 0; p.rocketT = 0;
  p.rifleT = 0; p.rifleCd = 0; p.dashChargeT = 0; p.overcharge = 0; p.overT = 0; p.beam = null; p.aegis = null;
  p.subArmed = false; p.dodge = null; p.pound = null;
  p.integrity = RAM.guard.integrity; p.kinetic = 0; p.guardBroken = false; p.rush = null; p.leap = null; p.braceT = 0;
  p.patch = null; p.tossArmed = false; p.rivetQ = 0; p.scrap = Math.max(p.scrap, FIX.scrap.start);
}

export function chest(p) { return { x: p.x, y: p.y + p.h * 0.62, z: p.z || 0 }; }

// Which Velocity Break tier is available right now (0 = none)?
export function vbTier(p) {
  if (p.state === 'pound') return 0;   // the pound's drop is fast, but it is not a Velocity Break
  if (p.boostT > 0 || p.launchedT > 0) return 3;
  if (p.dash && p.dash.level >= 2 && (p.state === 'dash' || p.postDash <= 6)) return p.dash.level;
  if (p.zipArriveT > 0) return 2;
  if (p.state === 'dash' || p.state === 'slide' || p.postDash <= 6) return 1;
  if (p.fastFall && p.vy < -18) return 2;
  if (p.dashCarry && !p.onGround && Math.hypot(p.vx, p.vy, p.vz) > HIGH_VEL) return 2;
  return 0;
}

function setState(p, s) { p.state = s; p.st = 0; }

// The way the move stick points on the ground plane, as a unit vector (null when it is centred)
export function moveDir(cmd, min = 0.3) {
  const mx = cmd.mx || 0, mz = cmd.mz || 0, m = Math.hypot(mx, mz);
  return m > min ? [mx / m, mz / m] : null;
}
const stickMag = cmd => Math.hypot(cmd.mx || 0, cmd.mz || 0);

// Aim, a 3D unit vector. The third-person camera supplies it (cmd.aimFree with cmd.camAim: the crosshair's line,
// pulled onto an enemy close to it by cmd.assist radians of aim assist); a gamepad with no free aim (and the
// headless tests) aims the way he faces, up or down with the stick as in eight directions.
function updateAim(p, cmd, world) {
  let d;
  const c = chest(p);
  p.camAim = !!cmd.camAim;
  if (cmd.aimFree) {
    d = [cmd.ax, cmd.ay, cmd.az || 0]; p.aimFree = true;
    if (cmd.assist > 0) {
      const e = world.nearestEnemyInCone(c.x, c.y, c.z, d[0], d[1], d[2], 40, cmd.assist, true);
      if (e) { const dx = e.x - c.x, dy = e.y + e.h * 0.55 - c.y, dz = e.z - c.z, m = Math.hypot(dx, dy, dz) || 1; d = [dx / m, dy / m, dz / m]; }
    }
  } else {
    p.aimFree = false;
    const hm = stickMag(cmd), s = snap8(hm, cmd.my);
    const h = hm > 0.35 ? [cmd.mx / hm, (cmd.mz || 0) / hm] : [p.facing, fz(p)];
    d = s ? [s[0] * h[0], s[1], s[0] * h[1]] : null;
    if (d && p.onGround && d[1] < 0) d = [p.facing, 0, fz(p)];      // down on the ground means crouch
    if (!d) d = [p.facing, 0, fz(p)];
    if (SETTINGS.aimAssist && p.device !== 'kbm') {
      const e = world.nearestEnemyInCone(c.x, c.y, c.z, d[0], d[1], d[2], 14, Math.PI / 8);
      if (e) { const dx = e.x - c.x, dy = e.y + e.h / 2 - c.y, dz = e.z - c.z, m = Math.hypot(dx, dy, dz); d = [dx / m, dy / m, dz / m]; }
    }
  }
  // Locked on: aim straight at the target. With automatic lock-on, free aim (the camera, the right stick) still
  // aims where it points, and holding the stick up or down aims that way.
  if (p.lockT && (SETTINGS.lockMode === 'manual' || (!cmd.aimFree && Math.abs(cmd.my) < 0.55))) {
    const t = p.lockT, dx = t.x - c.x, dy = t.y + t.h * 0.55 - c.y, dz = t.z - c.z, m = Math.hypot(dx, dy, dz) || 1;
    d = [dx / m, dy / m, dz / m];
  } else if (p.wallSliding && p.wallDir) {
    // on a wall: shoot out from it
    const k = d[0] * p.wallX + d[2] * p.wallZ;
    if (k > 0) d = [d[0] - 2 * k * p.wallX, d[1], d[2] - 2 * k * p.wallZ];
  }
  p.aimX = d[0]; p.aimY = d[1]; p.aimZ = d[2];
}
// The horizontal part of his aim, normalised (null when he aims nearly straight up or down)
export function aimH(p) { const m = Math.hypot(p.aimX, p.aimZ || 0); return m > 0.2 ? [p.aimX / m, (p.aimZ || 0) / m] : null; }
// Free aim he means to face (the stick or mouse of the old side view; never the third-person camera, which looks
// wherever the player looks, so his attacks follow the way he moves instead)
const faceAim = p => (p.aimFree && !p.camAim ? aimH(p) : null);

export function updatePlayer(p, cmd, world) {
  p.prevX = p.x; p.prevY = p.y; p.prevZ = p.z;
  for (const b in p.buf) p.buf[b] = cmd.pressed[b] ? 0 : Math.min(99, p.buf[b] + 1);
  trackChord(p, cmd); p.stick = [cmd.mx, cmd.my, cmd.mz || 0];
  if (p.state === 'dead') return;
  // Mode switches (Echo's scarf, Nova's bracer attachment and secondary weapon) are instant, so a press
  // during hitstop is never lost
  if (cmd.pressed.mode && p.modeCd === 0 && p.state !== 'downed' && p.state !== 'ult') {
    if (p.char === 'echo') cycleScarf(p, world);
    else if (marksman(p)) cycleAttachment(p, world);
    else if (p.char === 'fix') cycleGadget(p, world);
  }
  if (cmd.pressed.sub && p.subSwCd === 0 && p.state !== 'downed' && p.state !== 'ult') {
    if (marksman(p)) cycleSub(p, world);
    else if (p.char === 'fix') cyclePower(p, world);
  }
  if (p.hitstop > 0) { p.hitstop--; return; }
  // Both triggers together with a full bar: the ultimate (the world takes over from here)
  if (p.ult >= ULT.max && chordReady(p, cmd) && !world.ultCast && p.state !== 'downed' && p.state !== 'ult') { world.startUlt(p); return; }

  p.st++;
  for (const k of ['mercy', 'dashCd', 'fireCd', 'bulwarkCd', 'tracerCd', 'controlLock', 'launchedT',
    'zipArriveT', 'boostT', 'dropT', 'riposteT', 'coyote', 'modeCd', 'ambushT', 'shootT', 'carveT', 'rocketT',
    'rifleCd', 'wallCoyote', 'subSwCd', 'dodgeCd', 'overclockT', 'tuneT', 'braceT', 'padCd', 'furyT']) if (p[k] > 0) p[k]--;
  // Ability cooldowns recharge faster under Fix's boosts (Overclock, Tune-Up, an Amp Coil)
  const rate = boostRate(p);
  for (const k of ['aegisCd', 'burstCd', 'wallCd', 'linkCd', 'provokeCd']) if (p[k] > 0) p[k] = Math.max(0, p[k] - rate);
  // Aegis time, and Overcharge draining once it has not grown for a while
  if (p.aegis && --p.aegis.t <= 0) world.endAegis(p, 'expire');
  if (p.overcharge > 0) { if (p.overT > 0) p.overT--; else p.overcharge = Math.max(0, p.overcharge - AEGIS.over.drain); }
  p.postDash = Math.min(99, p.postDash + 1);
  if (p.char === 'echo') tickEcho(p, world, cmd);
  else if (p.char === 'ram') tickRam(p, world);
  else if (p.char === 'fix') tickFix(p, world);
  else tickFocus(p, world);

  if (p.state === 'downed') { updateDowned(p, cmd, world); return; }
  updateLock(p, cmd, world);
  updateAim(p, cmd, world);
  p.meleeHeldT = cmd.held.melee ? p.meleeHeldT + 1 : 0;
  // RAM's abilities and Fix's gadgets are instant and work from most states
  if (p.char === 'ram') ramAbilities(p, world);
  else if (p.char === 'fix') fixAbilities(p, world);

  const wasSliding = p.wallSliding;
  p.wallPrev = wasSliding; p.wallSliding = false;   // set again by wallCling in the states that allow a wall slide
  switch (p.state) {
    case 'normal': stateNormal(p, cmd, world); break;
    case 'dashCharge': stateDashCharge(p, cmd, world); break;
    case 'beam': stateBeam(p, cmd, world); break;
    case 'dashslash': stateDashSlash(p, cmd, world); break;
    case 'dash': stateDash(p, cmd, world); break;
    case 'slide': stateSlide(p, cmd, world); break;
    case 'vb': stateVB(p, cmd, world); break;
    case 'attack': stateAttack(p, cmd, world); break;
    case 'parry': stateParry(p, cmd, world); break;
    case 'hitstun': stateHitstun(p, cmd, world); break;
    case 'bulwark': stateBulwark(p, cmd, world); break;
    case 'lash': stateLash(p, cmd, world); break;
    case 'zip': stateZip(p, cmd, world); break;
    case 'dive': stateDive(p, cmd, world); break;
    case 'pound': statePound(p, cmd, world); break;
    case 'dodge': stateDodge(p, cmd, world); break;
    case 'guard': stateGuard(p, cmd, world); break;
    case 'rush': stateRush(p, cmd, world); break;
    case 'leap': stateLeap(p, cmd, world); break;
    case 'patch': statePatch(p, cmd, world); break;
    case 'ult': world.ultStep(p, cmd); break;
  }
  if (p.state !== 'ult') handleFire(p, cmd, world);
  if (p.tossArmed) fixToss(p, cmd, world);
  // Boosters only run in the normal state; anything else (dash, hitstun, a burst...) cuts them
  if (p.thrusting && (p.state !== 'normal' || p.onGround)) { p.thrusting = false; world.emit('thrustOff', { p }); }
  if (p.wallSliding !== wasSliding) world.emit('wallSlide', { p, on: p.wallSliding, dir: p.wallSliding ? p.wallDir : p.lastWallDir, wx: p.wallSliding ? p.wallX : p.lastWallX, wz: p.wallSliding ? p.wallZ : p.lastWallZ });

  const wasGround = p.onGround, fallV = p.vy;
  // (RAM stays standing, braced, while he charges a Battering Ram)
  const low = p.crouch || p.state === 'slide' || (p.state === 'dashCharge' && p.char !== 'ram');
  if (!['dash', 'zip'].includes(p.state)) p.h = low ? CHARS[p.char].crouchH : CHARS[p.char].height;
  moveBody(p, DT);
  if (p.onGround) {
    p.coyote = COYOTE; p.jumpsUsed = 0; p.airDashes = 1; p.fastFall = false; p.dashCarry = false; p.airDodge = true; p.airRise = true;
    p.rockets = 0; p.rocketT = 0; p.wallCoyote = 0; if (p.fuel < MARKSMAN.boost.fuel) p.fuel = Math.min(MARKSMAN.boost.fuel, p.fuel + MARKSMAN.boost.refill);
    if (!wasGround && p.st > 1) world.emit('land', { p, vy: fallV });
    p.lastSafeX = p.x; p.lastSafeY = p.y; p.lastSafeZ = p.z;
  }
  if (p.wallDir && !p.onGround) {
    // Touching a wall gives back the air dash and the double jump, and remembers the wall for a late wall jump
    p.airDashes = 1; p.jumpsUsed = 0; p.airDodge = true; p.airRise = true; p.lastWallDir = p.wallDir; p.lastWallX = p.wallX; p.lastWallZ = p.wallZ; p.wallCoyote = WALL.coyote;
  }
  p.iframe = (SETTINGS.dashIframes && p.state === 'dash' && p.st <= 8) || (p.state === 'dash' && !!p.dash && p.st <= p.dash.iframes) ||
    (p.state === 'dodge' && !!p.dodge && p.dodge.t <= DODGE.iframes) || p.state === 'ult';
}

// ---- Shared action starters ------------------------------------------------------------

function tryJump(p, cmd, world) {
  if (p.buf.jump > JUMP_BUFFER) return false;
  if (p.onGround && p.crouch && cmd.my < -0.6 && world.onOneWay(p)) {
    p.dropT = 12; p.y -= 0.05; p.buf.jump = 99; p.onGround = false; return true;
  }
  if (p.onGround || p.coyote > 0) {
    const c = CHARS[p.char];
    p.vy = c.jumpV; p.coyote = 0; p.onGround = false; p.crouch = false;
    if (p.postDash <= 8) p.dashCarry = true;
    p.buf.jump = 99; setState(p, 'normal'); world.emit('jump', { p });
    return true;
  }
  const wd = p.wallDir !== 0 ? p.wallDir : p.wallCoyote > 0 ? p.lastWallDir : 0;
  if (wd !== 0) {
    // Holding away from the wall leaps off it; toward it or neutral is a climb kick that rises high and
    // lets you come straight back to the same wall. (wx, wz: the way into the wall)
    const wx = p.wallDir !== 0 ? p.wallX : p.lastWallX, wz = p.wallDir !== 0 ? p.wallZ : p.lastWallZ;
    const w = CHARS[p.char].wall, away = (cmd.mx || 0) * wx + (cmd.mz || 0) * wz < -0.3, k = away ? w.jumpVx : WALL.climb.vx;
    p.vx = -wx * k; p.vz = -wz * k; p.vy = w.jumpVy * (away ? WALL.leapVy : 1);
    p.controlLock = away ? w.lock : WALL.climb.lock; setFacing(p, -wx, -wz);
    p.wallCoyote = 0; p.wallStick = 0; p.wallSliding = false;
    p.buf.jump = 99; p.fastFall = false; setState(p, 'normal'); world.emit('walljump', { p, climb: !away, dir: -wd, wx: -wx, wz: -wz });
    return true;
  }
  if (p.jumpsUsed < 1) {
    // While a rocket launch still climbs faster than a double jump would, the press waits in the buffer
    if (p.rocketT > 0 && p.vy > CHARS[p.char].dblV) return false;
    p.vy = CHARS[p.char].dblV; p.jumpsUsed = 1; p.fastFall = false;
    p.buf.jump = 99; setState(p, 'normal'); world.emit('djump', { p });
    return true;
  }
  return false;
}

function tryDash(p, cmd, world) {
  if (p.buf.dash > ACTION_BUFFER || p.dashCd > 0) return false;
  const c = CHARS[p.char];
  if (p.onGround && cmd.my < -0.5) {
    p.buf.dash = 99; p.dashCd = c.dash.cooldown;
    const m = moveDir(cmd);
    if (m) setFacing(p, m[0], m[1]);
    p.vx = p.facing * c.slide.speed; p.vz = fz(p) * c.slide.speed; p.crouch = true;
    setState(p, 'slide'); world.emit('slide', { p });
    return true;
  }
  // Charged dash: on the ground with no direction held, holding dash plants the feet and charges
  if (SETTINGS.dashCharge && p.onGround && p.state === 'normal' && cmd.held.dash && stickMag(cmd) < 0.3 && Math.abs(cmd.my) < 0.5) {
    p.buf.dash = 99; p.dashChargeT = 0; p.crouch = false;
    setState(p, 'dashCharge'); world.emit('dashChargeStart', { p });
    return true;
  }
  if (!p.onGround && p.airDashes <= 0) return false;
  let d = dashDir(p, cmd);
  if (p.onGround && d[1] < 0) { const h = Math.hypot(d[0], d[2]); d = h > 1e-3 ? [d[0] / h, 0, d[2] / h] : [p.facing, 0, fz(p)]; }
  if (p.wallDir) {   // from a wall, a dash goes out from it
    const k = d[0] * p.wallX + d[2] * p.wallZ;
    if (k > 0) d = [d[0] - 2 * k * p.wallX, d[1], d[2] - 2 * k * p.wallZ];
  }
  if (!p.onGround) p.airDashes--;
  startDash(p, d, world, 0);
  return true;
}

// Which way a dash goes: the move stick on the ground plane (or the way he faces, out from a wall he slides on),
// tilted up or down 45 degrees by the vertical intent (cmd.my: up, or crouch); straight up or down with no direction
function dashDir(p, cmd) {
  const m = moveDir(cmd), v = cmd.my > 0.55 ? 1 : cmd.my < -0.55 ? -1 : 0;
  const h = m || (p.wallSliding ? [-p.wallX, -p.wallZ] : null);
  if (!h) return v ? [0, v, 0] : [p.facing, 0, fz(p)];
  const k = v ? Math.SQRT1_2 : 1;
  return [h[0] * k, v * Math.SQRT1_2, h[1] * k];
}

// Level 0 is an ordinary dash; 1-3 come from a charged release (DASH_CHARGE). RAM's dash is the Ram Charge.
function startDash(p, d, world, level) {
  if (p.char === 'ram') { startRush(p, world, level, d); return; }
  const c = CHARS[p.char], D = DASH_CHARGE, L = level - 1;
  p.buf.dash = 99; p.dashCd = c.dash.cooldown;
  setFacing(p, d[0], d[2]);
  p.dash = { dx: d[0], dy: d[1], dz: d[2], t: Math.round(c.dash.ticks * (level ? D.ticks[L] : 1)), grounded: p.onGround, level,
    speed: c.dash.speed * (level ? D.speed[L] : 1), keep: level ? D.exitKeep[L] : c.dash.exitKeep,
    iframes: level ? D.iframes[L] : 0, instance: level === 3 ? world.newInstance() : 0 };
  p.fastFall = false; p.crouch = false; p.dashChargeT = 0;
  setState(p, 'dash'); world.emit('dash', { p, level, dx: d[0], dy: d[1], dz: d[2] });
}

// Planted and charging: skid to a stop, aim with the stick, let go to launch. Jump or parry cancel it;
// a tap (released before DASH_CHARGE.tap) is an ordinary dash toward where you face.
function stateDashCharge(p, cmd, world) {
  const D = DASH_CHARGE, C = D.charge;
  // The tap window counts in real ticks; the charge itself grows faster under Fix's boosts
  const t0 = p.dashChargeT; p.dashChargeT += p.dashChargeT < D.tap ? 1 : boostRate(p);
  // For the first few ticks nothing changes, so a quick tap reads as an ordinary dash; then he plants
  if (p.dashChargeT >= D.tap) approach2(p, 0, 0, 70 * DT);
  applyGravity(p, cmd);
  crossed(t0, p.dashChargeT, C, level => world.emit('dashLevel', { p, level }));
  const m = moveDir(cmd); if (m) setFacing(p, m[0], m[1]);
  if (tryParry(p, world)) { p.dashChargeT = 0; return; }
  if (p.buf.jump <= JUMP_BUFFER || !p.onGround) {
    p.dashChargeT = 0; setState(p, 'normal'); world.emit('dashChargeEnd', { p });
    if (p.onGround) tryJump(p, cmd, world);
    return;
  }
  if (cmd.held.dash) return;
  const t = p.dashChargeT, level = t >= C[2] ? 3 : t >= C[1] ? 2 : t >= C[0] ? 1 : 0;
  let d = dashDir(p, cmd);
  if (d[1] < 0) { const h = Math.hypot(d[0], d[2]); d = h > 1e-3 ? [d[0] / h, 0, d[2] / h] : [p.facing, 0, fz(p)]; }   // no digging into the floor
  startDash(p, d, world, level);
}

function tryParry(p, world) {
  if (p.buf.parry > PARRY_BUFFER) return false;
  if (marksman(p)) return tryDodge(p, world);   // Nova's Marksman kit dodges instead (Echo keeps his parry and deflect)
  if (p.char === 'ram') return startGuard(p, world);   // RAM raises the Rampart
  if (p.char === 'fix') return startPatch(p, world);   // Fix runs the Patch Beam
  p.buf.parry = 99; p.parryT = 0; p.parryResult = null;
  setState(p, 'parry'); world.emit('parryStart', { p });
  return true;
}

function trySignature(p, cmd, world) {
  if (p.buf.sig > ACTION_BUFFER) return false;
  if (p.char === 'ram' || p.char === 'fix') return false;   // (their abilities: ramAbilities, fixAbilities)
  if (p.char === 'nova') {
    if (marksman(p)) {
      // Marksman kit: the hard-light Aegis. Instant: no state change, he keeps moving and shooting.
      // Pressing again while it is up detonates it outward.
      if (p.aegis) { p.buf.sig = 99; world.detonateAegis(p); return false; }
      if (p.aegisCd > 0) return false;
      p.buf.sig = 99; world.raiseAegis(p);
      return false;
    }
    if (p.bulwarkCd > 0) return false;
    p.buf.sig = 99; p.bulwarkCd = NOVA.bulwarkCd;
    setState(p, 'bulwark');
    return true;
  }
  // Every scarf Signature spends a scarf charge; Vanish is not spent while already hidden
  if (p.lashCharges <= 0 || (p.scarfMode === 'veil' && p.veiled)) return false;
  p.buf.sig = 99; p.lashCharges--;
  if (p.lashRecharge <= 0) p.lashRecharge = ECHO.lashRecharge;
  // Vanish and Challenge are instant: no state change, so the current action carries on
  if (p.scarfMode === 'veil') {
    p.veiled = true; p.veilCharge = SCARF.veilFade; p.veilBreakT = 0;
    world.shakeOffTrackers(p); world.emit('vanish', { p });
    return false;
  }
  if (p.scarfMode === 'flare') { world.challenge(p); return false; }
  const c = chest(p);
  const target = world.findLashTarget(p, c.x, c.y, c.z, p.aimX, p.aimY, p.aimZ, ECHO.lashRange);
  p.lash = { tx: c.x + p.aimX * ECHO.lashRange, ty: c.y + p.aimY * ECHO.lashRange, tz: c.z + p.aimZ * ECHO.lashRange, target, len: 0, hit: false };
  if (target) { p.lash.tx = target.x; p.lash.ty = target.y + target.h * 0.55; p.lash.tz = target.z; }
  const ah = aimH(p); if (ah) setFacing(p, ah[0], ah[1]);
  setState(p, 'lash'); world.emit('lash', { p });
  return true;
}

function tryMelee(p, cmd, world) {
  if (p.buf.melee > ACTION_BUFFER) return false;
  const hunter = p.char === 'echo' && SETTINGS.echoKit === 'hunter';
  // Echo on a wall: Wall Slash (before anything else, so a slide never turns it into something else)
  if (hunter && p.wallSliding && !p.onGround) { p.buf.melee = 99; startMove(p, 'echo_wall', world); return true; }
  // In the air, the secondary aimed down: the ground pound. Holding down to fast-fall must not turn it into
  // a Velocity Break; a dash, slide, launch or zip still does.
  const down = cmd.my < -0.55 || (p.aimFree && p.aimY < POUND.aimDown);
  const moving = p.state === 'dash' || p.state === 'slide' || p.postDash <= 6 || p.boostT > 0 || p.launchedT > 0 || p.zipArriveT > 0;
  if (!p.onGround && down && !moving && (p.char !== 'echo' || hunter)) { p.buf.melee = 99; startPound(p, world); return true; }
  const tier = vbTier(p);
  if (tier > 0) { velocityBreak(p, tier, world); return true; }
  // Rising attacks (up + melee, or crouch + melee on the ground; once per airtime in the air): Nova's Solar
  // Uppercut, RAM's Hydraulic Uplift, Fix's Jack-Up (Echo's Rising Glaive is below)
  const rise = cmd.my > 0.55 || (p.onGround && cmd.my < -0.55);
  if (p.char !== 'echo' && rise && (p.onGround || p.airRise)) {
    p.buf.melee = 99; if (!p.onGround) p.airRise = false;
    startMove(p, p.char + '_rise', world); return true;
  }
  if (p.char === 'fix') return fixMelee(p, world);
  if (marksman(p)) {
    // Marksman kit: close to an enemy, his bracer combo; otherwise his secondary weapon (it cancels whatever
    // it interrupts)
    if (meleeTarget(p, world)) { p.buf.melee = 99; startMove(p, p.onGround ? 'nova_k1' : 'nova_kair', world); return true; }
    return pressSub(p, world);
  }
  p.buf.melee = 99;
  if (p.char === 'echo' && p.riposteT > 0) { startMove(p, 'echo_riposte', world); p.riposteT = 0; return true; }
  if (p.char === 'echo' && !p.onGround && cmd.my < -0.55) {
    // Pursuit kit: the dive (fast fall into a Velocity Break on landing)
    breakVeil(p, world, 'attack');
    p.vy = -FAST_FALL; p.vx = p.facing * 5; p.vz = fz(p) * 5;
    p.hitConfirm = false; p.instance = world.newInstance();
    setState(p, 'dive'); world.emit('dive', { p });
    return true;
  }
  let id;
  if (p.char === 'ram') id = p.onGround ? 'ram_b1' : 'ram_air';
  else if (p.char === 'nova') id = p.onGround ? 'nova_jab1' : 'nova_air';
  else if (!p.onGround) id = hunter ? (cmd.my > 0.55 ? 'echo_spin' : 'echo_ab1') : 'echo_air1';
  else if (rise) id = 'echo_rise';   // Echo's rising attack in either kit
  else id = hunter ? 'echo_b1' : 'echo_g1';
  startMove(p, id, world);
  return true;
}

// Marksman kit: an enemy close enough in front for the bracer combo (or the lock-on target in reach)
function meleeTarget(p, world) {
  const R = MARKSMAN.melee, fa = faceAim(p), fx = fa ? fa[0] : p.facing, fzz = fa ? fa[1] : fz(p);
  for (const e of world.enemies) {
    if (e.dead) continue;
    const rx = e.x - p.x, rz = e.z - p.z, ahead = rx * fx + rz * fzz, side = Math.abs(rx * -fzz + rz * fx), gap = ahead - e.w / 2 - p.w / 2;
    const dy = Math.abs(e.y + e.h / 2 - (p.y + p.h * 0.5));
    if (ahead > -0.2 && gap < R.reach - 0.8 && side < e.w / 2 + 1.0 && dy < R.up) return e;
    if (e === p.lockT && lockChosen(p) && Math.hypot(rx, rz) < LOCK.magnet && dy < R.up) return e;
  }
  return null;
}

// A movement direction for a lunge or a break: his velocity if he is moving, else the way he faces; flattened
// to the ground when it barely climbs or dips
function strikeDir(p, flatten) {
  const sp = Math.hypot(p.vx, p.vy, p.vz);
  let dx = sp > 0.5 ? p.vx / sp : p.facing, dy = sp > 0.5 ? p.vy / sp : 0, dz = sp > 0.5 ? p.vz / sp : fz(p);
  if (p.state === 'dash' && p.dash) { dx = p.dash.dx; dy = p.dash.dy; dz = p.dash.dz || 0; }
  if (flatten && Math.abs(dy) < 0.45) {
    dy = 0; const h = Math.hypot(dx, dz);
    if (h > 1e-3) { dx /= h; dz /= h; } else { dx = p.facing; dz = fz(p); }
  }
  const m = Math.hypot(dx, dy, dz) || 1;
  return [dx / m, dy / m, dz / m];
}

// Echo's Dash Slash (Hunter kit's Velocity Break): a lunging cut along the dash that carries him through
function startDashSlash(p, tier, world) {
  breakVeil(p, world, 'attack');
  p.buf.melee = 99;
  const [dx, dy, dz] = strikeDir(p, true);
  setFacing(p, dx, dz);
  p.slash = { tier, dx, dy, dz };
  p.hitConfirm = false; p.instance = world.newInstance();
  p.boostT = 0; p.launchedT = 0; p.zipArriveT = 0; p.postDash = 99;
  setState(p, 'dashslash'); world.emit('dashSlash', { p, tier, dx, dy, dz });
}

function stateDashSlash(p, cmd, world) {
  const D = DASH_SLASH, s = p.slash, t = p.st, T = s.tier - 1;
  if (t <= D.ticks) { const v = D.speed[T] * Math.pow(D.keep, t); p.vx = s.dx * v; p.vy = s.dy * v; p.vz = s.dz * v; }
  else if (!p.onGround) { const r = CHARS[p.char].run * 0.5; approach2(p, (cmd.mx || 0) * r, (cmd.mz || 0) * r, 30 * DT); applyGravity(p, cmd); }
  else { p.vx *= 0.8; p.vz *= 0.8; p.vy = -0.5; }
  if (t >= 2 && t <= D.ticks - 3) {
    const cy = 0.95 + s.dy * 0.5, b = fwdBoxP(p, D.box.fx, D.box.w, cy - D.box.h / 2, cy + D.box.h / 2);
    world.spawnHitbox({ owner: p, team: 'p', ...b,
      dmg: D.dmg[T], poise: D.poise[T], kb: [p.facing * 7, 3, fz(p) * 7], armorBreak: D.armorBreak[T], instance: p.instance, vbTier: s.tier, dashSlash: true });
  }
  if (p.hitConfirm && t >= 4) {
    if (SETTINGS.vbRefund && !p.onGround) p.airDashes = 1;
    if (cancelInto(p, cmd, world)) return;
  }
  if (t >= D.ticks + (p.hitConfirm ? D.hitRecover : D.recover)) setState(p, 'normal');
}
// A player's strike box: `off` m ahead of him, `len` m long, at least 1.3 m wide across the way he faces
const fwdBoxP = (p, off, len, y0, y1) => fwdBox(p, off, len, y0, y1, Math.max(len, 1.3));

function startMove(p, id, world) {
  breakVeil(p, world, 'attack');
  p.moveId = id; p.move = MOVES[id]; p.queued = null; p.hitConfirm = false; p.instance = world.newInstance();
  p.crouch = false; p.riseAir = !p.onGround;
  const fa = faceAim(p); if (fa) setFacing(p, fa[0], fa[1]);
  // Lock-on: turn to a target that is close, and step in toward it during the swing (lungeTo)
  p.lungeTo = null;
  const t = p.lockT;
  if (t && !t.dead && hdist(p, t) < LOCK.magnet && Math.abs(t.y - p.y) < 2.5) {
    const u = away(p, t, p); setFacing(p, u.x, u.z); p.lungeTo = t;
    if (p.onGround && hdist(p, t) - t.w / 2 - p.w / 2 > 0.3) { p.vx = p.facing * LOCK.lunge; p.vz = fz(p) * LOCK.lunge; }   // the step starts at once
  }
  setState(p, 'attack'); world.emit('swing', { p, id });
}

// A Velocity Break: Echo's Hunter kit turns it into the Dash Slash
function velocityBreak(p, tier, world) {
  if (p.char === 'echo' && SETTINGS.echoKit === 'hunter') startDashSlash(p, Math.max(1, tier), world); else startVB(p, tier, world);
}

function startVB(p, tier, world) {
  breakVeil(p, world, 'attack');
  p.buf.melee = 99;
  const [dx, dy, dz] = strikeDir(p, false);
  if (Math.hypot(dx, dz) > 0.2) setFacing(p, dx, dz);
  p.vbInfo = { tier, dx, dy, dz, keep: SETTINGS.vbStop === 'keep30' ? 0.3 : 0, v0x: p.vx, v0y: p.vy, v0z: p.vz };
  p.hitConfirm = false; p.instance = world.newInstance();
  p.boostT = 0; p.launchedT = 0; p.zipArriveT = 0; p.postDash = 99;
  setState(p, 'vb'); world.emit('vbStart', { p, tier });
}

// Anything that may interrupt a state early (on hit-confirm or late whiff recovery).
function cancelInto(p, cmd, world, { jump = true, dash = true, parry = true, sig = true, melee = true } = {}) {
  if (parry && tryParry(p, world)) return true;
  if (dash && tryDash(p, cmd, world)) return true;
  if (jump && tryJump(p, cmd, world)) return true;
  if (sig && trySignature(p, cmd, world)) return true;
  if (melee && tryMelee(p, cmd, world)) return true;
  return false;
}

// ---- States ----------------------------------------------------------------------------

function horizontalControl(p, cmd, world, scale = 1) {
  const c = CHARS[p.char], skates = marksman(p);
  let k = (skates ? MARKSMAN.skate.top : c.run) * (p.crouch ? c.crouchSpeed : 1) * (p.thrusting ? MARKSMAN.boost.air : 1) * scale;
  const rifle = p.rifleT >= HUNTER.rifle.raise;   // Echo's staff-rifle up: slower on foot
  if (rifle && p.onGround) k *= HUNTER.rifle.slow;
  const mx = cmd.mx || 0, mz = cmd.mz || 0, moving = Math.hypot(mx, mz) > 0.1;
  const firing = p.chargeT > 0 || p.fireCd > 0 || p.shootT > 0 || rifle;
  const ah = aimH(p), facingAim = ((p.aimFree && !p.camAim) || firing) && ah;
  const t = p.lockT && !p.lockT.dead ? p.lockT : null;
  if (facingAim) {
    // Facing where he shoots, he strafes; moving away from the aim is a slower backpedal
    if (moving && mx * ah[0] + mz * ah[1] < -0.3 * Math.hypot(mx, mz)) k *= skates ? MARKSMAN.skate.backpedal : c.backpedal;
    setFacing(p, ah[0], ah[1]);
  } else if (t && (!moving || (p.camAim && lockChosen(p)))) {
    // Locked on: standing still he faces the target; a target he picked himself, he circles facing it
    if (p.camAim) { const u = away(p, t, p); setFacing(p, u.x, u.z); }
    else if (Math.hypot(p.aimX, p.aimZ || 0) > 0.05) setFacing(p, p.aimX, p.aimZ || 0);
  } else if (moving && p.controlLock === 0) {
    setFacing(p, mx, mz);
  }
  if (p.controlLock > 0) return;
  const tx = mx * k, tz = mz * k;
  if (p.onGround && skates) { skateGround(p, tx, tz, world); return; }
  if (p.onGround) {
    const same = (tx !== 0 || tz !== 0) && tx * p.vx + tz * p.vz > 0;
    approach2(p, tx, tz, (same || Math.hypot(p.vx, p.vz) < 0.1 ? c.accelG : c.decelG) * DT);
  } else {
    // Keep dash-carried momentum in the air unless the player steers against it
    const sp = Math.hypot(p.vx, p.vz);
    if (p.dashCarry && sp > Math.hypot(tx, tz) && tx * p.vx + tz * p.vz >= 0) return;
    approach2(p, tx, tz, c.accelA * DT);
  }
}

// Skate-blade glide: a little slower to reach top speed, keeps momentum when the stick is let go,
// and carves to a stop when reversed. Crouching at speed tucks into a low glide.
function skateGround(p, tx, tz, world) {
  const S = MARKSMAN.skate, speed = Math.hypot(p.vx, p.vz), want = Math.hypot(tx, tz);
  let a;
  if (p.crouch && speed > S.tuckMin) { tx = 0; tz = 0; a = S.tuck; }
  else if (want === 0) a = S.coast;
  else if (tx * p.vx + tz * p.vz < 0 && speed > 0.5) {
    a = S.carve;
    if (speed > 5 && p.carveT === 0) { p.carveT = 10; world.emit('carve', { p }); }
  } else a = speed < want ? S.accel : S.coast;
  approach2(p, tx, tz, a * DT);
}

function applyGravity(p, cmd, mult = 1) {
  if (p.onGround && p.vy <= 0) { p.vy = -0.5; return; }
  let g = GRAVITY * mult;
  if (p.vy < 0) g *= FALL_MULT;
  else if (!cmd.held.jump && !p.dashCarry) g *= RISE_CUT_MULT;
  p.vy -= g * DT;
  const cap = p.fastFall ? FAST_FALL : MAX_FALL;
  if (p.vy < -cap) p.vy = -cap;
}

const CHARGED = { nova: 'nova_brace', echo: 'echo_charged', ram: 'ram_slam', fix: 'fix_slam' };
function stateNormal(p, cmd, world) {
  const c = CHARS[p.char];
  // RAM raises the Rampart, and Fix runs the Patch Beam, for as long as the button is held
  if (cmd.held.parry && p.char === 'ram' && !p.guardBroken) { startGuard(p, world); stateGuard(p, cmd, world); return; }
  if (cmd.held.parry && p.char === 'fix') { startPatch(p, world); statePatch(p, cmd, world); return; }
  if (p.onGround && cmd.my < -0.55) p.crouch = true;
  else if (p.crouch && hasHeadroom(p.x, p.y, p.z, p.w, c.height)) p.crouch = false;

  horizontalControl(p, cmd, world);
  if (!p.onGround && cmd.my < -0.7 && p.vy < 3 && p.wallDir === 0) p.fastFall = true;
  if (!thrust(p, cmd, world)) applyGravity(p, cmd);
  wallCling(p, cmd, true);
  cancelInto(p, cmd, world);
  if (p.state !== 'normal') { if (!cmd.held.melee) p.meleeCharged = false; return; }
  if (marksman(p)) return;   // the Marksman kit charges its secondary blaster instead (fireMarksman)
  // Charged melee: release after holding
  if (p.meleeCharged && !cmd.held.melee) {
    p.meleeCharged = false;
    p.tossArmed = false;
    startMove(p, CHARGED[p.char], world);
  }
  if (p.meleeHeldT >= 30 && p.state === 'normal' && !p.meleeCharged) { p.meleeCharged = true; world.emit('meleeCharged', { p }); }
}

// Wall slide (every character, in the normal, attack and parry states). Holding toward a wall in the air
// while not rising starts it; it grips for a moment, then eases up to the slide speed, braking a fall on
// the way in. Letting go of the stick keeps the grip for WALL.stick ticks (so press away, then jump, is
// still a wall jump). The character faces out from the wall; `turn` is off during an attack so a swing
// already under way keeps its direction.
function wallCling(p, cmd, turn) {
  const c = CHARS[p.char], W = WALL, was = !!p.wallPrev;
  let on = false;
  if (!p.onGround && p.wallDir !== 0 && p.vy <= 0.5) {
    const toward = (cmd.mx || 0) * p.wallX + (cmd.mz || 0) * p.wallZ > 0.3;
    if (toward) p.wallStick = W.stick;
    else if (was && p.wallStick > 0) p.wallStick--;
    on = toward || (was && p.wallStick > 0);
  }
  if (!on) { p.wallT = 0; return false; }
  p.wallT = was ? p.wallT + 1 : 0;
  const ramp = Math.max(0, Math.min(1, (p.wallT - W.grip) / W.ease));
  const target = cmd.my < -0.6 ? c.wall.slide * W.fast : W.gripSpeed + (c.wall.slide - W.gripSpeed) * ramp;
  if (p.vy < -target) p.vy = Math.min(-target, p.vy + (W.brake + GRAVITY * FALL_MULT) * DT);   // brake a fall into the slide
  else p.vy = Math.max(p.vy, -target);
  if ((cmd.mx || 0) * p.wallX + (cmd.mz || 0) * p.wallZ <= 0.3) { p.vx = p.wallX * 0.5; p.vz = p.wallZ * 0.5; }   // grip: stay against the wall
  p.wallSliding = true; p.fastFall = false;
  if (turn) setFacing(p, -p.wallX, -p.wallZ);
  return true;
}

// Marksman kit: light boosters. Once the double jump is spent, pressing jump again fires them and
// holding keeps them on: he hovers and climbs gently on a small tank of fuel that refills on the
// ground. Ordinary jumps are untouched. Returns true while thrusting.
function thrust(p, cmd, world) {
  if (!marksman(p)) return false;
  const B = MARKSMAN.boost;
  const start = cmd.pressed.jump && p.jumpsUsed >= 1 && p.wallDir === 0 && p.fuel >= B.minStart;
  const on = !p.onGround && !p.wallSliding && p.fuel > 0 && cmd.held.jump && (p.thrusting || start);
  if (on !== p.thrusting) { p.thrusting = on; world.emit(on ? 'thrustOn' : 'thrustOff', { p }); }
  if (!on) return false;
  p.fuel = Math.max(0, p.fuel - 1); p.fastFall = false;
  // They only add lift below their climb speed, so they never cut a rising jump short
  if (p.vy > B.rise) p.vy -= GRAVITY * DT; else p.vy = approach(p.vy, B.rise, B.thrust * DT);
  return true;
}

function stateDash(p, cmd, world) {
  const c = CHARS[p.char], d = p.dash;
  if (d.pursuit) {
    const t = d.pursuit;
    if (t.dead) d.pursuit = null;
    else {
      const u = away(p, t, p), gap = t.w / 2 + p.w / 2 + 0.2, tx = t.x - u.x * gap, tz = t.z - u.z * gap, ty = t.y + t.h * 0.3;
      const dx = tx - p.x, dy = ty - p.y, dz = tz - p.z, m = Math.hypot(dx, dy, dz);
      if (m < 0.9) { p.zipArriveT = 12; d.t = 0; } else { d.dx = dx / m; d.dy = dy / m; d.dz = dz / m; if (Math.hypot(d.dx, d.dz) > 0.2) setFacing(p, d.dx, d.dz); }
    }
  }
  const boost = p.boostT > 0 ? 1.35 : 1, speed = d.speed || c.dash.speed;
  p.vx = d.dx * speed * boost; p.vy = d.dy * speed * boost; p.vz = (d.dz || 0) * speed * boost;
  d.t--;
  if (d.level === 3) {
    // A full charge turns the dash into a strike through everything in its path (each enemy once)
    const S = DASH_CHARGE.strike;
    const h = Math.hypot(d.dx, d.dz || 0), kx = h > 0.1 ? d.dx / h : p.facing, kz = h > 0.1 ? (d.dz || 0) / h : fz(p);
    world.spawnHitbox({ owner: p, team: 'p', ...boxAt(p.x, p.z, 0.8, p.y, p.y + p.h + 0.2), dmg: S.dmg, poise: S.poise,
      kb: [kx * S.kb, 3, kz * S.kb], armorBreak: true, instance: d.instance, dashStrike: true });
  }
  if (p.buf.jump <= JUMP_BUFFER && (d.grounded || p.coyote > 0) && d.dy <= 0) {
    // Dash-jump: a charged dash carries more speed into the jump, up to DASH_CHARGE.jumpCarry
    const j = Math.min(speed, DASH_CHARGE.jumpCarry) * 0.85;
    p.vy = c.jumpV; p.vx = d.dx * j; p.vz = (d.dz || 0) * j; p.dashCarry = true; p.onGround = false;
    p.buf.jump = 99; setState(p, 'normal'); world.emit('jump', { p, dashJump: true });
    return;
  }
  if (p.buf.melee <= ACTION_BUFFER) { velocityBreak(p, vbTier(p), world); return; }
  if (tryParry(p, world)) return;
  if (d.t <= 0 || p.hitWall) {
    const keep = speed * (d.keep ?? c.dash.exitKeep);
    p.vx = d.dx * keep; p.vz = (d.dz || 0) * keep; p.vy = d.dy > 0 ? d.dy * speed * 0.4 : 0;
    p.postDash = 0; setState(p, 'normal');
  }
}

function stateSlide(p, cmd, world) {
  const c = CHARS[p.char];
  p.vx *= c.slide.decay; p.vz *= c.slide.decay; applyGravity(p, cmd);
  if (tryJump(p, cmd, world)) { p.dashCarry = true; return; }
  if (p.buf.melee <= ACTION_BUFFER) { velocityBreak(p, 1, world); return; }
  if (tryParry(p, world)) return;
  if (p.st >= c.slide.ticks || Math.hypot(p.vx, p.vz) < 2 || !p.onGround) {
    p.crouch = !hasHeadroom(p.x, p.y, p.z, p.w, c.height);
    setState(p, 'normal');
  }
}

function stateVB(p, cmd, world) {
  const v = p.vbInfo, t = p.st;
  if (t <= VB.stopTicks) {
    const k = t >= VB.stopTicks ? v.keep : 1 - (1 - v.keep) * (t / VB.stopTicks);
    p.vx = v.v0x * k; p.vy = v.v0y * k; p.vz = (v.v0z || 0) * k;
  } else if (!p.onGround) {
    if (t < VB.activeTo) p.vy = Math.max(p.vy, 0);         // brief hang for precision stops
    else applyGravity(p, cmd);
  } else { p.vx *= 0.8; p.vz *= 0.8; p.vy = -0.5; }
  if (t >= VB.activeFrom && t < VB.activeTo) {
    const down = v.dy < -0.6;
    const box = down ? boxAt(p.x, p.z, 1.0, p.y - 0.4, p.y + 1.0) : fwdBoxP(p, 0.85, 1.7, 0.2, 1.6);
    const tier = VB.tiers[v.tier];
    world.spawnHitbox({ owner: p, team: 'p', ...box, dmg: tier.dmg, poise: tier.poise, kb: [p.facing * tier.kb, down ? 4 : 3, fz(p) * tier.kb],
      armorBreak: tier.armorBreak, instance: p.instance, vbTier: v.tier });
  }
  if (p.hitConfirm && t >= VB.activeFrom) {
    if (SETTINGS.vbRefund && !p.onGround) p.airDashes = 1;
    if (cancelInto(p, cmd, world)) return;
  }
  const end = VB.activeTo + (p.hitConfirm ? 4 : VB.whiffRecovery);
  if (t >= VB.activeTo + VB.driftAfter && !p.onGround) { const r = CHARS[p.char].run * 0.5; approach2(p, (cmd.mx || 0) * r, (cmd.mz || 0) * r, 30 * DT); }
  if (t >= end) setState(p, 'normal');
}

function stateDive(p, cmd, world) {
  p.vy = -FAST_FALL; p.fastFall = true;
  const hitBelow = world.enemyBelow(p, 1.2);
  if (p.onGround || hitBelow || p.st > 90) {
    p.vbInfo = { tier: 2, dx: 0, dy: -1, dz: 0, keep: 0, v0x: p.vx, v0y: p.vy, v0z: p.vz };
    p.hitConfirm = false; p.instance = world.newInstance();
    setState(p, 'vb'); world.emit('vbStart', { p, tier: 2 });
  }
}

// Ground pound (POUND). Phases: 'hold' (he hangs; holding the button charges it), 'drop' (a fast fall that
// hits what it passes through), 'land' (the scatter blast has gone off; a short recovery). A parry or dash
// cancels the hold.
function startPound(p, world) {
  breakVeil(p, world, 'attack');
  p.pound = { phase: 'hold', t: 0, level: 0, held: true, y0: p.y };
  p.hitConfirm = false; p.instance = world.newInstance();
  p.dashCarry = false; p.fastFall = false; p.lungeTo = null;
  const fa = faceAim(p); if (fa) setFacing(p, fa[0], fa[1]);
  setState(p, 'pound'); world.emit('poundStart', { p });
}

function statePound(p, cmd, world) {
  const P = POUND, S = p.pound;
  if (!S) { setState(p, 'normal'); return; }
  S.t++;
  if (S.phase === 'hold') {
    // The mid-air slowdown: his rise and drift die away fast and he sinks slowly while it charges
    approach2(p, 0, 0, 50 * DT); p.vy = approach(p.vy, -P.hang, 80 * DT); p.fastFall = false;
    if (!cmd.held.melee) S.held = false;
    if (S.held) {
      S.c = (S.c || 0) + boostRate(p);
      const lv = S.c >= P.charge[2] ? 3 : S.c >= P.charge[1] ? 2 : S.c >= P.charge[0] ? 1 : 0;
      if (lv > S.level) { S.level = lv; world.emit('poundLevel', { p, level: lv }); }
    }
    if (tryParry(p, world) || tryDash(p, cmd, world)) { p.pound = null; return; }
    if (p.onGround) { landPound(p, world); return; }
    if ((!S.held && S.t >= P.windup) || S.t >= P.maxHold) { S.phase = 'drop'; S.t = 0; S.y0 = p.y; p.instance = world.newInstance(); world.emit('poundDrop', { p, level: S.level }); }
    return;
  }
  if (S.phase === 'drop') {
    // Echo's quick pound bounces off what it hits
    if (p.char === 'echo' && S.level === 0 && p.hitConfirm) {
      p.vy = P.bounce; p.vx *= 0.5; p.vz *= 0.5; p.airDashes = 1; p.jumpsUsed = 0; p.fastFall = false; p.dashCarry = false;
      p.pound = null; setState(p, 'normal'); world.emit('pogo', { p });
      return;
    }
    p.vy = -P.speed; p.fastFall = true; p.vx *= 0.9; p.vz *= 0.9;
    const k = 1 + 0.25 * S.level;
    world.spawnHitbox({ owner: p, team: 'p', ...boxAt(p.x, p.z, P.box.w / 2, p.y - 0.7, p.y + P.box.h - 0.7),
      dmg: P.drop.dmg * k, poise: P.drop.poise * k, kb: [0, -4, 0], instance: p.instance, pound: true });
    if (p.onGround) { landPound(p, world); return; }
    if (S.t > P.maxDrop) { p.pound = null; setState(p, 'normal'); }   // fell a long way (a pit)
    return;
  }
  // 'land': the blast has gone off; a short recovery, cancellable once it has hit something
  p.vx *= 0.7; p.vz *= 0.7; p.vy = -0.5;
  if (p.hitConfirm) S.hit = true;
  if (S.hit && S.t >= P.hitRecover && cancelInto(p, cmd, world, { melee: false })) { p.pound = null; return; }
  if (S.t >= P.recover) { p.pound = null; setState(p, 'normal'); }
}

// The scatter blast: every enemy in reach is hit and thrown outward, away from the impact
function landPound(p, world) {
  const P = POUND, S = p.pound, L = P.land[S.level], fall = Math.max(0, S.y0 - p.y);
  // RAM's Meteor Drop lands harder and wider; Fix's sends out a repair pulse as well
  const K = p.char === 'ram' ? RAM.pound : 1;
  const r = (L.r + P.fallBonus * Math.min(1, fall / 12)) * K;
  const inst = world.newInstance();
  world.spawnHitbox({ owner: p, team: 'p', ...boxAt(p.x, p.z, r, p.y - 0.3, p.y + 1.6 + 0.3 * S.level), dmg: L.dmg * K, poise: L.poise * K,
    kb: [L.kb, L.up, 0], radial: true, cx: p.x, cz: p.z, armorBreak: !!L.armorBreak || K > 1, instance: inst, scatter: true, ramKnock: p.char === 'ram' });
  if (p.char === 'fix') world.repairPulse(p, p.x, p.y, p.z, r + 1, FIX.poundHeal[S.level]);
  S.phase = 'land'; S.t = 0; S.inst = inst; S.hit = false;
  p.vx = 0; p.vz = 0; p.hitConfirm = false; p.hitstop = 2 + S.level;   // a beat of impact freeze, longer the bigger the pound
  world.emit('poundLand', { p, x: p.x, y: p.y, z: p.z, level: S.level, r, fall });
}

function stateAttack(p, cmd, world) {
  const m = p.move, t = p.st, f = fz(p);
  const activeStart = m.su, activeEnd = m.su + m.ac, end = m.su + m.ac + m.rc;
  // A rising attack takes off (no rise cut); from the air it climbs a little less
  if (m.rise && t === activeStart) {
    const k = m.fist ? 1.5 : 2.5;
    p.vy = m.rise * (p.riseAir ? m.airRise || 1 : 1); p.onGround = false; p.vx = p.facing * k; p.vz = f * k; p.dashCarry = true; p.fastFall = false;
  }
  if (m.riseBlast && t === activeEnd) {
    // The Solar Uppercut's flare: a burst of light off the fist at the top of the climb
    world.explode({ owner: p, x: p.x + p.facing * 0.35, y: p.y + p.h + 0.55, z: p.z + f * 0.35, spec: { ...m.riseBlast, armorBreak: false }, kind: m.blastKind || 'riseBlast', level: 1 });
  }
  if (t === activeStart) {
    if (m.jack && !p.riseAir) world.placePad(p);           // Jack-Up: the jack stays behind as a spring pad
    if (m.quake) world.spawnQuake(p, m.quake);              // Seismic Slam: a shockwave ring along the floor
    if (m.spark) world.sparkRing(p, m.spark);               // Torque Slam: a ring of sparks
  }
  if (m.sweep && t >= activeStart && t < activeEnd) world.sweepShots(p);   // Hydraulic Uplift: shots over him are swept away
  // On the ground an attack keeps pressing into the floor, so it never reads as airborne mid-swing
  if (p.onGround) { p.vx *= 0.82; p.vz *= 0.82; p.vy = -0.5; }
  else { applyGravity(p, cmd, m.hoverAll ?? (m.hover && p.hitConfirm ? 0.25 : 1)); wallCling(p, cmd, false); }
  if (m.hover && p.hitConfirm && p.vy < 1.5) p.vy = 1.5;
  if (m.hoverAll && p.vy < -3) p.vy = -3;
  if (t === activeStart && p.onGround && !m.launcher) { p.vx += p.facing * 2.2; p.vz += f * 2.2; }
  if (m.multi && t > activeStart && t < activeEnd && (t - activeStart) % m.multi === 0) p.instance = world.newInstance();
  if (t === activeStart && m.blastFist) {
    world.explode({ owner: p, x: p.x + p.facing * 1.2, y: p.y + 1.1, z: p.z + f * 1.2, spec: { ...m.blastFist, armorBreak: false }, kind: 'blast', level: 1 });
  }
  if (t === activeStart && m.wave && SETTINGS.echoKit === 'hunter') {
    // Crescent wave: the charged swing looses an energy crescent that flies on and cuts through shots
    const W = m.wave, x = p.x + p.facing * 1.0, y = p.y + 1.0, z = p.z + f * 1.0;
    world.spawnProjectile({ team: 'p', owner: p, x, y, z, vx: p.facing * W.speed, vy: 0, vz: f * W.speed, ttl: W.ttl, r: W.r,
      dmg: W.dmg, poise: W.poise, kb: 6, pierce: true, intercept: true, interceptHeavy: true, kind: 'wave' });
    world.emit('crescent', { p, x, y, z });
  }
  if (p.lungeTo && t < activeEnd && p.onGround) {
    // Locked on: close the gap to the target until the swing lands
    const e = p.lungeTo, gap = hdist(p, e) - e.w / 2 - p.w / 2;
    if (!e.dead && gap > 0.3) { const u = away(p, e, p), v = Math.min(LOCK.lunge, gap * 30); setFacing(p, u.x, u.z); p.vx = u.x * v; p.vz = u.z * v; }
    else p.lungeTo = null;
  }
  if (t >= activeStart && t < activeEnd) {
    const b = m.box, f2 = fz(p);
    const box = m.spin ? boxAt(p.x, p.z, b.w / 2, p.y + b.y - b.h / 2, p.y + b.y + b.h / 2) : fwdBoxP(p, b.fx, b.w, b.y - b.h / 2, b.y + b.h / 2);
    world.spawnHitbox({ owner: p, team: 'p', ...box,
      dmg: m.dmg, poise: m.poise, kb: [p.facing * m.kb[0], m.kb[1], f2 * m.kb[0]], armorBreak: !!m.armorBreak, heavy: !!m.heavy,
      launcher: !!m.launcher, shove: !!m.shove, instance: p.instance, moveId: p.moveId, spin: !!m.spin, cx: p.x, cz: p.z,
      wrench: !!m.wrench, ram: p.char === 'ram' && !!m.shield, ramKnock: p.char === 'ram' });
  }
  if (m.launcher && t === activeEnd && p.hitConfirm) p.vy = 9;   // Echo hops after a launched enemy
  if (p.buf.melee <= ACTION_BUFFER && m.next && t >= activeStart) p.queued = m.next;
  if (t >= activeEnd) {
    if (p.queued && t >= activeEnd + 2) {
      const nxt = p.queued;
      if (MOVES[nxt].air === !p.onGround || !MOVES[nxt].air) { p.buf.melee = 99; startMove(p, nxt, world); return; }
    }
    const lateWhiff = t >= activeEnd + Math.floor(m.rc * 0.6);
    if (p.hitConfirm) { if (cancelInto(p, cmd, world, { melee: false })) return; }
    else if (lateWhiff) { if (cancelInto(p, cmd, world, { jump: false, sig: false, melee: false })) return; }
  }
  if (t >= end) setState(p, 'normal');
}

function stateParry(p, cmd, world) {
  p.parryT++;
  if (p.onGround) { p.vx *= 0.7; p.vz *= 0.7; } else { applyGravity(p, cmd); approach2(p, (cmd.mx || 0) * 2, (cmd.mz || 0) * 2, 20 * DT); wallCling(p, cmd, true); }
  if (p.parryResult) {
    // Successful parry: short, cancellable recovery
    if (p.st > 3 && cancelInto(p, cmd, world)) return;
    if (p.st > 10) setState(p, 'normal');
    return;
  }
  if (p.parryT >= PARRY.window + PARRY.whiff) setState(p, 'normal');
}

function stateHitstun(p, cmd, world) {
  if (p.onGround) { p.vx *= 0.85; p.vz *= 0.85; }
  applyGravity(p, cmd);
  if (p.st >= p.stun) setState(p, 'normal');
}

function stateBulwark(p, cmd, world) {
  if (p.onGround) { p.vx *= 0.7; p.vz *= 0.7; } else { p.vy = Math.max(p.vy - 10 * DT, -2); }
  if (p.st === 3) world.bulwarkPulse(p);
  if (p.st >= 8 && cancelInto(p, cmd, world, { sig: false })) return;
  if (p.st >= 14) setState(p, 'normal');
}

function stateLash(p, cmd, world) {
  const L = p.lash;
  if (p.onGround) { p.vx *= 0.75; p.vz *= 0.75; } else { p.vy = Math.max(p.vy - 20 * DT, -3); }
  L.len = Math.min(1, p.st / 8);
  const hunter = SETTINGS.echoKit === 'hunter';
  if (p.st === 8 && L.target) {
    L.hit = true;
    const heavy = L.target.kind === 'enemy' && !(L.target.light && L.target.armor <= 0);
    // Hunter kit: keep holding to reel a light enemy in; a heavy one waits a moment to tell yank from zip
    if (hunter && heavy && cmd.held.sig) L.pending = true;
    else { world.lashConnect(p, L.target, hunter && cmd.held.sig); if (p.state !== 'lash') return; }
  }
  if (L.pending && p.st === 13) {
    L.pending = false;
    world.lashConnect(p, L.target, cmd.held.sig);
    if (p.state !== 'lash') return;
  }
  if (p.st >= 8 && L.hit && !L.pending && cancelInto(p, cmd, world, { sig: false })) { p.lash = null; return; }
  if (p.st >= (L.hit ? 14 : 20)) { p.lash = null; setState(p, 'normal'); }
}

function stateZip(p, cmd, world) {
  const z = p.zip, tgt = z.target;
  const u = away(p, tgt, p), gap = tgt.w / 2 + p.w / 2 + 0.1, tx = tgt.x - u.x * gap, tz = tgt.z - u.z * gap, ty = tgt.y + 0.2;
  const dx = tx - p.x, dy = ty - p.y, dz = tz - p.z, d = Math.hypot(dx, dy, dz);
  if (d < 0.6 || p.st > 30 || tgt.dead || p.hitWall) {
    p.vx *= 0.6; p.vy *= 0.6; p.vz *= 0.6; p.zipArriveT = 12; p.lash = null;
    setState(p, 'normal');
    return;
  }
  p.vx = dx / d * ECHO.zipSpeed; p.vy = dy / d * ECHO.zipSpeed; p.vz = dz / d * ECHO.zipSpeed;
  setFacing(p, dx, dz);
  if (p.buf.melee <= ACTION_BUFFER && d < 3) { p.zipArriveT = 12; velocityBreak(p, 2, world); }
}

function updateDowned(p, cmd, world) {
  p.vx = (cmd.mx || 0) * 1.2; p.vz = (cmd.mz || 0) * 1.2; p.crouch = false;
  applyGravity(p, cmd);
  p.h = 0.6;
  moveBody(p, DT);
  p.downedT--;
  if (p.downedT <= 0) world.bleedOut(p);
}

// Echo's utility belt: throw a snare, or plant one at his feet (setting a trap keeps Veil up)
function useSnare(p, world, plant) {
  if (p.snares <= 0 || !canFire(p)) return;
  if (plant) world.plantSnare(p);
  else { world.throwSnare(p); breakVeil(p, world, 'attack'); }
  p.snares--;
  if (p.snareRecharge <= 0) p.snareRecharge = HUNTER.snareRecharge;
}
// Echo's sniper focus (0-1) after holding fire for t ticks
export function rifleFocus(t) { const R = HUNTER.rifle; return Math.max(0, Math.min(1, (t - R.raise) / R.focus)); }

// ---- Lock-on ---------------------------------------------------------------------------

// A lock the player chose (any lock in manual mode; with automatic lock-on, one picked with the button)
export const lockChosen = p => SETTINGS.lockMode === 'manual' || !!p.lockPicked;

// A press locks onto the best target at once. While locked, a tap cycles to the next target and holding
// for LOCK.hold ticks lets go. The world keeps the lock valid (target death, range, line of sight).
// Automatic lock-on (the default) also locks the nearest enemy in sight whenever there is no target;
// letting go by holding the button pauses that until the next press.
function updateLock(p, cmd, world) {
  if (!SETTINGS.lockOn) { if (p.lockT) world.setLock(p, null, 'off'); p.lockSuspend = false; return; }
  const auto = SETTINGS.lockMode !== 'manual';
  const held = !!(cmd.held && cmd.held.lock), pressed = !!(cmd.pressed && cmd.pressed.lock);
  if (pressed) {
    p.lockHeld = 1; p.lockHoldDone = false;
    if (!p.lockT || p.lockSuspend) { p.lockSuspend = false; world.setLock(p, world.bestLockTarget(p), 'on'); p.lockHoldDone = true; }
  } else if (held && p.lockHeld > 0) {
    p.lockHeld++;
    if (p.lockHeld >= LOCK.hold && !p.lockHoldDone) { p.lockHoldDone = true; world.setLock(p, null, 'release'); if (auto) p.lockSuspend = true; }
  }
  if (!held && p.lockHeld > 0) {
    if (!p.lockHoldDone && p.lockT) world.setLock(p, world.nextLockTarget(p), 'cycle');
    p.lockHeld = 0; p.lockHoldDone = false;
  }
  world.validateLock(p);
  if (auto && !p.lockT && !p.lockSuspend) { const t = world.autoLockTarget(p); if (t) world.setLock(p, t, 'auto'); }
  if (!auto) p.lockSuspend = false;
}

// ---- Firing ----------------------------------------------------------------------------

function canFire(p) { return ['normal', 'dash', 'slide', 'lash', 'dodge'].includes(p.state) || (p.state === 'attack' && p.hitConfirm); }

function handleFire(p, cmd, world) {
  if (marksman(p)) { fireMarksman(p, cmd, world); return; }
  if (p.char === 'ram') { fireRam(p, cmd, world); return; }
  if (p.char === 'fix') { fireFix(p, cmd, world); return; }
  if (p.char === 'nova') {
    if (cmd.pressed.fire && p.fireCd === 0 && canFire(p)) {
      world.fireShot(p, 0); p.fireCd = NOVA.shotCd;
    }
    if (cmd.held.fire && canFire(p)) {
      const t0 = p.chargeT; p.chargeT += boostRate(p);
      crossed(t0, p.chargeT, [NOVA.charge1, NOVA.charge2], level => world.emit('chargeLevel', { p, level }));
    }
    if (cmd.released.fire || (!cmd.held.fire && p.chargeT > 0)) {
      if (p.chargeT >= NOVA.charge2) world.fireShot(p, 2);
      else if (p.chargeT >= NOVA.charge1) world.fireShot(p, 1);
      p.chargeT = 0;
    }
    return;
  }
  // Echo, Hunter kit: tap to throw a snare (crouch + tap plants one at your feet); hold to raise the
  // staff-rifle and let go to fire a long shot, or hold longer for a marking shot (HUNTER.rifle).
  // Settings: Echo's utility belt on LB moves the snares to LB (crouch + LB plants), and a tap of fire is
  // then a quick unscoped rifle shot.
  if (SETTINGS.echoKit === 'hunter') {
    const R = HUNTER.rifle, beltLB = SETTINGS.echoBelt === 'lb';
    if (beltLB && cmd.pressed.sub) useSnare(p, world, p.onGround && cmd.my < -0.55);
    if (cmd.pressed.fire) p.plantPress = p.onGround && cmd.my < -0.55;   // crouched when pressed: plant on release
    if (cmd.held.fire && canFire(p)) {
      // Raising the rifle takes real time; the focus builds faster under Fix's boosts
      const t0 = p.rifleT; p.rifleT += p.rifleT < R.raise ? 1 : boostRate(p);
      if (t0 < R.raise && p.rifleT >= R.raise) world.emit('rifleRaise', { p });
      else if (t0 < R.raise + R.focus && p.rifleT >= R.raise + R.focus) world.emit('rifleFocus', { p });   // full focus
    }
    if (!cmd.held.fire && p.rifleT > 0) {
      const t = p.rifleT; p.rifleT = 0;
      if (t < R.raise && !beltLB) useSnare(p, world, p.onGround && (p.plantPress || cmd.my < -0.55));
      else if (t < R.raise) {
        if (p.rifleCd === 0 && canFire(p)) { world.fireSniper(p, 0); p.rifleCd = p.rifleCdMax = R.cd; breakVeil(p, world, 'attack'); }
      } else if (p.rifleCd === 0 && canFire(p)) {
        world.fireSniper(p, rifleFocus(t)); p.rifleCd = p.rifleCdMax = R.cd; breakVeil(p, world, 'attack');
      } else world.emit('rifleLower', { p });
    }
    p.chargeT = 0;
    return;
  }
  // Echo, Pursuit kit: ranged options under test (Q-A)
  const mode = SETTINGS.echoRanged;
  if (mode === 'C') { p.chargeT = 0; return; }
  if (mode === 'A') {
    if (cmd.pressed.fire && p.tracerCd === 0 && canFire(p)) { world.fireTracer(p); p.tracerCd = ECHO.tracerCd; breakVeil(p, world, 'attack'); }
    return;
  }
  if (cmd.pressed.fire && p.fireCd === 0 && p.cells > 0 && canFire(p)) {
    world.fireBolt(p); p.cells--; p.fireCd = ECHO.boltCd; breakVeil(p, world, 'attack');
  }
  if (cmd.held.fire) p.chargeT++;
  if (!cmd.held.fire && p.chargeT > 0) {
    if (p.chargeT >= ECHO.tracerHold && p.tracerCd === 0 && canFire(p)) { world.fireTracer(p); p.tracerCd = ECHO.tracerCd; breakVeil(p, world, 'attack'); }
    p.chargeT = 0;
  }
}

// Marksman kit: tap for basic rounds, hold to charge the loaded attachment through three levels.
// Letting go within MARKSMAN.perfectWindow ticks of level 3 is a Perfect Release. The secondary
// blaster works the same way on the melee button: the press fires a quick burst (tryMelee) and
// holding charges a bigger one.
const levelOf = (t, C) => (t >= C[2] ? 3 : t >= C[1] ? 2 : t >= C[0] ? 1 : 0);
// Charge levels reached going from t0 to t1 (a charge can grow by more than one a tick with Overcharge)
function crossed(t0, t1, marks, fn) { marks.forEach((m, i) => { if (t0 < m && t1 >= m) fn(i + 1); }); }

function fireMarksman(p, cmd, world) {
  const M = MARKSMAN, C = M.charge, B = M.burst, L4 = M.beam.at;
  const rate = (p.overcharge > 0 ? AEGIS.over.charge : 1) * boostRate(p);
  if (cmd.pressed.fire && p.fireCd === 0 && canFire(p)) { world.fireShot(p, 0); p.fireCd = NOVA.shotCd; }
  if (cmd.held.fire && canFire(p)) {
    const t0 = p.chargeT; p.chargeT += rate;
    crossed(t0, p.chargeT, [...C, L4], level => world.emit('chargeLevel', { p, level }));
  }
  if (cmd.released.fire || (!cmd.held.fire && p.chargeT > 0)) {
    const t = p.chargeT, level = levelOf(t, C); p.chargeT = 0;
    if (t >= L4) { if (canFire(p)) startBeam(p, world); }
    else if (level) world.fireAttachment(p, p.attachment, level, level === 3 && t < C[2] + M.perfectWindow, t);
  }
  // Secondary weapon: holding charges it (not while a disc or well of his is still out); letting go fires
  // the charged level, or a tap (the Scatter fired its tap on the press, in tryMelee)
  const ready = subReady(p, world);
  if (cmd.held.melee && canFire(p) && ready) {
    const t0 = p.burstT; p.burstT += rate;
    crossed(t0, p.burstT, B.charge, level => world.emit('burstLevel', { p, level, sub: p.sub }));
  }
  if (!cmd.held.melee && (p.burstT > 0 || p.subArmed)) {
    const t = p.burstT, level = levelOf(t, B.charge), armed = p.subArmed; p.burstT = 0; p.subArmed = false;
    if (level && canFire(p) && ready) world.fireSub(p, level, level === 3 && t < B.charge[2] + B.perfectWindow);
    else if (!level && armed && canFire(p) && ready && p.sub !== 'scatter') world.fireSub(p, 0, false);
  }
}

// The secondary button pressed with no enemy close enough for the combo. The Scatter fires at once; the
// others fire when it is let go (fireMarksman). A disc or well already out is called back or collapsed.
function pressSub(p, world) {
  if (!subReady(p, world)) { p.buf.melee = 99; world.recallSub(p, p.sub); return false; }
  if (p.burstCd > 0) return false;
  p.buf.melee = 99;
  if (p.state !== 'normal') { if (p.state === 'dodge') p.dodge = null; setState(p, 'normal'); }
  if (p.sub === 'scatter') world.fireSub(p, 0, false); else p.subArmed = true;
  return true;
}
// A disc or a well: one of each at a time
const subReady = (p, world) => !((p.sub === 'disc' || p.sub === 'well') && world.subOut(p, p.sub));

function cycleSub(p, world) {
  p.sub = SUBS[(SUBS.indexOf(p.sub) + 1) % SUBS.length]; p.subSwCd = SUB.switchCd; p.burstT = 0; p.subArmed = false;
  world.emit('subSwitch', { p, sub: p.sub });
}

// ---- Nova: the dodge (Marksman kit, on the parry button) --------------------------------------
function tryDodge(p, world) {
  if (p.dodgeCd > 0 || (!p.onGround && !p.airDodge && !p.wallSliding)) return false;
  const D = DODGE, m = moveDir({ mx: p.stick[0], mz: p.stick[2] });
  // The way the stick points; with it centred, a backstep. Off a wall it always goes out from the wall.
  const [dx, dz] = p.wallSliding ? [-p.wallX, -p.wallZ] : m || [-p.facing, -fz(p)];
  p.buf.parry = 99; p.dodgeCd = D.cd;
  if (!p.onGround) p.airDodge = false;
  p.dodge = { dx, dz, t: 0, air: !p.onGround, speed: p.onGround ? D.speed : D.airSpeed, perfect: false };
  p.crouch = false; p.fastFall = false; p.dashCarry = false; p.wallSliding = false;
  setState(p, 'dodge'); world.emit('dodge', { p, dx, dz, air: p.dodge.air });
  return true;
}
function stateDodge(p, cmd, world) {
  const D = DODGE, d = p.dodge;
  if (!d) { setState(p, 'normal'); return; }
  d.t++;
  if (d.t <= D.ticks - 4) { const v = d.speed * Math.pow(D.keep, Math.max(0, d.t - 3)); p.vx = d.dx * v; p.vz = (d.dz || 0) * v; }
  else { const r = CHARS[p.char].run * 0.6; approach2(p, (cmd.mx || 0) * r, (cmd.mz || 0) * r, 60 * DT); }
  if (d.air && d.t <= 8) p.vy = 0; else applyGravity(p, cmd);
  // It can be cut short: a jump from the fourth tick, anything else once he is hittable again
  if (d.t >= 4 && tryJump(p, cmd, world)) { p.dodge = null; return; }
  if (d.t > D.iframes && cancelInto(p, cmd, world, { parry: false, jump: false })) { if (p.state !== 'dodge') p.dodge = null; return; }
  if (d.t >= D.ticks) { p.dodge = null; setState(p, 'normal'); }
}

// ---- Ultimates -----------------------------------------------------------------------------
// Both triggers pulled together: each press counted from when it happened, both within ULT.chord ticks of
// each other and both still held (so pulling the second trigger long after the first never counts)
export function trackChord(p, cmd) {
  p.chordP = cmd.pressed.parry ? 0 : Math.min(99, p.chordP + 1);
  p.chordF = cmd.pressed.fire ? 0 : Math.min(99, p.chordF + 1);
}
export const chordReady = (p, cmd) => !!cmd.pressed.ult ||
  (!!cmd.held.parry && !!cmd.held.fire && p.chordP <= ULT.chord && p.chordF <= ULT.chord);
export function gainUlt(p, amount, world) {
  if (!p || p.kind !== 'player' || !(amount > 0) || p.state === 'ult') return;
  const was = p.ult; p.ult = Math.min(ULT.max, p.ult + amount * boostRate(p));
  if (was < ULT.max && p.ult >= ULT.max) world.emit('ultReady', { p });
}

// Level 4: the sustained beam (Nova's, and RAM's Breach Beam: beamSpec). He braces (slow on the ground,
// hovering in the air), the beam follows the aim at a limited turn rate, and the world deals its damage
// (world.beamTick). Dash or parry cut it short; a hit ends it (combat.hitPlayer).
export const beamSpec = p => (p.char === 'ram' ? RAM.beam : MARKSMAN.beam);
function startBeam(p, world) {
  const B = beamSpec(p), ram = p.char === 'ram';
  p.beam = { t: B.ticks, dx: p.aimX, dy: p.aimY, dz: p.aimZ || 0, mult: ram ? 1 : focusMult(p) * spendOvercharge(p), attach: ram ? 'breach' : p.attachment, pulse: 0,
    armor: new Map(), family: { focused: false, rocketed: true, perfect: false }, segs: [] };
  p.chargeT = 0;
  setState(p, 'beam'); world.emit('beamStart', { p, attach: p.beam.attach, over: !ram && p.beam.mult > focusMult(p) });
}
function stateBeam(p, cmd, world) {
  const B = beamSpec(p), b = p.beam;
  if (!b) { setState(p, 'normal'); return; }
  // The beam follows the aim at a limited turn rate
  [b.dx, b.dy, b.dz] = turnToward(b.dx, b.dy, b.dz || 0, p.aimX, p.aimY, p.aimZ || 0, B.turn);
  if (Math.hypot(b.dx, b.dz) > 0.2) setFacing(p, b.dx, b.dz);
  // No push-back: on the ground he can creep along; in the air he hangs, sinking slowly, while it fires
  if (p.onGround) { const r = CHARS[p.char].run * B.slow; approach2(p, (cmd.mx || 0) * r, (cmd.mz || 0) * r, 40 * DT); p.vy = -0.5; }
  else { approach2(p, 0, 0, 20 * DT); p.vy = approach(p.vy, -B.hover, 40 * DT); p.fastFall = false; }
  if (tryParry(p, world) || tryDash(p, cmd, world)) { world.endBeam(p, 'cancel'); return; }
  world.beamTick(p);
  if (--b.t <= 0) { world.endBeam(p, 'done'); setState(p, 'normal'); }
}

// Overcharge (from the Aegis) makes a charged release hit harder, at a cost; returns the damage multiplier
export function spendOvercharge(p) {
  if (!(p.overcharge > 0)) return 1;
  p.overcharge = Math.max(0, p.overcharge - AEGIS.over.cost);
  return AEGIS.over.dmg;
}

// Charge stage from a charge counter: '', 'charging', 'L1', 'L2', 'perfect' (the release window), 'L3',
// or 'L4' (Marksman primary only: the beam)
function stageOf(t, C, win, l4 = Infinity) {
  if (t <= 0) return '';
  if (t < C[0]) return 'charging';
  if (t < C[1]) return 'L1';
  if (t < C[2]) return 'L2';
  if (t >= l4) return 'L4';
  return t < C[2] + win ? 'perfect' : 'L3';
}
export function chargeStage(p) {
  if (marksman(p)) return stageOf(p.chargeT, MARKSMAN.charge, MARKSMAN.perfectWindow, MARKSMAN.beam.at);
  if (p.char === 'ram') return stageOf(p.chargeT, RAM.cannon.charge, 0, RAM.beam.at);
  if (p.char === 'fix') return stageOf(p.chargeT, FIX.rivet.charge, 0);
  // Sentinel kit: two levels (lance, rail) and no Perfect Release
  return p.chargeT <= 0 ? '' : p.chargeT < NOVA.charge1 ? 'charging' : p.chargeT < NOVA.charge2 ? 'L1' : 'L2';
}
export const burstStage = p => stageOf(p.burstT, MARKSMAN.burst.charge, MARKSMAN.burst.perfectWindow);

// Rocket jump height (m, for a burst at his feet) earned by a shot charged for t ticks: it climbs
// steadily from charge level 1 to level 3; a Perfect Release reaches the highest. Attachments scale it.
export function rocketHeight(t, attach, perfect) {
  const R = MARKSMAN.rocket, C = MARKSMAN.charge;
  const f = Math.max(0, Math.min(1, (t - C[0]) / (C[2] - C[0])));
  return (perfect ? R.perfect : R.h[0] + (R.h[1] - R.h[0]) * f) * (R.attach[attach] ?? 1);
}

// ---- Nova: bracer attachments and Focus ---------------------------------------------------

function cycleAttachment(p, world) {
  const A = MARKSMAN.attachments;
  p.attachment = A[(A.indexOf(p.attachment) + 1) % A.length]; p.modeCd = MARKSMAN.switchCd;
  world.emit('attach', { p, attach: p.attachment });
}

export const focusMult = p => (marksman(p) ? 1 + MARKSMAN.focus.dmgPer * Math.floor(p.focus) : 1);

export function gainFocus(p, amount, world) {
  if (!marksman(p)) return;
  const F = MARKSMAN.focus, before = Math.floor(p.focus);
  p.focus = Math.min(F.max, p.focus + amount); p.focusT = F.decay;
  if (Math.floor(p.focus) > before) world.emit('focusUp', { p, level: Math.floor(p.focus) });
}

export function loseFocus(p, world) {
  if (p.focus >= 1) world.emit('focusLost', { p });
  p.focus = 0; p.focusT = 0;
}

// Focus drains one level after a quiet stretch, then another every decayStep ticks
function tickFocus(p, world) {
  if (p.focus <= 0 || --p.focusT > 0) return;
  p.focus = Math.max(0, p.focus - 1); p.focusT = MARKSMAN.focus.decayStep;
}

// ---- Echo: Resolve and Rally ------------------------------------------------------------

function tickEcho(p, world, cmd) {
  const rate = boostRate(p);
  if (p.snares < HUNTER.snareCharges) {
    p.snareRecharge -= rate;
    if (p.snareRecharge <= 0) { p.snares++; p.snareRecharge = p.snares < HUNTER.snareCharges ? HUNTER.snareRecharge : 0; }
  }
  if (p.leash) {
    const L = p.leash; L.t++;
    const e = L.e;
    if (!cmd.held.sig || L.t > HUNTER.leashTicks || e.dead || e.state !== 'caught' || ['hitstun', 'downed', 'dead'].includes(p.state)) world.releaseLeash(p);
  }
  if (p.lashCharges < ECHO.lashCharges) {
    p.lashRecharge -= rate;
    if (p.lashRecharge <= 0) { p.lashCharges++; p.lashRecharge = p.lashCharges < ECHO.lashCharges ? ECHO.lashRecharge : 0; }
  }
  tickScarf(p, world);
  const near = world.nearestEnemyDist(p.x, p.y + 1) < 6 || (p.scarfMode === 'flare' && p.targetedBy > 0);
  p.calmT = near ? 0 : p.calmT + 1;
  if (p.calmT > 120) p.resolve = Math.max(0, p.resolve - 5 / 60);
  if (p.strainT > 0) { p.strainT--; if (p.strainT === 0) p.strain = 0; }
}

export function addResolve(p, amount) {
  if (p.char !== 'echo') return;
  amount *= boostRate(p);
  if (p.scarfMode === 'flare') amount *= SCARF.flareResolve;
  p.resolve = Math.min(100, p.resolve + amount);
}

// Called when this player deals damage (Rally recovery + ranged refills).
export function onDealtDamage(p, dmg, isMelee) {
  if (p.char === 'fix') { p.scrap = Math.min(FIX.scrap.max, p.scrap + dmg * FIX.scrap.perDmg); return; }
  if (p.char !== 'echo') return;
  if (p.strain > 0) {
    const heal = Math.min(p.strain, dmg * 3);
    p.strain -= heal; p.hp = Math.min(p.maxHp, p.hp + heal);
  }
  if (isMelee) { addResolve(p, 6); if (p.cells < ECHO.cellsMax) p.cells++; }
}

// ---- Echo: scarf modes -----------------------------------------------------------------

export function setScarfMode(p, mode, world) {
  if (p.leash) world.releaseLeash(p);
  p.scarfMode = mode; p.modeCd = SCARF.switchCd;
  p.veiled = false; p.veilCharge = 0; p.veilBreakT = 0;
  world.emit('scarfMode', { p, mode });
}

function cycleScarf(p, world) {
  const M = SCARF.modes;
  setScarfMode(p, M[(M.indexOf(p.scarfMode) + 1) % M.length], world);
}

// Attacking or taking a hit drops Veil; it re-arms after SCARF.veilRearm quiet ticks.
// Breaking it with an attack while fully hidden makes that attack's first hit an ambush.
export function breakVeil(p, world, reason) {
  if (p.char !== 'echo' || p.scarfMode !== 'veil') return;
  const wasHidden = p.veiled, fading = p.veilCharge > 0;
  p.veilBreakT = SCARF.veilRearm;
  if (!wasHidden && !fading) return;
  p.veiled = false; p.veilCharge = 0;
  if (wasHidden && reason === 'attack') p.ambushT = SCARF.ambushWindow;
  world.emit('veilBreak', { p, reason, wasHidden });
}

// Flare widens the parry windows while at least one enemy is targeting Echo.
export function parryWindows(p) {
  const on = p.char === 'echo' && p.scarfMode === 'flare' && p.targetedBy > 0;
  return { window: PARRY.window + (on ? SCARF.flareParry : 0), perfect: PARRY.perfect + (on ? SCARF.flarePerfect : 0) };
}

function tickScarf(p, world) {
  p.targetedBy = 0;
  for (const e of world.enemies) if (!e.dead && e.target === p) p.targetedBy++;
  if (p.state === 'downed') { p.veiled = false; p.veilCharge = 0; return; }
  if (p.scarfMode === 'veil') {
    if (p.veilBreakT > 0) p.veilBreakT--;
    else if (!p.veiled && ++p.veilCharge >= SCARF.veilFade) { p.veiled = true; world.emit('veilOn', { p }); }
  } else if (p.scarfMode === 'flare' && p.targetedBy > 0) {
    p.resolve = Math.min(100, p.resolve + SCARF.flareTrickle * Math.min(3, p.targetedBy) / 60);
  }
}

// ---- Fix's boosts on anyone -----------------------------------------------------------------
// How much faster this player charges, recharges and fills their bars right now: Overclock (a power-up),
// Tune-Up (while Fix's Patch Beam holds them) and an Amp Coil's field multiply, up to FIX.maxRate
export function boostRate(p) {
  let k = 1;
  if (p.overclockT > 0) k *= FIX.power.overclock.rate;
  if (p.tuneT > 0) k *= FIX.beam.tune;
  if (p.ampK > 1) k *= p.ampK;
  return Math.min(FIX.maxRate, k);
}
// Plating: an overshield that takes damage before health
// (never past `cap`, and never taking away Plating someone already has above it)
export function addPlate(p, amount, cap = PLATE_MAX) { if (p.plate < cap) p.plate = Math.min(cap, p.plate + amount); }

// ---- RAM: the Rampart ----------------------------------------------------------------------

function tickRam(p, world) {
  const G = RAM.guard;
  p.guardOffT = Math.min(99, p.guardOffT + 1); p.blockT = Math.min(999, p.blockT + 1);
  // Integrity grows back once the shield has gone a while without blocking, and never while it is up
  if (p.state !== 'guard' && p.blockT > G.delay && p.integrity < G.integrity) {
    p.integrity = Math.min(G.integrity, p.integrity + G.regen / 60 * boostRate(p));
    if (p.guardBroken && p.integrity >= G.recover) { p.guardBroken = false; world.emit('rampartReady', { p }); }
  }
  if (p.link) world.tickLink(p);
}

function startGuard(p, world) {
  if (p.guardBroken) return false;
  p.buf.parry = 99; p.crouch = false;
  // Raised again right after it came down (a jump, a shove): no fresh Perfect Guard window, and no new sound
  const again = p.guardOffT < 12;
  p.guardT = again ? RAM.guard.perfect + 1 : 0;
  guardAim(p);
  setState(p, 'guard');
  if (!again) world.emit('guardOn', { p });
  return true;
}
// The Rampart faces where he aims: from straight ahead up to overhead, never lower than RAM.guard.minNy
function guardAim(p) {
  const ay = Math.max(RAM.guard.minNy, p.aimY), h = aimH(p) || [p.facing, fz(p)];
  const hm = Math.max(0.15, Math.hypot(p.aimX, p.aimZ || 0));   // overhead still leans the way he faces
  const m = Math.hypot(hm, ay) || 1;
  p.guardDir = [h[0] * hm / m, ay / m, h[1] * hm / m]; setFacing(p, h[0], h[1]);
}
function stateGuard(p, cmd, world) {
  const G = RAM.guard, c = CHARS.ram;
  p.guardT++;
  if (!cmd.held.parry || p.guardBroken) { p.guardOffT = 0; setState(p, 'normal'); world.emit('guardOff', { p }); return; }
  guardAim(p);
  // Behind the shield he walks slowly either way, still facing out
  const k = c.run * (p.onGround ? G.walk : 0.6);
  approach2(p, (cmd.mx || 0) * k, (cmd.mz || 0) * k, (p.onGround ? c.accelG : c.accelA) * DT);
  applyGravity(p, cmd);
  // Out of the guard: a shield shove (melee) or the Ram Charge (dash). A jump keeps the shield up.
  if (p.buf.melee <= ACTION_BUFFER) { p.buf.melee = 99; p.guardOffT = 0; world.emit('guardOff', { p }); startMove(p, 'ram_bash', world); return; }
  if (tryDash(p, cmd, world)) { p.guardOffT = 0; world.emit('guardOff', { p }); return; }
  if (tryJump(p, cmd, world)) p.state = 'guard';
}

// RAM's abilities, usable from most states: Bulwark Wall (suit ability), Guardian Link (mode), Provoke (LB)
function ramAbilities(p, world) {
  if (['hitstun', 'ult', 'leap', 'dashCharge'].includes(p.state)) return;
  if (p.buf.sig <= ACTION_BUFFER) { p.buf.sig = 99; if (p.wallCd <= 0) world.raiseWall(p); else world.emit('notReady', { p, what: 'wall' }); }
  if (p.buf.mode <= ACTION_BUFFER) { p.buf.mode = 99; if (p.linkCd <= 0) world.startLink(p); else world.emit('notReady', { p, what: 'link' }); }
  if (p.buf.sub <= ACTION_BUFFER) { p.buf.sub = 99; if (p.provokeCd <= 0) world.provoke(p); else world.emit('notReady', { p, what: 'provoke' }); }
}

// The Ram Charge (an ordinary dash, level 0) and the Battering Ram (a charged dash, levels 1-3): straight
// along the floor (or level through the air), the way he points; the world scoops up what is in front
// (world.ramPlow) and ends it at a wall or on something it can't move
function startRush(p, world, level, d) {
  const R = RAM.rush;
  if (d && Math.hypot(d[0], d[2] || 0) > 0.2) setFacing(p, d[0], d[2] || 0);
  p.buf.dash = 99; p.dashCd = CHARS.ram.dash.cooldown;
  p.dashChargeT = 0; p.crouch = false; p.fastFall = false; p.dash = null;
  p.rush = { dx: p.facing, dz: fz(p), t: 0, level, air: !p.onGround, ticks: R.ticks[level], speed: R.speed[level], carried: [], hit: new Set() };
  setState(p, 'rush'); world.emit('rush', { p, level, dx: p.facing, dz: fz(p) });
}
function stateRush(p, cmd, world) {
  const R = RAM.rush, r = p.rush;
  if (!r) { setState(p, 'normal'); return; }
  r.t++;
  const v = r.speed * (r.t > r.ticks - 3 ? 0.8 : 1); p.vx = r.dx * v; p.vz = r.dz * v;
  if (r.air) p.vy = 0; else applyGravity(p, cmd);   // run off a ledge and he falls, still charging
  // A jump out of a grounded charge carries its speed
  if (!r.air && p.buf.jump <= JUMP_BUFFER && (p.onGround || p.coyote > 0)) {
    world.endRush(p, 'jump');
    const j = Math.min(r.speed, 16) * 0.85;
    p.vy = CHARS.ram.jumpV; p.vx = r.dx * j; p.vz = r.dz * j; p.dashCarry = true; p.onGround = false; p.coyote = 0; p.buf.jump = 99;
    setState(p, 'normal'); world.emit('jump', { p, dashJump: true });
    return;
  }
  if (r.t >= r.ticks) { world.endRush(p, 'done'); p.vx = r.dx * r.speed * R.keep; p.vz = r.dz * r.speed * R.keep; p.postDash = 0; setState(p, 'normal'); }
}

// Guardian Link's leap to a teammate's side: a single bound, steered all the way to where they are now
function stateLeap(p, cmd, world) {
  const L = p.leap;
  if (!L) { setState(p, 'normal'); return; }
  L.t++;
  const q = L.q, ok = q && world.players.includes(q) && q.state !== 'dead';
  if (ok) { const g = q.w / 2 + p.w / 2 + 0.25; L.tx = q.x - L.side.x * g; L.tz = q.z - L.side.z * g; L.ty = q.y; }
  const left = Math.max(1, RAM.link.leapTicks - L.t) * DT;
  let vx = (L.tx - p.x) / left, vz = (L.tz - p.z) / left; const vm = Math.hypot(vx, vz);
  if (vm > 26) { vx *= 26 / vm; vz *= 26 / vm; }
  p.vx = vx; p.vz = vz;
  p.vy -= GRAVITY * DT; if (p.vy < -MAX_FALL * 1.4) p.vy = -MAX_FALL * 1.4;
  setFacing(p, L.tx - p.x, L.tz - p.z); p.fastFall = false;
  if ((p.onGround && L.t > 4) || L.t > RAM.link.leapTicks + 30) world.landLeap(p);
}

// The cannon: a tap fires a slug at once; holding charges a Breach Shot, fired when let go. While guarding,
// fire is the Kinetic Release instead.
function fireRam(p, cmd, world) {
  const C = RAM.cannon;
  if (p.state === 'guard') {
    if (cmd.pressed.fire && p.kinetic >= RAM.release.min) world.kineticRelease(p);
    else if (cmd.pressed.fire) world.emit('notReady', { p, what: 'kinetic' });
    p.chargeT = 0; return;
  }
  if (cmd.pressed.fire && p.fireCd === 0 && canFire(p)) { world.fireSlug(p, 0); p.fireCd = C.cd; }
  if (cmd.held.fire && canFire(p)) {
    const t0 = p.chargeT; p.chargeT += boostRate(p);
    crossed(t0, p.chargeT, [...C.charge, RAM.beam.at], level => world.emit('chargeLevel', { p, level }));
  }
  if (!cmd.held.fire && p.chargeT > 0) {
    const t = p.chargeT, level = levelOf(t, C.charge); p.chargeT = 0;
    if (t >= RAM.beam.at) { if (canFire(p)) startBeam(p, world); }   // Level 4: the Breach Beam
    else if (level && canFire(p)) world.fireSlug(p, level);
  }
}

// ---- Fix: gadgets, power-ups, the Patch Beam and the Rivet Gun ---------------------------------

function tickFix(p, world) {
  const S = FIX.scrap;
  if (p.scrap < S.max) p.scrap = Math.min(S.max, p.scrap + S.regen / 60 * boostRate(p));
}
function fixAbilities(p, world) {
  if (['hitstun', 'ult', 'dashCharge'].includes(p.state)) return;
  if (p.buf.sig <= ACTION_BUFFER) { p.buf.sig = 99; world.deployGadget(p); }
}
function cycleGadget(p, world) {
  const G = FIX.gadgets; p.gadgetSel = G[(G.indexOf(p.gadgetSel) + 1) % G.length]; p.modeCd = SUB.switchCd;
  world.emit('gadgetSelect', { p, kind: p.gadgetSel });
}
function cyclePower(p, world) {
  const P = FIX.powers; p.powerSel = P[(P.indexOf(p.powerSel) + 1) % P.length]; p.subSwCd = SUB.switchCd;
  world.emit('powerSelect', { p, kind: p.powerSel });
}
// Melee: close to an enemy or one of her gadgets it is the wrench (a hit on a gadget upgrades it). Otherwise
// the press readies a power-up: it is tossed when the button comes up, unless it is held on into the Torque
// Slam. Without the Scrap for one it is the wrench anyway.
function fixMelee(p, world) {
  p.buf.melee = 99;
  if (meleeTarget(p, world) || world.gadgetNear(p) || p.scrap < FIX.power.cost) { startMove(p, p.onGround ? 'fix_w1' : 'fix_air', world); return true; }
  p.tossArmed = true; p.tossT = 0;
  return true;
}
function fixToss(p, cmd, world) {
  p.tossT++;
  if (p.state === 'hitstun' || p.state === 'downed' || p.state === 'ult' || p.meleeHeldT >= 30) { p.tossArmed = false; return; }
  if (!cmd.held.melee) { p.tossArmed = false; world.tossPower(p); }
}

function startPatch(p, world) {
  p.buf.parry = 99; p.crouch = false; p.chargeT = 0; p.rivetQ = 0; p.tossArmed = false;
  const again = p.patch && p.patchOffT !== undefined && world.tick - p.patchOffT < 12;
  p.patch = { target: world.patchTarget(p, again ? p.patch.target : null), t: 0, self: false };
  setState(p, 'patch');
  if (!again) world.emit('patchOn', { p, q: p.patch.target });
  return true;
}
function statePatch(p, cmd, world) {
  if (!cmd.held.parry || !p.patch) { p.patchOffT = world.tick; setState(p, 'normal'); world.emit('patchOff', { p }); return; }
  const q = p.patch.target;
  horizontalControl(p, cmd, world, FIX.beam.slow);
  if (q && stickMag(cmd) < 0.1) { const u = away(p, q, p); setFacing(p, u.x, u.z); }   // standing still she turns to whoever she patches
  applyGravity(p, cmd);
  if (tryDash(p, cmd, world)) { p.patchOffT = world.tick; world.emit('patchOff', { p }); return; }
  if (tryJump(p, cmd, world)) p.state = 'patch';   // she keeps the beam on through a jump
  world.patchTick(p);
}

// The Rivet Gun: a tap fires a burst of rivets; holding charges a Hot Rivet, fired when let go
function fireFix(p, cmd, world) {
  const R = FIX.rivet;
  if (p.state === 'patch') { p.chargeT = 0; p.rivetQ = 0; return; }   // the beam takes both hands
  if (cmd.pressed.fire && p.fireCd === 0 && canFire(p)) { p.rivetQ = R.n; p.rivetT = 0; p.fireCd = R.cd; }
  if (p.rivetQ > 0 && --p.rivetT <= 0) { if (canFire(p)) world.fireRivet(p, R.n - p.rivetQ); p.rivetQ--; p.rivetT = R.every; }
  if (cmd.held.fire && canFire(p)) {
    const t0 = p.chargeT; p.chargeT += boostRate(p);
    crossed(t0, p.chargeT, R.charge, level => world.emit('chargeLevel', { p, level }));
  }
  if (!cmd.held.fire && p.chargeT > 0) {
    const level = levelOf(p.chargeT, R.charge); p.chargeT = 0;
    if (level && canFire(p)) world.fireHotRivet(p, level);
  }
}
