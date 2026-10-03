// Nova's hard-light Aegis: a faceted dome of light panels around him. Hits send a ripple across it and crack
// the panels around the impact (the cracks spread from main fractures to branches to fine crazing as the
// damage grows); badly cracked panels splinter off as shards, clusters of them go as its strength drops past
// two thirds and one third, and when it breaks every remaining panel shatters outward. A detonation blasts
// the panels out on purpose; running out of time dissolves them. The energy each hit puts into the shield
// visibly streams into his bracer (Overcharge). The whole dome and all its shards are one mesh: the vertex
// shader flies detached panels on their own in world space. Presentation only: reads the sim, never changes it.
import * as THREE from 'three';
import { AEGIS, CHARS } from './config.js';
import { toWorld, toWorldZ, planeDir3, yawOf } from './space.js';
import { pathFrame } from './level.js';

const DETAIL = 3;
const GOLD = CHARS.nova.energy;

// Crack texture: brightness is the order a line appears in (main fractures first, then branches, then fine
// crazing), so one threshold per panel reveals more of the pattern as that panel takes more damage
function crackTexture(size = 256) {
  const c = document.createElement('canvas'); c.width = c.height = size;
  const g = c.getContext('2d'); g.fillStyle = '#000'; g.fillRect(0, 0, size, size);
  g.globalCompositeOperation = 'lighten'; g.lineCap = 'round'; g.lineJoin = 'round';
  let seed = 7; const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
  const walk = (x, y, a, n, len, width, v, keep) => {
    g.strokeStyle = `rgb(${v},${v},${v})`; g.lineWidth = width; g.beginPath(); g.moveTo(x, y);
    const pts = [];
    for (let i = 0; i < n; i++) { a += (rnd() - 0.5) * 1.1; x += Math.cos(a) * len * (0.6 + rnd() * 0.8); y += Math.sin(a) * len * (0.6 + rnd() * 0.8); g.lineTo(x, y); pts.push([x, y, a]); }
    g.stroke(); if (keep) keep.push(...pts);
  };
  const mains = [];
  // Fine crazing first, then branches, then the main fractures on top (lighten keeps the brightest)
  for (let i = 0; i < 70; i++) walk(rnd() * size, rnd() * size, rnd() * 6.28, 2 + (rnd() * 2 | 0), size / 26, 1, 95);
  for (let i = 0; i < 6; i++) walk(rnd() * size, rnd() * size, rnd() * 6.28, 9, size / 11, 2.4, 255, mains);
  for (let i = 0; i < 26; i++) { const m = mains[(rnd() * mains.length) | 0]; walk(m[0], m[1], m[2] + (rnd() < 0.5 ? 1 : -1) * (0.6 + rnd() * 0.8), 4, size / 18, 1.6, 175); }
  const t = new THREE.CanvasTexture(c); t.wrapS = t.wrapT = THREE.RepeatWrapping; t.colorSpace = THREE.NoColorSpace;
  return t;
}

const VS = `
uniform float uTime, uRadius, uLife, uGrav;
attribute vec3 aBary; attribute vec2 aUV; attribute float aCrack; attribute vec3 aCenter; attribute vec4 aShard; attribute vec3 aVel; attribute vec3 aSpin;
varying vec3 vBary; varying vec2 vUV; varying float vCrack; varying vec3 vN; varying vec3 vView; varying float vFade; varying float vShard; varying vec3 vObjN;
mat3 rotAxis(vec3 k, float a) { float c = cos(a), s = sin(a), t = 1.0 - c;
  return mat3(t*k.x*k.x + c, t*k.x*k.y + s*k.z, t*k.x*k.z - s*k.y, t*k.x*k.y - s*k.z, t*k.y*k.y + c, t*k.y*k.z + s*k.x, t*k.x*k.z + s*k.y, t*k.y*k.z - s*k.x, t*k.z*k.z + c); }
void main() {
  vBary = aBary; vUV = aUV; vCrack = aCrack; vObjN = normalize(aCenter);
  vec3 wpos; vec3 wn;
  if (aShard.w >= 0.0) {
    float t = max(0.0, uTime - aShard.w), sp = length(aSpin);
    mat3 R = rotAxis(sp > 0.0 ? aSpin / sp : vec3(0.0, 1.0, 0.0), sp * t);
    vec3 off = mat3(modelMatrix) * ((position - aCenter) * uRadius);
    wpos = aShard.xyz + aVel * t + vec3(0.0, -0.5 * uGrav * t * t, 0.0) + R * off * (1.0 - 0.5 * clamp(t / uLife, 0.0, 1.0));
    wn = R * (mat3(modelMatrix) * normalize(aCenter));
    vFade = clamp(1.0 - t / uLife, 0.0, 1.0); vShard = 1.0;
  } else {
    wpos = (modelMatrix * vec4(position * uRadius, 1.0)).xyz;
    wn = mat3(modelMatrix) * normal; vFade = 1.0; vShard = 0.0;
  }
  vN = normalize(wn); vView = normalize(cameraPosition - wpos);
  gl_Position = projectionMatrix * viewMatrix * vec4(wpos, 1.0);
}`;
const FS = `
uniform sampler2D uCrackTex; uniform vec3 uColor, uHot; uniform float uTime, uAlpha, uWeak; uniform vec4 uHits[4];
varying vec3 vBary; varying vec2 vUV; varying float vCrack; varying vec3 vN; varying vec3 vView; varying float vFade; varying float vShard; varying vec3 vObjN;
void main() {
  if (vFade <= 0.0) discard;
  float rim = pow(1.0 - abs(dot(vN, vView)), 2.2);
  float e = min(min(vBary.x, vBary.y), vBary.z), edge = 1.0 - smoothstep(0.0, 0.045, e);
  float c = texture2D(uCrackTex, vUV).r, crack = (c > 0.05 && c > 1.0 - vCrack) ? 1.0 : 0.0;
  float rip = 0.0;
  for (int i = 0; i < 4; i++) {
    float t = uTime - uHits[i].w;
    if (t >= 0.0 && t < 0.5) {
      float ang = acos(clamp(dot(vObjN, uHits[i].xyz), -1.0, 1.0));
      rip += smoothstep(0.28, 0.0, abs(ang - t * 7.0)) * (1.0 - t / 0.5) + smoothstep(0.6, 0.0, ang) * max(0.0, 1.0 - t / 0.12);
    }
  }
  float panel = fract(sin(dot(vObjN, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
  float a = 0.07 + 0.04 * panel + 0.5 * rim + 0.4 * edge + 0.75 * crack + 0.45 * rip;
  vec3 col = uColor * (0.55 + 1.5 * rim + 1.4 * edge + 0.25 * panel) + uHot * (2.0 * crack + 1.7 * rip);
  if (vShard > 0.5) { a = (0.5 + 0.45 * edge + 0.9 * crack) * vFade; col = uColor * (1.4 + 1.2 * edge) + uHot * (2.4 * crack + 1.5 * vFade); }
  a *= uAlpha * (gl_FrontFacing ? 1.0 : 0.45);
  // A failing shield flickers
  a *= 1.0 - uWeak * 0.45 * step(0.5, fract(uTime * 17.0 + panel * 0.3));
  gl_FragColor = vec4(col, clamp(a, 0.0, 1.0));
  #include <tonemapping_fragment>
  #include <colorspace_fragment>
}`;

let GEO = null;
function baseGeometry() {
  if (GEO) return GEO;
  const g = new THREE.IcosahedronGeometry(1, DETAIL);   // non-indexed: three vertices per face
  const n = g.attributes.position.count, faces = n / 3, pos = g.attributes.position.array;
  const bary = new Float32Array(n * 3), uv = new Float32Array(n * 2), center = new Float32Array(n * 3);
  let seed = 3; const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
  const centers = [];
  for (let f = 0; f < faces; f++) {
    const cx = (pos[f * 9] + pos[f * 9 + 3] + pos[f * 9 + 6]) / 3, cy = (pos[f * 9 + 1] + pos[f * 9 + 4] + pos[f * 9 + 7]) / 3, cz = (pos[f * 9 + 2] + pos[f * 9 + 5] + pos[f * 9 + 8]) / 3;
    centers.push(new THREE.Vector3(cx, cy, cz));
    // Each panel shows its own patch of the crack texture
    const ou = rnd(), ov = rnd(), s = 0.3, ang = rnd() * 6.28;
    const corners = [[0, 0], [1, 0], [0.5, 0.87]];
    for (let k = 0; k < 3; k++) {
      const v = f * 3 + k;
      bary[v * 3 + k] = 1;
      const [cu, cv] = corners[k], ru = cu * Math.cos(ang) - cv * Math.sin(ang), rv = cu * Math.sin(ang) + cv * Math.cos(ang);
      uv[v * 2] = ou + ru * s; uv[v * 2 + 1] = ov + rv * s;
      center[v * 3] = cx; center[v * 3 + 1] = cy; center[v * 3 + 2] = cz;
    }
  }
  g.setAttribute('aBary', new THREE.BufferAttribute(bary, 3));
  g.setAttribute('aUV', new THREE.BufferAttribute(uv, 2));
  g.setAttribute('aCenter', new THREE.BufferAttribute(center, 3));
  GEO = { g, faces, centers };
  return GEO;
}

class Dome {
  constructor(scene, tex) {
    const B = baseGeometry(), n = B.faces * 3;
    const g = new THREE.BufferGeometry();
    for (const k of ['position', 'normal', 'aBary', 'aUV', 'aCenter']) g.setAttribute(k, B.g.attributes[k]);
    this.crack = new Float32Array(n); this.shard = new Float32Array(n * 4); this.vel = new Float32Array(n * 3); this.spin = new Float32Array(n * 3);
    g.setAttribute('aCrack', new THREE.BufferAttribute(this.crack, 1).setUsage(THREE.DynamicDrawUsage));
    g.setAttribute('aShard', new THREE.BufferAttribute(this.shard, 4).setUsage(THREE.DynamicDrawUsage));
    g.setAttribute('aVel', new THREE.BufferAttribute(this.vel, 3).setUsage(THREE.DynamicDrawUsage));
    g.setAttribute('aSpin', new THREE.BufferAttribute(this.spin, 3).setUsage(THREE.DynamicDrawUsage));
    this.u = {
      uTime: { value: 0 }, uRadius: { value: AEGIS.radius }, uLife: { value: 0.6 }, uGrav: { value: 10 }, uCrackTex: { value: tex },
      uColor: { value: new THREE.Color(GOLD) }, uHot: { value: new THREE.Color('#fff6e0') }, uAlpha: { value: 1 }, uWeak: { value: 0 },
      uHits: { value: Array.from({ length: 4 }, () => new THREE.Vector4(0, 1, 0, -99)) },
    };
    this.mat = new THREE.ShaderMaterial({ uniforms: this.u, vertexShader: VS, fragmentShader: FS, transparent: true, depthWrite: false, side: THREE.DoubleSide });
    this.mesh = new THREE.Mesh(g, this.mat); this.mesh.frustumCulled = false; this.mesh.renderOrder = 5; this.mesh.visible = false;
    scene.add(this.mesh);
    this.faces = B.faces; this.centers = B.centers; this.gone = new Uint8Array(B.faces); this.fc = new Float32Array(B.faces);
    this.state = 'off'; this.fadeT = 0; this.hitI = 0; this.formT = 0; this.stage = 0;
    this.v = new THREE.Vector3(); this.w = new THREE.Vector3();
    this.reset();
  }
  reset() {
    this.shard.fill(0); for (let i = 0; i < this.shard.length; i += 4) this.shard[i + 3] = -1;
    this.crack.fill(0); this.fc.fill(0); this.gone.fill(0); this.vel.fill(0); this.spin.fill(0);
    for (const h of this.u.uHits.value) h.w = -99;
    this.flag();
  }
  flag() { const a = this.mesh.geometry.attributes; a.aCrack.needsUpdate = a.aShard.needsUpdate = a.aVel.needsUpdate = a.aSpin.needsUpdate = true; }
  setCrack(f, c) { this.fc[f] = c; for (let k = 0; k < 3; k++) this.crack[f * 3 + k] = c; }
  // Send one panel flying off in world space
  detach(f, now, speed, life, spread = 1.2, up = 1) {
    if (this.gone[f]) return;
    this.gone[f] = 1;
    const wc = this.w.copy(this.centers[f]).multiplyScalar(AEGIS.radius); this.mesh.localToWorld(wc);
    const nrm = this.v.copy(this.centers[f]).normalize().transformDirection(this.mesh.matrixWorld);
    const vx = nrm.x * speed + (Math.random() - 0.5) * spread, vy = nrm.y * speed + (Math.random() - 0.2) * spread * up, vz = nrm.z * speed + (Math.random() - 0.5) * spread;
    const ax = Math.random() - 0.5, ay = Math.random() - 0.5, az = Math.random() - 0.5, am = Math.hypot(ax, ay, az) || 1, rate = 6 + Math.random() * 10;
    for (let k = 0; k < 3; k++) {
      const v = f * 3 + k;
      this.shard[v * 4] = wc.x; this.shard[v * 4 + 1] = wc.y; this.shard[v * 4 + 2] = wc.z; this.shard[v * 4 + 3] = now;
      this.vel[v * 3] = vx; this.vel[v * 3 + 1] = vy; this.vel[v * 3 + 2] = vz;
      this.spin[v * 3] = ax / am * rate; this.spin[v * 3 + 1] = ay / am * rate; this.spin[v * 3 + 2] = az / am * rate;
    }
    this.u.uLife.value = life;
  }
}

export class AegisFX {
  constructor(fx) {
    this.fx = fx; this.scene = fx.scene; this.tex = crackTexture(); this.domes = new Map(); this.t = 0;
    this.v = new THREE.Vector3(); this.v2 = new THREE.Vector3(); this.q = new THREE.Quaternion();
  }
  of(p) { let d = this.domes.get(p); if (!d) { d = new Dome(this.scene, this.tex); this.domes.set(p, d); } return d; }

  // The shield's centre (his chest) in world space
  place(p, d) {
    toWorldZ(p.x, p.y + p.h * 0.62, p.z || 0, d.mesh.position);
    d.mesh.rotation.y = yawOf(p.x);
    d.mesh.updateMatrixWorld(true);
  }
  // A sim-plane direction (dx, dy) at x as a unit vector in the dome's own space
  localDir(p, d, dx, dy, dz = 0) {
    return planeDir3(p.x, dx, dy, dz, this.v).normalize().applyQuaternion(d.mesh.getWorldQuaternion(this.q).invert());
  }

  onEvent(ev) {
    const p = ev.p;
    switch (ev.type) {
      case 'aegisOn': {
        const d = this.of(p); d.reset(); d.state = 'up'; d.formT = 0; d.stage = 0; d.mesh.visible = true; d.u.uAlpha.value = 0; d.u.uWeak.value = 0;
        this.place(p, d);
        const c = { x: p.x, y: p.y + p.h * 0.62 };
        this.fx.sprite(c.x, c.y, 'ring', '#fff1c9', AEGIS.radius * 1.4, 0.3, 1.6);
        this.fx.sprite(c.x, c.y, 'glow', GOLD, AEGIS.radius * 2.2, 0.22, 1.2);
        this.fx.burst(c.x, c.y, '#fff1c9', 22, 5, 0.26, 0.3);
        break;
      }
      case 'aegisHit': {
        const d = this.of(p); if (d.state !== 'up') break;
        this.place(p, d);
        const L = this.localDir(p, d, ev.dx, ev.dy, ev.dz || 0), now = this.t;
        d.u.uHits.value[d.hitI].set(L.x, L.y, L.z, now); d.hitI = (d.hitI + 1) % 4;
        // Crack the panels around the impact, most at its centre; the whole dome crazes as it weakens
        const amount = 0.35 + ev.dmg / AEGIS.hp * 3.2, cos = Math.cos(0.95), floor = (1 - ev.frac) * 0.42;
        for (let f = 0; f < d.faces; f++) {
          if (d.gone[f]) continue;
          const dot = d.centers[f].x * L.x + d.centers[f].y * L.y + d.centers[f].z * L.z;
          let c = d.fc[f];
          if (dot > cos) { const k = (dot - cos) / (1 - cos); c += amount * k * k * (0.7 + Math.random() * 0.6); }
          c = Math.max(c, floor * (0.6 + 0.8 * ((f * 0.618) % 1)));
          d.setCrack(f, c);
          if (c > 1.2) d.detach(f, now, 2.2 + Math.random() * 1.5, 0.55, 1.6);   // shattered through: this panel splinters off
        }
        // Past two thirds and one third of its strength, a cluster of the worst panels splinters away
        const stage = ev.frac < 0.34 ? 2 : ev.frac < 0.67 ? 1 : 0;
        if (stage > d.stage) {
          d.stage = stage;
          const order = [...Array(d.faces).keys()].filter(f => !d.gone[f]).sort((a, b) => d.fc[b] - d.fc[a]).slice(0, 10 + stage * 8);
          for (const f of order) d.detach(f, now, 2.5 + Math.random() * 2, 0.6, 2);
          this.fx.sprite(ev.x, ev.y, 'star', '#ffffff', 1.3, 0.16, 1.4);
        }
        d.flag();
        d.u.uWeak.value = ev.frac < 0.3 ? 1 : ev.frac < 0.5 ? 0.4 : 0;
        // Impact: sparks thrown off the surface, and energy streaming from the impact into his bracer
        const ax = Math.atan2(ev.dy, ev.dx);
        this.fx.burst(ev.x, ev.y, '#fff6e0', 10 + Math.round(ev.dmg), 6 + ev.dmg * 0.3, 0.22, 0.28, { dir: ax, spread: 2.2, grav: 6 });
        this.fx.sprite(ev.x, ev.y, 'star', '#fff1c9', 0.6 + ev.dmg * 0.04, 0.14, 1.5);
        this.absorb(p, ev.x, ev.y, 8 + Math.round(ev.dmg * 0.8));
        break;
      }
      case 'aegisOff': {
        const d = this.domes.get(p); if (!d || d.state !== 'up') break;
        this.place(p, d);
        const now = this.t, c = { x: ev.x, y: ev.y };
        if (ev.why === 'break') {
          // Shatters: every remaining panel bursts outward and tumbles away under gravity
          d.u.uGrav.value = 12;
          for (let f = 0; f < d.faces; f++) d.detach(f, now, 3 + Math.random() * 4, 0.85, 2.5, 1.5);
          d.fadeT = 0.9;
          this.fx.sprite(c.x, c.y, 'star', '#ffffff', AEGIS.radius * 2.4, 0.2, 1.5);
          this.fx.sprite(c.x, c.y, 'ring', '#fff1c9', AEGIS.radius * 1.2, 0.35, 3.2);
          this.fx.burst(c.x, c.y, '#fff6e0', 40, 11, 0.24, 0.45, { grav: 9 });
          this.fx.burst(c.x, c.y, GOLD, 26, 8, 0.3, 0.5, { grav: 7 });
        } else if (ev.why === 'detonate') {
          // Detonated: the panels blast outward fast, with a shock ring
          d.u.uGrav.value = 2;
          for (let f = 0; f < d.faces; f++) d.detach(f, now, 9 + Math.random() * 5, 0.42, 1.5, 0.5);
          d.fadeT = 0.5;
          this.fx.sprite(c.x, c.y, 'ring', '#ffffff', AEGIS.radius * 1.1, 0.32, 3.6);
          this.fx.sprite(c.x, c.y, 'glow', GOLD, AEGIS.radius * 3.2, 0.25, 1.6);
          this.fx.sprite(c.x, c.y, 'star', '#ffffff', AEGIS.radius * 2.8, 0.18, 1.4);
          this.fx.groundRing(p.x, p.y, '#fff1c9', 0.4, AEGIS.detonate.r * 1.3, 0.35, 0.9);
          this.fx.burst(c.x, c.y, '#fff1c9', 36, 14, 0.3, 0.35);
          this.absorb(p, c.x, c.y, 16);
        } else {
          // Time ran out: the panels come loose and dissolve upward
          d.u.uGrav.value = -1.5;
          for (let f = 0; f < d.faces; f++) d.detach(f, now + Math.random() * 0.15, 0.4 + Math.random() * 0.4, 0.4, 0.6, 0.5);
          d.fadeT = 0.6;
        }
        d.flag(); d.state = 'fading';
        break;
      }
    }
  }

  // Energy absorbed by the shield flows into the bracer: sparks that fly from (x, y) to his muzzle
  absorb(p, x, y, n) {
    const rig = this.fx.rigs.get(p), to = rig && rig.extra.muzzle ? rig.extra.muzzle.getWorldPosition(this.v2) : toWorld(p.x, p.y + 1.1, 0.25, this.v2);
    const from = toWorld(x, y, 0.2, this.v);
    for (let i = 0; i < n; i++) {
      const life = 0.16 + Math.random() * 0.14, o = new THREE.Vector3((Math.random() - 0.5) * 0.5, (Math.random() - 0.5) * 0.5, (Math.random() - 0.5) * 0.3);
      const P = this.fx.particle(from.clone().add(o), Math.random() < 0.5 ? '#ffffff' : '#ffe2a8', 0.17, life);
      P.v.copy(to).sub(from).sub(o).divideScalar(life); P.drag = 1; P.grav = 0;
    }
  }

  update(dt, world) {
    this.t += dt;
    const seen = new Set();
    for (const p of world.players) {
      seen.add(p);
      const d = this.domes.get(p);
      if (p.aegis && d && d.state === 'up') {
        this.place(p, d);
        d.formT += dt;
        const k = Math.min(1, d.formT / 0.12), frac = p.aegis.hp / p.aegis.max, tLeft = p.aegis.t / 60;
        d.u.uRadius.value = AEGIS.radius * (0.7 + 0.3 * (1 - (1 - k) * (1 - k))) * (1 + Math.sin(this.t * 5) * 0.008);
        // It thins in its last second, blinking
        d.u.uAlpha.value = k * (tLeft < 1 ? 0.55 + 0.45 * (Math.sin(this.t * 30) > 0 ? 1 : 0) : 1) * (0.75 + 0.25 * frac);
        d.mesh.visible = p.state !== 'dead' && p.state !== 'downed';
      } else if (d && d.state === 'up' && !p.aegis) {
        // Removed without an event (swap, reset): just hide it
        d.state = 'off'; d.mesh.visible = false;
      }
      // Overcharged: gold-white sparks crackle off the bracer
      if (p.overcharge > 0 && p.state !== 'dead' && p.state !== 'downed') {
        const rig = this.fx.rigs.get(p);
        if (rig && rig.root.visible && rig.extra.muzzle && Math.random() < 0.25 + p.overcharge / 160) {
          const at = rig.extra.muzzle.getWorldPosition(this.v), P = this.fx.particle(at, Math.random() < 0.5 ? '#ffffff' : '#ffe2a8', 0.12 + Math.random() * 0.08, 0.14 + Math.random() * 0.1);
          P.v.set((Math.random() - 0.5) * 4, Math.random() * 3, (Math.random() - 0.5) * 2); P.drag = 0.86; P.grav = 4;
        }
      }
    }
    for (const [p, d] of this.domes) {
      d.u.uTime.value = this.t;
      if (d.state === 'fading') { d.fadeT -= dt; if (d.fadeT <= 0) { d.state = 'off'; d.mesh.visible = false; } }
      if (!seen.has(p) && d.state !== 'fading') { this.scene.remove(d.mesh); d.mesh.geometry.dispose(); d.mat.dispose(); this.domes.delete(p); }
    }
  }

  // Warm-up stand-in (compiles the shader at load). It stays, hidden, so the compiled program stays cached.
  warmShow(at) { const d = this.warm || (this.warm = new Dome(this.scene, this.tex)); d.mesh.position.copy(at); d.mesh.visible = true; return d.mesh; }
  warmDone() { if (this.warm) this.warm.mesh.visible = false; }
}
