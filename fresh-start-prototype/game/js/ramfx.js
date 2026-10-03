// RAM's effects. The Rampart's hard-light pane where the sim's shield stands (like Nova's Aegis: it flares when it
// blocks, cracks where each hit lands, the cracks spreading as its Integrity runs low and healing as it comes
// back, chips off shards as it weakens and shatters into them when it breaks; pushed along the floor it grinds
// out sparks); the Bulwark Wall; the Ram Charge's
// hard-light wedge, streaks and dust; the Guardian Link's tether; the Kinetic Release's cone of force; the
// Hydraulic Uplift's jets; Provoke's roar; Siege Breaker's colossal ram's head; the sparks his charge throws
// up; and the craters his big impacts leave in floors and walls. Presentation only: reads the sim, never
// changes it.
import * as THREE from 'three';
import { CHARS, RAM, ULT } from './config.js';
import { toWorld, planeDir } from './space.js';
import { pathFrame, groundBelow, pointInSolid } from './level.js';
import { chest } from './player.js';
import { Strip } from './beamfx.js';

const BLUE = CHARS.ram.energy, PALE = '#cfe6ff', WHITE = '#ffffff';
const SPARKS = ['#fff1c4', '#ffc24a', '#ff8a1f', '#ff6a00'];   // hot steel on stone
const CRATER = { life: 9, fade: 2.5, glow: 1.2, pool: 10 };   // seconds a crater stays, fades out over, its cracks glow for

function canvasTex(size, draw) {
  const c = document.createElement('canvas'); c.width = c.height = size;
  draw(c.getContext('2d'), size);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}
// Hard light: a hex lattice that is brightest at its rim; `cracks` lines run across it (shown as it weakens)
const hexTex = cracks => canvasTex(256, (g, s) => {
  const grad = g.createLinearGradient(0, 0, s, 0);
  grad.addColorStop(0, 'rgba(255,255,255,0.95)'); grad.addColorStop(0.18, 'rgba(255,255,255,0.28)'); grad.addColorStop(0.82, 'rgba(255,255,255,0.28)'); grad.addColorStop(1, 'rgba(255,255,255,0.95)');
  g.fillStyle = grad; g.fillRect(0, 0, s, s);
  g.strokeStyle = 'rgba(255,255,255,0.75)'; g.lineWidth = 2;
  const r = s / 10;
  for (let row = -1; row < 12; row++) for (let col = -1; col < 8; col++) {
    const cx = col * r * 1.75 + (row % 2 ? r * 0.87 : 0), cy = row * r * 1.5;
    g.beginPath(); for (let i = 0; i <= 6; i++) { const a = Math.PI / 3 * i + Math.PI / 6; g.lineTo(cx + Math.cos(a) * r * 0.95, cy + Math.sin(a) * r * 0.95); } g.stroke();
  }
  if (cracks) {
    g.strokeStyle = 'rgba(255,255,255,1)'; g.lineWidth = 3;
    for (let k = 0; k < 7; k++) {
      let x = s * (0.2 + Math.random() * 0.6), y = s * Math.random(); g.beginPath(); g.moveTo(x, y);
      for (let i = 0; i < 6; i++) { x += (Math.random() - 0.5) * s * 0.25; y += (Math.random() - 0.5) * s * 0.3; g.lineTo(x, y); }
      g.stroke();
    }
  }
});

// A crater: a scorched hollow with a broken rim and cracks running out of it (normal blending), and the same
// cracks again as a glow map (still hot with hard light for a moment after the impact)
const craterTex = (seed, glow) => canvasTex(256, (g, s) => {
  let r = seed; const rnd = () => ((r = (r * 16807) % 2147483647) / 2147483647);
  const c = s / 2, R = s * 0.3;
  const cracks = [];
  for (let k = 0; k < 9; k++) {
    let a = (k / 9) * Math.PI * 2 + rnd() * 0.5, d = R * (0.5 + rnd() * 0.3); const pts = [[c + Math.cos(a) * d, c + Math.sin(a) * d]];
    const len = R * (0.7 + rnd() * 0.75);
    for (let i = 0; i < 5; i++) { d += len / 5; a += (rnd() - 0.5) * 0.45; pts.push([c + Math.cos(a) * d, c + Math.sin(a) * d]); }
    cracks.push(pts);
  }
  const crack = (w, col) => { g.strokeStyle = col; g.lineCap = 'round'; for (const pts of cracks) { g.lineWidth = w; g.beginPath(); g.moveTo(...pts[0]); for (const q of pts.slice(1)) { g.lineTo(...q); g.lineWidth *= 0.8; } g.stroke(); } };
  if (glow) { crack(6, 'rgba(255,255,255,0.95)'); const gg = g.createRadialGradient(c, c, 0, c, c, R); gg.addColorStop(0, 'rgba(255,255,255,0.8)'); gg.addColorStop(1, 'rgba(255,255,255,0)'); g.fillStyle = gg; g.fillRect(0, 0, s, s); return; }
  // the broken rim: a jagged ring of churned-up ground
  g.beginPath();
  for (let i = 0; i <= 40; i++) { const a = (i / 40) * Math.PI * 2, rr = R * (1.08 + (rnd() - 0.5) * 0.2); g.lineTo(c + Math.cos(a) * rr, c + Math.sin(a) * rr); }
  g.fillStyle = 'rgba(120,128,140,0.55)'; g.fill();
  // the hollow, darkest at its heart
  const hg = g.createRadialGradient(c, c, 0, c, c, R);
  hg.addColorStop(0, 'rgba(8,10,14,0.95)'); hg.addColorStop(0.6, 'rgba(22,26,32,0.85)'); hg.addColorStop(1, 'rgba(40,45,54,0.6)');
  g.beginPath();
  for (let i = 0; i <= 40; i++) { const a = (i / 40) * Math.PI * 2, rr = R * (0.92 + (rnd() - 0.5) * 0.14); g.lineTo(c + Math.cos(a) * rr, c + Math.sin(a) * rr); }
  g.fillStyle = hg; g.fill();
  crack(5, 'rgba(10,12,16,0.85)');
  // flecks of debris thrown around it
  g.fillStyle = 'rgba(30,34,40,0.7)';
  for (let i = 0; i < 26; i++) { const a = rnd() * Math.PI * 2, d = R * (1.1 + rnd() * 0.6), z = 1.5 + rnd() * 3; g.fillRect(c + Math.cos(a) * d, c + Math.sin(a) * d, z, z); }
});

// A crack in hard light: main fractures out from the point of impact, branches off them, and a pale bruise
// at the heart. White on transparent (tinted and faded per crack).
const crackStarTex = seed => canvasTex(256, (g, s) => {
  let r = seed; const rnd = () => ((r = (r * 16807) % 2147483647) / 2147483647);
  const c = s / 2; g.lineCap = 'round'; g.lineJoin = 'round';
  const walk = (x, y, a, n, step, w, alpha, branch) => {
    g.strokeStyle = `rgba(255,255,255,${alpha})`; g.lineWidth = w; g.beginPath(); g.moveTo(x, y);
    for (let i = 0; i < n; i++) {
      a += (rnd() - 0.5) * 0.7; x += Math.cos(a) * step; y += Math.sin(a) * step; g.lineTo(x, y);
      if (branch && rnd() < 0.35) { g.stroke(); walk(x, y, a + (rnd() < 0.5 ? -1 : 1) * (0.5 + rnd() * 0.6), 3 + (rnd() * 3 | 0), step * 0.7, w * 0.55, alpha * 0.8, false); g.strokeStyle = `rgba(255,255,255,${alpha})`; g.lineWidth = w; g.beginPath(); g.moveTo(x, y); }
    }
    g.stroke();
  };
  const n = 6 + (rnd() * 3 | 0);
  for (let k = 0; k < n; k++) walk(c, c, (k / n) * Math.PI * 2 + rnd() * 0.6, 6 + (rnd() * 4 | 0), s * 0.055, 4, 1, true);
  for (let k = 0; k < 10; k++) { const a = rnd() * Math.PI * 2, d = s * (0.08 + rnd() * 0.12); walk(c + Math.cos(a) * d, c + Math.sin(a) * d, a + Math.PI / 2, 2, s * 0.035, 1.5, 0.7, false); }   // crazing
  const gg = g.createRadialGradient(c, c, 0, c, c, s * 0.12); gg.addColorStop(0, 'rgba(255,255,255,0.9)'); gg.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = gg; g.fillRect(0, 0, s, s);
});

export class RamFX {
  constructor(fx) {
    this.fx = fx; this.scene = fx.scene; this.t = 0;
    this.tex = { hex: hexTex(false), cracked: hexTex(true) };
    for (const k in this.tex) { this.tex[k].wrapS = this.tex[k].wrapT = THREE.RepeatWrapping; }
    // Shared materials (one set of shaders however many RAMs and walls there are)
    const plane = (map, color, op) => new THREE.MeshBasicMaterial({ map, color, transparent: true, opacity: op, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide, toneMapped: false });
    this.mat = { pane: plane(this.tex.hex, BLUE, 0.6), crack: plane(this.tex.cracked, PALE, 0.0), wall: plane(this.tex.hex, BLUE, 0.55), wallCrack: plane(this.tex.cracked, PALE, 0),
      wedge: new THREE.MeshBasicMaterial({ color: new THREE.Color(BLUE).multiplyScalar(1.3), transparent: true, opacity: 0.5, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }),
      edge: new THREE.MeshBasicMaterial({ color: new THREE.Color(PALE).multiplyScalar(1.6), transparent: true, opacity: 0.9, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }) };
    this.panes = new Map(); this.walls = new Map(); this.wedges = new Map(); this.links = new Map(); this.heads = new Map(); this.ghostTick = new Map();
    // The Rampart cracking and shattering (like Nova's Aegis): cracks where hits land, shards of the pane
    this.crackTex = [11, 23, 37].map(crackStarTex);
    this.shards = [];
    for (let i = 0; i < 48; i++) {
      const g = new THREE.BufferGeometry(), v = [];
      for (let k = 0; k < 3; k++) { const a = (k / 3) * Math.PI * 2 + (Math.random() - 0.5) * 1.2, rr = 0.5 + Math.random() * 0.5; v.push(Math.cos(a) * rr, Math.sin(a) * rr, 0); }
      g.setAttribute('position', new THREE.Float32BufferAttribute(v, 3)); g.setAttribute('uv', new THREE.Float32BufferAttribute([0, 0, 1, 0, 0.5, 1], 2));
      // (ordinary blending, pale blue glass (some catching the light white), so the shards read on bright backgrounds too)
      const m = new THREE.Mesh(g, new THREE.MeshBasicMaterial({ color: new THREE.Color(Math.random() < 0.3 ? '#e4f2ff' : '#8fc4ff'), transparent: true, opacity: 0, depthWrite: false, side: THREE.DoubleSide, toneMapped: false }));
      m.visible = false; m.renderOrder = 6; this.scene.add(m);
      this.shards.push({ m, life: 0, max: 1, v: new THREE.Vector3(), spin: new THREE.Vector3(), size: 0.2 });
    }
    this.si2 = 0; this.qs = new THREE.Quaternion(); this.es = new THREE.Euler();
    // Craters: a small pool of decals (the oldest is reused), three looks picked at random
    this.craterTex = [1, 2, 3].map(k => ({ base: craterTex(k * 7919, false), glow: craterTex(k * 7919, true) }));
    this.craters = [];
    for (let i = 0; i < CRATER.pool; i++) {
      const g = new THREE.PlaneGeometry(1, 1);
      const base = new THREE.Mesh(g, new THREE.MeshBasicMaterial({ transparent: true, opacity: 0, depthWrite: false, side: THREE.DoubleSide, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 }));
      const glow = new THREE.Mesh(g, new THREE.MeshBasicMaterial({ color: BLUE, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide, toneMapped: false }));
      base.visible = glow.visible = false; base.renderOrder = 1; glow.renderOrder = 2; this.scene.add(base); this.scene.add(glow);
      this.craters.push({ base, glow, life: 0, age: 0 });
    }
    // Sparks: thin streaks stretched along their flight (one instanced mesh, ordinary blending so they read on
    // bright floors as well as dark ones); they fall, bounce off the floor and cool from white-hot to red
    this.spk = Array.from({ length: 160 }, () => ({ life: 0, max: 1, x: 0, y: 0, vx: 0, vy: 0, d: 0, c: new THREE.Color() }));
    this.si = 0;
    this.spkMesh = new THREE.InstancedMesh(new THREE.PlaneGeometry(1, 1), new THREE.MeshBasicMaterial({ transparent: true, depthWrite: false, side: THREE.DoubleSide, toneMapped: false }), this.spk.length);
    this.spkMesh.frustumCulled = false; this.spkMesh.renderOrder = 6;
    for (let i = 0; i < this.spk.length; i++) { this.spkMesh.setMatrixAt(i, new THREE.Matrix4().makeScale(0, 0, 0)); this.spkMesh.setColorAt(i, new THREE.Color(WHITE)); }
    this.scene.add(this.spkMesh);
    this.q = new THREE.Quaternion(); this.sc = new THREE.Vector3(); this.cool = new THREE.Color('#8a1c00'); this.col = new THREE.Color();
    this.v = new THREE.Vector3(); this.v2 = new THREE.Vector3(); this.m4 = new THREE.Matrix4();
  }

  // A camera-facing quad along a sim-plane direction: `len` m along (tx, ty), `wid` m across it, centred at (x, y)
  place(mesh, x, y, tx, ty, depth = 0.3) {
    const T = planeDir(x, tx, ty, this.v).normalize(), f = pathFrame(x), Z = this.v2.set(f.nx, 0, f.nz).normalize();
    const X = new THREE.Vector3().crossVectors(T, Z).normalize();
    this.m4.makeBasis(X, T, Z); mesh.quaternion.setFromRotationMatrix(this.m4);
    toWorld(x, y, depth, mesh.position);
  }
  paneOf(p) {
    let P = this.panes.get(p); if (P) return P;
    const g = new THREE.PlaneGeometry(1, 1);
    P = { face: new THREE.Mesh(g, this.mat.pane), crack: new THREE.Mesh(g, this.mat.crack), rim: new THREE.Mesh(g, this.mat.edge), flash: 0, k: 0, cracks: [], frac: 1 };
    for (const m of [P.face, P.crack, P.rim]) { m.visible = false; m.renderOrder = 5; this.scene.add(m); }
    this.panes.set(p, P); return P;
  }

  onEvent(ev) {
    const F = this.fx, p = ev.p;
    switch (ev.type) {
      case 'guardOn': { const c = chest(p); F.sprite(c.x + p.guardDir[0] * 0.8, c.y + p.guardDir[1] * 0.8, 'ring', BLUE, 1.0, 0.18, 2.2); break; }
      case 'guardBlock': {
        const P = this.paneOf(p); P.flash = Math.min(1, P.flash + 0.5 + ev.dmg * 0.03);
        this.crackAt(p, P, ev.x, ev.y, ev.dmg, ev.frac);
        // Clusters chip off as its strength drops past two thirds and one third
        if ((P.frac > 2 / 3 && ev.frac <= 2 / 3) || (P.frac > 1 / 3 && ev.frac <= 1 / 3)) this.shatter(p, ev.x, ev.y, 7, 0.5);
        P.frac = ev.frac;
        // A blow that shoves him back drags the shield along the floor
        if (p.onGround) this.sparks(p.x + p.facing * (p.w / 2 + 0.3), p.y + 0.06, -p.facing, 3 + Math.min(8, ev.dmg / 2 | 0), 0.9);
        F.burst(ev.x, ev.y, ev.heavy ? WHITE : PALE, ev.heavy ? 18 : 10, ev.heavy ? 9 : 6, 0.3, 0.28, { dir: Math.atan2(p.guardDir[1], p.guardDir[0]), spread: 1.6, grav: 6 });
        F.sprite(ev.x, ev.y, 'star', WHITE, ev.heavy ? 1.3 : 0.8, 0.12, 1.4);
        break;
      }
      case 'perfectGuard': {
        const P = this.paneOf(p); P.flash = 1.4;
        F.sprite(ev.x, ev.y, 'star', WHITE, 2.0, 0.18, 1.5); F.sprite(ev.x, ev.y, 'ring', BLUE, 1.2, 0.3, 3.2);
        F.burst(ev.x, ev.y, BLUE, 24, 10, 0.32, 0.35); F.popText(ev.x, ev.y + 0.6, 'PERFECT', PALE, 0.6);
        break;
      }
      case 'rampartBreak': {
        // The pane shatters into shards of hard light: every remaining piece of it bursts outward
        const c = chest(p), [nx, ny] = p.guardDir || [p.facing, 0], P = this.paneOf(p);
        this.shatter(p, null, null, 30, 1); P.frac = 0;
        for (const k of P.cracks) k.at = -1;
        for (let i = 0; i < 28; i++) {
          const u = (Math.random() - 0.5) * 2.6, x = c.x + nx * 0.8 - ny * u, y = c.y + ny * 0.8 + nx * u;
          F.burst(x, y, Math.random() < 0.5 ? PALE : BLUE, 1, 7, 0.3, 0.5, { dir: Math.atan2(ny, nx) + (Math.random() - 0.5) * 2, spread: 0.5, grav: 12 });
        }
        F.sprite(c.x + nx * 0.8, c.y + ny * 0.8, 'ring', WHITE, 1.6, 0.35, 3); F.popText(c.x, c.y + 1.2, 'BROKEN', '#ff8aa8', 0.8);
        break;
      }
      case 'rampartReady': { this.paneOf(p).frac = 1; const c = chest(p); F.sprite(c.x, c.y, 'ring', BLUE, 1.2, 0.3, 2.4); F.burst(c.x, c.y, PALE, 12, 4, 0.25, 0.3); break; }
      case 'kineticRelease': {
        // The cone of force: a shock ring along the guard, sparks fanning out through the cone, a flash
        const k = ev.k, a0 = Math.atan2(ev.ny, ev.nx), at = toWorld(ev.x, ev.y, 0.3, new THREE.Vector3());
        F.charge.shockRing(at, planeDir(ev.x, ev.nx, ev.ny, new THREE.Vector3()).normalize(), WHITE, 0.4, 1.4 + 2.2 * k, 0.3, ev.r * 0.6);
        F.charge.shockRing(at.clone(), planeDir(ev.x, ev.nx, ev.ny, new THREE.Vector3()).normalize(), BLUE, 0.3, 1.0 + 1.8 * k, 0.38, ev.r * 0.8);
        F.sprite(ev.x, ev.y, 'star', WHITE, 1.6 + 2 * k, 0.2, 1.5); F.sprite(ev.x, ev.y, 'glow', BLUE, 2 + 3 * k, 0.3, 1.6);
        for (let i = 0; i < 30 + 50 * k; i++) {
          const a = a0 + (Math.random() - 0.5) * ev.cone * 2, d = Math.random() * ev.r;
          F.burst(ev.x + Math.cos(a) * d * 0.3, ev.y + Math.sin(a) * d * 0.3, Math.random() < 0.4 ? WHITE : BLUE, 1, 10 + 12 * k, 0.32, 0.32, { dir: a, spread: 0.15 });
        }
        F.dust(ev.x, ev.y - 1.2, 0.4 + 0.6 * k, [ev.nx >= 0 ? 0 : Math.PI], { reach: 2 });
        break;
      }
      case 'rush': {
        const L = ev.level, x = p.x - ev.dx * 0.6;
        F.dust(p.x, p.y, 0.4 + 0.2 * L, [ev.dx > 0 ? Math.PI : 0], { noRing: L < 2 });
        F.smoke(x, p.y + 0.3, '#8e97a3', 4 + 3 * L, 3 + L, 0.5, 0.5, { dir: ev.dx > 0 ? Math.PI : 0, spread: 0.8, op: 0.45 });
        if (L) { F.sprite(p.x, p.y + 1.2, 'ring', BLUE, 0.8 + 0.3 * L, 0.2, 2.4); F.burst(p.x, p.y + 1.2, BLUE, 12 + 8 * L, 8 + 3 * L, 0.3, 0.3, { dir: ev.dx > 0 ? Math.PI : 0, spread: 1.1 }); }
        // The shield's edge bites the floor: a spray of sparks
        if (p.onGround) this.sparks(p.x + ev.dx * (p.w / 2 + 0.2), p.y + 0.08, ev.dx, 10 + 6 * L, 1.2);
        // The exhaust stacks roar
        for (let i = 0; i < 6; i++) F.smoke(p.x - p.facing * 0.55, p.y + 2.25, '#6f7883', 1, 2, 0.4, 0.6, { dir: Math.PI / 2 + (ev.dx > 0 ? 0.6 : -0.6), spread: 0.5, op: 0.45, grav: -1.2 });
        break;
      }
      case 'plowCatch': { const e = ev.e; F.sprite(e.x, e.y + e.h * 0.55, 'star', WHITE, 1.0, 0.12, 1.4); F.burst(e.x, e.y + e.h * 0.55, PALE, 12, 7, 0.28, 0.25); break; }
      case 'ramSplat': {
        const k = Math.min(1, 0.4 + 0.2 * ev.n + 0.1 * ev.level);
        F.sprite(ev.x, ev.y, 'star', WHITE, 2.4 + k, 0.2, 1.5); F.sprite(ev.x, ev.y, 'ring', BLUE, 1.6 + k, 0.35, 3.2); F.fireball(ev.x, ev.y, '#9fd0ff', 1.2 + k, 0.25);
        F.burst(ev.x, ev.y, '#5d6674', 20, 9, 0.32, 0.6, { dir: Math.PI / 2, spread: 2.4, grav: 18 }); F.burst(ev.x, ev.y, BLUE, 26, 11, 0.32, 0.35);
        F.dust(ev.x, p.y, 0.9, [0, Math.PI], { reach: 1.5 }); F.smoke(ev.x, ev.y, '#8e97a3', 8, 1.8, 0.7, 0.9, { op: 0.45, grow: 2.4, grav: -0.6 });
        F.popText(ev.x, ev.y + 1.1, 'SLAM', PALE, 0.65);
        this.wallCrater(ev.x, ev.y, Math.sign(ev.x - p.x) || p.facing, 0.75 + 0.12 * Math.min(4, ev.n) + 0.1 * ev.level);
        break;
      }
      case 'ramBonk': { F.sprite(ev.x, ev.y, 'star', WHITE, 1.6, 0.14, 1.5); F.sprite(ev.x, ev.y, 'ring', PALE, 1.0, 0.25, 2.6); F.burst(ev.x, ev.y, PALE, 18, 8, 0.28, 0.3, { grav: 8 }); break; }
      case 'wallUp': {
        const x = ev.x, y = ev.y;
        F.groundRing(x, y, BLUE, 0.3, 2.2, 0.35, 0.9); F.dust(x, y, 0.5, [0, Math.PI], { noRing: true });
        for (let i = 0; i < 20; i++) F.burst(x, y + Math.random() * RAM.wall.half * 2, Math.random() < 0.5 ? PALE : BLUE, 1, 4, 0.26, 0.4, { dir: Math.PI / 2, spread: 0.6 });
        F.sprite(x, y + 0.4, 'star', WHITE, 1.4, 0.16, 1.4);
        break;
      }
      case 'wallHit': { F.burst(ev.x, ev.y, PALE, 8, 5, 0.24, 0.22); const W = this.walls.get(ev.b); if (W) W.flash = 1; break; }
      case 'wallDown': {
        if (ev.broken) for (let i = 0; i < 26; i++) F.burst(ev.x, ev.y + Math.random() * RAM.wall.half * 2, Math.random() < 0.5 ? PALE : BLUE, 1, 7, 0.3, 0.5, { grav: 10 });
        else F.burst(ev.x, ev.y + 1.5, BLUE, 14, 2, 0.3, 0.6, { dir: Math.PI / 2, spread: 0.6 });
        break;
      }
      case 'link': {
        const q = ev.q, c = chest(q);
        F.sprite(c.x, c.y, 'ring', BLUE, 1.3, 0.35, 2.6); F.burst(c.x, c.y, PALE, 18, 4, 0.26, 0.4);
        break;
      }
      case 'linkHit': {
        const L = this.links.get(p); if (L) L.flash = 1;
        const c = chest(p); F.burst(c.x, c.y, PALE, 8, 4, 0.22, 0.25);
        break;
      }
      case 'linkEnd': { const L = this.links.get(p); if (L) L.fade = 0.25; break; }
      case 'leap': F.dust(p.x, p.y, 0.6, [0, Math.PI]); F.burst(p.x, p.y + 0.3, BLUE, 16, 6, 0.3, 0.3, { dir: -Math.PI / 2, spread: 1.6 }); break;
      case 'leapLand': {
        F.groundRing(ev.x, ev.y, WHITE, 0.4, 3.2, 0.32, 0.8); F.groundRing(ev.x, ev.y, BLUE, 0.3, 2.4, 0.3, 0.9);
        F.dust(ev.x, ev.y, 0.9, [0, Math.PI]); F.burst(ev.x, ev.y + 0.3, '#5d6674', 14, 8, 0.3, 0.55, { dir: Math.PI / 2, spread: 2.2, grav: 18 });
        F.sprite(ev.x, ev.y + 0.5, 'star', WHITE, 1.6, 0.16, 1.5);
        this.crater(ev.x, ev.y, 1.0);
        break;
      }
      case 'provoke': {
        for (const [sz, life, col] of [[1.4, 0.35, WHITE], [2.4, 0.5, BLUE], [3.4, 0.65, PALE]]) F.sprite(ev.x, ev.y, 'ring', col, sz, life, 3.6);
        F.burst(ev.x, ev.y, BLUE, 34, 9, 0.34, 0.45); F.dust(p.x, p.y, 0.6, [0, Math.PI]);
        F.popText(ev.x, ev.y + 1.6, 'PROVOKE', PALE, 0.7);
        break;
      }
      case 'quake': F.groundRing(ev.x, ev.y, BLUE, 0.4, 2.6, 0.32, 0.9); F.dust(p.x, p.y, 1.0, [0, Math.PI], { reach: 1.2 }); F.sprite(ev.x, ev.y + 0.3, 'star', WHITE, 1.8, 0.16, 1.5); this.crater(ev.x, ev.y, 1.15); break;
      case 'upliftBlast': {
        F.sprite(ev.x, ev.y, 'star', WHITE, 1.8, 0.16, 1.5); F.sprite(ev.x, ev.y, 'ring', BLUE, ev.r * 0.9, 0.3, 2.8);
        F.burst(ev.x, ev.y, BLUE, 26, 9, 0.3, 0.32); F.burst(ev.x, ev.y, PALE, 10, 6, 0.24, 0.25, { dir: Math.PI / 2, spread: 1.4 });
        break;
      }
      case 'fortify': for (const q of ev.members) { const c = chest(q); F.sprite(c.x, c.y, 'ring', BLUE, 1.6, 0.4, 2.2); F.burst(c.x, c.y, PALE, 16, 4, 0.26, 0.4); } break;
      case 'ramSlam': {
        const x = ev.x, y = ev.y, r = ev.r;
        F.sprite(x, y, 'star', WHITE, r * 1.4, 0.3, 1.6); F.sprite(x, y, 'glow', PALE, r * 1.6, 0.4, 1.5); F.fireball(x, y, '#9fd0ff', r * 0.8, 0.45);
        for (const [sz, life, g] of [[r * 0.45, 0.4, 4.4], [r * 0.3, 0.6, 6], [r * 0.18, 0.75, 8]]) F.sprite(x, y, 'ring', WHITE, sz, life, g);
        F.burst(x, y, BLUE, 80, 18, 0.42, 0.75, { grav: 5 }); F.burst(x, y, '#5d6674', 30, 12, 0.34, 0.8, { dir: Math.PI / 2, spread: 2.4, grav: 18 });
        const fl = F.floorUnder(x, y, 3);
        if (fl !== null) { F.groundRing(x, fl, WHITE, 0.5, r * 1.3, 0.55, 0.95); F.groundRing(x, fl, BLUE, 0.4, r, 0.5, 0.9); F.dust(x, fl, 1.4, [0, Math.PI], { reach: 3 }); }
        F.smoke(x, y, '#8e97a3', 14, 2.5, 1.2, 1.3, { op: 0.45, grow: 2.6, grav: -0.8 });
        this.crater(x, y, Math.min(3, r * 0.5), 3);
        break;
      }
    }
  }

  update(dt, world, view) {
    this.t += dt;
    this.updateCraters(dt); this.updateSparks(dt); this.updateShards(dt);
    const F = this.fx, cam = view.camera.position, seenP = new Set(), seenW = new Set(), seenL = new Set();
    for (const p of world.players) {
      if (p.char !== 'ram') continue;
      const rig = view.rigs.get(p), vis = !!rig && rig.root.visible && p.state !== 'dead';
      // ---- The Rampart's pane ----
      const P = this.paneOf(p); seenP.add(p);
      const up = vis && p.state === 'guard';
      P.k += ((up ? 1 : 0) - P.k) * (1 - Math.exp(-dt * (up ? 30 : 14)));
      P.flash = Math.max(0, P.flash - dt * 5);
      if (P.k > 0.02) {
        const G = RAM.guard, c = chest(p), [nx, ny] = p.guardDir || [p.facing, 0], cx = c.x + nx * G.reach, cy = c.y + ny * G.reach;
        const frac = Math.max(0, p.integrity / G.integrity), weak = frac < 0.35 ? (Math.sin(this.t * 40) > 0 ? 1 : 0.55) : 1;
        const len = G.half * 2 * (0.6 + 0.4 * P.k);
        // Just in front of the shield he holds (the pose puts it on the guard plane), so the hard light wraps it
        const dz = rig ? rig.extra.shield.position.z * CHARS.ram.scale + 0.16 : 0.3;
        // (a broad slab of light a little wider than the shield itself, its bright rim on the outer edge)
        this.place(P.face, cx, cy, -ny, nx, dz); P.face.scale.set(1.1, len, 1);
        this.place(P.rim, cx + nx * 0.55, cy + ny * 0.55, -ny, nx, dz + 0.02); P.rim.scale.set(0.05, len, 1);
        this.place(P.crack, cx, cy, -ny, nx, dz + 0.01); P.crack.scale.set(1.1, len, 1);
        this.mat.pane.opacity = (0.32 + 0.3 * frac + 0.6 * P.flash) * P.k * weak;
        this.mat.crack.opacity = Math.max(0, 0.75 - frac) * P.k * weak;
        this.mat.edge.opacity = (0.55 + 0.45 * P.flash) * P.k;
        this.mat.pane.map.offset.y = this.t * 0.25;
        P.face.visible = P.rim.visible = P.crack.visible = true;
        // The cracks where hits landed, riding on the pane (they heal as its Integrity comes back)
        for (const k of P.cracks) {
          const on = p.integrity < k.at;
          k.o += ((on ? 1 : 0) - k.o) * (1 - Math.exp(-dt * (on ? 30 : 3)));
          k.m.visible = k.o > 0.02;
          if (!k.m.visible) continue;
          const u = Math.max(-len / 2, Math.min(len / 2, k.u));
          this.place(k.m, cx - ny * u + nx * k.w, cy + nx * u + ny * k.w, -ny, nx, dz + 0.025);
          k.m.rotateZ(k.rot); k.m.scale.setScalar(k.size * (1 + 0.6 * (1 - frac)));
          k.m.material.opacity = k.o * P.k * weak * (0.6 + 0.4 * (1 - frac)) * (1 + P.flash);
        }
        // Pushed along the floor behind the shield, its lower edge grinds out sparks
        if (p.onGround && Math.abs(p.vx) > 0.4 && Math.random() < Math.min(1, Math.abs(p.vx) / 2.2)) {
          const bot = ny * nx >= 0 ? -1 : 1, ex = cx - ny * bot * len / 2;
          this.sparks(Math.abs(ny) > 0.5 ? ex : p.x + p.facing * (p.w / 2 + 0.3), p.y + 0.05, Math.sign(p.vx), 1 + (Math.abs(p.vx) > 1.5 ? 1 : 0), 0.75);
        }
        // Stored Kinetic: motes drift up the pane
        if (p.kinetic > 10 && Math.random() < p.kinetic / 120) {
          const u = (Math.random() - 0.5) * len, w = toWorld(cx - ny * u, cy + nx * u, dz + 0.04, this.v);
          const pt = F.particle(w, Math.random() < 0.5 ? WHITE : BLUE, 0.16, 0.4); pt.v.set(0, 1.2, 0); pt.drag = 1;
        }
      } else { P.face.visible = P.rim.visible = P.crack.visible = false; for (const k of P.cracks) k.m.visible = false; }
      // ---- The Ram Charge: a hard-light wedge in front, streaks and dust; afterimages ----
      const rushing = vis && p.state === 'rush' && p.rush, ult = vis && p.state === 'ult' && p.ultRun && p.ultRun.kind === 'ram';
      let W = this.wedges.get(p);
      if (!W) {
        W = new THREE.Mesh(new THREE.ConeGeometry(0.9, 1.4, 4, 1, true), this.mat.wedge); W.visible = false; W.renderOrder = 5; this.scene.add(W); this.wedges.set(p, W);
      }
      if (rushing || (ult && p.ultRun.t > ULT.ram.brace && !p.ultRun.slamT)) {
        const L = rushing ? p.rush.level : 3, dir = rushing ? p.rush.dx : p.ultRun.dx, s = (0.8 + 0.18 * L) * (ult ? 1.6 : 1);
        this.place(W, p.x + dir * (p.w / 2 + 0.55 * s), p.y + 1.2, dir, 0, 0.3);   // the cone's point leads
        W.scale.set(s * (1 + 0.08 * Math.sin(this.t * 40)), s, s * 0.5); W.visible = true;
        this.mat.wedge.opacity = 0.32 + 0.08 * L;
        for (let i = 0; i < 2 + L; i++) F.burst(p.x + dir * (p.w / 2 + 0.4), p.y + 0.3 + Math.random() * p.h, Math.random() < 0.5 ? WHITE : BLUE, 1, 14 + 4 * L, 0.2, 0.14, { dir: dir > 0 ? Math.PI : 0, spread: 0.12 });
        if (p.onGround && Math.random() < 0.7) F.dust(p.x - dir * 0.3, p.y, 0.3 + 0.08 * L, [dir > 0 ? Math.PI : 0], { noRing: true, op: 0.45 });
        // Sparks: the shield's lower edge and his boots grind along the floor; in the air the shield's rim crackles
        if (p.onGround) { this.sparks(p.x + dir * (p.w / 2 + 0.25), p.y + 0.06, dir, 3 + L, 1); if (Math.random() < 0.6) this.sparks(p.x - dir * 0.15, p.y + 0.04, dir, 1, 0.7); }
        else if (Math.random() < 0.6) F.burst(p.x + dir * (p.w / 2 + 0.3), p.y + 0.4 + Math.random() * 1.6, Math.random() < 0.5 ? WHITE : PALE, 2, 6, 0.14, 0.18, { dir: dir > 0 ? Math.PI : 0, spread: 1.6, grav: 6 });
        const last = this.ghostTick.get(p) ?? -99;
        if (rig && world.tick - last >= (L >= 2 ? 3 : 4)) { this.ghostTick.set(p, world.tick); F.ghosts.spawn(rig, new THREE.Color(BLUE).multiplyScalar(1.2 + 0.25 * L), 0.22 + 0.06 * L, 0.18); }
      } else W.visible = false;
      // The Hydraulic Uplift's leg jets
      if (vis && p.state === 'attack' && p.moveId === 'ram_rise' && p.st >= p.move.su && p.st < p.move.su + p.move.ac) {
        for (const dz of [-0.2, 0.2]) {
          const w = toWorld(p.x + (Math.random() - 0.5) * 0.3, p.y - 0.05, dz, this.v), pt = F.particle(w, Math.random() < 0.5 ? WHITE : BLUE, 0.3, 0.16);
          pt.v.set((Math.random() - 0.5) * 0.8, -6 - Math.random() * 3, 0); pt.drag = 0.86;
        }
        if (Math.random() < 0.5) F.smoke(p.x, p.y - 0.1, '#8e97a3', 1, 1, 0.5, 0.6, { dir: -Math.PI / 2, spread: 1.2, op: 0.4 });
      }
      // Braced by Provoke: a slow blue pulse at his feet
      if (vis && p.braceT > 0 && Math.floor(this.t * 3) !== Math.floor((this.t - dt) * 3)) F.groundRing(p.x, p.y, BLUE, 0.6, 1.6, 0.3, 0.6);
      // A guard leap leaves afterimages
      if (vis && p.state === 'leap' && rig) {
        const last = this.ghostTick.get(p) ?? -99;
        if (world.tick - last >= 3) { this.ghostTick.set(p, world.tick); F.ghosts.spawn(rig, new THREE.Color(BLUE).multiplyScalar(1.3), 0.25, 0.2); }
      }
      // ---- Guardian Link: a tether of light to the teammate he guards ----
      if (p.link && vis) {
        seenL.add(p);
        let L = this.links.get(p);
        if (!L) { L = { glow: new Strip(this.scene, 4), core: new Strip(this.scene, 5), pts: [], flash: 0, fade: 0 }; this.links.set(p, L); }
        L.fade = 0; L.flash = Math.max(0, L.flash - dt * 4);
        const a = chest(p), q = p.link.q, b = chest(q), n = 16, pts = L.pts; pts.length = 0;
        for (let i = 0; i <= n; i++) {
          const u = i / n, sag = Math.sin(u * Math.PI) * 0.6;
          pts.push(toWorld(a.x + (b.x - a.x) * u, a.y + (b.y - a.y) * u - sag + Math.sin(this.t * 7 + u * 9) * 0.05, 0.25, new THREE.Vector3()));
        }
        const k = Math.min(1, p.link.t / 40);
        L.glow.build(pts, cam, () => 0.12 + 0.08 * L.flash, i => [0.35, 0.65, 1.0, (0.22 + 0.4 * L.flash) * k]);
        L.core.build(pts, cam, () => 0.035, i => { const on = ((i + Math.floor(this.t * 14)) % 3) !== 0; return [0.8, 0.92, 1.0, (on ? 0.9 : 0.25) * k]; });
        if (Math.random() < 0.15) F.sprite(b.x, b.y + 0.9, 'ring', BLUE, 0.55, 0.3, 1.6);
      }
    }
    for (const [p, L] of this.links) {
      if (seenL.has(p)) continue;
      if (L.fade > 0) { L.fade -= dt; continue; }
      L.glow.mesh.visible = false; L.core.mesh.visible = false;
      if (!world.players.includes(p)) this.links.delete(p);
    }
    for (const [p, P] of this.panes) if (!seenP.has(p)) { P.face.visible = P.rim.visible = P.crack.visible = false; if (!world.players.includes(p)) this.panes.delete(p); }
    for (const [p, W] of this.wedges) if (!world.players.includes(p) || p.char !== 'ram') { W.visible = false; this.scene.remove(W); W.geometry.dispose(); this.wedges.delete(p); }
    // ---- Bulwark Walls: a slab of hard light with bright edges over a glowing base line. Each wall has its own
    // materials (clones share the shaders), so two walls never fight over one opacity. ----
    for (const b of world.barriers) {
      if (b.kind !== 'rampart') continue;
      seenW.add(b);
      let W = this.walls.get(b);
      if (!W) {
        const g = new THREE.PlaneGeometry(1, 1), face = this.mat.wall.clone(), crack = this.mat.wallCrack.clone(), edge = this.mat.edge.clone();
        W = { face: new THREE.Mesh(g, face), crack: new THREE.Mesh(g, crack), rimA: new THREE.Mesh(g, edge), rimB: new THREE.Mesh(g, edge), top: new THREE.Mesh(g, edge),
          base: new THREE.Mesh(g, edge), mats: [face, crack, edge], geo: g, flash: 0, born: this.t };
        W.meshes = [W.face, W.crack, W.rimA, W.rimB, W.top, W.base];
        for (const m of W.meshes) { m.renderOrder = 5; this.scene.add(m); }
        this.walls.set(b, W);
      }
      W.flash = Math.max(0, W.flash - dt * 5);
      const grow = Math.min(1, (this.t - W.born) / 0.18), H = b.half * 2 * grow, cy = b.y - b.half + H / 2, frac = Math.max(0, b.hp / b.maxHp), endK = Math.min(1, b.ttl / 30);
      const D = 0.36, blink = b.ttl < 60 && b.ttl % 10 < 5 ? 0.6 : 1, [face, crack, edge] = W.mats;
      this.place(W.face, b.x, cy, 0, 1, 0.1); W.face.scale.set(D * 2, H, 1);
      this.place(W.crack, b.x, cy, 0, 1, 0.11); W.crack.scale.set(D * 2, H, 1);
      this.place(W.rimA, b.x - D, cy, 0, 1, 0.12); W.rimA.scale.set(0.05, H, 1);
      this.place(W.rimB, b.x + D, cy, 0, 1, 0.12); W.rimB.scale.set(0.05, H, 1);
      this.place(W.top, b.x, b.y - b.half + H, 1, 0, 0.12); W.top.scale.set(0.05, D * 2 + 0.05, 1);
      this.place(W.base, b.x, b.y - b.half + 0.04, 1, 0, 0.12); W.base.scale.set(0.1, D * 2 + 0.6, 1);
      face.opacity = (0.45 + 0.35 * frac + 0.5 * W.flash) * endK * blink;
      crack.opacity = Math.max(0, 0.75 - frac) * endK;
      edge.opacity = (0.75 + 0.25 * W.flash) * endK * blink;
      this.mat.wall.map.offset.y = this.t * 0.2;
      if (Math.random() < 0.3) F.burst(b.x + (Math.random() - 0.5) * D * 2, b.y - b.half + Math.random() * H, PALE, 1, 0.8, 0.16, 0.4, { dir: Math.PI / 2, spread: 0.3 });
    }
    for (const [b, W] of this.walls) if (!seenW.has(b)) { for (const m of W.meshes) this.scene.remove(m); for (const m of W.mats) m.dispose(); W.geo.dispose(); this.walls.delete(b); }
    // ---- Siege Breaker's ram's head: horns of hard light over the wedge ----
    for (const p of world.players) {
      const R = p.state === 'ult' && p.ultRun && p.ultRun.kind === 'ram' ? p.ultRun : null;
      let H = this.heads.get(p);
      if (!R) { if (H) H.g.visible = false; continue; }
      if (!H) {
        const g = new THREE.Group();
        for (const sx of [1, -1]) { const h = new THREE.Mesh(new THREE.TorusGeometry(0.75, 0.16, 8, 24, Math.PI * 1.5), this.mat.wedge); h.position.set(-0.2, 0.55, sx * 0.25); h.rotation.z = 1.0; g.add(h); }
        const brow = new THREE.Mesh(new THREE.BoxGeometry(0.5, 1.6, 0.9), this.mat.wedge); brow.position.set(0.3, 0, 0); g.add(brow);
        const eyes = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.18, 0.95), this.mat.edge); eyes.position.set(0.55, 0.25, 0); g.add(eyes);
        g.renderOrder = 6; this.scene.add(g); H = { g }; this.heads.set(p, H);
      }
      const U = ULT.ram, form = R.slamT ? Math.max(0, 1 - (R.t - R.slamT) / 10) : Math.min(1, R.t / U.brace);
      if (form <= 0.01) { H.g.visible = false; continue; }
      toWorld(p.x + R.dx * 1.6, p.y + 1.5, 0.3, H.g.position);
      H.g.rotation.y = (rig => (rig ? rig.root.rotation.y : 0))(view.rigs.get(p)) + (R.dx > 0 ? 0 : Math.PI);
      H.g.scale.setScalar(1.6 * form * (1 + 0.05 * Math.sin(this.t * 30))); H.g.visible = true;
    }
  }

  // A hit on the Rampart cracks it where it landed: a new crack, or the one already there spreads
  crackAt(p, P, x, y, dmg, frac) {
    const G = RAM.guard, c = chest(p), [nx, ny] = p.guardDir || [p.facing, 0], cx = c.x + nx * G.reach, cy = c.y + ny * G.reach;
    const u = Math.max(-G.half * 0.9, Math.min(G.half * 0.9, (x - cx) * -ny + (y - cy) * nx));
    const at = frac * G.integrity + 12;   // it shows until the Integrity grows back past this
    let k = P.cracks.find(q => q.at > p.integrity && Math.abs(q.u - u) < 0.35);
    if (k) { k.size = Math.min(1.3, k.size + 0.08 + dmg * 0.01); k.at = Math.max(k.at, at); return; }
    k = P.cracks.find(q => q.o < 0.02 && !(q.at > p.integrity));
    if (!k && P.cracks.length < 8) {
      const m = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), new THREE.MeshBasicMaterial({ color: new THREE.Color(PALE).multiplyScalar(1.5), transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide, toneMapped: false }));
      m.visible = false; m.renderOrder = 6; this.scene.add(m); k = { m, o: 0 }; P.cracks.push(k);
    }
    if (!k) k = P.cracks.reduce((a, b) => (a.at < b.at ? a : b));
    k.m.material.map = this.crackTex[(Math.random() * this.crackTex.length) | 0]; k.m.material.needsUpdate = true;
    Object.assign(k, { u, w: (Math.random() - 0.5) * 0.35, size: 0.4 + Math.min(0.5, dmg * 0.025), rot: Math.random() * Math.PI * 2, at });
  }
  // Shards of the pane fly off: `n` of them, from (x, y) on it (a chip), or from all over it (null: it shatters)
  shatter(p, x, y, n, power) {
    const G = RAM.guard, c = chest(p), [nx, ny] = p.guardDir || [p.facing, 0], cx = c.x + nx * G.reach, cy = c.y + ny * G.reach;
    const P = this.paneOf(p), q = P.face.quaternion;
    for (let i = 0; i < n; i++) {
      const S = this.shards[this.si2]; this.si2 = (this.si2 + 1) % this.shards.length;
      const u = x === null ? (Math.random() - 0.5) * G.half * 2 : (x - cx) * -ny + (y - cy) * nx + (Math.random() - 0.5) * 0.5;
      const w = (Math.random() - 0.5) * 0.5, sx = cx - ny * u + nx * w, sy = cy + nx * u + ny * w;
      toWorld(sx, sy, 0.35, S.m.position); S.m.quaternion.copy(q);
      const sp = (3 + Math.random() * 6) * power, a = Math.atan2(ny, nx) + (Math.random() - 0.5) * 1.6;
      planeDir(sx, Math.cos(a) * sp, Math.sin(a) * sp + 2 + Math.random() * 3, S.v); S.v.z += (Math.random() - 0.2) * 3;
      S.spin.set((Math.random() - 0.5) * 18, (Math.random() - 0.5) * 18, (Math.random() - 0.5) * 18);
      S.size = (0.16 + Math.random() * 0.26) * (x === null ? 1.25 : 0.9); S.m.scale.setScalar(S.size);
      S.life = S.max = 0.55 + Math.random() * 0.5; S.m.visible = true;
    }
    if (x === null) { this.fx.sprite(cx, cy, 'glow', BLUE, 2.6, 0.25, 1.6); this.fx.sprite(cx, cy, 'star', WHITE, 2.2, 0.16, 1.5); }
    else this.fx.sprite(x, y, 'star', WHITE, 1.0, 0.12, 1.4);
  }
  updateShards(dt) {
    for (const S of this.shards) {
      if (S.life <= 0) { S.m.visible = false; continue; }
      S.life -= dt; S.v.y -= 14 * dt; S.v.multiplyScalar(Math.pow(0.985, dt * 60));
      S.m.position.addScaledVector(S.v, dt);
      this.qs.setFromEuler(this.es.set(S.spin.x * dt, S.spin.y * dt, S.spin.z * dt)); S.m.quaternion.multiply(this.qs);
      const k = Math.max(0, S.life / S.max);
      S.m.material.opacity = 0.95 * Math.min(1, k * 1.8); S.m.scale.setScalar(S.size * (0.6 + 0.4 * k));
    }
  }
  // Sparks thrown back from a point scraping along the floor, `dir` the way he is moving (streaks, see updateSparks),
  // with a few glowing particles among them that show on dark backgrounds
  sparks(x, y, dir, n, k = 1) {
    const a0 = dir > 0 ? Math.PI - 0.35 : 0.35;
    for (let i = 0; i < n; i++) {
      const S = this.spk[this.si]; this.si = (this.si + 1) % this.spk.length;
      const a = a0 + (Math.random() - 0.5) * 0.9, sp = (6 + Math.random() * 9) * k;
      Object.assign(S, { x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, d: 0.1 + Math.random() * 0.55 });
      S.life = S.max = 0.22 + Math.random() * 0.25; S.c.set(SPARKS[(Math.random() * SPARKS.length) | 0]);
      if (i % 2 === 0) this.fx.burst(x, y, SPARKS[0], 1, sp * 0.8, 0.2, 0.25, { dir: a, spread: 0.3, grav: 18, drag: 0.95 });   // a glow on dark backgrounds
    }
    // where the steel meets the floor it glows white-hot
    if (n >= 2) this.fx.sprite(x, y + 0.05, 'glow', '#ffb24a', 0.55 + 0.05 * n, 0.08, 1.3);
  }
  updateSparks(dt) {
    const M = this.spkMesh;
    for (let i = 0; i < this.spk.length; i++) {
      const S = this.spk[i];
      if (S.life <= 0) { if (S.max) { M.setMatrixAt(i, this.m4.makeScale(0, 0, 0)); S.max = 0; } continue; }
      S.life -= dt; S.vy -= 22 * dt; S.vx *= Math.pow(0.97, dt * 60);
      S.x += S.vx * dt; S.y += S.vy * dt;
      const g = groundBelow(S.x, S.y + 0.3);
      if (S.y < g && S.y > g - 0.3) { S.y = g; S.vy = Math.abs(S.vy) * 0.35; S.vx *= 0.7; }   // skips off the floor
      const k = Math.max(0, S.life / S.max), spd = Math.hypot(S.vx, S.vy);
      const T = planeDir(S.x, S.vx, S.vy, this.v).normalize(), f = pathFrame(S.x), Z = this.v2.set(f.nx, 0, f.nz).normalize();
      const X = (this.vX || (this.vX = new THREE.Vector3())).crossVectors(T, Z).normalize();   // (scratch vectors: no garbage per spark)
      this.q.setFromRotationMatrix(this.m4.makeBasis(X, T, Z));
      this.m4.compose(toWorld(S.x, S.y, S.d, this.vP || (this.vP = new THREE.Vector3())), this.q, this.sc.set(0.06 * (0.5 + 0.5 * k), 0.06 + spd * 0.028, 1));
      M.setMatrixAt(i, this.m4);
      M.setColorAt(i, this.col.copy(this.cool).lerp(S.c, Math.min(1, k * 1.6)));
    }
    M.instanceMatrix.needsUpdate = true; if (M.instanceColor) M.instanceColor.needsUpdate = true;
  }
  // A crater in the floor under (x, y), `r` m across its hollow, sized down to fit the ledge it is on; `heat`
  // makes its cracks glow brighter
  crater(x, y, r, heat = 1) {
    const fl = this.fx.floorUnder(x, y, 2.5); if (fl === null) return;
    const same = xx => Math.abs(groundBelow(xx, fl + 0.3) - fl) < 0.05;
    while (r > 0.4 && !(same(x - r) && same(x + r))) r -= 0.1;
    const C = this.nextCrater(r * 3.3);
    toWorld(x, fl + 0.02, 0, C.base.position); C.glow.position.copy(C.base.position);
    C.base.rotation.set(-Math.PI / 2, 0, Math.random() * Math.PI * 2); C.glow.rotation.copy(C.base.rotation);
    C.heat = heat;
  }
  // A crater in the wall a pile was slammed into: found by probing ahead of the impact for solid ground
  wallCrater(x, y, dir, r) {
    let wx = null;
    for (let d = 0; d <= 4; d += 0.05) if (pointInSolid(x + dir * d, y)) { wx = x + dir * (d - 0.02); break; }
    if (wx === null) return;
    const C = this.nextCrater(r * 3.3), f = pathFrame(wx);
    toWorld(wx, y, 0, C.base.position); C.glow.position.copy(C.base.position);
    C.base.lookAt(C.base.position.x - f.tx * dir, C.base.position.y, C.base.position.z - f.tz * dir);
    C.base.rotateZ(Math.random() * Math.PI * 2); C.glow.quaternion.copy(C.base.quaternion);
    C.heat = 1.4;
  }
  nextCrater(size) {
    const C = this.craters.find(q => q.life <= 0) || this.craters.reduce((a, b) => (a.age > b.age ? a : b));
    const T = this.craterTex[(Math.random() * this.craterTex.length) | 0];
    C.base.material.map = T.base; C.glow.material.map = T.glow; C.base.material.needsUpdate = C.glow.material.needsUpdate = true;
    C.base.scale.set(size, size, 1); C.glow.scale.set(size, size, 1);
    C.life = CRATER.life; C.age = 0; C.base.visible = C.glow.visible = true;
    return C;
  }
  updateCraters(dt) {
    for (const C of this.craters) {
      if (C.life <= 0) { C.base.visible = C.glow.visible = false; continue; }
      C.life -= dt; C.age += dt;
      C.base.material.opacity = Math.min(1, C.age * 12) * Math.min(1, Math.max(0, C.life) / CRATER.fade);
      C.glow.material.opacity = Math.max(0, 1 - C.age / CRATER.glow) * 0.9 * C.heat;
    }
  }

  warmShow(at) {
    const P = this.paneOf('warm'), out = [P.face, P.crack, P.rim];
    const C = this.craters[0]; C.base.material.map = this.craterTex[0].base; C.glow.material.map = this.craterTex[0].glow;
    out.push(C.base, C.glow, this.spkMesh, this.shards[0].m);
    for (const m of out) { m.position.copy(at); m.visible = true; }
    return out;
  }
  warmDone() {
    const P = this.panes.get('warm'); if (P) { P.face.visible = P.crack.visible = P.rim.visible = false; this.panes.delete('warm'); }
    for (const C of this.craters) { C.life = 0; C.base.visible = C.glow.visible = false; }
    this.spkMesh.position.set(0, 0, 0);   // (its instances are placed in world space)
    for (const S of this.shards) { S.life = 0; S.m.visible = false; }
  }
}
