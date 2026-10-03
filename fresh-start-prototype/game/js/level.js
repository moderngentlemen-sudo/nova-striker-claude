// Level data, the curved gameplay path (CP-08), and collision helpers.
// The simulation is purely 2D (x along the path, y up). Rendering maps x onto
// a path that runs straight, bends 180° around the Storm Spire tower, then runs straight.

export const ARC_START = 104;
export const ARC_R = 14;
export const ARC_END = ARC_START + Math.PI * ARC_R;

// Version 12 levels (x >= PATH2_X0): each is its own stretch of path somewhere else in the world, built as a chain
// of pieces, so it can wind through 3D space: ['line', length] runs straight on; ['arc', length, radius, s] turns,
// s = +1 wrapping round a centre away from the camera (like the Storm Spire), s = -1 bending toward the camera
// (the camera sits inside the curve and the level sweeps round it). The simulation never knows: it stays 2D.
export const PATH2_X0 = 400;
export const ROUTES = [
  { id: 'skyport', name: 'Skyport route', x0: -20, x1: 400, endX: 304 },
  { id: 'foundry', name: 'Helix Foundry', x0: 400, x1: 790, sx: 400, endX: 758, start: { x: -60, z: 150 }, phi: 0,
    pieces: [['line', 64], ['arc', 52, 26, -1], ['arc', 46, 22, 1], ['line', 22], ['arc', 78, 15, 1], ['line', 40], ['line', 62]] },
  { id: 'undercity', name: 'Undercity Descent', x0: 790, x1: 1200, sx: 800, endX: 1177, start: { x: -200, z: -100 }, phi: 5 * Math.PI / 4,
    pieces: [['line', 50], ['arc', 44, 28, -1], ['line', 30], ['arc', 56, 18, 1], ['arc', 30, 30, -1], ['line', 75], ['arc', 34, 22, 1], ['line', 62]] },
];
export const routeAt = x => ROUTES.find(r => x >= r.x0 && x < r.x1) || ROUTES[0];
const nrm = phi => ({ x: Math.sin(phi), z: Math.cos(phi) });   // the camera side of a heading phi (tangent: cos, -sin)
export const SEGS = [], SEGS_OF = {};
for (const R of ROUTES) {
  if (!R.pieces) continue;
  let x = R.sx, P = { ...R.start }, phi = R.phi;
  SEGS_OF[R.id] = [];
  for (const [kind, len, r, s] of R.pieces) {
    const seg = { route: R.id, kind, x0: x, x1: x + len, P: { ...P }, phi, r, s };
    if (kind === 'arc') { const n0 = nrm(phi); seg.C = { x: P.x - s * n0.x * r, z: P.z - s * n0.z * r }; }
    SEGS.push(seg); SEGS_OF[R.id].push(seg);
    const f = frameOn(seg, len); P = { x: f.px, z: f.pz }; phi = kind === 'arc' ? phi + s * len / r : phi; x += len;
  }
}
function frameOn(g, u) {
  if (g.kind === 'line') {
    const tx = Math.cos(g.phi), tz = -Math.sin(g.phi);
    return { px: g.P.x + tx * u, pz: g.P.z + tz * u, tx, tz, nx: -tz, nz: tx };
  }
  const phi = g.phi + g.s * u / g.r, n = nrm(phi);
  return { px: g.C.x + g.s * n.x * g.r, pz: g.C.z + g.s * n.z * g.r, tx: Math.cos(phi), tz: -Math.sin(phi), nx: n.x, nz: n.z };
}
// Does the stretch x0..x1 curve (its geometry is drawn in short pieces that follow the bend)?
export function curvedSpan(x0, x1) {
  if (x1 > ARC_START && x0 < ARC_END && x0 < PATH2_X0) return true;
  return SEGS.some(g => g.kind === 'arc' && x1 > g.x0 && x0 < g.x1);
}

// Returns world position/tangent/normal for sim x. Normal points toward the camera.
export function pathFrame(x) {
  if (x >= PATH2_X0) {
    const segs = SEGS_OF[routeAt(x).id];
    let g = segs.find(q => x < q.x1) || segs[segs.length - 1];
    if (x < segs[0].x0) g = segs[0];
    return frameOn(g, x - g.x0);   // (past either end it runs straight on along the end piece)
  }
  if (x <= ARC_START) {
    return { px: x, pz: 0, tx: 1, tz: 0, nx: 0, nz: 1 };
  }
  if (x < ARC_END) {
    const th = (x - ARC_START) / ARC_R;
    const s = Math.sin(th), c = Math.cos(th);
    return { px: ARC_START + ARC_R * s, pz: -ARC_R + ARC_R * c, tx: c, tz: -s, nx: s, nz: c };
  }
  const d = x - ARC_END;
  return { px: ARC_START - d, pz: -2 * ARC_R, tx: -1, tz: 0, nx: 0, nz: -1 };
}
export const TOWER_CENTER = { x: ARC_START, z: -ARC_R };

// Boxes: [x0, x1, y0, y1, type, tag]. type 's' solid, 'o' one-way, 'g' gate (solid when closed).
const RAW = [
  // Movement gym
  [-12, -10, -6, 30, 's', 'bound'],
  [-10, 14, -6, 0, 's', 'ground'],
  [18, 32, -6, 0, 's', 'ground'],
  [28.5, 29.3, 2.1, 8.6, 's', 'panel'],      // floating wall for wall-jump practice
  [32, 40, -6, 6, 's', 'ledge'],
  [40, 60, -6, 0, 's', 'ground'],
  [44, 50, 1.0, 6.5, 's', 'tunnel'],         // slide or crouch under
  [57.5, 58.5, 0, 3, 's', 'pillar'],         // turret pillar
  // Concourse Lock arena
  [60, 97, -6, 0, 's', 'ground'],
  // Gates reach far above any jump, rocket jump or booster climb, and cannot be wall-slid (energy)
  [62, 62.8, 0, 30, 'g', 'L'],
  [96.2, 97, 0, 30, 'g', 'R'],
  [74, 84, 2.0, 2.4, 'o', 'dais'],
  [63.2, 66.6, 5.0, 5.4, 'o', 'perch'],
  [92.2, 96.0, 5.0, 5.4, 'o', 'perch'],
  [67.5, 68.3, 2.6, 5.0, 's', 'column'],      // floats above head height: walk under, wall-jump off
  [89.9, 90.7, 2.6, 5.0, 's', 'column'],
  // Storm Spire climb (curved path)
  [97, 113, -6, 0, 's', 'ground'],
  [113, 118, -6, 2.5, 's', 'step'],
  [118, 143, -6, 0, 's', 'ground'],
  [120, 124, 4.6, 5.2, 'o', 'plat'],
  [126, 130, 7.6, 8.2, 'o', 'plat'],
  [132, 136.5, 10.6, 11.2, 'o', 'plat'],
  [138.5, 142, 13.2, 13.8, 'o', 'plat'],
  [143, 162, -6, 15.6, 's', 'top'],
  // Skyline Relay: rooftops and sky bridges beyond the Storm Spire (gaps are safe to fail: a fall
  // brings you back to solid ground for a little damage)
  [162, 186, 12.6, 15.6, 's', 'bridge'],      // sky bridge off the tower top
  [190, 214, 5, 15.6, 's', 'roof'],           // relay roof: drone patrol (4 m gap before it)
  [200, 203.5, 15.6, 17.4, 's', 'cover'],     // low block to hide behind or stand on
  [219, 244, 5, 12.6, 's', 'yard'],           // mortar yard, 3 m below the roof
  [229, 229.8, 12.6, 14.8, 's', 'wall'],      // cover walls (chargers crash into them)
  [238, 238.8, 12.6, 14.8, 's', 'wall'],
  [244, 248, 5, 15.6, 's', 'step'],           // climb: step, then the mortar ledge
  [248, 258, 5, 18.6, 's', 'ledge'],
  [258, 258.8, 18.6, 50, 'g', 'L2'],          // Relay Gate
  [258, 298, 5, 18.6, 's', 'relay'],
  [264, 270, 21.6, 22.0, 'o', 'plat'],
  [276, 281, 23.6, 24.0, 'o', 'plat'],
  [287, 293, 21.6, 22.0, 'o', 'plat'],
  [297.2, 298, 18.6, 50, 'g', 'R2'],
  [298, 316, 5, 18.6, 's', 'pad'],            // beacon pad: the end of the route, and the Stormcaller's arena
  [301.5, 305, 21.8, 22.2, 'o', 'plat'],      // perches over the pad: above its sweep, closer to the gunship
  [309, 312.5, 21.8, 22.2, 'o', 'plat'],
  [316, 318, -6, 50, 's', 'bound'],

  // ---- Helix Foundry (Version 12) ----
  // The approach, then an S-bend: a trench that sweeps round the camera, then a bend round the smelter
  [398, 400, -6, 40, 's', 'bound'],
  [400, 430, -6, 0, 's', 'ground'], [433, 464, -6, 0, 's', 'ground'],      // a 3 m pit at 430
  [446, 452, 0, 1.4, 's', 'block'],
  [464, 516, -6, 0, 's', 'ground'],                                       // the trench (bends toward the camera)
  [474, 480, 3, 3.4, 'o', 'walk'], [486, 492, 4.5, 4.9, 'o', 'walk'], [498, 504, 3, 3.4, 'o', 'walk'],
  [516, 584, -6, 0, 's', 'ground'],                                       // round the smelter, then the bridge to the core
  // The helix: a 300° climb round the reactor core on rising platforms over the foundry floor
  [584, 662, -6, 0, 's', 'floor'],
  ...Array.from({ length: 10 }, (_, i) => [586 + i * 7, 592 + i * 7, 2.2 * (i + 1) - 0.4, 2.2 * (i + 1), 'o', 'ring']),   // 1 m gaps
  [655, 662, 20, 24.2, 's', 'landing'],
  // The sky bridge out over the foundry, and the Crucible at its end
  [662, 702, 22.2, 24.2, 's', 'bridge'],
  [702, 762, 18, 24.2, 's', 'crucible'],
  [708, 708.8, 24.2, 60, 'g', 'F1'], [755.2, 756, 24.2, 60, 'g', 'F2'],
  [716, 721, 27.4, 27.8, 'o', 'perch'], [740, 745, 27.4, 27.8, 'o', 'perch'],
  [762, 764, -6, 60, 's', 'bound'],

  // ---- Undercity Descent (Version 12) ----
  // Rooftops, a bend toward the camera through the vents, gaps over the street, then down the stair that winds
  // round the cooling tower, a bend into the transit line, and the plaza at the bottom
  [798, 800, 0, 60, 's', 'bound'],
  [800, 824, 14, 20, 's', 'roof'], [827, 894, 14, 20, 's', 'roof'],
  [862, 868, 23, 23.4, 'o', 'vent'],
  [894, 902, 14, 20, 's', 'roof'], [905, 911, 12, 18, 's', 'roof'], [914, 924, 10, 16, 's', 'roof'],
  ...Array.from({ length: 8 }, (_, i) => [924 + i * 7, 931 + i * 7, 16 - 1.4 * (i + 1) - 6, 16 - 1.4 * (i + 1), 's', 'stair']),
  [980, 1010, -2, 4, 's', 'ground'],
  [1010, 1085, -6, 0, 's', 'ground'],
  [1032, 1040, 3.6, 4.0, 'o', 'gantry'],
  [1085, 1119, -6, 0, 's', 'ground'],
  [1119, 1179, -6, 0, 's', 'plaza'],
  [1124, 1124.8, 0, 40, 'g', 'U1'], [1174.2, 1175, 0, 40, 'g', 'U2'],
  [1133, 1138, 3.4, 3.8, 'o', 'perch'], [1160, 1165, 3.4, 3.8, 'o', 'perch'],
  [1179, 1181, -6, 60, 's', 'bound'],

  // ---- Breakable pieces (Version 12): type 'd', solid until broken. The tag is what it is (DESTRUCT); the
  // seventh field is a power-up it drops when broken (POWERUPS) ----
  // Skyport route: a few crates and a barricade where they make a fight more interesting (none in the Concourse
  // Lock: its boss fight is tuned to an open floor)
  [206, 207, 15.6, 16.6, 'd', 'crate', 'ultcell'], [233.5, 234.5, 12.6, 13.6, 'd', 'crate'], [272, 273.2, 18.6, 20.2, 'd', 'barricade'],
  // Helix Foundry
  [412, 413, 0, 1, 'd', 'crate', 'medkit'], [413.1, 414.1, 0, 1, 'd', 'crate'], [412.5, 413.5, 1, 2, 'd', 'crate', 'fury'],
  [422, 423.2, 0, 1.6, 'd', 'barricade'], [440, 441, 0, 1, 'd', 'crate'], [457, 458.2, 0, 1.6, 'd', 'barricade'],
  [482, 482.4, 0, 3.2, 'd', 'glass'], [494, 495, 0, 1, 'd', 'crate', 'plating'], [508, 509.2, 0, 1.6, 'd', 'barricade'],
  [528, 529.2, 0, 2.4, 'd', 'pillar'], [546, 547, 0, 1, 'd', 'crate', 'overclock'], [570, 571, 0, 1, 'd', 'crate'], [571.1, 572.1, 0, 1, 'd', 'crate', 'ultcell'],
  [608.5, 609.5, 8.8, 9.8, 'd', 'crate', 'medkit'], [629.5, 630.5, 15.4, 16.4, 'd', 'crate', 'fury'],
  [676, 676.4, 24.2, 27, 'd', 'glass'], [690, 691.2, 24.2, 25.8, 'd', 'barricade'], [697, 698, 24.2, 25.2, 'd', 'crate', 'plating'],
  [728, 729, 24.2, 27.4, 'd', 'pillar'], [734, 735, 24.2, 27.4, 'd', 'pillar'], [748, 749, 24.2, 25.2, 'd', 'crate', 'medkit'],
  // Undercity Descent
  [812, 812.4, 20, 23, 'd', 'glass'], [818, 819, 20, 21, 'd', 'crate', 'medkit'], [840, 841.2, 20, 21.6, 'd', 'barricade'],
  [878, 879.2, 20, 21.6, 'd', 'barricade'], [888, 889, 20, 21, 'd', 'crate', 'fury'], [917, 918, 16, 17, 'd', 'crate', 'ultcell'],
  [990, 991, 4, 5, 'd', 'crate'], [991.1, 992.1, 4, 5, 'd', 'crate', 'plating'],
  [1024, 1025.2, 0, 1.8, 'd', 'barricade'], [1028, 1029, 0, 1, 'd', 'crate'], [1029.1, 1030.1, 0, 1, 'd', 'crate', 'medkit'],
  [1046, 1047.2, 0, 1.8, 'd', 'barricade'], [1056, 1056.4, 0, 3.5, 'd', 'glass'], [1066, 1067.2, 0, 1.8, 'd', 'barricade'],
  [1072, 1073, 0, 1, 'd', 'crate', 'overclock'], [1100, 1101.2, 0, 3, 'd', 'pillar'], [1110, 1111, 0, 1, 'd', 'crate', 'ultcell'],
  [1142, 1143, 0, 3.4, 'd', 'pillar'], [1152, 1153, 0, 3.4, 'd', 'pillar'], [1168, 1169, 0, 1, 'd', 'crate', 'medkit'],
];

// Breakable pieces: hit points, the debris colour, and what can break them (glass shatters at anything; a
// pillar shrugs off small arms fire: only blows of at least `min` damage count)
export const DESTRUCT = {
  crate:     { hp: 12, min: 0, color: '#c98b4a', debris: 'wood' },
  barricade: { hp: 40, min: 0, color: '#8a97a8', debris: 'metal' },
  glass:     { hp: 1, min: 0, color: '#bfe8ff', debris: 'glass' },
  pillar:    { hp: 70, min: 3, color: '#b9c2cc', debris: 'stone' },
};

// ---- The third dimension ----
// The simulation's x runs along the path and y up; z is the lateral offset across the path (positive toward the
// side the old side-on camera sat on). Every box has a z extent: walkways are corridors HW m either side of the
// path line (wider in the arenas) with low rails along their edges; platforms, cover and set pieces are narrower,
// so they can be walked round; the walls at the ends of a route and the energy gates span all of it.
export const HW = 4.5, ARENA_HW = 6.5;
const WALKWAY = new Set(['ground', 'floor', 'roof', 'yard', 'relay', 'pad', 'crucible', 'plaza', 'top', 'ledge', 'step', 'landing', 'stair', 'bridge']);
const ARENAS = [[60, 97], [258, 318], [702, 762], [1119, 1179]];
export const laneHW = (x0, x1 = x0) => (ARENAS.some(([a, b]) => x0 >= a - 0.01 && x1 <= b + 0.01) ? ARENA_HW : HW);
// Breakable pieces stand at different places across the walkway (stacked crates share a spot)
const SPOTS = [-2.4, 1.6, -0.6, 2.4, 0.4, -1.6];
function zSpan(x0, x1, type, tag) {
  if (tag === 'bound') return [-16, 16];
  if (type === 'g') return [-ARENA_HW - 1.5, ARENA_HW + 1.5];
  if (tag === 'bridge') return [-2.2, 2.2];
  if (WALKWAY.has(tag) || tag === 'tunnel') { const h = laneHW(x0, x1); return [-h, h]; }
  if (type === 'd') {
    const o = SPOTS[Math.floor(x0 / 6) % SPOTS.length];
    if (tag === 'crate') return [o - 0.5, o + 0.5];
    if (tag === 'pillar') return [o - 0.6, o + 0.6];
    if (tag === 'glass') return [-2.5, 2.5];
    return o < 0 ? [o - 1, o + 3] : [o - 3, o + 1];   // a barricade, leaving a way round on one side
  }
  if (tag === 'ring') return [-2.6, 2.6];
  if (type === 'o') return tag === 'dais' ? [-2.6, 2.6] : [-1.8, 1.8];
  if (tag === 'column') return [-0.4, 0.4];
  if (tag === 'pillar') return [-0.5, 0.5];
  if (tag === 'panel') return [-1.6, 1.6];
  return [-2, 2];   // cover, walls, blocks
}
export const BOXES = RAW.map(([x0, x1, y0, y1, type, tag, loot], id) => {
  const [z0, z1] = zSpan(x0, x1, type, tag);
  return { id, x0, x1, y0, y1, z0, z1, type, tag, loot: loot || null, hp: type === 'd' ? DESTRUCT[tag].hp : 0, broken: false };
});
// Low rails along both edges of every walkway: they stop a knockback carrying anyone off the side (a jump clears them)
for (const b of BOXES.filter(q => WALKWAY.has(q.tag))) {
  BOXES.push({ id: BOXES.length, x0: b.x0, x1: b.x1, y0: b.y1, y1: b.y1 + 1.0, z0: b.z0, z1: b.z0 + 0.25, type: 's', tag: 'rail', loot: null, hp: 0, broken: false });
  BOXES.push({ id: BOXES.length, x0: b.x0, x1: b.x1, y0: b.y1, y1: b.y1 + 1.0, z0: b.z1 - 0.25, z1: b.z1, type: 's', tag: 'rail', loot: null, hp: 0, broken: false });
}
// Put every breakable piece back (a reset to a checkpoint, a zone load)
export function restoreBoxes() { for (const b of BOXES) if (b.type === 'd') { b.broken = false; b.hp = DESTRUCT[b.tag].hp; } }
// Left and right ends of the level: projectiles that leave this span are gone
export const LEVEL_X0 = Math.min(...BOXES.map(b => b.x0)), LEVEL_X1 = Math.max(...BOXES.map(b => b.x1));
export const GATES = { L: false, R: false, L2: false, R2: false, F1: false, F2: false, U1: false, U2: false };

export const ZONES = [
  { id: 'gym', name: 'Movement Gym', x0: -10, x1: 60, spawn: { x: 0, y: 0 } },
  { id: 'arena', name: 'Concourse Lock', x0: 60, x1: 97, spawn: { x: 58.5, y: 0 } },
  { id: 'tower', name: 'Storm Spire Climb', x0: 97, x1: 162, spawn: { x: 100, y: 0 } },
  { id: 'skyline', name: 'Skyline Relay', x0: 162, x1: 318, spawn: { x: 166, y: 15.6 } },
  { id: 'foundry', name: 'Helix Foundry', x0: 398, x1: 764, spawn: { x: 402, y: 0 } },
  { id: 'undercity', name: 'Undercity Descent', x0: 798, x1: 1181, spawn: { x: 802, y: 20 } },
];
export function zoneAt(x) {
  return ZONES.find(z => x >= z.x0 && x < z.x1) || ZONES[0];
}

export const CHECKPOINTS = [
  { x: 0, y: 0 }, { x: 58.5, y: 0 }, { x: 100, y: 0 }, { x: 152, y: 15.6 },
  { x: 166, y: 15.6 }, { x: 193, y: 15.6 }, { x: 222, y: 12.6 }, { x: 252, y: 18.6 }, { x: 302, y: 18.6 },
  // Helix Foundry, Undercity Descent
  { x: 402, y: 0 }, { x: 466, y: 0 }, { x: 586, y: 0 }, { x: 664, y: 24.2 }, { x: 704, y: 24.2 },
  { x: 802, y: 20 }, { x: 852, y: 20 }, { x: 926, y: 14.6 }, { x: 1012, y: 0 }, { x: 1121, y: 0 },
];

// Skyline Relay encounters. Each starts when a player passes `trigger`; the next wave comes when at
// most one enemy of the current wave is left, and the last wave must be cleared. `extra` joins the
// first wave with three or more players. Gated encounters seal the Relay Gate until cleared.
export const ENCOUNTERS = [
  { id: 'patrol', trigger: 191, banner: ['Skyline Relay', 'Drones inbound.'],
    waves: [[['drone', 204, 19.5], ['drone', 209, 20.5], ['swarmer', 207, 15.6], ['swarmer', 211, 15.6]]],
    extra: [['drone', 212, 19]] },
  { id: 'yard', trigger: 221, banner: ['Mortar Yard', 'Watch for the landing markers.'],
    waves: [[['mortar', 251, 18.6], ['mortar', 255.5, 18.6], ['charger', 240, 12.6], ['swarmer', 233, 12.6]]],
    extra: [['swarmer', 236, 12.6]] },
  { id: 'relay', trigger: 261, gates: ['L2', 'R2'], inside: 260, banner: ['Relay Gate', 'Gates sealed. Take the relay.'],
    waves: [
      [['shield', 286, 18.6], ['charger', 292, 18.6], ['drone', 272, 23], ['drone', 284, 25], ['sniper', 278.5, 24]],
      [['brute', 290, 18.6], ['charger', 266, 18.6], ['drone', 270, 24], ['drone', 288, 24.5], ['mortar', 294, 18.6],
        ['swarmer', 280, 18.6], ['swarmer', 284, 18.6]],
    ],
    waveBanners: [null, ['Final wave', 'The Brute holds the relay.']],
    extra: [['swarmer', 276, 18.6]],
    cleared: ['Relay secured', 'Gates open. The beacon is ahead.'] },
  // The level boss: the gunship guarding the beacon. Its gate seals behind the team.
  { id: 'beacon', trigger: 300.5, gates: ['R2'], inside: 299.5, boss: 'stormcaller', bossAt: [308, 32], banner: ['Stormcaller', 'It keeps the relay beacon.'],
    cleared: ['Beacon secured', 'The Stormcaller is down.'] },

  // ---- Helix Foundry ----
  { id: 'f-approach', trigger: 406, banner: ['Helix Foundry', 'Break through the approach.'],
    waves: [[['swarmer', 420, 0], ['swarmer', 426, 0], ['shield', 450, 1.4], ['sniper', 460, 0]]], extra: [['swarmer', 438, 0]] },
  { id: 'f-trench', trigger: 470, banner: ['The Trench', 'It sweeps round you. Watch the walkways.'],
    waves: [[['drone', 490, 8], ['drone', 501, 7], ['swarmer', 489, 4.9], ['charger', 512, 0]],
      [['shield', 532, 0], ['mortar', 556, 0], ['swarmer', 522, 0], ['swarmer', 540, 0]]],
    extra: [['drone', 496, 9]] },
  { id: 'f-helix', trigger: 590, banner: ['The Helix', 'Climb the core.'],
    waves: [[['swarmer', 602, 6.6], ['sniper', 616, 11], ['drone', 624, 16], ['swarmer', 630, 15.4]],
      [['shield', 658, 24.2], ['drone', 645, 23], ['drone', 652, 25]]],
    extra: [['sniper', 637, 17.6]] },
  { id: 'f-crucible', trigger: 710, gates: ['F1', 'F2'], inside: 709.5, banner: ['The Crucible', 'Sealed in. Hold the floor.'],
    waves: [[['shield', 740, 24.2], ['charger', 748, 24.2], ['sniper', 718, 27.8], ['drone', 730, 30]],
      [['brute', 745, 24.2], ['charger', 720, 24.2], ['swarmer', 735, 24.2], ['swarmer', 750, 24.2], ['mortar', 752, 24.2], ['drone', 742, 31]]],
    waveBanners: [null, ['Final wave', 'The foundry Brute.']], extra: [['swarmer', 726, 24.2]],
    cleared: ['Foundry secured', 'The Helix Foundry is yours.'] },

  // ---- Undercity Descent ----
  { id: 'u-roofs', trigger: 806, banner: ['Undercity Descent', 'Down through the city.'],
    waves: [[['drone', 818, 25], ['swarmer', 835, 20], ['sniper', 846, 20], ['swarmer', 830, 20]]], extra: [['drone', 840, 26]] },
  { id: 'u-vents', trigger: 856, banner: ['The Vents', 'The rooftops swing round you.'],
    waves: [[['charger', 885, 20], ['shield', 890, 20], ['drone', 870, 27]],
      [['swarmer', 880, 20], ['swarmer', 884, 20], ['mortar', 919, 16], ['sniper', 908, 18]]] },
  { id: 'u-stair', trigger: 930, banner: ['Cooling Tower', 'Down the stair.'],
    waves: [[['drone', 950, 17], ['sniper', 968, 6.2], ['swarmer', 976, 4.8], ['shield', 962, 7.6]]], extra: [['drone', 962, 14]] },
  { id: 'u-transit', trigger: 1016, banner: ['Transit Line', 'Through the barricades.'],
    waves: [[['shield', 1040, 0], ['swarmer', 1030, 0], ['swarmer', 1034, 0], ['charger', 1060, 0], ['drone', 1050, 7]],
      [['brute', 1078, 0], ['sniper', 1036, 4.0], ['mortar', 1082, 0], ['swarmer', 1070, 0]]],
    extra: [['drone', 1044, 8]] },
  { id: 'u-plaza', trigger: 1126, gates: ['U1', 'U2'], inside: 1125.5, banner: ['The Plaza', 'Last stand at the bottom.'],
    waves: [[['shield', 1150, 0], ['charger', 1160, 0], ['drone', 1140, 7], ['drone', 1156, 8], ['sniper', 1162, 3.8]],
      [['brute', 1145, 0], ['brute', 1166, 0], ['swarmer', 1135, 0], ['swarmer', 1158, 0], ['mortar', 1171, 0]]],
    waveBanners: [null, ['Final wave', 'Two Brutes.']], extra: [['swarmer', 1150, 0]],
    cleared: ['Undercity secured', 'You made it to the bottom.'] },
];
// Which route an encounter belongs to (its trigger only counts for players on that route)
for (const E of ENCOUNTERS) E.route = routeAt(E.trigger).id;

// Power-ups waiting along the routes (more drop from broken crates): [x, y, kind]
export const LEVEL_PICKUPS = [
  [116, 2.5, 'medkit'], [150, 15.6, 'overclock'], [196, 15.6, 'plating'], [254, 18.6, 'medkit'],
  [403, 0, 'overclock'], [468, 0, 'plating'], [489, 4.9, 'ultcell'], [586, 0, 'medkit'], [617.5, 11, 'overclock'], [659, 24.2, 'medkit'],
  [700, 24.2, 'fury'], [705, 24.2, 'plating'],
  [804, 20, 'overclock'], [865, 23.4, 'plating'], [927, 14.6, 'medkit'], [1013, 0, 'fury'], [1036, 4.0, 'ultcell'], [1090, 0, 'medkit'], [1122, 0, 'plating'],
];

// Lift pads on the floor (Helix Foundry): anyone who comes down on one is thrown straight up to `top` (the
// platform overhead), so a missed jump on the helix is a bounce back up, not a long walk back: [x, y, top]
export const LIFTS = [[593, 0, 8.8], [607, 0, 13.2], [621, 0, 17.6], [635, 0, 22], [649, 0, 24.2]];
export const LIFT_HW = 2.6;   // how far across the floor each lift pad reaches (the helix platforms over them are as deep)

// How low counts as falling out (a fall below this brings you back): per stretch of the routes
const KILL = [[662, 764, 14], [800, 924, 6]];
export function killYAt(x) { for (const [a, b, y] of KILL) if (x >= a && x < b) return y; return KILL_Y; }
export const ROUTE_END_X = 304;   // (the Skyport route's; each route has its own endX in ROUTES)

export const KILL_Y = -10;
export const ARENA_TRIGGER_X = 64;
export const TOWER_TRIGGER_X = 108;

function isSolid(b) {
  return b.type === 's' || (b.type === 'g' && GATES[b.tag]) || (b.type === 'd' && !b.broken);
}
// The breakable piece at a point, if any
export function breakableAt(x, y, z = 0, pad = 0) {
  for (const b of BOXES) if (b.type === 'd' && !b.broken && x > b.x0 - pad && x < b.x1 + pad && y > b.y0 - pad && y < b.y1 + pad && z > b.z0 - pad && z < b.z1 + pad) return b;
  return null;
}

function overlaps(x0, x1, y0, y1, z0, z1, b) {
  return x0 < b.x1 && x1 > b.x0 && y0 < b.y1 && y1 > b.y0 && z0 < b.z1 && z1 > b.z0;
}

// Body: { x (centre), y (feet), z (centre), w (footprint: w by w), h, vx, vy, vz }. Sets onGround, wallDir (with
// wallX, wallZ: the way into the wall touched), hitWall (non-zero when a wall stopped it: ±1 along x, ±2 along z)
// and hitCeil.
export function moveBody(body, dt) {
  const hw = body.w / 2;
  if (body.z === undefined) body.z = 0;
  if (body.vz === undefined) body.vz = 0;
  body.hitWall = 0; body.hitCeil = false;
  // Along the path
  body.x += body.vx * dt;
  for (const b of BOXES) {
    if (!isSolid(b)) continue;
    if (overlaps(body.x - hw, body.x + hw, body.y, body.y + body.h, body.z - hw, body.z + hw, b)) {
      if (body.x < (b.x0 + b.x1) / 2) { body.x = b.x0 - hw - 1e-4; body.hitWall = 1; }
      else { body.x = b.x1 + hw + 1e-4; body.hitWall = -1; }
      body.vx = 0;
    }
  }
  // Across it
  body.z += body.vz * dt;
  for (const b of BOXES) {
    if (!isSolid(b)) continue;
    if (overlaps(body.x - hw, body.x + hw, body.y, body.y + body.h, body.z - hw, body.z + hw, b)) {
      if (body.z < (b.z0 + b.z1) / 2) { body.z = b.z0 - hw - 1e-4; body.hitWall = body.hitWall || 2; }
      else { body.z = b.z1 + hw + 1e-4; body.hitWall = body.hitWall || -2; }
      body.vz = 0;
    }
  }
  // Vertical
  const prevY = body.y;
  body.y += body.vy * dt;
  body.onGround = false;
  for (const b of BOXES) {
    const solid = isSolid(b);
    if (!solid && b.type !== 'o') continue;
    if (!overlaps(body.x - hw, body.x + hw, body.y, body.y + body.h, body.z - hw, body.z + hw, b)) continue;
    if (b.type === 'o') {
      if (body.vy <= 0 && prevY >= b.y1 - 0.02 && !(body.dropT > 0)) {
        body.y = b.y1; body.vy = 0; body.onGround = true;
      }
      continue;
    }
    if (body.vy <= 0 && prevY >= b.y1 - 0.08) {
      body.y = b.y1; body.vy = 0; body.onGround = true;
    } else if (body.vy > 0) {
      body.y = b.y0 - body.h - 1e-4; body.vy = 0; body.hitCeil = true;
    } else {
      // Embedded (e.g. stood up under a ceiling): push toward the nearer vertical side
      const up = b.y1 - body.y, down = body.y + body.h - b.y0;
      if (up < down) { body.y = b.y1; body.onGround = true; } else { body.y = b.y0 - body.h; }
      body.vy = 0;
    }
  }
  // Wall contact probe (for wall cling while airborne). Gates are energy barriers and rails are too low: nothing
  // to cling to.
  body.wallDir = 0; body.wallX = 0; body.wallZ = 0;
  if (!body.onGround) {
    for (const b of BOXES) {
      if (!isSolid(b) || b.type === 'g' || b.tag === 'rail') continue;
      const ya = body.y + 0.3, yb = body.y + body.h - 0.2;
      if (!(ya < b.y1 && yb > b.y0)) continue;
      const inZ = body.z + hw > b.z0 + 0.05 && body.z - hw < b.z1 - 0.05, inX = body.x + hw > b.x0 + 0.05 && body.x - hw < b.x1 - 0.05;
      if (inZ && Math.abs(body.x + hw - b.x0) < 0.06) { body.wallDir = 1; body.wallX = 1; body.wallZ = 0; }
      else if (inZ && Math.abs(body.x - hw - b.x1) < 0.06) { body.wallDir = -1; body.wallX = -1; body.wallZ = 0; }
      else if (inX && Math.abs(body.z + hw - b.z0) < 0.06) { body.wallDir = 2; body.wallX = 0; body.wallZ = 1; }
      else if (inX && Math.abs(body.z - hw - b.z1) < 0.06) { body.wallDir = -2; body.wallX = 0; body.wallZ = -1; }
    }
  }
}

// Can a body of height h (and footprint w) stand at (x, y, z)?
export function hasHeadroom(x, y, z, w, h) {
  const hw = w / 2;
  for (const b of BOXES) {
    if (!isSolid(b)) continue;
    if (overlaps(x - hw, x + hw, y + 0.05, y + h, z - hw, z + hw, b)) return false;
  }
  return true;
}

// The highest floor (solid or one-way top) under a point, at or below y
export function groundBelow(x, y, z = 0) {
  let best = -Infinity;
  for (const b of BOXES) {
    if (!(isSolid(b) || b.type === 'o')) continue;
    if (x > b.x0 && x < b.x1 && z > b.z0 && z < b.z1 && b.y1 <= y + 0.01 && b.y1 > best) best = b.y1;
  }
  return best;
}

// Slab test of a ray from (x, y, z) along (dx, dy, dz) against a box (x0..x1, y0..y1, z0..z1): the entry distance
// and the face normal hit, or null. A ray that starts inside the box enters at 0.
export function rayBoxT(x, y, z, dx, dy, dz, b) {
  let tin = -Infinity, tout = Infinity, nx = 0, ny = 0, nz = 0;
  const axis = (o, d, lo, hi, set) => {
    if (Math.abs(d) < 1e-9) return o > lo && o < hi;
    const ta = (lo - o) / d, tb = (hi - o) / d, tn = Math.min(ta, tb);
    if (tn > tin) { tin = tn; set(d > 0 ? -1 : 1); }
    tout = Math.min(tout, Math.max(ta, tb));
    return true;
  };
  if (!axis(x, dx, b.x0, b.x1, s => { nx = s; ny = 0; nz = 0; })) return null;
  if (!axis(y, dy, b.y0, b.y1, s => { nx = 0; ny = s; nz = 0; })) return null;
  if (!axis(z, dz, b.z0 ?? -Infinity, b.z1 ?? Infinity, s => { nx = 0; ny = 0; nz = s; })) return null;
  if (tin > tout || tout < 0) return null;
  return { t: Math.max(0, tin), nx, ny, nz };
}

// Segment-vs-solid test for line of sight
export function segmentBlocked(ax, ay, az, bx, by, bz) {
  const dx = bx - ax, dy = by - ay, dz = bz - az, L = Math.hypot(dx, dy, dz);
  if (L < 1e-9) return pointInSolid(ax, ay, az);
  for (const b of BOXES) {
    if (!isSolid(b) || b.tag === 'rail') continue;   // (a rail is too low to hide behind)
    const h = rayBoxT(ax, ay, az, dx / L, dy / L, dz / L, b);
    if (h && h.t <= L) return true;
  }
  return false;
}

// The first solid surface along a ray (unit direction), up to `range` m: where it is, how far, and the surface
// normal there (wall: false when nothing is hit within range)
export function rayCast(x, y, z, dx, dy, dz, range) {
  let best = range, nx = 0, ny = 0, nz = 0, box = null;
  for (const b of BOXES) {
    if (!isSolid(b)) continue;
    const h = rayBoxT(x, y, z, dx, dy, dz, b);
    if (h && h.t < best) { best = h.t; nx = h.nx; ny = h.ny; nz = h.nz; box = b; }
  }
  return { t: best, x: x + dx * best, y: y + dy * best, z: z + dz * best, nx, ny, nz, wall: best < range, box };
}

export function pointInSolid(x, y, z = 0) {
  for (const b of BOXES) {
    if (isSolid(b) && x > b.x0 && x < b.x1 && y > b.y0 && y < b.y1 && z > b.z0 && z < b.z1) return true;
  }
  return false;
}
