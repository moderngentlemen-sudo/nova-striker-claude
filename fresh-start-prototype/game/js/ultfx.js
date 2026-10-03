// The ultimates. The call: a pillar of light and rising energy around each caster (the render dims the rest
// of the world). Nova's Supernova: light gathering into a small sun over him, the colossal beam he steers
// (rippling, throwing sparks and rings), then the nova. Echo's Thousand Cuts: a storm of cuts, each leaving
// an afterimage of him mid-strike beside its target, then every cut at once. A team ultimate ends in an
// eclipse over the whole screen that shatters into light. Presentation only: reads the sim, never changes it.
import * as THREE from 'three';
import { ULT, CHARS } from './config.js';
import { toWorld, toWorldZ, planeDir, planeDir3, yawOf, LANE } from './space.js';
import { Strip } from './beamfx.js';

const CYAN = '#7fe3ff', GOLD = CHARS.nova.energy, ORANGE = CHARS.echo.energy;

export class UltFX {
  constructor(fx) {
    this.fx = fx; this.scene = fx.scene; this.t = 0;
    this.beams = new Map(); this.pending = []; this.lastCut = new Map();
    this.v = new THREE.Vector3(); this.v2 = new THREE.Vector3(); this.v3 = new THREE.Vector3();
    // The eclipse: a solid dark disc (ordinary blending, so it can darken) over a corona of light, with a thin
    // bright rim
    const cv = document.createElement('canvas'); cv.width = cv.height = 128;
    const g = cv.getContext('2d'), grad = g.createRadialGradient(64, 64, 54, 64, 64, 64);
    grad.addColorStop(0, 'rgba(255,255,255,1)'); grad.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = grad; g.beginPath(); g.arc(64, 64, 64, 0, Math.PI * 2); g.fill();
    g.fillStyle = '#ffffff'; g.beginPath(); g.arc(64, 64, 54, 0, Math.PI * 2); g.fill();
    const solid = new THREE.CanvasTexture(cv); solid.colorSpace = THREE.SRGBColorSpace;
    const sp = (map, blend, color, order) => { const s = new THREE.Sprite(new THREE.SpriteMaterial({ map, color, transparent: true, depthWrite: false, depthTest: false, blending: blend })); s.visible = false; s.renderOrder = order; this.scene.add(s); return s; };
    this.eclipse = { disc: sp(solid, THREE.NormalBlending, 0x02040a, 12), corona: sp(fx.tex.glow, THREE.AdditiveBlending, 0xbff4ff, 11), ring: sp(fx.tex.ring, THREE.AdditiveBlending, 0xffffff, 12), t: -1 };
  }
  beamOf(p) {
    let B = this.beams.get(p); if (B) return B;
    const sp = (tex) => { const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.fx.tex[tex], transparent: true, depthWrite: false, blending: THREE.AdditiveBlending })); s.visible = false; s.renderOrder = 7; this.scene.add(s); return s; };
    B = { halo: new Strip(this.scene, 4), sheath: new Strip(this.scene, 5), core: new Strip(this.scene, 6), sun: sp('glow'), sunCore: sp('glow'), flare: sp('star'), pts: [], ringT: 0, end: 1 };
    this.beams.set(p, B); return B;
  }
  after(secs, fn) { this.pending.push({ t: secs, fn }); }

  onEvent(ev) {
    const F = this.fx;
    switch (ev.type) {
      case 'ultCast': case 'ultJoin': {
        // A pillar of light around the caster, rings spreading over the floor, energy rising
        const p = ev.p, x = p.x, y = p.y, col = CHARS[p.char].energy;
        F.sprite(x, y + 3, 'glow', CYAN, 7.5, 0.9, 1.08, 0.1, 0.16); F.sprite(x, y + 3, 'glow', '#ffffff', 7, 0.5, 1.05, 0.12, 0.05);
        F.sprite(x, y + 1, 'star', '#ffffff', 3.2, 0.3, 1.6); F.sprite(x, y + 1, 'ring', CYAN, 1.2, 0.6, 4.5);
        F.groundRing(x, y, CYAN, 0.4, 4.5, 0.8, 0.9); F.groundRing(x, y, col, 0.3, 3, 0.6, 0.8);
        F.burst(x, y + 0.6, CYAN, 40, 7, 0.3, 0.8, { dir: Math.PI / 2, spread: 1.4, drag: 0.94, grav: -4 });
        F.burst(x, y + 1, col, 24, 5, 0.26, 0.6);
        F.dust(x, y, 0.8, [0, Math.PI]);
        break;
      }
      case 'ultRun': for (const m of ev.members) F.sprite(m.x, m.y + 1, 'star', '#ffffff', 2.4, 0.2, 1.5); break;
      case 'ultBegin':
        if (ev.kind === 'echo') {
          // He vanishes: a burst of orange light and a ring
          const p = ev.p; F.sprite(p.x, p.y + 1, 'ring', ORANGE, 1.1, 0.3, 3); F.burst(p.x, p.y + 1, ORANGE, 30, 8, 0.3, 0.4); F.burst(p.x, p.y + 1, '#ffffff', 14, 6, 0.22, 0.3);
          this.lastCut.set(p, { x: p.x, y: p.y + 1 });
        }
        break;
      case 'ultNova': {
        // The nova: a burst of white-gold light the size of its reach, rings, sparks, a column of smoke
        const x = ev.x, y = ev.y, r = ev.r;
        F.sprite(x, y, 'star', '#ffffff', r * 1.6, 0.35, 1.6); F.sprite(x, y, 'glow', '#fff4d6', r * 1.8, 0.45, 1.5);
        F.fireball(x, y, '#ffd27a', r * 0.9, 0.5);
        for (const [sz, life, g] of [[r * 0.5, 0.45, 4.4], [r * 0.35, 0.65, 6], [r * 0.2, 0.8, 8]]) F.sprite(x, y, 'ring', '#ffffff', sz, life, g);
        F.sprite(x, y, 'ring', CYAN, r * 0.4, 0.7, 5.5);
        F.burst(x, y, GOLD, 90, 18, 0.45, 0.8, { grav: 4 }); F.burst(x, y, '#ffffff', 50, 14, 0.3, 0.5);
        const fl = F.floorUnder(x, y, 4);
        if (fl !== null) { F.groundRing(x, fl, '#fff1c9', 0.5, r * 1.3, 0.6, 0.95); F.dust(x, fl, 1.4, [0, Math.PI], { reach: 4 }); }
        F.smoke(x, y, '#8e97a3', 14, 2.5, 1.2, 1.4, { op: 0.45, grow: 2.6, grav: -0.8 });
        break;
      }
      case 'ultCut': this.cut(ev); break;
      case 'ultFinisher': {
        const p = ev.p;
        if (ev.flourish) { F.sprite(p.x, p.y + 1, 'ring', ORANGE, 2, 0.4, 3.6); F.slashMark(p.x, p.y + 1, '#fff1d6', 9, 0.1, 0.3); F.burst(p.x, p.y + 1, ORANGE, 40, 12, 0.35, 0.5); break; }
        // Every cut lands again at once: a cross of slashes on every target, and a long cut across the screen
        for (const e of ev.targets) {
          const x = e.x, y = e.y + e.h * 0.55; LANE.z = e.z;
          F.slashMark(x, y, '#fff1d6', 3.6, 0.7, 0.28); F.slashMark(x, y, ORANGE, 3.6, -0.7, 0.28); F.slashMark(x, y, '#ffffff', 4.4, 0, 0.2);
          F.sprite(x, y, 'star', '#ffffff', 2.4, 0.2, 1.5); F.burst(x, y, ORANGE, 30, 10, 0.34, 0.45); F.burst(x, y, '#ffffff', 14, 8, 0.24, 0.3);
        }
        const mid = ev.targets.length ? ev.targets.reduce((s, e) => s + e.x, 0) / ev.targets.length : p.x;
        LANE.z = ev.targets.length ? ev.targets.reduce((s, e) => s + e.z, 0) / ev.targets.length : p.z;
        F.slashMark(mid, p.y + 1.2, '#ffffff', 26, 0, 0.3);
        // He is back, landing the last cut
        LANE.z = p.z; F.sprite(p.x, p.y + 1, 'ring', ORANGE, 1.4, 0.35, 3.4); F.burst(p.x, p.y + 1, ORANGE, 20, 6, 0.28, 0.35);
        break;
      }
      case 'ultEnd': F.sprite(ev.p.x, ev.p.y + 1, 'ring', CYAN, 0.9, 0.3, 2.8); break;
      case 'teamFinisher': this.teamFinisher(ev); break;
    }
  }

  // One of Echo's cuts: a long slash across the target, an afterimage of him beside it mid-strike, sparks, and
  // a streak of light from where the last cut was
  cut(ev) {
    const F = this.fx, p = ev.p, x = ev.x, y = ev.y, side = -ev.dir, rig = this.fx.rigs.get(p);
    F.slashMark(x, y, '#fff1d6', 3 + Math.random(), ev.ang, 0.2); F.slashMark(x, y, ORANGE, 2.2, ev.ang + 0.25, 0.16);
    F.sprite(x, y, 'star', '#ffffff', 1.1, 0.12, 1.4);
    F.burst(x, y, Math.random() < 0.5 ? '#ffffff' : ORANGE, 12, 9, 0.24, 0.24);
    const gx = x + side * 1.1, gy = ev.e.y;
    if (rig) {
      // Stand the (hidden) rig beside the target for a moment and leave a ghost of it there
      const pos = rig.root.position.clone(), rot = rig.root.rotation.y, fl = rig.flip.scale.x;
      toWorld(gx, gy, 0, rig.root.position); rig.root.rotation.y = yawOf(gx, -side, 0); rig.flip.scale.x = 1; rig.root.updateMatrixWorld(true);
      F.ghosts.spawn(rig, new THREE.Color(ORANGE).multiplyScalar(1.8), 0.6, 0.3);
      rig.root.position.copy(pos); rig.root.rotation.y = rot; rig.flip.scale.x = fl; rig.root.updateMatrixWorld(true);
    }
    const last = this.lastCut.get(p);
    if (last) {
      // A thin streak of light along the blink
      const n = 6;
      const z1 = LANE.z;
      for (let i = 0; i <= n; i++) { const u = i / n; LANE.z = last.z + (z1 - last.z) * u; F.burst(last.x + (gx - last.x) * u, last.y + (gy + 1 - last.y) * u, i % 2 ? '#ffffff' : ORANGE, 1, 1, 0.18, 0.16); }
      LANE.z = z1;
    }
    this.lastCut.set(p, { x: gx, y: gy + 1, z: LANE.z });
  }

  // The team finisher: an eclipse fills the screen, its corona flares, then it shatters into light
  teamFinisher(ev) {
    const E = this.eclipse, F = this.fx;
    E.t = 0; E.x = ev.x; E.y = ev.y; E.z = ev.z || 0;
    this.after(0.42, () => {
      F.sprite(ev.x, ev.y, 'star', '#ffffff', 14, 0.45, 1.6); F.sprite(ev.x, ev.y, 'glow', '#ffffff', 16, 0.5, 1.4);
      for (const [sz, life, g] of [[3, 0.5, 5], [2, 0.7, 7], [1.2, 0.9, 10]]) F.sprite(ev.x, ev.y, 'ring', '#ffffff', sz, life, g);
      F.sprite(ev.x, ev.y, 'ring', CYAN, 2.4, 0.9, 7);
      F.burst(ev.x, ev.y, CYAN, 120, 22, 0.45, 0.9); F.burst(ev.x, ev.y, '#ffffff', 60, 16, 0.3, 0.6);
      for (const c of ev.chars || []) F.burst(ev.x, ev.y, CHARS[c].energy, 50, 18, 0.38, 0.7);
    });
  }

  update(dt, world, view) {
    this.t += dt;
    const F = this.fx, cam = view.camera.position;
    for (const q of this.pending) { q.t -= dt; if (q.t <= 0) { q.done = true; q.fn(); } }
    this.pending = this.pending.filter(q => !q.done);
    // The eclipse
    const E = this.eclipse;
    if (E.t >= 0) {
      E.t += dt; const k = E.t, grow = Math.min(1, k / 0.3), gone = k > 0.42 ? Math.max(0, 1 - (k - 0.42) / 0.12) : 1;
      for (const s of [E.disc, E.corona, E.ring]) { toWorldZ(E.x, E.y + 1.5, E.z || 0, s.position); s.visible = gone > 0; }
      E.disc.scale.setScalar(6.4 * grow); E.disc.material.opacity = gone;
      E.corona.scale.setScalar(11.5 * grow * (1 + 0.05 * Math.sin(this.t * 40))); E.corona.material.opacity = gone;
      E.ring.scale.setScalar(8 * grow); E.ring.material.opacity = 0.9 * gone; E.ring.material.rotation = this.t * 2;
      if (gone <= 0) { E.t = -1; for (const s of [E.disc, E.corona, E.ring]) s.visible = false; }
    }
    // The call: energy rising around every member while it is called
    const U = world.ultCast;
    if (U && U.phase === 'cast') {
      for (const m of U.members) {
        for (let i = 0; i < 3; i++) {
          const w = toWorldZ(m.x + (Math.random() - 0.5) * 1.6, m.y + Math.random() * 0.4, m.z + (Math.random() - 0.5) * 1.6, this.v);
          const P = F.particle(w, Math.random() < 0.5 ? CYAN : '#ffffff', 0.18, 0.5); P.v.set(0, 5 + Math.random() * 4, 0); P.drag = 0.97;
        }
      }
    }
    // Nova's Supernova
    for (const p of world.players) {
      const R = p.ultRun, live = p.state === 'ult' && R && R.kind === 'nova' && world.players.includes(p);
      if (!live && !this.beams.has(p)) continue;
      const B = this.beamOf(p), N = ULT.nova;
      const hide = () => { for (const s of [B.halo.mesh, B.sheath.mesh, B.core.mesh, B.sun, B.sunCore, B.flare]) s.visible = false; };
      if (!live) { hide(); if (!world.players.includes(p)) this.beams.delete(p); continue; }
      const c = { x: p.x, y: p.y + p.h * 0.62 }; LANE.z = p.z;
      // The gathering sun over his raised hands
      if (R.t <= N.gather + 2) {
        const k = Math.min(1, R.t / N.gather), at = toWorld(c.x, c.y + 1.1, 0.3, this.v);
        B.sun.position.copy(at); B.sun.material.color.set(GOLD); B.sun.scale.setScalar(0.4 + 2.4 * k * (1 + 0.08 * Math.sin(this.t * 50))); B.sun.visible = true;
        B.sunCore.position.copy(at); B.sunCore.material.color.set('#ffffff'); B.sunCore.scale.setScalar(0.2 + 1.2 * k); B.sunCore.visible = true;
        for (let i = 0; i < 4; i++) {
          const a = Math.random() * Math.PI * 2, r = 2.5 + Math.random() * 1.5, tv = planeDir(c.x, Math.cos(a) * r, Math.sin(a) * r, this.v2);
          const P = F.particle(this.v3.copy(at).add(tv), Math.random() < 0.4 ? '#ffffff' : GOLD, 0.2, 0.28); P.v.copy(tv).multiplyScalar(-3.4); P.drag = 1;
        }
      } else { B.sun.visible = false; B.sunCore.visible = false; }
      if (!R.segs) { B.halo.mesh.visible = B.sheath.mesh.visible = B.core.mesh.visible = B.flare.visible = false; continue; }
      // The beam: along its segment every 0.8 m; it opens fast, ripples, and narrows away at the end
      const g = R.segs[0], gz0 = g.z0 || 0, gz1 = g.z1 || 0, len = Math.hypot(g.x1 - g.x0, g.y1 - g.y0, gz1 - gz0), n = Math.min(60, Math.ceil(len / 0.8)), pts = B.pts;
      pts.length = 0;
      for (let i = 0; i <= n; i++) pts.push(toWorldZ(g.x0 + (g.x1 - g.x0) * i / n, g.y0 + (g.y1 - g.y0) * i / n, gz0 + (gz1 - gz0) * i / n, new THREE.Vector3()));
      const bt = R.t - N.gather, open = Math.min(1, bt / 6), close = Math.min(1, (N.beam - bt) / 8), base = Math.max(0, open * close), W = N.width, t = this.t;
      // A cyan halo, a gold sheath that surges along it, and a white-hot core (kept below clipping so the colours read)
      B.halo.build(pts, cam, i => W * 1.9 * base * Math.min(1, 0.35 + i * 0.2), i => [0.3, 0.72, 1.0, 0.22 * Math.min(1, 0.3 + i * 0.2)]);
      B.sheath.build(pts, cam, i => W * base * Math.min(1, 0.4 + i * 0.25) * (1 + 0.14 * Math.sin(t * 34 - i * 0.7) + 0.05 * Math.sin(t * 87 + i)), i => [1.35, 0.92, 0.38, 0.62]);
      B.core.build(pts, cam, i => W * 0.4 * base * (0.9 + Math.random() * 0.2) * Math.min(1, 0.5 + i * 0.3), () => [2.1, 2.0, 1.75, 1]);
      B.flare.position.copy(pts[0]); B.flare.material.color.set('#ffffff'); B.flare.scale.setScalar((2.4 + Math.random()) * base); B.flare.material.rotation = t * 5; B.flare.visible = true;
      // Sparks down its length, rings pushed along it, and dust blown off the floor under it
      for (let i = 0; i < 8; i++) {
        const j = Math.floor(Math.random() * (pts.length - 1)), P = F.particle(pts[j], Math.random() < 0.5 ? '#ffffff' : GOLD, 0.24, 0.16);
        P.v.copy(pts[j + 1]).sub(pts[j]).normalize().multiplyScalar(30 + Math.random() * 16); P.v.x += (Math.random() - 0.5) * 6; P.v.y += (Math.random() - 0.5) * 6; P.drag = 0.95;
      }
      B.ringT -= dt;
      if (B.ringT <= 0) { B.ringT = 0.06; F.charge.shockRing(pts[0].clone(), planeDir3(p.x, R.dx, R.dy, R.dz || 0, new THREE.Vector3()).normalize(), '#ffffff', 0.5, 1.6, 0.26, 7); }
      for (let d = 1; d < len; d += 2.2) {
        if (Math.random() > 0.2) continue;
        LANE.z = gz0 + (gz1 - gz0) * d / len;
        const x = g.x0 + (g.x1 - g.x0) * d / len, y = g.y0 + (g.y1 - g.y0) * d / len, fl = F.floorUnder(x, y, 2.5);
        if (fl !== null) F.smoke(x, fl + 0.1, '#b9c1cb', 1, 3, 0.4, 0.5, { dir: g.x1 >= g.x0 ? 0.35 : Math.PI - 0.35, spread: 0.6, grav: -0.5, op: 0.35 });
      }
    }
    LANE.z = 0;
  }

  // Warm-up stand-ins (compiles the beam strips and the eclipse sprites at load)
  warmShow(at, cam) {
    const B = this.beamOf('warm'); B.pts = [at.clone(), at.clone().add(new THREE.Vector3(1, 0, 0)), at.clone().add(new THREE.Vector3(2, 0, 0))];
    B.halo.build(B.pts, cam, () => 0.2, () => [1, 1, 1, 1]); B.sheath.build(B.pts, cam, () => 0.2, () => [1, 1, 1, 1]); B.core.build(B.pts, cam, () => 0.1, () => [1, 1, 1, 1]);
    const E = this.eclipse; for (const s of [E.disc, E.corona, E.ring, B.sun, B.flare]) { s.position.copy(at); s.visible = true; }
    return [B.halo.mesh, B.sheath.mesh, B.core.mesh, E.disc, E.corona, E.ring, B.sun, B.flare];
  }
  warmDone() {
    const B = this.beams.get('warm');
    if (B) { for (const s of [B.halo.mesh, B.sheath.mesh, B.core.mesh, B.sun, B.sunCore, B.flare]) s.visible = false; this.beams.delete('warm'); this.warmB = B; }
    const E = this.eclipse; for (const s of [E.disc, E.corona, E.ring]) s.visible = false;
  }
}
