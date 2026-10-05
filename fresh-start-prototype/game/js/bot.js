// AI teammates. Settings: AI teammates fills the empty slots of the team with computer-controlled players, and
// AI skill sets how sharp they are. Each one produces the same command a gamepad would (stick, held/pressed/
// released buttons, free aim) once a tick, so the simulation treats it exactly like a person: same moves, same
// cooldowns, same rules.
//
// How a bot thinks, every tick:
//   1. Safety first: a downed teammate is gone to (Fix revives with her beam from range); an enemy shockwave
//      coming along the floor is jumped; a mortar shell's landing zone is left.
//   2. Who to fight (re-chosen a few times a second, sticking with its choice unless a better one appears):
//      the player's lock-on target (focus fire), an enemy winding up on a teammate (peel), one nearly dead
//      (finish it), and what its role is good at (Nova and Fix take fliers, snipers and mortars; Echo and RAM
//      what they can reach; armour goes to those who break it). Ranged bots skip what they can't see.
//   3. Where to stand, by role: RAM between the team and the nearest enemy, shield toward it; Echo on the far
//      side of its target (a flank); Nova at range, backing off anything that gets close (kiting); Fix behind
//      the team near whoever is hurt. Bots spread out rather than stack, and keep up with the player they
//      follow (jumping walls and gaps, climbing tall walls, catching up if stuck).
//   4. What to use, by character:
//      Nova  shots charged to the level the target deserves; attachment for the job (Lance for armour and
//            bosses, Arc for crowds, Volley for fliers); the Level 4 beam down a line of enemies; bracer combo
//            up close; dodges wind-ups and shots
//      Echo  blade combos, dash-ins, parries and staff deflects; snares on chargers and heavies coming for the
//            team (on fire or LB, per Settings: Echo's utility belt); the rifle at range
//      RAM   guards wind-ups and shots aimed at him or at the team behind him, lets the stored Kinetic go, Ram
//            Charge into a group, Breach Cannon, the Breach Beam (Level 4) down a line, Bulwark Wall against a
//            barrage, Provoke a crowd, Guardian Link a teammate in trouble
//      Fix   Patch Beam whenever someone is hurt or down; picks the gadget the fight needs (Patch Pylon when the
//            team is hurt, Sentry otherwise) and builds it; rivets and wrench in between
//   Everyone uses their ultimate when it is ready and enemies are close, and joins a teammate's team ultimate.
// Skill (BOT.skill) sets how often and how fast a bot answers a wind-up or shot, how far it reads the fight,
// and whether it uses the advanced plays (beams, walls, snares, kiting).
//
// Team commands (any person can give them; input.js: D-pad up tap/hold, down tap/hold, or Z/G/X/C):
//   attack   Attack my target: every bot goes for the commander's lock-on target (or the enemy nearest them)
//            wherever it is, until it falls
//   cover    Cover me: the bots stay at the commander's side: RAM in front with his shield toward the enemy,
//            Fix with her beam on them, Nova and Echo on whatever goes for them
//   regroup  Regroup on me: the bots come in tight around the commander, fighting only what is on top of them,
//            for ORDERS.regroup ticks; then they go back to following as usual
//   hold     Hold here: the bots stand their ground around where the commander stood, fighting what comes
//            within ORDERS.holdRange of it, until another command
// A new command replaces the last one; giving the same one again cancels it (back to following).
import { ULT, RAM, FIX, ROSTER, MARKSMAN, HUNTER, SETTINGS } from './config.js';
import { groundBelow, hasHeadroom, segmentBlocked } from './level.js';

const BTNS = ['jump', 'dash', 'melee', 'fire', 'parry', 'sig', 'mode', 'lock', 'sub', 'ult'];
export const BOT = {
  sight: 11,         // m: enemies this close are fought
  leash: 9,          // m: ...unless the bot is this far from the player it follows (it catches up first)
  follow: 1.6,       // m: where it stands behind the player it follows (more for each further bot)
  reviveRange: 22,   // m: a downed teammate this close is gone to first
  range: { nova: 6.5, echo: 1.3, ram: 1.5, fix: 5.5 },   // m it likes to fight at
  // Settings: AI skill. react = chance of answering a given wind-up or shot; delay = ticks before it does;
  // retarget = ticks between choosing targets; smart = uses the advanced plays (beams, walls, snares, kiting,
  // peeling for teammates, leaving mortar zones)
  skill: {
    rookie:  { react: 0.3, delay: 14, retarget: 20, smart: false },
    veteran: { react: 0.6, delay: 8, retarget: 12, smart: true },
    elite:   { react: 0.9, delay: 4, retarget: 6, smart: true },
  },
};

export const isBot = p => typeof p.device === 'string' && p.device.startsWith('cpu');

const sign = v => (v > 0 ? 1 : v < 0 ? -1 : 0);
const alive = e => !e.dead && e.hp > 0;
const up = q => q.state !== 'downed' && q.state !== 'dead';
const RANGED_FOES = new Set(['sniper', 'mortar', 'turret', 'drone']);
export const ORDERS = {
  names: { attack: 'Attack my target', cover: 'Cover me', regroup: 'Regroup on me', hold: 'Hold here' },
  regroup: 360, holdRange: 7, coverRange: 6,
  // Each character's answer
  lines: {
    nova: { attack: 'Marking it. Going in.', cover: 'I have your back.', regroup: 'Coming to you.', hold: 'Holding this line.', done: 'Target down.' },
    echo: { attack: 'It is mine.', cover: 'Nothing gets near you.', regroup: 'On my way.', hold: 'I will hold.', done: 'Done.' },
    ram: { attack: 'Plowing through!', cover: 'Behind me. Stay there.', regroup: 'Falling back to you.', hold: 'They will not pass.', done: 'Down it goes.' },
    fix: { attack: 'Rivets out!', cover: 'Beam on you, stay close.', regroup: 'Right behind you!', hold: 'Setting up here.', done: 'Scrap it!' },
  },
};

export class Bots {
  constructor() { this.mem = new Map(); this.order = null; }

  // A team command from player `by`: returns each bot's answer as [bot, line] (empty if there are no bots)
  issue(world, by, type) {
    const bots = world.players.filter(isBot);
    if (!bots.length || !ORDERS.names[type]) return [];
    if (this.order && this.order.type === type && this.order.by === by && type !== 'attack') { this.order = null; return bots.map(b => [b, 'Back to following.']); }
    let target = null;
    if (type === 'attack') {
      target = by.lockT && alive(by.lockT) ? by.lockT : null;
      if (!target) { let bd = BOT.sight + 6; for (const e of world.enemies) { if (!alive(e)) continue; const d = Math.hypot(e.x - by.x, e.y - by.y); if (d < bd) { bd = d; target = e; } } }
      if (!target) return bots.map(b => [b, 'No target in sight.']);
    }
    this.order = { type, by, target, x: by.x, y: by.y, t: world.tick };
    for (const b of bots) { const M = this.mem.get(b); if (M) { M.retarget = 0; M.hold = 0; } }
    return bots.map(b => [b, ORDERS.lines[b.char][type]]);
  }
  // The command in force (it lapses when its commander leaves, its target falls, or a regroup has run its time);
  // `done` is set once when an attack order's target falls
  activeOrder(world) {
    const O = this.order; if (!O) return null;
    if (!world.players.includes(O.by)) { this.order = null; return null; }
    if (O.type === 'attack' && !(O.target && alive(O.target) && world.enemies.includes(O.target))) { this.order = null; this.done = world.tick; return null; }
    if (O.type === 'regroup' && world.tick - O.t > ORDERS.regroup) { this.order = null; return null; }
    return O;
  }

  // Keep the number of AI players at `want`, never more than the slots the human players leave free. They
  // take the characters no one is playing (Nova, Echo, RAM, Fix order), and are only added once a person has.
  sync(world, want) {
    const humans = world.players.filter(p => !isBot(p)), bots = world.players.filter(isBot);
    const allowed = humans.length ? Math.max(0, Math.min(want, 4 - humans.length)) : 0;
    for (let i = bots.length - 1; i >= allowed; i--) { world.removePlayer(bots[i].slot); this.mem.delete(bots[i]); }
    for (let n = Math.min(bots.length, allowed); n < allowed; n++) {
      const used = new Set(world.players.map(p => p.char)), char = ROSTER.find(c => !used.has(c)) || ROSTER[n % ROSTER.length];
      let id = 1; const devs = new Set(world.players.map(p => p.device)); while (devs.has('cpu' + id)) id++;
      world.addPlayer('cpu' + id, char);
    }
  }
  // Free a slot for a person joining a full team: the last AI player leaves
  makeRoom(world) {
    const bots = world.players.filter(isBot);
    if (world.players.length < 4 || !bots.length) return false;
    const b = bots[bots.length - 1]; world.removePlayer(b.slot); this.mem.delete(b); return true;
  }

  // The commands for every AI player this tick, added to `cmds` (by slot)
  commands(world, cmds) {
    for (const p of world.players) if (isBot(p)) cmds[p.slot] = this.command(world, p);
    for (const p of this.mem.keys()) if (!world.players.includes(p)) this.mem.delete(p);
    return cmds;
  }

  command(world, p) {
    let M = this.mem.get(p);
    if (!M) { M = { prev: {}, jumpT: 0, fireT: 0, hold: 0, guardT: 0, react: new Map(), seed: (p.slot + 1) * 7919, t: 0, target: null, retarget: 0, gadgetT: 240, snareT: 0, modeT: 0 }; this.mem.set(p, M); }
    M.t++;
    const S = BOT.skill[SETTINGS.aiSkill] || BOT.skill.veteran;
    const rnd = () => ((M.seed = (M.seed * 16807) % 2147483647) / 2147483647);
    const held = {}; let mx = 0, my = 0, aimFree = false, ax = 0, ay = 0;
    const out = () => {
      const pressed = {}, released = {};
      for (const b of BTNS) { held[b] = !!held[b]; pressed[b] = held[b] && !M.prev[b]; released[b] = !held[b] && !!M.prev[b]; }
      M.prev = { ...held };
      return { mx, my, aimFree, ax, ay, held, pressed, released };
    };
    if (p.state === 'downed' || p.state === 'dead' || p.state === 'ult') { M.hold = 0; return out(); }

    const team = world.players.filter(q => q !== p);
    const O = this.activeOrder(world);
    // Autoplay (explore): with no person playing, the first AI player up takes the lead and pushes on through the
    // level, and the others follow it
    const point = this.explore && !world.players.some(q => !isBot(q) && up(q)) ? world.players.find(q => isBot(q) && up(q)) : null;
    const leader = (O && O.by !== p && up(O.by) ? O.by : null) || team.find(q => !isBot(q) && up(q)) || (point ? (point !== p ? point : null) : team.find(up)) || null;
    const bots = world.players.filter(isBot), order = bots.indexOf(p);
    const cx = p.x, cy = p.y + p.h / 2;
    const dist = e => Math.hypot(e.x - cx, e.y + e.h / 2 - cy);
    let foes = world.enemies.filter(e => alive(e) && e.state !== 'plowed' && dist(e) < BOT.sight + 4);
    // (in autoplay the training props of the Movement Gym, which can't be destroyed, are left alone)
    if (this.explore) foes = foes.filter(e => e.hp !== Infinity);
    // Commands narrow what it fights: a regroup only what is on top of it; a hold only what comes near the spot
    if (O && O.type === 'regroup') foes = foes.filter(e => dist(e) < 3.5);
    if (O && O.type === 'hold') foes = foes.filter(e => Math.abs(e.x - O.x) < ORDERS.holdRange || ((p.char === 'nova' || p.char === 'fix') && dist(e) < BOT.sight && Math.abs(e.x - O.x) < BOT.sight));

    // ---- Join a teammate's team ultimate ----
    if (world.ultCast && !world.ultCast.members.includes(p) && p.ult >= ULT.max) { held.ult = (M.t % 4) < 2; return out(); }

    // ---- 2. Who to fight ----
    if (O && O.type === 'attack') { M.target = O.target; M.retarget = S.retarget; }
    else if (--M.retarget <= 0 || !M.target || !alive(M.target) || !foes.includes(M.target)) {
      M.retarget = S.retarget;
      let best = null, bs = Infinity;
      for (const e of foes) {
        let s = this.score(world, p, e, dist(e), leader, team, M, S);
        if (O && O.type === 'cover') s += (e.target === O.by ? -4 : 0) + Math.abs(e.x - O.by.x) * 0.5;   // what threatens the commander
        if (s < bs) { bs = s; best = e; }
      }
      M.target = best;
    }
    const attackOrder = O && O.type === 'attack';
    let tgt = M.target && alive(M.target) && (attackOrder || dist(M.target) < BOT.sight + 2) ? M.target : null;
    const farFromLeader = !attackOrder && leader && Math.abs(leader.x - p.x) > BOT.leash;
    if (farFromLeader && tgt && Math.abs(tgt.x - leader.x) > BOT.leash) tgt = null;   // catch up first

    // ---- 3. Where to stand ----
    const downed = team.filter(q => q.state === 'downed' && Math.abs(q.x - p.x) < BOT.reviveRange).sort((a, b) => Math.abs(a.x - p.x) - Math.abs(b.x - p.x))[0];
    let goal = p.x, stopAt = 0.5, goalY = p.y;
    if (downed && !(p.char === 'fix' && Math.abs(downed.x - p.x) < FIX.beam.range - 1)) { goal = downed.x; goalY = downed.y; stopAt = 0.6; }
    else if (tgt && !farFromLeader) {
      [goal, stopAt] = this.post(world, p, tgt, foes, leader, team, S, dist);
      goalY = tgt.y;
    } else if (leader) {
      goal = leader.x - (leader.facing || 1) * BOT.follow * (1 + order * 0.7); goalY = leader.y; stopAt = 0.8;
      // Climbing after them (a stair of platforms): aim for the platform they are on, not the gap behind it
      if (leader.y > p.y + 1.5 && leader.onGround) { goal = leader.x - (leader.facing || 1) * 0.4 * order; stopAt = 0.3; }
    } else if (point === p) { goal = p.x + 6; goalY = p.y; stopAt = 0.3; }   // (the routes all run toward +x)
    // Commands move where it stands
    if (O && !downed) {
      const near = foes.reduce((a, e) => (!a || dist(e) < dist(a) ? e : a), null);
      if (O.type === 'regroup' && leader) { goal = leader.x - (leader.facing || 1) * (0.9 + order * 0.8); goalY = leader.y; stopAt = 0.5; }
      else if (O.type === 'hold') {
        const spot = O.x + (order - (bots.length - 1) / 2) * 1.3;
        const melee = p.char === 'ram' || p.char === 'echo';
        if (!(tgt && melee && Math.abs(tgt.x - O.x) < ORDERS.holdRange)) { goal = spot; goalY = O.y; stopAt = 0.5; }
      } else if (O.type === 'cover' && leader) {
        const threatX = near ? near.x : leader.x + (leader.facing || 1) * 3, side = sign(threatX - leader.x) || leader.facing || 1;
        if (p.char === 'ram') { goal = leader.x + side * 1.4; stopAt = 0.4; }                                  // in front, shield up
        else if (p.char === 'echo' && tgt && Math.abs(tgt.x - leader.x) < 4) { /* free to cut it down */ }
        else { goal = leader.x - side * (1.2 + order * 0.7); stopAt = 0.6; }                                    // close, behind
        goalY = leader.y;
      }
    }
    // Patched up on the way: a Medkit or Plating within reach when it is hurt (the other power-ups are left for
    // the players)
    if (!tgt && !downed && !(O && O.type === 'hold') && p.hp < p.maxHp * 0.7) {
      const pk = (world.pickups || []).find(k => (k.kind === 'medkit' || k.kind === 'plating') && !k.target && Math.abs(k.x - p.x) < 7 && Math.abs(k.y - p.y) < 2.5);
      if (pk) { goal = pk.x; goalY = pk.y; stopAt = 0.2; }
    }
    // spread out: don't stand where another bot already is
    for (const q of bots) if (q !== p && up(q) && bots.indexOf(q) < order && Math.abs(q.x - goal) < 0.8 && Math.abs(q.y - p.y) < 1.5) goal += (goal >= q.x ? 1 : -1) * 0.9;
    // ---- 1. Hazards: leave a mortar's landing zone ----
    const shell = S.smart ? this.mortarZone(world, p) : null;
    if (shell !== null) { goal = p.x + (p.x >= shell ? 1 : -1) * 3; stopAt = 0.2; }
    const dx = goal - p.x;
    if (Math.abs(dx) > stopAt) mx = sign(dx);

    // ---- Platforming: jump walls, gaps and up to where the goal is; never walk off a ledge the goal isn't past ----
    const dir = mx || p.facing;
    let wall = mx && !hasHeadroom(p.x + dir * 0.45, p.y + 0.3, p.w, Math.max(0.6, p.h - 0.4));
    // A wall far taller than where the goal is, with the goal just past it (a level's end wall, a pillar to stand
    // behind): there is nothing to climb to, so it waits on this side instead
    if (wall && Math.abs(dx) < 6) {
      let top = p.y; while (top < p.y + 30 && !hasHeadroom(p.x + dir * 0.6, top, p.w * 0.5, 1)) top += 1;
      if (top > goalY + 3.5) { mx = 0; wall = false; }
    }
    const floorAhead = groundBelow(p.x + dir * (p.w / 2 + 0.6), p.y + 0.2);
    const gap = mx && p.onGround && floorAhead < p.y - 1.2;
    const goalPastGap = Math.abs(dx) > 2.2;
    if (gap && !goalPastGap && goalY >= p.y - 0.5) mx = 0;           // don't step off for nothing
    const wantUp = goalY > p.y + 1.1 && Math.abs(dx) < 5;
    // an enemy shockwave running along the floor at us: hop it
    const wave = p.onGround && world.shockwaves.some(w => w.team !== 'p' && Math.abs(w.y - p.y) < 0.6 && sign(p.x - w.x) === sign(w.dir) && Math.abs(p.x - w.x) < 2.4)
      && this.answer(world, M, 'wave' + Math.floor(world.tick / 30), S, rnd, true);
    // a wall in the way while airborne: climb kicks (jump while holding toward it) take it up a tall wall
    const climb = !p.onGround && p.wallDir !== 0 && p.wallDir === dir && (wall || goalY > p.y + 0.5);
    // (a jump only starts on a fresh press, so each one begins by letting go of the button if it is held)
    if (M.jumpT <= 0 && p.onGround && (wave || wall || (gap && goalPastGap && goalY >= p.y - 0.5) || wantUp)) { M.jumpT = 14; M.jumpNew = true; }
    else if (M.jumpT <= 0 && !p.onGround && (climb ? p.vy < 3 : p.vy < 1 && p.jumpsUsed < 1 && (wall || (goalY > p.y + 0.8 && Math.abs(dx) < 4) || (floorAhead < p.y - 3 && goalPastGap)))) { M.jumpT = 10; M.jumpNew = true; }
    if (M.jumpT > 0) {
      if (M.jumpNew && M.prev.jump) held.jump = false;
      else { held.jump = true; M.jumpNew = false; M.jumpT--; }
    }
    // Stuck (no headway toward a goal over 3 m away for about 3 s, out of a fight): catch up with the team the
    // way a player left off screen does (no penalty)
    if (M.t % 60 === 0) {
      // (stuck below them counts too: a fall off a climb, with no way back up from here)
      const below = goalY > p.y + 3, noHeadway = Math.abs(p.x - (M.lastX ?? p.x + 9)) < 0.6 && p.y < (M.lastY ?? -1e9) + 1;
      M.stuck = !tgt && (Math.abs(dx) > 3 || below) && noHeadway ? (M.stuck || 0) + 1 : 0; M.lastX = p.x; M.lastY = p.y;
      if (M.stuck >= 3) { M.stuck = 0; world.recall(p, false); return out(); }
    }

    if (!tgt) { M.fireT = 0; M.hold = 0; this.support(world, p, M, held, team, S, rnd); return out(); }

    // ---- 4. Fighting ----
    const d = dist(tgt), tx = tgt.x - p.x, ty = (tgt.y + tgt.h / 2) - cy, len = Math.hypot(tx, ty) || 1;
    aimFree = true; ax = tx / len; ay = ty / len;
    if (!mx && Math.abs(tx) > 0.2 && sign(tx) !== p.facing) mx = sign(tx) * 0.3;   // turn to face it
    if (!p.onGround) ay = Math.max(ay, -0.3);                                       // (aiming hard down in the air is a ground pound)
    const seen = !segmentBlocked(cx, cy, tgt.x, tgt.y + tgt.h / 2);
    const threat = this.threat(world, p, M, S, rnd);
    const close = Math.abs(tx) < 2.0 + tgt.w / 2 && Math.abs(ty) < 1.6;
    const crowd = foes.filter(e => dist(e) < 6).length;
    const inLine = this.inLine(foes, cx, cy, ax, ay);
    const nearest = foes.reduce((m, e) => Math.min(m, dist(e)), Infinity);
    const armoured = tgt.armor > 0 || tgt.boss;

    switch (p.char) {
      case 'nova': {
        // attachment for the job (Marksman kit)
        if (S.smart && p.attachment && MARKSMAN.attachments.includes(p.attachment) && p.modeCd === 0 && --M.modeT <= 0) {
          const want = armoured ? 'lance' : crowd >= 3 ? 'arc' : tgt.flier ? 'volley' : p.attachment;
          if (want !== p.attachment) { held.mode = true; M.modeT = 20; }
        }
        if (threat) { held.parry = true; mx = -sign(tx) || -p.facing; M.hold = 0; }          // dodge away
        else if (close && !(M.beam && M.hold > 0 && nearest > 1.6)) { aimFree = false; held.melee = (M.t % 9) < 3; M.hold = 0; }
        else if (seen) {
          // the Level 4 beam down a line of enemies (or a boss), when nothing is on top of her
          const beam = this.beamPlan(M, S, inLine, tgt, nearest, 3) && p.attachment;
          held.fire = this.charge(M, p, beam ? MARKSMAN.beam.at + 2 : armoured ? MARKSMAN.charge[2] + 2 : crowd >= 2 ? MARKSMAN.charge[1] + 2 : MARKSMAN.charge[0] + 2);
          if (beam) mx = 0;   // braced while it charges
        } else M.hold = 0;
        break;
      }
      case 'echo': {
        if (threat) { held.parry = (M.t % 3) === 0; M.hold = 0; }                         // parry / deflect
        else if (close) { aimFree = false; held.melee = (M.t % 8) < 3; M.hold = 0; }
        else if (d > 3 && d < 6.5 && p.onGround && Math.abs(ty) < 1 && (M.t % 50) === 0) { held.dash = true; mx = sign(tx); }
        else if (d >= 6.5 && seen) held.fire = this.charge(M, p, HUNTER.rifle.raise + (armoured ? HUNTER.rifle.focus : 20));
        // snares on a charger or heavy coming for the team
        if (S.smart && SETTINGS.echoKit === 'hunter' && p.snares > 0 && --M.snareT <= 0) {
          const rusher = foes.find(e => (e.type === 'charger' || e.type === 'brute' || e.type === 'shield') && dist(e) < 7 && dist(e) > 1.5 && sign(e.x - p.x) === p.facing);
          if (rusher) {
            M.snareT = 150; M.hold = 0;
            if (SETTINGS.echoBelt === 'lb') held.sub = true; else { held.fire = !M.prev.fire; }   // a tap
          }
        }
        break;
      }
      case 'ram': {
        const guarding = p.state === 'guard';
        const cover = (S.smart || (O && O.type === 'cover')) && this.coverNeeded(world, p, team);
        if (threat || cover || (guarding && M.guardT > 0)) {
          held.parry = true; aimFree = true; ax = sign(tx) || p.facing; ay = 0; M.hold = 0;
          if (threat || cover) M.guardT = 24; M.guardT--;
          if (p.kinetic >= 45 && d < 5) held.fire = (M.t % 6) < 2;                         // let the Kinetic go
        } else if (close && !(M.beam && M.hold > 0 && nearest > 1.6)) { aimFree = false; held.melee = (M.t % 12) < 3; M.hold = 0; }
        else if (M.beam && M.hold > 0) { const b = this.beamPlan(M, S, inLine, tgt, nearest, 1.6); held.fire = this.charge(M, p, b ? RAM.beam.at + 2 : 1); mx = 0; }
        else if (d > 3 && d < 8 && p.onGround && Math.abs(ty) < 0.8 && (M.t % 70) === 0 && !p.rush && crowd >= 1 && !armoured) { held.dash = true; mx = sign(tx); M.hold = 0; }
        else if (seen && d < 12) {
          const beam = this.beamPlan(M, S, inLine, tgt, nearest, 2.5);
          held.fire = this.charge(M, p, beam ? RAM.beam.at + 2 : armoured ? RAM.cannon.charge[1] + 2 : RAM.cannon.charge[0] + 2);
          if (beam) mx = 0;   // braced while it charges
        } else M.hold = 0;
        const hurt = team.find(q => up(q) && q.hp < q.maxHp * 0.45 && Math.abs(q.x - p.x) < RAM.link.range);
        const barrage = world.projectiles.filter(pr => pr.team === 'e' && !pr.dead && Math.abs(pr.x - p.x) < 10).length + foes.filter(e => RANGED_FOES.has(e.type)).length;
        if (crowd >= 3 && p.provokeCd === 0 && (M.t % 30) === 0) held.sub = true;
        else if (hurt && p.linkCd === 0 && !p.link && (M.t % 30) === 15) held.mode = true;
        else if (S.smart && barrage >= 4 && p.wallCd === 0 && p.onGround && (M.t % 30) === 7) held.sig = true;
        break;
      }
      case 'fix': {
        if (this.patch(world, p, team)) { held.parry = true; aimFree = false; M.hold = 0; }
        else if (close) { aimFree = false; held.melee = (M.t % 9) < 3; M.hold = 0; }
        else if (seen) held.fire = this.charge(M, p, armoured ? FIX.rivet.charge[1] + 2 : 1);
        else M.hold = 0;
        this.gadget(world, p, M, team, crowd, held);
        break;
      }
    }
    // Ultimate: when it is full and there is something worth hitting close by
    if (p.ult >= ULT.max && !world.ultCast && d < 7 && (crowd >= 2 || tgt.boss || armoured) && (M.t % 20) === 0) held.ult = true;
    return out();
  }

  // How much a bot wants to fight `e` (lower is better)
  score(world, p, e, d, leader, team, M, S) {
    let s = d;
    const ranged = p.char === 'nova' || p.char === 'fix', vy = Math.abs((e.y + e.h / 2) - (p.y + p.h / 2));
    if (e === M.target) s -= 1;                                             // stick with it
    if (leader && leader.lockT === e) s -= 3.5;                             // focus fire with the player
    if (S.smart && e.target && e.target !== p && team.includes(e.target) && (e.state === 'windup' || e.state === 'aim' || e.state === 'lock')) s -= 3;   // peel for a teammate
    if (e.hp <= 3) s -= 1.5;                                                // finish it
    if (ranged && (e.flier || RANGED_FOES.has(e.type))) s -= 2;              // shooters take the shooters
    if (!ranged && (e.flier || vy > 2.6)) s += 4;                            // melee can't reach it
    if (e.armor > 0 && (p.char === 'echo' || p.char === 'fix')) s += 2;      // leave armour to the breakers
    if (ranged && segmentBlocked(p.x, p.y + p.h / 2, e.x, e.y + e.h / 2)) s += 3;   // can't see it
    return s;
  }

  // Where to stand against `tgt`, by role: [x, how close counts as there]
  post(world, p, tgt, foes, leader, team, S, dist) {
    const R = BOT.range[p.char], side = sign(p.x - tgt.x) || -tgt.facing || 1, ref = leader || p;
    switch (p.char) {
      case 'ram': {
        // between the team and the nearest enemy to it, shield toward it
        const front = foes.reduce((a, e) => (!a || Math.abs(e.x - ref.x) < Math.abs(a.x - ref.x) ? e : a), null) || tgt;
        const s = sign(ref.x - front.x) || side;
        return [front.x + s * (R + front.w / 2), 0.35];
      }
      case 'echo': {
        // a flank: the far side of the target from the team, if there is floor there
        if (S.smart && leader) {
          const s = sign(tgt.x - leader.x) || side, fx = tgt.x + s * (R + tgt.w / 2);
          if (Math.abs(groundBelow(fx, tgt.y + 0.5) - tgt.y) < 0.6) return [fx, 0.35];
        }
        return [tgt.x + side * (R + tgt.w / 2), 0.35];
      }
      case 'nova': {
        // at range on the team's side; anything that gets within 3 m is backed away from (kiting)
        const close = foes.find(e => dist(e) < 3);
        if (S.smart && close) return [p.x + (sign(p.x - close.x) || -close.facing || 1) * 3, 0.3];
        const s = leader ? sign(leader.x - tgt.x) || side : side;
        return [tgt.x + s * (R + tgt.w / 2), 1.2];
      }
      default: {   // fix: behind the team, near whoever is hurt most, well away from the fighting
        const hurt = [p, ...team].filter(up).sort((a, b) => a.hp / a.maxHp - b.hp / b.maxHp)[0];
        const anchor = hurt && hurt !== p && hurt.hp < hurt.maxHp * 0.85 ? hurt : ref;
        const s = sign(anchor.x - tgt.x) || side;
        let x = anchor.x + s * 2.5;
        if (Math.abs(x - tgt.x) < 4) x = tgt.x + s * 4.5;
        return [x, 1.0];
      }
    }
  }

  // Out of a fight: Fix keeps the team patched up (and builds a Pylon if they are hurt); RAM guards shots
  support(world, p, M, held, team, S, rnd) {
    if (p.char === 'fix') { if (this.patch(world, p, team)) held.parry = true; this.gadget(world, p, M, team, 0, held); }
    if (p.char === 'ram' && this.threat(world, p, M, S, rnd)) held.parry = true;
  }

  // Fix: someone in Patch Beam range is down or hurt (or she is, with no one else to see to); under Cover me
  // she keeps the beam on the commander at the first scratch
  patch(world, p, team) {
    const R = FIX.beam.range, O = this.order;
    if (O && O.type === 'cover' && up(O.by) && O.by !== p && O.by.hp < O.by.maxHp * 0.97 && Math.hypot(O.by.x - p.x, O.by.y - p.y) < R) return true;
    const need = team.some(q => (q.state === 'downed' || (up(q) && q.hp < q.maxHp * 0.75)) && Math.hypot(q.x - p.x, q.y - p.y) < R);
    return need || p.patch || (p.hp < p.maxHp * 0.5);
  }
  // Fix: pick the gadget the fight needs, then build it when there is Scrap to spare (one at a time)
  gadget(world, p, M, team, crowd, held) {
    const hurt = team.filter(q => up(q) && q.hp < q.maxHp * 0.6).length;
    const want = hurt >= 1 ? 'pylon' : crowd >= 1 ? 'sentry' : null;
    if (!want) return;
    if (p.gadgetSel !== want) { if (p.modeCd === 0 && (M.t % 12) === 0) held.mode = true; return; }
    const cost = FIX.gadget[want].cost;
    if (p.scrap >= Math.max(55, cost + 10) && --M.gadgetT <= 0 && !world.gadgets.some(g => g.owner === p && !g.dead && g.kind !== 'pad')) { held.sig = true; M.gadgetT = 480; }
  }

  // Holding fire to a charge level: keep holding until the charge reaches `to` ticks, then let go (a short
  // breath between shots)
  charge(M, p, to) {
    if (M.hold < 0) { M.hold++; return false; }
    if (M.hold === 0 && p.chargeT === 0 && (p.rifleT || 0) === 0) { M.hold = 1; return true; }
    M.hold++;
    const now = Math.max(p.chargeT || 0, p.rifleT || 0);
    if (now >= to || M.hold > to + 40) { M.hold = -6; return false; }
    return true;
  }
  // A Level 4 beam: begun when enemies line up (or for a boss) with none too close, then kept to (braced)
  // until it fires, unless something gets right on top of it
  beamPlan(M, S, inLine, tgt, nearest, room) {
    if (M.beam && M.hold > 0) { if (nearest < 1.6) M.beam = false; return M.beam; }
    M.beam = S.smart && (inLine >= 3 || !!tgt.boss) && nearest > room;
    return M.beam;
  }
  // How many enemies sit along the aim line (within a beam's width of it, ahead)
  inLine(foes, cx, cy, ax, ay) {
    let n = 0;
    for (const e of foes) {
      const rx = e.x - cx, ry = e.y + e.h / 2 - cy, along = rx * ax + ry * ay;
      if (along > 0 && along < 30 && Math.abs(rx * ay - ry * ax) < 0.9 + e.h / 3) n++;
    }
    return n;
  }
  // RAM: enemy shots heading at a teammate standing behind him
  coverNeeded(world, p, team) {
    for (const pr of world.projectiles) {
      if (pr.team !== 'e' || pr.dead || pr.kind === 'mortar') continue;
      for (const q of team) {
        if (!up(q) || Math.abs(q.x - p.x) > 4) continue;
        const behind = sign(q.x - p.x) === -sign(pr.vx) || Math.abs(q.x - p.x) < 0.6;
        const rx = q.x - pr.x, ry = q.y + q.h / 2 - pr.y, dd = Math.hypot(rx, ry);
        if (behind && dd < 7 && rx * pr.vx + ry * pr.vy > 0 && sign(pr.x - p.x) === sign(pr.x - q.x)) return true;
      }
    }
    return false;
  }
  // A mortar shell coming down within reach of us: where it lands (x), or null
  mortarZone(world, p) {
    for (const pr of world.projectiles) {
      if (pr.team !== 'e' || pr.dead || pr.kind !== 'mortar') continue;
      let x = pr.x, y = pr.y, vx = pr.vx, vy = pr.vy; const g = pr.gravity || 0, dt = 1 / 60;
      for (let i = 0; i < 120; i++) {
        vy -= g * dt; x += vx * dt; y += vy * dt;
        if (vy < 0 && y <= groundBelow(x, y + 0.5) + 0.1) break;
      }
      const r = (pr.blast && pr.blast.r) || 2;
      if (Math.abs(x - p.x) < r + 0.6 && Math.abs(y - p.y) < 2) return x;
    }
    return null;
  }

  // Answer a given threat or not: decided once per threat by chance (skill), and only after the skill's delay
  answer(world, M, key, S, rnd, now = false) {
    let r = M.react.get(key);
    if (!r) { r = { at: world.tick, yes: rnd() < S.react }; M.react.set(key, r); }
    return r.yes && (now || world.tick - r.at >= S.delay);
  }
  // An enemy winding up an attack on us, or an enemy shot that will reach us within a few ticks
  threat(world, p, M, S, rnd) {
    const cx = p.x, cy = p.y + p.h / 2;
    let seen = null;
    for (const e of world.enemies) {
      if (!alive(e) || e.state !== 'windup' || (e.target && e.target !== p)) continue;
      if (Math.abs(e.x - cx) < 3.2 && Math.abs(e.y - p.y) < 2) { seen = e; break; }
    }
    if (!seen) for (const pr of world.projectiles) {
      if (pr.team !== 'e' || pr.dead) continue;
      const rx = cx - pr.x, ry = cy - pr.y, d = Math.hypot(rx, ry);
      if (d > 5 || (rx * pr.vx + ry * pr.vy) <= 0) continue;
      const tHit = d / (Math.hypot(pr.vx, pr.vy) || 1) * 60;
      if (tHit < 22) { seen = pr; break; }
    }
    for (const [k, r] of M.react) if (world.tick - r.at > 90) M.react.delete(k);
    if (!seen) return false;
    return this.answer(world, M, seen, S, rnd);
  }
}
