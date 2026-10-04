// Enemy construct rigs: pale ceramic plating, graphite joints, reserved hostile magenta energy.
import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { HOSTILE } from './config.js';
import { addRim } from './rigs.js';

const rbox = (w, h, d, r = 0.06) => new RoundedBoxGeometry(w, h, d, 3, Math.min(r, w / 2 - 1e-3, h / 2 - 1e-3, d / 2 - 1e-3));
const cap = (r, len) => new THREE.CapsuleGeometry(r, len, 4, 10);

function mats() {
  return {
    plate: new THREE.MeshPhysicalMaterial({ color: 0xe6e9f0, roughness: 0.36, metalness: 0.15, clearcoat: 0.5, clearcoatRoughness: 0.2, emissive: 0xffffff, emissiveIntensity: 0 }),
    joint: new THREE.MeshStandardMaterial({ color: 0x2b2f3a, roughness: 0.6, metalness: 0.2 }),
    energy: new THREE.MeshStandardMaterial({ color: HOSTILE, emissive: HOSTILE, emissiveIntensity: 2.2, roughness: 0.3 }),
  };
}
function rimAll(M) { addRim(M.plate, '#ff9cc5', 0.35); addRim(M.joint, '#ff7fb2', 0.3); return M; }
function add(parent, geo, mat, x = 0, y = 0, z = 0) {
  const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); m.castShadow = true; parent.add(m); return m;
}
function grp(parent, x = 0, y = 0, z = 0) { const g = new THREE.Group(); g.position.set(x, y, z); parent.add(g); return g; }

export function buildEnemyRig(e) {
  const M = rimAll(mats());
  const root = new THREE.Group(), flip = grp(root), body = grp(flip);
  const R = { root, flip, body, mats: M, type: e.type, parts: {}, lean: 0, bob: 0 };
  const P = R.parts;
  switch (e.type) {
    case 'swarmer': {
      P.core = grp(body, 0, 0.42, 0);
      add(P.core, rbox(0.62, 0.42, 0.52, 0.16), M.plate);
      add(P.core, rbox(0.3, 0.12, 0.54, 0.05), M.joint, -0.05, 0.2);
      P.eye = add(P.core, new THREE.SphereGeometry(0.11, 14, 10), M.energy, 0.29, 0.03);
      for (const [x, z] of [[0.18, 0.2], [-0.18, 0.2], [0.18, -0.2], [-0.18, -0.2]]) {
        const l = add(body, cap(0.045, 0.3), M.joint, x, 0.2, z); l.rotation.z = x > 0 ? -0.5 : 0.5;
      }
      break;
    }
    case 'shield': {
      for (const z of [0.16, -0.16]) add(body, cap(0.09, 0.62), M.joint, 0, 0.42, z);
      P.torso = grp(body, 0, 1.0, 0);
      add(P.torso, rbox(0.55, 0.78, 0.6, 0.12), M.plate, 0, 0.25);
      P.eye = add(P.torso, rbox(0.06, 0.07, 0.3, 0.02), M.energy, 0.28, 0.58);
      P.shield = grp(P.torso, 0.55, 0.1, 0);
      add(P.shield, rbox(0.14, 1.55, 1.05, 0.08), M.plate);
      for (const y of [-0.74, 0.74]) add(P.shield, rbox(0.16, 0.05, 1.0, 0.02), M.energy, 0.02, y);
      for (const z of [-0.5, 0.5]) add(P.shield, rbox(0.16, 1.45, 0.05, 0.02), M.energy, 0.02, 0, z);
      break;
    }
    case 'sniper': {
      for (const z of [0.12, -0.12]) add(body, cap(0.07, 0.7), M.joint, 0, 0.45, z);
      P.torso = grp(body, 0, 1.05, 0);
      add(P.torso, rbox(0.4, 0.62, 0.46, 0.1), M.plate, 0, 0.2);
      P.eye = add(P.torso, new THREE.SphereGeometry(0.07, 12, 10), M.energy, 0.2, 0.52);
      P.gun = grp(P.torso, 0.05, 0.3, 0.26);
      const barrel = add(P.gun, new THREE.CylinderGeometry(0.05, 0.06, 1.5, 10), M.joint, 0.75, 0, 0); barrel.rotation.z = Math.PI / 2;
      add(P.gun, rbox(0.4, 0.14, 0.12, 0.03), M.plate, 0.1, 0, 0);
      P.muzzle = add(P.gun, new THREE.SphereGeometry(0.06, 10, 8), M.energy, 1.52, 0, 0);
      break;
    }
    case 'brute': {
      for (const z of [0.36, -0.36]) add(body, cap(0.2, 0.8), M.joint, 0, 0.62, z);
      P.torso = grp(body, 0, 1.4, 0);
      add(P.torso, rbox(1.1, 1.05, 1.1, 0.25), M.joint, 0, 0.35);
      P.core = add(P.torso, new THREE.SphereGeometry(0.22, 16, 12), M.energy, 0.5, 0.4);
      P.plates = [
        add(P.torso, rbox(0.3, 0.8, 0.95, 0.12), M.plate, 0.52, 0.35),
        add(P.torso, rbox(0.7, 0.3, 0.45, 0.12), M.plate, 0, 0.98, 0.42),
        add(P.torso, rbox(0.7, 0.3, 0.45, 0.12), M.plate, 0, 0.98, -0.42),
      ];
      P.head = add(P.torso, rbox(0.34, 0.3, 0.36, 0.1), M.plate, 0.3, 1.02);
      add(P.head, rbox(0.06, 0.06, 0.28, 0.02), M.energy, 0.17, 0.02);
      P.armN = grp(P.torso, 0, 0.8, 0.72); P.armF = grp(P.torso, 0, 0.8, -0.72);
      for (const a of [P.armN, P.armF]) {
        add(a, cap(0.17, 0.75), M.joint, 0, -0.45);
        add(a, rbox(0.5, 0.45, 0.45, 0.14), M.plate, 0.05, -1.0);
      }
      break;
    }
    case 'post': {
      add(body, new THREE.CylinderGeometry(0.5, 0.6, 0.25, 20), M.joint, 0, 0.12);
      add(body, cap(0.2, 1.4), M.plate, 0, 1.0);
      P.arm = grp(body, 0, 1.45, 0.3);
      add(P.arm, rbox(1.3, 0.2, 0.2, 0.06), M.plate, 0.6, 0);
      P.eye = add(body, new THREE.SphereGeometry(0.12, 14, 10), M.energy, 0.18, 1.8);
      break;
    }
    case 'turret': {
      add(body, new THREE.SphereGeometry(0.42, 18, 12, 0, Math.PI * 2, 0, Math.PI / 2), M.plate, 0, 0);
      P.gun = grp(body, 0, 0.25, 0);
      const b = add(P.gun, new THREE.CylinderGeometry(0.07, 0.09, 0.8, 10), M.joint, 0.4, 0, 0); b.rotation.z = Math.PI / 2;
      P.eye = add(P.gun, new THREE.SphereGeometry(0.07, 10, 8), M.energy, 0.82, 0, 0);
      break;
    }
    case 'drone': {   // a flattened ceramic pod under a spinning rotor ring, one magenta eye
      P.core = grp(body, 0, 0.3, 0);
      add(P.core, new THREE.SphereGeometry(0.3, 18, 12), M.plate).scale.set(1.2, 0.72, 1.2);
      P.eye = add(P.core, new THREE.SphereGeometry(0.1, 12, 10), M.energy, 0.31, -0.02);
      add(P.core, rbox(0.5, 0.04, 0.05, 0.01), M.energy, 0, -0.2);                                 // underglow
      for (const z of [0.26, -0.26]) add(P.core, rbox(0.3, 0.12, 0.05, 0.02), M.joint, -0.12, -0.06, z);   // fins
      P.rotor = grp(P.core, 0, 0.24, 0);
      const ring = add(P.rotor, new THREE.TorusGeometry(0.44, 0.035, 6, 28), M.joint); ring.rotation.x = Math.PI / 2;
      for (let i = 0; i < 3; i++) { const blade = add(P.rotor, rbox(0.82, 0.02, 0.08, 0.01), M.plate); blade.rotation.y = i * Math.PI / 3; }
      break;
    }
    case 'mortar': {  // squat emplacement with a tilted tube; the rim glows when it fires
      add(body, new THREE.CylinderGeometry(0.56, 0.64, 0.3, 20), M.joint, 0, 0.15);
      P.torso = grp(body, 0, 0.55, 0);
      add(P.torso, rbox(0.82, 0.5, 0.82, 0.14), M.plate);
      P.eye = add(P.torso, rbox(0.05, 0.07, 0.5, 0.02), M.energy, 0.41, 0.06);
      P.tube = grp(P.torso, 0.05, 0.22, 0);
      add(P.tube, new THREE.CylinderGeometry(0.17, 0.21, 0.95, 14), M.plate, 0, 0.47);
      P.muzzle = add(P.tube, new THREE.TorusGeometry(0.17, 0.045, 6, 16), M.energy, 0, 0.95); P.muzzle.rotation.x = Math.PI / 2;
      P.tube.rotation.z = -0.45;
      break;
    }
    case 'charger': { // low, wide, four-legged; a ram plate with magenta slits and two energy horns
      P.legs = [];
      for (const [x, z] of [[0.38, 0.3], [-0.38, 0.3], [0.38, -0.3], [-0.38, -0.3]]) {
        const l = grp(body, x, 0.62, z); add(l, cap(0.09, 0.42), M.joint, 0, -0.3); P.legs.push(l);
      }
      P.torso = grp(body, 0, 0.9, 0);
      add(P.torso, rbox(1.1, 0.66, 0.82, 0.2), M.joint, -0.05, 0);
      P.ram = add(P.torso, rbox(0.3, 0.78, 0.92, 0.12), M.plate, 0.56, 0.02);
      for (const y of [-0.12, 0.16]) add(P.ram, rbox(0.04, 0.05, 0.6, 0.02), M.energy, 0.16, y);
      P.horns = [];
      for (const z of [0.3, -0.3]) { const h = add(P.torso, new THREE.ConeGeometry(0.07, 0.4, 6), M.energy, 0.66, 0.46, z); h.rotation.z = -1.0; P.horns.push(h); }
      P.plates = [add(P.torso, rbox(0.72, 0.16, 0.86, 0.07), M.plate, -0.12, 0.4)];   // armor plate: gone once broken
      break;
    }
    case 'warden': {  // the Lockwarden: a hulking walker, a hammer fist and a blade arm, a missile pod, four armor plates
      P.legs = [];
      for (const z of [0.5, -0.5]) {
        const hip = grp(body, 0, 1.55, z), knee = grp(hip, 0.05, -0.75, 0);
        add(hip, cap(0.24, 0.55), M.joint, 0, -0.38); add(hip, rbox(0.5, 0.55, 0.42, 0.14), M.plate, 0.08, -0.3);
        add(knee, cap(0.2, 0.5), M.joint, 0, -0.35); add(knee, rbox(0.42, 0.5, 0.38, 0.12), M.plate, 0.12, -0.35);
        add(knee, rbox(0.8, 0.22, 0.5, 0.08), M.joint, 0.12, -0.72);   // foot
        P.legs.push({ hip, knee });
      }
      P.torso = grp(body, 0, 1.65, 0);
      add(P.torso, rbox(1.25, 0.4, 1.0, 0.14), M.joint, 0, 0);                       // pelvis
      P.chest = grp(P.torso, 0, 0.3, 0);
      add(P.chest, rbox(1.5, 1.05, 1.3, 0.3), M.joint, 0, 0.55);
      P.core = add(P.chest, new THREE.SphereGeometry(0.26, 18, 14), M.energy, 0.72, 0.55);
      P.head = grp(P.chest, 0.42, 1.2, 0);
      add(P.head, rbox(0.55, 0.38, 0.5, 0.12), M.plate);
      P.eye = add(P.head, rbox(0.06, 0.08, 0.42, 0.02), M.energy, 0.28, 0.02);
      P.pod = grp(P.chest, -0.7, 1.05, 0);
      add(P.pod, rbox(0.6, 0.55, 0.9, 0.1), M.plate);
      P.tubes = [];
      for (let i = 0; i < 5; i++) { const t = add(P.pod, new THREE.CylinderGeometry(0.07, 0.07, 0.06, 10), M.energy, -0.05 + (i % 2) * 0.12, 0.29, -0.3 + i * 0.15); P.tubes.push(t); }
      // Armor: chest plate, two pauldrons, head crest (hidden as they break)
      P.plates = [add(P.chest, rbox(0.3, 0.8, 1.05, 0.12), M.plate, 0.72, 0.45), add(P.chest, rbox(0.8, 0.35, 0.55, 0.14), M.plate, 0, 1.12, 0.62),
        add(P.chest, rbox(0.8, 0.35, 0.55, 0.14), M.plate, 0, 1.12, -0.62), add(P.head, rbox(0.4, 0.14, 0.3, 0.05), M.plate, -0.05, 0.25)];
      // Near arm: the hammer; far arm: the blade
      P.armN = grp(P.chest, 0.05, 0.95, 0.95); P.armF = grp(P.chest, 0.05, 0.95, -0.95);
      add(P.armN, cap(0.2, 0.7), M.joint, 0, -0.45); P.hammer = grp(P.armN, 0.05, -1.05, 0);
      add(P.hammer, rbox(0.85, 0.72, 0.72, 0.16), M.plate); add(P.hammer, rbox(0.06, 0.5, 0.6, 0.02), M.energy, 0.44, 0);
      add(P.armF, cap(0.18, 0.65), M.joint, 0, -0.42);
      P.blade = add(P.armF, rbox(0.22, 1.7, 0.1, 0.04), M.plate, 0.1, -1.4);
      add(P.blade, rbox(0.05, 1.6, 0.12, 0.02), M.energy, 0.12, 0);
      break;
    }
    case 'stormcaller': {  // the Stormcaller: a gunship; wide ceramic hull, magenta eye and chin cannon, two rotor nacelles
      P.hull = grp(body, 0, 0.75, 0);
      add(P.hull, new THREE.SphereGeometry(0.62, 24, 16), M.plate).scale.set(2.3, 0.72, 1.25);
      add(P.hull, new THREE.SphereGeometry(0.5, 20, 12), M.joint, -0.1, -0.2).scale.set(2.2, 0.55, 1.1);
      P.eye = add(P.hull, new THREE.SphereGeometry(0.2, 16, 12), M.energy, 1.28, 0.02);
      add(P.hull, rbox(1.8, 0.05, 0.05, 0.02), M.energy, 0.1, 0.18, 0.62); add(P.hull, rbox(1.8, 0.05, 0.05, 0.02), M.energy, 0.1, 0.18, -0.62);
      P.cannon = grp(P.hull, 0.8, -0.38, 0);
      const barrel = add(P.cannon, new THREE.CylinderGeometry(0.09, 0.12, 0.9, 12), M.joint, 0.45, 0, 0); barrel.rotation.z = Math.PI / 2;
      P.muzzle = add(P.cannon, new THREE.SphereGeometry(0.08, 10, 8), M.energy, 0.92, 0, 0);
      for (const z of [0.5, -0.5]) { const fin = add(P.hull, rbox(0.55, 0.35, 0.05, 0.03), M.plate, -1.25, 0.28, z * 0.5); fin.rotation.z = 0.5; }
      P.rotors = [];
      for (const z of [1.1, -1.1]) {
        const nac = grp(P.hull, -0.15, 0.05, z);
        add(nac, new THREE.CylinderGeometry(0.28, 0.34, 0.3, 16), M.joint);
        add(nac, new THREE.CylinderGeometry(0.2, 0.2, 0.04, 16), M.energy, 0, -0.17);
        const rotor = grp(nac, 0, 0.2, 0);
        const ring = add(rotor, new THREE.TorusGeometry(0.62, 0.04, 6, 32), M.joint); ring.rotation.x = Math.PI / 2;
        for (let i = 0; i < 3; i++) { const b = add(rotor, rbox(1.2, 0.02, 0.1, 0.01), M.plate); b.rotation.y = i * Math.PI / 3; }
        P.rotors.push(rotor);
      }
      // Phase two's storm shield: a faceted magenta shell while it has armor
      const shieldMat = new THREE.MeshStandardMaterial({ color: HOSTILE, emissive: HOSTILE, emissiveIntensity: 1.6, transparent: true, opacity: 0.22, flatShading: true, depthWrite: false, side: THREE.DoubleSide });
      P.shield = new THREE.Mesh(new THREE.IcosahedronGeometry(1, 1), shieldMat); P.shield.scale.set(1.9, 0.95, 1.4); P.shield.visible = false; P.hull.add(P.shield);
      P.shieldMat = shieldMat;
      break;
    }
  }
  return R;
}

export function animateEnemy(R, e, dt, t) {
  const P = R.parts, M = R.mats;
  R.flip.scale.x = e.type === 'shield' ? e.shieldDir : e.facing;
  let lean = 0, glow = 2.2;
  const s = e.state;
  if (s === 'windup' || s === 'slamWindup' || s === 'aim') {
    lean = -0.18; glow = 3.5 + Math.sin(t * 30) * 1.2;
    if (s === 'slamWindup') { lean = -0.3; glow = 5 + Math.sin(t * 45) * 2; R.body.position.x = Math.sin(t * 70) * 0.03; }
  } else if (s === 'lock') { glow = 6; }
  else if (s === 'attack') lean = 0.35;
  else if (s === 'charge') { lean = 0.28; glow = 5; }
  else if (s === 'dazed') { lean = -0.3 + Math.sin(t * 7) * 0.1; glow = 0.5; }
  else if (s === 'stagger') { lean = -0.35 + Math.sin(t * 9) * 0.12; glow = 0.6; }
  else if (s === 'hitstun') lean = -0.2;
  else if (s === 'launched') lean = Math.sin(t * 12) * 0.5;
  else if (s === 'caught') lean = 0.3;
  if (s !== 'slamWindup') R.body.position.x = 0;
  R.lean += (lean - R.lean) * 0.35;
  R.body.rotation.z = -R.lean;
  M.energy.emissiveIntensity = glow;
  M.plate.emissiveIntensity = e.flash > 0 ? 0.9 : 0;

  if (e.type === 'swarmer') {
    const moving = Math.abs(e.vx) > 0.5 && e.onGround;
    R.bob = moving ? Math.abs(Math.sin(t * 16)) * 0.12 : R.bob * 0.8;
    P.core.position.y = 0.42 + R.bob + (s === 'windup' ? -0.1 : 0);
  } else if (e.type === 'sniper') {
    if (s === 'aim' || s === 'lock') {
      const dx = (e.aimX - e.x) * e.facing, dy = e.aimY - (e.y + 1.35);
      P.gun.rotation.z = Math.atan2(dy, Math.max(0.1, dx));
    } else P.gun.rotation.z *= 0.9;
  } else if (e.type === 'brute') {
    P.plates.forEach((pl, i) => { pl.visible = i < e.armor; });
    const swing = s === 'windup' ? -1.2 : s === 'attack' ? 1.4 : s === 'slamWindup' ? -2.6 : s === 'slamRecover' && e.st < 8 ? 1.2 : 0;
    P.armN.rotation.z += (swing - P.armN.rotation.z) * 0.3;
    P.armF.rotation.z += ((s === 'slamWindup' ? -2.6 : s === 'slamRecover' && e.st < 8 ? 1.2 : 0.1) - P.armF.rotation.z) * 0.3;
    M.energy.emissiveIntensity = glow + (3 - e.armor) * 0.8;
  } else if (e.type === 'post') {
    const target = s === 'windup' ? -0.9 : s === 'attack' ? 1.1 : 0;
    P.arm.rotation.z += (target - P.arm.rotation.z) * 0.3;
  } else if (e.type === 'drone') {
    P.rotor.rotation.y += dt * (s === 'windup' ? 40 : 22);
    P.core.rotation.z = -Math.max(-0.4, Math.min(0.4, e.vx * 0.06 * e.facing));
  } else if (e.type === 'mortar') {
    const raise = s === 'windup' ? Math.min(1, e.st / 20) : 0, kick = s === 'recover' && e.st < 10 ? 1 - e.st / 10 : 0;
    P.tube.rotation.z = -0.45 + raise * 0.25 - kick * 0.2;
    P.tube.scale.y = 1 - kick * 0.18;
  } else if (e.type === 'charger') {
    P.plates.forEach(pl => { pl.visible = e.armor > 0; });
    const run = s === 'charge' ? 26 : Math.abs(e.vx) > 0.5 ? 12 : 0, paw = s === 'windup' ? Math.sin(t * 22) * 0.35 : 0;
    P.legs.forEach((l, i) => { l.rotation.z = run ? Math.sin(t * run + i * Math.PI / 2) * 0.55 : i === 0 ? paw : 0; });
  } else if (e.type === 'turret') {
    const tg = e.target;
    if (tg) { const a = Math.atan2(tg.y + 1 - (e.y + 0.25), (tg.x - e.x) * e.facing); P.gun.rotation.z += (a - P.gun.rotation.z) * 0.2; }
  }

  if (e.type === 'warden') animateWarden(R, e, dt, t);
  else if (e.type === 'stormcaller') animateStorm(R, e, dt, t);

  if (e.dead && e.boss) {
    // A boss shudders and sags through its explosions, then goes in the last blast
    const k = Math.min(1, e.deathT / 68), gone = Math.max(0, (e.deathT - 68) / 8);
    R.body.position.x = Math.sin(t * 60) * 0.06 * (1 - gone); R.body.rotation.z = -0.35 * k + Math.sin(t * 23) * 0.04;
    R.root.scale.setScalar(Math.max(0.01, 1 - Math.min(1, gone)));
    M.energy.emissiveIntensity = 4 + Math.sin(t * 40) * 3; M.plate.emissiveIntensity = Math.max(0, Math.sin(t * 25)) * 0.8;
  } else if (e.dead) {
    const k = Math.min(1, e.deathT / 40);
    R.root.scale.setScalar(Math.max(0.01, 1 - k * 0.9));
    R.body.rotation.z = -0.8 * k;
  }
}

const ease = (v, target, k) => v + (target - v) * k;

// The Lockwarden: walks with heavy, bobbing strides; each attack has its own windup and strike pose
function animateWarden(R, e, dt, t) {
  const P = R.parts, M = R.mats, s = e.state, A = e.atk, k = Math.min(1, dt * 14), kind = A && A.kind;
  P.plates.forEach((pl, i) => { pl.visible = i < e.armor; });
  const walking = (s === 'idle' || s === 'approach') && Math.abs(e.vx) > 0.4;
  R.phase = (R.phase || 0) + (walking ? dt * Math.abs(e.vx) * 1.6 : 0);
  const sw = walking ? Math.sin(R.phase) : 0;
  let hipN = sw * 0.45, hipF = -sw * 0.45, knN = -Math.max(0, -Math.cos(R.phase)) * 0.6 * (walking ? 1 : 0), knF = -Math.max(0, Math.cos(R.phase)) * 0.6 * (walking ? 1 : 0);
  let armN = 0.15, armF = -0.1, twist = 0, crouch = walking ? Math.abs(Math.cos(R.phase)) * 0.08 : 0, lean = 0, head = 0, pod = 0;
  const w = s === 'windup', u = w && A ? Math.min(1, e.st / Math.max(1, A.wind)) : 0;
  if (s === 'intro' && !e.onGround) { hipN = 0.3; hipF = -0.2; knN = -0.3; knF = -0.4; armN = -0.6; armF = -0.6; }
  else if (s === 'intro' && e.st < 140) { crouch = Math.max(0, 0.5 - e.st * 0.01); armN = 0.9; armF = 0.9; }
  else if (s === 'roar') { armN = -1.9; armF = -1.9; head = -0.4; lean = -0.2; twist = Math.sin(t * 30) * 0.03; }
  else if (s === 'dazed' || s === 'stagger') { lean = 0.35 + Math.sin(t * 6) * 0.08; head = 0.5; armN = 0.4; armF = 0.3; crouch = 0.25; }
  else if (kind === 'sweep') { if (w) { armF = -1.5 * u; twist = 0.5 * u; } else if (s === 'attack') { armF = 1.5; twist = -0.55; } else { armF = 0.9; twist = -0.3; } }
  else if (kind === 'hammer') { if (w) { armN = -2.7 * u; lean = -0.15 * u; } else if (s === 'attack') { armN = 1.35; lean = 0.35; crouch = 0.2; } else { armN = 1.1; lean = 0.25; crouch = 0.15; } }
  else if (kind === 'stomp') {
    if (w) { crouch = 0.45 * u; armN = armF = -0.8 * u; }
    else if (s === 'jump') { hipN = hipF = 0.9; knN = knF = -1.4; armN = armF = -1.2; }
    else { crouch = Math.max(0, 0.35 - e.st * 0.02); armN = armF = 0.6; }
  } else if (kind === 'missiles') { lean = 0.25 * (w ? u : 1); pod = w ? u : Math.max(0, 1 - e.st / 12); }
  else if (kind === 'charge' || s === 'charge') { lean = 0.5; armN = 0.9; armF = -0.9; crouch = 0.15; if (s === 'charge') { hipN = Math.sin(t * 20) * 0.7; hipF = -hipN; } }
  else if (kind === 'laser') { head = A && A.high ? -0.15 : 0.35; lean = 0.1; }
  const L = P.legs;
  L[0].hip.rotation.z = ease(L[0].hip.rotation.z, hipN, k); L[1].hip.rotation.z = ease(L[1].hip.rotation.z, hipF, k);
  L[0].knee.rotation.z = ease(L[0].knee.rotation.z, knN, k); L[1].knee.rotation.z = ease(L[1].knee.rotation.z, knF, k);
  P.armN.rotation.z = ease(P.armN.rotation.z, armN, s === 'attack' ? Math.min(1, dt * 30) : k);
  P.armF.rotation.z = ease(P.armF.rotation.z, armF, s === 'attack' ? Math.min(1, dt * 30) : k);
  P.chest.rotation.y = ease(P.chest.rotation.y, twist, k); P.head.rotation.z = ease(P.head.rotation.z, -head, k);
  P.torso.position.y = 1.65 - crouch; for (const l of L) l.hip.position.y = 1.55 - crouch;
  R.body.rotation.z = ease(R.body.rotation.z, -lean, k);
  // Its energy: the core brightens as it weakens; windups and the laser blaze; the pod tubes glow before a volley
  const hurt = 1 - e.hp / e.maxHp, charging = w || s === 'laser' || s === 'charge' || s === 'roar';
  M.energy.emissiveIntensity = 2.2 + hurt * 2 + (charging ? 2.5 + Math.sin(t * 34) * 1.2 : 0) + (s === 'dazed' || s === 'stagger' ? -1.6 : 0);
  P.eye.scale.set(1, 1, s === 'laser' ? 1.3 : 1); P.eye.scale.y = kind === 'laser' && (w || s === 'laser') ? 1.8 : 1;
  P.tubes.forEach(tb => { tb.scale.set(1 + pod * 0.6, 1 + pod * 3, 1 + pod * 0.6); });
  P.core.scale.setScalar(1 + hurt * 0.4 + (charging ? Math.sin(t * 30) * 0.08 : 0));
}

// The Stormcaller: banks with its speed, rotors spinning; the chin cannon tracks its target; it noses down to dive
function animateStorm(R, e, dt, t) {
  const P = R.parts, M = R.mats, s = e.state, A = e.atk, k = Math.min(1, dt * 8);
  const down = s === 'crashed';
  let bank = -Math.max(-0.4, Math.min(0.4, e.vx * 0.05)) * e.facing, pitch = 0;
  if (s === 'dive') pitch = -Math.atan2(-(A && A.dy || -1), Math.abs(A && A.dx || 0.3)) * 0.6;
  if (down) { bank = 0.35; pitch = -0.2 + Math.sin(t * 5) * 0.03; }
  if (s === 'roar') bank = Math.sin(t * 24) * 0.12;
  P.hull.rotation.x = ease(P.hull.rotation.x, down ? 0.25 : bank * 0.5, k);
  P.hull.rotation.z = ease(P.hull.rotation.z, pitch, k);
  P.hull.position.y = 0.75 + (down ? -0.15 : Math.sin(t * 2.4) * 0.05);
  for (const r of P.rotors) r.rotation.y += dt * (down ? 4 : s === 'dive' ? 40 : 26);
  // The cannon follows the target (or the laser's line)
  const tg = e.target; let aim = 0;
  if (s === 'laser' || (s === 'windup' && A && A.kind === 'sweep')) aim = -0.35;
  else if (tg) aim = Math.max(-1.2, Math.min(0.4, Math.atan2(tg.y + 1 - (e.y + 0.4), Math.max(0.5, (tg.x - e.x) * e.facing))));
  P.cannon.rotation.z = ease(P.cannon.rotation.z, aim, k);
  P.shield.visible = e.armor > 0; if (P.shield.visible) { P.shield.rotation.y += dt * 0.6; P.shieldMat.opacity = 0.18 + 0.08 * Math.sin(t * 6) + (e.flash > 0 ? 0.25 : 0); }
  const hurt = 1 - e.hp / e.maxHp, charging = s === 'windup' || s === 'laser' || s === 'dive' || s === 'roar' || s === 'volley';
  M.energy.emissiveIntensity = 2.2 + hurt * 2 + (charging ? 2.4 + Math.sin(t * 30) * 1.1 : 0) - (down ? 1.4 : 0);
  P.eye.scale.setScalar(1 + (charging ? 0.25 : 0));
}
