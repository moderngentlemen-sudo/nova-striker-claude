// World: owns every entity, runs the fixed-tick simulation, and emits events for
// rendering, audio and UI. Nothing in here touches the DOM or Three.js.
import { SETTINGS, DIFFICULTY, NOVA, MARKSMAN, ECHO, HUNTER, SCARF, CHARS, GRAVITY, DT, LOCK, AEGIS, DEFLECT, SUB, DODGE, ULT, RAM, FIX, MAX_FALL, POWERUPS } from './config.js';
import { BOXES, GATES, CHECKPOINTS, ZONES, KILL_Y, ARENA_TRIGGER_X, TOWER_TRIGGER_X, ENCOUNTERS, ROUTE_END_X, hasHeadroom, groundBelow, segmentBlocked, rayCast, rayBoxT, pointInSolid,
  killYAt, routeAt, ROUTES, restoreBoxes, LEVEL_PICKUPS, DESTRUCT, breakableAt, LIFTS } from './level.js';
import { createPlayer, updatePlayer, setCharacter, chest, addResolve, focusMult, marksman, rocketHeight, chargeStage, spendOvercharge, parryWindows, gainUlt, trackChord, chordReady, lockChosen, boostRate, addPlate, beamSpec } from './player.js';
import { createEnemy, updateEnemy, ENEMY_TYPES } from './enemies.js';
import { spawnBoss, BOSS } from './bosses.js';
import { resolveHitboxes, updateProjectiles, updateShockwaves, crossesBarrier, hitEnemy, hitPlayer, awardFocus, hurtbox } from './combat.js';
import { EMPTY_CMD } from './input.js';

const sign = v => (v > 0 ? 1 : v < 0 ? -1 : 0);
// How tall each of Fix's gadgets stands (what enemy shots and strikes can hit)
const GADGET_H = { pylon: 1.3, sentry: 0.95, coil: 1.6, pad: 0.3 };
// Height gained by a body launched upward at v with no rise cut, as the fixed-tick integration plays it out
const apexGain = v => Math.max(0, v * v / (2 * GRAVITY) - v * DT / 2);

// Placeholder lines for the CP-09 test. Not canon; written only to test the feel.
const BARKS = {
  nova: {
    intercept_save: ['Got that one.', 'Covered.'], saved_reply: ['Thanks. Eyes up.'],
    perfect: ['Denied.', 'Not today.'], revive: ['Up. I have you.', 'On your feet.'],
    revived: ['Thanks.', 'Noted.'], lash_reply: ['Show-off.', 'I had him.'],
    lash_save: ['Over here.'], lock_broken: ["Lock's open. Move."],
    challenge_reply: ["Don't make a habit of that.", 'I see them. Covering you.'],
    perfect_shot: ['Textbook.', 'Right on the mark.', 'Clean.'],
  },
  echo: {
    intercept_save: ['Got it!'], saved_reply: ['I had it.', "Didn't need that."],
    perfect: ['Too slow.', 'Again!'], revive: ['Come on, get up.', 'Not like this. Up.'],
    revived: ['I was fine.', 'Thanks.'], lash_reply: ['Nice pull.'],
    lash_save: ['Mine now!', 'Over here!'], lock_broken: ["That's how it's done."],
    challenge: ['Eyes on me!', 'Come on, all of you!'], ambush: ['Missed me?', 'Right behind you.'],
  },
  ram: {
    intercept_save: ['Behind me.'], saved_reply: ['Appreciated.', 'Good eye.'],
    perfect: ['Not getting through.'], revive: ['Up. Stay behind me.', 'On your feet. I hold the line.'],
    revived: ['Still standing.', 'Back to the front.'], lash_reply: ['Good pull.'], lash_save: ['Hold on.'], lock_broken: ['Line holds.'],
    challenge_reply: ['Bring them to me.', 'I will take the rest.'], provoke: ['Over here!', 'Come and try me!'],
    guardian: ['I have your back.', 'Stay close to me.'], perfect_guard: ['Denied.', 'Back at you.'],
  },
  fix: {
    intercept_save: ['Got it!'], saved_reply: ['Thanks! Owe you one.'], perfect: ['Ha! Clean.'],
    revive: ['Jump-start! Up you get.', 'Patched. Back in it.'], revived: ['Back in business.', 'Who fixed me? Thanks.'],
    lash_reply: ['Neat trick.'], lash_save: ['Gotcha!'], lock_broken: ['Tools down. Nice work.'],
    challenge_reply: ['Keep them busy, I will patch you after.'], gadget: ['Deploying!', 'Built it.'], upgrade: ['Tuned up.', 'Better than new.'],
  },
};

export class World {
  constructor() {
    this.players = []; this.enemies = []; this.projectiles = []; this.hitboxes = [];
    this.barriers = []; this.shockwaves = []; this.events = []; this.scheduled = []; this.snares = []; this.wells = []; this.ultCast = null;
    this.gadgets = []; this.pickups = [];
    this.hitSets = new Map(); this.tick = 0; this.instanceSeq = 1;
    this.checkpoint = 0; this.wipeT = 0; this.globalBarkCd = 0;
    this.arena = { state: 'idle' }; this.towerSpawned = false;
    this.encounters = ENCOUNTERS.map(def => ({ def, state: 'idle', wave: 0 })); this.routeDone = false; this.routesDone = {};
    restoreBoxes(); this.spawnLevelPickups();
    this.aspect = 16 / 9;
    this.cam = { x: 0, y: 3, dist: 16, halfW: 10, halfH: 5 };
    this.director = makeDirector(this);
    this.spawnGym();
  }

  emit(type, data = {}) { this.events.push({ type, ...data }); }
  newInstance() { return this.instanceSeq++; }
  schedule(ticks, fn) { this.scheduled.push({ t: this.tick + ticks, fn }); }
  activePlayers() { return this.players.filter(p => p.state !== 'dead' && p.state !== 'downed'); }

  // ---- Spawning helpers used by players, enemies and combat ----
  spawnHitbox(hb) { this.hitboxes.push(hb); }
  spawnProjectile(pr) {
    this.projectiles.push({ ttl: 60, r: 0.15, dmg: 1, poise: 6, hitSet: new Set(), dead: false, ...pr, px: pr.x, py: pr.y });
  }
  spawnShockwave(e, dir, dmg, scale = 1) {
    this.shockwaves.push({ owner: e, x: e.x + dir * (e.w / 2), y: e.y, dir, speed: 11, ttl: Math.round(60 * scale), dmg, h: 0.9, instance: this.newInstance() });
  }
  telegraph(e, cat, ticks) { this.emit('telegraph', { e, cat, ticks }); }

  fireShot(p, level) {
    const c = chest(p), ax = p.aimX, ay = p.aimY, far = marksman(p);
    const spec = level === 0 ? { speed: NOVA.shotSpeed, dmg: NOVA.shotDmg, poise: 6, r: 0.16, kb: 1.5, kind: 'shot' }
      : level === 1 ? { speed: NOVA.lance.speed, dmg: NOVA.lance.dmg, poise: NOVA.lance.poise, r: 0.24, pierce: true, kb: 5, kind: 'lance' }
        : { speed: NOVA.rail.speed, dmg: NOVA.rail.dmg, poise: NOVA.rail.poise, r: 0.3, pierce: true, rail: true, armorBreak: true, kb: 9, kind: 'rail' };
    spec.dmg *= focusMult(p);
    // Marksman kit: rounds fly the whole level and splash where they land
    if (far) { spec.splash = MARKSMAN.round.splash; spec.ttl = MARKSMAN.life; }
    this.spawnProjectile({ team: 'p', owner: p, x: c.x + ax * 0.7, y: c.y + ay * 0.7, vx: ax * spec.speed, vy: ay * spec.speed,
      ttl: Math.round(NOVA.shotRange / spec.speed * 60), intercept: true, homing: level > 0, level, ...spec });
    if (level === 2 && !p.onGround) p.vy = Math.max(p.vy, 1.5);   // a mid-air shot briefly holds him up (no push-back)
    else if (level === 1 && !p.onGround) p.vy = Math.max(p.vy, 0.5);
    this.emit('shot', { p, level, x: c.x + ax * 0.7, y: c.y + ay * 0.7 });
  }

  // Marksman kit: a charged release fires the loaded attachment. Every projectile from one release
  // shares a family, so the shot as a whole earns Focus once and rocket-jumps Nova at most once. The
  // family also carries how long the shot was charged (chargeT), which sets the rocket jump height.
  fireAttachment(p, kind, level, perfect, chargeT = MARKSMAN.charge[level - 1]) {
    const M = MARKSMAN, c = chest(p), ax = p.aimX, ay = p.aimY;
    const over = spendOvercharge(p);   // Aegis Overcharge: a stronger release
    const x = c.x + ax * 0.7, y = c.y + ay * 0.7, fm = focusMult(p) * over;
    const mult = fm * (perfect ? M.perfectMult : 1);
    const family = { focused: false, rocketed: false, perfect, chargeT, attach: kind, level };
    const base = { team: 'p', owner: p, x, y, level, perfect, family, intercept: true, ttl: M.life };
    const splash = S => ({ ...S, dmg: S.dmg * mult, poise: S.poise * mult, r: S.r * (perfect ? 1.2 : 1) });
    if (kind === 'lance') {
      const L = M.lance[level];
      this.spawnProjectile({ ...base, vx: ax * L.speed, vy: ay * L.speed,
        r: L.r * (perfect ? 1.2 : 1), dmg: L.dmg * mult, poise: L.poise * mult, kb: L.kb, pierce: true, homing: true,
        armorBreak: !!L.armorBreak || perfect, rail: !!L.rail || perfect, interceptHeavy: true, kind: level === 3 ? 'rail' : 'lance',
        splash: splash(L.splash) });
      if (!p.onGround) p.vy = Math.max(p.vy, level === 3 ? 1.5 : 0.5);
    } else if (kind === 'volley') {
      const V = M.volley, key = perfect ? 'perfect' : level, n = V.darts[key], fan = V.fan[key], a0 = Math.atan2(ay, ax);
      // Darts are spread over the enemies in front: the lower darts take the lower targets. Locked onto a
      // target he chose, every dart goes for it.
      const targets = p.lockT && !p.lockT.dead && lockChosen(p) ? [p.lockT] : this.enemiesInCone(c.x, c.y, ax, ay, V.seekRange, V.seekCone);
      const S = { ...V.splash, dmg: V.splash.dmg * fm, poise: V.splash.poise * fm, rocket: V.rocket };
      for (let i = 0; i < n; i++) {
        const a = a0 + fan * (i / (n - 1) - 0.5), sp = V.speed * (1 + (i % 2) * 0.08);
        const target = targets.length ? targets[Math.min(targets.length - 1, Math.floor(i * targets.length / n))] : null;
        this.spawnProjectile({ ...base, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, r: V.r,
          dmg: V.dmg * fm, poise: V.poise * fm, kb: 2, interceptHeavy: false, kind: 'dart', splash: S,
          seek: { target, delay: V.seekDelay, until: V.seekFor, turn: V.turn, age: 0 } });
      }
    } else if (kind === 'arc') {
      const A = M.arc, S = A[level], dx = ax, dy = ay + A.lift, m = Math.hypot(dx, dy) || 1;
      this.spawnProjectile({ ...base, intercept: false, vx: dx / m * A.speed, vy: dy / m * A.speed, gravity: A.gravity,
        r: 0.2, dmg: 0, poise: 0, kind: 'shell',
        blast: { r: S.r * (perfect ? A.perfectRadius : 1), dmg: S.dmg * mult, poise: S.poise * mult, armorBreak: !!S.armorBreak || perfect, rocket: S.rocket } });
    } else {
      const P = M.prism, S = P[level];
      this.spawnProjectile({ ...base, vx: ax * P.speed, vy: ay * P.speed, r: P.r * (perfect ? 1.2 : 1),
        dmg: S.dmg * mult, poise: S.poise * mult, kb: 3, homing: true, interceptHeavy: true, kind: 'prism', splash: splash(S.splash),
        prism: { shards: S.shards, bounces: S.bounces + (perfect ? P.perfectBounces : 0), mult } });
    }
    this.emit('shot', { p, level, attach: kind, perfect, x, y, ax, ay, over: over > 1 });
    if (perfect) { this.emit('perfectRelease', { p, x, y, attach: kind }); this.bark(p, 'perfect_shot', 0.25); }
  }

  // Explosions: splash from Nova's shots, Arc shells, the level 3 burst, and enemy mortar shells.
  // A player blast hits every enemy in the radius (over the top of shields) except `skip`, the one a
  // shot already hit directly; with `rocket` set it can also launch Nova. An enemy blast hits players
  // and cannot be parried.
  explode({ owner, team = 'p', x, y, spec, level = 0, perfect = false, family = null, skip = null, rocket = false, kind = 'splash' }) {
    this.blastBoxes(x, y, spec.r, (spec.dmg || 2) * 1.5, owner);
    const reach = (ent, r) => {
      const nx = Math.max(ent.x - ent.w / 2, Math.min(x, ent.x + ent.w / 2)), ny = Math.max(ent.y, Math.min(y, ent.y + ent.h));
      return Math.hypot(x - nx, y - ny) <= r;
    };
    if (team === 'e') {
      const id = this.newInstance();
      for (const q of this.players) {
        if (q.state === 'dead' || q.state === 'downed' || !reach(q, spec.r)) continue;
        hitPlayer(this, q, { owner, dmg: spec.dmg, unblockable: true, cat: 'unblockable', heavy: true, kb: [(sign(q.x - x) || 1) * 7, 6], instance: id, at: { x, y } });
      }
      this.emit('enemyBlast', { x, y, r: spec.r });
      return;
    }
    for (const e of this.enemies) {
      if (e.dead || e === skip || !reach(e, spec.r)) continue;
      const res = hitEnemy(this, e, { owner, dmg: spec.dmg, poise: spec.poise, armorBreak: !!spec.armorBreak, kb: [(sign(e.x - x) || 1) * 7, 5], blast: true }, 'blast');
      if (family && (res === 'hit' || res === 'kill')) awardFocus(this, { owner, family });
    }
    if (rocket && spec.rocket && owner && marksman(owner)) this.rocketPush(owner, x, y, spec, family, perfect);
    this.emit(kind, { p: owner, x, y, r: spec.r, level, perfect });
  }

  // Rocket jump: a charged shot bursting on terrain close to Nova launches him away from the burst.
  // The launch speed is the one that reaches the height the charge earned (rocketHeight), weaker for a
  // burst further away and for each extra rocket jump in the same airtime. It sets his climb speed rather
  // than adding to it, so the height the charge earned is a real ceiling. A short impact pause (freeze)
  // sells the blast before he leaves the ground.
  rocketPush(p, x, y, spec, family, perfect) {
    const R = MARKSMAN.rocket;
    if (family && family.rocketed) return;
    const reach = spec.r + R.reach;
    if (Math.hypot(p.x - x, p.y + p.h * 0.5 - y) > reach || p.state === 'downed' || p.state === 'dead') return;
    // A burst anywhere under his boots counts as right under him: the first `slack` m sideways are
    // ignored for both the direction and the strength
    const ox = p.x - x, sx = Math.sign(ox) * Math.max(0, Math.abs(ox) - R.slack);
    let dx = sx, dy = p.y + p.h * 0.5 - y;
    const dEff = Math.hypot(dx, dy);
    if (dEff < 0.05) { dx = 0; dy = 1; } else { dx /= dEff; dy /= dEff; }
    if (family) family.rocketed = true;
    const H = rocketHeight(family ? family.chargeT : MARKSMAN.charge[2], family ? family.attach : 'arc', perfect);
    const near = Math.max(0, dEff - R.close) / Math.max(0.01, reach - R.close);   // 0 for a burst at his feet
    const air = p.onGround ? 1 : R.air[Math.min(p.rockets, R.air.length - 1)];
    const k = Math.sqrt(2 * GRAVITY * H) * (1 - R.falloff * Math.min(1, near)) * air;
    if (!p.onGround) p.rockets++;
    let vx = p.vx + dx * k * R.side;
    if (Math.abs(vx) > R.sideMax && Math.abs(vx) > Math.abs(p.vx)) vx = sign(vx) * Math.max(R.sideMax, Math.abs(p.vx));
    p.vx = vx;
    if (dy > 0) p.vy = Math.max(p.vy, dy * k); else p.vy += dy * k * 0.5;
    if (dy > 0.2) { p.onGround = false; p.coyote = 0; }   // launched: no late ground jump to cut the climb
    p.dashCarry = true; p.fastFall = false;   // keeps the launch: no rise cut, momentum carries
    const power = Math.min(1, k / Math.sqrt(2 * GRAVITY * R.perfect));
    const h = dy > 0 ? apexGain(dy * k) : 0;
    p.rocketT = 50; p.rocketPow = power;
    p.hitstop = Math.max(p.hitstop, R.freeze[power < 0.45 ? 0 : power < 0.8 ? 1 : 2]);
    this.emit('rocketJump', { p, x, y, k, h, power, perfect, level: family ? family.level : 3, dx, dy });
  }

  // What a rocket jump would do right now: while Nova charges with his aim pointing down and a surface
  // close enough below, the height his feet would reach if he let go now. Presentation only (the apex
  // marker); the real launch is worked out when the shot bursts.
  rocketPreview(p) {
    const M = MARKSMAN, R = M.rocket;
    // (A Level 4 charge fires the beam instead, so it has no rocket jump to preview)
    if (!marksman(p) || p.chargeT < M.charge[0] || p.chargeT >= M.beam.at || p.aimY > -0.6 || !['normal', 'slide'].includes(p.state)) return null;
    const gy = groundBelow(p.x, p.y + 0.1);
    if (gy === -Infinity) return null;
    const stage = chargeStage(p), perfect = stage === 'perfect', level = p.chargeT >= M.charge[2] ? 3 : p.chargeT >= M.charge[1] ? 2 : 1;
    const A = p.attachment;
    let r = A === 'lance' ? M.lance[level].splash.r * (perfect ? 1.2 : 1) : A === 'volley' ? M.volley.splash.r
      : A === 'arc' ? M.arc[level].r * (perfect ? M.arc.perfectRadius : 1) : M.prism[level].splash.r * (perfect ? 1.2 : 1);
    const d = p.y + p.h * 0.5 - (gy + 0.15), reach = r + R.reach;
    if (d > reach) return null;
    const H = rocketHeight(p.chargeT, A, perfect), near = Math.max(0, d - R.close) / Math.max(0.01, reach - R.close);
    const air = p.onGround ? 1 : R.air[Math.min(p.rockets, R.air.length - 1)];
    const k = Math.sqrt(2 * GRAVITY * H) * (1 - R.falloff * Math.min(1, near)) * air;
    const up = Math.max(p.vy, k);
    return { x: p.x, y: p.y, apex: p.y + apexGain(up), level, perfect, h: H };
  }

  // ---- Nova: the Level 4 beam ----
  // Every tick: trace the beam to the first wall (a Prism beam bounces once), erase enemy shots it touches,
  // and every `pulse` ticks hit every enemy in it. Attachments add their flavour.
  beamTick(p) {
    const B = beamSpec(p), b = p.beam, c = chest(p);
    const segs = []; let sx = c.x + b.dx * 0.6, sy = c.y + b.dy * 0.6, dx = b.dx, dy = b.dy;
    const bounces = b.attach === 'prism' ? B.prism.bounces : 0;
    for (let i = 0; i <= bounces; i++) {
      const h = rayCast(sx, sy, dx, dy, B.range);
      segs.push({ x0: sx, y0: sy, x1: h.x, y1: h.y, wall: h.wall, nx: h.nx, ny: h.ny });
      if (!h.wall || i === bounces) break;
      const dot = dx * h.nx + dy * h.ny; dx -= 2 * dot * h.nx; dy -= 2 * dot * h.ny;
      sx = h.x + h.nx * 0.05; sy = h.y + h.ny * 0.05;
    }
    b.segs = segs; b.pulse++;
    const endBox = segs.length && rayCast(segs[segs.length - 1].x0, segs[segs.length - 1].y0, dx, dy, B.range).box;
    if (endBox && endBox.type === 'd' && b.pulse % B.pulse === 1) this.damageBox(endBox, B.dmg * b.mult * 2, segs[segs.length - 1].x1, segs[segs.length - 1].y1, p);
    const near = (x, y, r) => segs.some(g => distToSeg(x, y, g) < r);
    for (const pr of this.projectiles) if (pr.team === 'e' && !pr.dead && near(pr.x, pr.y, B.width + pr.r)) { pr.dead = true; this.emit('erase', { x: pr.x, y: pr.y }); }
    if (b.pulse % B.pulse === 1) {
      for (const e of this.enemies) {
        if (e.dead) continue;
        const hb = hurtbox(e);
        if (!segs.some(g => segHitsBox(g, hb, B.width))) continue;
        const last = b.armor.get(e.id), ab = last === undefined || b.pulse - last >= B.armorEvery;
        if (ab) b.armor.set(e.id, b.pulse);
        const res = hitEnemy(this, e, { owner: p, dmg: B.dmg * b.mult, poise: B.poise * b.mult, kb: [sign(b.dx) * (B.kb || 3), 1], vx: b.dx, armorBreak: ab, rail: true, beam: true }, 'proj');
        if (p.char === 'nova' && (res === 'hit' || res === 'kill')) awardFocus(this, { owner: p, family: b.family });
      }
    }
    const end = segs[segs.length - 1];
    if (b.attach === 'arc' && b.pulse % B.arc.every === 0) {
      this.explode({ owner: p, x: end.x1, y: end.y1, spec: { ...B.arc.blast, dmg: B.arc.blast.dmg * b.mult, poise: B.arc.blast.poise * b.mult, armorBreak: true }, level: 2, kind: 'blast' });
    }
    if (b.attach === 'volley' && b.pulse % B.volley.every === 0) {
      const V = MARKSMAN.volley, a = Math.atan2(b.dy, b.dx) + (Math.random() - 0.5) * 0.9, sx0 = c.x + b.dx * 0.7, sy0 = c.y + b.dy * 0.7;
      const target = this.nearestEnemyInCone(sx0, sy0, b.dx, b.dy, V.seekRange, V.seekCone);
      this.spawnProjectile({ team: 'p', owner: p, x: sx0, y: sy0, vx: Math.cos(a) * V.speed, vy: Math.sin(a) * V.speed, ttl: MARKSMAN.life, r: V.r,
        dmg: V.dmg * b.mult, poise: V.poise * b.mult, kb: 2, kind: 'dart', level: 4, family: b.family, intercept: true, interceptHeavy: false,
        splash: { ...V.splash }, seek: { target, delay: 4, until: V.seekFor, turn: V.turn, age: 0 } });
    }
  }
  endBeam(p, why) {
    if (!p.beam) return;
    this.emit('beamEnd', { p, why });
    p.beam = null;
  }

  // ---- Nova: the hard-light Aegis ----
  raiseAegis(p) {
    const A = AEGIS;
    p.aegis = { hp: A.hp, max: A.hp, t: A.ticks, seen: new Set() };
    this.emit('aegisOn', { p });
  }
  // Ends it: 'break' (damage) shatters it outward, 'detonate' (pressed again) blasts it outward on purpose
  endAegis(p, why) {
    const S = p.aegis; if (!S) return;
    const A = AEGIS, c = chest(p), frac = Math.max(0, S.hp / S.max);
    p.aegis = null; p.aegisCd = A.cd;
    if (why === 'break' || why === 'detonate') {
      const B = why === 'break' ? A.shatter : A.detonate, k = why === 'detonate' ? 0.5 + 0.5 * frac : 1;
      this.spawnHitbox({ owner: p, team: 'p', x0: c.x - B.r, x1: c.x + B.r, y0: c.y - B.r, y1: c.y + B.r, dmg: B.dmg * k, poise: B.poise * k,
        kb: [B.kb, 5], radial: true, cx: c.x, armorBreak: true, instance: this.newInstance(), aegisBurst: true });
      if (why === 'detonate') { p.overcharge = Math.min(A.over.max, p.overcharge + A.detonate.over * frac); p.overT = A.over.hold; }
    }
    this.emit('aegisOff', { p, why, x: c.x, y: c.y, frac });
  }
  detonateAegis(p) { this.endAegis(p, 'detonate'); }
  // The Nova whose Aegis shelters this player (their own, or one they stand inside), if any
  shieldFor(q) {
    for (const n of this.players) {
      if (!n.aegis || n.state === 'dead' || n.state === 'downed') continue;
      if (n === q) return n;
      const a = chest(n), b = chest(q);
      if (Math.hypot(a.x - b.x, a.y - b.y) < AEGIS.radius) return n;
    }
    return null;
  }
  // The Aegis a point (a shot of radius r) has reached, if any
  aegisAt(x, y, r) {
    for (const n of this.players) {
      if (!n.aegis || n.state === 'dead' || n.state === 'downed') continue;
      const c = chest(n);
      if (Math.hypot(x - c.x, y - c.y) < AEGIS.radius + r) return n;
    }
    return null;
  }
  // The Aegis takes a hit coming from (fx, fy): damage to the hard light becomes Overcharge. `key` makes
  // one attack (a hitbox, a blast) count once even when it reaches several players inside.
  absorbAegis(n, dmg, fx, fy, key) {
    const S = n.aegis; if (!S) return;
    if (key !== undefined) { if (S.seen.has(key)) return; S.seen.add(key); }
    const A = AEGIS, c = chest(n);
    let dx = fx - c.x, dy = fy - c.y; const m = Math.hypot(dx, dy) || 1; dx /= m; dy /= m;
    S.hp -= dmg;
    n.overcharge = Math.min(A.over.max, n.overcharge + dmg * A.over.perDmg); n.overT = A.over.hold;
    this.emit('aegisHit', { p: n, x: c.x + dx * A.radius, y: c.y + dy * A.radius, dx, dy, dmg, frac: Math.max(0, S.hp / S.max) });
    if (S.hp <= 0) this.endAegis(n, 'break');
  }

  // ---- Echo: sniper rifle and staff deflect ----
  // An instant shot down the level. Focus f (0-1) sets damage and poise; an upper-body hit is a critical;
  // at full focus it pierces everything in line, breaks armor and tags. No recoil: he stays where he is.
  fireSniper(p, f) {
    const R = HUNTER.rifle, c = chest(p), ax = p.aimX, ay = p.aimY, full = f >= 1;
    const x0 = c.x + ax * 0.9, y0 = c.y + ay * 0.9, wall = rayCast(x0, y0, ax, ay, R.range);
    if (wall.box && wall.box.type === 'd') this.damageBox(wall.box, R.minDmg + (R.maxDmg - R.minDmg) * f, wall.x, wall.y, p);
    const line = [];
    for (const e of this.enemies) {
      if (e.dead) continue;
      const hb = hurtbox(e), h = rayBoxT(x0, y0, ax, ay, hb.x0 - 0.06, hb.y0 - 0.06, hb.x1 + 0.06, hb.y1 + 0.06);
      if (h && h.t <= wall.t) line.push({ e, t: h.t });
    }
    line.sort((a, b) => a.t - b.t);
    const dmg = R.minDmg + (R.maxDmg - R.minDmg) * f * f, poise = R.poise[0] + (R.poise[1] - R.poise[0]) * f;
    let endT = wall.t, crits = 0, n = 0;
    for (const { e, t } of line) {
      const hy = y0 + ay * (t + 0.15), crit = hy > e.y + e.h * R.critZone;
      const res = hitEnemy(this, e, { owner: p, dmg: dmg * (crit ? R.crit : 1), poise: poise * (crit ? 1.3 : 1), kb: [sign(ax) * R.kb * (0.5 + f), 2],
        vx: ax, armorBreak: full, rail: full, snipe: true }, 'proj');
      n++;
      if (crit && res !== 'blocked') { crits++; this.emit('crit', { p, e, x: x0 + ax * t, y: hy }); }
      if (full && !e.dead) { e.tagged = Math.max(e.tagged, R.tag); this.emit('tag', { x: e.x, y: e.y + e.h, e }); }
      if (!full || res === 'blocked') { endT = t; break; }
    }
    this.emit('snipe', { p, x0, y0, x1: x0 + ax * endT, y1: y0 + ay * endT, ax, ay, f, full, crits, n, wall: endT === wall.t && wall.wall });
  }
  // Echo's deflect spin stuns every enemy the twirling staff touches (DEFLECT.stunReach), once per spin: light
  // enemies for DEFLECT.stun.light ticks, heavy ones for less. Bosses shrug it off; an enemy already staggered
  // or rooted by a snare keeps that instead; the Movement Gym's drill post doesn't stun (it is there to practise
  // parry timing against)
  spinStun(p) {
    const D = DEFLECT;
    if (SETTINGS.echoKit !== 'hunter' || p.parryT < 1 || p.parryT > D.window) return;
    if (p.parryT === 1 || !p.spinHit) p.spinHit = new Set();
    for (const e of this.enemies) {
      if (e.dead || e.boss || e.type === 'post' || p.spinHit.has(e) || e.state === 'stagger' || e.state === 'snared') continue;
      const gap = Math.abs(e.x - p.x) - e.w / 2 - p.w / 2;
      if (gap > D.stunReach || e.y > p.y + p.h + 0.3 || e.y + e.h < p.y - 0.3) continue;
      p.spinHit.add(e);
      const ticks = e.light ? D.stun.light : D.stun.heavy;
      this.director.release(e);
      e.stun = e.state === 'hitstun' ? Math.max(e.stun, e.st + ticks) : ticks;
      if (e.state !== 'hitstun') { e.state = 'hitstun'; e.st = 0; }
      e.atk = null; e.vx = (Math.sign(e.x - p.x) || p.facing) * 2.5; e.flash = Math.max(e.flash, 4);
      this.emit('spinStun', { p, e, x: e.x, y: e.y + e.h * 0.6 });
    }
  }

  // Echo's staff knocks an enemy shot back toward whoever fired it, as his own, faster; a perfect parry
  // hits harder and opens a riposte
  deflect(p, pr) {
    const D = DEFLECT, perfect = p.state === 'parry' && p.parryT <= parryWindows(p).perfect;
    const src = pr.owner && pr.owner.kind === 'enemy' && !pr.owner.dead ? pr.owner : null, sp = Math.hypot(pr.vx, pr.vy) * D.speed;
    let vx = -pr.vx * D.speed, vy = -pr.vy * D.speed;
    if (src) { const dx = src.x - pr.x, dy = src.y + src.h * 0.6 - pr.y, m = Math.hypot(dx, dy) || 1; vx = dx / m * sp; vy = dy / m * sp; }
    const heavy = !!pr.heavy;
    Object.assign(pr, { team: 'p', owner: p, vx, vy, dmg: (heavy ? D.dmg.heavy : D.dmg.standard) * (perfect ? D.perfect : 1), poise: heavy ? 40 : 20, kb: 5,
      hitSet: new Set(), deflected: true, intercept: false, heavy: false, homing: false, ttl: Math.max(pr.ttl, 120), gravity: 0 });
    if (p.state === 'parry' && !p.parryResult) { p.parryResult = perfect ? 'perfect' : 'normal'; p.st = 0; p.hitstop = perfect ? 5 : 3; }
    if (perfect) { p.riposteT = 20; addResolve(p, 12); gainUlt(p, ULT.gain.perfect, this); } else addResolve(p, 6);
    this.emit('deflect', { p, x: pr.x, y: pr.y, perfect, heavy });
  }

  // Prism rounds split into shards that fan out along (dx, dy); shards skip the enemy that split them
  splitPrism(pr, x, y, dx, dy, skip) {
    const S = MARKSMAN.prism.shard, n = pr.prism.shards, a0 = Math.atan2(dy, dx);
    const splash = { ...S.splash, dmg: S.splash.dmg * pr.prism.mult, poise: S.splash.poise * pr.prism.mult };
    for (let i = 0; i < n; i++) {
      const a = a0 + S.fan * (i - (n - 1) / 2);
      this.spawnProjectile({ team: 'p', owner: pr.owner, x, y, vx: Math.cos(a) * S.speed, vy: Math.sin(a) * S.speed, ttl: MARKSMAN.life, r: S.r,
        dmg: S.dmg * pr.prism.mult, poise: S.poise * pr.prism.mult, kb: 2, level: pr.level, perfect: pr.perfect, family: pr.family,
        intercept: true, interceptHeavy: false, bounces: pr.prism.bounces, kind: 'shard', splash });
      if (skip) this.projectiles[this.projectiles.length - 1].hitSet.add(skip.id);
    }
    this.emit('split', { p: pr.owner, x, y, n });
  }

  // ---- Nova: secondary weapons (SUB) ----
  fireSub(p, level, perfect = false) {
    const k = p.sub;
    if (k === 'grenade') this.throwGrenade(p, level, perfect);
    else if (k === 'chain') this.fireChain(p, level, perfect);
    else if (k === 'disc') this.throwDisc(p, level, perfect);
    else if (k === 'well') this.launchWell(p, level, perfect);
    else this.fireBurst(p, level, perfect);
    p.shootT = 10;
  }

  // Scatter: point-blank pellets, level 0 for the quick press or 1-3 when charged; level 3 adds a blast at
  // the muzzle. No recoil: Nova stays where he is.
  fireBurst(p, level, perfect = false) {
    const B = MARKSMAN.burst, S = level ? B[level] : B.tap, c = chest(p), ax = p.aimX, ay = p.aimY, a0 = Math.atan2(ay, ax);
    const x = c.x + ax * 0.5, y = c.y + ay * 0.5, mult = perfect ? MARKSMAN.perfectMult : 1;
    for (let i = 0; i < S.pellets; i++) {
      const a = a0 + S.fan * (i / (S.pellets - 1) - 0.5);
      this.spawnProjectile({ team: 'p', owner: p, x, y, vx: Math.cos(a) * S.speed, vy: Math.sin(a) * S.speed, ttl: MARKSMAN.life, r: 0.16,
        dmg: S.dmg * mult, poise: S.poise * mult, kb: S.kb, kbY: 2, intercept: true, interceptHeavy: false, kind: 'pellet',
        falloff: { x, y, d: B.falloff }, armorBreak: !!S.armorBreak && i === (S.pellets >> 1) });
    }
    if (S.blast) {
      this.explode({ owner: p, x: x + ax * 0.7, y: y + ay * 0.7, level, perfect, kind: 'blast',
        spec: { r: S.blast.r * (perfect ? 1.25 : 1), dmg: S.blast.dmg * mult, poise: S.blast.poise * mult, armorBreak: true } });
    }
    p.shootT = 10; p.burstCd = B.cd;
    this.emit('burst', { p, x, y, ax, ay, level, charged: level > 0, perfect });
  }

  // Grenade: a bouncing frag on a fuse (combat.js bounces it); it bursts early on an enemy, and level 3
  // scatters bomblets when it goes off (clusterBurst)
  throwGrenade(p, level, perfect) {
    const G = SUB.grenade, c = chest(p), ax = p.aimX, ay = p.aimY, dy = ay + G.lift, m = Math.hypot(ax, dy) || 1, sp = G.speed[level];
    const mult = perfect ? MARKSMAN.perfectMult : 1, B = G.blast[level];
    const x = c.x + ax * 0.6, y = c.y + ay * 0.6;
    this.spawnProjectile({ team: 'p', owner: p, x, y, vx: ax / m * sp, vy: dy / m * sp, gravity: G.gravity, bouncy: G.bounce, r: G.r, ttl: G.fuse[level],
      dmg: 0, poise: 0, kind: 'grenade', level, perfect, intercept: false,
      blast: { r: B.r * (perfect ? 1.2 : 1), dmg: B.dmg * mult, poise: B.poise * mult, armorBreak: !!B.armorBreak || perfect },
      cluster: level === 3 ? G.bomblets : null });
    p.burstCd = G.cd;
    this.emit('grenadeThrow', { p, x, y, level, perfect });
  }
  clusterBurst(pr, x, y) {
    const K = pr.cluster;
    for (let i = 0; i < K.n; i++) {
      const a = Math.PI / 2 + (i / (K.n - 1) - 0.5) * 2.2;
      this.spawnProjectile({ team: 'p', owner: pr.owner, x, y: y + 0.2, vx: Math.cos(a) * K.speed, vy: Math.sin(a) * K.speed + K.lift * 0.3, gravity: SUB.grenade.gravity,
        bouncy: 0.35, r: 0.14, ttl: K.fuse + i * 3, dmg: 0, poise: 0, kind: 'bomblet', level: 1, intercept: false, blast: { ...K.blast } });
    }
    this.emit('cluster', { p: pr.owner, x, y, n: K.n });
  }

  // Chain: instant lightning from the bracer to the nearest enemy in front (the lock-on target first), then
  // from each enemy on to the nearest one within `hop` m it has not hit. It needs a clear line each jump,
  // arcs round shields, and stuns light enemies (combat.hitEnemy: hit.stun).
  fireChain(p, level, perfect) {
    const C = SUB.chain, c = chest(p), ax = p.aimX, ay = p.aimY, mult = perfect ? MARKSMAN.perfectMult : 1;
    const x0 = c.x + ax * 0.6, y0 = c.y + ay * 0.6, R = C.range[level], cos = Math.cos(C.cone);
    const mid = e => ({ x: e.x, y: e.y + e.h * 0.55 });
    const clear = (a, b) => !segmentBlocked(a.x, a.y, b.x, b.y);
    let first = null;
    const t = p.lockT;
    if (t && !t.dead && Math.hypot(t.x - x0, mid(t).y - y0) <= R && clear({ x: x0, y: y0 }, mid(t))) first = t;
    else {
      let bd = R;
      for (const e of this.enemies) {
        if (e.dead) continue;
        const m = mid(e), dx = m.x - x0, dy = m.y - y0, d = Math.hypot(dx, dy);
        if (d < bd && d > 1e-3 && (dx * ax + dy * ay) / d > cos && clear({ x: x0, y: y0 }, m)) { bd = d; first = e; }
      }
    }
    const pts = [{ x: x0, y: y0 }], hit = new Set();
    let cur = first, from = { x: x0, y: y0 };
    while (cur && hit.size < C.jumps[level]) {
      hit.add(cur);
      const m = mid(cur); pts.push(m);
      hitEnemy(this, cur, { owner: p, dmg: C.dmg[level] * mult, poise: C.poise[level] * mult, kb: [(sign(m.x - from.x) || p.facing) * 2, 1], stun: C.stun[level], shock: true,
        armorBreak: level === 3 && hit.size === 1 }, 'blast');
      from = m; cur = null; let bd = C.hop;
      for (const e of this.enemies) {
        if (e.dead || hit.has(e)) continue;
        const n = mid(e), d = Math.hypot(n.x - m.x, n.y - m.y);
        if (d < bd && clear(m, n)) { bd = d; cur = e; }
      }
    }
    // Nothing in reach: the arc lashes out and earths itself on the nearest surface in front
    if (!first) { const h = rayCast(x0, y0, ax, ay, R * 0.7); pts.push({ x: h.x, y: h.y, fizzle: true }); }
    p.burstCd = C.cd;
    this.emit('chain', { p, pts, level, perfect, n: hit.size });
  }

  // Disc: out along the aim, (from level 2) a hover at the far end, then back to him (combat.steerDisc)
  throwDisc(p, level, perfect) {
    const D = SUB.disc, c = chest(p), ax = p.aimX, ay = p.aimY, sp = D.speed[level], mult = perfect ? MARKSMAN.perfectMult : 1;
    const x = c.x + ax * 0.6, y = c.y + ay * 0.6;
    this.spawnProjectile({ team: 'p', owner: p, x, y, vx: ax * sp, vy: ay * sp, ttl: 100000, r: D.r[level] * (perfect ? 1.2 : 1),
      dmg: D.dmg[level] * mult, poise: D.poise[level] * mult, kb: 3, pierce: true, intercept: true, interceptHeavy: level >= 2, kind: 'disc', level, perfect,
      disc: { phase: 'out', t: 0, out: D.out[level], hover: D.hover[level] + (perfect ? 20 : 0), dx: ax, dy: ay, speed: sp } });
    p.burstCd = D.cd;
    this.emit('discThrow', { p, x, y, level, perfect });
  }
  // Gravity Well: an orb that opens where it stops (updateWells)
  launchWell(p, level, perfect) {
    const W = SUB.well, c = chest(p), x = c.x + p.aimX * 0.7, y = c.y + p.aimY * 0.7;
    this.wells.push({ owner: p, x, y, px: x, py: y, vx: p.aimX * W.speed, vy: p.aimY * W.speed, phase: 'orb', t: 0, level, perfect,
      r: W.r[level] * (perfect ? 1.2 : 1), life: W.life[level] + (perfect ? 30 : 0), mult: perfect ? MARKSMAN.perfectMult : 1, held: new Set() });
    p.burstCd = W.cd;
    this.emit('wellLaunch', { p, x, y, level, perfect });
  }
  // Is a disc or a well of his still out?
  subOut(p, kind) {
    if (kind === 'disc') return this.projectiles.some(pr => pr.owner === p && pr.kind === 'disc' && !pr.dead);
    if (kind === 'well') return this.wells.some(w => w.owner === p);
    return false;
  }
  // Pressed again while it is out: the disc turns for home; the well opens where the orb is, or collapses
  recallSub(p, kind) {
    if (kind === 'disc') {
      const pr = this.projectiles.find(q => q.owner === p && q.kind === 'disc' && !q.dead);
      if (pr && pr.disc.phase !== 'back') { pr.disc.phase = 'back'; pr.disc.t = 0; pr.ghost = true; pr.hitSet.clear(); this.emit('discRecall', { p, x: pr.x, y: pr.y }); }
    } else if (kind === 'well') {
      const w = this.wells.find(q => q.owner === p);
      if (w) { if (w.phase === 'orb') this.openWell(w); else w.collapse = true; }
    }
  }
  openWell(w) {
    const W = SUB.well;
    w.phase = 'open'; w.t = 0;
    // A well that opens at floor level lifts a little, so what it catches floats up into it
    const gy = groundBelow(w.x, w.y + 0.05);
    if (gy > -Infinity && w.y - gy < W.lift && !pointInSolid(w.x, gy + W.lift)) w.y = gy + W.lift;
    this.emit('wellOpen', { p: w.owner, x: w.x, y: w.y, r: w.r, level: w.level });
  }
  updateWells(frozen) {
    const W = SUB.well;
    for (const w of this.wells) {
      w.px = w.x; w.py = w.y; w.t++;
      const gone = !this.players.includes(w.owner);
      if (w.phase === 'orb') {
        const nx = w.x + w.vx * DT, ny = w.y + w.vy * DT;
        let open = w.t >= W.travel[w.level] || gone;
        if (pointInSolid(nx, ny)) open = true; else { w.x = nx; w.y = ny; }
        if (!open) for (const e of this.enemies) { if (!e.dead && Math.abs(e.x - w.x) < e.w / 2 + 0.35 && w.y > e.y - 0.35 && w.y < e.y + e.h + 0.35) { open = true; break; } }
        if (open) this.openWell(w);
        continue;
      }
      // Open: pull light enemies in and hold them, drag heavy ones, swallow enemy shots, hurt everything
      const L = w.level, tickHit = w.t % W.tick === 0;
      for (const e of this.enemies) {
        if (e.dead) continue;
        const cy = e.y + e.h * 0.5, dx = w.x - e.x, dy = w.y - cy, d = Math.hypot(dx, dy);
        if (d > w.r) continue;
        if (tickHit) hitEnemy(this, e, { owner: w.owner, dmg: W.tickDmg[L] * w.mult, poise: 4, kb: [0, 0], well: true }, 'blast');
        if (e.dead || e.boss || frozen) continue;
        const T = ENEMY_TYPES[e.type] || {};
        if (T.stationary) continue;
        const ux = d > 1e-3 ? dx / d : 0, uy = d > 1e-3 ? dy / d : 0, sp = Math.min(W.pull[L], d * 6);
        if (e.light && e.armor <= 0) {
          if (!['stagger', 'caught', 'snared'].includes(e.state)) {
            if (e.state === 'windup' || e.state === 'aim' || e.state === 'lock') this.director.release(e);
            e.state = 'launched'; e.st = 1;
          }
          e.vx = ux * sp; e.vy = uy * sp + (e.flier ? 0 : GRAVITY * DT); e.wellT = 2;
        } else {
          // Heavy: dragged along the ground toward it
          const step = ux * W.pull[L] * W.heavy * DT, nx = e.x + step;
          if (!pointInSolid(nx + sign(step) * e.w / 2, e.y + 0.3)) e.x = nx;
          e.wellT = 2;
        }
      }
      for (const pr of this.projectiles) {
        if (pr.team !== 'e' || pr.dead) continue;
        const dx = w.x - pr.x, dy = w.y - pr.y, d = Math.hypot(dx, dy);
        if (d > w.r) continue;
        if (d < 0.6) { pr.dead = true; this.emit('erase', { x: pr.x, y: pr.y }); continue; }
        const sp = Math.hypot(pr.vx, pr.vy); pr.vx += dx / d * 40 * DT; pr.vy += dy / d * 40 * DT;
        const s2 = Math.hypot(pr.vx, pr.vy) || 1; pr.vx *= sp / s2; pr.vy *= sp / s2;
      }
      if (w.t >= w.life || w.collapse || gone) {
        const S = W.implode[L];
        this.explode({ owner: gone ? null : w.owner, x: w.x, y: w.y, level: L + 1, perfect: w.perfect, kind: 'wellCollapse',
          spec: { r: S.r * (w.perfect ? 1.2 : 1), dmg: S.dmg * w.mult, poise: S.poise * w.mult, armorBreak: !!S.armorBreak } });
        w.dead = true;
      }
    }
    this.wells = this.wells.filter(w => !w.dead);
  }

  fireBolt(p) {
    const c = chest(p);
    this.spawnProjectile({ team: 'p', owner: p, x: c.x + p.aimX * 0.7, y: c.y + p.aimY * 0.7, vx: p.aimX * ECHO.boltSpeed, vy: p.aimY * ECHO.boltSpeed,
      ttl: 36, r: 0.15, dmg: ECHO.boltDmg, poise: 8, kb: 2, kind: 'bolt' });
    this.emit('shot', { p, level: 0, bolt: true, x: c.x, y: c.y });
  }
  fireTracer(p) {
    const c = chest(p);
    this.spawnProjectile({ team: 'p', owner: p, x: c.x + p.aimX * 0.7, y: c.y + p.aimY * 0.7, vx: p.aimX * 40, vy: p.aimY * 40,
      ttl: 42, r: 0.16, dmg: ECHO.tracerDmg, poise: 5, kb: 1, tracer: true, kind: 'tracer' });
    this.emit('tracer', { p, x: c.x, y: c.y });
  }

  bulwarkPulse(p) {
    const c = chest(p), ax = p.aimX, ay = p.aimY, cos = Math.cos(Math.PI * 50 / 180);
    for (const e of this.enemies) {
      if (e.dead || e.type === 'turret') continue;
      const dx = e.x - c.x, dy = e.y + e.h / 2 - c.y, d = Math.hypot(dx, dy) || 0.01;
      if ((d < 4.4 && (dx * ax + dy * ay) / d > cos) || d < 1.4) {
        hitEnemy(this, e, { owner: p, dmg: 1, poise: 40, kb: [ax * 12, 4 + ay * 6], bulwark: true }, 'pulse');
      }
    }
    for (const pr of this.projectiles) {
      if (pr.team !== 'e' || pr.dead) continue;
      const dx = pr.x - c.x, dy = pr.y - c.y, d = Math.hypot(dx, dy) || 0.01;
      if ((d < 4.4 && (dx * ax + dy * ay) / d > cos) || d < 1.6) { pr.dead = true; this.emit('erase', { x: pr.x, y: pr.y }); }
    }
    this.barriers.push({ x: c.x + ax * 2.4, y: c.y + ay * 2.4, nx: ax, ny: ay, half: NOVA.barrierHalf, ttl: NOVA.barrierTicks, max: NOVA.barrierTicks, owner: p });
    this.emit('bulwark', { p, x: c.x, y: c.y, ax, ay });
  }

  findLashTarget(p, cx, cy, ax, ay, range) {
    let best = null, bt = range + 0.01;
    const consider = ent => {
      const dx = ent.x - cx, dy = ent.y + ent.h * 0.5 - cy, t = dx * ax + dy * ay;
      if (t <= 0.3 || t > range) return;
      if (Math.abs(dx * -ay + dy * ax) > ent.h * 0.5 + 0.9) return;
      if (t < bt) { bt = t; best = ent; }
    };
    for (const e of this.enemies) if (!e.dead) consider(e);
    for (const q of this.players) if (q !== p && q.state === 'downed') consider(q);
    return best;
  }

  lashConnect(p, t, held = false) {
    if (t.kind === 'player') {
      t.x = p.x + p.facing * 0.9; t.y = p.y + 0.1;
      this.emit('lashAlly', { p, q: t });
      return;
    }
    if (t.light && t.armor <= 0) {
      const ally = this.players.find(q => q !== p && q.state !== 'dead' && q.state !== 'downed' && Math.hypot(q.x - t.x, q.y - t.y) < 3);
      this.director.release(t);
      t.state = 'caught'; t.st = 0; t.catcher = p; t.catchSide = sign(t.x - p.x) || p.facing; t.shieldDir = t.catchSide; t.dropT = 16;
      addResolve(p, 4);
      this.emit('lashPull', { p, e: t });
      if (held) { p.leash = { e: t, t: 0 }; this.emit('leash', { p, e: t }); }
      if (ally) { this.bark(p, 'lash_save', 0.8); this.schedule(50, () => this.bark(ally, 'lash_reply', 1, true)); }
    } else if (held) {
      // Hunter kit: yank a heavy target off balance instead of zipping to it
      hitEnemy(this, t, { owner: p, dmg: 0.5, poise: HUNTER.yankPoise, kb: [sign(p.x - t.x) * 3, 0] }, 'pulse');
      this.emit('yank', { p, e: t });
    } else {
      p.zip = { target: t }; p.state = 'zip'; p.st = 0;
      this.emit('lashZip', { p, e: t });
    }
  }

  releaseLeash(p) {
    const L = p.leash; p.leash = null;
    if (L && L.e && L.e.state === 'caught') L.e.st = Math.max(L.e.st, 20);
    this.emit('leashEnd', { p });
  }

  // ---- Scarf modes ----
  // Flare Signature (Challenge): every enemy close by turns on Echo, including a sniper already
  // aiming at someone else, and attacks sooner.
  challenge(p) {
    const c = chest(p); let n = 0;
    for (const e of this.enemies) {
      if (e.dead || e.type === 'post' || e.type === 'turret') continue;
      if (Math.hypot(e.x - c.x, e.y + e.h / 2 - c.y) > SCARF.challengeRange) continue;
      e.taunter = p; e.tauntT = SCARF.tauntTicks; e.target = p; n++;
      if (e.type === 'sniper' && (e.state === 'aim' || e.state === 'lock')) { e.aimX = p.x; e.aimY = p.y + 1.0; }
      if (e.cd > 20) e.cd = 20;
      this.emit('taunted', { e });
    }
    addResolve(p, 4);
    this.emit('challenge', { p, x: c.x, y: c.y, n });
    const ally = this.players.find(q => q !== p && q.state !== 'dead' && q.state !== 'downed');
    if (n > 0) { this.bark(p, 'challenge', 0.6); if (ally) this.schedule(60, () => this.bark(ally, 'challenge_reply', 0.6, true)); }
  }
  // Veil Signature (Vanish): everything tracking Echo loses him, including a sniper mid-aim.
  shakeOffTrackers(p) {
    for (const e of this.enemies) {
      if (e.dead) continue;
      if (e.taunter === p) { e.tauntT = 0; e.taunter = null; }
      if (e.target !== p) continue;
      e.target = null;
      if (e.type === 'sniper' && e.state === 'aim') { this.director.release(e); e.state = 'idle'; e.st = 0; }
      this.emit('lostTrack', { e, p });
    }
  }

  // ---- Hunter kit snares ----
  throwSnare(p) {
    const c = chest(p);
    // Bola throw: flies straight at an enemy inside the throw cone; otherwise arcs out and plants as a trap
    const t = this.nearestEnemyInCone(c.x, c.y, p.aimX, p.aimY, 10, Math.PI / 6);
    let vx, vy, gravity;
    if (t) {
      const dx = t.x - c.x, dy = t.y + t.h * 0.5 - c.y, d = Math.hypot(dx, dy) || 1;
      vx = dx / d * 20; vy = dy / d * 20; gravity = 3;
    } else {
      vx = p.aimX * HUNTER.throwSpeed; vy = p.aimY * HUNTER.throwSpeed + HUNTER.throwLift; gravity = HUNTER.snareGravity;
    }
    this.spawnProjectile({ team: 'p', owner: p, x: c.x + p.aimX * 0.6, y: c.y + p.aimY * 0.6, vx, vy, gravity,
      r: 0.3, ttl: 110, dmg: 0, snare: true, kind: 'snare' });
    this.emit('snareThrow', { p, x: c.x, y: c.y });
  }
  plantSnare(p) { this.addSnare(p, p.x + p.facing * 0.6, p.y); }
  snareLanded(pr, x, y) {
    const gy = groundBelow(x, y + 0.2);
    if (gy > -Infinity && y - gy < 6) this.addSnare(pr.owner, x, gy);
  }
  addSnare(owner, x, y) {
    const mine = this.snares.filter(s => s.owner === owner);
    if (mine.length >= HUNTER.maxPlanted) mine[0].dead = true;
    this.snares = this.snares.filter(s => !s.dead);
    this.snares.push({ owner, x, y, armT: HUNTER.armTicks, ttl: HUNTER.life, dead: false });
    this.emit('snarePlant', { x, y, p: owner });
  }
  applySnare(e, owner) {
    if (e.dead) return;
    if (e.type === 'post' || e.type === 'turret') { this.emit('snared', { e, x: e.x, y: e.y + 0.4, owner, weak: true }); return; }
    e.tagged = Math.max(e.tagged, 600);
    if (e.light && e.armor <= 0) {
      this.director.release(e);
      if (e.catcher && e.catcher.leash && e.catcher.leash.e === e) this.releaseLeash(e.catcher);
      e.state = 'snared'; e.st = 0; e.stun = HUNTER.rootLight; e.vx = 0;
    } else {
      hitEnemy(this, e, { owner, dmg: 0.5, poise: HUNTER.rootHeavyPoise, kb: [0, 0] }, 'pulse');
    }
    this.emit('snared', { e, x: e.x, y: e.y + e.h * 0.4, owner });
  }
  updateSnares() {
    for (const s of this.snares) {
      s.ttl--; if (s.armT > 0) { s.armT--; continue; }
      for (const e of this.enemies) {
        if (e.dead || e.state === 'snared' || e.type === 'turret') continue;
        if (Math.abs(e.x - s.x) < e.w / 2 + 0.45 && e.y < s.y + 0.6 && e.y + e.h > s.y - 0.1) {
          this.applySnare(e, s.owner); s.dead = true; this.emit('snareTrigger', { x: s.x, y: s.y, e });
          break;
        }
      }
    }
    this.snares = this.snares.filter(s => !s.dead && s.ttl > 0);
  }

  // Echo's dash chases tagged enemies roughly in the dash direction.
  pursuitTarget(p, dx, dy) {
    if (p.char !== 'echo') return null;
    let best = null, bd = 12;
    const c = chest(p);
    for (const e of this.enemies) {
      if (e.dead || e.tagged <= 0) continue;
      const ex = e.x - c.x, ey = e.y + e.h / 2 - c.y, d = Math.hypot(ex, ey);
      if (d < bd && d > 1 && (ex * dx + ey * dy) / d > 0.5) { bd = d; best = e; }
    }
    return best;
  }

  // ---- Lock-on ----
  // Candidates in range, best first: near, in front, in sight (training targets last)
  lockCandidates(p) {
    const c = chest(p), out = [];
    for (const e of this.enemies) {
      if (e.dead) continue;
      const ty = e.y + e.h * 0.55, dx = e.x - c.x, d = Math.hypot(dx, ty - c.y);
      if (d > LOCK.range) continue;
      const behind = dx * p.facing < -0.5, blocked = segmentBlocked(c.x, c.y, e.x, ty);
      out.push({ e, score: d + (behind ? 7 : 0) + (blocked ? 10 : 0) + (e.type === 'post' || e.type === 'turret' ? 4 : 0) });
    }
    return out.sort((a, b) => a.score - b.score).map(o => o.e);
  }
  bestLockTarget(p) { return this.lockCandidates(p).find(e => e !== p.lockT) || null; }
  // Automatic lock-on: the nearest enemy in sight within LOCK.auto
  autoLockTarget(p) {
    const c = chest(p);
    return this.lockCandidates(p).find(e => Math.hypot(e.x - c.x, e.y + e.h * 0.55 - c.y) <= LOCK.auto && !segmentBlocked(c.x, c.y, e.x, e.y + e.h * 0.55)) || null;
  }
  nextLockTarget(p) {
    const list = this.lockCandidates(p);
    if (!list.length) return p.lockT;
    return list[(list.indexOf(p.lockT) + 1) % list.length];
  }
  setLock(p, e, why) {
    const prev = p.lockT;
    p.lockT = e; p.lockLost = 0;
    p.lockPicked = !!e && (why === 'on' || why === 'cycle');   // chosen by the player, not by automatic lock-on
    if (e && e !== prev) this.emit(prev ? 'lockSwitch' : 'lockOn', { p, e, why });
    else if (!e && prev) this.emit('lockOff', { p, why });
    else if (!e && why === 'on') this.emit('lockNone', { p });
  }
  // The target died (the lock moves to the next one), left (removed), or is out of range or sight too long
  validateLock(p) {
    const t = p.lockT; if (!t) return;
    if (t.dead || !this.enemies.includes(t)) { this.setLock(p, this.bestLockTarget(p), 'switch'); return; }
    const c = chest(p), ty = t.y + t.h * 0.55;
    if (Math.hypot(t.x - c.x, ty - c.y) > LOCK.keep) { this.setLock(p, null, 'range'); return; }
    p.lockLost = segmentBlocked(c.x, c.y, t.x, ty) ? p.lockLost + 1 : 0;
    if (p.lockLost > LOCK.lost) this.setLock(p, null, 'sight');
  }

  nearestEnemyInCone(x, y, dx, dy, range, half) {
    let best = null, bd = range; const cos = Math.cos(half);
    for (const e of this.enemies) {
      if (e.dead || e.type === 'post' || e.type === 'turret') continue;
      const ex = e.x - x, ey = e.y + e.h / 2 - y, d = Math.hypot(ex, ey);
      if (d < bd && (ex * dx + ey * dy) / d > cos) { bd = d; best = e; }
    }
    return best;
  }
  // Enemies within range and half-angle of a direction, ordered by signed angle from it
  enemiesInCone(x, y, dx, dy, range, half) {
    const cos = Math.cos(half), out = [];
    for (const e of this.enemies) {
      if (e.dead) continue;
      const ex = e.x - x, ey = e.y + e.h / 2 - y, d = Math.hypot(ex, ey);
      if (d > range || d < 1e-3 || (ex * dx + ey * dy) / d < cos) continue;
      out.push({ e, a: Math.atan2(dx * ey - dy * ex, dx * ex + dy * ey) });
    }
    return out.sort((a, b) => a.a - b.a).map(o => o.e);
  }
  nearestEnemyDist(x, y) {
    let bd = Infinity;
    for (const e of this.enemies) if (!e.dead) bd = Math.min(bd, Math.hypot(e.x - x, e.y + e.h / 2 - y));
    return bd;
  }
  enemyBelow(p, dist) {
    return this.enemies.find(e => !e.dead && Math.abs(e.x - p.x) < (e.w + p.w) / 2 && p.y - (e.y + e.h) < dist && p.y > e.y);
  }
  onOneWay(p) {
    return BOXES.some(b => b.type === 'o' && Math.abs(p.y - b.y1) < 0.03 && p.x > b.x0 && p.x < b.x1);
  }
  projectileTarget(b, savior) {
    const sp = Math.hypot(b.vx, b.vy) || 1;
    for (const q of this.players) {
      if (q === savior || q.state === 'dead' || q.state === 'downed') continue;
      const dx = q.x - b.x, dy = q.y + 1 - b.y, along = (dx * b.vx + dy * b.vy) / sp;
      if (along > 0 && along < 5 && Math.abs(dx * b.vy - dy * b.vx) / sp < 1.4) return q;
    }
    return null;
  }

  // ---- Barks (CP-09 test) ----
  bark(p, key, chance = 1, force = false) {
    if (!SETTINGS.barks || !p || Math.random() > chance) return;
    if (!force && ((p.barkCd || 0) > 0 || this.globalBarkCd > 0)) return;
    const lines = BARKS[p.char][key];
    if (!lines) return;
    p.barkCd = 300; this.globalBarkCd = 60;
    this.emit('bark', { p, text: lines[Math.floor(Math.random() * lines.length)] });
  }

  // ---- Players ----
  addPlayer(device, charId) {
    const used = new Set(this.players.map(p => p.slot));
    let slot = 0; while (used.has(slot)) slot++;
    if (slot > 3) return null;
    const anchor = this.activePlayers()[0];
    const cp = CHECKPOINTS[this.checkpoint];
    const x = anchor ? anchor.x - 0.8 : cp.x + slot * 0.8, y = anchor ? anchor.lastSafeY : cp.y;
    const p = createPlayer(slot, device, charId, x, y);
    p.mercy = 120;
    this.players.push(p); this.players.sort((a, b) => a.slot - b.slot);
    this.emit('join', { p });
    return p;
  }
  removePlayer(slot) {
    const gone = this.players.find(p => p.slot === slot);
    if (gone) this.leaveRole(gone);
    this.players = this.players.filter(p => p.slot !== slot);
    this.wells = this.wells.filter(w => this.players.includes(w.owner));
    for (const p of this.players) if (p.link && p.link.q === gone) this.endLink(p, 'gone');
    for (const b of this.barriers) if (b.owner === gone) b.ttl = 0;
    if (this.ultCast) { this.ultCast.members = this.ultCast.members.filter(m => this.players.includes(m)); if (!this.ultCast.members.length) this.ultCast = null; }
    this.emit('leave', { slot });
  }
  swapCharacter(p, charId) {
    if (this.ultCast || p.state === 'ult') return;   // not in the middle of an ultimate
    if (p.thrusting) this.emit('thrustOff', { p });
    if (p.aegis) this.endAegis(p, 'swap');
    if (p.beam) this.endBeam(p, 'swap');
    this.leaveRole(p);
    setCharacter(p, charId);
    this.emit('swap', { p });
  }

  // What a RAM or a Fix leaves behind when they swap out or leave: a charge in progress lets go of its pile,
  // a link ends, a leap is cut short, his Bulwark Wall comes down, and a Fix's gadgets pack up
  leaveRole(p) {
    if (p.rush) this.endRush(p, 'cancel');
    if (p.link) this.endLink(p, 'swap');
    p.leap = null;
    for (const b of this.barriers) if (b.kind === 'rampart' && b.owner === p) b.ttl = 0;
    for (const g of this.gadgets) if (g.owner === p && !g.dead) { g.dead = true; this.emit('gadgetEnd', { g, why: 'gone' }); }
  }
  downPlayer(p) {
    if (p.lockT) this.setLock(p, null, 'downed');
    if (p.aegis) this.endAegis(p, 'down');
    if (p.beam) this.endBeam(p, 'down');
    if (p.rush) this.endRush(p, 'cancel');
    if (p.link) this.endLink(p, 'down');
    p.leap = null; p.patch = null; p.tossArmed = false; p.fixRevive = false; p.reviveBy = null; p.reviveGain = 0;
    p.hp = 0; p.chargeT = 0; p.meleeCharged = false; p.dash = null; p.lash = null; p.zip = null; p.rifleT = 0; p.dashChargeT = 0;
    p.dodge = null; p.pound = null; p.burstT = 0; p.subArmed = false;
    p.veiled = false; p.veilCharge = 0; p.ambushT = 0;
    p.state = 'downed'; p.st = 0; p.revive = 0; p.autoRevive = 0;
    if (this.players.length === 1) {
      p.downedT = 9999;
      if (p.secondWind) { p.secondWind = false; p.autoRevive = 70; this.emit('downed', { p, secondWind: true }); }
      else { this.emit('downed', { p }); this.startWipe(); }
      return;
    }
    p.downedT = 600;
    this.emit('downed', { p });
    if (this.activePlayers().length === 0) this.startWipe();
  }
  bleedOut(p) {
    p.state = 'dead'; p.respawnT = 360;
    this.emit('bleedOut', { p });
    if (this.activePlayers().length === 0) this.startWipe();
  }
  revivePlayer(p, by, frac) {
    p.state = 'normal'; p.st = 0; p.hp = Math.round(p.maxHp * frac); p.mercy = 90; p.revive = 0;
    p.fixRevive = false; p.reviveBy = null; p.reviveGain = 0;
    p.h = CHARS[p.char].height; p.crouch = !hasHeadroom(p.x, p.y, p.w, p.h);
    this.emit('revived', { p, by });
    if (by) { this.bark(by, 'revive', 1, true); this.schedule(40, () => this.bark(p, 'revived', 1, true)); }
  }
  startWipe() { if (this.wipeT <= 0) { this.wipeT = 100; this.emit('wipe', {}); } }

  resetToCheckpoint() {
    const cp = CHECKPOINTS[this.checkpoint];
    this.players.forEach((p, i) => {
      p.x = cp.x + i * 0.8; p.y = cp.y; p.prevX = p.x; p.prevY = p.y; p.vx = 0; p.vy = 0;
      p.hp = p.maxHp; p.strain = 0; p.state = 'normal'; p.st = 0; p.secondWind = true; p.mercy = 60;
      p.h = CHARS[p.char].height; p.lastSafeX = p.x; p.lastSafeY = p.y; p.resolve = 0; p.chargeT = 0;
      p.veiled = false; p.veilCharge = 0; p.veilBreakT = 0; p.ambushT = 0; p.focus = 0;
      p.aegis = null; p.aegisCd = 0; p.overcharge = 0; p.beam = null;
      p.dodge = null; p.pound = null; p.burstT = 0; p.subArmed = false; p.ultRun = null; p.lockSuspend = false;
      p.rush = null; p.link = null; p.leap = null; p.patch = null; p.tossArmed = false; p.integrity = RAM.guard.integrity; p.guardBroken = false; p.kinetic = 0;
      p.plate = 0; p.overclockT = 0; p.tuneT = 0; p.braceT = 0; p.scrap = Math.max(p.scrap, FIX.scrap.start); p.fixRevive = false; p.reviveGain = 0;
    });
    this.projectiles = []; this.shockwaves = []; this.barriers = []; this.snares = []; this.wells = []; this.ultCast = null;
    this.gadgets = []; this.pickups = [];
    restoreBoxes(); this.spawnLevelPickups();
    for (const e of this.enemies) if (e.state === 'plowed') { e.state = 'idle'; e.plowBy = null; }
    for (const p of this.players) p.leash = null;
    if (this.arena.state !== 'cleared') this.resetArena();
    if (this.towerSpawned && this.enemies.some(e => e.zone === 'tower' && !e.dead)) {
      this.enemies = this.enemies.filter(e => e.zone !== 'tower'); this.towerSpawned = false;
    }
    // Skyline encounters that were not finished start over (and their gates open)
    for (const S of this.encounters) {
      if (S.state === 'cleared') continue;
      this.enemies = this.enemies.filter(e => e.enc !== S.def.id);
      S.state = 'idle'; S.wave = 0;
      for (const g of S.def.gates || []) GATES[g] = false;
    }
    this.director.reset();
    this.emit('respawnAll', {});
  }

  teleport(zoneId) {
    const z = ZONES.find(q => q.id === zoneId); if (!z) return;
    this.checkpoint = CHECKPOINTS.findIndex(c => c.x === z.spawn.x && c.y === z.spawn.y);
    if (this.checkpoint < 0) this.checkpoint = 0;
    if (zoneId === 'arena') { this.arena.state = 'idle'; }
    this.wipeT = 0;
    this.resetToCheckpoint();
    this.emit('banner', { text: z.name, sub: 'Zone loaded' });
  }

  resetArena() {
    const boss = this.arena.state === 'boss' || this.arena.state === 'bossReady';
    this.enemies = this.enemies.filter(e => e.zone !== 'arena');
    GATES.L = false; GATES.R = false;
    this.arena = { state: boss ? 'bossReady' : 'idle' };   // a wipe in the boss fight comes back to the boss
  }

  // The Concourse Lock's last wave: the Lockwarden drops in
  startWarden() {
    this.arena.state = 'boss';
    spawnBoss(this, 'warden', 87, 12, { zone: 'arena' });   // drops in beside the dais, not onto it
    this.emit('banner', { text: BOSS.warden.name, sub: BOSS.warden.title });
  }

  // Straight to a boss fight (pause menu): the arena's boss, or the beacon's with the relay already won
  bossRush(id) {
    if (id === 'warden') {
      this.teleport('arena'); this.arena.state = 'bossReady';
      for (const p of this.players) { p.x = 64.5 + p.slot * 0.8; p.prevX = p.x; }
      return;
    }
    for (const S of this.encounters) { S.state = S.def.boss ? 'idle' : 'cleared'; for (const g of S.def.gates || []) GATES[g] = false; }
    this.enemies = this.enemies.filter(e => e.zone !== 'skyline');
    this.checkpoint = CHECKPOINTS.findIndex(c => c.x === 302); this.wipeT = 0;
    this.resetToCheckpoint();
    this.emit('banner', { text: 'Skyline Relay', sub: 'The beacon pad' });
  }

  // ---- Nova: the perfect dodge ----
  // An attack reached him early in a dodge: time slows for enemies close by (and their shots), and he
  // gains Overcharge and ultimate charge
  perfectDodge(p) {
    const D = DODGE, c = chest(p);
    p.dodge.perfect = true;
    for (const e of this.enemies) if (!e.dead && Math.hypot(e.x - c.x, e.y + e.h / 2 - c.y) < D.slowRange + e.w / 2) e.slowT = e.boss ? D.slowTicks >> 1 : D.slowTicks;
    for (const pr of this.projectiles) if (pr.team === 'e' && !pr.dead && Math.hypot(pr.x - c.x, pr.y - c.y) < D.slowRange) pr.slowT = D.slowTicks;
    p.overcharge = Math.min(AEGIS.over.max, p.overcharge + D.over); p.overT = AEGIS.over.hold;
    gainUlt(p, ULT.gain.perfect, this);
    this.emit('perfectDodge', { p, x: c.x, y: c.y });
    this.bark(p, 'perfect', 0.3);
  }

  // ---- RAM: the Rampart, the Breach Cannon, the Ram Charge, and his abilities ----
  // The cannon on his shoulder: a heavy slug on a tap (level 0), or a Breach Shot (levels 1-3) that punches
  // through `pierce` enemies; level 3 bursts at the end of its flight (endBlast)
  fireSlug(p, level) {
    const C = RAM.cannon, S = level ? C[level] : C.slug, c = chest(p), ax = p.aimX, ay = p.aimY;
    const x = c.x + ax * 0.95, y = c.y + 0.3 + ay * 0.95;
    this.spawnProjectile({ team: 'p', owner: p, x, y, vx: ax * S.speed, vy: ay * S.speed, ttl: S.ttl, r: S.r, dmg: S.dmg, poise: S.poise, kb: S.kb, kbY: 2,
      pierce: !!S.pierce, pierceLeft: S.pierce || 0, armorBreak: !!S.armorBreak, intercept: true, interceptHeavy: level >= 1, level,
      kind: level ? 'breach' : 'slug', endBlast: S.blast || null });
    p.shootT = 12;
    this.emit('shot', { p, level, x, y, ax, ay, cannon: true });
  }

  // Which way the Rampart covers: a point is in front of it
  guardFaces(p, x, y) { const c = chest(p), [nx, ny] = p.guardDir; return (x - c.x) * nx + (y - c.y) * ny > -0.25; }
  // The guarding RAM whose shield a shot crossed between two points, coming from in front of it (so it covers
  // everyone behind him), if any
  rampartCross(ox, oy, x, y, r) {
    const G = RAM.guard;
    for (const p of this.players) {
      if (p.char !== 'ram' || p.state !== 'guard' || p.guardBroken) continue;
      const c = chest(p), [nx, ny] = p.guardDir, bx = c.x + nx * G.reach, by = c.y + ny * G.reach;
      const s0 = (ox - bx) * nx + (oy - by) * ny, s1 = (x - bx) * nx + (y - by) * ny;
      if (s0 < -r || s1 > r) continue;
      const t = s0 - s1 > 1e-6 ? Math.max(0, Math.min(1, s0 / (s0 - s1))) : 0;
      const cx = ox + (x - ox) * t, cy = oy + (y - oy) * t;
      if (Math.abs((cx - bx) * -ny + (cy - by) * nx) <= G.half + r) return p;
    }
    return null;
  }
  // A shot stopped by the Rampart: absorbed (Integrity, Kinetic), or on a Perfect Guard sent straight back at
  // whoever fired it, faster, as his. A shell bursts on it harmlessly.
  blockShot(p, pr) {
    const G = RAM.guard, perfect = p.guardT <= G.perfect;
    const diff = DIFFICULTY[SETTINGS.difficulty] || DIFFICULTY.normal;
    if (perfect && !pr.blast) {
      const src = pr.owner && pr.owner.kind === 'enemy' && !pr.owner.dead ? pr.owner : null, sp = Math.hypot(pr.vx, pr.vy) * G.reflect;
      let vx = -pr.vx * G.reflect, vy = -pr.vy * G.reflect;
      if (src) { const dx = src.x - pr.x, dy = src.y + src.h * 0.6 - pr.y, m = Math.hypot(dx, dy) || 1; vx = dx / m * sp; vy = dy / m * sp; }
      Object.assign(pr, { team: 'p', owner: p, vx, vy, dmg: Math.max(3, pr.dmg * 0.6) * (pr.heavy ? 1.6 : 1), poise: pr.heavy ? 45 : 22, kb: 6,
        hitSet: new Set(), deflected: true, reflected: true, intercept: false, heavy: false, homing: false, ttl: Math.max(pr.ttl, 120), gravity: 0 });
      this.guardResult(p, 0, true, pr.x, pr.y, false);
      return 'reflect';
    }
    pr.dead = true;
    if (pr.blast) this.emit('enemyBlast', { x: pr.x, y: pr.y, r: pr.blast.r * 0.6 });
    this.guardResult(p, (pr.blast ? pr.blast.dmg : pr.dmg) * diff.dmg, perfect, pr.x, pr.y, !!pr.heavy || !!pr.blast);
    return 'block';
  }
  // A blocked hit: the Integrity it costs, the Kinetic it stores, ultimate charge; a broken shield
  guardResult(p, dmg, perfect, x, y, heavy) {
    const G = RAM.guard, rate = boostRate(p);
    p.blockT = 0;
    if (perfect) {
      p.kinetic = Math.min(100, p.kinetic + G.perfectKinetic * rate);
      gainUlt(p, ULT.gain.perfect, this);
      this.emit('perfectGuard', { p, x, y, heavy });
      this.bark(p, 'perfect_guard', 0.3);
      return;
    }
    p.integrity -= dmg;
    p.kinetic = Math.min(100, p.kinetic + dmg * G.kinetic * rate);
    gainUlt(p, dmg * ULT.gain.blocked, this);
    this.emit('guardBlock', { p, x, y, dmg, heavy, frac: Math.max(0, p.integrity / G.integrity) });
    if (heavy && p.onGround) p.vx = -p.facing * 3.5;   // a heavy blow shoves him back a step
    if (p.integrity <= 0) {
      p.integrity = 0; p.guardBroken = true; p.guardOffT = 0;
      p.state = 'hitstun'; p.st = 0; p.stun = G.brokenStun; p.vx = -p.facing * 4; p.vy = 2;
      this.emit('rampartBreak', { p, x, y });
    }
  }

  // Kinetic Release: the Rampart dumps its stored Kinetic as a cone of force along the guard
  kineticRelease(p) {
    const K = RAM.release, k = Math.min(1, p.kinetic / 100), c = chest(p), [nx, ny] = p.guardDir;
    const at = a => a[0] + (a[1] - a[0]) * k;
    const r = at(K.r), dmg = at(K.dmg), poise = at(K.poise), kb = at(K.kb), cos = Math.cos(K.cone);
    const ox = c.x + nx * 0.6, oy = c.y + ny * 0.6;
    for (const e of this.enemies) {
      if (e.dead) continue;
      const hb = hurtbox(e), qx = Math.max(hb.x0, Math.min(ox, hb.x1)), qy = Math.max(hb.y0, Math.min(oy, hb.y1));
      const dx = qx - ox, dy = qy - oy, d = Math.hypot(dx, dy);
      if (d > r || (d > 0.9 && (dx * nx + dy * ny) / d < cos)) continue;
      hitEnemy(this, e, { owner: p, dmg, poise, kb: [(sign(e.x - p.x) || p.facing) * kb, 4 + 4 * k], armorBreak: k >= 0.5, heavy: true, ram: true, ramKnock: true }, 'pulse');
    }
    for (const pr of this.projectiles) {
      if (pr.team !== 'e' || pr.dead) continue;
      const dx = pr.x - ox, dy = pr.y - oy, d = Math.hypot(dx, dy) || 1e-3;
      if (d < r && (dx * nx + dy * ny) / d > cos) { pr.dead = true; this.emit('erase', { x: pr.x, y: pr.y }); }
    }
    p.kinetic = 0;
    this.emit('kineticRelease', { p, x: ox, y: oy, nx, ny, r, k, cone: K.cone });
  }

  // The Ram Charge, after he has moved this tick: what is in front of him is scooped up (light enemies, and
  // from RAM.rush.heavyFrom heavy ones too) and carried along, stacked against the shield. A boss or a rooted
  // enemy stops it with a heavy hit; a wall (for him or for the pile) stops it with a slam.
  ramPlow(p) {
    const R = RAM.rush, r = p.rush; if (!r) return;
    const L = r.level, dir = r.dx, front = p.x + dir * p.w / 2;
    // A breakable piece in the way is smashed (a pillar takes a Battering Ram); he carries on through what breaks
    for (const hy of [0.4, p.h * 0.5, p.h - 0.4]) {
      const bk = breakableAt(front + dir * 0.2, p.y + hy, 0.05);
      if (bk && this.damageBox(bk, 25 + 30 * L, front + dir * 0.2, p.y + hy, p)) p.hitWall = 0;
    }
    for (const e of this.enemies) {
      if (e.dead || r.hit.has(e) || e.state === 'plowed') continue;
      const ax0 = dir > 0 ? front - 0.3 : front - R.reach, ax1 = dir > 0 ? front + R.reach : front + 0.3;
      if (e.x + e.w / 2 < ax0 || e.x - e.w / 2 > ax1 || e.y > p.y + p.h - 0.1 || e.y + e.h < p.y + 0.15) continue;
      r.hit.add(e);
      const rooted = e.boss || ['post', 'turret', 'sniper', 'mortar'].includes(e.type), heavy = !e.light;
      if (rooted || (heavy && L < R.heavyFrom)) {
        hitEnemy(this, e, { owner: p, dmg: R.bonk.dmg[L], poise: R.bonk.poise[L], kb: [dir * 6, 2], armorBreak: L >= 2, heavy: true, ram: true, ramKnock: true }, 'pulse');
        this.endRush(p, 'bonk', e); p.state = 'normal'; p.st = 0;
        return;
      }
      hitEnemy(this, e, { owner: p, dmg: R.catchDmg[L], poise: R.poise[L], kb: [dir * 3, 0], armorBreak: L >= 2, ram: true }, 'pulse');
      if (e.dead) continue;
      this.director.release(e);
      e.state = 'plowed'; e.st = 0; e.plowBy = p; r.carried.push(e);
      if (r.carried.length === 1) p.hitstop = Math.max(p.hitstop, 2);
      this.emit('plowCatch', { p, e, level: L });
    }
    this.carryPile(p, r.carried, dir);
    const jam = r.carried.some(e => !e.dead && !hasHeadroom(e.x, e.y + 0.05, e.w, e.h - 0.1));
    if (p.hitWall || jam) { this.endRush(p, 'wall'); p.state = 'normal'; p.st = 0; p.vx = 0; }
  }
  // Carried enemies are stacked in front of him, at his feet; a pile that runs into a wall pushes him back
  carryPile(p, pile, dir) {
    let off = p.x + dir * p.w / 2;
    for (const e of pile) {
      if (e.dead) continue;
      e.prevX = e.x; e.prevY = e.y;
      e.x = off + dir * (e.w / 2 + 0.06); off += dir * (e.w + 0.06);
      e.y = p.y; e.vx = p.vx; e.vy = 0;
    }
  }
  // The charge ends: at a wall the pile is slammed into it (a big hit, and they reel); otherwise it is
  // thrown on ahead of him. Hitting something he can't move bounces him back a step.
  endRush(p, why, bonked = null) {
    const r = p.rush; if (!r) return;
    const R = RAM.rush, L = r.level, dir = r.dx;
    const pile = r.carried.filter(e => !e.dead && e.state === 'plowed');
    if (why === 'wall' && pile.length) {
      // the pile can't stay inside the wall: back him off until it fits
      for (let i = 0; i < 24 && pile.some(e => !hasHeadroom(e.x, e.y + 0.05, e.w, e.h - 0.1)); i++) { p.x -= dir * 0.1; this.carryPile(p, pile, dir); }
    }
    for (const e of pile) {
      e.plowBy = null;
      if (why === 'wall') {
        hitEnemy(this, e, { owner: p, dmg: R.splat.dmg[L], poise: R.splat.poise[L], kb: [-dir * 2, 2], armorBreak: true, heavy: true, ram: true }, 'pulse');
        if (e.dead) continue;
        this.director.release(e); e.state = 'stagger'; e.st = 0; e.stun = R.splat.stun; e.poise = 0; e.vx = -dir * 2;
        this.emit('stagger', { x: e.x, y: e.y + e.h * 0.6, e });
      } else if (why === 'cancel') { e.state = 'hitstun'; e.st = 0; e.stun = 16; }
      else { e.state = 'launched'; e.st = 0; e.vx = dir * R.end.kb[0] * (1 + 0.15 * L); e.vy = R.end.kb[1]; }
    }
    if (why === 'wall' && pile.length) this.emit('ramSplat', { p, x: p.x + dir * (p.w / 2 + 0.6), y: p.y + 1, n: pile.length, level: L });
    if (why === 'bonk') { p.vx = -dir * 4; p.hitstop = Math.max(p.hitstop, 4); this.emit('ramBonk', { p, e: bonked, x: p.x + dir * p.w / 2, y: p.y + 1.2, level: L }); }
    p.rush = null;
    this.emit('rushEnd', { p, why, n: pile.length });
  }

  // Bulwark Wall: a hard-light wall planted in front of him (one at a time). It is a barrier: enemy shots stop
  // at it, the team's pass through it boosted (combat.updateProjectiles), and enemies can't get through it
  // (wallBlock) until it breaks.
  raiseWall(p) {
    const W = RAM.wall;
    for (const b of this.barriers) if (b.kind === 'rampart' && b.owner === p) b.ttl = 0;
    let x = p.x + p.facing * W.dist;
    for (let i = 0; i < 6 && pointInSolid(x, p.y + 0.5); i++) x -= p.facing * 0.3;   // against a wall it goes up close
    const gy = groundBelow(x, p.y + 0.5), base = gy > -Infinity && p.y - gy < 4 ? gy : p.y;
    this.barriers.push({ kind: 'rampart', owner: p, x, y: base + W.half, nx: p.facing, ny: 0, half: W.half, ttl: W.ticks, max: W.ticks, hp: W.hp, maxHp: W.hp, hitT: 0 });
    p.wallCd = W.cd;
    this.emit('wallUp', { p, x, y: base });
  }
  // An enemy that would pass through a Bulwark Wall is held on its own side (a boss smashes it)
  wallBlock(e) {
    for (const b of this.barriers) {
      if (b.kind !== 'rampart' || b.ttl <= 0) continue;
      if (e.y > b.y + b.half || e.y + e.h < b.y - b.half) continue;
      const hw = e.w / 2, s1 = e.x - b.x;
      if (e.boss) { if (Math.abs(s1) < hw) { b.hp = 0; b.ttl = 0; } continue; }
      const side = sign(e.prevX - b.x) || sign(s1) || 1;
      if (side * s1 < hw) { e.x = b.x + side * (hw + 0.02); if (e.vx * side < 0) e.vx = 0; }
    }
  }
  // The Bulwark Wall standing between an attacker and a player, if any (the wall takes the strike)
  wallBetween(a, q) {
    for (const b of this.barriers) {
      if (b.kind !== 'rampart' || b.ttl <= 0 || (a.x - b.x) * (q.x - b.x) >= 0) continue;
      if (q.y > b.y + b.half || q.y + q.h < b.y - b.half) continue;
      return b;
    }
    return null;
  }
  hurtWall(b, dmg, x, y) {
    b.hp -= dmg; b.hitT = 8;
    this.emit('wallHit', { b, x, y, frac: Math.max(0, b.hp / b.maxHp) });
    if (b.hp <= 0) b.ttl = 0;
  }

  // Guardian Link: to the teammate who needs it most (the most hurt, then the nearest), leaping to their side
  // first when they are far
  startLink(p) {
    const L = RAM.link;
    let best = null, bs = Infinity;
    for (const q of this.players) {
      if (q === p || q.state === 'dead' || q.state === 'downed') continue;
      const d = Math.hypot(q.x - p.x, q.y - p.y);
      if (d > L.range) continue;
      const s = d - 12 * (1 - q.hp / q.maxHp) + (this.guardianOf(q) ? 6 : 0);
      if (s < bs) { bs = s; best = q; }
    }
    if (!best) { this.emit('linkNone', { p }); return; }
    p.linkCd = L.cd;
    if (Math.hypot(best.x - p.x, best.y - p.y) > L.leapAt && p.state !== 'ult') {
      if (p.rush) this.endRush(p, 'cancel');
      const T = L.leapTicks * DT, dy = best.y - p.y;
      p.leap = { q: best, t: 0, side: sign(best.x - p.x) || 1, tx: best.x, ty: best.y };
      Object.assign(p, { state: 'leap', st: 0, vy: Math.min(30, Math.max(11, (dy + 0.5 * GRAVITY * T * T) / T)), onGround: false, crouch: false,
        dash: null, dodge: null, pound: null, chargeT: 0, dashChargeT: 0, meleeCharged: false });
      this.emit('leap', { p, q: best });
    } else this.makeLink(p, best);
  }
  makeLink(p, q) {
    if (p.link && p.link.q !== q) this.endLink(p, 'replaced');
    p.link = { q, t: RAM.link.ticks };
    addPlate(q, RAM.link.plate);
    this.emit('link', { p, q });
    this.bark(p, 'guardian', 0.5);
  }
  // The leap lands: enemies close by are shoved away, and the link is made
  landLeap(p) {
    const L = RAM.link, leap = p.leap; p.leap = null;
    p.state = 'normal'; p.st = 0; p.vx *= 0.2;
    this.spawnHitbox({ owner: p, team: 'p', x0: p.x - L.land.r, x1: p.x + L.land.r, y0: p.y - 0.3, y1: p.y + 1.9, dmg: L.land.dmg, poise: L.land.poise,
      kb: [9, 5], radial: true, cx: p.x, instance: this.newInstance(), scatter: true, ramKnock: true });
    this.emit('leapLand', { p, x: p.x, y: p.y });
    const q = leap && leap.q;
    if (q && this.players.includes(q) && q.state !== 'dead' && q.state !== 'downed') this.makeLink(p, q);
  }
  tickLink(p) {
    const k = p.link, q = k.q;
    if (--k.t <= 0 || !this.players.includes(q) || q.state === 'dead' || q.state === 'downed' || p.state === 'downed' || p.state === 'dead' ||
      Math.hypot(q.x - p.x, q.y - p.y) > RAM.link.breakAt) this.endLink(p, k.t <= 0 ? 'expire' : 'break');
  }
  endLink(p, why) { if (!p.link) return; const q = p.link.q; p.link = null; this.emit('linkEnd', { p, q, why }); }
  // The RAM linked to this player, if any
  guardianOf(q) { for (const g of this.players) if (g.link && g.link.q === q && g.state !== 'downed' && g.state !== 'dead') return g; return null; }
  // His share of a hit on the teammate he guards (his Plating first; it can put him down)
  linkHit(g, amount, from) {
    let dmg = amount;
    if (g.plate > 0) { const a = Math.min(g.plate, dmg); g.plate -= a; dmg -= a; }
    g.hp -= dmg; gainUlt(g, amount * ULT.gain.taken, this);
    this.emit('linkHit', { p: g, q: from, dmg: amount });
    if (g.hp <= 0) { g.hp = 0; this.downPlayer(g); }
  }

  // Provoke: every enemy close by turns on him (a sniper mid-aim too) and attacks sooner; he braces, and the
  // roar shoves light enemies right beside him
  provoke(p) {
    const P = RAM.provoke, c = chest(p); let n = 0;
    for (const e of this.enemies) {
      if (e.dead || e.type === 'post' || e.type === 'turret') continue;
      const d = Math.hypot(e.x - c.x, e.y + e.h / 2 - c.y);
      if (d > P.range) continue;
      e.taunter = p; e.tauntT = P.ticks; e.target = p; n++;
      if (e.type === 'sniper' && (e.state === 'aim' || e.state === 'lock')) { e.aimX = p.x; e.aimY = p.y + 1.2; }
      if (e.cd > 20) e.cd = 20;
      this.emit('taunted', { e, by: p });
      if (d < P.shove.r + e.w / 2 && e.light && !e.boss && !e.flier && e.state !== 'plowed') {
        this.director.release(e); e.state = 'launched'; e.st = 0; e.vx = (sign(e.x - p.x) || p.facing) * P.shove.kb; e.vy = 5;
      }
    }
    p.braceT = P.ticks; p.provokeCd = P.cd;
    this.emit('provoke', { p, x: c.x, y: c.y, n, r: P.range });
    this.bark(p, 'provoke', 0.6);
  }

  // Seismic Slam: shockwaves run out both ways along the floor from where the shield struck
  spawnQuake(p, Q) {
    for (const dir of [1, -1]) {
      this.shockwaves.push({ owner: p, team: 'p', x: p.x + dir * (p.w / 2 + 0.5), y: p.y, dir, speed: Q.speed, ttl: Q.ttl, dmg: Q.dmg, poise: Q.poise, h: Q.h,
        instance: this.newInstance() });
    }
    this.emit('quake', { p, x: p.x + p.facing * 1.0, y: p.y });
  }
  // Hydraulic Uplift: enemy shots in the sweep of the rising shield (in front of him and over his head) go
  sweepShots(p) {
    const x0 = p.facing > 0 ? p.x - 0.6 : p.x - 2.4, x1 = p.facing > 0 ? p.x + 2.4 : p.x + 0.6, y0 = p.y + 0.4, y1 = p.y + p.h + 1.8;
    for (const pr of this.projectiles) {
      if (pr.team !== 'e' || pr.dead || pr.x < x0 || pr.x > x1 || pr.y < y0 || pr.y > y1) continue;
      pr.dead = true; this.emit('erase', { x: pr.x, y: pr.y });
    }
  }

  // ---- Fix: the Rivet Gun, the Patch Beam, gadgets and power-ups ----
  fireRivet(p, i) {
    const R = FIX.rivet, c = chest(p), a = Math.atan2(p.aimY, p.aimX) + (i - 1) * 0.035;
    const x = c.x + p.aimX * 0.7, y = c.y + p.aimY * 0.7;
    this.spawnProjectile({ team: 'p', owner: p, x, y, vx: Math.cos(a) * R.speed, vy: Math.sin(a) * R.speed, ttl: R.ttl, r: R.r, dmg: R.dmg, poise: R.poise, kb: 1.5,
      intercept: true, interceptHeavy: false, kind: 'rivet', level: 0 });
    p.shootT = 10;
    this.emit('shot', { p, level: 0, x, y, ax: p.aimX, ay: p.aimY, rivet: true });
  }
  // A Hot Rivet: it sticks in what it hits (an enemy, or a wall) and bursts after its fuse (combat.js)
  fireHotRivet(p, level) {
    const H = FIX.rivet.hot, c = chest(p), x = c.x + p.aimX * 0.7, y = c.y + p.aimY * 0.7, L = level - 1;
    this.spawnProjectile({ team: 'p', owner: p, x, y, vx: p.aimX * H.speed, vy: p.aimY * H.speed, ttl: H.ttl, r: H.r, dmg: H.dmg[L], poise: H.poise[L], kb: 2,
      intercept: true, interceptHeavy: level >= 2, kind: 'hotRivet', level, stick: { fuse: H.fuse, blast: H.blast[L] } });
    p.shootT = 12;
    this.emit('shot', { p, level, x, y, ax: p.aimX, ay: p.aimY, rivet: true });
  }

  heal(q, amount, from = null) {
    if (!(amount > 0) || q.state === 'dead' || q.state === 'downed') return 0;
    const before = q.hp; q.hp = Math.min(q.maxHp, q.hp + amount);
    if (q.strain) q.strain = Math.max(0, Math.min(q.strain, q.maxHp - q.hp));
    const healed = q.hp - before;
    if (from && from !== q && healed > 0) gainUlt(from, healed * ULT.gain.heal, this);
    return healed;
  }
  // Fix's ground pound: a repair pulse from the landing
  repairPulse(p, x, y, r, amount) {
    for (const q of this.players) if (q.state !== 'dead' && q.state !== 'downed' && Math.hypot(q.x - x, q.y - y) <= r) this.heal(q, amount, p);
    this.emit('repairPulse', { p, x, y, r });
  }
  // Torque Slam: a ring of sparks that stuns light enemies and drones
  sparkRing(p, S) {
    const x = p.x + p.facing * 0.9, y = p.y + 0.5;
    for (const e of this.enemies) {
      if (e.dead) continue;
      const hb = hurtbox(e), qx = Math.max(hb.x0, Math.min(x, hb.x1)), qy = Math.max(hb.y0, Math.min(y, hb.y1));
      if (Math.hypot(qx - x, qy - y) > S.r) continue;
      const res = hitEnemy(this, e, { owner: p, dmg: S.dmg, poise: S.poise, kb: [(sign(e.x - x) || p.facing) * 3, 2], shock: true, stun: S.stun }, 'blast');
      if (res !== 'none' && !e.dead && e.light && !e.boss && e.armor <= 0 && e.state !== 'plowed') {
        this.director.release(e); e.state = 'hitstun'; e.st = 0; e.stun = S.stun; e.shockT = S.stun;
      }
    }
    this.emit('sparkRing', { p, x, y, r: S.r });
  }

  // The Patch Beam's target: the teammate who needs it most within range (a downed one first, then the most
  // hurt, then the nearest; not one behind where she aims). The current one is kept while it is in reach.
  patchTarget(p, keep) {
    const B = FIX.beam;
    let best = null, bs = Infinity;
    for (const q of this.players) {
      if (q === p || q.state === 'dead') continue;
      const d = Math.hypot(q.x - p.x, q.y + q.h * 0.5 - (p.y + p.h * 0.5));
      if (d > (q === keep ? B.keep : B.range)) continue;
      let s = d - (q.state === 'downed' ? 30 : 12 * (1 - q.hp / q.maxHp)) - (q === keep ? 4 : 0);
      if (p.aimFree && Math.abs(p.aimX) > 0.3 && (q.x - p.x) * p.aimX < -1.5) s += 8;
      if (s < bs) { bs = s; best = q; }
    }
    return best;
  }
  patchTick(p) {
    const B = FIX.beam, P = p.patch; P.t++;
    let q = P.target;
    const valid = q && this.players.includes(q) && q.state !== 'dead' && Math.hypot(q.x - p.x, q.y - p.y) <= B.keep;
    // Every half second it looks again in case someone needs it more
    if (!valid || P.t % 30 === 0) {
      const n = this.patchTarget(p, valid ? q : null);
      if (n !== q) { P.target = q = n; this.emit('patchTarget', { p, q }); }
    }
    if (!q) { P.self = true; this.heal(p, B.self / 60); return; }
    P.self = false;
    if (q.state === 'downed') { q.reviveGain = (q.reviveGain || 0) + B.revive; q.fixRevive = true; q.reviveBy = p; return; }
    if (this.heal(q, B.heal / 60, p) <= 0) addPlate(q, B.plateRate / 60, B.plate);
    q.tuneT = Math.max(q.tuneT, 3);
  }

  // Gadgets
  deployGadget(p) {
    const kind = p.gadgetSel, G = FIX.gadget[kind];
    if (p.scrap < G.cost) { this.emit('noScrap', { p, kind, cost: G.cost }); return false; }
    let x = p.x + p.facing * 0.95;
    for (let i = 0; i < 4 && pointInSolid(x, p.y + 0.4); i++) x -= p.facing * 0.3;
    const gy = groundBelow(x, p.y + 0.3);
    if (gy === -Infinity) { this.emit('noScrap', { p, kind, cost: G.cost, spot: true }); return false; }   // nothing to stand it on
    const old = this.gadgets.find(g => g.owner === p && g.kind === kind && !g.dead);
    if (old) { old.dead = true; this.emit('gadgetEnd', { g: old, why: 'moved' }); }
    p.scrap -= G.cost;
    const g = { id: this.newInstance(), kind, owner: p, x, y: p.y, py: p.y, gy, vy: 0, landed: p.y - gy < 0.05, level: 1, pts: 0, t: 0, life: G.life[0],
      hp: G.hp, maxHp: G.hp, cd: 24, rocketCd: 50, aim: p.facing, aimY: 0, dead: false, h: GADGET_H[kind] };
    if (g.landed) g.y = gy;
    this.gadgets.push(g);
    this.emit('gadgetDeploy', { p, g });
    this.bark(p, 'gadget', 0.25);
    return true;
  }
  // Jack-Up's jack, left behind as a spring pad (one at a time)
  placePad(p) {
    for (const g of this.gadgets) if (g.owner === p && g.kind === 'pad') g.dead = true;
    const gy = groundBelow(p.x, p.y + 0.3);
    if (gy === -Infinity || p.y - gy > 0.3) return;
    this.gadgets.push({ id: this.newInstance(), kind: 'pad', owner: p, x: p.x, y: gy, py: gy, gy, vy: 0, landed: true, level: 1, pts: 0, t: 0, life: FIX.pad.life,
      hp: 999, maxHp: 999, dead: false, h: GADGET_H.pad });
    this.emit('padPlace', { p, x: p.x, y: gy });
  }
  // One of her gadgets (not a pad) close in front of her: melee is then the wrench
  gadgetNear(p) {
    return this.gadgets.some(g => g.owner === p && g.kind !== 'pad' && !g.dead && (g.x - p.x) * p.facing > -0.6 && Math.abs(g.x - p.x) < 1.9 && Math.abs(g.y - p.y) < 1.6);
  }
  // A gadget an enemy shot (radius r at x, y) has reached
  gadgetAt(x, y, r) {
    for (const g of this.gadgets) {
      if (g.dead || g.kind === 'pad') continue;
      const nx = Math.max(g.x - 0.4, Math.min(x, g.x + 0.4)), ny = Math.max(g.y, Math.min(y, g.y + g.h));
      if (Math.hypot(x - nx, y - ny) < r) return g;
    }
    return null;
  }
  hurtGadget(g, dmg) {
    if (g.dead || g.kind === 'pad') return;
    g.hp -= dmg; g.hitT = 8;
    this.emit('gadgetHit', { g, dmg });
    if (g.hp <= 0) { g.dead = true; this.emit('gadgetEnd', { g, why: 'broken' }); }
  }
  // A wrench hit: two raise a gadget a level (up to 3), refreshing it; at level 3 they repair it and buy it time
  wrenchGadget(g, p) {
    if (g.dead || g.kind === 'pad') return;
    const G = FIX.gadget[g.kind];
    if (g.level < 3) {
      if (++g.pts >= 2) { g.pts = 0; g.level++; g.t = 0; g.life = G.life[g.level - 1]; g.hp = g.maxHp; this.emit('gadgetUp', { p, g }); this.bark(p, 'upgrade', 0.3); }
      else this.emit('gadgetWrench', { p, g });
    } else { g.hp = g.maxHp; g.t = Math.max(0, g.t - 120); this.emit('gadgetWrench', { p, g, max: true }); }
  }
  // An Amp Coil's field, worked out before anyone acts: allies inside charge and fill their bars faster
  ampField() {
    for (const p of this.players) p.ampK = 1;
    for (const g of this.gadgets) {
      if (g.kind !== 'coil' || g.dead || !g.landed) continue;
      const C = FIX.gadget.coil, L = g.level - 1;
      for (const p of this.players) if (p.state !== 'dead' && Math.hypot(p.x - g.x, p.y + p.h * 0.5 - (g.y + 0.8)) <= C.r[L]) p.ampK = Math.max(p.ampK, C.rate[L]);
    }
  }
  updateGadgets() {
    for (const g of this.gadgets) {
      if (g.dead) continue;
      const o = g.owner;
      if (!this.players.includes(o) || o.char !== 'fix') { g.dead = true; this.emit('gadgetEnd', { g, why: 'gone' }); continue; }
      g.py = g.y;
      if (!g.landed) {
        g.vy -= GRAVITY * DT; g.y += g.vy * DT;
        if (g.y <= g.gy) { g.y = g.gy; g.vy = 0; g.landed = true; this.emit('gadgetLand', { g }); }
      }
      if (g.hitT > 0) g.hitT--;
      if (++g.t >= g.life) { g.dead = true; this.emit('gadgetEnd', { g, why: 'expire' }); continue; }
      if (!g.landed) continue;
      const L = g.level - 1;
      if (g.kind === 'pylon') this.pylonTick(g, L);
      else if (g.kind === 'sentry') this.sentryTick(g, L);
      else if (g.kind === 'pad') this.padTick(g);
    }
    this.gadgets = this.gadgets.filter(g => !g.dead);
  }
  // Patch Pylon: heals everyone in its field; a downed teammate inside it gets back up on their own, slowly;
  // at level 3 it builds Plating on anyone at full health
  pylonTick(g, L) {
    const P = FIX.gadget.pylon;
    for (const q of this.players) {
      if (q.state === 'dead' || Math.hypot(q.x - g.x, q.y + q.h * 0.5 - (g.y + 0.7)) > P.r[L]) continue;
      if (q.state === 'downed') { q.reviveGain = (q.reviveGain || 0) + P.revive[L]; q.fixRevive = true; q.reviveBy = q.reviveBy || g.owner; continue; }
      if (this.heal(q, P.heal[L] / 60, g.owner) <= 0 && P.plate[L]) addPlate(q, P.plate[L] / 60, P.plateMax);
    }
  }
  // Sentry: the nearest enemy in sight and in range; a bolt every few ticks, and at level 3 a homing rocket too
  sentryTick(g, L) {
    const S = FIX.gadget.sentry, ox = g.x, oy = g.y + 0.78;
    if (g.cd > 0) g.cd--; if (g.rocketCd > 0) g.rocketCd--;
    let best = null, bd = S.range[L];
    for (const e of this.enemies) {
      if (e.dead || e.type === 'post') continue;
      const ex = e.x, ey = e.y + e.h * 0.55, d = Math.hypot(ex - ox, ey - oy);
      if (d < bd && !segmentBlocked(ox, oy, ex, ey)) { bd = d; best = e; }
    }
    g.target = best;
    if (!best) return;
    const dx = best.x - ox, dy = best.y + best.h * 0.55 - oy, m = Math.hypot(dx, dy) || 1;
    g.aim = dx / m; g.aimY = dy / m;
    if (g.cd <= 0) {
      this.spawnProjectile({ team: 'p', owner: g.owner, x: ox + g.aim * 0.45, y: oy + g.aimY * 0.45, vx: g.aim * S.speed, vy: g.aimY * S.speed, ttl: 60, r: 0.1,
        dmg: S.dmg[L], poise: S.poise, kb: 1.5, kind: 'sentryBolt', intercept: true, interceptHeavy: false, gadget: true });
      g.cd = S.every[L];
      this.emit('sentryShot', { g, x: ox + g.aim * 0.45, y: oy + g.aimY * 0.45 });
    }
    if (L >= 2 && g.rocketCd <= 0) {
      const R = S.rocket;
      this.spawnProjectile({ team: 'p', owner: g.owner, x: ox, y: oy + 0.2, vx: g.aim * R.speed * 0.5, vy: R.speed * 0.6, ttl: 150, r: 0.14, dmg: 0, poise: 0,
        kind: 'sentryRocket', intercept: false, blast: { ...R.blast }, seek: { target: best, delay: 8, until: 150, turn: 0.12, age: 0 }, gadget: true });
      g.rocketCd = R.every;
      this.emit('sentryRocket', { g, x: ox, y: oy + 0.2 });
    }
  }
  // Spring pad: whoever comes down onto it is bounced high (their double jump and air dash back); light enemies
  // standing on it are thrown up
  padTick(g) {
    const P = FIX.pad;
    for (const q of this.players) {
      if (q.padCd > 0 || !['normal', 'guard', 'patch', 'attack'].includes(q.state) || q.vy > 0.5) continue;
      if (Math.abs(q.x - g.x) > (P.w + q.w) / 2 || q.y < g.y - 0.05 || q.y > g.y + 0.45) continue;
      q.vy = P.bounce; q.onGround = false; q.coyote = 0; q.jumpsUsed = 0; q.airDashes = 1; q.airRise = true; q.airDodge = true;
      q.dashCarry = true; q.fastFall = false; q.padCd = 20;
      this.emit('padBounce', { p: q, g, x: g.x, y: g.y });
    }
    for (const e of this.enemies) {
      if (e.dead || !e.light || e.flier || e.boss || e.state === 'launched' || e.state === 'plowed') continue;
      if (Math.abs(e.x - g.x) > (P.w + e.w) / 2 || Math.abs(e.y - g.y) > 0.3) continue;
      this.director.release(e); e.state = 'launched'; e.st = 0; e.vy = P.enemyBounce; e.vx = 0;
      this.emit('padBounce', { e, g, x: g.x, y: g.y });
    }
  }

  // Power-ups: tossed to the nearest teammate in front (it homes in on them), or dropped at her feet
  tossPower(p) {
    const kind = p.powerSel, P = FIX.power;
    if (p.scrap < P.cost) { this.emit('noScrap', { p, kind, cost: P.cost }); return false; }
    p.scrap -= P.cost;
    let target = null, bd = P.range;
    for (const q of this.players) {
      if (q === p || q.state === 'dead' || q.state === 'downed') continue;
      const dx = q.x - p.x, d = Math.hypot(dx, q.y - p.y);
      if (d < bd && (dx * p.facing > -1 || d < 2.5)) { bd = d; target = q; }
    }
    const c = chest(p), x = c.x + p.facing * 0.4, y = c.y + 0.25;
    this.pickups.push({ id: this.newInstance(), kind, owner: p, x, y, px: x, py: y, vx: target ? (sign(target.x - p.x) || p.facing) * P.speed * 0.6 : p.facing * 2.5,
      vy: target ? P.lift : 4, target, t: 0, life: P.life, rest: false, dead: false });
    this.emit('powerToss', { p, kind, q: target, x, y });
    return true;
  }
  updatePickups() {
    const P = FIX.power;
    for (const k of this.pickups) {
      k.px = k.x; k.py = k.y; k.t++;
      if (k.target && (k.target.state === 'dead' || k.target.state === 'downed' || !this.players.includes(k.target))) k.target = null;
      if (k.target) {
        // a pass that speeds up into their hands
        const q = k.target, dx = q.x - k.x, dy = q.y + q.h * 0.55 - k.y, d = Math.hypot(dx, dy) || 1, sp = Math.min(26, P.speed + k.t * 0.5);
        k.vx += (dx / d * sp - k.vx) * 0.25; k.vy += (dy / d * sp - k.vy) * 0.25;
        k.x += k.vx * DT; k.y += k.vy * DT;
      } else if (!k.rest) {
        k.vy -= P.gravity * DT; const nx = k.x + k.vx * DT, ny = k.y + k.vy * DT;
        if (pointInSolid(nx, k.y)) k.vx *= -0.3; else k.x = nx;
        const g = groundBelow(k.x, k.y + 0.05);
        if (k.vy <= 0 && g > -Infinity && ny - 0.18 <= g) { k.y = g + 0.18; k.vy = 0; k.vx = 0; k.rest = true; } else k.y = ny;
        if (k.y < killYAt(k.x)) k.dead = true;
      }
      for (const q of this.players) {
        if (k.dead || q.state === 'dead' || q.state === 'downed' || (q === k.owner && k.t < P.ownerDelay)) continue;
        if (Math.abs(q.x - k.x) < q.w / 2 + P.grab * 0.5 && k.y > q.y - 0.4 && k.y < q.y + q.h + 0.4) { this.applyPower(q, k.kind, k.owner); k.dead = true; }
      }
      if (!k.dead && --k.life <= 0) { k.dead = true; this.emit('powerFade', { x: k.x, y: k.y, kind: k.kind }); }
    }
    this.pickups = this.pickups.filter(k => !k.dead);
  }
  applyPower(q, kind, from) {
    const P = FIX.power;
    if (kind === 'overclock') q.overclockT = Math.max(q.overclockT, P.overclock.ticks);
    else if (kind === 'plating') addPlate(q, P.plating.plate);
    else if (kind === 'ultcell') gainUlt(q, POWERUPS.ultcell.ult, this);
    else if (kind === 'fury') q.furyT = Math.max(q.furyT || 0, POWERUPS.fury.ticks);
    else this.heal(q, P.medkit.heal, from);
    this.emit('powerUp', { p: q, kind, from });
  }
  // Lift pads (level.js LIFTS): a player coming down onto one is thrown up to the platform over it
  liftTick() {
    for (const [x, y, top] of LIFTS) for (const q of this.players) {
      if (q.padCd > 0 || !['normal', 'guard', 'patch', 'attack'].includes(q.state) || q.vy > 0.5) continue;
      if (Math.abs(q.x - x) > 0.8 + q.w / 2 || q.y < y - 0.05 || q.y > y + 0.45) continue;
      q.vy = Math.sqrt(2 * GRAVITY * (top - y + 1.6)); q.onGround = false; q.coyote = 0; q.jumpsUsed = 0; q.airDashes = 1; q.airRise = true;
      q.fastFall = false; q.dashCarry = true; q.padCd = 30;   // (dashCarry: the full rise, as off Fix's spring pad)
      this.emit('liftBounce', { p: q, x, y, top });
    }
  }

  // Power-ups along the routes (level.js LEVEL_PICKUPS): they wait where they are until someone takes them
  spawnLevelPickups() {
    this.pickups = this.pickups.filter(k => !k.level);
    for (const [x, y, kind] of LEVEL_PICKUPS) this.addLevelPickup(x, y + 0.18, kind, true);
  }
  addLevelPickup(x, y, kind, rest = false, vy = 0) {
    const k = { id: this.newInstance(), kind, owner: null, x, y, px: x, py: y, vx: 0, vy, target: null, t: 0, life: rest ? 1e9 : POWERUPS.dropLife, rest, dead: false, level: true };
    this.pickups.push(k); return k;
  }

  // ---- Breakable pieces (level.js DESTRUCT) ----
  // Damage to a breakable piece: a pillar ignores blows under its `min`; at 0 it breaks (it stops being solid,
  // anything on it falls) and drops its power-up, if it holds one
  damageBox(b, dmg, x, y, by = null) {
    if (!b || b.broken || b.type !== 'd') return false;
    const D = DESTRUCT[b.tag];
    if (dmg < D.min) { this.emit('boxChip', { b, x, y, hard: true }); return false; }
    b.hp -= dmg; b.hitT = this.tick;
    if (b.hp > 0) { this.emit('boxChip', { b, x, y }); return false; }
    b.broken = true;
    this.emit('boxBreak', { b, x: (b.x0 + b.x1) / 2, y: (b.y0 + b.y1) / 2, by });
    if (b.loot) this.addLevelPickup((b.x0 + b.x1) / 2, (b.y0 + b.y1) / 2, b.loot, false, 5);
    return true;
  }
  // Every breakable piece touching a box (a strike), once per attack instance
  strikeBoxes(hb) {
    let set = this.hitSets.get(hb.instance); if (!set) { set = new Set(); this.hitSets.set(hb.instance, set); }
    for (const b of BOXES) {
      if (b.type !== 'd' || b.broken || set.has('b' + b.id)) continue;
      if (hb.x0 < b.x1 && hb.x1 > b.x0 && hb.y0 < b.y1 && hb.y1 > b.y0) {
        set.add('b' + b.id);
        this.damageBox(b, (hb.dmg || 1) * (hb.heavy || hb.armorBreak ? 2 : 1) + (hb.ram ? 6 : 0), (b.x0 + b.x1) / 2, Math.min(b.y1, Math.max(b.y0, (hb.y0 + hb.y1) / 2)), hb.owner);
      }
    }
  }
  // A blast: every breakable piece within r (more damage the closer)
  blastBoxes(x, y, r, dmg, by) {
    for (const b of BOXES) {
      if (b.type !== 'd' || b.broken) continue;
      const nx = Math.max(b.x0, Math.min(x, b.x1)), ny = Math.max(b.y0, Math.min(y, b.y1)), d = Math.hypot(x - nx, y - ny);
      if (d <= r) this.damageBox(b, dmg * (1.5 - 0.5 * d / Math.max(0.1, r)), nx, ny, by);
    }
  }

  // An enemy fell: Fix picks up Scrap from it if she is close
  onKill(e) {
    for (const p of this.players) {
      if (p.char !== 'fix' || p.state === 'dead' || p.state === 'downed' || Math.hypot(p.x - e.x, p.y - e.y) > FIX.scrap.killRange) continue;
      p.scrap = Math.min(FIX.scrap.max, p.scrap + FIX.scrap.kill);
      this.emit('scrap', { p, x: e.x, y: e.y + e.h / 2 });
    }
  }

  // ---- Ultimates (ULT) ----
  // A full bar and both triggers: the call. The world freezes for ULT.cast ticks while the caster powers up;
  // teammates with a full bar can pull both triggers to join (each join keeps the call open ULT.join more).
  startUlt(p) {
    p.ult = 0; p.chordP = p.chordF = 99;
    this.enterUlt(p);
    this.ultCast = { members: [p], phase: 'cast', t: 0, len: ULT.cast, name: ULT[p.char].name, team: false, power: 1 };
    // Nothing is left mid-motion to smear while everything holds still
    for (const q of [...this.players, ...this.enemies]) { q.prevX = q.x; q.prevY = q.y; }
    for (const pr of this.projectiles) { pr.px = pr.x; pr.py = pr.y; }
    for (const w of this.wells) { w.px = w.x; w.py = w.y; }
    this.emit('ultCast', { p, name: this.ultCast.name, x: p.x, y: p.y + p.h * 0.6 });
  }
  enterUlt(p) {
    if (p.beam) this.endBeam(p, 'ult');
    if (p.rush) this.endRush(p, 'cancel');
    p.leap = null; p.patch = null; p.tossArmed = false;
    if (p.thrusting) { p.thrusting = false; this.emit('thrustOff', { p }); }
    if (p.leash) this.releaseLeash(p);
    Object.assign(p, { state: 'ult', st: 0, ultRun: null, dash: null, dodge: null, pound: null, lash: null, zip: null, slash: null, chargeT: 0, burstT: 0,
      subArmed: false, rifleT: 0, dashChargeT: 0, meleeCharged: false, crouch: false, hitstop: 0, wallSliding: false });
  }
  ultCastTick(cmds) {
    const U = this.ultCast; U.t++;
    for (const q of this.players) {
      if (U.members.includes(q) || q.state === 'dead' || q.state === 'downed') continue;
      const cmd = cmds[q.slot] || EMPTY_CMD;
      trackChord(q, cmd);
      if (q.ult >= ULT.max && chordReady(q, cmd)) {
        q.ult = 0; q.chordP = q.chordF = 99; q.prevX = q.x; q.prevY = q.y;
        this.enterUlt(q); U.members.push(q); U.len = Math.max(U.len, U.t + ULT.join);
        this.emit('ultJoin', { p: q, n: U.members.length });
      }
    }
    if (U.t >= U.len) this.runUlt();
  }
  runUlt() {
    const U = this.ultCast; U.phase = 'run'; U.t = 0;
    U.team = U.members.length > 1; U.power = U.team ? ULT.team.power : 1;
    if (U.team) U.name = U.members.length > 2 ? ULT.teamAll : ULT.teamNames[U.members.map(m => m.char).sort().join('+')] || ULT.teamAll;
    for (const m of U.members) this.beginUlt(m, U.power);
    this.emit('ultRun', { members: [...U.members], team: U.team, name: U.name });
  }
  beginUlt(p, power) {
    const c = chest(p);
    if (p.char === 'nova') {
      // Supernova: it opens toward the lock-on target if he has one
      let dx = p.aimX, dy = p.aimY;
      if (p.lockT && !p.lockT.dead) { const ex = p.lockT.x - c.x, ey = p.lockT.y + p.lockT.h * 0.55 - c.y, m = Math.hypot(ex, ey) || 1; dx = ex / m; dy = ey / m; }
      p.ultRun = { kind: 'nova', t: 0, power, dx, dy, pulse: 0, segs: null };
    } else if (p.char === 'ram') {
      // Siege Breaker: the charge runs the way he aims (toward the lock-on target if he has one)
      let dx = Math.abs(p.aimX) > 0.2 ? sign(p.aimX) : p.facing;
      if (p.lockT && !p.lockT.dead) dx = sign(p.lockT.x - p.x) || dx;
      p.facing = dx;
      p.ultRun = { kind: 'ram', t: 0, power, dx, carried: [], hit: new Set(), slamT: 0, blocked: false };
    } else if (p.char === 'fix') {
      p.ultRun = { kind: 'fix', t: 0, power, podX: p.x, podY: p.y };
    } else {
      // Thousand Cuts: the targets are picked now and the cuts shared out among them, nearest first
      const E = ULT.echo, d = e => Math.hypot(e.x - c.x, e.y + e.h / 2 - c.y);
      const targets = this.enemies.filter(e => !e.dead && d(e) <= E.range).sort((a, b) => d(a) - d(b)).slice(0, E.targets);
      const cuts = targets.length ? Array.from({ length: E.strikes }, (_, i) => ({ e: targets[i % targets.length], at: E.start + i * E.every, i })) : [];
      const fin = cuts.length ? cuts[cuts.length - 1].at + 14 : E.start;
      p.ultRun = { kind: 'echo', t: 0, power, targets, cuts, fin, end: fin + E.end, x0: p.x, y0: p.y };
    }
    this.emit('ultBegin', { p, kind: p.ultRun.kind });
  }
  // Ultimate damage: breaks armor, reduced on bosses, and never charges anyone's ultimate
  ultHit(p, e, dmg, poise, kx = 0) {
    return hitEnemy(this, e, { owner: p, dmg: dmg * (e.boss ? ULT.boss : 1), poise, kb: [kx, 3], armorBreak: true, ult: true }, 'blast');
  }
  // Each member's ultimate, a tick at a time (called from their 'ult' state)
  ultStep(p, cmd) {
    const R = p.ultRun;
    if (!R) { p.vx = 0; p.vy = p.onGround ? -0.5 : 0; return; }
    R.t++;
    if (R.kind === 'nova') this.ultNova(p, R);
    else if (R.kind === 'ram') this.ultRam(p, R);
    else if (R.kind === 'fix') this.ultFix(p, R);
    else this.ultEcho(p, R);
  }
  // Siege Breaker: he braces while a colossal hard-light ram's head forms on the shield, Fortifying the team,
  // then charges along the floor scooping up every enemy in his path (ultRamCarry, after he moves) until a
  // wall, a boss or the end of the charge, and slams the pile down
  ultRam(p, R) {
    const U = ULT.ram, t = R.t, fall = () => { p.vy = p.onGround ? -0.5 : Math.max(p.vy - GRAVITY * DT, -MAX_FALL); };
    p.facing = R.dx;
    if (t === 1) {
      for (const q of this.players) if (q.state !== 'dead' && q.state !== 'downed') addPlate(q, U.fortify);
      this.emit('fortify', { p, members: this.players.filter(q => q.state !== 'dead' && q.state !== 'downed') });
    }
    if (R.slamT) { p.vx *= 0.7; fall(); if (t >= R.slamT + U.end) this.finishUlt(p); return; }
    if (t <= U.brace) { p.vx = 0; fall(); return; }
    p.vx = R.dx * U.speed; fall();
    if (t >= U.brace + U.charge) this.ultRamSlam(p, R);
  }
  ultRamCarry(p) {
    const R = p.ultRun, U = ULT.ram;
    if (!R || R.kind !== 'ram' || R.slamT || R.t <= U.brace) return;
    const dir = R.dx, front = p.x + dir * p.w / 2;
    for (const e of this.enemies) {
      if (e.dead || R.hit.has(e)) continue;
      const ax0 = dir > 0 ? front - 0.3 : front - 1.3, ax1 = dir > 0 ? front + 1.3 : front + 0.3;
      if (e.x + e.w / 2 < ax0 || e.x - e.w / 2 > ax1 || e.y > p.y + p.h + 0.6 || e.y + e.h < p.y) continue;
      R.hit.add(e);
      if (e.boss || ['post', 'turret'].includes(e.type)) { this.ultHit(p, e, U.catchDmg * 2 * R.power, 80, dir * 3); R.blocked = true; continue; }
      this.ultHit(p, e, U.catchDmg * R.power, 40, 0);
      if (!e.dead) { R.carried.push(e); e.state = 'plowed'; e.st = 0; e.plowBy = p; this.emit('plowCatch', { p, e, level: 3 }); }
    }
    this.carryPile(p, R.carried, dir);
    if ((R.t - U.brace) % U.every === 0) for (const e of R.carried) if (!e.dead) this.ultHit(p, e, U.dmg * R.power, 10, 0);
    const jam = R.carried.some(e => !e.dead && !hasHeadroom(e.x, e.y + 0.05, e.w, e.h - 0.1));
    if (p.hitWall || R.blocked || jam) this.ultRamSlam(p, R);
  }
  ultRamSlam(p, R) {
    const S = ULT.ram.slam; R.slamT = R.t; p.vx = 0;
    const x = p.x + R.dx * 1.4, y = p.y + 1.0, r = S.r * (R.power > 1 ? 1.15 : 1);
    for (const e of this.enemies) {
      if (e.dead) continue;
      const nx = Math.max(e.x - e.w / 2, Math.min(x, e.x + e.w / 2)), ny = Math.max(e.y, Math.min(y, e.y + e.h));
      if (Math.hypot(nx - x, ny - y) <= r) this.ultHit(p, e, S.dmg * R.power, S.poise, (sign(e.x - p.x) || R.dx) * 12);
    }
    for (const e of R.carried) { if (e.dead) continue; e.plowBy = null; e.state = 'launched'; e.st = 0; e.vx = R.dx * 11; e.vy = 9; }
    R.carried = [];
    this.emit('ramSlam', { p, x, y, r });
  }
  // Overhaul: a supply pod drops in front of her; its pulses of repair light heal the whole team (the first
  // brings back anyone who is down) and hurt every enemy on screen; then the team is Overclocked and Plated,
  // and her gadgets jump to level 3
  ultFix(p, R) {
    const U = ULT.fix, t = R.t;
    p.vx *= 0.7; p.vy = p.onGround ? -0.5 : Math.max(p.vy - GRAVITY * DT, -MAX_FALL);
    if (t === 1) {
      let x = p.x + p.facing * 2.4;
      for (let i = 0; i < 8 && pointInSolid(x, p.y + 0.5); i++) x -= p.facing * 0.3;
      const g = groundBelow(x, p.y + 1); R.podX = x; R.podY = g > -Infinity && p.y - g < 6 ? g : p.y;
      this.emit('podCall', { p, x: R.podX, y: R.podY, ticks: U.drop });
    }
    if (t === U.drop) {
      for (const e of this.enemies) {
        if (e.dead || Math.abs(e.x - R.podX) > U.pod.r + e.w / 2 || e.y > R.podY + 3 || e.y + e.h < R.podY - 0.5) continue;
        this.ultHit(p, e, U.pod.dmg * R.power, U.pod.poise, (sign(e.x - R.podX) || 1) * 9);
      }
      this.emit('podLand', { p, x: R.podX, y: R.podY });
    }
    const i = U.pulses.indexOf(t);
    if (i >= 0) {
      for (const q of this.players) {
        if (q.state === 'dead') continue;
        if (q.state === 'downed') { if (i === 0) this.revivePlayer(q, p, FIX.reviveHp); continue; }
        this.heal(q, q.maxHp * U.heal * (R.power > 1 ? 1.2 : 1));
      }
      const C = this.cam;
      for (const e of this.enemies) {
        if (e.dead || Math.abs(e.x - C.x) > C.halfW + 2 || Math.abs(e.y + e.h / 2 - C.y) > C.halfH + 2) continue;
        this.ultHit(p, e, U.dmg * R.power, 30, (sign(e.x - R.podX) || 1) * 4);
      }
      this.emit('overhaulPulse', { p, x: R.podX, y: R.podY + 1.2, i });
    }
    if (t === U.end - 8) {
      for (const q of this.players) if (q.state !== 'dead' && q.state !== 'downed') { q.overclockT = Math.max(q.overclockT, U.overclock); addPlate(q, U.plate); }
      for (const g of this.gadgets) if (g.owner === p && g.kind !== 'pad') { g.level = 3; g.pts = 0; g.t = 0; g.life = FIX.gadget[g.kind].life[2]; g.hp = g.maxHp; }
      this.emit('overhaulDone', { p, x: R.podX, y: R.podY });
    }
    if (t >= U.end) this.finishUlt(p);
  }
  ultNova(p, R) {
    const N = ULT.nova, t = R.t, c = chest(p);
    // He rises into a hover while the light gathers, then holds there
    p.vx *= 0.7; p.vy = t <= 12 ? N.rise * 10 * (1 - t / 12) : 0;
    if (t > N.gather && t <= N.gather + N.beam) {
      // The beam turns toward his aim, runs N.range m through everything, and pulses every N.pulse ticks
      const a0 = Math.atan2(R.dy, R.dx); let da = Math.atan2(p.aimY, p.aimX) - a0;
      while (da > Math.PI) da -= 2 * Math.PI; while (da < -Math.PI) da += 2 * Math.PI;
      const a = a0 + Math.max(-N.turn, Math.min(N.turn, da)); R.dx = Math.cos(a); R.dy = Math.sin(a);
      if (Math.abs(R.dx) > 0.2) p.facing = sign(R.dx);
      const g = { x0: c.x + R.dx * 0.6, y0: c.y + R.dy * 0.6, x1: c.x + R.dx * N.range, y1: c.y + R.dy * N.range };
      R.segs = [g];
      for (const pr of this.projectiles) if (pr.team === 'e' && !pr.dead && distToSeg(pr.x, pr.y, g) < N.width + pr.r) { pr.dead = true; this.emit('erase', { x: pr.x, y: pr.y }); }
      if ((t - N.gather) % N.pulse === 1) {
        R.pulse++;
        for (const e of this.enemies) if (!e.dead && segHitsBox(g, hurtbox(e), N.width)) this.ultHit(p, e, N.dmg * R.power, 30, sign(R.dx) * 2);
      }
    } else R.segs = null;
    if (t === N.gather + N.beam + 6) {
      // The nova: a burst of light from where he hangs
      const B = N.nova, r = B.r * (R.power > 1 ? 1.15 : 1);
      for (const e of this.enemies) {
        if (e.dead) continue;
        const nx = Math.max(e.x - e.w / 2, Math.min(c.x, e.x + e.w / 2)), ny = Math.max(e.y, Math.min(c.y, e.y + e.h));
        if (Math.hypot(nx - c.x, ny - c.y) <= r) this.ultHit(p, e, B.dmg * R.power, B.poise, (sign(e.x - c.x) || 1) * 9);
      }
      this.emit('ultNova', { p, x: c.x, y: c.y, r });
    }
    if (t >= N.end) this.finishUlt(p);
  }
  ultEcho(p, R) {
    const E = ULT.echo, t = R.t;
    p.vx = 0; p.vy = p.onGround ? -0.5 : 0;   // he is gone: only his cuts are seen
    for (const cut of R.cuts) {
      if (cut.at !== t) continue;
      let e = cut.e;
      if (e.dead) e = R.targets.find(q => !q.dead);   // its target fell: the cut goes to one still standing
      if (!e) continue;
      const dir = cut.i % 2 ? 1 : -1;
      this.ultHit(p, e, E.dmg * R.power, 16, dir * 2);
      this.emit('ultCut', { p, e, x: e.x, y: e.y + e.h * 0.55, i: cut.i, dir, ang: ((cut.i * 2.39996) % Math.PI) - Math.PI / 2 });
    }
    if (t === R.fin) {
      // Every cut lands again at once
      if (R.cuts.length) for (const e of R.targets) { if (!e.dead) this.ultHit(p, e, E.finisher * R.power, 90, (sign(e.x - p.x) || 1) * 8); }
      else {
        // No one in reach: a flourish around him
        const c = chest(p);
        for (const e of this.enemies) if (!e.dead && Math.hypot(e.x - c.x, e.y + e.h / 2 - c.y) <= E.flourish.r) this.ultHit(p, e, E.flourish.dmg * R.power, 60);
      }
      this.emit('ultFinisher', { p, x: p.x, y: p.y + p.h * 0.6, targets: R.targets.filter(e => !e.dead || e.deathT < 3), flourish: !R.cuts.length });
    }
    if (t >= R.end) this.finishUlt(p);
  }
  finishUlt(p) {
    const R = p.ultRun;
    if (R && R.carried) for (const e of R.carried) if (!e.dead && e.state === 'plowed') { e.plowBy = null; e.state = 'launched'; e.st = 0; e.vy = 6; }
    p.ultRun = null; p.state = 'normal'; p.st = 0; p.mercy = Math.max(p.mercy, ULT.mercy); p.vy = Math.min(p.vy, 0);
    this.emit('ultEnd', { p });
  }
  // The run (and a team ultimate's finisher) after every member has finished
  ultTick() {
    const U = this.ultCast;
    U.members = U.members.filter(m => this.players.includes(m));
    U.t++;
    if (U.phase === 'run') {
      if (U.members.some(m => m.ultRun)) return;
      if (U.team && U.members.length) { U.phase = 'finish'; U.t = 0; }
      else this.ultCast = null;
    } else if (U.phase === 'finish') {
      if (U.t === 8) {
        // The team finisher: every enemy on screen
        const C = this.cam, n = U.members.length, lead = U.members[0];
        for (const e of this.enemies) {
          if (e.dead || Math.abs(e.x - C.x) > C.halfW + 2 || Math.abs(e.y + e.h / 2 - C.y) > C.halfH + 2) continue;
          this.ultHit(lead, e, ULT.team.dmg * n, 120, (sign(e.x - C.x) || 1) * 10);
        }
        this.emit('teamFinisher', { name: U.name, members: [...U.members], x: C.x, y: C.y, chars: U.members.map(m => m.char) });
      }
      if (U.t >= ULT.team.t) this.ultCast = null;
    }
  }

  spawnGym() {
    this.enemies.push(createEnemy('post', 54.5, 0, { zone: 'gym' }));
    this.enemies.push(createEnemy('turret', 58, 3.05, { zone: 'gym', facing: -1 }));
  }

  // ---- The tick ----
  step(cmds) {
    this.tick++;
    // An ultimate being called: the world holds still, and only teammates joining in are listened to
    if (this.ultCast && this.ultCast.phase === 'cast') { this.ultCastTick(cmds); return; }
    if (this.globalBarkCd > 0) this.globalBarkCd--;
    const due = this.scheduled.filter(s => s.t <= this.tick);
    this.scheduled = this.scheduled.filter(s => s.t > this.tick);
    due.forEach(s => s.fn());
    // While an ultimate plays out, enemies, their shots and shockwaves stay frozen
    const frozen = !!this.ultCast;
    this.ampField();

    for (const p of this.players) {
      if (p.barkCd > 0) p.barkCd--;
      if (p.state === 'dead') { this.tickDead(p); continue; }
      const c0 = chest(p);
      updatePlayer(p, cmds[p.slot] || EMPTY_CMD, this);
      if (p.state === 'parry' && p.char === 'echo') this.spinStun(p);
      // RAM's charges carry what they scoop up, after he has moved
      if (p.state === 'rush') this.ramPlow(p);
      else if (p.state === 'ult' && p.ultRun && p.ultRun.kind === 'ram') this.ultRamCarry(p);
      if (p.state === 'dash') {
        const c1 = chest(p);
        for (const b of this.barriers) {
          if (b.kind !== 'rampart' && p.boostT <= 0 && crossesBarrier(b, c0.x, c0.y, c1.x, c1.y)) { p.boostT = 40; this.emit('boost', { p, x: c1.x, y: c1.y }); }
        }
        if (p.dash && !p.dash.pursuit && p.st === 1) {
          const t = this.pursuitTarget(p, p.dash.dx, p.dash.dy);
          if (t) { p.dash.pursuit = t; p.dash.t = Math.max(p.dash.t, 20); this.emit('pursuit', { p, e: t }); }
        }
      }
    }
    for (const e of this.enemies) {
      if (frozen && !e.dead) { e.prevX = e.x; e.prevY = e.y; if (e.flash > 0) e.flash--; continue; }
      updateEnemy(e, this);
    }
    if (this.barriers.some(b => b.kind === 'rampart')) for (const e of this.enemies) if (!e.dead) this.wallBlock(e);
    if (!frozen) updateShockwaves(this);
    updateProjectiles(this, frozen);
    this.updateWells(frozen);
    this.updateSnares();
    this.updateGadgets();
    this.updatePickups();
    this.liftTick();
    resolveHitboxes(this);
    if (this.ultCast) this.ultTick();
    for (const b of this.barriers) {
      b.ttl--; if (b.hitT > 0) b.hitT--;
      if (b.ttl <= 0 && b.kind === 'rampart') this.emit('wallDown', { b, x: b.x, y: b.y - b.half, broken: b.hp <= 0 });
    }
    this.barriers = this.barriers.filter(b => b.ttl > 0);

    this.tickRevives();
    this.updateCamera();
    this.updateEncounters();

    this.enemies = this.enemies.filter(e => !(e.dead && e.deathT > (e.boss ? 84 : 45)));   // a boss stays for its explosions
    if (this.tick % 120 === 0) {
      const keep = this.instanceSeq - 400;
      for (const k of this.hitSets.keys()) if (k < keep) this.hitSets.delete(k);
    }
    if (this.wipeT > 0) { this.wipeT--; if (this.wipeT === 0) this.resetToCheckpoint(); }
  }

  tickDead(p) {
    p.prevX = p.x; p.prevY = p.y;
    if (this.wipeT > 0) return;
    p.respawnT--;
    if (p.respawnT <= 0) {
      const ally = this.activePlayers()[0];
      if (!ally) return;
      p.x = ally.lastSafeX; p.y = ally.lastSafeY; p.prevX = p.x; p.prevY = p.y; p.vx = 0; p.vy = 0;
      p.state = 'normal'; p.st = 0; p.hp = Math.round(p.maxHp * 0.3); p.mercy = 120; p.h = CHARS[p.char].height;
      this.emit('respawn', { p });
    }
  }

  // Reviving: everyone standing beside a downed teammate adds to it (Fix counts FIX.revive times over), and so
  // do Fix's Patch Beam and Patch Pylons from range (reviveGain). Whoever Fix brings back (beside them, by beam
  // or by pylon) comes back with more health.
  tickRevives() {
    for (const p of this.players) {
      if (p.state !== 'downed') { p.reviveGain = 0; continue; }
      if (p.autoRevive > 0) { p.autoRevive--; if (p.autoRevive === 0) this.revivePlayer(p, null, 0.4); continue; }
      let gain = p.reviveGain || 0, by = p.reviveBy || null;
      for (const q of this.players) {
        if (q === p || q.state === 'downed' || q.state === 'dead' || q.state === 'hitstun' || Math.abs(q.x - p.x) >= 1.7 || Math.abs(q.y - p.y) >= 1.6) continue;
        gain += q.char === 'fix' ? FIX.revive : 1; by = by || q;
        if (q.char === 'fix') { p.fixRevive = true; by = q; }
      }
      p.reviveGain = 0;
      if (gain > 0) {
        p.revive += gain;
        if (p.revive >= 120) this.revivePlayer(p, by, p.fixRevive ? FIX.reviveHp : 0.4);
      } else p.revive = Math.max(0, p.revive - 0.5);
    }
  }

  updateCamera() {
    const act = this.players.filter(p => p.state !== 'dead');
    if (!act.length) return;
    let x0 = Infinity, x1 = -Infinity, y0 = Infinity, y1 = -Infinity;
    for (const p of act) {
      x0 = Math.min(x0, p.x); x1 = Math.max(x1, p.x); y0 = Math.min(y0, p.y); y1 = Math.max(y1, p.y + p.h);
      // Lining up a rocket jump, the camera eases back to show how high it will go, and it keeps the
      // peak in view on the way up, so the frame never has to chase the climb
      const pv = p.chargeT > 0 && this.rocketPreview(p);
      if (pv) y1 = Math.max(y1, pv.apex + 0.6);
      if (p.rocketT > 0 && p.vy > 0) { y1 = Math.max(y1, p.y + apexGain(p.vy) * 0.85 + p.h * 0.5); y0 = Math.min(y0, p.y - 2); }
    }
    // A boss in the fight stays in the frame
    const mid = (x0 + x1) / 2;
    for (const e of this.enemies) {
      if (!e.boss || e.dead || Math.abs(e.x - mid) > 24) continue;
      x0 = Math.min(x0, e.x - e.w / 2); x1 = Math.max(x1, e.x + e.w / 2); y0 = Math.min(y0, e.y); y1 = Math.max(y1, Math.min(e.y + e.h, y0 + 16));
    }
    const tanH = Math.tan((SETTINGS.fov * Math.PI / 180) / 2);
    const needW = (x1 - x0) + 9, needH = (y1 - y0) + 7;
    const MIN_D = 13, MAX_D = 32;
    let dist = Math.max(needH / 2 / tanH, needW / 2 / (tanH * this.aspect));
    dist = Math.max(MIN_D, Math.min(MAX_D, dist));
    const halfH = dist * tanH, halfW = halfH * this.aspect;
    this.cam = { x: (x0 + x1) / 2 + 0.8, y: (y0 + y1) / 2 + 1.0, dist, halfW, halfH };
    // Spread limit and recall (co-op). Being left behind is never a damage penalty.
    const multi = act.length > 1;
    for (const p of this.players) {
      if (p.state === 'dead') continue;
      if (p.y < killYAt(p.x)) { this.recall(p, true); continue; }
      if (!multi) continue;
      const right = this.cam.x + halfW - 0.7;
      if (p.x > right) { p.x = right; if (p.vx > 0) p.vx = 0; }
      const off = p.x < this.cam.x - halfW - 0.5 || p.y + p.h < this.cam.y - halfH - 1;
      p.offscreenT = off ? p.offscreenT + 1 : 0;
      if (p.offscreenT > 90 && p.state !== 'downed') this.recall(p, false);
    }
  }

  recall(p, pit) {
    const allies = this.activePlayers().filter(q => q !== p);
    const a = allies.sort((m, n) => Math.abs(m.x - p.x) - Math.abs(n.x - p.x))[0];
    const tx = a ? a.lastSafeX : p.lastSafeX, ty = a ? a.lastSafeY : p.lastSafeY;
    p.x = tx; p.y = ty; p.prevX = tx; p.prevY = ty; p.vx = 0; p.vy = 0; p.offscreenT = 0;
    p.mercy = 90;
    // An ultimate under way ends here (left running out of its state it would never finish, and the world
    // would stay frozen); one still being called carries on from the new spot
    if (p.state === 'ult') { if (p.ultRun) this.finishUlt(p); }
    else if (p.state !== 'downed') { p.state = 'normal'; p.st = 0; }
    this.emit('recall', { p, pit });
    if (pit && p.state !== 'downed') {
      p.hp -= 10;
      if (p.hp <= 0) this.downPlayer(p);
    }
  }

  updateEncounters() {
    // Checkpoints
    for (let i = this.checkpoint + 1; i < CHECKPOINTS.length; i++) {
      const cp = CHECKPOINTS[i];
      if (this.players.some(p => p.state !== 'dead' && p.x >= cp.x - 0.5 && p.y >= cp.y - 0.5 && p.onGround)) {
        if (i === 1 || this.arena.state === 'cleared' || i > 2) { this.checkpoint = i; this.emit('checkpoint', { i }); }
      }
    }
    // Concourse Lock
    const n = Math.max(1, this.players.length);
    const A = this.arena;
    if (A.state === 'idle' && this.players.some(p => p.state !== 'dead' && p.x > ARENA_TRIGGER_X && p.x < 96)) {
      GATES.L = true; GATES.R = true; A.state = 'wave1';
      for (const p of this.players) if (p.x < 63) { p.x = 64 + p.slot * 0.8; p.y = 0; p.vx = 0; p.vy = 0; }
      const sp = [createEnemy('shield', 88, 0), createEnemy('shield', 92, 0), createEnemy('sniper', 94.1, 5.4)];
      if (n >= 3) { sp.push(createEnemy('shield', 71, 0)); sp.push(createEnemy('sniper', 64.9, 5.4, { facing: 1 })); }
      for (const e of sp) { e.zone = 'arena'; this.enemies.push(e); }
      this.emit('banner', { text: 'Concourse Lock', sub: 'Gate sealed. Break the lock.' });
      this.emit('gates', { closed: true });
    } else if (A.state === 'wave1') {
      const alive = this.enemies.filter(e => e.zone === 'arena' && !e.dead).length;
      if (alive <= 1) {
        A.state = 'wave2';
        const count = n === 1 ? 3 : n === 2 ? 4 : 6;
        for (let i = 0; i < count; i++) {
          const e = createEnemy('swarmer', i % 2 ? 66 : 94, 0); e.zone = 'arena'; e.cd = 20 + i * 12; this.enemies.push(e);
        }
        const b = createEnemy('brute', 90, 0); b.zone = 'arena'; this.enemies.push(b);
        this.emit('banner', { text: 'Wave 2', sub: 'The Brute holds the lock.' });
      }
    } else if (A.state === 'wave2') {
      if (!this.enemies.some(e => e.zone === 'arena' && !e.dead)) this.startWarden();
    } else if (A.state === 'bossReady') {
      // After a wipe in the boss fight, walking back in goes straight to the boss
      if (this.players.some(p => p.state !== 'dead' && p.x > ARENA_TRIGGER_X && p.x < 96)) {
        GATES.L = true; GATES.R = true; this.emit('gates', { closed: true });
        for (const p of this.players) if (p.x < 63) { p.x = 64 + p.slot * 0.8; p.y = 0; p.vx = 0; p.vy = 0; }
        this.startWarden();
      }
    } else if (A.state === 'boss') {
      if (!this.enemies.some(e => e.zone === 'arena' && !e.dead)) {
        A.state = 'cleared'; GATES.L = false; GATES.R = false;
        this.emit('banner', { text: 'Lockwarden destroyed', sub: 'Gates open. Storm Spire climb ahead.' });
        this.emit('gates', { closed: false });
        const talker = this.activePlayers()[Math.floor(Math.random() * Math.max(1, this.activePlayers().length))];
        this.bark(talker, 'lock_broken', 1, true);
      }
    }
    this.updateSkyline(n);
    // Storm Spire climb enemies
    if (!this.towerSpawned && this.players.some(p => p.x > TOWER_TRIGGER_X && p.x < 162)) {
      this.towerSpawned = true;
      for (const [t, x, y] of [['swarmer', 122, 5.2], ['swarmer', 134, 11.2], ['drone', 129, 12], ['shield', 152, 15.6], ['drone', 147, 19.5], ['sniper', 158, 15.6]]) {
        const e = createEnemy(t, x, y); e.zone = 'tower'; this.enemies.push(e);
      }
    }
  }
}

// Distance from a point to a beam segment, and whether a thick segment touches a box
function distToSeg(x, y, g) {
  const vx = g.x1 - g.x0, vy = g.y1 - g.y0, L = vx * vx + vy * vy;
  const t = L > 1e-9 ? Math.max(0, Math.min(1, ((x - g.x0) * vx + (y - g.y0) * vy) / L)) : 0;
  return Math.hypot(x - (g.x0 + vx * t), y - (g.y0 + vy * t));
}
function segHitsBox(g, b, w) {
  const vx = g.x1 - g.x0, vy = g.y1 - g.y0, L = Math.hypot(vx, vy);
  if (L < 1e-6) return false;
  const h = rayBoxT(g.x0, g.y0, vx / L, vy / L, b.x0 - w, b.y0 - w, b.x1 + w, b.y1 + w);
  return !!h && h.t <= L;
}

// Skyline Relay: data-driven encounters (level.js ENCOUNTERS)
World.prototype.updateSkyline = function (n) {
  // (a trigger counts only for players on that encounter's route: the routes share one long x axis)
  const here = (x0, route = 'skyport') => this.players.some(p => p.state !== 'dead' && p.state !== 'downed' && p.x > x0 && routeAt(p.x).id === route);
  for (const S of this.encounters) {
    const E = S.def;
    if (S.state === 'idle') {
      if (!here(E.trigger, E.route)) continue;
      S.state = 'active'; S.wave = 0;
      if (E.boss) S.boss = spawnBoss(this, E.boss, E.bossAt[0], E.bossAt[1], { zone: 'skyline', enc: E.id });
      else this.spawnWave(S, n);
      if (E.gates) {
        for (const g of E.gates) GATES[g] = true;
        // Anyone still outside the gate is brought in, as in the Concourse Lock
        for (const p of this.players) if (p.x < E.inside - 1) { p.x = E.inside + p.slot * 0.8; p.y = groundBelow(p.x, 40); p.vx = 0; p.vy = 0; p.prevX = p.x; p.prevY = p.y; }
        this.emit('gates', { closed: true });
      }
      this.emit('banner', { text: E.banner[0], sub: E.banner[1] });
    } else if (S.state === 'active') {
      if (E.boss && S.boss && S.boss.dead && S.state === 'active') {
        // The boss is down: its drones go with it
        for (const e of this.enemies) if (e.enc === E.id && !e.dead && e.add) { e.hp = 0; e.dead = true; e.deathT = 0; this.emit('kill', { x: e.x, y: e.y + e.h / 2, e, owner: null }); }
      }
      const alive = this.enemies.filter(e => e.enc === E.id && !e.dead).length;
      const last = E.boss ? true : S.wave >= E.waves.length - 1;
      if (!last && alive <= 1) {
        S.wave++; this.spawnWave(S, n);
        const b = E.waveBanners && E.waveBanners[S.wave];
        if (b) this.emit('banner', { text: b[0], sub: b[1] });
      } else if (last && alive === 0) {
        S.state = 'cleared'; if (E.boss) this.bossClearedT = this.tick;
        if (E.gates) { for (const g of E.gates) GATES[g] = false; this.emit('gates', { closed: false }); }
        if (E.cleared) {
          this.emit('banner', { text: E.cleared[0], sub: E.cleared[1] });
          const act = this.activePlayers();
          this.bark(act[Math.floor(Math.random() * Math.max(1, act.length))], 'lock_broken', 1, true);
        }
      }
    }
  }
  // A route completes once every encounter on it is won and someone reaches its end (a few seconds after a boss
  // falls, so the banners don't collide)
  this.routesDone = this.routesDone || {};
  for (const R of ROUTES) {
    if (this.routesDone[R.id] || !here(R.endX, R.id) || this.tick - (this.bossClearedT ?? -1e9) <= 150) continue;
    if (!this.encounters.every(S => S.def.route !== R.id || S.state === 'cleared')) continue;
    this.routesDone[R.id] = true; if (R.id === 'skyport') this.routeDone = true;
    this.emit('banner', { text: 'Route complete', sub: R.id === 'skyport' ? 'You reached the end of the Skyport route.' : `${R.name} cleared.` });
  }
};

World.prototype.spawnWave = function (S, n) {
  const E = S.def, list = [...E.waves[S.wave], ...(S.wave === 0 && n >= 3 && E.extra ? E.extra : [])];
  list.forEach(([type, x, y], i) => {
    const e = createEnemy(type, x, y, { zone: 'skyline', enc: E.id, cd: 40 + i * 14 });
    this.enemies.push(e);
  });
};

function makeDirector(world) {
  const used = { melee: new Set(), ranged: new Set() };
  return {
    cap(pool) {
      const n = Math.max(1, world.players.filter(p => p.state !== 'dead').length);
      const d = (DIFFICULTY[SETTINGS.difficulty] || DIFFICULTY.normal).tokens;
      // Flare's cost: one more enemy may commit to a melee attack at a time
      const flare = world.players.some(p => p.char === 'echo' && p.scarfMode === 'flare' && p.state !== 'dead' && p.state !== 'downed') ? 1 : 0;
      return pool === 'melee' ? Math.max(1, 2 + (n - 1) + d + flare) : n >= 3 ? 2 : 1;
    },
    request(e, pool) {
      if (e.token) return true;
      if (used[pool].size < this.cap(pool)) { used[pool].add(e); e.token = pool; return true; }
      return false;
    },
    release(e) { if (e.token) { used[e.token].delete(e); e.token = null; } },
    reset() { used.melee.clear(); used.ranged.clear(); },
    usage() { return { melee: used.melee.size, ranged: used.ranged.size, meleeCap: this.cap('melee'), rangedCap: this.cap('ranged') }; },
  };
}
