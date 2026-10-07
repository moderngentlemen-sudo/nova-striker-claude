// The three.js stand-ins for the Unity helpers the converted rig code calls (Geo, MeshAt, G, materials, the shared skeleton).
import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
const RBox = (w, h, d, r = 0.05) => new RoundedBoxGeometry(w, h, d, 3, Math.min(r, w / 2 - 1e-3, h / 2 - 1e-3, d / 2 - 1e-3));
const Geo = {
  Cylinder: (rt, rb, h, rad = 32, hs = 1, open = false) => new THREE.CylinderGeometry(rt, rb, h, rad, hs, open),
  Sphere: (r, w = 32, h = 16, ps = 0, pl = Math.PI * 2, ts = 0, tl = Math.PI) => new THREE.SphereGeometry(r, w, h, ps, pl, ts, tl),
  Octahedron: (r, d = 0) => new THREE.OctahedronGeometry(r, d), Cone: (r, h, rs = 32) => new THREE.ConeGeometry(r, h, rs),
  Torus: (r, t, rs = 12, ts = 48, arc = Math.PI * 2) => new THREE.TorusGeometry(r, t, rs, ts, arc),
  Plane: (w, h, ws = 1, hs = 1) => new THREE.PlaneGeometry(w, h, ws, hs),
  Box: (w, h, d) => new THREE.BoxGeometry(w, h, d), Circle: (r, s) => new THREE.CircleGeometry(r, s), Icosahedron: (r, d) => new THREE.IcosahedronGeometry(r, d),
  Capsule: (r, l, c, rs) => new THREE.CapsuleGeometry(r, l, c, rs),
};
const mkMat = (o) => {
  const p = { roughness: o.roughness ?? 1, metalness: o.metalness ?? 0 };
  if (o.colorCss) p.color = new THREE.Color(o.colorCss); if (o.colorHex !== undefined) p.color = new THREE.Color(o.colorHex);
  if (o.emissiveCss) p.emissive = new THREE.Color(o.emissiveCss); if (o.emissiveHex !== undefined) p.emissive = new THREE.Color(o.emissiveHex);
  if (o.emissiveIntensity !== undefined) p.emissiveIntensity = o.emissiveIntensity;
  if (o.map) p.map = o.map; if (o.emissiveMap) p.emissiveMap = o.emissiveMap;
  for (const k of ['transparent', 'opacity', 'side', 'blending', 'depthWrite', 'clearcoat', 'clearcoatRoughness']) if (o[k] !== undefined) p[k] = o[k];
  return new THREE.MeshPhysicalMaterial(p);
};
const GlowMat = (c, k = 2.4) => mkMat({ colorCss: c, emissiveCss: c, emissiveIntensity: k, roughness: 0.3 });
const MeshAt = (geo, mat, x = 0, y = 0, z = 0) => { const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); return m; };
const addc = (parent, child) => { parent.add(child); return child; };   // (the game's add returns the child)
const G = (x = 0, y = 0, z = 0) => { const g = new THREE.Group(); g.position.set(x, y, z); return g; };
const ATTACH_LOOK = { lance: { tint: '#ffb547' } };
const CHARS = { nova: { energy: '#ffb547', trim: '#2f5f9e', base: '#eef2f7', under: '#1c2c48' }, echo: { energy: '#ff9a1f', trim: '#15151b', base: '#f4f4f2', under: '#1b1b22' },
  ram: { energy: '#58a6ff', trim: '#2d3540', base: '#aeb8c4', under: '#1b2129' } };
const RimAll = (M) => M; const charId = 'echo';
const SETTINGS = { novaHead: 'bare', echoHead: 'bare' };
function MakeLimb(parent, M, upperLen, lowerLen, r, z) {
  const top = G(0, 0, z); parent.add(top);
  top.add(MeshAt(Geo.Capsule(r, upperLen - r, 4, 12), M.under, 0, -upperLen / 2));
  const joint = G(0, -upperLen, 0); top.add(joint);
  joint.add(MeshAt(Geo.Capsule(r * 0.92, lowerLen - r, 4, 12), M.under, 0, -lowerLen / 2));
  const end = G(0, -lowerLen, 0); joint.add(end);
  return { top, joint, end };
}
function Skeleton(id, M, arm, leg, shoulderZ, hipZ, upperArm = 0.3, foreArm = 0.29) {
  const root = G(), body = G(); root.add(body);
  const hips = G(0, 0.95, 0); body.add(hips); const spine = G(0, 0.08, 0); hips.add(spine);
  const head = G(0, 0.66, 0); spine.add(head);
  const armN = MakeLimb(spine, M, upperArm, foreArm, arm, shoulderZ), armF = MakeLimb(spine, M, upperArm, foreArm, arm, -shoulderZ);
  armN.top.position.y = 0.53; armF.top.position.y = 0.53;
  const legN = MakeLimb(hips, M, 0.46, 0.46, leg, hipZ), legF = MakeLimb(hips, M, 0.46, 0.46, leg, -hipZ);
  return { root, body, hips, spine, head, armN, armF, legN, legF };
}
const Std = (c, r, m = 0.08) => mkMat({ colorCss: c, roughness: r, metalness: m });
const Armour = (c, r, m) => mkMat({ colorCss: c, roughness: r, metalness: m, clearcoat: 0.65, clearcoatRoughness: 0.18 });
const Mats = (c) => ({
  base: Armour(c.base, 0.4, 0.1), trim: Armour(c.trim, 0.36, 0.3), under: Std(c.under, 0.62, 0.15), energy: GlowMat(c.energy),
  visor: mkMat({ colorHex: 0x0b1018, roughness: 0.12, metalness: 0.7 }),
  amber: mkMat({ colorHex: 0xffa53a, emissiveHex: 0xff8a1a, emissiveIntensity: 0.6, roughness: 0.1, transparent: true, opacity: 0.72 }),
});
// The Rampart's hexagon texture, drawn on a canvas (Rigs.HexTexture's pattern, near enough)
function HexTexture() {
  const c = document.createElement('canvas'); c.width = 128; c.height = 222; const g = c.getContext('2d');
  g.fillStyle = '#666'; g.fillRect(0, 0, 128, 222); g.strokeStyle = '#fff'; g.lineWidth = 3; const R = 21, cw = Math.sqrt(3) * R, ch = 1.5 * R;
  for (let j = -1; j < 12; j++) for (let i = -1; i < 6; i++) {
    const cx = i * cw + (j % 2 ? cw / 2 : 0), cy = j * ch; g.beginPath();
    for (let k = 0; k < 7; k++) { const a = Math.PI / 6 + k * Math.PI / 3; g.lineTo(cx + Math.cos(a) * R, cy + Math.sin(a) * R); } g.stroke();
  }
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}
function build() {
