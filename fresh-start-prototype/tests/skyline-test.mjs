// Skyline Relay: the route is traversable by both characters, and its encounters trigger, progress,
// seal and open the Relay Gate, and reset on a wipe. Headless, real simulation.
import { World } from '../game/js/world.js';
import { SETTINGS } from '../game/js/config.js';
import { GATES, pointInSolid, groundBelow, ENCOUNTERS, ROUTE_END_X } from '../game/js/level.js';
SETTINGS.novaKit = 'marksman'; SETTINGS.echoKit = 'hunter';
const BT = ['jump', 'dash', 'melee', 'fire', 'parry', 'sig', 'mode'];
function mk(prev, o = {}) {
  const held = {}; for (const b of BT) held[b] = !!(o.held && o.held[b]);
  const pressed = {}, released = {};
  for (const b of BT) { pressed[b] = held[b] && !prev.held[b]; released[b] = !held[b] && !!prev.held[b]; }
  return { mx: o.mx || 0, my: o.my || 0, aimFree: false, ax: 0, ay: 0, held, pressed, released };
}
const assert = (c, m) => { console.log((c ? 'PASS ' : 'FAIL ') + m); if (!c) process.exitCode = 1; };
const count = (log, t, f = () => true) => log.filter(e => e.type === t && f(e)).length;
function setup(char, zone = 'skyline') {
  const w = new World(); w.enemies = [];
  const p = w.addPlayer('test', char);
  w.teleport(zone);
  let prev = { held: {} }; const log = [];
  const run = (o = {}, n = 1) => { for (let i = 0; i < n; i++) { const c = mk(prev, o); prev = c; w.step({ 0: c }); log.push(...w.events); w.events.length = 0; } };
  run({}, 5);
  return { w, p, run, log };
}
const killAll = (w, p, enc) => w.enemies.filter(e => !e.dead && (!enc || e.enc === enc)).forEach(e => {
  e.hp = 0.001; e.armor = 0;
  w.spawnHitbox({ owner: p, team: 'p', x0: e.x - 1, x1: e.x + 1, y0: e.y - 1, y1: e.y + 3, dmg: 99, poise: 0, kb: [0, 0], armorBreak: true, instance: w.newInstance() });
});

// A simple runner: hold right; jump at a ledge edge or a wall; double jump near the top of the jump
function runRoute(char) {
  const { w, p, run, log } = setup(char);
  for (const S of w.encounters) S.state = 'cleared';   // traversal only: no fights
  let jumpHold = 0, doubled = false, falls = 0, t = 0;
  for (; t < 60 * 60 && p.x < ROUTE_END_X + 6; t++) {
    // (probed across the width of the body: in 3D a crate can stand to one side of the path line)
    const wall = [-0.35, 0, 0.35].some(dz => pointInSolid(p.x + 0.9, p.y + 0.4, p.z + dz) || pointInSolid(p.x + 0.9, p.y + 1.4, p.z + dz));
    const gap = p.onGround && groundBelow(p.x + 1.1, p.y + 0.2, p.z) < p.y - 0.5;
    const o = { mx: 1, held: {} };
    if (p.onGround) { doubled = false; if ((wall || gap) && jumpHold === 0) jumpHold = 14; }
    else if (!doubled && jumpHold === 0 && p.vy < 2 && (wall || groundBelow(p.x + 1.5, p.y, p.z) < p.y - 3)) { doubled = true; jumpHold = 12; }
    if (jumpHold > 0) { o.held.jump = jumpHold > 1; jumpHold--; }
    const before = count(log, 'recall');
    run(o, 1);
    if (count(log, 'recall') > before) falls++;
  }
  return { x: p.x, y: p.y, falls, secs: t / 60 };
}
for (const char of ['nova', 'echo']) {
  const r = runRoute(char);
  assert(r.x >= ROUTE_END_X && r.falls === 0, `${char} runs the Skyline Relay from the tower top to the beacon in ${r.secs.toFixed(1)} s without falling (x ${r.x.toFixed(1)})`);
}

{ // Drone patrol triggers on the roof
  const { w, p, run, log } = setup('nova');
  p.x = 190.5; p.y = 15.6; run({}, 2); p.x = 192; run({}, 2);
  const S = w.encounters.find(s => s.def.id === 'patrol');
  const spawned = w.enemies.filter(e => e.enc === 'patrol');
  assert(S.state === 'active' && spawned.length === 4 && spawned.filter(e => e.type === 'drone').length === 2 && log.some(e => e.type === 'banner' && e.text === 'Skyline Relay'),
    `Drone patrol starts at x=${S.def.trigger} with ${spawned.length} enemies`);
}
{ // Relay Gate: seals, pulls stragglers in, runs two waves, opens when cleared, then the route completes
  const w = new World(); w.enemies = [];
  const p = w.addPlayer('a', 'nova'), q = w.addPlayer('b', 'echo');
  let prev = { 0: { held: {} }, 1: { held: {} } }; const log = [];
  const run = (n = 1) => { for (let i = 0; i < n; i++) { const c0 = mk(prev[0]), c1 = mk(prev[1]); prev = { 0: c0, 1: c1 }; w.step({ 0: c0, 1: c1 }); log.push(...w.events); w.events.length = 0; } };
  w.teleport('skyline'); run(3);
  for (const S of w.encounters) if (S.def.id !== 'relay') S.state = 'cleared';
  p.x = 262; p.y = 18.6; q.x = 250; q.y = 18.6; run(2);
  const R = w.encounters.find(s => s.def.id === 'relay');
  const sealed = GATES.L2 && GATES.R2, pulled = q.x > 258.8, w1 = w.enemies.filter(e => e.enc === 'relay' && !e.dead).length;
  killAll(w, p, 'relay'); run(3);
  const wave2 = R.wave === 1 && w.enemies.some(e => e.enc === 'relay' && e.type === 'brute' && !e.dead);
  for (let i = 0; i < 5; i++) { killAll(w, p, 'relay'); run(3); }
  const open = !GATES.L2 && !GATES.R2 && R.state === 'cleared';
  for (const r of [p, q]) { r.x = ROUTE_END_X + 1; r.y = 18.6; }   // together: the co-op camera keeps players close
  run(3);
  assert(sealed && pulled && w1 >= 5 && wave2 && open && count(log, 'banner', b => b.text === 'Relay secured') === 1 && count(log, 'banner', b => b.text === 'Route complete') === 1,
    `Relay Gate seals (straggler pulled in), wave 1 (${w1}) then the Brute wave, opens when cleared, and the route completes`);
}
{ // A wipe mid-encounter resets it: its enemies go, the gates open, and it can start again
  const { w, p, run, log } = setup('nova');
  for (const S of w.encounters) if (S.def.id !== 'relay') S.state = 'cleared';
  p.x = 262; p.y = 18.6; run(2);
  const R = w.encounters.find(s => s.def.id === 'relay');
  const was = R.state;
  w.resetToCheckpoint(); run(2);
  assert(was === 'active' && R.state === 'idle' && !GATES.L2 && !GATES.R2 && !w.enemies.some(e => e.enc === 'relay'),
    `Wipe resets the Relay Gate encounter (${was} -> ${R.state}), gates open`);
}
{ // Teleporting to the new zone lands on the tower-top bridge
  const { p } = setup('echo');
  assert(Math.abs(p.x - 166) < 1 && Math.abs(p.y - 15.6) < 0.05, `Skyline Relay zone spawn at x=${p.x.toFixed(1)}, y=${p.y.toFixed(1)}`);
}
{ // Every encounter's spawn points are inside the level, above solid ground
  const bad = [];
  for (const E of ENCOUNTERS) for (const wv of [...(E.waves || []), E.extra || []]) for (const [t, x, y] of wv) {
    if (t === 'drone') continue;
    if (Math.abs(groundBelow(x, y + 0.1) - y) > 0.05) bad.push(`${E.id}:${t}@${x}`);
  }
  assert(bad.length === 0, `Ground enemies spawn standing on a surface${bad.length ? ': ' + bad.join(', ') : ''}`);
}
