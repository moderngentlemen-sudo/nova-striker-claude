// Charge and launch presentation. While a shot charges, energy gathers at the muzzle as a growing orb
// pulled in from around it, and each attachment has its own look (Lance: streaks drawn in along the aim
// and a sight line at level 3; Volley: one orbiting mote per dart; Arc: a heavy shell core and a dotted
// trajectory; Prism: a spinning crystal). Level-ups flash, the Perfect Release window pulses white, and
// the release throws a muzzle flash, a shockwave ring along the aim and sparks. Also here: ribbon trails
// behind charged projectiles, the rocket apex marker, Echo's laser sight, and the charged-dash aura.
// Presentation only: reads the sim, never changes it.
import * as THREE from 'three';
import { toWorld, toWorldZ, planeDir, planeDir3, LANE } from './space.js';
import { pointInSolid, rayCast, rayBoxT } from './level.js';
import { MARKSMAN, ATTACH_LOOK, CHARS, HUNTER, DASH_CHARGE, NOVA, SUB_LOOK, RAM, FIX } from './config.js';
import { chargeStage, burstStage, marksman, rocketHeight, rifleFocus, chest } from './player.js';
import { hurtbox } from './combat.js';

const WHITE = new THREE.Color('#ffffff');
const Y = new THREE.Vector3(0, 1, 0);
const RIBBON_MAT = new THREE.MeshBasicMaterial({ vertexColors: true, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
// Ribbon look per projectile kind: points kept, half-width (m)
const TRAIL = {
  lance: [8, 0.13], rail: [11, 0.2], dart: [6, 0.05], shell: [10, 0.12], prism: [8, 0.12], shard: [4, 0.045],
  rifle: [7, 0.055], markShot: [11, 0.12], deflected: [9, 0.12], breach: [10, 0.16], hotRivet: [6, 0.06],
};

// A camera-facing strip through the last few positions of something fast, fading toward the tail
class Ribbon {
  constructor(scene, n, width, color) {
    this.n = n; this.width = width; this.color = new THREE.Color(color); this.pts = []; this.fade = 1; this.orphan = false;
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(new Float32Array(n * 6), 3));
    g.setAttribute('color', new THREE.BufferAttribute(new Float32Array(n * 6), 3));
    const idx = []; for (let i = 0; i < n - 1; i++) { const a = i * 2; idx.push(a, a + 1, a + 2, a + 1, a + 3, a + 2); }
    g.setIndex(idx);
    this.mesh = new THREE.Mesh(g, RIBBON_MAT); this.mesh.frustumCulled = false; this.mesh.renderOrder = 3; scene.add(this.mesh);
    this.scene = scene;
  }
  push(v) { this.pts.unshift(v.clone()); if (this.pts.length > this.n) this.pts.pop(); }
  rebuild(cam) {
    const pos = this.mesh.geometry.attributes.position.array, col = this.mesh.geometry.attributes.color.array;
    const m = this.pts.length, dir = new THREE.Vector3(), toCam = new THREE.Vector3(), side = new THREE.Vector3();
    for (let i = 0; i < this.n; i++) {
      const j = Math.min(i, m - 1), p = this.pts[j];
      if (!p) { pos.fill(0); col.fill(0); break; }
      dir.copy(this.pts[Math.max(0, j - 1)]).sub(this.pts[Math.min(m - 1, j + 1)]);
      if (dir.lengthSq() < 1e-8) dir.set(1, 0, 0); dir.normalize();
      toCam.copy(cam).sub(p).normalize();
      const w = i < m ? this.width * (1 - i / this.n) : 0;
      side.crossVectors(dir, toCam).normalize().multiplyScalar(w);
      pos[i * 6] = p.x + side.x; pos[i * 6 + 1] = p.y + side.y; pos[i * 6 + 2] = p.z + side.z;
      pos[i * 6 + 3] = p.x - side.x; pos[i * 6 + 4] = p.y - side.y; pos[i * 6 + 5] = p.z - side.z;
      const k = i < m ? (1 - i / (this.n - 1)) * this.fade : 0;
      for (const o of [0, 3]) { col[i * 6 + o] = this.color.r * k; col[i * 6 + o + 1] = this.color.g * k; col[i * 6 + o + 2] = this.color.b * k; }
    }
    this.mesh.geometry.attributes.position.needsUpdate = true; this.mesh.geometry.attributes.color.needsUpdate = true;
  }
  dispose() { this.scene.remove(this.mesh); this.mesh.geometry.dispose(); }
}

export class ChargeFX {
  constructor(fx) {
    this.fx = fx; this.scene = fx.scene; this.tex = fx.tex;
    this.state = new Map(); this.trails = new Map(); this.flashes = [];
    this.v = new THREE.Vector3(); this.v2 = new THREE.Vector3(); this.v3 = new THREE.Vector3(); this.dir = new THREE.Vector3();
    this.t = 0;
  }

  sprite(tex, blend = THREE.AdditiveBlending) {
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.tex[tex], transparent: true, depthWrite: false, blending: blend }));
    s.visible = false; s.renderOrder = 4; this.scene.add(s); return s;
  }
  line(color, radius = 0.02) {
    const m = new THREE.Mesh(new THREE.CylinderGeometry(radius, radius, 1, 6, 1, true),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.5, blending: THREE.AdditiveBlending, depthWrite: false }));
    m.visible = false; m.renderOrder = 3; this.scene.add(m); return m;
  }
  // Stretch a line mesh between two world points
  span(m, a, b) {
    const d = this.v3.copy(b).sub(a), len = d.length();
    if (len < 1e-3) { m.visible = false; return; }
    m.position.copy(a).addScaledVector(d, 0.5); m.scale.set(1, len, 1);
    m.quaternion.setFromUnitVectors(Y, d.divideScalar(len)); m.visible = true;
  }

  // Per-player pieces, made the first time they are needed
  of(p) {
    let S = this.state.get(p);
    if (S) return S;
    S = {
      // The orb is solid colour (reads on bright scenes) with an additive white-hot core on top
      orb: this.sprite('glow', THREE.NormalBlending), core: this.sprite('glow'), halo: this.sprite('ring'),
      motes: Array.from({ length: 9 }, () => this.sprite('glow', THREE.NormalBlending)),
      crystal: new THREE.Mesh(new THREE.OctahedronGeometry(0.15), new THREE.MeshStandardMaterial({ color: '#fff0c8', emissive: '#ffd889', emissiveIntensity: 1.6, roughness: 0.15, metalness: 0.3, flatShading: true })),
      sight: this.line('#fff1c9', 0.018), laser: this.line('#ff9a1f', 0.016), laserDot: this.sprite('glow'),
      dots: null, aura: null, apex: null, spin: 0, land: null,
    };
    S.crystal.visible = false; S.crystal.renderOrder = 6; this.scene.add(S.crystal);
    // Arc trajectory preview: a row of dots
    const n = 22, g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(new Float32Array(n * 3), 3));
    S.dots = new THREE.Points(g, new THREE.PointsMaterial({ color: ATTACH_LOOK.arc.tint, size: 0.2, map: this.tex.glow, transparent: true, opacity: 0.95, depthWrite: false }));
    S.dots.frustumCulled = false; S.dots.visible = false; this.scene.add(S.dots); S.dotN = n;
    // Charged-dash aura: a ring on the ground
    S.aura = new THREE.Mesh(new THREE.PlaneGeometry(2.4, 2.4), new THREE.MeshBasicMaterial({ map: this.tex.ring, transparent: true, opacity: 0.6, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide }));
    S.aura.rotation.x = -Math.PI / 2; S.aura.visible = false; this.scene.add(S.aura);
    // Arc: where the shell would burst, its blast radius on the surface
    S.land = new THREE.Mesh(new THREE.PlaneGeometry(2, 2), new THREE.MeshBasicMaterial({ map: this.tex.ring, color: ATTACH_LOOK.arc.tint, transparent: true, opacity: 0.55, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide }));
    S.land.rotation.x = -Math.PI / 2; S.land.visible = false; this.scene.add(S.land);
    // Rocket apex marker: a line at the height he will reach, ticks for each level, and a thin beam up to it
    const apex = new THREE.Group(); apex.visible = false; this.scene.add(apex);
    const lineMat = new THREE.MeshBasicMaterial({ color: '#ffe2a8', transparent: true, opacity: 0.85, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
    const bar = new THREE.Mesh(new THREE.PlaneGeometry(1.1, 0.06), lineMat); apex.add(bar);
    const star = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.tex.star, color: '#fff1c9', transparent: true, depthWrite: false, blending: THREE.AdditiveBlending }));
    star.scale.setScalar(0.55); apex.add(star);
    const ticks = Array.from({ length: 4 }, () => { const m = new THREE.Mesh(new THREE.PlaneGeometry(0.45, 0.035), lineMat.clone()); this.scene.add(m); m.visible = false; return m; });
    S.apex = { group: apex, bar, star, ticks, beam: this.line('#ffe2a8', 0.012), mat: lineMat };
    this.state.set(p, S);
    return S;
  }

  // Where the charge gathers: Nova's bracer muzzle, the tip of Echo's rifle, or in front of the chest
  muzzle(p, rig, out) {
    if (rig && (p.char === 'nova' || p.char === 'ram' || p.char === 'fix') && rig.extra.muzzle) return rig.extra.muzzle.getWorldPosition(out);
    if (rig && p.char === 'echo' && rig.extra.staffTip && rig.extra.handStaff.visible) return rig.extra.staffTip.getWorldPosition(out);
    return toWorldZ(p.x + p.facing * 0.45, p.y + p.h * 0.62, (p.z || 0) + (p.facingZ || 0) * 0.45, out);
  }

  // A player left: take their pieces out of the scene and free them
  dispose(S) {
    const objs = [S.orb, S.core, S.halo, S.crystal, S.sight, S.laser, S.laserDot, S.dots, S.aura, S.land, S.apex.group, S.apex.beam, ...S.motes, ...S.apex.ticks];
    for (const o of objs) {
      this.scene.remove(o);
      o.traverse(q => { if (q.geometry && !q.isSprite) q.geometry.dispose(); if (q.material) q.material.dispose(); });
    }
  }

  hide(S) {
    S.orb.visible = S.core.visible = S.halo.visible = S.crystal.visible = S.sight.visible = S.laser.visible = S.laserDot.visible = false;
    S.dots.visible = S.aura.visible = S.apex.group.visible = S.apex.beam.visible = S.land.visible = false;
    S.motes.forEach(m => { m.visible = false; }); S.apex.ticks.forEach(m => { m.visible = false; });
  }

  update(dt, world, view) {
    this.t += dt;
    const seen = new Set();
    for (const p of world.players) {
      seen.add(p);
      const S = this.of(p), rig = view.rigs.get(p);
      this.hide(S); LANE.z = p.z || 0;
      if (!rig || p.state === 'downed' || p.state === 'dead' || !rig.root.visible) continue;
      if (p.char === 'nova') this.novaCharge(p, S, rig, world, view);
      else if (p.char === 'echo') this.echoRifle(p, S, rig, world);
      else this.weaponCharge(p, S, rig);
      if (p.state === 'dashCharge') this.dashAura(p, S, rig);
    }
    LANE.z = 0;
    for (const [p, S] of this.state) if (!seen.has(p)) { this.dispose(S); this.state.delete(p); }
    this.updateTrails(dt, view.camera.position);
    this.updateFlashes(dt);
  }

  // ---- Nova ----
  novaCharge(p, S, rig, world, view) {
    const mk = marksman(p), C = MARKSMAN.charge, B = MARKSMAN.burst.charge;
    const stage = chargeStage(p), bstage = mk ? burstStage(p) : '';
    const f = mk ? Math.min(1, p.chargeT / C[2]) : Math.min(1, p.chargeT / NOVA.charge2);
    const fb = mk ? Math.min(1, p.burstT / B[2]) : 0;
    const L4 = MARKSMAN.beam.at, l4 = mk && p.chargeT >= L4, f4 = mk ? Math.max(0, Math.min(1, (p.chargeT - C[2]) / (L4 - C[2]))) : 0;
    const level = l4 ? 4 : mk ? (p.chargeT >= C[2] ? 3 : p.chargeT >= C[1] ? 2 : p.chargeT >= C[0] ? 1 : 0) : (p.chargeT >= NOVA.charge2 ? 2 : p.chargeT >= NOVA.charge1 ? 1 : 0);
    if (!l4) this.apexMarker(p, S, world, view);
    if (f <= 0 && fb <= 0) return;
    const burst = fb > f, k = Math.max(f, fb) + (burst ? 0 : 0.45 * f4), perfect = stage === 'perfect' || bstage === 'perfect';
    const attach = mk ? p.attachment : 'lance', over = p.overcharge > 0;
    let tint = burst ? (SUB_LOOK[p.sub] || SUB_LOOK.scatter).tint : mk ? ATTACH_LOOK[attach].tint : CHARS.nova.energy;
    if (over || l4) tint = '#' + new THREE.Color(tint).lerp(WHITE, l4 ? 0.55 : 0.3).getHexString();   // Overcharge and Level 4 burn whiter
    const at = this.muzzle(p, rig, this.v);
    const t = this.t, pulse = 1 + Math.sin(t * (perfect || l4 ? 55 : 18)) * (perfect ? 0.22 : l4 ? 0.14 : 0.06 * k);
    const big = !burst && attach === 'arc' ? 1.3 : !burst && attach === 'prism' && !l4 ? 0.6 : 1;   // the Prism's crystal is the show
    // The orb and its white-hot core grow with the charge (and keep swelling on the way to Level 4)
    S.orb.position.copy(at); S.orb.material.color.set(perfect || l4 ? '#fff4d6' : burst ? (p.sub === 'scatter' ? '#ff9f40' : tint) : attach === 'prism' ? '#ffd889' : tint);
    S.orb.scale.setScalar((0.16 + 0.55 * k) * big * pulse); S.orb.material.opacity = 0.65 + 0.3 * Math.min(1, k); S.orb.visible = true;
    S.core.position.copy(at); S.core.material.color.set('#ffffff'); S.core.scale.setScalar((0.1 + 0.3 * k) * big * pulse); S.core.visible = true;
    if (level >= 2 || perfect || (burst && fb >= B[1] / B[2])) {
      S.halo.position.copy(at); S.halo.material.color.set(perfect || l4 ? '#ffffff' : tint);
      S.halo.scale.setScalar((0.5 + 0.5 * k) * big * (perfect ? 1.3 + Math.sin(t * 40) * 0.2 : l4 ? 1.5 + Math.sin(t * 24) * 0.12 : 1)); S.halo.material.rotation = t * (l4 ? 9 : 3);
      S.halo.material.opacity = perfect || l4 ? 0.95 : 0.55; S.halo.visible = true;
    }
    if (!burst && mk && f4 > 0) {
      // On the way to Level 4: arcs of energy crackle off the orb, more and more of them
      for (let i = 0; i < 1 + f4 * 3; i++) {
        if (Math.random() > 0.35 + 0.5 * f4) continue;
        const a = Math.random() * Math.PI * 2, r = 0.25 + 0.3 * f4, P = this.fx.particle(this.v2.copy(at), Math.random() < 0.6 ? '#ffffff' : tint, 0.1 + 0.08 * f4, 0.07);
        planeDir(p.x, Math.cos(a) * r / 0.07, Math.sin(a) * r / 0.07, P.v); P.drag = 0.8; P.grav = 0;
      }
    }
    if (l4) this.beamPreview(p, S, at);
    // Energy drawn in from around the muzzle, faster with each level
    const rate = 0.6 + (burst ? 2 : Math.min(level, 3)) * 0.9 + (perfect ? 2 : 0) + (l4 ? 2.5 : 0) + (over ? 0.8 : 0);
    const dir = planeDir3(p.x, p.aimX, p.aimY, p.aimZ || 0, this.dir).normalize();   // its own vector: inward() reuses v2
    for (let i = 0; i < rate; i++) {
      if (Math.random() > rate - i) break;
      if (!burst && attach === 'lance') {
        // Lance: streaks pulled in from ahead, along the aim
        const d = 1.1 + Math.random() * 1.3, j = (Math.random() - 0.5) * 0.5;
        this.inward(at, dir.x * d, dir.y * d + j, dir.z * d, tint, 0.13, 0.11);
      } else {
        const a = Math.random() * Math.PI * 2, r = 0.8 + Math.random() * 0.6;
        const tv = planeDir(p.x, Math.cos(a) * r, Math.sin(a) * r, this.v3);
        this.inward(at, tv.x, tv.y, tv.z, Math.random() < 0.3 ? '#ffffff' : tint, 0.15, 0.2);
      }
    }
    if (burst || l4) return;
    if (attach === 'lance' && level >= 3) {
      // Level 3 Lance: a sight line down the aim
      const end = this.v3.copy(at).addScaledVector(dir, 6);
      this.span(S.sight, at, end); S.sight.material.opacity = perfect ? 0.85 : 0.35; S.sight.material.color.set(perfect ? '#ffffff' : '#fff1c9'); S.sight.scale.x = S.sight.scale.z = 1;
    } else if (attach === 'volley') {
      // Volley: one orbiting mote per dart the release would fire
      const n = MARKSMAN.volley.darts[perfect ? 'perfect' : Math.max(1, level)], r = 0.28 + 0.12 * k;
      for (let i = 0; i < n; i++) {
        const a = t * 7 + i * Math.PI * 2 / n, m = S.motes[i];
        const o = planeDir(p.x, Math.cos(a) * r, Math.sin(a) * r, this.v3);
        m.position.copy(at).add(o); m.material.color.set(level ? '#ffc04d' : '#9aa6b8'); m.scale.setScalar(level ? 0.2 : 0.12); m.visible = true;
      }
    } else if (attach === 'arc' && level >= 1) {
      this.arcPreview(p, S, level, perfect);
    } else if (attach === 'prism') {
      S.spin += 0.12 + 0.2 * k;
      S.crystal.position.copy(at).addScaledVector(dir, 0.25); S.crystal.rotation.set(S.spin * 0.7, S.spin, 0); S.crystal.scale.setScalar(0.7 + 1.6 * k); S.crystal.visible = true;
      if (Math.random() < 0.08 + 0.2 * k) this.flash(this.v3.copy(at).add(this.v2.set((Math.random() - 0.5) * 0.4, (Math.random() - 0.5) * 0.4, 0.05)), 'star', ATTACH_LOOK.prism.tint, 0.25 + 0.3 * k, 0.12, 1.2);
    }
  }

  // Level 4 is ready: a flickering white guide line where the beam will go, to the first wall
  beamPreview(p, S, at) {
    const c = chest(p), az = p.aimZ || 0, h = rayCast(c.x + p.aimX * 0.6, c.y + p.aimY * 0.6, c.z + az * 0.6, p.aimX, p.aimY, az, MARKSMAN.beam.range);
    const end = toWorldZ(h.x, h.y, h.z, this.v3);
    this.span(S.sight, at, end); S.sight.material.color.set('#ffffff');
    S.sight.material.opacity = 0.35 + 0.3 * Math.abs(Math.sin(this.t * 22)); S.sight.scale.x = S.sight.scale.z = 1.4;
  }

  // A particle that flies from `at + (ox, oy, oz)` into `at` over its life
  inward(at, ox, oy, oz, color, size, life) {
    const P = this.fx.particle(this.v2.set(at.x + ox, at.y + oy, at.z + oz), color, size, life);
    P.v.set(-ox / life, -oy / life, -oz / life); P.drag = 1; P.grav = 0;
  }

  // Dotted path of the Arc shell for the current aim, until it meets something solid
  arcPreview(p, S, level, perfect) {
    const A = MARKSMAN.arc, az = p.aimZ || 0, c = { x: p.x, y: p.y + p.h * 0.62, z: p.z };
    const dx = p.aimX, dy = p.aimY + A.lift, m = Math.hypot(dx, dy, az) || 1;
    let x = c.x + p.aimX * 0.7, y = c.y + p.aimY * 0.7, z = c.z + az * 0.7, vx = dx / m * A.speed, vy = dy / m * A.speed, vz = az / m * A.speed;
    const pos = S.dots.geometry.attributes.position.array, dt = 0.045;
    let i = 0, hit = false;
    for (; i < S.dotN; i++) {
      for (let s = 0; s < 3 && !hit; s++) { vy -= A.gravity * dt / 3; x += vx * dt / 3; y += vy * dt / 3; z += vz * dt / 3; if (pointInSolid(x, y, z)) hit = true; }
      toWorldZ(x, y, z, this.v3); pos[i * 3] = this.v3.x; pos[i * 3 + 1] = this.v3.y; pos[i * 3 + 2] = this.v3.z;
      if (hit) { i++; break; }
    }
    for (let j = i; j < S.dotN; j++) { pos[j * 3] = pos[(i - 1) * 3]; pos[j * 3 + 1] = pos[(i - 1) * 3 + 1]; pos[j * 3 + 2] = pos[(i - 1) * 3 + 2]; }
    S.dots.geometry.attributes.position.needsUpdate = true;
    S.dots.material.color.set(perfect ? '#fff1c9' : '#ff8a1f'); S.dots.material.size = 0.18 + 0.04 * level; S.dots.visible = true;
    if (hit) {
      // Where it will burst: the blast radius for this level
      const r = A[level].r * (perfect ? A.perfectRadius : 1);
      toWorldZ(x, y + 0.05, z, S.land.position); S.land.scale.setScalar(r * (1 + Math.sin(this.t * 12) * 0.04));
      S.land.material.color.set(perfect ? '#ffffff' : ATTACH_LOOK.arc.tint); S.land.visible = true;
    }
  }

  // Rocket apex marker: while he charges aiming at his feet, a line at the height he would reach if he
  // let go now, with faint ticks for charge levels 1-3 and the Perfect Release
  apexMarker(p, S, world, view) {
    const pv = world.rocketPreview(p);
    if (!pv) return;
    const A = S.apex, cam = view.camera, r = (pv.apex - p.y) / Math.max(0.01, pv.h);
    toWorld(pv.x, pv.apex + 0.02, 0.3, A.group.position); A.group.quaternion.copy(cam.quaternion);
    const col = pv.perfect ? '#ffffff' : ATTACH_LOOK[p.attachment].tint;
    A.mat.color.set(col); A.mat.opacity = pv.perfect ? 0.95 : 0.7 + 0.2 * Math.sin(this.t * 10);
    A.star.material.color.set(col); A.star.scale.setScalar(pv.perfect ? 0.8 + Math.sin(this.t * 40) * 0.15 : 0.5);
    A.group.visible = true;
    const head = toWorld(p.x, p.y + p.h + 0.15, 0.3, this.v), top = toWorld(pv.x, pv.apex - 0.05, 0.3, this.v2);
    this.span(A.beam, head, top); A.beam.material.opacity = 0.28;
    const C = MARKSMAN.charge, hs = [rocketHeight(C[0], p.attachment, false), rocketHeight(C[1], p.attachment, false), rocketHeight(C[2], p.attachment, false), rocketHeight(C[2], p.attachment, true)];
    hs.forEach((h, i) => {
      const m = A.ticks[i], y = p.y + h * r;
      toWorld(pv.x, y, 0.3, m.position); m.quaternion.copy(cam.quaternion);
      m.material.color.set(i === 3 ? '#ffffff' : '#ffe2a8'); m.material.opacity = y <= pv.apex + 0.05 ? 0.55 : 0.22; m.visible = true;
    });
  }

  // ---- Echo ----
  // The sniper's laser follows the exact line the shot will take: to the first wall, or the first enemy in the
  // way. Searching (nothing under it) it flickers; resting on an enemy it holds solid; fully charged it turns
  // red. Focus narrows it from a wide faint band to a thin line. On the crit zone (upper body) the dot becomes
  // a sharp star.
  echoRifle(p, S, rig, world) {
    const R = HUNTER.rifle;
    if (p.rifleT < R.raise || p.state === 'attack') return;
    const ready = p.rifleCd === 0, k = rifleFocus(p.rifleT), full = k >= 1;
    const az = p.aimZ || 0, at = this.muzzle(p, rig, this.v), c = chest(p), x0 = c.x + p.aimX * 0.9, y0 = c.y + p.aimY * 0.9, z0 = c.z + az * 0.9;
    let tEnd = rayCast(x0, y0, z0, p.aimX, p.aimY, az, R.range).t, hitE = null;
    for (const e of world.enemies) {
      if (e.dead) continue;
      const hb = hurtbox(e), h = rayBoxT(x0, y0, z0, p.aimX, p.aimY, az, { x0: hb.x0 - 0.06, y0: hb.y0 - 0.06, z0: hb.z0 - 0.06, x1: hb.x1 + 0.06, y1: hb.y1 + 0.06, z1: hb.z1 + 0.06 });
      if (h && h.t < tEnd) { tEnd = h.t; hitE = e; }
    }
    const crit = hitE && y0 + p.aimY * (tEnd + 0.15) > hitE.y + hitE.h * R.critZone;
    const end = toWorldZ(x0 + p.aimX * tEnd, y0 + p.aimY * tEnd, z0 + az * tEnd, this.v2);
    this.span(S.laser, at, end);
    const red = '#ff2414', col = full ? red : '#ff9a1f';
    // Searching: a nervous flicker with dropouts. On a target: steady.
    const search = !hitE, flick = search ? (Math.random() < 0.18 ? 0.15 : 0.55 + Math.random() * 0.45) : 1;
    S.laser.material.color.set(col);
    S.laser.material.opacity = !ready ? 0.1 : (full ? 0.85 : 0.2 + 0.45 * k) * flick;
    S.laser.scale.x = S.laser.scale.z = full ? 1.6 : 3.4 - 2.6 * k;
    S.laserDot.position.copy(end);
    const dotTex = crit ? this.tex.star : this.tex.glow;
    if (S.laserDot.material.map !== dotTex) { S.laserDot.material.map = dotTex; S.laserDot.material.needsUpdate = true; }
    S.laserDot.material.color.set(full ? red : hitE ? '#ffffff' : '#ff9a1f'); S.laserDot.material.opacity = flick;
    S.laserDot.material.rotation = crit ? this.t * 4 : 0;
    S.laserDot.scale.setScalar((hitE ? 0.45 : 0.25) * (full ? 1.4 : 1) * (crit ? 1.9 : 1)); S.laserDot.visible = ready;
    // Charge glint at the tip; a red scope flare when fully charged
    S.orb.position.copy(at); S.orb.material.color.set(full ? red : CHARS.echo.energy);
    S.orb.scale.setScalar((0.12 + 0.3 * k) * (full ? 1.2 + Math.sin(this.t * 30) * 0.15 : 1)); S.orb.material.opacity = ready ? 0.9 : 0.35; S.orb.visible = true;
    if (full && ready) { S.halo.position.copy(at); S.halo.material.color.set(red); S.halo.scale.setScalar(0.55 + Math.sin(this.t * 18) * 0.08); S.halo.material.rotation = this.t * 2; S.halo.material.opacity = 0.8; S.halo.visible = true; }
    if (ready && Math.random() < 0.3 + 0.5 * k) {
      const a = Math.random() * Math.PI * 2, r = 0.5 + Math.random() * 0.4, tv = planeDir(p.x, Math.cos(a) * r, Math.sin(a) * r, this.v3);
      this.inward(at, tv.x, tv.y, tv.z, full ? red : CHARS.echo.energy, 0.12, 0.16);
    }
  }

  // ---- RAM's cannon and Fix's rivet gun: an orb at the muzzle that grows through the levels ----
  weaponCharge(p, S, rig) {
    const C = p.char === 'ram' ? RAM.cannon.charge : FIX.rivet.charge;
    if (!(p.chargeT > 0)) return;
    const k = Math.min(1, p.chargeT / C[2]), level = p.chargeT >= C[2] ? 3 : p.chargeT >= C[1] ? 2 : p.chargeT >= C[0] ? 1 : 0;
    const tint = CHARS[p.char].energy, at = this.muzzle(p, rig, this.v), pulse = 1 + Math.sin(this.t * 18) * 0.06 * k;
    S.orb.position.copy(at); S.orb.material.color.set(level >= 3 ? '#ffffff' : tint); S.orb.scale.setScalar((0.14 + 0.5 * k) * pulse * (p.char === 'ram' ? 1.3 : 1));
    S.orb.material.opacity = 0.65 + 0.3 * k; S.orb.visible = true;
    S.core.position.copy(at); S.core.material.color.set('#ffffff'); S.core.scale.setScalar((0.08 + 0.26 * k) * pulse); S.core.visible = true;
    if (level >= 2) { S.halo.position.copy(at); S.halo.material.color.set(tint); S.halo.scale.setScalar(0.5 + 0.5 * k); S.halo.material.rotation = this.t * 3; S.halo.material.opacity = 0.55; S.halo.visible = true; }
    for (let i = 0; i < 1 + level; i++) {
      const a = Math.random() * Math.PI * 2, r = 0.7 + Math.random() * 0.5, tv = planeDir(p.x, Math.cos(a) * r, Math.sin(a) * r, this.v3);
      this.inward(at, tv.x, tv.y, tv.z, Math.random() < 0.3 ? '#ffffff' : tint, 0.13, 0.2);
    }
  }

  // ---- Both: charged dash ----
  dashAura(p, S, rig) {
    const C = DASH_CHARGE.charge, t = p.dashChargeT;
    if (t < DASH_CHARGE.tap) return;
    const level = t >= C[2] ? 3 : t >= C[1] ? 2 : t >= C[0] ? 1 : 0, k = Math.min(1, t / C[2]);
    const col = new THREE.Color(CHARS[p.char].energy).lerp(WHITE, level >= 3 ? 0.55 : level * 0.15);
    toWorld(p.x, p.y + 0.04, 0, S.aura.position);
    S.aura.material.color.copy(col); S.aura.material.opacity = 0.35 + 0.45 * k;
    S.aura.scale.setScalar((0.55 + 0.5 * k) * (1 + Math.sin(this.t * (10 + level * 8)) * 0.06)); S.aura.visible = true;
    // Energy gathers into the body, faster with each level
    const at = toWorld(p.x, p.y + 0.75, 0.1, this.v);
    for (let i = 0; i < 1 + level * 1.5; i++) {
      const a = Math.random() * Math.PI * 2, r = 1.0 + Math.random() * 0.6, tv = planeDir(p.x, Math.cos(a) * r, Math.sin(a) * r * 0.8, this.v3);
      this.inward(at, tv.x, tv.y, tv.z, Math.random() < 0.3 ? '#ffffff' : '#' + col.getHexString(), 0.16, 0.22);
    }
  }

  // ---- Events ----
  levelUp(p, rig, level, kind) {
    const at = kind === 'dash' ? toWorld(p.x, p.y + 0.8, 0.2, this.v) : this.muzzle(p, rig, this.v);
    const tint = kind === 'burst' ? (SUB_LOOK[p.sub] || SUB_LOOK.scatter).tint : kind === 'dash' || kind === 'pound' || p.char === 'ram' || p.char === 'fix' ? CHARS[p.char].energy : kind === 'rifle' ? CHARS.echo.energy
      : marksman(p) ? ATTACH_LOOK[p.attachment].tint : CHARS.nova.energy;
    const top = level >= 3;
    this.flash(at, 'ring', top ? '#ffffff' : tint, 0.35 + level * 0.18, 0.18, 3.2);
    this.flash(at, 'star', '#ffffff', 0.35 + level * 0.25, 0.14, 1.5);
    if (level >= 4) { this.flash(at, 'glow', '#fff1c9', 1.6, 0.2, 1.8); this.shockRing(at.clone(), new THREE.Vector3(0, 0, 1), '#ffffff', 0.3, 1.4, 0.25, 0); }
    for (let i = 0; i < 8 + level * 5; i++) {
      const a = Math.random() * Math.PI * 2, sp = 3 + level * 1.5, P = this.fx.particle(at, Math.random() < 0.4 ? '#ffffff' : tint, 0.16, 0.22);
      planeDir(p.x, Math.cos(a) * sp, Math.sin(a) * sp, P.v); P.drag = 0.88; P.grav = 0;
    }
  }

  // A charged shot leaves: flash, a shockwave ring travelling out along the aim, sparks, and for a full
  // Lance a white line down its path
  release(ev, p, rig) {
    const L = ev.level || 0, perfect = !!ev.perfect, attach = ev.attach || 'lance';
    const at = this.muzzle(p, rig, this.v).clone();
    const tint = ev.rifle ? CHARS.echo.energy : ev.cannon ? CHARS.ram.energy : ev.rivet ? CHARS.fix.energy : ATTACH_LOOK[attach] ? ATTACH_LOOK[attach].tint : CHARS.nova.energy;
    const dir = planeDir3(p.x, ev.ax ?? p.aimX, ev.ay ?? p.aimY, ev.az ?? p.aimZ ?? 0, new THREE.Vector3()).normalize();
    const s = (perfect ? 1.5 : 1) * (0.7 + L * 0.35);
    this.flash(at, 'star', '#ffffff', 0.9 * s, 0.12, 1.4);
    this.flash(at, 'glow', perfect ? '#ffffff' : tint, 1.4 * s, 0.16, 1.8);
    this.shockRing(at, dir, perfect ? '#ffffff' : tint, 0.25 * s, 1.1 * s, 0.2 + 0.03 * L, 1.2 + 0.4 * L);
    for (let i = 0; i < 10 + L * 7 + (perfect ? 10 : 0); i++) {
      const spread = 0.35 + Math.random() * 0.5, sp = 7 + Math.random() * 9 + L * 2;
      const P = this.fx.particle(at, Math.random() < 0.35 ? '#ffffff' : tint, 0.18 + 0.05 * L, 0.2 + Math.random() * 0.12);
      P.v.copy(dir).multiplyScalar(sp).add(this.v2.set((Math.random() - 0.5) * sp * spread, (Math.random() - 0.5) * sp * spread, (Math.random() - 0.5) * sp * spread * 0.5));
      P.drag = 0.86; P.grav = 3;
    }
    if (!ev.beam && ((attach === 'lance' && (L >= 3 || perfect)) || ev.mark)) {
      const m = this.line(perfect ? '#ffffff' : ev.mark ? '#ffe0b0' : '#fff1c9', perfect ? 0.06 : 0.04);
      this.span(m, at, this.v3.copy(at).addScaledVector(dir, ev.mark ? 22 : 16));
      this.flashes.push({ m, life: 0.14, max: 0.14, base: 0.9, dispose: true });
    }
  }

  // A ring that faces along `dir`, grows from r0 to r1 and moves `travel` m forward as it fades
  shockRing(at, dir, color, r0, r1, life, travel) {
    const m = new THREE.Mesh(new THREE.RingGeometry(0.72, 1, 32), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.8, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide }));
    m.position.copy(at); m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 0, 1), dir); m.scale.setScalar(r0); m.renderOrder = 4;
    this.scene.add(m);
    this.flashes.push({ m, life, max: life, base: 0.8, r0, r1, dir: dir.clone(), travel, from: at.clone(), dispose: true });
  }
  flash(at, tex, color, size, life, grow) {
    const s = this.sprite(tex); s.material.color.set(color); s.position.copy(at); s.scale.setScalar(size); s.visible = true;
    this.flashes.push({ m: s, life, max: life, base: 1, size, grow, dispose: true, sprite: true });
  }
  updateFlashes(dt) {
    for (const f of this.flashes) {
      f.life -= dt; const k = 1 - Math.max(0, f.life) / f.max;
      f.m.material.opacity = f.base * (1 - k) * (1 - k * 0.3);
      if (f.sprite) f.m.scale.setScalar(f.size * (1 + (f.grow - 1) * k));
      if (f.r0 !== undefined) { f.m.scale.setScalar(f.r0 + (f.r1 - f.r0) * (1 - (1 - k) * (1 - k))); f.m.position.copy(f.from).addScaledVector(f.dir, f.travel * k); }
      if (f.life <= 0) { this.scene.remove(f.m); if (f.m.geometry) f.m.geometry.dispose(); f.m.material.dispose(); f.dead = true; }
    }
    this.flashes = this.flashes.filter(f => !f.dead);
  }

  // ---- Ribbon trails behind charged projectiles ----
  wantsTrail(pr) {
    if (pr.deflected) return true;
    if (pr.team !== 'p' || !TRAIL[pr.kind]) return false;
    return pr.kind === 'rifle' || pr.kind === 'markShot' || (pr.level || 0) >= 1;
  }
  trail(pr, pos) {
    let r = this.trails.get(pr);
    if (!r) {
      const [n, w] = TRAIL[pr.deflected ? 'deflected' : pr.kind], lv = pr.deflected ? 1 : pr.level || 1;
      const col = pr.reflected || pr.kind === 'breach' ? CHARS.ram.energy : pr.kind === 'hotRivet' ? '#ff9a4a' : pr.deflected ? CHARS.echo.energy : pr.perfect ? '#fff4d6' : pr.kind === 'rifle' || pr.kind === 'markShot' ? (pr.kind === 'markShot' ? '#ffc070' : CHARS.echo.energy)
        : pr.kind === 'dart' ? ATTACH_LOOK.volley.tint : pr.kind === 'shell' ? ATTACH_LOOK.arc.tint : pr.kind === 'prism' || pr.kind === 'shard' ? ATTACH_LOOK.prism.tint : ATTACH_LOOK.lance.tint;
      r = new Ribbon(this.scene, n, w * (0.8 + 0.15 * lv) * (pr.perfect ? 1.3 : 1), col);
      this.trails.set(pr, r);
    }
    r.push(pos);
  }
  // Trails whose projectile is no longer in the world (a zone reset clears them without killing them)
  orphanUnseen(seen) { for (const [pr, r] of this.trails) if (!seen.has(pr)) r.orphan = true; }
  updateTrails(dt, cam) {
    for (const [pr, r] of this.trails) {
      if (pr.dead || r.orphan) {
        // The projectile is gone: the tail catches up and fades
        r.orphan = true; r.pts.pop(); r.fade -= dt * 6;
        if (!r.pts.length || r.fade <= 0) { r.dispose(); this.trails.delete(pr); continue; }
      }
      r.rebuild(cam);
    }
  }
}
