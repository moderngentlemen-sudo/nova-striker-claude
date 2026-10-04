// Writes the Helix Foundry stretch (level.js) to project/level.json, so the Godot slice runs on the same
// geometry as the three.js and Babylon builds. Run from fresh-start-prototype: node engine-trials/godot/export-level.mjs
import { writeFileSync } from 'node:fs';
import { BOXES, SEGS_OF, LIFTS, DESTRUCT, ROUTES } from '../../game/js/level.js';
import { CHARS, GRAVITY, FALL_MULT, RISE_CUT_MULT, MAX_FALL, FAST_FALL, COYOTE, JUMP_BUFFER, WALL } from '../../game/js/config.js';
const R = ROUTES.find(r => r.id === 'foundry'), X0 = 398, X1 = 764;
const boxes = BOXES.filter(b => b.x1 >= X0 && b.x0 <= X1 && b.type !== 'g').map(({ x0, x1, y0, y1, type, tag }) => ({ x0, x1, y0, y1, type, tag }));
const n = CHARS.nova;
const out = {
  route: { id: R.id, name: R.name, x0: X0, x1: X1, spawn: [402, 0] },
  segs: SEGS_OF.foundry.map(({ kind, x0, x1, P, phi, r, s, C }) => ({ kind, x0, x1, P, phi, r: r || 0, s: s || 0, C: C || null })),
  boxes, lifts: LIFTS, destruct: DESTRUCT,
  nova: { run: n.run, accelG: n.accelG, decelG: n.decelG, accelA: n.accelA, jumpV: n.jumpV, dblV: n.dblV, dash: n.dash, slide: n.slide,
    wall: n.wall, width: n.width, height: n.height, crouchH: n.crouchH },
  physics: { GRAVITY, FALL_MULT, RISE_CUT_MULT, MAX_FALL, FAST_FALL, COYOTE, JUMP_BUFFER, WALL },
};
writeFileSync(new URL('./project/level.json', import.meta.url), JSON.stringify(out, null, 1));
console.log(`${boxes.length} boxes, ${out.segs.length} path pieces`);
