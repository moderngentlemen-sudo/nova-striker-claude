// Golden traces: runs scripted scenarios through the prototype's real simulation (../../game/js) and writes,
// for each, the inputs and the state after every tick to project/tests/traces/<name>.json. The Godot test
// (tests/test_traces.gd) replays the same inputs through the port and compares tick by tick, so any slip in
// the port shows up as the first tick where the two runs part.
//
// Math.random is replaced by mulberry32 with the scenario's seed (the port's Rng), so enemies' cooldowns and
// bosses' choices match too. Run from fresh-start-prototype: node engine-trials/godot/tools/trace-js.mjs
import { writeFileSync, mkdirSync } from 'node:fs';

function mulberry32(a) {
  return function () {
    a |= 0; a = a + 0x6D2B79F5 | 0;
    let t = Math.imul(a ^ a >>> 15, 1 | a);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}
Math.random = mulberry32(1);   // (seeded again per scenario; set before the modules load anything)

const { World } = await import('../../../game/js/world.js');
const { createEnemy } = await import('../../../game/js/enemies.js');
const { SETTINGS, DEFAULT_SETTINGS } = await import('../../../game/js/config.js');
const L = await import('../../../game/js/level.js');

const BTNS = ['jump', 'dash', 'melee', 'fire', 'parry', 'sig', 'mode', 'lock', 'sub', 'ult'];
function mk(prev, o = {}) {
  const held = {}, pressed = {}, released = {};
  for (const b of BTNS) { held[b] = !!(o.held && o.held[b]); pressed[b] = held[b] && !prev[b]; released[b] = !held[b] && !!prev[b]; }
  return { mx: o.mx || 0, my: o.my || 0, aimFree: !!o.aim, ax: o.aim ? o.aim[0] : 0, ay: o.aim ? o.aim[1] : 0, held, pressed, released };
}
const r6 = v => (typeof v === 'number' ? Math.round(v * 1e9) / 1e9 : v);

// A scenario: who plays, where, which enemies, the settings, and the input as [ticks, input] steps
const SCENARIOS = [
  { name: 'move_gym', x: 2, y: 0, clear: true, steps: [
    [10, {}], [30, { mx: 1 }], [20, { mx: 1, held: { jump: true } }], [25, { mx: 1 }],
    [1, { held: { jump: true } }], [6, {}], [1, { held: { jump: true } }], [3, { held: { jump: true } }], [30, {}],
    [1, { mx: 1, held: { dash: true } }], [20, {}], [1, { mx: -1, my: -1, held: { dash: true } }], [25, { my: -1 }], [10, {}],
    [40, { held: { dash: true } }], [1, {}], [30, {}],   // a charged dash (level 2)
    [1, { mx: 1, held: { dash: true } }], [2, { mx: 1 }], [1, { mx: 1, held: { jump: true } }], [30, { mx: 1 }],   // a dash-jump
    [20, { mx: -1 }], [8, { mx: 1 }], [30, {}],   // skate carve
    [1, { held: { jump: true } }], [8, { held: { jump: true } }], [1, {}], [1, { held: { jump: true } }], [6, {}],
    [1, { held: { jump: true } }], [40, { held: { jump: true } }], [40, {}],   // double jump then boosters
  ] },
  { name: 'wall_ledge', x: 27, y: 0, clear: true, steps: [
    [10, {}], [12, { mx: 1 }], [1, { mx: 1, held: { jump: true } }], [40, { mx: 1, held: { jump: true } }],   // onto the ledge wall at x 32
    [30, { mx: 1 }], [1, { mx: -1 }], [2, { mx: -1 }], [1, { mx: -1, held: { jump: true } }], [20, { mx: -1 }],   // a wall leap
    [40, { mx: 1 }], [1, { mx: 1, held: { jump: true } }], [12, { mx: 1 }], [1, { mx: 1, held: { jump: true } }], [12, { mx: 1 }],
    [1, { mx: 1, held: { jump: true } }], [20, { mx: 1 }], [30, { mx: 1, my: -1 }], [40, {}],   // climb kicks, a fast slide down
  ] },
  { name: 'foundry_fight', zone: 'foundry', steps: [
    [20, {}], [60, { mx: 1 }], [1, { mx: 1, held: { fire: true } }], [3, { mx: 1 }], [1, { mx: 1, held: { fire: true } }], [3, { mx: 1 }],
    [45, { aim: [1, 0], held: { fire: true } }], [1, { aim: [1, 0] }], [30, { mx: 1 }],
    [1, { mx: 1, held: { jump: true } }], [15, { mx: 1 }], [80, { mx: 1, held: { fire: true } }], [1, { mx: 1 }], [40, { mx: 1 }],
    [1, { held: { melee: true } }], [12, {}], [1, { held: { melee: true } }], [12, {}], [1, { held: { melee: true } }], [30, {}],
    [1, { held: { parry: true } }], [20, { mx: 1 }], [1, { mx: 1, held: { jump: true } }], [20, { mx: 1 }],
    [120, { mx: 1, held: { fire: true } }], [1, {}], [60, { mx: 1 }], [200, { mx: 1 }],
  ] },
];

const outDir = new URL('../project/tests/traces/', import.meta.url);
mkdirSync(outDir, { recursive: true });
for (const S of SCENARIOS) {
  Math.random = mulberry32(S.seed || 1);
  Object.assign(SETTINGS, DEFAULT_SETTINGS, S.settings || {});
  for (const g in L.GATES) L.GATES[g] = false;
  const idStart = createEnemy('swarmer', 0, 0).id + 1;   // the id the next enemy gets (the probe's random draw is undone by reseeding)
  Math.random = mulberry32(S.seed || 1);
  const w = new World();
  if (S.zone) w.teleport(S.zone);
  if (S.clear) w.enemies = [];
  const p = w.addPlayer('test', S.char || 'nova');
  if (S.x !== undefined) { p.x = S.x; p.y = S.y; p.prevX = p.x; p.prevY = p.y; p.lastSafeX = p.x; p.lastSafeY = p.y; }
  p.mercy = 0;
  const ticks = [];
  let prev = {};
  for (const [n, o] of S.steps) {
    for (let i = 0; i < n; i++) {
      const c = mk(prev, o); prev = c.held;
      w.step({ [p.slot]: c });
      ticks.push({
        p: [p.x, p.y, p.vx, p.vy, p.state, p.facing, p.h, p.hp, p.chargeT, p.burstT].map(r6),
        e: w.enemies.map(e => [e.id, e.type, e.x, e.y, e.hp, e.state].map(r6)),
        n: w.projectiles.length,
        ev: w.events.map(e => e.type),
      });
      w.events.length = 0;
    }
  }
  writeFileSync(new URL(`${S.name}.json`, outDir), JSON.stringify({ name: S.name, char: S.char || 'nova', zone: S.zone || null, clear: !!S.clear,
    x: S.x ?? null, y: S.y ?? null, seed: S.seed || 1, idStart, settings: S.settings || {}, steps: S.steps, ticks }) + '\n');
  console.log(`${S.name}: ${ticks.length} ticks, ends at x ${p.x.toFixed(2)} (${p.state}), ${w.enemies.length} enemies`);
}
