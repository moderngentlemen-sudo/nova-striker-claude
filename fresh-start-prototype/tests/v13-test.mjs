// Version 13 checks (3D, third person): moving across a walkway and the rails along its edges, shots and strikes
// that go across the path as well as along it, ring shockwaves that reach the side of their source, the Warden's
// sweeping laser, and lock-on candidates that prefer what is in front of the camera.
import { World } from '../game/js/world.js';
import { createEnemy } from '../game/js/enemies.js';
import { forceBossAttack } from '../game/js/bosses.js';
import * as L from '../game/js/level.js';
import { SETTINGS } from '../game/js/config.js';
SETTINGS.novaKit = 'marksman'; SETTINGS.echoKit = 'hunter'; SETTINGS.lockMode = 'manual';

const BT = ['jump', 'dash', 'melee', 'fire', 'parry', 'sig', 'mode', 'lock', 'sub', 'ult'];
const assert = (c, m) => { console.log((c ? 'PASS ' : 'FAIL ') + m); if (!c) process.exitCode = 1; };
function person() {
  let prev = {};
  return (o = {}) => {
    const held = {}, pressed = {}, released = {};
    for (const b of BT) { held[b] = !!(o.held && o.held[b]); pressed[b] = held[b] && !prev[b]; released[b] = !held[b] && !!prev[b]; }
    prev = held;
    return { mx: o.mx || 0, mz: o.mz || 0, my: o.my || 0, aimFree: !!o.aim, camAim: !!o.cam, ax: o.aim ? o.aim[0] : 0, ay: o.aim ? o.aim[1] : 0, az: o.aim ? o.aim[2] : 0, held, pressed, released };
  };
}
const fresh = (char = 'nova', x = 20) => {
  const w = new World(); for (const S of w.encounters) S.state = 'cleared';
  const p = w.addPlayer('kbm', char); Object.assign(p, { x, y: 0, z: 0, prevX: x, prevY: 0, prevZ: 0, mercy: 1e9 }); return { w, p };
};

{ // Across the walkway: the stick's z moves him sideways, and the rail along the edge stops him
  const { w, p } = fresh(); const c = person();
  for (let t = 0; t < 40; t++) w.step({ [p.slot]: c({ mz: 1 }) });
  const mid = p.z;
  for (let t = 0; t < 120; t++) w.step({ [p.slot]: c({ mz: 1 }) });
  const edge = L.HW - 0.25 - p.w / 2;
  assert(mid > 1.5 && Math.abs(p.z - edge) < 0.05 && p.y === 0 && p.facingZ > 0.9, `Sideways: ${mid.toFixed(2)} m in 40 ticks, held at the rail (z ${p.z.toFixed(2)}, rail face ${edge.toFixed(2)}), on the floor, facing across`);
}
{ // Diagonal running is no faster than straight running
  const run = (mx, mz) => { const { w, p } = fresh('echo', 20); const c = person(); for (let t = 0; t < 20; t++) w.step({ [p.slot]: c({ mx, mz }) }); return Math.hypot(p.vx, p.vz); };
  const a = run(1, 0), b = run(Math.SQRT1_2, Math.SQRT1_2);
  assert(Math.abs(a - b) < 0.05, `Diagonal speed ${b.toFixed(2)} m/s matches straight ${a.toFixed(2)} m/s`);
}
{ // Rails: solid to walk into, too low to block a line of sight or count as a wall to cling to
  const z = L.HW - 0.1, sight = !L.segmentBlocked(20, 1.2, 0, 20, 0.6, z + 2);
  const rail = L.BOXES.find(b => b.tag === 'rail' && b.x0 <= 20 && b.x1 >= 20 && b.z1 > 0);
  assert(rail && L.pointInSolid(20, 0.5, rail.z0 + 0.1) && sight, `Rail along the walkway edge is solid (${!!rail}) and never blocks sight (${sight})`);
}
{ // A shot fired across the path flies across it and hits an enemy standing to the side (and none goes the other way)
  const shoot = dir => {
    const { w, p } = fresh(); const c = person();
    const e = createEnemy('swarmer', 20, 0, { z: 3.5, cd: 9999 }); e.hp = 999; w.enemies.push(e);
    const dy = e.y + e.h / 2 - (p.y + p.h * 0.62), m = Math.hypot(dy, 3.5);
    for (let t = 0; t < 60; t++) w.step({ [p.slot]: c({ aim: [0, dy / m, dir * 3.5 / m], cam: true, held: { fire: t % 20 < 3 } }) });
    return e.hp < 999;
  };
  const toward = shoot(1), away = shoot(-1);
  assert(toward && !away, `Across the path: a shot aimed at an enemy beside him hits (${toward}); one aimed the other way does not (${!away})`);
}
{ // A melee strike lands on an enemy to his side once he faces it
  const { w, p } = fresh('echo'); const c = person();
  const e = createEnemy('swarmer', 20, 0, { z: 1.3, cd: 9999 }); e.hp = 999; w.enemies.push(e);
  for (let t = 0; t < 4; t++) w.step({ [p.slot]: c({ mz: 1 }) });
  let hit = false; for (let t = 0; t < 40 && !hit; t++) { w.step({ [p.slot]: c({ held: { melee: t < 3 } }) }); hit = e.hp < 999; }
  assert(hit, `Melee across the path: facing (${p.facing.toFixed(2)}, ${p.facingZ.toFixed(2)}), the strike lands`);
}
{ // Ring shockwaves spread in every direction along the floor: they reach a player beside the source, and a
  // player in the air over the ring is missed
  const ring = (dz, air) => {
    const { w, p } = fresh('nova', 30); const c = person();
    Object.assign(p, { z: dz, prevZ: dz, mercy: 0 });
    const b = createEnemy('brute', 30, 0, { cd: 9999 }); w.enemies.push(b);
    w.spawnShockwave(b, 10, 1);
    let hit = false;
    for (let t = 0; t < 40; t++) { if (air) { p.y = 1.5; p.vy = 0; p.onGround = false; } w.step({ [p.slot]: c() }); for (const ev of w.events) if (ev.type === 'playerHit' && ev.p === p) hit = true; w.events.length = 0; }
    return hit;
  };
  const side = ring(3, false), over = ring(3, true);
  assert(side && !over, `Shockwave ring: reaches a player 3 m to the side (${side}), passes under one in the air (${!over})`);
}
{ // The Warden's laser sweeps round: a player well off to the side of its first line is still caught as it turns
  const w = new World(); w.enemies = [];
  const p = w.addPlayer('p0', 'ram'); const c = person();
  w.bossRush('warden'); for (let t = 0; t < 3; t++) w.step({ [p.slot]: c() });
  const W = w.enemies.find(e => e.type === 'warden');
  Object.assign(W, { x: 88, z: 0, prevX: 88, prevZ: 0, vx: 0, vz: 0, state: 'idle', st: 0, invuln: 0, phase: 2, laserHigh: false, facing: -1, facingZ: 0 });
  const put = () => Object.assign(p, { x: 84, y: 0, z: 4.5, prevX: 84, prevZ: 4.5, vx: 0, vz: 0, mercy: 0 });
  put(); p.hp = p.maxHp = 9999;
  forceBossAttack(w, W, 'laser');
  let hit = false;
  for (let t = 0; t < 200 && !hit; t++) { w.step({ [p.slot]: c() }); for (const ev of w.events) if (ev.type === 'playerHit' && ev.p === p) hit = true; w.events.length = 0; put(); }
  assert(hit, `Warden laser: a player 4.5 m to the side of its first line (about 50 degrees off it) is caught as it sweeps round (${hit})`);
}
{ // Lock-on with the camera: of two enemies at the same distance, the one in front of the view is chosen
  const { w, p } = fresh('nova', 20);
  const a = createEnemy('swarmer', 24, 0, { cd: 9999 }), b = createEnemy('swarmer', 20, 0, { z: -4, cd: 9999 });
  w.enemies.push(b, a);
  p.camAim = true; p.aimX = 1; p.aimY = 0; p.aimZ = 0;
  const front = w.lockCandidates(p)[0];
  p.aimX = 0; p.aimZ = -1;
  const side = w.lockCandidates(p)[0];
  assert(front === a && side === b, `Lock-on follows the camera: ahead picks the one ahead (${front === a}), turned picks the one beside (${side === b})`);
}
{ // Every walkway and arena floor is at least as wide as the corridor, and every breakable piece lies inside it
  const narrow = L.BOXES.filter(b => b.type === 's' && ['ground', 'floor', 'plaza', 'yard', 'landing'].includes(b.tag) && b.z1 - b.z0 < 2 * L.HW - 0.01).length;
  const out = L.BOXES.filter(b => b.type === 'd' && (b.z0 < -L.HW || b.z1 > L.HW)).length;
  assert(narrow === 0 && out === 0, `Corridors: ${narrow} narrow floors, ${out} breakables outside the walkway`);
}
