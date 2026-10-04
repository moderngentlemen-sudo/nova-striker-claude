// Writes the game's tuning and level data from the three.js prototype (../../game/js) to the Godot project,
// so the port runs on exactly the same numbers and geometry:
//   project/data/tuning.json  every table in config.js, plus the enemy and boss tables
//   project/data/level.json   boxes, routes and path pieces, zones, checkpoints, encounters, pickups, lifts
// Run from fresh-start-prototype: node engine-trials/godot/tools/export-data.mjs
// Infinity is written as the string "inf" (JSON has no Infinity); the Godot loader turns it back.
import { writeFileSync } from 'node:fs';
import * as CONFIG from '../../../game/js/config.js';
import * as LEVEL from '../../../game/js/level.js';
import { ENEMY_TYPES, MORTAR, CHARGER } from '../../../game/js/enemies.js';
import { BOSS } from '../../../game/js/bosses.js';   // (also adds the bosses to ENEMY_TYPES)

const replacer = (k, v) => (v === Infinity ? 'inf' : v === -Infinity ? '-inf' : v);
const write = (name, data) => {
  writeFileSync(new URL(`../project/data/${name}`, import.meta.url), JSON.stringify(data, replacer, 1) + '\n');
};

// ---- Tuning: every plain-data export of config.js (functions and the live SETTINGS object left out) ----
const tuning = {};
for (const [k, v] of Object.entries(CONFIG)) {
  if (typeof v === 'function' || k === 'SETTINGS') continue;
  tuning[k] = v;
}
Object.assign(tuning, { ENEMY_TYPES, MORTAR, CHARGER, BOSS });
write('tuning.json', tuning);

// ---- Level ----
// level.js keeps its fall-out heights in a private table; it is copied here and checked against killYAt
const KILL = [[662, 764, 14], [800, 924, 6]];
for (let x = -20; x < 1200; x += 0.5) {
  const want = LEVEL.killYAt(x), got = (KILL.find(([a, b]) => x >= a && x < b) || [0, 0, LEVEL.KILL_Y])[2];
  if (want !== got) throw new Error(`KILL table out of date at x ${x}: level.js says ${want}`);
}
const level = {
  ARC_START: LEVEL.ARC_START, ARC_R: LEVEL.ARC_R, ARC_END: LEVEL.ARC_END, PATH2_X0: LEVEL.PATH2_X0,
  ROUTES: LEVEL.ROUTES.map(({ id, name, x0, x1, endX, sx, start, phi }) => ({ id, name, x0, x1, endX, sx: sx ?? null, start: start ?? null, phi: phi ?? null })),
  SEGS_OF: Object.fromEntries(Object.entries(LEVEL.SEGS_OF).map(([id, segs]) =>
    [id, segs.map(({ kind, x0, x1, P, phi, r, s, C }) => ({ kind, x0, x1, P, phi, r: r ?? 0, s: s ?? 0, C: C ?? null }))])),
  TOWER_CENTER: LEVEL.TOWER_CENTER,
  BOXES: LEVEL.BOXES.map(({ id, x0, x1, y0, y1, type, tag, loot }) => ({ id, x0, x1, y0, y1, type, tag, loot })),
  DESTRUCT: LEVEL.DESTRUCT,
  GATES: Object.keys(LEVEL.GATES),
  ZONES: LEVEL.ZONES, CHECKPOINTS: LEVEL.CHECKPOINTS, ENCOUNTERS: LEVEL.ENCOUNTERS,
  LEVEL_PICKUPS: LEVEL.LEVEL_PICKUPS, LIFTS: LEVEL.LIFTS, KILL, KILL_Y: LEVEL.KILL_Y,
  ROUTE_END_X: LEVEL.ROUTE_END_X, ARENA_TRIGGER_X: LEVEL.ARENA_TRIGGER_X, TOWER_TRIGGER_X: LEVEL.TOWER_TRIGGER_X,
};
write('level.json', level);
console.log(`tuning: ${Object.keys(tuning).length} tables; level: ${level.BOXES.length} boxes, ${level.ENCOUNTERS.length} encounters, ` +
  `${Object.values(level.SEGS_OF).reduce((n, s) => n + s.length, 0)} path pieces`);
