// Three.js view: scene, level geometry on the (curving) gameplay path, cameras, post-processing.
// Version 13: every person playing has a third-person camera of their own (camera.js), drawn split-screen.
import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { BOXES, GATES, pathFrame, ARC_START, ARC_END, ARC_R, TOWER_CENTER, curvedSpan, routeAt } from './level.js';
import { buildLandmarks, Breakables, ATMOS } from './landmarks.js';
import { SETTINGS, PLAYER_COLORS, CHARS, IMPACT_STYLES, IMPACT_ACCENT } from './config.js';
import { buildPlayerRig } from './rigs.js';
import { animatePlayer } from './anim.js';
import { buildEnemyRig, animateEnemy } from './enemyRigs.js';
import { FX, ImpactShader } from './fx.js';
import { toWorldZ, yawOf, laneOf } from './space.js';
import { PlayerCam, TeamCam, layout } from './camera.js';
import { isBot } from './bot.js';

const yawAt = x => { const f = pathFrame(x); return Math.atan2(-f.tz, f.tx); };
// A rig leaving the scene frees its geometry buffers. Enemy rigs free their materials too (a warmed stand-in
// in fx.warm keeps those shaders compiled); player rigs keep theirs so a character swap never recompiles one.
function disposeTree(root, materials = true) {
  root.traverse(o => {
    if (o.geometry && !o.isSprite) o.geometry.dispose();
    if (materials && o.material) (Array.isArray(o.material) ? o.material : [o.material]).forEach(m => m.dispose());
  });
}

export class View {
  constructor(canvas) {
    this.canvas = canvas;
    this.r = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
    this.r.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    this.r.toneMapping = THREE.ACESFilmicToneMapping; this.r.toneMappingExposure = 0.95;
    this.r.shadowMap.enabled = true; this.r.shadowMap.type = THREE.PCFSoftShadowMap;
    this.scene = new THREE.Scene();
    this.scene.fog = new THREE.Fog(0xc6e2f4, 70, 260);
    // Before anyone joins (the title screen) the team camera looks on from the side
    this.teamCam = new TeamCam();
    this.camera = this.teamCam.cam;
    this.cams = new Map();     // player -> PlayerCam
    this.views = [];           // what is drawn this frame: { c: a PlayerCam or the TeamCam, vp }
    this.cam = { x: 0, y: 3, dist: 16 };
    this.trauma = 0; this.time = 0; this.bloomKick = 0; this.punch = 0; this.impact = null; this.impactCd = 0; this.hitPause = 0;
    this.rigs = new Map(); this.enemyRigs = new Map();
    this.tmpV = new THREE.Vector3(); this.tmpP = new THREE.Vector3();

    const hemi = new THREE.HemisphereLight(0xd8ecff, 0x7a6f63, 0.95); this.scene.add(hemi); this.hemi = hemi;
    this.sun = new THREE.DirectionalLight(0xffeed6, 2.1);
    this.sun.position.set(-18, 30, 22); this.sun.castShadow = true;
    this.sun.shadow.mapSize.set(2048, 2048);
    const sc = this.sun.shadow.camera; sc.left = -36; sc.right = 36; sc.top = 36; sc.bottom = -36; sc.near = 1; sc.far = 140;
    this.sun.shadow.bias = -0.0006; this.sun.shadow.normalBias = 0.03;
    this.scene.add(this.sun); this.scene.add(this.sun.target);
    const rim = new THREE.DirectionalLight(0xa9dbff, 1.4); rim.position.set(14, 12, -26); this.scene.add(rim);

    this.fx = new FX(this.scene, this.rigs);
    this.baked = new Map();
    this.buildSky(); this.buildBackdrop(); this.buildLevel(); this.buildProps(); buildLandmarks(this); this.flushBaked();
    this.breakables = new Breakables(this.scene, this.fx);

    this.composer = new EffectComposer(this.r);
    this.renderPass = new RenderPass(this.scene, this.camera);
    this.bloom = new UnrealBloomPass(new THREE.Vector2(1280, 720), 0.65, 0.5, 1.5);
    this.output = new OutputPass();
    this.ink = new ShaderPass(ImpactShader); this.ink.enabled = false; this.dim = 0;
    this.composer.addPass(this.renderPass); this.composer.addPass(this.bloom); this.composer.addPass(this.output); this.composer.addPass(this.ink);
    this.fx.warm(this.r, this.camera, this.composer.renderTarget1);
    this.views = [this.teamCam];
    // The impact frame's pass is off until the first big moment: compile it now so that moment never stalls
    try { this.ink.enabled = true; this.ink.uniforms.amount.value = 1; this.composer.render(0); } catch (e) { /* best effort */ }
    this.ink.enabled = false; this.ink.uniforms.amount.value = 0;
  }

  resize(w, h, world) {
    this.r.setSize(w, h, false);
    if (world) world.aspect = w / h;
    this.w = w; this.h = h; this.layoutKey = '';
  }
  // The views for this frame: one per person playing (split-screen), and with three of them the team camera in
  // the spare quarter. The composer works at one view's size (they are all the same size).
  layoutViews(world) {
    const humans = world.players.filter(p => !isBot(p));
    for (const p of humans) if (!this.cams.has(p)) this.cams.set(p, new PlayerCam(p));
    for (const p of this.cams.keys()) if (!humans.includes(p)) this.cams.delete(p);
    const list = humans.map(p => this.cams.get(p));
    if (list.length === 3 || !list.length) list.push(this.teamCam);
    const rects = layout(list.length === 1 ? 1 : Math.max(2, humans.length), this.w || 1, this.h || 1);
    this.views = list.map((c, i) => { c.vp = rects[i] || rects[0]; return c; });
    const vw = this.views[0].vp.w, vh = this.views[0].vp.h, key = `${vw}x${vh}`;
    if (key !== this.layoutKey) {
      this.layoutKey = key;
      this.composer.setSize(vw, vh); this.ink.uniforms.res.value.set(vw, vh);
    }
    for (const c of this.views) if (c.cam.aspect !== c.vp.w / c.vp.h) { c.cam.aspect = c.vp.w / c.vp.h; c.cam.updateProjectionMatrix(); }
  }
  // A person's camera (made on first use, so a command sampled before the first frame still has one)
  camFor(p) {
    if (isBot(p)) return null;
    let c = this.cams.get(p);
    if (!c) { c = new PlayerCam(p); this.cams.set(p, c); }
    return c;
  }

  // Static geometry is merged into one mesh per material to keep draw calls low.
  bake(mesh, cast = true) {
    mesh.updateMatrixWorld(true);
    let g = mesh.geometry.index ? mesh.geometry.toNonIndexed() : mesh.geometry.clone();
    for (const k of Object.keys(g.attributes)) if (!['position', 'normal', 'uv'].includes(k)) g.deleteAttribute(k);
    g.applyMatrix4(mesh.matrixWorld);
    const key = mesh.material.uuid + (cast ? ':c' : ':n');
    if (!this.baked.has(key)) this.baked.set(key, { mat: mesh.material, cast, geos: [] });
    this.baked.get(key).geos.push(g);
    return mesh;
  }
  flushBaked() {
    for (const { mat, cast, geos } of this.baked.values()) {
      const m = new THREE.Mesh(mergeGeometries(geos, false), mat);
      m.castShadow = cast; m.receiveShadow = true; this.scene.add(m);
    }
    this.baked.clear();
  }

  // ---- Environment ----
  buildSky() {
    const geo = new THREE.SphereGeometry(420, 32, 16);
    const mat = new THREE.ShaderMaterial({
      side: THREE.BackSide, depthWrite: false, fog: false,
      uniforms: { top: { value: new THREE.Color(0x2b7fd3) }, mid: { value: new THREE.Color(0x7fbfee) }, bot: { value: new THREE.Color(0xd9eefa) } },
      vertexShader: 'varying vec3 vP; void main(){ vP = normalize(position); gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
      fragmentShader: 'uniform vec3 top, mid, bot; varying vec3 vP; void main(){ float h = vP.y; vec3 c = h > 0.15 ? mix(mid, top, smoothstep(0.15, 0.7, h)) : mix(bot, mid, smoothstep(-0.2, 0.15, h)); gl_FragColor = vec4(c, 1.0); }',
    });
    this.sky = new THREE.Mesh(geo, mat); this.scene.add(this.sky);
    const sunGlow = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.fx.tex.glow, color: 0xfff3d6, transparent: true, depthWrite: false, fog: false }));
    sunGlow.scale.set(90, 90, 1); sunGlow.position.set(-160, 130, -300); this.scene.add(sunGlow);
  }

  buildBackdrop() {
    const cloudMat = new THREE.SpriteMaterial({ map: this.fx.tex.glow, color: 0xeef5fa, transparent: true, opacity: 0.5, depthWrite: false });
    for (let i = 0; i < 44; i++) {
      const s = new THREE.Sprite(cloudMat);
      const a = Math.random() * Math.PI * 2, r = 170 + Math.random() * 160;
      s.position.set(60 + Math.cos(a) * r, -12 + Math.random() * 28, -40 + Math.sin(a) * r * 0.8);
      const k = 30 + Math.random() * 60; s.scale.set(k * 1.8, k, 1); this.scene.add(s);
    }
    // Cloud sea below Skyport
    const sea = new THREE.Mesh(new THREE.PlaneGeometry(1200, 1200), new THREE.MeshStandardMaterial({ color: 0xeef6fb, roughness: 1 }));
    sea.rotation.x = -Math.PI / 2; sea.position.y = -34; this.scene.add(sea);
    // Distant spires
    const spireMat = new THREE.MeshStandardMaterial({ color: 0xc3d7ea, roughness: 0.55 });
    const glowMat = new THREE.MeshStandardMaterial({ color: 0x7fe3ff, emissive: 0x5fd8ff, emissiveIntensity: 1.4 });
    const spots = [[-30, -120], [20, -150], [55, -105], [85, -175], [130, -135], [-70, -170], [175, -110], [215, -160], [250, -95], [10, -210], [110, -220], [290, -150]];
    for (const [x, z] of spots) {
      const h = 45 + Math.random() * 65, w = 4 + Math.random() * 6;
      const m = new THREE.Mesh(new RoundedBoxGeometry(w, h, w, 3, 1.5), spireMat); m.position.set(x, h / 2 - 34, z); this.bake(m, false);
      const band = new THREE.Mesh(new THREE.BoxGeometry(w * 1.02, 0.5, w * 1.02), glowMat); band.position.set(x, h - 38, z); this.bake(band, false);
      const deck = new THREE.Mesh(new THREE.CylinderGeometry(w * 1.3, w * 1.3, 1.2, 24), spireMat); deck.position.set(x, h * 0.6 - 34, z); this.bake(deck, false);
    }
    // Transit rails with moving pods (the civilization's advanced transit)
    this.pods = [];
    const railMat = new THREE.MeshStandardMaterial({ color: 0xbfd0df, roughness: 0.4 });
    const podMat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.3, emissive: 0x5fd8ff, emissiveIntensity: 0.25 });
    for (const [y, z, speed] of [[22, -70, 14], [30, -92, -10], [16, -115, 18]]) {
      const rail = new THREE.Mesh(new THREE.CylinderGeometry(0.25, 0.25, 600, 8), railMat); rail.rotation.z = Math.PI / 2; rail.position.set(60, y, z); this.bake(rail, false);
      for (let i = 0; i < 3; i++) {
        const pod = new THREE.Mesh(new THREE.CapsuleGeometry(1.2, 6, 4, 12), podMat); pod.rotation.z = Math.PI / 2;
        pod.position.set(-200 + i * 160, y - 1.4, z); this.scene.add(pod); this.pods.push({ pod, speed });
      }
    }
    // The Storm Spire tower the path wraps around (CP-08 test)
    const towerMat = new THREE.MeshStandardMaterial({ color: 0xeef3f8, roughness: 0.4 });
    const tower = new THREE.Mesh(new THREE.CylinderGeometry(ARC_R - 3.4, ARC_R - 2.4, 110, 48), towerMat);
    tower.position.set(TOWER_CENTER.x, 20, TOWER_CENTER.z); this.bake(tower, false);
    for (let i = 0; i < 12; i++) {
      const a = i / 12 * Math.PI * 2;
      const strip = new THREE.Mesh(new THREE.BoxGeometry(0.3, 90, 0.3), glowMat);
      strip.position.set(TOWER_CENTER.x + Math.sin(a) * (ARC_R - 3.3), 25, TOWER_CENTER.z + Math.cos(a) * (ARC_R - 3.3)); this.bake(strip, false);
    }
    for (const y of [-4, 18, 40, 62]) {
      const ring = new THREE.Mesh(new THREE.TorusGeometry(ARC_R - 2.8, 0.35, 8, 48), glowMat); ring.rotation.x = Math.PI / 2;
      ring.position.set(TOWER_CENTER.x, y, TOWER_CENTER.z); this.bake(ring, false);
    }
  }

  buildLevel() {
    const M = {
      cap: new THREE.MeshStandardMaterial({ color: 0xd9dfe7, roughness: 0.82 }),
      body: new THREE.MeshStandardMaterial({ color: 0x5f7897, roughness: 0.72 }),
      dark: new THREE.MeshStandardMaterial({ color: 0x46596f, roughness: 0.7 }),
      trim: new THREE.MeshStandardMaterial({ color: 0x7fe3ff, emissive: 0x4fd6ff, emissiveIntensity: 2.0 }),
      gate: new THREE.MeshStandardMaterial({ color: 0xff2e7e, emissive: 0xff2e7e, emissiveIntensity: 1.6, transparent: true, opacity: 0.45, depthWrite: false }),
    };
    this.gateMeshes = [];
    const pc = document.createElement('canvas'); pc.width = pc.height = 128;
    const g = pc.getContext('2d'); g.fillStyle = '#ffffff'; g.fillRect(0, 0, 128, 128);
    g.fillStyle = '#c9d2dc'; g.fillRect(0, 0, 128, 3); g.fillRect(0, 0, 3, 128);
    g.fillStyle = '#e6ebf0'; g.fillRect(62, 20, 4, 88);
    this.panelTex = new THREE.CanvasTexture(pc); this.panelTex.colorSpace = THREE.SRGBColorSpace;
    this.panelTex.wrapS = this.panelTex.wrapT = THREE.RepeatWrapping; this.panelTex.anisotropy = 4;
    M.capTex = M.cap.clone(); M.capTex.map = this.panelTex;
    M.rail = new THREE.MeshStandardMaterial({ color: 0xa9b8c8, roughness: 0.45, metalness: 0.5 });
    for (const b of BOXES) {
      // every box has its own extent across the path now (level.js zSpan)
      const depth = b.z1 - b.z0, zc = (b.z0 + b.z1) / 2, h = b.y1 - b.y0;
      const segs = [];
      if (b.type === 'd') continue;   // (breakable pieces have their own meshes: landmarks.js Breakables)
      const curved = curvedSpan(b.x0, b.x1);
      if (!curved) segs.push([b.x0, b.x1]);
      else { const n = Math.ceil((b.x1 - b.x0) / 0.9); for (let i = 0; i < n; i++) segs.push([b.x0 + (b.x1 - b.x0) * i / n, b.x0 + (b.x1 - b.x0) * (i + 1) / n]); }
      for (const [x0, x1] of segs) {
        const xm = (x0 + x1) / 2, w = (x1 - x0) * (curved ? 1.04 : 1), yaw = yawAt(xm);
        const place = (mesh, y, dz = 0, bake = true) => { toWorldZ(xm, y, zc + dz, mesh.position); mesh.rotation.y = yaw; if (bake) this.bake(mesh, mesh.userData.cast !== false); else this.scene.add(mesh); return mesh; };
        if (b.type === 'g') {
          const g = place(new THREE.Mesh(new THREE.BoxGeometry(w, h, depth), M.gate), b.y0 + h / 2, 0, false);
          this.gateMeshes.push({ mesh: g, tag: b.tag });
          continue;
        }
        if (b.tag === 'rail') {
          // a guard rail: a top bar on posts, with a glowing strip along it
          place(new THREE.Mesh(new THREE.BoxGeometry(w, 0.1, 0.16), M.rail), b.y1 - 0.05);
          place(new THREE.Mesh(new THREE.BoxGeometry(w, 0.04, 0.05), M.trim), b.y0 + 0.5);
          const n = Math.max(1, Math.round(w / 1.6));
          for (let i = 0; i <= n; i++) { const m = new THREE.Mesh(new THREE.BoxGeometry(0.09, h, 0.09), M.rail); toWorldZ(x0 + (x1 - x0) * i / n, b.y0 + h / 2, zc, m.position); m.rotation.y = yaw; this.bake(m, false); }
          continue;
        }
        if (b.tag === 'bound') { place(new THREE.Mesh(new THREE.BoxGeometry(w, h, depth), M.dark), b.y0 + h / 2); continue; }
        const capH = Math.min(0.22, h * 0.4);
        const bodyGeo = curved || b.type === 'o' ? new THREE.BoxGeometry(w, h - capH, depth) : new RoundedBoxGeometry(w, h - capH, depth, 2, 0.12);
        const bodyMesh = new THREE.Mesh(bodyGeo, b.type === 'o' ? M.dark : M.body);
        bodyMesh.userData.cast = b.type !== 's' || h < 10;
        place(bodyMesh, b.y0 + (h - capH) / 2);
        const capGeo = curved ? new THREE.BoxGeometry(w, capH, depth + 0.1) : new RoundedBoxGeometry(w + 0.08, capH, depth + 0.1, 2, 0.06);
        const uv = capGeo.attributes.uv;
        for (let i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) * Math.max(1, w / 2), uv.getY(i) * Math.max(1, (depth + 0.1) / 2));
        place(new THREE.Mesh(capGeo, M.capTex), b.y1 - capH / 2);
        if (b.tag !== 'tunnel' && b.tag !== 'panel') for (const s of [1, -1]) place(new THREE.Mesh(new THREE.BoxGeometry(w, 0.06, 0.06), M.trim), b.y1 - capH - 0.05, s * (depth / 2 + 0.02));
        if (b.type === 'o') place(new THREE.Mesh(new THREE.BoxGeometry(w * 0.9, 0.05, depth * 0.8), M.trim), b.y0 - 0.01);
      }
    }
  }

  buildProps() {
    const white = new THREE.MeshStandardMaterial({ color: 0xf1f4f7, roughness: 0.5 });
    const navy = new THREE.MeshStandardMaterial({ color: 0x2b4f7e, roughness: 0.6 });
    const leafA = new THREE.MeshStandardMaterial({ color: 0x5fb36a, roughness: 0.9 });
    const leafB = new THREE.MeshStandardMaterial({ color: 0x3f8f58, roughness: 0.9 });
    const lamp = new THREE.MeshStandardMaterial({ color: 0x9ff0ff, emissive: 0x5fe0ff, emissiveIntensity: 2.2 });
    const cloth = [0x2fb5c9, 0x2b5d9b, 0xf1f4f7].map(c => new THREE.MeshStandardMaterial({ color: c, roughness: 0.8, side: THREE.DoubleSide }));
    // Scale and shadow flags must be set before baking, since baking copies the geometry.
    const add = (geo, mat, x, y, z, { cast = true, scale = null } = {}) => {
      const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z);
      if (scale) m.scale.set(...scale);
      this.bake(m, cast); return m;
    };
    const potGeo = new RoundedBoxGeometry(1.4, 0.9, 1.4, 2, 0.2), leafGeo = new THREE.IcosahedronGeometry(0.75, 1);
    // A lower back terrace holds the props so they sit below and behind character silhouettes
    const Y = -1.1;
    add(new THREE.BoxGeometry(108, 5, 8), new THREE.MeshStandardMaterial({ color: 0x55708f, roughness: 0.75 }), 43.5, Y - 2.5, -6.4, { cast: false });
    add(new THREE.BoxGeometry(108, 0.2, 8.1), new THREE.MeshStandardMaterial({ color: 0xc9d2dd, roughness: 0.85 }), 43.5, Y - 0.1, -6.4, { cast: false });
    for (let x = -4; x < 96; x += 11) {
      const z = -5.2;
      add(potGeo, white, x, Y + 0.45, z);
      add(leafGeo, leafA, x - 0.2, Y + 1.25, z, { scale: [1, 0.8, 1] });
      add(leafGeo, leafB, x + 0.35, Y + 1.05, z + 0.2, { scale: [0.7, 0.7, 0.7] });
      add(new THREE.CylinderGeometry(0.07, 0.09, 5.5, 8), navy, x + 5, Y + 2.75, z - 2, { cast: false });
      add(new THREE.SphereGeometry(0.22, 12, 10), lamp, x + 5, Y + 5.6, z - 2, { cast: false });
      add(new THREE.PlaneGeometry(1.1, 2.6), cloth[(x / 11 + 10) % 3 | 0], x + 5.62, Y + 3.9, z - 2, { cast: false });
    }
    const archGeo = new THREE.TorusGeometry(9, 0.55, 10, 40, Math.PI);
    for (const x of [8, 48, 88]) add(archGeo, white, x, -1, -16, { cast: false });

    // Skyline Relay dressing: antenna masts behind the rooftops, and the relay beacon at the route's end
    const at = (x, y, depth) => toWorldZ(x, y, depth, new THREE.Vector3());
    for (const [x, y, h] of [[195, 15.6, 6], [212, 15.6, 8], [226, 12.6, 5], [252, 18.6, 6], [262, 18.6, 7], [294, 18.6, 7]]) {
      const b = at(x, y, -2.9);
      add(new THREE.CylinderGeometry(0.1, 0.15, h, 10), navy, b.x, y + h / 2, b.z, { cast: false });
      add(new THREE.SphereGeometry(0.24, 12, 10), lamp, b.x, y + h + 0.15, b.z, { cast: false });
    }
    const bc = at(310, 18.6, -1.2);
    add(new THREE.CylinderGeometry(0.45, 0.85, 14, 16), white, bc.x, 18.6 + 7, bc.z);
    for (const k of [0.3, 0.55, 0.8]) {
      const ring = new THREE.Mesh(new THREE.TorusGeometry(1.45 - k * 0.6, 0.12, 8, 32), lamp);
      ring.rotation.x = Math.PI / 2; ring.position.set(bc.x, 18.6 + 14 * k, bc.z); this.bake(ring, false);
    }
    const beacon = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.fx.tex.glow, color: 0x9ff0ff, transparent: true, depthWrite: false }));
    beacon.scale.set(9, 9, 1); beacon.position.set(bc.x, 18.6 + 14.6, bc.z); this.scene.add(beacon);
  }

  // ---- Entities ----
  syncEntities(world, alpha, dt) {
    const t = this.time;
    const seen = new Set();
    for (const p of world.players) {
      seen.add(p);
      let rig = this.rigs.get(p);
      if (!rig || rig.char !== p.char) {
        if (rig) { this.scene.remove(rig.root); disposeTree(rig.root, false); }
        rig = buildPlayerRig(p.char);
        const ringMat = new THREE.MeshBasicMaterial({ color: PLAYER_COLORS[p.slot], transparent: true, opacity: 0.65, depthWrite: false });
        const ring = new THREE.Mesh(new THREE.RingGeometry(0.42, 0.55, 32), ringMat); ring.rotation.x = -Math.PI / 2; ring.position.y = 0.03;
        ring.scale.setScalar(Math.max(1, CHARS[p.char].width / 0.72));   // RAM stands in a wider ring
        rig.root.add(ring); rig.ring = ring;
        this.scene.add(rig.root); this.rigs.set(p, rig);
      }
      const x = p.prevX + (p.x - p.prevX) * alpha, y = p.prevY + (p.y - p.prevY) * alpha, z = (p.prevZ ?? p.z) + (p.z - (p.prevZ ?? p.z)) * alpha;
      toWorldZ(x, y, z, rig.root.position);
      // turn to face the way he faces (smoothly: a 180 still reads as a quick turn)
      const want = yawOf(x, p.facing, p.facingZ || 0), cur = rig.yaw ?? want, dy = Math.atan2(Math.sin(want - cur), Math.cos(want - cur));
      rig.yaw = Math.abs(dy) < 0.02 ? want : cur + dy * (1 - Math.exp(-dt * 26));
      rig.root.rotation.y = rig.yaw;
      rig.flip.scale.x = 1;
      animatePlayer(rig, p, dt, t);
      // Echo is gone during Thousand Cuts until every cut lands at once; a dodging Nova flickers like a hologram
      const cutting = p.state === 'ult' && p.ultRun && p.ultRun.kind === 'echo' && p.ultRun.t < p.ultRun.fin;
      const phasing = p.state === 'dodge' && p.dodge && p.dodge.t <= 11 && Math.floor(t * 30) % 3 === 0;
      rig.root.visible = p.state !== 'dead' && !cutting && !phasing && !(p.mercy > 0 && p.state !== 'downed' && p.state !== 'ult' && Math.floor(t * 14) % 2 === 0);
      rig.ring.visible = p.state !== 'downed';
    }
    for (const [p, rig] of this.rigs) if (!seen.has(p)) { this.scene.remove(rig.root); disposeTree(rig.root, false); this.rigs.delete(p); }

    const seenE = new Set();
    for (const e of world.enemies) {
      seenE.add(e);
      let R = this.enemyRigs.get(e);
      if (!R) { R = buildEnemyRig(e); this.scene.add(R.root); this.enemyRigs.set(e, R); }
      const x = e.prevX + (e.x - e.prevX) * alpha, y = e.prevY + (e.y - e.prevY) * alpha, z = (e.prevZ ?? e.z) + (e.z - (e.prevZ ?? e.z)) * alpha;
      toWorldZ(x, y, z, R.root.position);
      const fx = e.type === 'shield' ? e.shieldDir : e.facing, fzz = e.type === 'shield' ? e.shieldDirZ || 0 : e.facingZ || 0;
      const want = yawOf(x, fx, fzz), cur = R.yaw ?? want, dy = Math.atan2(Math.sin(want - cur), Math.cos(want - cur));
      R.yaw = Math.abs(dy) < 0.02 ? want : cur + dy * (1 - Math.exp(-dt * 16));
      R.root.rotation.y = R.yaw;
      animateEnemy(R, e, dt, t);
      if (e.tagged > 0 && !R.tag) {
        R.tag = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.fx.tex.star, color: 0xffa53a, transparent: true, depthWrite: false }));
        R.tag.scale.set(0.7, 0.7, 1); R.tag.position.set(0, e.h + 0.5, 0); R.root.add(R.tag);
      }
      if (R.tag) R.tag.visible = e.tagged > 0 && !e.dead;
    }
    for (const [e, R] of this.enemyRigs) if (!seenE.has(e)) { this.scene.remove(R.root); disposeTree(R.root); this.enemyRigs.delete(e); }

    for (const g of this.gateMeshes) {
      g.mesh.visible = GATES[g.tag];
      g.mesh.material.opacity = 0.35 + 0.12 * Math.sin(t * 6);
    }
    for (const pd of this.pods) { pd.pod.position.x += pd.speed * dt; if (pd.pod.position.x > 260) pd.pod.position.x = -220; if (pd.pod.position.x < -220) pd.pod.position.x = 260; }
  }

  // ---- Cameras ----
  // Every view's camera follows its player (camera.js); shake, the landing punch and the route's light are shared
  updateCamera(world, dt, alpha) {
    this.trauma = Math.max(0, this.trauma - dt * 1.8);
    if (world.players.some(p => p.state === 'beam' && p.beam)) this.trauma = Math.max(this.trauma, 0.22);   // the beam shakes the frame the whole time
    if (world.players.some(p => p.state === 'ult' && p.ultRun && p.ultRun.segs)) this.trauma = Math.max(this.trauma, 0.4);   // and Supernova far more
    this.punch *= Math.exp(-dt * 10); this.bloomKick = Math.max(0, this.bloomKick - dt * 3.2);
    const shake = SETTINGS.shake && this.trauma > 0 ? this.trauma * this.trauma * 0.3 : 0;
    for (const c of this.views) c.update(world, dt, alpha, shake, this.punch * 0.5);
    // Each route has its own light (landmarks.js ATMOS): it blends in as the team arrives
    const k = 1 - Math.exp(-dt * 4); this.cam.x += ((world.cam ? world.cam.x : 0) - this.cam.x) * k;
    const A = ATMOS[routeAt(this.cam.x).id] || ATMOS.skyport, ka = 1 - Math.exp(-dt * 2.5);
    const lerpC = (c, hex) => c.lerp(this.tmpC.set(hex), ka); this.tmpC = this.tmpC || new THREE.Color();
    lerpC(this.scene.fog.color, A.fog); this.scene.fog.near += (A.near - this.scene.fog.near) * ka; this.scene.fog.far += (A.far - this.scene.fog.far) * ka;
    const SU = this.sky.material.uniforms; lerpC(SU.top.value, A.top); lerpC(SU.mid.value, A.mid); lerpC(SU.bot.value, A.bot);
    lerpC(this.sun.color, A.sun); lerpC(this.hemi.color, A.hemi);
    this.camera = this.views[0].cam;   // (the effects that face a camera face the first view's)
  }
  // Keep the shadow frustum on the part of the world a view is looking at
  aimSun(c) {
    const f = c.fwd, at = this.tmpV.copy(c.pivot || c.at || c.cam.position);
    if (f) at.addScaledVector(f, 10).setY((c.pivot || at).y);
    this.sun.position.set(at.x - 18, 30 + at.y, at.z + 22); this.sun.target.position.copy(at);
  }

  onEvent(ev) {
    this.fx.onEvent(ev);
    this.impactZ = laneOf(ev);
    if (ev.type === 'boxChip' || ev.type === 'boxBreak' || ev.type === 'liftBounce') this.breakables.onEvent(ev);
    if (ev.type === 'boxBreak') this.trauma = Math.min(1, this.trauma + (ev.b.tag === 'pillar' ? 0.4 : ev.b.tag === 'glass' ? 0.12 : 0.2));
    const shake = { armorBreak: 0.5, slam: 0.45, guardBreak: 0.3, impact: 0.5, ambush: 0.35, challenge: 0.2, playerHit: ev.heavy ? 0.35 : 0.15,
      blast: 0.14 + (ev.level || 1) * 0.06 + (ev.perfect ? 0.1 : 0), burst: ev.charged ? 0.06 + ev.level * 0.04 : 0.05, perfectRelease: 0.1,
      splash: ev.level ? 0.04 + ev.level * 0.03 : 0, rocketJump: 0.2 + 0.42 * (ev.power || 0.5), enemyBlast: 0.3, chargeCrash: 0.3,
      shot: ev.level ? 0.03 + ev.level * 0.04 : 0, dash: ev.level ? 0.04 + ev.level * 0.05 : 0, dashLevel: 0.02 + ev.level * 0.02,
      chargeLevel: ev.level >= 4 ? 0.1 : ev.level >= 3 ? 0.04 : 0, walljump: 0.03,
      land: ev.vy < -16 ? Math.min(0.3, (-ev.vy - 16) * 0.025) : 0,
      snipe: 0.08 + 0.16 * (ev.f || 0), crit: 0.05, deflect: ev.perfect ? 0.12 : 0.05, dashSlash: 0.04 * (ev.tier || 1), crescent: 0.06, pogo: 0.04,
      poundLand: 0.22 + 0.12 * (ev.level || 0), poundDrop: 0.03, aegisHit: 0.05, beamStart: 0.3,
      aegisOff: ev.why === 'break' ? 0.3 : ev.why === 'detonate' ? 0.4 : 0, bossSlam: ev.big ? 0.55 : 0.35, bossPhase: 0.6, bossDown: 0.9, bossCrash: 0.45, bossIntro: 0.15,
      frag: 0.12 + 0.05 * (ev.level || 0), cluster: 0.1, chain: 0.04 + 0.03 * (ev.level || 0), wellOpen: 0.08, wellCollapse: 0.18 + 0.06 * (ev.level || 1), riseBlast: 0.14, perfectDodge: 0.2,
      ultCast: 0.35, ultJoin: 0.3, ultNova: 0.95, ultCut: 0.06, ultFinisher: 0.8, teamFinisher: 0.3,
      // RAM and Fix
      guardBlock: ev.heavy ? 0.2 : 0.07, perfectGuard: 0.18, rampartBreak: 0.45, kineticRelease: 0.25 + 0.5 * (ev.k || 0), rush: ev.level ? 0.05 + 0.06 * ev.level : 0.04,
      plowCatch: 0.08, ramSplat: 0.55, ramBonk: 0.3, wallUp: 0.18, wallDown: ev.broken ? 0.25 : 0, wallHit: 0.04, leapLand: 0.4, provoke: 0.3, quake: 0.4, upliftBlast: 0.2,
      linkHit: 0.05, ramSlam: 1, fortify: 0.2, padBounce: 0.05, rivetBlast: 0.12, sparkRing: 0.2, podLand: 0.7, overhaulPulse: 0.3, gadgetEnd: ev.why === 'broken' ? 0.1 : 0 }[ev.type];
    if (shake) this.trauma = Math.min(1, this.trauma + shake);
    // Big releases light the whole frame for a moment (bloom) and the rocket jump thumps the camera
    const glow = { rocketJump: 0.45 + 0.75 * (ev.power || 0.5), perfectRelease: 0.4, dash: ev.level >= 3 ? 0.35 : 0,
      shot: ev.level >= 3 ? 0.22 : 0, blast: ev.level >= 3 ? 0.15 : 0, snipe: ev.full ? 0.3 : 0.08, beamStart: 0.6, chargeLevel: ev.level >= 4 ? 0.3 : 0,
      aegisOff: ev.why === 'detonate' ? 0.5 : ev.why === 'break' ? 0.3 : 0, poundLand: ev.level >= 2 ? 0.2 + 0.1 * ev.level : 0, bossPhase: 0.6, bossDown: 1,
      wellCollapse: 0.25, riseBlast: 0.2, perfectDodge: 0.35, ultCast: 0.6, ultJoin: 0.5, ultNova: 1.4, ultFinisher: 1, teamFinisher: 1.4, chain: 0.08 * (1 + (ev.level || 0)),
      perfectGuard: 0.3, kineticRelease: 0.3 + 0.7 * (ev.k || 0), ramSplat: 0.5, quake: 0.3, upliftBlast: 0.25, ramSlam: 1.3, podLand: 0.8, overhaulPulse: 0.7, overhaulDone: 0.6,
      fortify: 0.4, wallUp: 0.25, leapLand: 0.3, provoke: 0.35, sparkRing: 0.3, gadgetUp: 0.15, powerUp: 0.15 }[ev.type];
    if (glow) this.bloomKick = Math.min(1.4, this.bloomKick + glow);
    if (ev.type === 'rocketJump') this.punch = Math.min(this.punch, -(0.25 + 0.5 * (ev.power || 0.5)));
    if (ev.type === 'poundLand') this.punch = Math.min(this.punch, -(0.15 + 0.12 * ev.level));   // the frame thumps down with the landing
    if (ev.type === 'kill' && ev.e.type === 'brute') this.trauma = Math.min(1, this.trauma + 0.6);
    // The big moments get an impact frame, in the colour of whoever set it off (if the setting asks for that)
    this.impactBy = ev.p || (ev.owner && ev.owner.kind === 'player' ? ev.owner : null) || ev.by || (ev.members && ev.members[0]) || null;
    if (ev.type === 'impact' || (ev.type === 'armorBreak' && ev.left === 0) || (ev.type === 'parry' && ev.perfect && ev.heavy)) this.startImpact(ev.x, ev.y, ev.type === 'impact' ? 1 : 0.8);
    else if (ev.type === 'kill' && (ev.e.type === 'brute' || ev.e.boss)) this.startImpact(ev.x, ev.y, 1);
    else if (ev.type === 'poundLand' && ev.level >= 3) this.startImpact(ev.x, ev.y + 0.6, 1);
    else if (ev.type === 'snipe' && ev.full && ev.crits > 0) this.startImpact(ev.x1, ev.y1, 0.8);
    else if (ev.type === 'bossPhase' || ev.type === 'bossDown') this.startImpact(ev.x, ev.y, 1.2);
    else if (ev.type === 'ultNova') this.startImpact(ev.x, ev.y, 1.4, true);
    else if (ev.type === 'ultFinisher') this.startImpact(ev.x, ev.y, 1.2, true);
    else if (ev.type === 'teamFinisher') this.pendingImpact = { t: 0.42, x: ev.x, y: ev.y, z: this.impactZ, k: 1.5, by: this.impactBy };   // when the eclipse shatters
    else if (ev.type === 'perfectDodge') this.startImpact(ev.x, ev.y, 0.6);
    else if (ev.type === 'ramSplat' && ev.n >= 2) this.startImpact(ev.x, ev.y, 0.9);
    else if (ev.type === 'kineticRelease' && ev.k >= 0.8) this.startImpact(ev.x, ev.y, 0.9);
    else if (ev.type === 'perfectGuard' && ev.heavy) this.startImpact(ev.x, ev.y, 0.7);
    else if (ev.type === 'ramSlam') this.startImpact(ev.x, ev.y, 1.4, true);
    else if (ev.type === 'podLand') this.startImpact(ev.x, ev.y + 1, 1.1, true);
    if (ev.type === 'ultNova') this.punch = Math.min(this.punch, -0.6);
    if (ev.type === 'ramSplat' || ev.type === 'leapLand' || ev.type === 'quake') this.punch = Math.min(this.punch, -0.3);
    if (ev.type === 'ramSlam' || ev.type === 'podLand') this.punch = Math.min(this.punch, -0.6);
  }

  // Impact frame (Q-C test): a flash, then the chosen look (Settings: Impact frame style; see ImpactShader for
  // the seven) spreading from the hit, a zoom punch and colour split, easing back out. Its key colour is the
  // look's own, or (Settings: Impact frame colour) the player colour or character colour of whoever set it off
  // (the event's player, or the player nearest the impact). In play, a short hit-pause holds the simulation
  // (main.js) while it runs. At most one every 0.9 s. The same pass dims the world while an ultimate is called.
  startImpact(x, y, strength = 1, force = false) {
    if (!SETTINGS.impactFrames || (this.impactCd > 0 && !force)) return;
    const z = this.impactZ ?? 0;   // (onEvent sets it from the event's lane)
    this.impact = { t: 0, dur: 0.26 + 0.12 * strength, x, y, z, k: strength, seed: Math.random() * 100 };
    const style = IMPACT_STYLES.includes(SETTINGS.impactStyle) ? SETTINGS.impactStyle : 'scifi', U = this.ink.uniforms;
    let by = this.impactBy;
    if (!by && this.world) { let bd = Infinity; for (const q of this.world.players) { const dd = Math.hypot(q.x - x, q.y - y, q.z - z); if (dd < bd) { bd = dd; by = q; } } }
    const mode = SETTINGS.impactColor, col = by && mode === 'player' ? PLAYER_COLORS[by.slot] : by && mode === 'character' ? CHARS[by.char].energy : null;
    U.style.value = IMPACT_STYLES.indexOf(style); U.accent.value.set(col || IMPACT_ACCENT[style]); U.tinted.value = col ? 1 : 0;
    this.impactCd = 0.9; this.hitPause = 0.05 + 0.05 * strength;
    this.trauma = Math.min(1, this.trauma + 0.25 * strength); this.bloomKick = Math.min(1.4, this.bloomKick + 0.4 * strength);
  }
  updateImpact(dt, world) {
    this.impactCd = Math.max(0, this.impactCd - dt);
    const I = this.impact, U = this.ink.uniforms, C = world && world.ultCast;
    // An ultimate's call drains the colour from the world; it comes back as the ultimate plays out
    // (and the world darkens again under a team finisher's eclipse, until it shatters)
    const dimTo = C ? (C.phase === 'cast' ? 0.85 : C.phase === 'run' ? 0.3 : C.t >= 8 && C.t < 34 ? 0.8 : 0.12) : 0;
    this.dim += (dimTo - this.dim) * (1 - Math.exp(-dt * (dimTo > this.dim ? 14 : 5)));
    if (this.dim < 0.01 && !dimTo) this.dim = 0;
    U.dim.value = this.dim; U.time.value = this.time;
    if (!I) { U.amount.value = 0; this.ink.enabled = this.dim > 0; return this.ink.enabled; }
    I.t += dt; const k = Math.min(1, I.t / I.dur);
    U.seed.value = I.seed;   // (its centre is set for each view as it draws: render)
    U.invert.value = k < 0.1 ? 1 : Math.max(0, 1 - (k - 0.1) / 0.06);
    U.amount.value = k < 0.78 ? 1 : Math.max(0, 1 - (k - 0.78) / 0.22);
    U.zoom.value = 0.07 * I.k * (1 - k) * (1 - k); U.split.value = 0.008 * I.k * (1 - k);
    U.ring.value = 0.05 + k * 1.25; U.glitch.value = Math.max(0, 1 - k * 2.4) * Math.min(1, I.k); U.phase.value = k;
    this.ink.enabled = true;
    if (I.t >= I.dur) this.impact = null;
    return true;
  }

  render(world, alpha, dt) {
    this.time += dt;
    const PI = this.pendingImpact;
    this.world = world;
    if (PI && (PI.t -= dt) <= 0) { this.pendingImpact = null; this.impactBy = PI.by; this.impactZ = PI.z; this.startImpact(PI.x, PI.y, PI.k, true); this.punch = Math.min(this.punch, -0.6); this.trauma = 1; }
    this.layoutViews(world);
    this.syncEntities(world, alpha, dt);
    this.updateCamera(world, dt, alpha);
    this.fx.update(dt, world, { alpha, rigs: this.rigs, camera: this.camera });
    this.breakables.update(dt, world);
    const inking = this.updateImpact(dt, world), low = SETTINGS.quality === 'low' && !inking, r = this.r;
    r.shadowMap.enabled = SETTINGS.quality !== 'low';
    this.bloom.enabled = SETTINGS.quality !== 'low';
    this.bloom.strength = 0.65 + this.bloomKick * 0.9;
    const H = this.h || 1, split = this.views.length > 1;
    r.setScissorTest(split);
    for (const c of this.views) {
      const v = c.vp, own = c.p ? this.rigs.get(c.p) : null, shown = own ? own.root.visible : false;
      if (own && c.close) own.root.visible = false;   // a camera pressed up against him looks through him
      const hid = c.p ? this.clearSight(c, own) : null;   // and through a teammate between it and him
      r.setViewport(v.x, H - v.y - v.h, v.w, v.h); r.setScissor(v.x, H - v.y - v.h, v.w, v.h);
      this.aimSun(c);
      if (this.impact) { const s = this.project(c, this.impact.x, this.impact.y, this.impact.z); this.ink.uniforms.center.value.set(s.nx, s.ny); }
      if (low) r.render(this.scene, c.cam);
      else { this.renderPass.camera = c.cam; this.composer.render(dt); }
      if (own) own.root.visible = shown;
      if (hid) for (const r of hid) r.root.visible = true;
    }
    r.setScissorTest(false); r.setViewport(0, 0, this.w || 1, H);
  }

  // Teammates standing between a view's camera and its player (or right up against the camera) are hidden from
  // that view while it draws: returns the rigs hidden, to show again
  clearSight(c, own) {
    let out = null;
    const A = c.cam.position, B = c.pivot, ab = this.tmpV.subVectors(B, A), L2 = ab.lengthSq() || 1;
    for (const [q, rig] of this.rigs) {
      if (rig === own || !rig.root.visible) continue;
      const P = rig.root.position, t = Math.max(0, Math.min(1, ((P.x - A.x) * ab.x + (P.y + 1 - A.y) * ab.y + (P.z - A.z) * ab.z) / L2));
      const dx = A.x + ab.x * t - P.x, dy = A.y + ab.y * t - (P.y + 1), dz = A.z + ab.z * t - P.z;
      if (Math.hypot(dx, dz) < 0.5 + (q.w || 0.7) * 0.6 && Math.abs(dy) < (q.h || 1.8) * 0.6 && t < 0.9) { rig.root.visible = false; (out || (out = [])).push(rig); }
    }
    return out;
  }

  // A sim point in a view: CSS px on the canvas (x, y), normalised in the view (nx, ny: 0..1 from bottom left),
  // and whether it is in front of the camera and inside the view (vis)
  project(c, x, y, z = 0, out = { x: 0, y: 0, nx: 0, ny: 0, vis: true }) {
    const v = toWorldZ(x, y, z, this.tmpP).project(c.cam), vp = c.vp;
    out.nx = v.x * 0.5 + 0.5; out.ny = v.y * 0.5 + 0.5;
    out.x = vp.x + out.nx * vp.w; out.y = vp.y + (1 - out.ny) * vp.h;
    out.vis = v.z < 1 && v.z > -1 && out.nx > -0.02 && out.nx < 1.02 && out.ny > -0.02 && out.ny < 1.02;
    return out;
  }
  // The same in view i (the first by default), or in the view of the person playing p (screenOf(x, y, z, p))
  screenOf(x, y, z = 0, which = 0, out) {
    const c = typeof which === 'number' ? this.views[which] || this.views[0] : this.cams.get(which) || this.views[0];
    if (!c) return { x: 0, y: 0, nx: 0, ny: 0, vis: false };
    return this.project(c, x, y, z, out);
  }
}
