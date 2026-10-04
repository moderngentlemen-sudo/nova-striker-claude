// Character models (Version 13, Phase 2 groundwork): the path from authored 3D models (glTF/GLB, skinned and
// animated) into the game. The procedural rig (rigs.js) keeps running underneath, invisibly, so everything
// that follows its joints (effects, weapons' muzzles, ghosts) still works; the model is drawn in its place and
// plays the animation clip that matches the character's state, blending between them.
//
// To bring in a real character, add an entry to MODELS: the file, how to stand it up (scale to the character's
// height, a turn so it faces along +x, which is "forward" for every rig), and which of its clips play for each
// state. Settings > Character models picks built-in rigs or the models.
//
// The only model here now is a stand-in to prove the pipeline: RobotExpressive by Tomás Laulhé (Quaternius),
// CC0 1.0, from the three.js examples, standing in for Nova.
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import * as SkeletonUtils from 'three/addons/utils/SkeletonUtils.js';
import { CHARS } from './config.js';
import { outlineMaterial } from './look.js';

export const MODELS = {
  nova: {
    url: 'https://cdn.jsdelivr.net/gh/mrdoob/three.js@r170/examples/models/gltf/RobotExpressive/RobotExpressive.glb',
    credit: 'RobotExpressive by Tomás Laulhé (Quaternius), CC0',
    turn: Math.PI / 2,                      // its front is +z; the rigs face +x
    tint: { Main: 0xe9eef5, Grey: 0x2b4f7e, Black: 0x141a24 },   // material name -> colour, toward Nova's palette
    // state -> clip (and how fast it plays); the first that exists is used
    clips: {
      idle: ['Idle'], walk: ['Walking'], run: ['Running'], jump: ['Jump'], fall: ['Jump'], land: ['Idle'],
      dash: ['Running'], attack: ['Punch'], shoot: ['Punch'], hurt: ['No'], down: ['Death'], victory: ['Dance', 'Wave'],
    },
    once: ['jump', 'attack', 'shoot', 'hurt', 'down'],   // clips that play through once and hold
  },
};

const loader = new GLTFLoader();
const cache = new Map();   // url -> Promise<gltf>
function load(url) {
  if (!cache.has(url)) cache.set(url, loader.loadAsync(url));
  return cache.get(url);
}
export function hasModel(charId) { return !!MODELS[charId]; }

// The state a character's model should show
function stateOf(p) {
  const st = p.state;
  if (st === 'dead' || st === 'downed') return 'down';
  if (st === 'hitstun') return 'hurt';
  if (st === 'dash' || st === 'slide' || st === 'dodge' || st === 'rush' || st === 'zip') return 'dash';
  if (st === 'attack' || st === 'dashslash' || st === 'vb' || st === 'pound' || st === 'lash') return 'attack';
  if (p.shootT > 0 || p.chargeT > 0) return 'shoot';
  if (!p.onGround) return p.vy > 0 ? 'jump' : 'fall';
  const s = Math.abs(p.vx);
  return s > 4.5 ? 'run' : s > 0.6 ? 'walk' : 'idle';
}

// A model dressed onto one character's rig
export class ModelSkin {
  constructor(rig, charId) {
    this.rig = rig; this.def = MODELS[charId]; this.char = charId;
    this.ready = false; this.failed = false; this.cur = null;
    load(this.def.url).then(g => this.build(g)).catch(err => { this.failed = true; console.warn('Character model failed to load; keeping the built-in rig', err); });
  }

  build(gltf) {
    if (this.disposed) return;
    const D = this.def, model = SkeletonUtils.clone(gltf.scene);
    // stand it up: feet on the ground, as tall as the character, facing +x
    // (measured posed: skinned meshes' raw vertex data can be in quite different units from what is drawn)
    model.updateMatrixWorld(true);
    model.traverse(o => { if (o.isSkinnedMesh) { o.skeleton.update(); o.computeBoundingBox(); } });
    const box = new THREE.Box3().setFromObject(model, true), h = box.max.y - box.min.y || 1;
    const k = CHARS[this.char].height / h;
    model.scale.setScalar(k); model.position.y = -box.min.y * k; model.rotation.y = D.turn || 0;
    const holder = new THREE.Group(); holder.add(model);
    const shells = [];
    const parts = []; model.traverse(o => { if (o.isMesh) parts.push(o); });   // (collected first: the loop adds shells)
    model.updateMatrixWorld(true);
    // A part's own units can be far from the game's (this model's skeleton is scaled 100 times inside): its
    // outline width is set from how big the part is drawn against how big its raw geometry is
    const unitsOf = o => {
      if (!o.geometry.boundingBox) o.geometry.computeBoundingBox();
      const raw = o.geometry.boundingBox.getSize(new THREE.Vector3()).length() || 1;
      const drawn = new THREE.Box3().setFromObject(o, true).getSize(new THREE.Vector3()).length() || raw;
      return drawn / raw;   // world metres per unit of the part's geometry
    };
    for (const o of parts) {
      o.castShadow = true; o.receiveShadow = false; o.frustumCulled = false;
      const mats = Array.isArray(o.material) ? o.material : [o.material];
      o.material = mats.map(m => {
        const c = m.clone();
        if (D.tint && D.tint[m.name] !== undefined) c.color = new THREE.Color(D.tint[m.name]);
        if (c.isMeshStandardMaterial) { c.envMapIntensity = 1.5; c.roughness = Math.min(c.roughness, 0.45); }
        return c;
      });
      if (o.material.length === 1) o.material = o.material[0];
      // the outline follows the skeleton too
      // (a skinned part's shell sits beside it with the same local transform and the same skeleton binding; a
      // rigid part's shell simply rides on it)
      let shell;
      const shellMat = outlineMaterial(0x0b0f18, 0.016 / unitsOf(o));
      if (o.isSkinnedMesh) {
        shell = new THREE.SkinnedMesh(o.geometry, shellMat);
        shell.position.copy(o.position); shell.quaternion.copy(o.quaternion); shell.scale.copy(o.scale);
        shell.bind(o.skeleton, o.bindMatrix.clone()); o.parent.add(shell);
      } else { shell = new THREE.Mesh(o.geometry, shellMat); o.add(shell); }
      shell.userData.outline = true; shell.frustumCulled = false; shell.castShadow = false; shells.push(shell);
    }
    this.mixer = new THREE.AnimationMixer(model);
    this.clips = {};
    for (const [state, names] of Object.entries(D.clips)) {
      const clip = names.map(n => THREE.AnimationClip.findByName(gltf.animations, n)).find(Boolean);
      if (!clip) continue;
      const a = this.mixer.clipAction(clip);
      if ((D.once || []).includes(state)) { a.setLoop(THREE.LoopOnce, 1); a.clampWhenFinished = true; }
      this.clips[state] = a;
    }
    this.holder = holder; this.shells = shells;
    this.rig.flip.add(holder);   // (under the flip, so it turns to face the way the character faces)
    this.ready = true;
  }

  // Each frame: hide the built-in meshes, play the right clip
  update(p, dt, on) {
    if (!this.ready) return;
    this.holder.visible = on;
    this.setRigHidden(on);
    if (!on) return;
    const want = stateOf(p), next = this.clips[want] || this.clips.idle;
    if (next && next !== this.cur) {
      next.reset().setEffectiveWeight(1).fadeIn(0.12).play();
      if (this.cur) this.cur.fadeOut(0.12);
      this.cur = next;
    }
    // running plays faster with speed; everything else at its own pace
    if (this.cur) this.cur.timeScale = want === 'run' ? Math.max(0.8, Math.abs(p.vx) / 6.5) : want === 'dash' ? 1.8 : 1;
    this.mixer.update(dt);
    const inked = !this.rig.cloak || this.rig.cloak < 0.05;
    for (const s of this.shells) s.visible = inked;
  }

  // The built-in rig's body parts stay in the scene graph (their joints still move, so effects and attached
  // gear follow them) but aren't drawn. Glowing and see-through parts (blades, shields, plates, energy) are
  // gear, not body: they stay, riding the rig's joints like attachment sockets on the model. Hidden every
  // frame, since the animation shows and hides parts as it goes.
  setRigHidden(hide) {
    if (!this.body) {
      this.body = [];
      this.rig.root.traverse(o => {
        if (!o.isMesh || o === this.rig.ring || o.userData.outline || this.holder.getObjectById(o.id)) return;
        const m = o.material;
        if (!m || m.transparent || (m.emissiveIntensity && m.emissiveIntensity > 1) || m.isMeshBasicMaterial) return;
        this.body.push(o);
      });
    }
    if (hide) for (const o of this.body) o.visible = false;
    else if (this.rigHidden) for (const o of this.body) o.visible = true;
    this.rigHidden = hide;
  }

  dispose() {
    this.disposed = true;
    if (this.holder) { this.holder.removeFromParent(); this.mixer.stopAllAction(); }
    if (this.body) for (const o of this.body) o.visible = true;
  }
}
