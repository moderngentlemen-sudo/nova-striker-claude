// Nova's secondary weapons, his dodge and the Solar Uppercut: chain lightning, the Gravity Well, enemies
// crackling with a chain's shock, slowed by a perfect dodge or caught in a well, the dodge's afterimages and
// the uppercut's boot jets. Presentation only: reads the sim, never changes it.
import * as THREE from 'three';
import { SUB, SUB_LOOK, CHARS } from './config.js';
import { toWorld, toWorldZ, planeDir, LANE } from './space.js';
import { Strip } from './beamfx.js';

const GOLD = CHARS.nova.energy, PHASE = '#cfeeff', SLOW = '#9fdcff';

function canvasTex(size, draw) {
  const c = document.createElement('canvas'); c.width = c.height = size;
  draw(c.getContext('2d'), size);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}
// The well's accretion disc: bright at its inner edge, streaked with arcs so its spin shows
const discTex = () => canvasTex(256, (g, s) => {
  const c = s / 2, r0 = s * 0.5 * 0.55;
  const grad = g.createRadialGradient(c, c, r0, c, c, s / 2);
  grad.addColorStop(0, 'rgba(255,250,235,1)'); grad.addColorStop(0.25, 'rgba(255,214,140,0.8)'); grad.addColorStop(1, 'rgba(255,170,60,0)');
  g.fillStyle = grad; g.beginPath(); g.arc(c, c, s / 2, 0, Math.PI * 2); g.arc(c, c, r0, 0, Math.PI * 2, true); g.fill();
  g.lineCap = 'round';
  for (let i = 0; i < 26; i++) {
    const r = r0 + Math.random() * (s / 2 - r0) * 0.85, a = Math.random() * Math.PI * 2;
    g.strokeStyle = `rgba(255,${200 + Math.floor(Math.random() * 55)},${120 + Math.floor(Math.random() * 100)},${0.35 + Math.random() * 0.6})`;
    g.lineWidth = 1 + Math.random() * 3; g.beginPath(); g.arc(c, c, r, a, a + 0.3 + Math.random() * 1.1); g.stroke();
  }
});

export class SubFX {
  constructor(fx) {
    this.fx = fx; this.scene = fx.scene; this.t = 0;
    // Chain lightning: a small pool of bolts, each a glow strip under a white core
    this.bolts = Array.from({ length: 6 }, () => ({ glow: new Strip(fx.scene, 6), core: new Strip(fx.scene, 7), life: 0, max: 1, pts: [], w: [], level: 0 }));
    this.chainTint = new THREE.Color(SUB_LOOK.chain.tint);
    // The well's pieces share their geometry and materials (one set of shaders however many wells there are)
    this.wellParts = {
      core: new THREE.SphereGeometry(1, 20, 14), coreMat: new THREE.MeshBasicMaterial({ color: 0x000000 }),
      disc: new THREE.RingGeometry(0.55, 1, 48, 1),
      discMat: new THREE.MeshBasicMaterial({ map: discTex(), transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide, toneMapped: false, color: new THREE.Color(1.6, 1.3, 1) }),
      rimMat: new THREE.SpriteMaterial({ map: fx.tex.ring, color: new THREE.Color('#ffe7b0'), transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, opacity: 0.8 }),
      haloMat: new THREE.SpriteMaterial({ map: fx.tex.ring, color: new THREE.Color(GOLD), transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, opacity: 0.09 }),
      glowMat: new THREE.SpriteMaterial({ map: fx.tex.glow, color: new THREE.Color(GOLD), transparent: true, blending: THREE.AdditiveBlending, depthWrite: false }),
    };
    this.wells = new Map();
    // A slowed enemy wears a slowly turning ring of pale light
    this.auraMat = new THREE.SpriteMaterial({ map: fx.tex.ring, color: new THREE.Color(SLOW), transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, opacity: 0.7 });
    this.auras = new Map();
    this.lastSt = new Map(); this.ghostTick = new Map();
    this.v = new THREE.Vector3(); this.v2 = new THREE.Vector3();
  }

  onEvent(ev) {
    const F = this.fx;
    switch (ev.type) {
      case 'subSwitch': {
        const p = ev.p, L = SUB_LOOK[ev.sub];
        F.popText(p.x, p.y + p.h + 0.7, L.name.toUpperCase(), L.tint, 0.6);
        F.sprite(p.x + p.facing * 0.35, p.y + 1.05, 'ring', L.tint, 0.5, 0.2, 2); F.burst(p.x + p.facing * 0.35, p.y + 1.05, L.tint, 6, 2.5, 0.25, 0.25);
        break;
      }
      case 'grenadeThrow': F.burst(ev.x, ev.y, SUB_LOOK.grenade.tint, 6 + 3 * ev.level, 4, 0.22, 0.2); F.sprite(ev.x, ev.y, 'ring', '#ffe2b8', 0.35 + 0.08 * ev.level, 0.14, 2); break;
      case 'bounce':
        if (ev.sp > 5) { F.burst(ev.x, ev.y, '#ffe2b8', 4, 3, 0.18, 0.18, { dir: Math.PI / 2, spread: 2 }); F.dust(ev.x, ev.y, 0.12, [0, Math.PI], { noRing: true, reach: 0.4 }); }
        break;
      case 'frag': {
        const k = (ev.level || 0) + (ev.perfect ? 1 : 0), c = SUB_LOOK.grenade.tint;
        F.fireball(ev.x, ev.y + 0.2, '#ff8a2e', 0.8 + ev.r * 0.45, 0.24 + 0.03 * k);
        F.sprite(ev.x, ev.y + 0.1, 'star', '#ffffff', 0.8 + ev.r * 0.5, 0.14, 1.5);
        F.sprite(ev.x, ev.y + 0.1, 'ring', '#ffe2b8', ev.r * 0.9, 0.26, 2.4);
        F.burst(ev.x, ev.y + 0.2, c, 16 + 6 * k, 7 + ev.r * 2, 0.34, 0.4, { grav: 8 }); F.burst(ev.x, ev.y + 0.2, '#5d6674', 8 + 3 * k, 6, 0.26, 0.6, { dir: Math.PI / 2, spread: 2.2, grav: 18 });
        F.smoke(ev.x, ev.y + 0.3, '#7d8692', 4 + 2 * k, 1.4, 0.6 + 0.12 * k, 0.8, { dir: Math.PI / 2, spread: 1.6, grav: -0.9, op: 0.5 });
        F.dust(ev.x, ev.y, 0.35 + 0.12 * k, [0, Math.PI], { reach: 1 + ev.r * 0.4 });
        break;
      }
      case 'cluster': F.sprite(ev.x, ev.y + 0.3, 'star', '#ffffff', 1.8, 0.18, 1.6); F.burst(ev.x, ev.y + 0.3, '#ffd28a', 18, 9, 0.3, 0.3, { dir: Math.PI / 2, spread: 2.6, grav: 10 }); break;
      case 'chain': this.chain(ev); break;
      case 'discThrow': F.sprite(ev.x, ev.y, 'ring', SUB_LOOK.disc.tint, 0.5 + 0.1 * ev.level, 0.16, 2.2); F.burst(ev.x, ev.y, '#fff1c9', 6 + 2 * ev.level, 4, 0.2, 0.2); break;
      case 'discRecall': F.sprite(ev.x, ev.y, 'ring', '#ffffff', 0.6, 0.18, 2.2); break;
      case 'discCatch': F.sprite(ev.x, ev.y, 'star', '#fff4d6', 0.9, 0.12, 1.4); F.burst(ev.x, ev.y, SUB_LOOK.disc.tint, 8, 3, 0.2, 0.22); break;
      case 'discFade': F.burst(ev.x, ev.y, SUB_LOOK.disc.tint, 10, 3, 0.2, 0.3); break;
      case 'wellLaunch': F.sprite(ev.x, ev.y, 'glow', GOLD, 0.8, 0.16, 1.6); break;
      case 'wellOpen': {
        // It opens inward: a ring collapsing onto the point, light drawn into it
        F.sprite(ev.x, ev.y, 'ring', '#ffe7b0', ev.r * 2.2, 0.28, 0.15); F.sprite(ev.x, ev.y, 'star', '#ffffff', 1.4, 0.14, 1.3);
        for (let i = 0; i < 24; i++) {
          const a = Math.random() * Math.PI * 2, r = ev.r * (0.7 + Math.random() * 0.4), w = toWorld(ev.x + Math.cos(a) * r, ev.y + Math.sin(a) * r, 0.2, this.v);
          const P = F.particle(w, Math.random() < 0.4 ? '#ffffff' : GOLD, 0.2, 0.3); planeDir(ev.x, -Math.cos(a) * r * 3.4, -Math.sin(a) * r * 3.4, P.v); P.drag = 0.96;
        }
        break;
      }
      case 'wellCollapse': {
        const k = ev.level || 1;
        F.sprite(ev.x, ev.y, 'star', '#ffffff', 2 + 0.5 * k, 0.2, 1.6); F.fireball(ev.x, ev.y, '#ffd27a', 1 + 0.4 * k, 0.3);
        F.sprite(ev.x, ev.y, 'ring', '#ffffff', ev.r * 0.6, 0.35, 3.2); F.sprite(ev.x, ev.y, 'ring', GOLD, ev.r * 0.4, 0.45, 4);
        F.burst(ev.x, ev.y, GOLD, 24 + 8 * k, 10 + 2 * k, 0.35, 0.45, { grav: 4 }); F.burst(ev.x, ev.y, '#ffffff', 12 + 4 * k, 7, 0.24, 0.3);
        F.dust(ev.x, ev.y, 0.5 + 0.15 * k, [0, Math.PI], { reach: 2.2 });
        break;
      }
      case 'dodge': {
        const p = ev.p;
        F.burst(p.x - ev.dx * 0.3, p.y + 0.9, PHASE, 10, 4, 0.22, 0.25, { dir: ev.dx > 0 ? Math.PI : 0, spread: 1.2 });
        if (!ev.air) F.dust(p.x, p.y, 0.3, [ev.dx > 0 ? Math.PI : 0], { noRing: true });
        break;
      }
      case 'perfectDodge': {
        // Time slows: a pale shock of light and rings spreading out over the slowed area
        F.sprite(ev.x, ev.y, 'star', '#ffffff', 2.2, 0.22, 1.6); F.sprite(ev.x, ev.y, 'ring', PHASE, 1.2, 0.5, 5.5); F.sprite(ev.x, ev.y, 'ring', '#ffffff', 0.8, 0.3, 4);
        F.groundRing(ev.p.x, ev.p.y, SLOW, 0.4, 7, 0.7, 0.8);
        F.burst(ev.x, ev.y, PHASE, 30, 7, 0.3, 0.7, { drag: 0.95 });
        break;
      }
      case 'riseBlast': {
        // The Solar Uppercut's flare off the fist
        F.sprite(ev.x, ev.y, 'star', '#ffffff', 2.2, 0.18, 1.5); F.sprite(ev.x, ev.y, 'glow', '#ffd27a', 2.2, 0.22, 1.8);
        F.sprite(ev.x, ev.y, 'ring', '#fff1c9', ev.r * 0.8, 0.3, 3);
        F.burst(ev.x, ev.y, GOLD, 26, 9, 0.32, 0.4, { grav: 6 }); F.burst(ev.x, ev.y, '#ffffff', 12, 7, 0.22, 0.25);
        break;
      }
    }
  }

  // ---- Chain lightning ----
  chain(ev) {
    const F = this.fx, b = this.bolts.find(q => q.life <= 0) || this.bolts.reduce((a, c) => (a.life < c.life ? a : c));
    b.pts = ev.pts.map(q => ({ x: q.x, y: q.y, z: q.z ?? 0 })); b.level = ev.level || 0; b.life = b.max = 0.2 + 0.05 * b.level + (ev.perfect ? 0.08 : 0);
    const c = SUB_LOOK.chain.tint, s = ev.pts[0];
    F.sprite(s.x, s.y, 'star', '#fff6cc', 0.5 + 0.1 * b.level, 0.1, 1.4);
    for (let i = 1; i < ev.pts.length; i++) {
      const q = ev.pts[i]; LANE.z = q.z ?? LANE.z;
      if (q.fizzle) { F.burst(q.x, q.y, c, 8, 4, 0.2, 0.22); continue; }
      F.sprite(q.x, q.y, 'star', '#fff6cc', 0.6 + 0.1 * b.level, 0.12, 1.4); F.sprite(q.x, q.y, 'ring', c, 0.35 + 0.08 * b.level, 0.16, 2);
      F.burst(q.x, q.y, Math.random() < 0.5 ? '#ffffff' : c, 8 + 2 * b.level, 6 + b.level, 0.18, 0.22);
    }
  }
  // A jagged path through the bolt's points: every leg is cut into short pieces pushed off the line at random,
  // most in the middle of the leg; it is redrawn every frame, so the bolt flickers
  jag(b) {
    const out = b.w; let n = 0; const P = b.pts;
    const put = (x, y, z) => { if (!out[n]) out[n] = new THREE.Vector3(); toWorldZ(x, y, z, out[n]); n++; };
    for (let i = 0; i < P.length - 1 && n < 70; i++) {
      const a = P[i], c = P[i + 1], dx = c.x - a.x, dy = c.y - a.y, dz = c.z - a.z, len = Math.hypot(dx, dy, dz) || 1;
      const m = Math.max(2, Math.min(10, Math.ceil(len / 0.45))), nx = -dy / len, ny = dx / len;
      for (let j = i ? 1 : 0; j <= m && n < 72; j++) {
        const u = j / m, amp = j === 0 || j === m ? 0 : (0.16 + 0.05 * b.level) * Math.sin(Math.PI * u) * (Math.random() * 2 - 1) * Math.min(1.6, len / 2);
        put(a.x + dx * u + nx * amp, a.y + dy * u + ny * amp, a.z + dz * u + amp * 0.6 * Math.sin(j * 2.3));
      }
    }
    return out.slice(0, n);
  }

  // ---- Gravity wells ----
  makeWell(w) {
    const W = this.wellParts, g = new THREE.Group();
    const core = new THREE.Mesh(W.core, W.coreMat), disc = new THREE.Mesh(W.disc, W.discMat);
    const rim = new THREE.Sprite(W.rimMat), halo = new THREE.Sprite(W.haloMat), glow = new THREE.Sprite(W.glowMat);
    disc.renderOrder = 5; rim.renderOrder = 5; halo.renderOrder = 4; glow.renderOrder = 5;
    g.add(glow, core, disc, rim, halo); this.scene.add(g);
    const M = { g, core, disc, rim, halo, glow, spin: Math.random() * 6 };
    this.wells.set(w, M); return M;
  }
  syncWells(world, alpha, dt) {
    const seen = new Set(), F = this.fx;
    for (const w of world.wells) {
      seen.add(w);
      const M = this.wells.get(w) || this.makeWell(w);
      const x = w.px + (w.x - w.px) * alpha, y = w.py + (w.y - w.py) * alpha, z = (w.pz ?? w.z) + (w.z - (w.pz ?? w.z)) * alpha;
      LANE.z = z; toWorldZ(x, y, z, M.g.position);
      if (w.phase === 'orb') {
        // The orb: a small black heart in a gold glow, shedding sparks
        M.core.scale.setScalar(0.13); M.glow.scale.setScalar(0.9 + 0.15 * Math.sin(this.t * 30)); M.disc.visible = M.rim.visible = M.halo.visible = false;
        if (Math.random() < 0.7) F.burst(x, y, Math.random() < 0.5 ? '#ffffff' : GOLD, 1, 1, 0.16, 0.2);
        continue;
      }
      // Open: a black singularity in a spinning accretion disc, a halo showing its reach, light spiralling in
      const L = w.level, open = Math.min(1, w.t / 8), left = Math.max(0, w.life - w.t), fade = Math.min(1, left / 12);
      const pulse = left < 20 ? 1 + 0.15 * Math.sin(this.t * 40) : 1;
      M.core.scale.setScalar((0.26 + 0.06 * L) * open * pulse);
      M.disc.visible = M.rim.visible = M.halo.visible = true;
      M.spin += dt * (7 + 2 * L);
      M.disc.rotation.set(1.18, 0, M.spin); M.disc.scale.setScalar((1.4 + 0.22 * L) * open * (0.5 + 0.5 * fade));
      M.rim.scale.setScalar((0.85 + 0.12 * L) * open); M.glow.scale.setScalar((1.6 + 0.3 * L) * open);
      M.halo.scale.setScalar(w.r * 2 * open); M.halo.material.rotation = this.t * 0.6;
      for (let i = 0; i < 3 + L; i++) {
        const a = Math.random() * Math.PI * 2, r = w.r * (0.55 + Math.random() * 0.45), wp = toWorldZ(x + Math.cos(a) * r, y + Math.sin(a) * r, z + (Math.random() - 0.5) * r, this.v);
        const P = F.particle(wp, Math.random() < 0.3 ? '#ffffff' : GOLD, 0.14, 0.4);
        const s = r * 2.4; planeDir(x, -Math.cos(a) * s - Math.sin(a) * s * 0.8, -Math.sin(a) * s + Math.cos(a) * s * 0.8, P.v); P.drag = 0.97;
      }
    }
    for (const [w, M] of this.wells) if (!seen.has(w)) { this.scene.remove(M.g); this.wells.delete(w); }
  }

  update(dt, world, view) {
    this.t += dt;
    const F = this.fx, cam = view.camera.position;
    // Chain lightning
    for (const b of this.bolts) {
      if (b.life <= 0) { b.glow.mesh.visible = b.core.mesh.visible = false; continue; }
      b.life -= dt;
      const k = Math.max(0, b.life / b.max), fl = 0.55 + Math.random() * 0.45, pts = this.jag(b), c = this.chainTint;
      if (pts.length < 2) continue;
      b.glow.build(pts, cam, () => (0.14 + 0.035 * b.level) * (0.6 + 0.4 * k), () => [c.r * 1.3, c.g * 1.15, c.b * 0.7, 0.5 * k * fl]);
      b.core.build(pts, cam, () => 0.035 + 0.01 * b.level, () => [1.9, 1.85, 1.6, k * fl]);
    }
    this.syncWells(world, view.alpha, dt);
    // Enemies: crackling with a chain's shock, gold dust swirling round those a well holds, a pale ring on the slowed
    const seen = new Set();
    for (const e of world.enemies) {
      if (e.dead) continue;
      LANE.z = e.z || 0;
      if (e.shockT > 0 && Math.random() < 0.55) {
        const w = toWorld(e.x + (Math.random() - 0.5) * e.w, e.y + Math.random() * e.h, 0.3, this.v);
        const P = F.particle(w, Math.random() < 0.5 ? '#ffffff' : SUB_LOOK.chain.tint, 0.14, 0.12); P.v.set((Math.random() - 0.5) * 6, (Math.random() - 0.5) * 6, 0); P.drag = 0.8;
      }
      if (e.wellT > 0 && Math.random() < 0.5) F.burst(e.x, e.y + e.h * 0.5, GOLD, 1, 2.5, 0.16, 0.3);
      if (e.slowT > 0) {
        seen.add(e);
        let s = this.auras.get(e);
        if (!s) { s = new THREE.Sprite(this.auraMat.clone()); s.renderOrder = 5; this.scene.add(s); this.auras.set(e, s); }
        toWorld(e.x, e.y + e.h * 0.5, 0.35, s.position); s.scale.setScalar(Math.max(e.w, e.h) * 1.35);
        s.material.rotation = -this.t * 1.2; s.material.opacity = 0.65 * Math.min(1, e.slowT / 20);
        if (Math.random() < 0.3) { const P = F.particle(toWorld(e.x + (Math.random() - 0.5) * e.w, e.y + Math.random() * e.h, 0.3, this.v), SLOW, 0.12, 0.8); P.v.set(0, 0.35, 0); P.drag = 1; }
      }
    }
    for (const [e, s] of this.auras) if (!seen.has(e)) { this.scene.remove(s); s.material.dispose(); this.auras.delete(e); }
    // Players: the dodge's afterimages, and the Solar Uppercut's boot jets and column of light
    for (const p of world.players) {
      const rig = view.rigs.get(p); if (!rig || !rig.root.visible) continue;
      LANE.z = p.z || 0;
      if (p.state === 'dodge' && p.dodge && p.dodge.t % 2 === 0 && p.dodge.t <= 12 && this.ghostTick.get(p) !== world.tick) {
        this.ghostTick.set(p, world.tick);
        F.ghosts.spawn(rig, p.dodge.perfect ? '#ffffff' : PHASE, p.dodge.perfect ? 0.55 : 0.34, 0.24);
      }
      const m = p.move, rising = p.state === 'attack' && p.moveId === 'nova_rise' && m;
      const last = this.lastSt.get(p); this.lastSt.set(p, rising ? p.st : -1);
      if (!rising) continue;
      if (p.st >= m.su && last !== undefined && last < m.su) {
        F.sprite(p.x, p.y + 1.6, 'glow', '#ffd27a', 3.6, 0.3, 1.1, 0.2, 0.22);   // a column of gold light where he took off
        F.groundRing(p.x, p.y, '#fff1c9', 0.3, 1.8, 0.3, 0.9); F.dust(p.x, p.y, 0.5, [0, Math.PI], { noRing: true });
      }
      if (p.st >= m.su && p.st < m.su + m.ac) {
        for (const dz of [-0.13, 0.13]) {
          const w = toWorldZ(p.x + (Math.random() - 0.5) * 0.12, p.y - 0.05, p.z + dz, this.v2);
          const P = F.particle(w, Math.random() < 0.5 ? '#fff1c9' : GOLD, 0.34, 0.2); P.v.set((Math.random() - 0.5) * 0.8, -7 - Math.random() * 4, (Math.random() - 0.5) * 0.8); P.drag = 0.86;
        }
        if (Math.random() < 0.5) F.smoke(p.x, p.y - 0.2, '#8e97a3', 1, 0.8, 0.4, 0.5, { dir: -Math.PI / 2, spread: 1, op: 0.35 });
      }
    }
    LANE.z = 0;
  }

  // Warm-up stand-ins (compiles the bolt strips and the well's materials at load)
  warmShow(at, cam) {
    const b = this.bolts[0], pts = [at.clone(), at.clone().add(new THREE.Vector3(1, 0.3, 0)), at.clone().add(new THREE.Vector3(2, 0, 0))];
    b.glow.build(pts, cam, () => 0.1, () => [1, 1, 1, 1]); b.core.build(pts, cam, () => 0.05, () => [1, 1, 1, 1]);
    const M = this.makeWell({}); M.g.position.copy(at); this.warmWell = M;
    const a = new THREE.Sprite(this.auraMat); a.position.copy(at); this.scene.add(a); this.warmAura = a;
    return [b.glow.mesh, b.core.mesh, M.g, a];
  }
  warmDone() {
    const b = this.bolts[0]; b.glow.mesh.visible = b.core.mesh.visible = false;
    if (this.warmWell) { this.warmWell.g.visible = false; for (const [w, M] of this.wells) if (M === this.warmWell) this.wells.delete(w); }
    if (this.warmAura) this.warmAura.visible = false;
  }
}
