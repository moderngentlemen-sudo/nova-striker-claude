// Version 12 set dressing and breakables. Presentation only: reads the level data and the sim, never changes it.
//   buildLandmarks(view): the 3D set pieces that make the new routes' paths read as places in space, placed from
//     the path pieces themselves (level.js SEGS): whatever an arc wraps round gets a structure at its centre (the
//     Foundry's reactor core inside the helix, its furnace dome, the Undercity's cooling tower), an arc that bends
//     toward the camera gets a curved wall behind it (the trench, the vents), and the straights get their
//     surroundings (foundry machinery and pipes; city blocks with lit windows; the transit rail and a train).
//   ATMOS: each route's light: fog colour and range, sky gradient, sun colour (lerped by the camera's route).
//   Breakables: the breakable pieces' meshes (crate, barricade, glass, pillar: level.js DESTRUCT), shaking and
//     darkening as they take damage, and the debris they burst into (an instanced pool that bounces and settles).
import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { SEGS, BOXES, DESTRUCT, LIFTS, HW, groundBelow, pathFrame } from './level.js';
import { toWorldZ, planeDir } from './space.js';

const yawAt = x => { const f = pathFrame(x); return Math.atan2(-f.tz, f.tx); };
let seed = 1234; const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);

export const ATMOS = {
  skyport: { fog: 0xc6e2f4, near: 70, far: 260, top: 0x2b7fd3, mid: 0x7fbfee, bot: 0xd9eefa, sun: 0xffeed6, hemi: 0xd8ecff },
  foundry: { fog: 0xe6c3a0, near: 50, far: 210, top: 0x3a4f78, mid: 0xd99a6a, bot: 0xf2c79a, sun: 0xffc690, hemi: 0xffe0c2 },
  undercity: { fog: 0x8e8fb8, near: 45, far: 200, top: 0x1c2147, mid: 0x6d5f9e, bot: 0xd6a0b8, sun: 0xffb7c9, hemi: 0xbfc6ff },
};

// A canvas texture of lit windows (the Undercity's towers)
function windowTex() {
  const c = document.createElement('canvas'); c.width = 64; c.height = 128;
  const g = c.getContext('2d'); g.fillStyle = '#1b1f33'; g.fillRect(0, 0, 64, 128);
  for (let y = 4; y < 128; y += 10) for (let x = 4; x < 64; x += 12) {
    const on = rnd() < 0.42; g.fillStyle = on ? (rnd() < 0.7 ? '#ffd9a0' : '#9fe7ff') : '#262b45'; g.fillRect(x, y, 7, 5);
  }
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; return t;
}
// Hazard stripes (barricades)
function stripeTex() {
  const c = document.createElement('canvas'); c.width = c.height = 64;
  const g = c.getContext('2d'); g.fillStyle = '#2c3442'; g.fillRect(0, 0, 64, 64); g.fillStyle = '#ffc23a';
  for (let i = -64; i < 128; i += 22) { g.beginPath(); g.moveTo(i, 0); g.lineTo(i + 11, 0); g.lineTo(i + 11 - 64, 64); g.lineTo(i - 64, 64); g.fill(); }
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; return t;
}
// Planks (crates)
function plankTex() {
  const c = document.createElement('canvas'); c.width = c.height = 64;
  const g = c.getContext('2d'); g.fillStyle = '#c98b4a'; g.fillRect(0, 0, 64, 64);
  g.strokeStyle = '#8a5a2b'; g.lineWidth = 3; g.strokeRect(2, 2, 60, 60);
  g.beginPath(); g.moveTo(4, 4); g.lineTo(60, 60); g.stroke();
  g.fillStyle = 'rgba(0,0,0,0.12)'; for (let y = 0; y < 64; y += 13) g.fillRect(0, y, 64, 2);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}

export function buildLandmarks(view) {
  const add = (geo, mat, x, y, z, o = {}) => {
    const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z);
    if (o.ry) m.rotation.y = o.ry; if (o.rx) m.rotation.x = o.rx; if (o.scale) m.scale.set(...o.scale);
    view.bake(m, o.cast ?? false); return m;
  };
  const at = (x, y, depth) => toWorldZ(x, y, depth, new THREE.Vector3());
  const M = {
    steel: new THREE.MeshStandardMaterial({ color: 0x6b6f7a, roughness: 0.55, metalness: 0.4 }),
    rust: new THREE.MeshStandardMaterial({ color: 0x8a5a3c, roughness: 0.8, metalness: 0.2 }),
    core: new THREE.MeshStandardMaterial({ color: 0xd8dde4, roughness: 0.35, metalness: 0.3 }),
    molten: new THREE.MeshStandardMaterial({ color: 0xff8a2a, emissive: 0xff6a10, emissiveIntensity: 2.4 }),
    hot: new THREE.MeshStandardMaterial({ color: 0xffd28a, emissive: 0xffa040, emissiveIntensity: 2.0 }),
    concrete: new THREE.MeshStandardMaterial({ color: 0x8d8fa3, roughness: 0.9 }),
    windows: new THREE.MeshStandardMaterial({ map: windowTex(), emissive: 0xffffff, emissiveMap: null, emissiveIntensity: 0.0, roughness: 0.8 }),
    neon: new THREE.MeshStandardMaterial({ color: 0xff7ad9, emissive: 0xff4fc8, emissiveIntensity: 2.2 }),
    cyan: new THREE.MeshStandardMaterial({ color: 0x7fe3ff, emissive: 0x4fd6ff, emissiveIntensity: 2.0 }),
    train: new THREE.MeshStandardMaterial({ color: 0xe9edf3, roughness: 0.35, emissive: 0x4fd6ff, emissiveIntensity: 0.15 }),
  };
  M.windows.emissiveMap = M.windows.map; M.windows.emissiveIntensity = 1.3;

  for (const g of SEGS) {
    const foundry = g.route === 'foundry';
    if (g.kind === 'arc' && g.s > 0) {
      // Whatever the path wraps round: a structure at the arc's centre
      const R = g.r - HW - 1.4;   // clear of the corridor's inner edge
      if (foundry && g.r < 18) {
        // The reactor core inside the helix: a tall drum with glowing rings at each turn of the climb
        add(new THREE.CylinderGeometry(R, R + 0.8, 70, 48), M.core, g.C.x, 18, g.C.z);
        for (let y = -2; y < 50; y += 5.5) { const ring = new THREE.Mesh(new THREE.TorusGeometry(R + 0.35, 0.28, 8, 48), M.molten); ring.rotation.x = Math.PI / 2; ring.position.set(g.C.x, y, g.C.z); view.bake(ring, false); }
        for (let i = 0; i < 10; i++) { const a = i / 10 * Math.PI * 2; add(new THREE.BoxGeometry(0.5, 64, 0.5), M.hot, g.C.x + Math.sin(a) * (R + 0.2), 18, g.C.z + Math.cos(a) * (R + 0.2)); }
        add(new THREE.CylinderGeometry(R * 0.55, R, 6, 32), M.steel, g.C.x, 56, g.C.z);
      } else if (foundry) {
        // The furnace dome the path bends round, venting heat
        add(new THREE.SphereGeometry(R, 32, 18, 0, Math.PI * 2, 0, Math.PI / 2), M.rust, g.C.x, -2, g.C.z);
        add(new THREE.CylinderGeometry(R, R, 4, 32), M.steel, g.C.x, -4, g.C.z);
        for (let i = 0; i < 6; i++) { const a = i / 6 * Math.PI * 2; add(new THREE.CylinderGeometry(0.9, 1.2, 26, 12), M.steel, g.C.x + Math.sin(a) * R * 0.5, 11, g.C.z + Math.cos(a) * R * 0.5); add(new THREE.CylinderGeometry(1.0, 1.0, 0.6, 12), M.molten, g.C.x + Math.sin(a) * R * 0.5, 24.2, g.C.z + Math.cos(a) * R * 0.5); }
      } else {
        // The cooling tower the stair winds round: a hyperboloid shell
        const pts = []; for (let i = 0; i <= 12; i++) { const t = i / 12; pts.push(new THREE.Vector2(R * (1 - 0.32 * Math.sin(t * Math.PI * 0.95)), -20 + t * 56)); }
        add(new THREE.LatheGeometry(pts, 48), new THREE.MeshStandardMaterial({ color: 0xa7a9bd, roughness: 0.85, side: THREE.DoubleSide }), g.C.x, 0, g.C.z);
        for (const y of [8, 24]) { const ring = new THREE.Mesh(new THREE.TorusGeometry(R * (y < 20 ? 0.74 : 0.7) + 0.2, 0.18, 6, 48), M.neon); ring.rotation.x = Math.PI / 2; ring.position.set(g.C.x, y, g.C.z); view.bake(ring, false); }
      }
    }
    // Along every piece: what stands behind the path (and, on a bend toward the camera, a wall that curves with it)
    for (let x = g.x0; x < g.x1; x += foundry ? 3.2 : 5) {
      const ry = yawAt(x), back = g.kind === 'arc' && g.s < 0;
      if (foundry) {
        if (back) { const p = at(x, 2, -9); add(new THREE.BoxGeometry(3.4, 22, 1.4), M.steel, p.x, p.y, p.z, { ry }); if (rnd() < 0.5) { const q = at(x, 6 + rnd() * 8, -8.1); add(new THREE.CylinderGeometry(0.35, 0.35, 3.4, 8), M.molten, q.x, q.y, q.z, { ry: ry + Math.PI / 2, rx: Math.PI / 2 }); } }
        else if (rnd() < 0.55) {
          const d = -(10 + rnd() * 14), h = 8 + rnd() * 22, p = at(x, -10 + h / 2, d);
          add(new THREE.BoxGeometry(2.6, h, 2.6), rnd() < 0.5 ? M.steel : M.rust, p.x, p.y, p.z, { ry });
          if (rnd() < 0.5) { const q = at(x, -10 + h + 0.4, d); add(new THREE.CylinderGeometry(0.8, 0.8, 0.8, 10), M.molten, q.x, q.y, q.z); }
        }
        // the molten channel far below the walkways
        const m = at(x, -9, -1); add(new THREE.BoxGeometry(3.4, 0.4, 5), M.molten, m.x, m.y, m.z, { ry });
      } else {
        if (back) { const h = 26 + rnd() * 18, p = at(x, -6 + h / 2, -10.5); add(new THREE.BoxGeometry(4.6, h, 4), M.windows, p.x, p.y, p.z, { ry }); }
        else {
          for (const d of [-(12 + rnd() * 10), -(30 + rnd() * 20)]) {
            const h = 20 + rnd() * 50, w = 5 + rnd() * 5, p = at(x, -20 + h / 2, d);
            const geo = new THREE.BoxGeometry(w, h, w); const uv = geo.attributes.uv; for (let i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) * w / 6, uv.getY(i) * h / 12);
            add(geo, M.windows, p.x, p.y, p.z, { ry });
            if (rnd() < 0.25) { const q = at(x, -20 + h + 0.3, d); add(new THREE.BoxGeometry(w * 0.9, 0.3, 0.3), M.neon, q.x, q.y, q.z, { ry }); }
          }
        }
      }
    }
  }
  // Lift pads on the foundry floor: a glowing disc with a faint column of light up to the platform it serves
  for (const [x, y, top] of LIFTS) {
    const p = at(x, y + 0.06, 0); add(new THREE.CylinderGeometry(0.9, 1.0, 0.12, 24), M.hot, p.x, p.y, p.z);
    const c = at(x, y + 0.14, 0); add(new THREE.TorusGeometry(0.7, 0.06, 6, 24), M.cyan, c.x, c.y, c.z, { rx: Math.PI / 2 });
    const beam = new THREE.Sprite(new THREE.SpriteMaterial({ map: view.fx.tex.glow, color: 0xffc070, transparent: true, opacity: 0.22, blending: THREE.AdditiveBlending, depthWrite: false }));
    const b = at(x, (y + top) / 2, 0); beam.position.copy(b); beam.scale.set(1.2, top - y, 1); view.scene.add(beam);
  }
  // The Undercity's transit line: a rail overhead along it, and a train standing on the far track
  for (const g of SEGS.filter(q => q.route === 'undercity' && q.kind === 'line' && q.x1 - q.x0 > 70)) {
    for (let x = g.x0; x < g.x1; x += 2.5) {
      const r = at(x, 9, -1.5); add(new THREE.BoxGeometry(2.6, 0.3, 0.5), M.concrete, r.x, r.y, r.z, { ry: yawAt(x) });
      const s = at(x, 9.2, -1.5); add(new THREE.BoxGeometry(2.6, 0.08, 0.12), M.cyan, s.x, s.y - 0.25, s.z, { ry: yawAt(x) });
      if (Math.round(x - g.x0) % 10 === 0) { const c = at(x, 1, -7); add(new THREE.BoxGeometry(0.4, 14, 0.4), M.concrete, c.x, 2, c.z); }
    }
    const t0 = g.x0 + 20; for (let i = 0; i < 3; i++) { const t = at(t0 + i * 9, 1.5, -8.5); add(new THREE.CapsuleGeometry(1.4, 6.4, 4, 12), M.train, t.x, 1.6, t.z, { ry: yawAt(t0 + i * 9), rx: 0 }).rotation.z = Math.PI / 2; }
  }
}

// ---- Breakables ----
export class Breakables {
  constructor(scene, fx) {
    this.scene = scene; this.fx = fx; this.meshes = new Map(); this.t = 0;
    const planks = plankTex(), stripes = stripeTex();
    this.mat = {
      crate: new THREE.MeshStandardMaterial({ map: planks, roughness: 0.85 }),
      barricade: new THREE.MeshStandardMaterial({ map: stripes, roughness: 0.6, metalness: 0.3 }),
      glass: new THREE.MeshStandardMaterial({ color: 0xbfe8ff, roughness: 0.05, metalness: 0.1, transparent: true, opacity: 0.38, depthWrite: false }),
      frame: new THREE.MeshStandardMaterial({ color: 0x5f7897, roughness: 0.6 }),
      pillar: new THREE.MeshStandardMaterial({ color: 0xb9c2cc, roughness: 0.8 }),
      cap: new THREE.MeshStandardMaterial({ color: 0x7fe3ff, emissive: 0x4fd6ff, emissiveIntensity: 1.6 }),
    };
    for (const b of BOXES) {
      if (b.type !== 'd') continue;
      const w = b.x1 - b.x0, h = b.y1 - b.y0, dz = b.z1 - b.z0, xm = (b.x0 + b.x1) / 2, g = new THREE.Group();
      if (b.tag === 'crate') { g.add(new THREE.Mesh(new RoundedBoxGeometry(w, h, dz, 2, 0.05), this.mat.crate)); }
      else if (b.tag === 'barricade') { g.add(new THREE.Mesh(new RoundedBoxGeometry(w, h, dz, 2, 0.08), this.mat.barricade)); }
      else if (b.tag === 'glass') {
        g.add(new THREE.Mesh(new THREE.BoxGeometry(w * 0.6, h, dz - 0.1), this.mat.glass));
        for (const y of [-h / 2, h / 2]) { const f = new THREE.Mesh(new THREE.BoxGeometry(w, 0.12, dz), this.mat.frame); f.position.y = y; g.add(f); }
      } else {
        g.add(new THREE.Mesh(new THREE.CylinderGeometry(w * 0.5, w * 0.58, h, 16), this.mat.pillar));
        const c = new THREE.Mesh(new THREE.BoxGeometry(w * 1.25, 0.18, w * 1.25), this.mat.cap); c.position.y = h / 2 - 0.09; g.add(c);
      }
      g.traverse(o => { if (o.isMesh) { o.castShadow = b.tag !== 'glass'; o.receiveShadow = true; } });
      toWorldZ(xm, b.y0 + h / 2, (b.z0 + b.z1) / 2, g.position); g.rotation.y = yawAt(xm);
      scene.add(g); this.meshes.set(b, { g, shake: 0, home: g.position.clone() });
    }
    // Debris: a pool of chunks (one instanced mesh; each chunk tinted to what it came from)
    this.N = 220; this.di = 0;
    this.chunks = Array.from({ length: this.N }, () => ({ life: 0, max: 1, x: 0, y: 0, z: 0, vx: 0, vy: 0, vz: 0, s: 0.2, rx: 0, ry: 0, wx: 0, wy: 0, sx: 0, sz: 0 }));
    this.debris = new THREE.InstancedMesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshStandardMaterial({ roughness: 0.7 }), this.N);
    this.debris.castShadow = true; this.debris.frustumCulled = false;
    const z = new THREE.Matrix4().makeScale(0, 0, 0), c = new THREE.Color();
    for (let i = 0; i < this.N; i++) { this.debris.setMatrixAt(i, z); this.debris.setColorAt(i, c.set('#ffffff')); }
    scene.add(this.debris);
    this.m4 = new THREE.Matrix4(); this.q = new THREE.Quaternion(); this.e = new THREE.Euler(); this.v = new THREE.Vector3(); this.sc = new THREE.Vector3(); this.col = new THREE.Color();
  }

  onEvent(ev) {
    const F = this.fx, b = ev.b;
    if (ev.type === 'liftBounce') { F.sprite(ev.x, ev.y + 0.3, 'ring', '#ffc070', 1.4, 0.3, 2.4); F.burst(ev.x, ev.y + 0.2, '#ffd28a', 18, 6, 0.24, 0.35, { dir: Math.PI / 2, spread: 0.8 }); return; }
    if (ev.type === 'boxChip') {
      const M = this.meshes.get(b); if (M) M.shake = 1;
      const D = DESTRUCT[b.tag];
      F.burst(ev.x, ev.y, ev.hard ? '#ffffff' : D.color, ev.hard ? 4 : 6, 4, 0.18, 0.3, { grav: 12 });
      if (b.tag === 'glass') F.sprite(ev.x, ev.y, 'star', '#ffffff', 0.6, 0.1, 1.4);
    } else if (ev.type === 'boxBreak') {
      const D = DESTRUCT[b.tag], w = b.x1 - b.x0, h = b.y1 - b.y0;
      const n = b.tag === 'glass' ? 26 : b.tag === 'crate' ? 14 : b.tag === 'barricade' ? 18 : 22;
      for (let i = 0; i < n; i++) {
        const x = b.x0 + rnd() * w, y = b.y0 + rnd() * h, z = b.z0 + rnd() * (b.z1 - b.z0), by = ev.by, push = by ? Math.sign(x - by.x) || 1 : (rnd() < 0.5 ? -1 : 1);
        this.chunk(x, y, z, push * (2 + rnd() * 5), 2 + rnd() * 6, (rnd() - 0.5) * 4,
          b.tag === 'glass' ? 0.06 + rnd() * 0.18 : 0.12 + rnd() * 0.22, D.color, b.tag === 'glass');
      }
      F.smoke(ev.x, ev.y, b.tag === 'crate' ? '#b59a7a' : '#9aa3ae', b.tag === 'glass' ? 2 : 8, 1.6, 0.6, 0.8, { op: 0.45, grow: 2.2 });
      if (b.tag === 'glass') { F.burst(ev.x, ev.y, '#e8f8ff', 20, 7, 0.16, 0.3, { grav: 10 }); F.sprite(ev.x, ev.y, 'star', '#ffffff', 1.6, 0.14, 1.5); }
      else F.dust(ev.x, b.y0, 0.6, [0, Math.PI]);
      if (b.tag === 'pillar') F.sprite(ev.x, ev.y, 'ring', '#ffffff', 1.4, 0.3, 2.4);
      if (b.loot) F.sprite(ev.x, ev.y, 'glow', '#ffe9a8', 1.6, 0.4, 1.6);
    }
  }
  chunk(x, y, z, vx, vy, vz, s, color, glass) {
    const C = this.chunks[this.di], i = this.di; this.di = (this.di + 1) % this.N;
    const w = toWorldZ(x, y, z, this.v), d = planeDir(x, vx, vy, new THREE.Vector3(), vz);
    Object.assign(C, { life: glass ? 1.4 : 4.5, max: glass ? 1.4 : 4.5, x: w.x, y: w.y, z: w.z, vx: d.x, vy: d.y, vz: d.z, s, sx: x, sz: z, rx: rnd() * 6, ry: rnd() * 6, wx: (rnd() - 0.5) * 14, wy: (rnd() - 0.5) * 14, glass });
    this.debris.setColorAt(i, this.col.set(color).multiplyScalar(0.75 + rnd() * 0.4));
    this.debris.instanceColor.needsUpdate = true;
  }

  update(dt, world) {
    this.t += dt;
    for (const [b, M] of this.meshes) {
      M.g.visible = !b.broken;
      if (b.broken) continue;
      M.shake = Math.max(0, M.shake - dt * 6);
      M.g.position.copy(M.home); if (M.shake > 0) { M.g.position.x += (rnd() - 0.5) * 0.12 * M.shake; M.g.position.z += (rnd() - 0.5) * 0.12 * M.shake; }
      const k = b.hp / DESTRUCT[b.tag].hp;   // darker and lower as it takes damage
      M.g.traverse(o => { if (o.isMesh && o.material !== this.mat.glass && o.material !== this.mat.cap) { if (!o.userData.own) { o.material = o.material.clone(); o.userData.own = true; } o.material.color.setScalar(0.55 + 0.45 * k); } });
    }
    for (let i = 0; i < this.N; i++) {
      const C = this.chunks[i];
      if (C.life <= 0) { if (C.max) { this.debris.setMatrixAt(i, this.m4.makeScale(0, 0, 0)); C.max = 0; } continue; }
      C.life -= dt; C.vy -= 20 * dt;
      C.x += C.vx * dt; C.y += C.vy * dt; C.z += C.vz * dt; C.rx += C.wx * dt; C.ry += C.wy * dt;
      const floor = groundBelow(C.sx, C.y + 0.4, C.sz);
      if (C.y - C.s / 2 < floor && C.y > floor - 1) { C.y = floor + C.s / 2; C.vy = Math.abs(C.vy) * 0.3; C.vx *= 0.6; C.vz *= 0.6; C.wx *= 0.6; C.wy *= 0.6; }
      const fade = Math.min(1, C.life / 0.6);
      this.q.setFromEuler(this.e.set(C.rx, C.ry, 0));
      this.m4.compose(this.v.set(C.x, C.y, C.z), this.q, this.sc.set(C.s * fade, C.s * (C.glass ? 0.25 : 1) * fade, C.s * fade));
      this.debris.setMatrixAt(i, this.m4);
    }
    this.debris.instanceMatrix.needsUpdate = true;
  }
}
