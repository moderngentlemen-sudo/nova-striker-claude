// Fix's effects, and the boosts she gives everyone. The Patch Beam (a mint arc from her welder to whoever she
// patches, rising sparks of repair on them; a shower of sparks when she welds her own kit); her gadgets (the
// Patch Pylon and its field, the Sentry turning to its target, the Amp Coil arcing to teammates in its field,
// Jack-Up's spring pad), with their level lights; power-up capsules; Scrap flying to her; Hot Rivets; and
// Overhaul's supply pod and pulses of repair. On every player: a shimmer of Plating, Overclock's sparks.
// Presentation only: reads the sim, never changes it.
import * as THREE from 'three';
import { CHARS, FIX, FIX_LOOK, ULT } from './config.js';
import { toWorld, toWorldZ, planeDir, yawOf, LANE } from './space.js';
import { pathFrame } from './level.js';
import { chest } from './player.js';
import { Strip } from './beamfx.js';

const MINT = CHARS.fix.energy, PALE = '#d8fff0', WHITE = '#ffffff', CYAN = '#6fe3ff', PLATE = '#a9c8ff', HAZARD = '#ffd23f';

export class FixFX {
  constructor(fx) {
    this.fx = fx; this.scene = fx.scene; this.t = 0;
    this.beams = new Map(); this.gadgets = new Map(); this.pickups = new Map(); this.pods = new Map(); this.arcs = [];
    this.v = new THREE.Vector3(); this.v2 = new THREE.Vector3();
    const std = (c, r = 0.4, m = 0.3) => new THREE.MeshStandardMaterial({ color: c, roughness: r, metalness: m });
    const glow = (c, k = 2.6) => new THREE.MeshStandardMaterial({ color: c, emissive: c, emissiveIntensity: k, roughness: 0.3 });
    this.mat = { body: std('#e9ecef'), dark: std('#2a2e36', 0.6, 0.2), teal: std(CHARS.fix.trim, 0.45, 0.2), hazard: glow(HAZARD, 0.4), mint: glow(MINT), cyan: glow(CYAN), amber: glow('#ffcf5a'),
      off: std('#3a404a', 0.5, 0.2),
      field: new THREE.MeshBasicMaterial({ map: fx.tex.ring, color: MINT, transparent: true, opacity: 0.4, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide }),
      coilField: new THREE.MeshBasicMaterial({ map: fx.tex.ring, color: CYAN, transparent: true, opacity: 0.4, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide }) };
    this.capMats = {};
    for (const k of [...FIX.powers, 'ultcell', 'fury']) this.capMats[k] = glow(FIX_LOOK[k].tint, 1.8);
  }

  // ---- Gadget models (built once a gadget appears; shared materials) ----
  build(g) {
    const M = this.mat, root = new THREE.Group(), add = (geo, mat, x = 0, y = 0, z = 0) => { const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); m.castShadow = true; root.add(m); return m; };
    const G = { root, pips: [], kind: g.kind, born: this.t, hit: 0 };
    if (g.kind === 'pylon') {
      for (let i = 0; i < 3; i++) { const a = i * 2.1, leg = add(new THREE.CylinderGeometry(0.025, 0.035, 0.5, 6), M.dark, Math.cos(a) * 0.18, 0.2, Math.sin(a) * 0.18); leg.rotation.set(Math.sin(a) * 0.4, 0, -Math.cos(a) * 0.4); }
      add(new THREE.CylinderGeometry(0.12, 0.16, 0.18, 12), M.teal, 0, 0.42);
      add(new THREE.CapsuleGeometry(0.09, 0.5, 4, 12), M.body, 0, 0.82);
      G.ring = add(new THREE.TorusGeometry(0.16, 0.035, 8, 20), M.mint, 0, 1.12); G.ring.rotation.x = Math.PI / 2;
      G.core = add(new THREE.SphereGeometry(0.09, 12, 10), M.mint, 0, 1.26);
      G.field = new THREE.Mesh(new THREE.PlaneGeometry(2, 2), M.field); G.field.rotation.x = -Math.PI / 2; G.field.position.y = 0.04; root.add(G.field);
    } else if (g.kind === 'sentry') {
      for (let i = 0; i < 3; i++) { const a = i * 2.1, leg = add(new THREE.CylinderGeometry(0.025, 0.03, 0.55, 6), M.dark, Math.cos(a) * 0.2, 0.22, Math.sin(a) * 0.2); leg.rotation.set(Math.sin(a) * 0.5, 0, -Math.cos(a) * 0.5); }
      add(new THREE.CylinderGeometry(0.08, 0.1, 0.16, 10), M.teal, 0, 0.5);
      const head = new THREE.Group(); head.position.set(0, 0.74, 0); root.add(head);
      const box = new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.22, 0.26), M.body); head.add(box);
      const stripe = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.04, 0.27), M.hazard); stripe.position.y = -0.06; head.add(stripe);
      const barrel = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.05, 0.36, 10), M.dark); barrel.rotation.z = -Math.PI / 2; barrel.position.set(0.3, 0.02, 0); head.add(barrel);
      const eye = new THREE.Mesh(new THREE.SphereGeometry(0.045, 10, 8), M.amber); eye.position.set(0.17, 0.05, 0.08); head.add(eye);
      const pod = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.12, 0.12), M.teal); pod.position.set(-0.05, 0.16, -0.08); pod.visible = false; head.add(pod);
      G.head = head; G.pod = pod; G.muzzle = new THREE.Vector3();
    } else if (g.kind === 'coil') {
      add(new THREE.CylinderGeometry(0.2, 0.26, 0.14, 14), M.dark, 0, 0.07);
      add(new THREE.CylinderGeometry(0.05, 0.07, 1.2, 10), M.teal, 0, 0.72);
      G.rings = [0.45, 0.75, 1.05].map((y, i) => { const r = add(new THREE.TorusGeometry(0.2 - i * 0.04, 0.03, 8, 20), M.cyan, 0, y); r.rotation.x = Math.PI / 2; return r; });
      G.core = add(new THREE.SphereGeometry(0.13, 14, 10), M.cyan, 0, 1.42);
      G.field = new THREE.Mesh(new THREE.PlaneGeometry(2, 2), M.coilField); G.field.rotation.x = -Math.PI / 2; G.field.position.y = 0.05; root.add(G.field);
    } else {
      // Jack-Up's spring pad: a base plate, a coiled spring and a mint pad on top
      add(new THREE.CylinderGeometry(0.5, 0.55, 0.06, 18), M.dark, 0, 0.03);
      G.spring = new THREE.Group(); root.add(G.spring);
      for (let i = 0; i < 4; i++) { const r = new THREE.Mesh(new THREE.TorusGeometry(0.28, 0.03, 6, 18), M.hazard); r.rotation.x = Math.PI / 2; r.position.y = 0.08 + i * 0.05; G.spring.add(r); }
      G.top = add(new THREE.CylinderGeometry(0.46, 0.46, 0.05, 18), M.mint, 0, 0.3);
    }
    // Level lights: one lit per level
    if (g.kind !== 'pad') for (let i = 0; i < 3; i++) { const pip = add(new THREE.SphereGeometry(0.035, 8, 6), M.off, -0.1 + i * 0.1, g.kind === 'sentry' ? 0.42 : 0.3, 0.2); G.pips.push(pip); }
    this.scene.add(root);
    return G;
  }
  pickupMesh(kind, level = false) {
    const g = new THREE.Group(), body = new THREE.Mesh(new THREE.CapsuleGeometry(0.13, 0.22, 4, 12), this.capMats[kind]); body.rotation.z = Math.PI / 2; g.add(body);
    if (level) {
      // Power-ups along the route stand out from a distance: bigger, with a column of light over them
      g.scale.setScalar(1.7);
      const beam = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.fx.tex.glow, color: FIX_LOOK[kind].tint, transparent: true, opacity: 0.35, blending: THREE.AdditiveBlending, depthWrite: false }));
      beam.scale.set(0.35, 3.2, 1); beam.position.y = 1.2; g.add(beam);
    }
    const band = new THREE.Mesh(new THREE.TorusGeometry(0.15, 0.03, 6, 16), this.mat.body); band.rotation.y = Math.PI / 2; g.add(band);
    const glow = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.fx.tex.glow, color: FIX_LOOK[kind].tint, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false }));
    glow.scale.setScalar(0.9); g.add(glow);
    this.scene.add(g);
    return g;
  }

  onEvent(ev) {
    const F = this.fx, p = ev.p;
    switch (ev.type) {
      case 'gadgetSelect': case 'powerSelect': {
        const L = FIX_LOOK[ev.kind]; F.popText(p.x, p.y + p.h + 0.7, L.name.toUpperCase(), L.tint, 0.6);
        F.sprite(p.x + p.facing * 0.3, p.y + 1.0, 'ring', L.tint, 0.5, 0.2, 2); break;
      }
      case 'gadgetDeploy': {
        const rig = F.rigs.get(p); if (rig) rig.craneT = 0.7;
        const g = ev.g; F.burst(g.x, g.gy + 0.4, HAZARD, 14, 4, 0.22, 0.3, { dir: Math.PI / 2, spread: 1.8, grav: 8 }); F.sprite(g.x, g.gy + 0.5, 'ring', MINT, 0.9, 0.25, 2.4);
        F.popText(g.x, g.gy + 1.9, FIX_LOOK[g.kind].name.toUpperCase(), FIX_LOOK[g.kind].tint, 0.7);
        break;
      }
      case 'gadgetLand': F.dust(ev.g.x, ev.g.y, 0.3, [0, Math.PI], { noRing: true }); break;
      case 'gadgetUp': {
        const g = ev.g; F.sprite(g.x, g.y + 0.8, 'star', WHITE, 1.6, 0.18, 1.5); F.sprite(g.x, g.y + 0.8, 'ring', MINT, 1.2, 0.3, 2.8);
        F.burst(g.x, g.y + 0.8, MINT, 24, 6, 0.28, 0.4); F.popText(g.x, g.y + 2.0, `LEVEL ${g.level}`, MINT, 0.75);
        const G = this.gadgets.get(g); if (G) G.pop = 1;
        break;
      }
      case 'gadgetWrench': { const g = ev.g; F.burst(g.x, g.y + 0.6, ev.max ? MINT : HAZARD, 12, 6, 0.2, 0.25, { grav: 10 }); F.sprite(g.x, g.y + 0.6, 'star', WHITE, 0.9, 0.12, 1.4); break; }
      case 'gadgetHit': { const G = this.gadgets.get(ev.g); if (G) G.hit = 1; F.burst(ev.g.x, ev.g.y + 0.6, '#ffd2e4', 6, 4, 0.2, 0.2); break; }
      case 'gadgetEnd': {
        const g = ev.g;
        if (ev.why === 'broken') { F.burst(g.x, g.y + 0.5, '#5d6674', 18, 7, 0.28, 0.6, { grav: 14 }); F.burst(g.x, g.y + 0.6, HAZARD, 14, 6, 0.24, 0.35); F.smoke(g.x, g.y + 0.5, '#7d8692', 6, 1.2, 0.5, 0.8, { op: 0.45 }); }
        else { F.burst(g.x, g.y + 0.5, g.kind === 'coil' ? CYAN : MINT, 12, 3, 0.24, 0.4, { dir: Math.PI / 2, spread: 1.2 }); }
        break;
      }
      case 'sentryShot': F.sprite(ev.x, ev.y, 'star', '#fff1c9', 0.5, 0.06, 1.3); break;
      case 'sentryRocket': F.smoke(ev.x, ev.y, '#8e97a3', 3, 1, 0.35, 0.5, { op: 0.45 }); F.burst(ev.x, ev.y, '#ffcf5a', 8, 4, 0.22, 0.2); break;
      case 'padPlace': F.dust(ev.x, ev.y, 0.5, [0, Math.PI]); F.sprite(ev.x, ev.y + 0.2, 'ring', MINT, 0.9, 0.25, 2.4); break;
      case 'padBounce': {
        F.sprite(ev.x, ev.y + 0.3, 'ring', MINT, 1.1, 0.25, 2.6); F.burst(ev.x, ev.y + 0.3, MINT, 16, 7, 0.24, 0.3, { dir: Math.PI / 2, spread: 1.2 });
        const G = this.gadgets.get(ev.g); if (G) G.boing = 1;
        break;
      }
      case 'powerToss': F.sprite(ev.x, ev.y, 'ring', FIX_LOOK[ev.kind].tint, 0.5, 0.16, 2); break;
      case 'powerUp': {
        const q = ev.p, c = chest(q), L = FIX_LOOK[ev.kind];
        F.sprite(c.x, c.y, 'ring', L.tint, 1.4, 0.35, 2.6); F.burst(c.x, c.y, L.tint, 24, 5, 0.28, 0.45); F.popText(c.x, c.y + 1.3, L.name.toUpperCase(), L.tint, 0.75);
        break;
      }
      case 'powerFade': F.burst(ev.x, ev.y, FIX_LOOK[ev.kind].tint, 8, 2, 0.2, 0.3); break;
      case 'noScrap': {
        if (this.t - (this.noScrapT || -9) < 0.6) break;
        this.noScrapT = this.t; F.popText(p.x, p.y + p.h + 0.7, ev.spot ? 'NO FLOOR' : 'NEED SCRAP', '#ffb4a0', 0.6);
        break;
      }
      case 'scrap': {
        // Bits of scrap fly from the wreck to her
        const c = chest(p), n = 10;
        for (let i = 0; i < n; i++) {
          const w = toWorld(ev.x + (Math.random() - 0.5) * 0.6, ev.y + (Math.random() - 0.5) * 0.6, (Math.random() - 0.5) * 0.6, new THREE.Vector3()), to = toWorldZ(c.x, c.y, c.z, new THREE.Vector3());
          const pt = F.particle(w, Math.random() < 0.5 ? HAZARD : '#c8cdd4', 0.16, 0.45); pt.v.copy(to).sub(w).multiplyScalar(2.2); pt.v.y += 2 + Math.random() * 2; pt.drag = 0.97;
        }
        break;
      }
      case 'rivetStick': F.sprite(ev.x, ev.y, 'star', '#ffcf9a', 0.6, 0.1, 1.4); F.burst(ev.x, ev.y, '#ffb070', 6, 4, 0.16, 0.2, { grav: 8 }); break;
      case 'rivetBlast': {
        const x = ev.x, y = ev.y, r = ev.r;
        F.fireball(x, y, '#ff9a4a', 0.6 + r * 0.4, 0.22); F.sprite(x, y, 'ring', MINT, r * 0.9, 0.25, 2.4); F.sprite(x, y, 'star', WHITE, r * 0.7, 0.14, 1.4);
        F.burst(x, y, HAZARD, 16 + 6 * (ev.level || 1), 7 + r * 2, 0.3, 0.35, { grav: 8 }); F.dust(x, y, 0.3, [0, Math.PI], { reach: 1 + r * 0.4 });
        break;
      }
      case 'sparkRing': {
        F.sprite(ev.x, ev.y, 'ring', CYAN, ev.r * 0.8, 0.3, 3); F.sprite(ev.x, ev.y, 'star', WHITE, 1.4, 0.14, 1.5);
        for (let i = 0; i < 26; i++) { const a = Math.random() * Math.PI * 2; F.burst(ev.x + Math.cos(a) * 0.3, ev.y + Math.sin(a) * 0.3, Math.random() < 0.5 ? WHITE : CYAN, 1, 9, 0.2, 0.25, { dir: a, spread: 0.2 }); }
        this.arcBurst(ev.x, ev.y, ev.r);
        break;
      }
      case 'repairPulse': F.groundRing(ev.x, ev.y, MINT, 0.3, ev.r, 0.35, 0.8); F.burst(ev.x, ev.y + 0.5, MINT, 14, 4, 0.24, 0.4, { dir: Math.PI / 2, spread: 1.6 }); break;
      case 'patchOn': case 'patchTarget': if (ev.q) { const c = chest(ev.q); LANE.z = c.z; F.sprite(c.x, c.y, 'ring', MINT, 1.0, 0.25, 2.2); } break;
      case 'podCall': {
        // A landing marker for the supply pod
        const P = this.podOf(p); P.x = ev.x; P.y = ev.y; P.z = ev.z || 0; P.t = 0; P.drop = ev.ticks / 60; P.landed = false; P.alive = true;
        F.groundRing(ev.x, ev.y, MINT, 0.5, 2.4, ev.ticks / 60, 0.8);
        break;
      }
      case 'podLand': {
        const x = ev.x, y = ev.y, P = this.podOf(p); P.landed = true; P.t = 0;
        F.sprite(x, y + 1, 'star', WHITE, 4, 0.3, 1.6); F.fireball(x, y + 0.6, '#9ff5d0', 2.2, 0.35);
        F.groundRing(x, y, WHITE, 0.5, 4.5, 0.45, 0.9); F.groundRing(x, y, MINT, 0.4, 3.4, 0.4, 0.9);
        F.dust(x, y, 1.3, [0, Math.PI], { reach: 2 }); F.burst(x, y + 0.4, '#5d6674', 24, 10, 0.32, 0.7, { dir: Math.PI / 2, spread: 2.4, grav: 18 });
        F.smoke(x, y + 0.6, '#8e97a3', 10, 2, 1.0, 1.1, { op: 0.45, grow: 2.4, grav: -0.8 });
        break;
      }
      case 'overhaulPulse': {
        const x = ev.x, y = ev.y, k = 1 + ev.i * 0.25;
        for (const [sz, life, g] of [[2.5 * k, 0.5, 6], [1.6 * k, 0.7, 8]]) F.sprite(x, y, 'ring', MINT, sz, life, g);
        F.sprite(x, y, 'ring', WHITE, 1.2 * k, 0.4, 7); F.sprite(x, y, 'glow', PALE, 5 * k, 0.4, 1.6);
        F.burst(x, y, MINT, 60, 16, 0.36, 0.6); F.burst(x, y, WHITE, 24, 10, 0.26, 0.4);
        const fl = F.floorUnder(x, y, 3); if (fl !== null) F.groundRing(x, fl, MINT, 0.6, 9 * k, 0.6, 0.85);
        break;
      }
      case 'overhaulDone': {
        const P = this.pods.get(p); if (P) P.fade = 0.5;
        F.sprite(ev.x, ev.y + 1.2, 'star', WHITE, 3, 0.25, 1.6);
        break;
      }
    }
  }
  podOf(p) {
    let P = this.pods.get(p); if (P) return P;
    const g = new THREE.Group(), M = this.mat;
    const shell = new THREE.Mesh(new THREE.CapsuleGeometry(0.55, 1.0, 6, 16), M.body); shell.position.y = 1.1; g.add(shell);
    const band = new THREE.Mesh(new THREE.CylinderGeometry(0.58, 0.58, 0.18, 18), M.teal); band.position.y = 1.0; g.add(band);
    const lights = new THREE.Mesh(new THREE.TorusGeometry(0.58, 0.04, 6, 24), M.mint); lights.rotation.x = Math.PI / 2; lights.position.y = 1.5; g.add(lights);
    for (let i = 0; i < 3; i++) { const fin = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.6, 0.4), M.dark); const a = i * 2.1; fin.position.set(Math.cos(a) * 0.55, 0.45, Math.sin(a) * 0.55); fin.rotation.y = -a; g.add(fin); }
    const stripe = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.9, 0.02), M.hazard); stripe.position.set(0, 1.2, 0.56); g.add(stripe);
    g.visible = false; this.scene.add(g);
    P = { g, t: 0, alive: false, landed: false, fade: 0 }; this.pods.set(p, P);
    return P;
  }
  // A crackle of short arcs from a point
  arcBurst(x, y, r) {
    for (let i = 0; i < 4; i++) {
      const a = Math.random() * Math.PI * 2, d = r * (0.6 + Math.random() * 0.4), pts = [];
      const b = Math.random() * Math.PI * 2;   // (out across the floor plane too)
      for (let j = 0; j <= 6; j++) { const u = j / 6, jt = (Math.random() - 0.5) * 0.3 * (u > 0 && u < 1); pts.push({ x: x + Math.cos(a) * d * u + jt, y: y + Math.sin(a) * d * u * 0.6 + jt, z: LANE.z + Math.sin(b) * d * u }); }
      this.addArc(pts, CYAN, 0.12);
    }
  }
  addArc(pts, color, life) {
    let A = this.arcs.find(a => a.life <= 0);
    if (!A) { if (this.arcs.length >= 10) A = this.arcs[0]; else { A = { glow: new Strip(this.scene, 5), core: new Strip(this.scene, 6), life: 0 }; this.arcs.push(A); } }
    A.pts = pts.map(q => toWorldZ(q.x, q.y, q.z ?? LANE.z, new THREE.Vector3())); A.life = A.max = life; A.color = new THREE.Color(color);
  }

  update(dt, world, view) {
    this.t += dt;
    const F = this.fx, cam = view.camera.position;
    // ---- The Patch Beam ----
    const seenB = new Set();
    for (const p of world.players) {
      if (p.char !== 'fix' || p.state !== 'patch' || !p.patch) continue;
      const rig = view.rigs.get(p); if (!rig || !rig.root.visible) continue;
      seenB.add(p);
      let B = this.beams.get(p);
      if (!B) { B = { glow: new Strip(this.scene, 4), core: new Strip(this.scene, 5), pts: [], k: 0 }; this.beams.set(p, B); }
      B.k = Math.min(1, B.k + dt * 8);
      const tip = rig.extra.weldTip.getWorldPosition(this.v), q = p.patch.target;
      if (!q) {
        // Welding her own kit: sparks spray off the welder
        B.glow.mesh.visible = B.core.mesh.visible = false;
        for (let i = 0; i < 3; i++) { const pt = F.particle(tip, Math.random() < 0.6 ? '#fff6d0' : HAZARD, 0.12, 0.3); pt.v.set((Math.random() - 0.5) * 4, Math.random() * 3, (Math.random() - 0.5) * 2); pt.grav = 9; pt.drag = 0.95; }
        continue;
      }
      const end = toWorldZ(q.x, q.y + q.h * (q.state === 'downed' ? 0.3 : 0.55), q.z, this.v2), n = 18, pts = B.pts; pts.length = 0;
      const tw = tip.clone();
      for (let i = 0; i <= n; i++) {
        const u = i / n, w = new THREE.Vector3().lerpVectors(tw, end, u);
        const s = Math.sin(u * Math.PI);
        w.y += s * (0.35 + 0.1 * Math.sin(this.t * 9)) + Math.sin(this.t * 31 + u * 14) * 0.05 * s;
        pts.push(w);
      }
      const k = B.k, downed = q.state === 'downed';
      B.glow.build(pts, cam, i => (0.2 + 0.06 * Math.sin(this.t * 20 + i)) * k, () => (downed ? [1.2, 1.0, 0.5, 0.45] : [0.25, 1.0, 0.7, 0.5]));
      B.core.build(pts, cam, () => 0.045 * k, () => (downed ? [2.0, 1.8, 1.2, 1] : [0.9, 2.0, 1.5, 1]));
      // Repair rising off whoever she patches
      if (Math.random() < 0.6) {
        const w = toWorldZ(q.x + (Math.random() - 0.5) * q.w, q.y + Math.random() * q.h, q.z + (Math.random() - 0.5) * q.w, new THREE.Vector3()), pt = F.particle(w, Math.random() < 0.5 ? PALE : MINT, 0.18, 0.5);
        pt.v.set(0, 1.6, 0); pt.drag = 0.98;
      }
      if (Math.random() < 0.4) { const pt = F.particle(tip, WHITE, 0.14, 0.12); pt.v.set((Math.random() - 0.5) * 3, Math.random() * 2, 0); }
    }
    for (const [p, B] of this.beams) if (!seenB.has(p)) { B.glow.mesh.visible = B.core.mesh.visible = false; B.k = 0; if (!world.players.includes(p)) this.beams.delete(p); }
    // ---- Gadgets ----
    const seenG = new Set();
    for (const g of world.gadgets || []) {
      seenG.add(g);
      let G = this.gadgets.get(g);
      if (!G) { G = this.build(g); this.gadgets.set(g, G); }
      const y = g.py + (g.y - g.py) * (view.alpha ?? 1);
      toWorldZ(g.x, y, g.z || 0, G.root.position); LANE.z = g.z || 0;
      G.root.rotation.y = yawOf(g.x);
      const age = this.t - G.born, grow = Math.min(1, age / 0.25), pop = (G.pop = Math.max(0, (G.pop || 0) - dt * 3));
      G.hit = Math.max(0, G.hit - dt * 6);
      G.root.scale.setScalar(grow * (1 + 0.25 * pop) * (g.kind === 'pad' && G.boing ? 1 : 1));
      const endK = Math.min(1, (g.life - g.t) / 45);
      G.root.visible = endK > 0.05 && (endK > 0.5 || Math.floor(this.t * 12) % 2 === 0);
      for (let i = 0; i < G.pips.length; i++) G.pips[i].material = i < g.level ? (g.kind === 'coil' ? this.mat.cyan : this.mat.mint) : this.mat.off;
      if (g.kind === 'pylon') {
        const r = FIX.gadget.pylon.r[g.level - 1];
        G.ring.rotation.z += dt * 2; G.core.scale.setScalar(1 + 0.15 * Math.sin(this.t * 5));
        G.field.scale.setScalar(r * (0.97 + 0.03 * Math.sin(this.t * 3))); this.mat.field.opacity = 0.22 + 0.1 * Math.sin(this.t * 3);
        if (Math.random() < 0.25) F.burst(g.x + (Math.random() - 0.5) * r * 1.4, g.y + 0.2, MINT, 1, 1, 0.16, 0.7, { dir: Math.PI / 2, spread: 0.2 });
      } else if (g.kind === 'sentry') {
        // The head turns to its target (the gadget's own frame faces along the path): a yaw round, then a pitch up
        const ax = g.aim ?? 1, az = g.aimZ || 0, yaw = Math.atan2(-az, ax), pitch = Math.atan2(g.aimY || 0, Math.hypot(ax, az) || 1e-3);
        let dy = yaw - G.head.rotation.y; while (dy > Math.PI) dy -= Math.PI * 2; while (dy < -Math.PI) dy += Math.PI * 2;
        G.head.rotation.y += dy * Math.min(1, dt * 14); G.head.rotation.z += (pitch - G.head.rotation.z) * Math.min(1, dt * 14);
        G.pod.visible = g.level >= 3;
      } else if (g.kind === 'coil') {
        const r = FIX.gadget.coil.r[g.level - 1];
        G.rings.forEach((m, i) => { m.rotation.z += dt * (2 + i); m.scale.setScalar(1 + 0.1 * Math.sin(this.t * 8 + i)); });
        G.field.scale.setScalar(r); this.mat.coilField.opacity = 0.2 + 0.1 * Math.sin(this.t * 6);
        // Arcs to the teammates in its field now and then
        if (Math.random() < 0.08) {
          const top = { x: g.x, y: g.y + 1.42, z: g.z || 0 };
          for (const q of world.players) {
            if (q.state === 'dead' || Math.hypot(q.x - g.x, q.y + q.h * 0.5 - (g.y + 0.8), q.z - top.z) > r) continue;
            const c = chest(q), pts = [];
            for (let j = 0; j <= 7; j++) { const u = j / 7, jt = () => (j && j < 7 ? (Math.random() - 0.5) * 0.4 : 0); pts.push({ x: top.x + (c.x - top.x) * u + jt(), y: top.y + (c.y - top.y) * u + jt(), z: top.z + (c.z - top.z) * u + jt() }); }
            this.addArc(pts, CYAN, 0.1);
          }
        }
      } else if (g.kind === 'pad') {
        G.boing = Math.max(0, (G.boing || 0) - dt * 5);
        G.spring.scale.y = 1 + 1.6 * G.boing; G.top.position.y = 0.3 + 0.3 * G.boing;
      }
      if (G.hit > 0) G.root.position.x += (Math.random() - 0.5) * 0.04 * G.hit;
    }
    for (const [g, G] of this.gadgets) if (!seenG.has(g)) { this.scene.remove(G.root); G.root.traverse(o => { if (o.geometry) o.geometry.dispose(); }); this.gadgets.delete(g); }
    // ---- Arcs ----
    for (const A of this.arcs) {
      if (A.life <= 0) { A.glow.mesh.visible = A.core.mesh.visible = false; continue; }
      A.life -= dt; const k = Math.max(0, A.life / A.max), c = A.color;
      A.glow.build(A.pts, cam, () => 0.1, () => [c.r, c.g, c.b, 0.5 * k]);
      A.core.build(A.pts, cam, () => 0.03, () => [1.6, 1.8, 2, k]);
    }
    // ---- Power-up capsules ----
    const seenK = new Set();
    for (const k of world.pickups || []) {
      seenK.add(k);
      let m = this.pickups.get(k);
      if (!m) { m = this.pickupMesh(k.kind, !!k.level); this.pickups.set(k, m); }
      const a = view.alpha ?? 1, x = k.px + (k.x - k.px) * a, y = k.py + (k.y - k.py) * a, z = (k.pz ?? k.z) + (k.z - (k.pz ?? k.z)) * a;
      LANE.z = z; toWorldZ(x, y + (k.rest ? 0.12 + Math.sin(this.t * 4 + k.id) * 0.06 : 0), z, m.position);
      m.rotation.y += dt * 3; m.visible = k.life > 90 || Math.floor(this.t * 10) % 2 === 0;
      if (!k.rest && Math.random() < 0.7) F.burst(x, y, FIX_LOOK[k.kind].tint, 1, 0.5, 0.18, 0.25);
    }
    for (const [k, m] of this.pickups) if (!seenK.has(k)) { this.scene.remove(m); m.traverse(o => { if (o.geometry) o.geometry.dispose(); if (o.isSprite) o.material.dispose(); }); this.pickups.delete(k); }
    // ---- Boosts on everyone: Plating shimmers; Overclock throws off sparks ----
    for (const p of world.players) {
      if (p.state === 'dead') continue;
      const rig = view.rigs.get(p); if (!rig || !rig.root.visible) continue;
      LANE.z = p.z;
      if (p.plate > 0.5 && Math.random() < 0.18 + p.plate / 200) {
        const a = Math.random() * Math.PI * 2, c = chest(p), r = p.h * 0.5, w = toWorld(c.x + Math.cos(a) * r * 0.6, c.y + Math.sin(a) * r, 0.35, new THREE.Vector3());
        const pt = F.particle(w, PLATE, 0.2, 0.3); pt.v.set(0, 0.3, 0); pt.drag = 0.9;
      }
      if (p.overclockT > 0 && Math.random() < 0.5) {
        const w = toWorld(p.x + (Math.random() - 0.5) * p.w, p.y + Math.random() * p.h, 0.35, new THREE.Vector3()), pt = F.particle(w, Math.random() < 0.5 ? WHITE : CYAN, 0.16, 0.3);
        pt.v.set(0, 3 + Math.random() * 2, 0); pt.drag = 0.92;
      }
      // ...and a quick cyan pulse at their feet a couple of times a second, so a boosted teammate reads at a glance
      if (p.overclockT > 0 && p.onGround && Math.floor(this.t * 2.2) !== Math.floor((this.t - dt) * 2.2)) F.groundRing(p.x, p.y, CYAN, 0.3, 1.2, 0.28, 0.55);
      if (p.ampK > 1 && Math.random() < 0.12) {
        const w = toWorld(p.x, p.y + p.h * 0.5, 0.35, new THREE.Vector3()), pt = F.particle(w, CYAN, 0.12, 0.25); pt.v.set((Math.random() - 0.5) * 2, 1.5, 0);
      }
    }
    // ---- Overhaul's supply pod ----
    for (const [p, P] of this.pods) {
      if (!P.alive) { P.g.visible = false; continue; }
      P.t += dt;
      if (P.fade > 0) { P.fade -= dt; if (P.fade <= 0) { P.alive = false; P.g.visible = false; F.burst(P.x, P.y + 1, MINT, 20, 4, 0.26, 0.5, { dir: Math.PI / 2, spread: 1.6 }); continue; } }
      const u = P.landed ? 1 : Math.min(1, P.t / Math.max(0.05, P.drop));
      LANE.z = P.z || 0; toWorldZ(P.x, P.y + (1 - u) * (1 - u) * 22, P.z || 0, P.g.position);
      P.g.rotation.y = yawOf(P.x); P.g.visible = true;
      P.g.scale.setScalar(P.fade > 0 ? Math.max(0.01, P.fade / 0.5) : 1);
      if (!P.landed) { F.burst(P.x, P.y + (1 - u) * (1 - u) * 22 + 2.2, '#fff1c9', 2, 3, 0.4, 0.2, { dir: Math.PI / 2, spread: 0.4 }); F.smoke(P.x, P.y + (1 - u) * (1 - u) * 22 + 2.4, '#8e97a3', 1, 1, 0.5, 0.6, { op: 0.4 }); }
      else if (Math.random() < 0.3) F.burst(P.x, P.y + 1.6, MINT, 1, 2, 0.2, 0.4, { dir: Math.PI / 2, spread: 0.6 });
      if (!world.players.includes(p)) { P.alive = false; P.g.visible = false; }
    }
    LANE.z = 0;
  }

  warmShow(at) {
    const out = [];
    for (const kind of ['pylon', 'sentry', 'coil', 'pad']) { const G = this.build({ kind }); G.root.position.copy(at); out.push(G.root); this.warmG = (this.warmG || []).concat(G); }
    const k = this.pickupMesh('overclock'); k.position.copy(at); out.push(k); this.warmK = k;
    const P = this.podOf('warm'); P.g.position.copy(at); out.push(P.g);
    return out;
  }
  warmDone() {
    for (const G of this.warmG || []) { G.root.visible = false; this.scene.remove(G.root); }
    if (this.warmK) { this.warmK.visible = false; this.scene.remove(this.warmK); }
    const P = this.pods.get('warm'); if (P) { P.g.visible = false; this.scene.remove(P.g); this.pods.delete('warm'); }
  }
}
