// The three.js stand-ins for the Unity helpers the converted rig code calls (Geo, MeshAt, G, materials, the shared skeleton).
import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
const RBox = (w, h, d, r = 0.05) => new RoundedBoxGeometry(w, h, d, 3, Math.min(r, w / 2 - 1e-3, h / 2 - 1e-3, d / 2 - 1e-3));
const Geo = {
  Cylinder: (rt, rb, h, rad = 32, hs = 1, open = false) => new THREE.CylinderGeometry(rt, rb, h, rad, hs, open),
  Sphere: (r, w = 32, h = 16, ps = 0, pl = Math.PI * 2, ts = 0, tl = Math.PI) => new THREE.SphereGeometry(r, w, h, ps, pl, ts, tl),
  Octahedron: (r, d = 0) => new THREE.OctahedronGeometry(r, d), Cone: (r, h, rs = 32) => new THREE.ConeGeometry(r, h, rs),
  Torus: (r, t, rs = 12, ts = 48) => new THREE.TorusGeometry(r, t, rs, ts),
  Circle: (r, s) => new THREE.CircleGeometry(r, s), Icosahedron: (r, d) => new THREE.IcosahedronGeometry(r, d),
  Capsule: (r, l, c, rs) => new THREE.CapsuleGeometry(r, l, c, rs),
};
const mkMat = (o) => {
  const p = { roughness: o.roughness ?? 1, metalness: o.metalness ?? 0 };
  if (o.colorCss) p.color = new THREE.Color(o.colorCss); if (o.colorHex !== undefined) p.color = new THREE.Color(o.colorHex);
  if (o.emissiveCss) p.emissive = new THREE.Color(o.emissiveCss); if (o.emissiveHex !== undefined) p.emissive = new THREE.Color(o.emissiveHex);
  if (o.emissiveIntensity !== undefined) p.emissiveIntensity = o.emissiveIntensity;
  for (const k of ['transparent', 'opacity', 'side', 'blending', 'depthWrite', 'clearcoat', 'clearcoatRoughness']) if (o[k] !== undefined) p[k] = o[k];
  return new THREE.MeshPhysicalMaterial(p);
};
const GlowMat = (c, k = 2.4) => mkMat({ colorCss: c, emissiveCss: c, emissiveIntensity: k, roughness: 0.3 });
const MeshAt = (geo, mat, x = 0, y = 0, z = 0) => { const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); return m; };
const G = (x = 0, y = 0, z = 0) => { const g = new THREE.Group(); g.position.set(x, y, z); return g; };
const ATTACH_LOOK = { lance: { tint: '#ffb547' } };
const CHARS = { nova: { energy: '#ffb547' } }; const RimAll = (M) => M;
const SETTINGS = { novaHead: 'bare' };
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
const Mats = (c) => ({
  base: mkMat({ colorCss: '#eef2f7', roughness: 0.4, metalness: 0.1, clearcoat: 0.65, clearcoatRoughness: 0.18 }),
  trim: mkMat({ colorCss: '#2f5f9e', roughness: 0.36, metalness: 0.3, clearcoat: 0.65, clearcoatRoughness: 0.18 }),
  under: Std('#1c2c48', 0.62, 0.15), energy: GlowMat(c.energy),
});
function build() {
