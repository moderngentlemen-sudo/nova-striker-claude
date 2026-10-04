// Engine trial: Babylon.js. The same game simulation as the three.js prototype (world.js, level.js, player.js,
// input.js: the 60 Hz 2D sim, unchanged), drawn by Babylon instead. One character (Nova) and one stretch of
// level: the Helix Foundry, from the start through the trench, round the smelter and up the helix.
//
// What Babylon gives here that the three.js version builds by hand: a rendering pipeline with bloom, FXAA,
// tone mapping and grain in one object; a glow layer for emissive parts; shadows; a particle system for
// sparks and debris; and the inspector (` key) to poke at every mesh, light and material while it runs.
/* global BABYLON */
import { DT, CHARS, HOSTILE, SETTINGS, loadSettings } from '../../game/js/config.js';
import { Input } from '../../game/js/input.js';
import { World } from '../../game/js/world.js';
import { BOXES, DESTRUCT, SEGS_OF, pathFrame, routeAt, curvedSpan } from '../../game/js/level.js';

loadSettings();
SETTINGS.aiTeammates = 0;
const canvas = document.getElementById('game');
const engine = new BABYLON.Engine(canvas, true, { stencil: true, antialias: true }, true);
const scene = new BABYLON.Scene(engine);
scene.useRightHandedSystem = true;   // the same axes as the three.js build (and the sim's pathFrame)
const C3 = hex => BABYLON.Color3.FromHexString(typeof hex === 'number' ? '#' + hex.toString(16).padStart(6, '0') : hex);

// ---- Light and air (the foundry's: warm fog, an amber sun, a dusk sky) ----
const FOG = C3('#e6c3a0');
scene.clearColor = new BABYLON.Color4(FOG.r, FOG.g, FOG.b, 1);
scene.fogMode = BABYLON.Scene.FOGMODE_LINEAR; scene.fogColor = FOG; scene.fogStart = 50; scene.fogEnd = 210;
scene.ambientColor = new BABYLON.Color3(0.25, 0.2, 0.18);
const hemi = new BABYLON.HemisphericLight('hemi', new BABYLON.Vector3(0, 1, 0), scene);
hemi.diffuse = C3('#ffe0c2'); hemi.groundColor = C3('#5b4436'); hemi.intensity = 0.75;
const sun = new BABYLON.DirectionalLight('sun', new BABYLON.Vector3(0.5, -1, -0.6).normalize(), scene);
sun.diffuse = C3('#ffc690'); sun.intensity = 2.1;
sun.shadowMinZ = 1; sun.shadowMaxZ = 120;
const shadows = new BABYLON.ShadowGenerator(2048, sun);
shadows.usePercentageCloserFiltering = true; shadows.filteringQuality = BABYLON.ShadowGenerator.QUALITY_MEDIUM;
shadows.bias = 0.002; shadows.normalBias = 0.02;
sun.autoUpdateExtends = false; sun.orthoLeft = -30; sun.orthoRight = 30; sun.orthoTop = 30; sun.orthoBottom = -30;

// A sky dome: a vertical gradient, drawn behind everything and untouched by fog
BABYLON.Effect.ShadersStore.skyVertexShader = `precision highp float; attribute vec3 position; uniform mat4 worldViewProjection;
  varying float vY; void main(){ vY = normalize(position).y; gl_Position = worldViewProjection * vec4(position,1.0); }`;
BABYLON.Effect.ShadersStore.skyFragmentShader = `precision highp float; varying float vY; uniform vec3 top; uniform vec3 mid; uniform vec3 bot;
  void main(){ vec3 c = vY > 0.0 ? mix(mid, top, smoothstep(0.0, 0.6, vY)) : mix(mid, bot, smoothstep(0.0, -0.3, vY)); gl_FragColor = vec4(c,1.0); }`;
const skyMat = new BABYLON.ShaderMaterial('sky', scene, { vertex: 'sky', fragment: 'sky' }, { attributes: ['position'], uniforms: ['worldViewProjection', 'top', 'mid', 'bot'] });
skyMat.setColor3('top', C3('#3a4f78')); skyMat.setColor3('mid', C3('#d99a6a')); skyMat.setColor3('bot', C3('#f2c79a'));
skyMat.backFaceCulling = false; skyMat.disableDepthWrite = true;
const sky = BABYLON.MeshBuilder.CreateSphere('skyDome', { diameter: 900, segments: 16 }, scene);
sky.material = skyMat; sky.infiniteDistance = true; sky.renderingGroupId = 0; sky.applyFog = false;
// Metals and gloss reflect the sky: a reflection probe renders the dome once into a cube map for the PBR materials
const probe = new BABYLON.ReflectionProbe('skyProbe', 128, scene);
probe.renderList.push(sky); probe.refreshRate = BABYLON.RenderTargetTexture.REFRESHRATE_RENDER_ONCE;
scene.environmentTexture = probe.cubeTexture; scene.environmentIntensity = 0.9;

// ---- Materials ----
const pbr = (name, color, { rough = 0.7, metal = 0.1, emissive = null, ei = 1 } = {}) => {
  const m = new BABYLON.PBRMaterial(name, scene);
  m.albedoColor = C3(color); m.roughness = rough; m.metallic = metal;
  if (emissive) { m.emissiveColor = C3(emissive); m.emissiveIntensity = ei; }
  return m;
};
const M = {
  ground: pbr('ground', '#8d7b6e', { rough: 0.85 }), cap: pbr('cap', '#c9b6a0', { rough: 0.6 }),
  walk: pbr('walk', '#6d7a88', { rough: 0.4, metal: 0.6 }), trim: pbr('trim', '#5a3418', { emissive: '#ff8a2a', ei: 1.3 }),
  dark: pbr('dark', '#3a3430'), block: pbr('block', '#a08f80'),
  core: pbr('core', '#2e3542', { rough: 0.35, metal: 0.8 }), coreGlow: pbr('coreGlow', '#5a3418', { emissive: '#ff8a2a', ei: 1.5 }),
  gate: pbr('gate', HOSTILE, { emissive: HOSTILE, ei: 1.6 }),
};
M.gate.alpha = 0.45;
const destructMat = {};
for (const [k, d] of Object.entries(DESTRUCT)) destructMat[k] = pbr('d_' + k, d.color, { rough: k === 'glass' ? 0.05 : 0.8, metal: k === 'barricade' ? 0.6 : 0 });
destructMat.glass.alpha = 0.45;

// ---- The level: every Helix Foundry box, laid along the curved path (curved spans in short pieces) ----
const yawAt = x => { const f = pathFrame(x); return Math.atan2(-f.tz, f.tx); };
const depthFor = b => (b.type === 'o' ? 2.6 : ['panel', 'column', 'pillar'].includes(b.tag) ? 1.8 : b.type === 'g' ? 3.2 : 4.4);
const R = SEGS_OF.foundry, RX0 = 398, RX1 = 764;
const statics = [], breakMesh = new Map(), gateMesh = [];
function piece(name, x0, x1, y0, y1, depth, mat, z = 0) {
  const w = x1 - x0, xm = (x0 + x1) / 2, f = pathFrame(xm);
  const m = BABYLON.MeshBuilder.CreateBox(name, { width: w + (curvedSpan(x0, x1) ? 0.04 : 0), height: y1 - y0, depth }, scene);
  m.position.set(f.px + f.nx * z, (y0 + y1) / 2, f.pz + f.nz * z);
  m.rotation.y = yawAt(xm); m.material = mat;
  return m;
}
for (const b of BOXES) {
  if (b.x1 < RX0 || b.x0 > RX1) continue;
  const depth = depthFor(b);
  if (b.type === 'd') {   // breakables are their own meshes (they shake, then burst)
    const m = piece('break', b.x0, b.x1, b.y0, b.y1, depth * 0.6, destructMat[b.tag]);
    shadows.addShadowCaster(m); m.receiveShadows = true; breakMesh.set(b, m); continue;
  }
  if (b.type === 'g') { gateMesh.push([b, piece('gate', b.x0, b.x1, b.y0, b.y1, depth, M.gate)]); continue; }
  const curved = curvedSpan(b.x0, b.x1), n = curved ? Math.max(1, Math.ceil((b.x1 - b.x0) / 1.2)) : 1;
  const mat = b.tag === 'bound' ? M.dark : b.type === 'o' ? M.walk : b.tag === 'block' ? M.block : M.ground;
  const capH = b.type === 'o' ? 0 : Math.min(0.3, (b.y1 - b.y0) * 0.3);
  for (let i = 0; i < n; i++) {
    const a = b.x0 + (b.x1 - b.x0) * i / n, c = b.x0 + (b.x1 - b.x0) * (i + 1) / n;
    statics.push(piece('body', a, c, b.y0, b.y1 - capH, depth, mat));
    if (capH) statics.push(piece('cap', a, c, b.y1 - capH, b.y1, depth + 0.1, M.cap));
    if (b.tag !== 'bound') statics.push(piece('trim', a, c, b.y1 - capH - 0.06, b.y1 - capH - 0.0, 0.06, M.trim, depth / 2 + 0.03));
  }
}
// One draw per material: Babylon merges the pieces (the three.js build does this with its own batching)
const byMat = new Map();
for (const m of statics) { const k = m.material; if (!byMat.has(k)) byMat.set(k, []); byMat.get(k).push(m); }
for (const [, list] of byMat) {
  const merged = BABYLON.Mesh.MergeMeshes(list, true, true);
  merged.receiveShadows = true; merged.freezeWorldMatrix(); merged.material.freeze?.();
  if (merged.material !== M.trim) shadows.addShadowCaster(merged);
}

// Landmarks: the reactor core at the helix's centre, the smelter, stacks out in the haze
const helixArc = R.find(g => g.kind === 'arc' && g.r === 15), smelterArc = R.find(g => g.kind === 'arc' && g.r === 22);
const core = BABYLON.MeshBuilder.CreateCylinder('core', { height: 46, diameter: 9, tessellation: 40 }, scene);
core.position.set(helixArc.C.x, 17, helixArc.C.z); core.material = M.core; shadows.addShadowCaster(core); core.receiveShadows = true;
for (let i = 0; i < 6; i++) {
  const band = BABYLON.MeshBuilder.CreateTorus('coreBand', { diameter: 9.4, thickness: 0.35, tessellation: 48 }, scene);
  band.position.set(helixArc.C.x, 2 + i * 7, helixArc.C.z); band.material = M.coreGlow;
}
const dome = BABYLON.MeshBuilder.CreateSphere('smelter', { diameter: 22, slice: 0.5, segments: 24 }, scene);
dome.position.set(smelterArc.C.x, -1, smelterArc.C.z); dome.material = pbr('smelterMat', '#6b5a52', { rough: 0.5, metal: 0.5 });
const furnace = BABYLON.MeshBuilder.CreateTorus('furnace', { diameter: 22, thickness: 0.6, tessellation: 64 }, scene);
furnace.position.set(smelterArc.C.x, 0.4, smelterArc.C.z); furnace.material = M.coreGlow;
// (a stack is only placed where it can't stand between the camera and the path at any point of the route)
const segDist = (px, pz, ax, az, bx, bz) => { const dx = bx - ax, dz = bz - az, t = Math.max(0, Math.min(1, ((px - ax) * dx + (pz - az) * dz) / (dx * dx + dz * dz))); return Math.hypot(px - ax - dx * t, pz - az - dz * t); };
const clearOfView = (x, z, r) => { for (let u = RX0; u < RX1; u += 2) { const f = pathFrame(u); if (segDist(x, z, f.px, f.pz, f.px + f.nx * 22, f.pz + f.nz * 22) < r) return false; } return true; };
for (let i = 0, placed = 0; i < 40 && placed < 9; i++) {
  const f = pathFrame(RX0 + (i * 37) % (RX1 - RX0)), back = 30 + (i % 4) * 16, h = 36 + (i % 5) * 7;
  const x = f.px - f.nx * back, z = f.pz - f.nz * back;
  if (!clearOfView(x, z, 6)) continue;
  placed++;
  const s = BABYLON.MeshBuilder.CreateCylinder('stack', { height: h, diameterTop: 3, diameterBottom: 4.2 }, scene);
  s.position.set(x, h / 2 - 6, z); s.material = M.dark;
  const lip = BABYLON.MeshBuilder.CreateTorus('stackLip', { diameter: 3.1, thickness: 0.3 }, scene);
  lip.position.set(x, h - 6, z); lip.material = M.trim;
}

// ---- Post: Babylon's default pipeline (bloom, ACES, FXAA, grain, sharpen) and a glow layer ----
const camera = new BABYLON.UniversalCamera('cam', new BABYLON.Vector3(0, 5, 20), scene);
camera.fov = (SETTINGS.fov || 40) * Math.PI / 180; camera.minZ = 0.1; camera.maxZ = 1000;
const pipe = new BABYLON.DefaultRenderingPipeline('post', true, scene, [camera]);
pipe.bloomEnabled = true; pipe.bloomThreshold = 0.8; pipe.bloomWeight = 0.45; pipe.bloomKernel = 48; pipe.bloomScale = 0.5;
pipe.fxaaEnabled = true; pipe.imageProcessingEnabled = true;
pipe.imageProcessing.toneMappingEnabled = true; pipe.imageProcessing.toneMappingType = BABYLON.ImageProcessingConfiguration.TONEMAPPING_ACES;
pipe.imageProcessing.exposure = 1.1; pipe.imageProcessing.contrast = 1.15;
pipe.imageProcessing.vignetteEnabled = true; pipe.imageProcessing.vignetteWeight = 1.4;
const glow = new BABYLON.GlowLayer('glow', scene, { mainTextureSamples: 2, blurKernelSize: 32 }); glow.intensity = 0.35;

// ---- Nova: a rig built from primitives, posed from the sim each frame ----
const NOVA = CHARS.nova;
function buildRig(color) {
  const root = new BABYLON.TransformNode('nova', scene), body = new BABYLON.TransformNode('body', scene); body.parent = root;
  const suit = pbr('suit', '#e9eef5', { rough: 0.35, metal: 0.2 }), accent = pbr('accent', color, { emissive: color, ei: 1.4 });
  const dark = pbr('under', '#2a3140', { rough: 0.6 });
  const mk = (mesh, mat, parent, x, y, z) => { mesh.material = mat; mesh.parent = parent; mesh.position.set(x, y, z); shadows.addShadowCaster(mesh); return mesh; };
  const hips = new BABYLON.TransformNode('hips', scene); hips.parent = body; hips.position.y = 0.86;
  const torso = mk(BABYLON.MeshBuilder.CreateCapsule('torso', { height: 0.72, radius: 0.24 }, scene), suit, hips, 0, 0.38, 0);
  mk(BABYLON.MeshBuilder.CreateBox('chestPlate', { width: 0.36, height: 0.22, depth: 0.08 }, scene), accent, torso, 0.0, 0.1, 0.21);
  const head = mk(BABYLON.MeshBuilder.CreateSphere('head', { diameter: 0.38, segments: 16 }, scene), suit, hips, 0, 0.92, 0);
  mk(BABYLON.MeshBuilder.CreateBox('visor', { width: 0.3, height: 0.09, depth: 0.12 }, scene), accent, head, 0, 0.02, 0.15);
  const limb = (name, len, r, mat, parent, x, y) => {
    const pivot = new BABYLON.TransformNode(name, scene); pivot.parent = parent; pivot.position.set(x, y, 0);
    mk(BABYLON.MeshBuilder.CreateCapsule(name + 'M', { height: len, radius: r }, scene), mat, pivot, 0, -len / 2 + r, 0);
    return pivot;
  };
  const legL = limb('legL', 0.86, 0.11, dark, hips, 0, 0.02), legR = limb('legR', 0.86, 0.11, dark, hips, 0, 0.02);
  legL.position.z = 0.12; legR.position.z = -0.12;
  const armL = limb('armL', 0.62, 0.085, suit, hips, 0, 0.66), armR = limb('armR', 0.62, 0.085, suit, hips, 0, 0.66);
  armL.position.z = 0.3; armR.position.z = -0.3;
  mk(BABYLON.MeshBuilder.CreateCylinder('blaster', { height: 0.5, diameter: 0.14 }, scene), accent, armL, 0, -0.55, 0).rotation.x = Math.PI / 2;
  // the scarf: a ribbon whose points trail the body (Babylon's ribbon mesh, updated in place)
  const pts = Array.from({ length: 8 }, (_, i) => new BABYLON.Vector3(-i * 0.12, 1.62, 0));
  const ribbon = (p) => [p.map(v => v.add(new BABYLON.Vector3(0, 0.07, 0))), p.map(v => v.add(new BABYLON.Vector3(0, -0.07, 0)))];
  const scarf = BABYLON.MeshBuilder.CreateRibbon('scarf', { pathArray: ribbon(pts), updatable: true, sideOrientation: BABYLON.Mesh.DOUBLESIDE }, scene);
  scarf.material = accent;
  return { root, body, hips, torso, head, legL, legR, armL, armR, scarf, pts, ribbon, yaw: 0, phase: 0, lean: 0, squash: 1 };
}
const rig = buildRig('#5ac8fa');

// Enemies and shots: simple stand-ins (one mesh per enemy, thin instances for the shots)
const enemyMat = pbr('enemy', '#3b2734', { rough: 0.5, metal: 0.4 }), enemyEye = pbr('enemyEye', HOSTILE, { emissive: HOSTILE, ei: 3 });
const flashMat = pbr('flash', '#ffffff', { emissive: '#ffffff', ei: 2 });
const enemyMeshes = new Map();
function enemyMesh(e) {
  let m = enemyMeshes.get(e); if (m) return m;
  m = BABYLON.MeshBuilder.CreateCapsule('enemy', { height: e.h, radius: Math.min(e.w, e.h) / 2 }, scene);
  m.material = enemyMat; shadows.addShadowCaster(m);
  const eye = BABYLON.MeshBuilder.CreateBox('eye', { width: e.w * 0.6, height: 0.1, depth: 0.1 }, scene);
  eye.parent = m; eye.position.set(0, e.h * 0.25, e.w * 0.45); eye.material = enemyEye;
  enemyMeshes.set(e, m); return m;
}
const shotMesh = BABYLON.MeshBuilder.CreateSphere('shot', { diameter: 1, segments: 8 }, scene);
shotMesh.material = pbr('shotMat', '#ffffff', { emissive: '#9fe7ff', ei: 4 }); shotMesh.isPickable = false;
const enemyShotMat = pbr('eShotMat', '#ffffff', { emissive: HOSTILE, ei: 4 });
const eShotMesh = shotMesh.clone('eShot'); eShotMesh.material = enemyShotMat;

// Sparks and debris: Babylon's particle system (a burst per event)
const sparkTex = (() => {
  const t = new BABYLON.DynamicTexture('spark', 64, scene, false), g = t.getContext();
  const grd = g.createRadialGradient(32, 32, 0, 32, 32, 32); grd.addColorStop(0, 'rgba(255,255,255,1)'); grd.addColorStop(0.4, 'rgba(255,220,160,0.8)'); grd.addColorStop(1, 'rgba(255,160,60,0)');
  g.fillStyle = grd; g.fillRect(0, 0, 64, 64); t.update(); t.hasAlpha = true; return t;
})();
function burst(x, y, color, count = 30, power = 6, size = 0.18, gravity = -18) {
  const f = pathFrame(x), ps = new BABYLON.ParticleSystem('burst', count, scene);
  ps.particleTexture = sparkTex; ps.emitter = new BABYLON.Vector3(f.px + f.nx * 0.4, y, f.pz + f.nz * 0.4);
  ps.minEmitBox = ps.maxEmitBox = BABYLON.Vector3.Zero();
  const c = C3(color); ps.color1 = new BABYLON.Color4(c.r, c.g, c.b, 1); ps.color2 = new BABYLON.Color4(1, 1, 1, 1); ps.colorDead = new BABYLON.Color4(c.r, c.g, c.b, 0);
  ps.minSize = size * 0.5; ps.maxSize = size; ps.minLifeTime = 0.2; ps.maxLifeTime = 0.6;
  ps.direction1 = new BABYLON.Vector3(-1, -0.2, -1); ps.direction2 = new BABYLON.Vector3(1, 1.4, 1);
  ps.minEmitPower = power * 0.4; ps.maxEmitPower = power; ps.gravity = new BABYLON.Vector3(0, gravity, 0);
  ps.manualEmitCount = count; ps.blendMode = BABYLON.ParticleSystem.BLENDMODE_ADD; ps.targetStopDuration = 0.8; ps.disposeOnStop = true;
  ps.start();
}

// ---- The simulation (unchanged from the three.js build) ----
const input = new Input(canvas);
const world = new World();
let started = false;
function start(dev = 'kbm') {
  if (started) return;
  world.addPlayer(dev, 'nova'); world.teleport('foundry'); started = true;
  document.getElementById('start').style.display = 'none'; canvas.focus();
}
function stepSim() {
  const cmds = {};
  for (const p of world.players) cmds[p.slot] = input.sample(p.device, (mx, my) => aimFromMouse(mx, my, p), SETTINGS.p1Aim);
  world.step(cmds);
  for (const ev of world.events) onEvent(ev);
  world.events.length = 0;
}
function onEvent(ev) {
  if (ev.type === 'hit') burst(ev.x, ev.y, '#ffd28a', ev.heavy ? 50 : 24, ev.heavy ? 9 : 6);
  else if (ev.type === 'boxChip') burst(ev.x, ev.y, DESTRUCT[ev.b.tag].color, 10, 4, 0.12);
  else if (ev.type === 'boxBreak') { burst(ev.x, ev.y, DESTRUCT[ev.b.tag].color, 60, 9, 0.3); trauma = Math.min(1, trauma + 0.25); }
  else if (ev.type === 'land' && ev.vy < -14) burst(ev.p.x, ev.p.y + 0.05, '#e6c3a0', 16, 3, 0.25, -4);
  else if (ev.type === 'dash') burst(ev.p.x, ev.p.y + 0.9, '#9fe7ff', 18, 4, 0.14, 0);
}

// ---- Camera: the same follow as the three.js build (interpolated sim target, lead, eased vertical) ----
const cam = { x: 400, y: 3, dist: 16 }, LEAD = { per: 0.22, max: 2.2, rate: 1.8 };
let camPrev = null, camCur = null, camTick = -1, lead = 0, trauma = 0;
function updateCamera(alpha, dt) {
  if (world.tick !== camTick) { camPrev = camCur || { ...world.cam }; camCur = { ...world.cam }; camTick = world.tick; }
  const P0 = camPrev || world.cam, P1 = camCur || world.cam;
  const T = { x: P0.x + (P1.x - P0.x) * alpha, y: P0.y + (P1.y - P0.y) * alpha, dist: P0.dist + (P1.dist - P0.dist) * alpha };
  const p = world.players[0];
  if (p) { const want = Math.max(-LEAD.max, Math.min(LEAD.max, p.vx * LEAD.per)); lead += (want - lead) * (1 - Math.exp(-dt * LEAD.rate)); T.x += lead; }
  const k = 1 - Math.exp(-dt * 5.5), ky = 1 - Math.exp(-dt * (5.5 + Math.max(0, Math.abs(T.y - cam.y) - 1.2) * 5));
  cam.x += (T.x - cam.x) * k; cam.y += (T.y - cam.y) * ky; cam.dist += (T.dist - cam.dist) * k;
  trauma = Math.max(0, trauma - dt * 1.8);
  const f = pathFrame(cam.x), d = cam.dist, s = trauma * trauma * 0.35;
  camera.position.set(f.px + f.nx * d + (Math.random() - 0.5) * s, cam.y + d * 0.1 + (Math.random() - 0.5) * s, f.pz + f.nz * d);
  camera.setTarget(new BABYLON.Vector3(f.px, cam.y, f.pz));
  sun.position = new BABYLON.Vector3(f.px - 18, 30 + cam.y, f.pz + 22);
  sun.direction = new BABYLON.Vector3(f.px - sun.position.x, cam.y - sun.position.y, f.pz - sun.position.z).normalize();
}
function screenOf(x, y) {
  const f = pathFrame(x), w = engine.getRenderWidth(), h = engine.getRenderHeight();
  const v = BABYLON.Vector3.Project(new BABYLON.Vector3(f.px, y, f.pz), BABYLON.Matrix.Identity(), scene.getTransformMatrix(), camera.viewport.toGlobal(w, h));
  const r = canvas.getBoundingClientRect();
  return { x: v.x * r.width / w, y: v.y * r.height / h };
}
function aimFromMouse(mx, my, p) {
  const c = screenOf(p.x, p.y + p.h * 0.62), dx = mx - c.x, dy = -(my - c.y), m = Math.hypot(dx, dy);
  return m < 6 ? null : [dx / m, dy / m];
}

// ---- Posing ----
const lerp = (a, b, t) => a + (b - a) * t;
function poseRig(p, alpha, dt) {
  const x = lerp(p.prevX, p.x, alpha), y = lerp(p.prevY, p.y, alpha), f = pathFrame(x);
  rig.root.position.set(f.px, y, f.pz);
  // face along the path (left/right), with a quick turn rather than a snap
  const want = yawAt(x) + (p.facing < 0 ? Math.PI : 0);
  let dy = want - rig.yaw; dy = Math.atan2(Math.sin(dy), Math.cos(dy));
  rig.yaw += dy * Math.min(1, dt / 0.08); rig.root.rotation.y = rig.yaw;
  const speed = Math.abs(p.vx), run = p.onGround ? Math.min(1, speed / NOVA.run) : 0;
  rig.phase += dt * (4 + speed * 1.6) * (p.onGround ? 1 : 0.2);
  const sw = Math.sin(rig.phase) * 0.9 * run;
  rig.legL.rotation.z = sw; rig.legR.rotation.z = -sw;
  rig.armL.rotation.z = -sw * 0.7; rig.armR.rotation.z = sw * 0.7;
  if (!p.onGround) { const t = Math.max(-1, Math.min(1, p.vy / 15)); rig.legL.rotation.z = 0.5 * t + 0.3; rig.legR.rotation.z = -0.3 - 0.2 * t; rig.armL.rotation.z = rig.armR.rotation.z = -0.6 - 0.4 * t; }
  const dash = p.state === 'dash' || p.state === 'slide' || p.state === 'dodge';
  rig.lean = lerp(rig.lean, dash ? -0.55 : -run * 0.18, Math.min(1, dt * 14)); rig.hips.rotation.z = rig.lean;
  const crouch = p.h < NOVA.height - 0.1;
  rig.squash = lerp(rig.squash, crouch ? 0.62 : 1, Math.min(1, dt * 18));
  rig.body.scaling.set(1, rig.squash, 1);
  rig.hips.position.y = 0.86 + (p.onGround ? Math.abs(Math.cos(rig.phase)) * 0.05 * run : 0);
  // scarf: each point eases toward the one ahead, the first pinned at the neck
  const neck = rig.head.getAbsolutePosition().add(new BABYLON.Vector3(0, -0.22, 0));
  rig.pts[0].copyFrom(neck);
  const back = new BABYLON.Vector3(-Math.cos(rig.yaw), 0, Math.sin(rig.yaw));
  for (let i = 1; i < rig.pts.length; i++) {
    const tgt = rig.pts[i - 1].add(back.scale(0.12)).add(new BABYLON.Vector3(0, -0.03 + Math.sin(rig.phase * 0.7 + i) * 0.02, 0));
    rig.pts[i] = BABYLON.Vector3.Lerp(rig.pts[i], tgt, Math.min(1, dt * 22));
  }
  BABYLON.MeshBuilder.CreateRibbon(null, { pathArray: rig.ribbon(rig.pts), instance: rig.scarf });
}
const tmpM = BABYLON.Matrix.Identity();
function drawWorld(alpha, dt) {
  const p = world.players[0]; if (p) poseRig(p, alpha, dt);
  const live = new Set();
  for (const e of world.enemies) {
    if (e.dead && e.deathT > 20) continue;
    const m = enemyMesh(e); live.add(e);
    const x = lerp(e.prevX, e.x, alpha), y = lerp(e.prevY, e.y, alpha), f = pathFrame(x);
    m.position.set(f.px, y + e.h / 2, f.pz); m.rotation.y = yawAt(x) + (e.facing < 0 ? Math.PI : 0) - Math.PI / 2;
    m.material = e.flash > 0 ? flashMat : enemyMat; m.setEnabled(!e.dead);
  }
  for (const [e, m] of enemyMeshes) if (!live.has(e)) { m.dispose(); enemyMeshes.delete(e); }
  const mine = [], theirs = [];
  for (const pr of world.projectiles) {
    if (pr.dead) continue;
    const x = lerp(pr.px ?? pr.x, pr.x, alpha), y = lerp(pr.py ?? pr.y, pr.y, alpha), f = pathFrame(x), r = Math.max(0.12, pr.r) * 2;
    BABYLON.Matrix.ComposeToRef(new BABYLON.Vector3(r, r, r), BABYLON.Quaternion.Identity(), new BABYLON.Vector3(f.px, y, f.pz), tmpM);
    (pr.team === 'e' ? theirs : mine).push(...tmpM.m);
  }
  shotMesh.thinInstanceSetBuffer('matrix', new Float32Array(mine), 16); shotMesh.isVisible = mine.length > 0;
  eShotMesh.thinInstanceSetBuffer('matrix', new Float32Array(theirs), 16); eShotMesh.isVisible = theirs.length > 0;
  for (const [b, m] of breakMesh) {
    m.setEnabled(!b.broken);
    if (!b.broken) { const hurt = 1 - b.hp / DESTRUCT[b.tag].hp; m.scaling.y = 1 - hurt * 0.08; }
  }
}

// ---- The loop: fixed 60 Hz sim, interpolated draw (Babylon's render loop drives it) ----
let acc = 0, last = performance.now();
const hud = document.getElementById('hud');
let hudT = 0;
engine.runRenderLoop(() => {
  const now = performance.now(), dt = Math.min(0.1, (now - last) / 1000); last = now;
  if (!started) { const j = input.pollJoins(new Set()); if (j.length) start(j[0]); }
  if (started && !window.__BJ.manual) {
    acc += dt; let n = 0;
    while (acc >= DT && n < 5) { stepSim(); acc -= DT; n++; }
    if (n === 5) acc = 0;
  }
  const alpha = Math.min(1, acc / DT);
  drawWorld(alpha, dt);
  updateCamera(alpha, dt);
  scene.render();
  if ((hudT += dt) > 0.25) {
    hudT = 0; const p = world.players[0];
    hud.textContent = `Babylon.js ${BABYLON.Engine.Version} · ${engine.getFps().toFixed(0)} fps · ${scene.getActiveMeshes().length} meshes` +
      (p ? ` · x ${p.x.toFixed(1)} · ${routeAt(p.x).name}` : '');
  }
});
window.addEventListener('resize', () => engine.resize());
// The inspector (` key): Babylon's scene explorer and property editor, loaded on first use
let inspector = null, inspectorLoading = false;
window.addEventListener('keydown', e => {
  if (e.code !== 'Backquote' || !started) return;
  if (inspector) { inspector.dispose(); inspector = null; return; }
  if (window.INSPECTOR) { inspector = window.INSPECTOR.ShowInspector(scene); return; }
  if (inspectorLoading) return;
  inspectorLoading = true;
  const s = document.createElement('script');
  s.src = 'https://cdn.jsdelivr.net/npm/babylonjs-inspector@9.29.0/babylon.inspector-v2.bundle.js';
  s.onload = () => { inspectorLoading = false; inspector = window.INSPECTOR.ShowInspector(scene); };
  s.onerror = () => { inspectorLoading = false; };
  document.head.appendChild(s);
});
document.getElementById('start').addEventListener('click', () => start('kbm'));

// Test hooks
window.__BJ = { world, scene, engine, start, manual: false, step(n = 1) { for (let i = 0; i < n; i++) stepSim(); }, input,
  snap() { Object.assign(cam, world.cam); camPrev = camCur = { ...world.cam }; camTick = world.tick; lead = 0; } };
