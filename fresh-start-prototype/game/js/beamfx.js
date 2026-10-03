// The Level 4 beams (Nova's, and RAM's broader Breach Beam in his blue): a white-hot core inside a surging sheath in the attachment's colour, following the
// sim's beam path (a Prism beam bounces once). It snaps open, flickers and pulses along its length, pushes
// rings down its path, streams sparks, flares at the bracer and throws sparks and smoke where it hits a wall;
// when it ends it narrows to nothing. Presentation only: reads the sim, never changes it.
import * as THREE from 'three';
import { ATTACH_LOOK, MARKSMAN, RAM, CHARS } from './config.js';
import { toWorld, toWorldZ, planeDir3, LANE } from './space.js';

const MAXP = 72;
const WHITE = new THREE.Color('#ffffff');

// A camera-facing strip through a polyline, with a width and an RGBA colour per point (also used by the
// chain lightning and the ultimates)
export class Strip {
  constructor(scene, order) {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(new Float32Array(MAXP * 6), 3).setUsage(THREE.DynamicDrawUsage));
    g.setAttribute('color', new THREE.BufferAttribute(new Float32Array(MAXP * 8), 4).setUsage(THREE.DynamicDrawUsage));
    const idx = []; for (let i = 0; i < MAXP - 1; i++) { const a = i * 2; idx.push(a, a + 1, a + 2, a + 1, a + 3, a + 2); }
    g.setIndex(idx);
    this.mesh = new THREE.Mesh(g, new THREE.MeshBasicMaterial({ vertexColors: true, transparent: true, depthWrite: false, side: THREE.DoubleSide, toneMapped: false }));
    this.mesh.frustumCulled = false; this.mesh.renderOrder = order; this.mesh.visible = false; scene.add(this.mesh);
    this.d = new THREE.Vector3(); this.c = new THREE.Vector3(); this.s = new THREE.Vector3();
  }
  // pts: world points; w(i), rgba(i) per point
  build(pts, cam, w, rgba) {
    const pos = this.mesh.geometry.attributes.position.array, col = this.mesh.geometry.attributes.color.array, n = Math.min(MAXP, pts.length);
    for (let i = 0; i < MAXP; i++) {
      const j = Math.min(i, n - 1), p = pts[j];
      this.d.copy(pts[Math.min(n - 1, j + 1)]).sub(pts[Math.max(0, j - 1)]);
      if (this.d.lengthSq() < 1e-8) this.d.set(1, 0, 0); this.d.normalize();
      this.s.crossVectors(this.d, this.c.copy(cam).sub(p).normalize()).normalize().multiplyScalar(i < n ? w(j) : 0);
      pos[i * 6] = p.x + this.s.x; pos[i * 6 + 1] = p.y + this.s.y; pos[i * 6 + 2] = p.z + this.s.z;
      pos[i * 6 + 3] = p.x - this.s.x; pos[i * 6 + 4] = p.y - this.s.y; pos[i * 6 + 5] = p.z - this.s.z;
      const c = rgba(j);
      for (const o of [0, 4]) { col[i * 8 + o] = c[0]; col[i * 8 + o + 1] = c[1]; col[i * 8 + o + 2] = c[2]; col[i * 8 + o + 3] = i < n ? c[3] : 0; }
    }
    this.mesh.geometry.attributes.position.needsUpdate = true; this.mesh.geometry.attributes.color.needsUpdate = true;
    this.mesh.visible = true;
  }
}

export class BeamFX {
  constructor(fx) {
    this.fx = fx; this.scene = fx.scene; this.beams = new Map(); this.t = 0;
    this.v = new THREE.Vector3(); this.v2 = new THREE.Vector3();
  }
  of(p) {
    let B = this.beams.get(p);
    if (B) return B;
    const glow = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.fx.tex.glow, transparent: true, depthWrite: false }));
    const core = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.fx.tex.glow, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending }));
    const hit = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.fx.tex.star, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending }));
    for (const s of [glow, core, hit]) { s.visible = false; s.renderOrder = 6; this.scene.add(s); }
    B = { edge: new Strip(this.scene, 3), sheath: new Strip(this.scene, 4), core: new Strip(this.scene, 5), glow, core2: core, hit, pts: [], age: 0, end: -1, tint: new THREE.Color(), ringT: 0 };
    this.beams.set(p, B); return B;
  }

  onEvent(ev) {
    if (ev.type === 'beamStart') {
      const B = this.of(ev.p); B.age = 0; B.end = -1; B.tint.set(ev.attach === 'breach' ? CHARS.ram.energy : ATTACH_LOOK[ev.attach] ? ATTACH_LOOK[ev.attach].tint : '#ffd889');
      B.ram = ev.p.char === 'ram'; B.W = B.ram ? RAM.beam.width : MARKSMAN.beam.width;   // RAM's Breach Beam is a broader column
      if (ev.over) B.tint.lerp(WHITE, 0.35);
    } else if (ev.type === 'beamEnd') {
      const B = this.beams.get(ev.p); if (B) B.end = 0;
    }
  }

  // The beam's path in world points: along each sim segment every ~0.7 m
  path(p, B) {
    const pts = B.pts; pts.length = 0;
    for (const g of p.beam.segs) {
      const z0 = g.z0 || 0, z1 = g.z1 || 0, len = Math.hypot(g.x1 - g.x0, g.y1 - g.y0, z1 - z0), n = Math.max(1, Math.ceil(len / 0.7));
      for (let i = pts.length ? 1 : 0; i <= n && pts.length < MAXP; i++) pts.push(toWorldZ(g.x0 + (g.x1 - g.x0) * i / n, g.y0 + (g.y1 - g.y0) * i / n, z0 + (z1 - z0) * i / n, new THREE.Vector3()));
    }
  }

  update(dt, world, view) {
    this.t += dt;
    const cam = view.camera.position;
    for (const [p, B] of this.beams) {
      const live = p.beam && p.state === 'beam' && p.beam.segs && p.beam.segs.length && world.players.includes(p);
      if (live) { B.age += dt; this.path(p, B); B.segs = p.beam.segs.map(g => ({ ...g })); B.dx = p.beam.dx; B.dy = p.beam.dy; B.dz = p.beam.dz || 0; B.px = p.x; }
      else if (B.end < 0 && B.pts.length) B.end = 0;
      if (B.end >= 0) B.end += dt;
      const closing = B.end >= 0 ? Math.max(0, 1 - B.end / 0.16) : 1;
      if (!B.pts.length || closing <= 0) {
        B.edge.mesh.visible = B.sheath.mesh.visible = B.core.mesh.visible = B.glow.visible = B.core2.visible = B.hit.visible = false;
        if (closing <= 0) B.pts.length = 0;
        if (!world.players.includes(p) && closing <= 0) { for (const o of [B.edge.mesh, B.sheath.mesh, B.core.mesh, B.glow, B.core2, B.hit]) { this.scene.remove(o); o.material.dispose(); if (o.geometry && !o.isSprite) o.geometry.dispose(); } this.beams.delete(p); }
        continue;
      }
      const open = Math.min(1, B.age / 0.07), W = B.W || MARKSMAN.beam.width, t = this.t, n = B.pts.length, tint = B.tint;
      const base = open * closing;
      // The sheath surges along its length; the core flickers
      // Widths are half-widths: the sheath spans the beam's real hit width, the core a third of it
      // RAM's: a deep-blue outer edge under it, so the column reads against bright skies and floors too
      if (B.ram) B.edge.build(B.pts, cam, i => W * 1.3 * base * Math.min(1, 0.45 + i * 0.25) * (1 + 0.1 * Math.sin(t * 30 - i * 0.7)), i => [tint.r * 0.25, tint.g * 0.3, tint.b * 0.55, 0.75 * Math.min(1, 0.4 + i * 0.3) * (i === n - 1 ? 0.3 : 1)]);
      B.edge.mesh.visible = !!B.ram;
      const sk = B.ram ? 1.05 : 1.5;
      B.sheath.build(B.pts, cam, i => W * 1.0 * base * Math.min(1, 0.45 + i * 0.25) * (1 + 0.16 * Math.sin(t * 38 - i * 0.9) + 0.06 * Math.sin(t * 91 + i)),
        i => [tint.r * sk, tint.g * sk, tint.b * sk, (B.ram ? 0.8 : 0.5) * Math.min(1, 0.4 + i * 0.3) * (i === n - 1 ? 0.3 : 1)]);
      const flick = 0.85 + Math.random() * 0.3;
      B.core.build(B.pts, cam, i => W * (B.ram ? 0.24 : 0.36) * base * flick * Math.min(1, 0.5 + i * 0.3), () => [2.6, 2.5, 2.3, 0.95]);
      // Bracer flare and the impact point
      const a = B.pts[0], e = B.pts[n - 1];
      B.glow.position.copy(a); B.glow.material.color.copy(tint); B.glow.scale.setScalar((1.0 + Math.sin(t * 40) * 0.12) * base); B.glow.material.opacity = 0.7; B.glow.visible = true;
      B.core2.position.copy(a); B.core2.material.color.set('#ffffff'); B.core2.scale.setScalar(0.9 * base * flick); B.core2.visible = true;
      const endSeg = B.segs[B.segs.length - 1];
      B.hit.visible = !!endSeg.wall; B.hit.position.copy(e); B.hit.material.color.copy(tint).lerp(WHITE, 0.5);
      B.hit.scale.setScalar((1.2 + Math.random() * 0.6) * base); B.hit.material.rotation = t * 7;
      if (!live) continue;
      // Sparks streaming down the beam
      for (let i = 0; i < 4; i++) {
        const j = Math.floor(Math.random() * (n - 1)), q = B.pts[j], P = this.fx.particle(q, Math.random() < 0.5 ? '#ffffff' : '#' + tint.getHexString(), 0.16 + Math.random() * 0.12, 0.12);
        P.v.copy(B.pts[Math.min(n - 1, j + 1)]).sub(q).normalize().multiplyScalar(26 + Math.random() * 14);
        P.v.x += (Math.random() - 0.5) * 3; P.v.y += (Math.random() - 0.5) * 3; P.drag = 0.95; P.grav = 0;
      }
      // Rings pushed down the beam from the bracer
      B.ringT -= dt;
      if (B.ringT <= 0) {
        B.ringT = 0.07;
        const dir = planeDir3(B.px, B.dx, B.dy, B.dz || 0, this.v2).normalize();
        this.fx.charge.shockRing(a.clone(), dir, '#' + tint.getHexString(), 0.14, 0.38, 0.2, 4.5);
      }
      // Where it meets a wall (and each Prism bounce): sparks thrown back out, smoke, scorch glow
      for (const g of B.segs) {
        if (!g.wall) continue;
        const out = Math.atan2(g.ny, g.nx); LANE.z = (g.z1 || 0) + (g.nz || 0) * 0.2;
        this.fx.burst(g.x1, g.y1, Math.random() < 0.5 ? '#ffffff' : '#' + tint.getHexString(), 3, 9, 0.2, 0.25, { dir: out, spread: 2.2, grav: 12 });
        if (Math.random() < 0.35) this.fx.smoke(g.x1 + g.nx * 0.2, g.y1 + g.ny * 0.2, '#8e97a3', 1, 1.5, 0.5, 0.6, { dir: out, spread: 1.4, op: 0.4 });
      }
    }
  }

  // Warm-up stand-in (compiles the strip material at load)
  warmShow(at, cam) {
    const B = this.of('warm'); B.pts = [at.clone(), at.clone().add(new THREE.Vector3(1, 0, 0)), at.clone().add(new THREE.Vector3(2, 0, 0))];
    B.sheath.build(B.pts, cam, () => 0.2, () => [1, 1, 1, 1]); B.core.build(B.pts, cam, () => 0.1, () => [1, 1, 1, 1]);
    return [B.sheath.mesh, B.core.mesh];
  }
  warmDone() { const B = this.beams.get('warm'); if (B) { B.sheath.mesh.visible = B.core.mesh.visible = false; B.pts.length = 0; this.warmB = B; this.beams.delete('warm'); } }
}
