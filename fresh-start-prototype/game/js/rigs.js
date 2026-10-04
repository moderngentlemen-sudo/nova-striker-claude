// Procedural character rigs (placeholder art that keeps each silhouette's defining features: Nova's Sentinel
// Bracer and visor helmet; Echo's scarf, collar, gauntlets, staff; RAM's tower shield, shoulder cannon and
// horned helmet on a far heavier frame; Fix's goggles, tool pack with its crane arm, wrench and welder).
// Every rig shares one skeleton (same joints and proportions), so the animation drives them all; RAM and Fix
// are drawn bigger or smaller through `size` (CHARS[].scale).
import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { CHARS, ATTACH_LOOK } from './config.js';

const rbox = (w, h, d, r = 0.05) => new RoundedBoxGeometry(w, h, d, 3, Math.min(r, w / 2 - 1e-3, h / 2 - 1e-3, d / 2 - 1e-3));
const cap = (r, len) => new THREE.CapsuleGeometry(r, len, 4, 12);

// Fresnel rim so characters separate from bright backgrounds (readability, especially in 4P)
// The uniforms persist on mat.userData.rim so Echo's Veil can turn the rim into a shimmering outline;
// rimAlpha keeps that outline visible while the rest of the body fades out.
export function addRim(mat, color, strength = 0.45, power = 2.4) {
  const u = { rimColor: { value: new THREE.Color(color) }, rimStrength: { value: strength }, rimAlpha: { value: 0 } };
  mat.userData.rim = u; mat.userData.rimBase = strength; mat.userData.rimCol = new THREE.Color(color);
  mat.onBeforeCompile = shader => {
    Object.assign(shader.uniforms, u);
    shader.fragmentShader = 'uniform vec3 rimColor; uniform float rimStrength; uniform float rimAlpha;\n' + shader.fragmentShader.replace(
      '#include <emissivemap_fragment>',
      `#include <emissivemap_fragment>
      vec3 rimV = isOrthographic ? vec3(0.0, 0.0, 1.0) : normalize(vViewPosition);
      float rimF = pow(1.0 - clamp(abs(dot(normal, rimV)), 0.0, 1.0), ${power.toFixed(2)});
      totalEmissiveRadiance += rimColor * rimF * rimStrength;
      diffuseColor.a = max(diffuseColor.a, rimF * rimAlpha);`);
  };
  mat.customProgramCacheKey = () => 'rim-' + power.toFixed(2);
}

function mats(c) {
  const std = (color, rough, metal = 0.08) => new THREE.MeshStandardMaterial({ color, roughness: rough, metalness: metal });
  // Armour (Version 13): painted metal under a clear lacquer, so it carries a soft coloured sheen and a sharp
  // highlight from the route's environment map (look.js); it takes more of that light than the level does, so
  // characters stand out from the stage
  const armour = (color, rough, metal) => new THREE.MeshPhysicalMaterial({ color, roughness: rough, metalness: metal, clearcoat: 0.65, clearcoatRoughness: 0.18, envMapIntensity: 1.5 });
  return {
    base: armour(c.base, 0.4, 0.1), trim: armour(c.trim, 0.36, 0.3), under: std(c.under, 0.62, 0.15),
    energy: new THREE.MeshStandardMaterial({ color: c.energy, emissive: c.energy, emissiveIntensity: 2.4, roughness: 0.3 }),
    visor: new THREE.MeshStandardMaterial({ color: 0x0b1018, roughness: 0.12, metalness: 0.7 }),
    amber: new THREE.MeshStandardMaterial({ color: 0xffa53a, emissive: 0xff8a1a, emissiveIntensity: 0.6, roughness: 0.1, transparent: true, opacity: 0.72 }),
  };
}
function rimAll(M) { for (const k of ['base', 'trim', 'under']) addRim(M[k], '#d6ecff', k === 'under' ? 0.35 : 0.5); return M; }

function mesh(geo, mat, x = 0, y = 0, z = 0) {
  const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); m.castShadow = true; return m;
}
function group(x = 0, y = 0, z = 0) { const g = new THREE.Group(); g.position.set(x, y, z); return g; }

function limb(parent, M, upperLen, lowerLen, r, z, isArm) {
  const top = group(0, 0, z); parent.add(top);
  top.add(mesh(cap(r, upperLen - r), isArm ? M.under : M.under, 0, -upperLen / 2));
  const joint = group(0, -upperLen, 0); top.add(joint);
  joint.add(mesh(cap(r * 0.92, lowerLen - r), M.under, 0, -lowerLen / 2));
  const end = group(0, -lowerLen, 0); joint.add(end);
  return { top, joint, end };
}

export function buildPlayerRig(charId) {
  if (charId === 'ram' || charId === 'fix') return buildNewRig(charId);
  const c = CHARS[charId], M = rimAll(mats(c)), nova = charId === 'nova';
  const root = group(), size = group(), flip = group(), body = group();
  root.add(size); size.add(flip); flip.add(body);
  const hips = group(0, 0.95, 0); body.add(hips);
  hips.add(mesh(rbox(0.34, 0.2, 0.38, 0.07), M.trim, 0, 0.02));
  const spine = group(0, 0.08, 0); hips.add(spine);
  // Torso: armored chest over the undersuit
  spine.add(mesh(rbox(0.3, 0.3, 0.34, 0.08), M.under, 0, 0.18));
  const chestW = nova ? 0.5 : 0.44;
  spine.add(mesh(rbox(nova ? 0.38 : 0.34, 0.34, chestW, 0.1), M.base, 0.02, 0.42));
  spine.add(mesh(rbox(0.05, 0.05, chestW * 0.7, 0.02), M.energy, nova ? 0.2 : 0.18, 0.44));   // chest seam
  // Shoulders
  for (const z of [0.28, -0.28]) spine.add(mesh(rbox(0.26, 0.16, 0.2, 0.07), nova ? M.base : M.trim, 0, 0.55, z * (nova ? 1.05 : 0.95)));
  // Head
  const head = group(0, 0.66, 0); spine.add(head);
  const heads = {};
  if (nova) head.add(mesh(new THREE.SphereGeometry(0.15, 20, 16), M.base, 0, 0.1));
  if (nova) {
    head.add(mesh(rbox(0.2, 0.08, 0.28, 0.03), M.visor, 0.07, 0.11));
    head.add(mesh(rbox(0.04, 0.02, 0.26, 0.01), M.energy, 0.17, 0.11));
    head.add(mesh(rbox(0.18, 0.05, 0.1, 0.02), M.trim, -0.05, 0.25));      // helmet crest
  } else {
    // Neutral face core: his face and hair come from your approved reference, not from here
    const faceMat = new THREE.MeshStandardMaterial({ color: 0xc9bdb1, roughness: 0.85 });
    head.add(mesh(new THREE.SphereGeometry(0.14, 24, 18), faceMat, 0, 0.1));
    const eyeMat = new THREE.MeshStandardMaterial({ color: 0x1a1a20, roughness: 0.4 });
    for (const z of [0.045, -0.045]) head.add(mesh(new THREE.SphereGeometry(0.018, 8, 6), eyeMat, 0.128, 0.125, z));
    // Short, messy blond hair (shown with the bare face and the mask; tucked away under the helmet)
    const hairMat = new THREE.MeshStandardMaterial({ color: 0xd9b464, roughness: 0.8 });
    const hair = group(0, 0.1, 0); head.add(hair);
    const hairCap = mesh(new THREE.SphereGeometry(0.15, 24, 12, 0, Math.PI * 2, 0, Math.PI * 0.42), hairMat);
    hairCap.rotation.z = 0.35; hair.add(hairCap);   // tilted back: hairline above the brow, fuller at the back
    for (const [x, y, z, rz] of [[0.09, 0.1, 0.05, -0.9], [0.1, 0.09, -0.04, -1.05], [0.05, 0.13, 0.0, -0.6], [0.0, 0.14, -0.06, -0.3]]) {
      const tuft = mesh(new THREE.ConeGeometry(0.035, 0.09, 5), hairMat, x, y, z); tuft.rotation.z = rz; hair.add(tuft);
    }
    // Full helmet: open-faced shell with a semi-transparent amber visor (face stays visible)
    const helmet = group(); head.add(helmet);
    helmet.add(mesh(new THREE.SphereGeometry(0.165, 28, 18, Math.PI + 0.95, Math.PI * 2 - 1.9, 0, Math.PI * 0.78), M.base, 0, 0.1));
    const visor = mesh(new THREE.SphereGeometry(0.17, 20, 14, Math.PI - 0.98, 1.96, 0.62, 1.2), M.amber, 0, 0.1);
    helmet.add(visor);
    for (const z of [0.15, -0.15]) helmet.add(mesh(rbox(0.07, 0.03, 0.02, 0.008), M.energy, 0.02, 0.17, z));
    // Survival mask: covers the lower face and both ears; eyes stay visible
    const mask = group(); head.add(mask);
    mask.add(mesh(new THREE.SphereGeometry(0.152, 24, 12, 0, Math.PI * 2, Math.PI * 0.55, Math.PI * 0.45), M.base, 0, 0.1));
    for (const z of [0.148, -0.148]) {
      const ear = mesh(new THREE.CylinderGeometry(0.06, 0.06, 0.04, 16), M.trim, -0.01, 0.1, z); ear.rotation.x = Math.PI / 2; mask.add(ear);
    }
    for (const z of [0.03, -0.03]) mask.add(mesh(rbox(0.02, 0.018, 0.03, 0.006), M.energy, 0.148, 0.03, z));
    heads.helmet = helmet; heads.mask = mask; heads.hair = hair;
    // Protective collar the scarf is built into
    const ring = mesh(new THREE.TorusGeometry(0.17, 0.06, 10, 20), M.trim, 0.0, 0.62);
    ring.rotation.x = Math.PI / 2; spine.add(ring);
  }
  const collar = group(-0.2, 0.58, 0.16); spine.add(collar);

  const armN = limb(spine, M, 0.3, 0.29, 0.065, 0.3, true);
  const armF = limb(spine, M, 0.3, 0.29, 0.065, -0.3, true);
  armN.top.position.y = 0.53; armF.top.position.y = 0.53;
  for (const a of [armN, armF]) a.end.add(mesh(new THREE.SphereGeometry(0.07, 12, 10), M.under, 0, -0.03));
  const legN = limb(hips, M, 0.46, 0.46, 0.085, 0.13, false);
  const legF = limb(hips, M, 0.46, 0.46, 0.085, -0.13, false);
  for (const l of [legN, legF]) {
    l.top.add(mesh(rbox(0.2, 0.26, 0.2, 0.07), M.base, 0.02, -0.18));                  // thigh plate
    l.joint.add(mesh(rbox(0.19, 0.3, 0.18, 0.06), nova ? M.base : M.trim, 0.03, -0.24)); // shin guard
    l.end.add(mesh(rbox(0.26, 0.1, 0.16, 0.04), M.trim, 0.06, -0.02));                   // boot
  }
  // Marksman kit: skate blades kept subtle, just a fine gold line along each side of the sole;
  // light-booster jets under the boots, shown only while they fire
  const blades = [], jets = [];
  if (nova) {
    const jetMat = new THREE.MeshBasicMaterial({ color: 0xfff1c9, transparent: true, opacity: 0.85, blending: THREE.AdditiveBlending, depthWrite: false });
    for (const l of [legN, legF]) {
      const j = new THREE.Mesh(new THREE.ConeGeometry(0.07, 0.34, 10, 1, true), jetMat); j.rotation.z = Math.PI; j.position.set(0.04, -0.26, 0);
      j.visible = false; l.end.add(j); jets.push(j);
    }
    const bladeMat = new THREE.MeshStandardMaterial({ color: c.energy, emissive: c.energy, emissiveIntensity: 1.6, roughness: 0.3 });
    for (const l of [legN, legF]) for (const z of [0.083, -0.083]) {
      const b = mesh(rbox(0.28, 0.018, 0.01, 0.004), bladeMat, 0.06, -0.066, z); l.end.add(b); blades.push(b);
    }
  }

  const extra = {};
  if (nova) {
    // CP-07: the Sentinel Bracer, gun and shield in one device
    const bracer = group(0, -0.14, 0); armN.joint.add(bracer);
    bracer.add(mesh(rbox(0.16, 0.34, 0.2, 0.04), M.base, 0.02, 0));
    bracer.add(mesh(rbox(0.05, 0.3, 0.14, 0.02), M.trim, 0.1, 0));
    bracer.add(mesh(rbox(0.03, 0.22, 0.03, 0.01), M.energy, 0.12, -0.02, 0.06));
    const muzzle = mesh(new THREE.CylinderGeometry(0.045, 0.06, 0.08, 12), M.energy, 0.0, -0.2, 0); bracer.add(muzzle);
    extra.muzzle = muzzle;   // charge effects gather here
    const shield = group(0.14, -0.1, 0); bracer.add(shield);
    const plateMat = new THREE.MeshStandardMaterial({ color: 0xfff1d6, emissive: c.energy, emissiveIntensity: 1.6, transparent: true, opacity: 0.55, side: THREE.DoubleSide, roughness: 0.2 });
    const plate = new THREE.Mesh(new THREE.CircleGeometry(0.55, 6), plateMat); plate.rotation.y = Math.PI / 2; shield.add(plate);
    shield.scale.setScalar(0.001);
    extra.bracer = bracer; extra.shield = shield; extra.plateMat = plateMat;
    // Marksman kit: the loaded attachment, tinted to match the HUD
    const moduleMat = new THREE.MeshStandardMaterial({ color: ATTACH_LOOK.lance.tint, emissive: ATTACH_LOOK.lance.tint, emissiveIntensity: 2.2, roughness: 0.3 });
    const module = mesh(rbox(0.07, 0.1, 0.09, 0.02), moduleMat, 0.11, 0.09, 0); bracer.add(module);
    extra.module = module; extra.moduleMat = moduleMat;
    // Close range: hard light forms a faceted gauntlet over each fist, and a greave over the lead boot for
    // the air kick. Hidden until a strike needs it.
    const hardMat = new THREE.MeshStandardMaterial({ color: '#fff4d6', emissive: c.energy, emissiveIntensity: 2.6, transparent: true, opacity: 0.82, roughness: 0.15, flatShading: true });
    extra.gauntlets = [armN, armF].map(a => {
      const g = new THREE.Mesh(new THREE.IcosahedronGeometry(0.13, 0), hardMat); g.position.set(0.02, -0.05, 0); g.scale.set(1.1, 1.3, 1); g.visible = false; a.end.add(g); return g;
    });
    const greave = new THREE.Mesh(new THREE.IcosahedronGeometry(0.15, 0), hardMat); greave.position.set(0.08, -0.03, 0); greave.scale.set(1.7, 0.9, 1); greave.visible = false;
    legN.end.add(greave); extra.greave = greave; extra.hardMat = hardMat;
    // Back pack
    spine.add(mesh(rbox(0.14, 0.3, 0.3, 0.05), M.trim, -0.22, 0.4));
    extra.edges = { fistN: [armN.joint, armN.end, 0.55], fistF: [armF.joint, armF.end, 0.55], boot: [legN.joint, legN.end, 0.5] };
  } else {
    // Gauntlet multi-tools
    for (const a of [armN, armF]) {
      a.joint.add(mesh(rbox(0.15, 0.24, 0.16, 0.04), M.trim, 0.01, -0.15));
      a.joint.add(mesh(rbox(0.03, 0.2, 0.03, 0.01), M.energy, 0.09, -0.15));
    }
    // Hunter kit: hard-light combat blades that extend from both gauntlets past the fist
    const bladeGeo = new THREE.BoxGeometry(0.035, 0.62, 0.11);
    const bladeN = mesh(bladeGeo, M.energy, 0.02, -0.36, 0), bladeF = mesh(bladeGeo, M.energy, 0.02, -0.36, 0);
    armN.end.add(bladeN); armF.end.add(bladeF); bladeN.visible = bladeF.visible = false;
    extra.blade = bladeN; extra.bladeF = bladeF;
    // Staff: stowed diagonally on the back, drawn into the hands for staff moves and rifle shots
    const staffGeo = new THREE.CylinderGeometry(0.035, 0.035, 1.5, 10);
    const back = mesh(staffGeo, M.trim, -0.24, 0.42, 0); back.rotation.z = 0.9; spine.add(back);
    const bt1 = mesh(new THREE.ConeGeometry(0.06, 0.4, 4), M.energy, 0, 0.95, 0), bt2 = mesh(new THREE.ConeGeometry(0.06, 0.4, 4), M.energy, 0, -0.95, 0);
    bt2.rotation.z = Math.PI; bt1.scale.z = bt2.scale.z = 0.35; back.add(bt1); back.add(bt2); extra.backTips = [bt1, bt2];
    const hand = group(0, -0.02, 0); armN.end.add(hand);
    const hs = mesh(staffGeo, M.trim, 0, 0, 0); hs.rotation.z = Math.PI / 2; hand.add(hs);
    for (const s of [-0.75, 0.75]) hand.add(mesh(new THREE.SphereGeometry(0.05, 10, 8), M.energy, s, 0, 0));
    // Glaive edges on both ends (Hunter kit)
    const tipGeo = new THREE.ConeGeometry(0.07, 0.5, 4);
    const tipA = mesh(tipGeo, M.energy, 1.0, 0, 0), tipB = mesh(tipGeo, M.energy, -1.0, 0, 0);
    tipA.rotation.z = -Math.PI / 2; tipB.rotation.z = Math.PI / 2; tipA.scale.z = tipB.scale.z = 0.35;
    hand.add(tipA); hand.add(tipB); extra.glaive = [tipA, tipB];
    const staffTip = group(1.05, 0, 0); hand.add(staffTip); extra.staffTip = staffTip;   // rifle muzzle (front tip)
    const staffTail = group(-1.05, 0, 0); hand.add(staffTail);
    const bladeTipN = group(0.02, -0.67, 0), bladeTipF = group(0.02, -0.67, 0); armN.end.add(bladeTipN); armF.end.add(bladeTipF);
    // Weapon edges for swing trails: [base, tip, how far out from the base the trail starts (0-1)]
    extra.edges = { bladeN: [armN.end, bladeTipN, 0.15], bladeF: [armF.end, bladeTipF, 0.15], glaiveA: [hand, staffTip, 0.4], glaiveB: [hand, staffTail, 0.4] };
    hand.visible = false; extra.hand = hand;
    extra.backStaff = back; extra.handStaff = hand;
    // Utility belt
    hips.add(mesh(rbox(0.36, 0.07, 0.4, 0.03), M.under, 0, 0.1));
    for (const z of [-0.12, 0.12]) hips.add(mesh(rbox(0.08, 0.09, 0.07, 0.02), M.trim, 0.17, 0.08, z));
    // Hunter kit: two energy snares clipped to the belt
    extra.beltSnares = [];
    for (const z of [0.22, -0.22]) {
      const g = group(-0.03, 0.05, z); hips.add(g);
      const disc = mesh(new THREE.CylinderGeometry(0.085, 0.085, 0.035, 18), M.under); disc.rotation.x = Math.PI / 2; g.add(disc);
      const ring = mesh(new THREE.TorusGeometry(0.07, 0.012, 6, 18), M.energy, 0, 0, z > 0 ? 0.02 : -0.02); g.add(ring);
      extra.beltSnares.push(g);
    }
  }

  extra.blades = blades; extra.jets = jets;
  const rig = finishRig({ root, size, flip, body, hips, spine, head, collar, armN, armF, legN, legF, extra, mats: M, char: charId, heads });
  rig.setHead = mode => {
    if (nova || rig.headMode === mode) return;
    rig.headMode = mode; heads.helmet.visible = mode === 'helmet'; heads.mask.visible = mode === 'mask'; heads.hair.visible = mode !== 'helmet';
  };
  rig.setHead('helmet');
  return rig;
}

// The common tail of every rig: shadows, its state for the animation, and Echo's Veil (any rig can fade)
function finishRig(parts) {
  const { root, mats: M } = parts;
  root.traverse(o => { if (o.isMesh) o.receiveShadow = false; });
  const rig = { ...parts, phase: 0, cur: {}, scarf: null, headMode: null, yaw: 0, stretch: 0, lastVy: 0, wasGround: true, lastRocketT: 0, wasCrouch: false };
  rig.setHead = () => {};

  // Veil (Echo): the body turns glassy and a pale rim outlines it. Collected before render.js adds the
  // player-colour ring, so the ring stays solid for teammates.
  const SHIMMER = new THREE.Color('#e6f4ff');
  const cloakMats = new Set(); root.traverse(o => { if (o.isMesh) cloakMats.add(o.material); });
  const cloakBase = [...cloakMats].map(m => ({ m, op: m.opacity, tr: m.transparent, energy: m === M.energy }));
  rig.cloak = 0;
  rig.setCloak = k => {
    k = Math.max(0, Math.min(1, k));
    if (k === rig.cloak) return;
    const flip = (rig.cloak > 0.001) !== (k > 0.001); rig.cloak = k;
    for (const b of cloakBase) {
      if (flip) { b.m.transparent = k > 0.001 || b.tr; b.m.needsUpdate = true; }
      b.m.opacity = b.op * (1 - (b.energy ? 0.55 : 0.88) * k);
      const r = b.m.userData.rim;
      if (r) {
        r.rimStrength.value = b.m.userData.rimBase + 1.8 * k; r.rimAlpha.value = 0.95 * k;
        r.rimColor.value.copy(b.m.userData.rimCol).lerp(SHIMMER, k);
      }
    }
  };
  return rig;
}

// ---- RAM and Fix ----------------------------------------------------------------------------------
// The shared skeleton: hips, spine, head, two arms and two legs at the same joints as Nova and Echo, so every
// pose fits; `arm`/`leg` set limb radius and where the shoulders and hips sit across the body.
function skeleton(charId, M, { arm, leg, shoulderZ, hipZ, upperArm = 0.3, foreArm = 0.29 }) {
  const root = group(), size = group(), flip = group(), body = group();
  root.add(size); size.add(flip); flip.add(body);
  size.scale.setScalar(CHARS[charId].scale || 1);
  const hips = group(0, 0.95, 0); body.add(hips);
  const spine = group(0, 0.08, 0); hips.add(spine);
  const head = group(0, 0.66, 0); spine.add(head);
  const collar = group(-0.2, 0.58, 0.16); spine.add(collar);
  const armN = limb(spine, M, upperArm, foreArm, arm, shoulderZ, true), armF = limb(spine, M, upperArm, foreArm, arm, -shoulderZ, true);
  armN.top.position.y = 0.53; armF.top.position.y = 0.53;
  const legN = limb(hips, M, 0.46, 0.46, leg, hipZ, false), legF = limb(hips, M, 0.46, 0.46, leg, -hipZ, false);
  return { root, size, flip, body, hips, spine, head, collar, armN, armF, legN, legF };
}
const glowMat = (c, k = 2.4) => new THREE.MeshStandardMaterial({ color: c, emissive: c, emissiveIntensity: k, roughness: 0.3 });

function buildNewRig(charId) {
  const c = CHARS[charId], M = rimAll(mats(c)), extra = { blades: [], jets: [] };
  let S;
  if (charId === 'ram') {
    // RAM: a heavy frame (broad, deep chest, massive pauldrons, thick limbs), a small horned helmet sunk between
    // the shoulders, a reactor pack with exhaust stacks, hydraulic pistons on the shins and the right forearm
    S = skeleton(charId, M, { arm: 0.095, leg: 0.11, shoulderZ: 0.42, hipZ: 0.16, upperArm: 0.31 });
    const { hips, spine, head, armN, armF, legN, legF } = S;
    hips.add(mesh(rbox(0.44, 0.24, 0.56, 0.08), M.trim, 0, 0.02));
    hips.add(mesh(rbox(0.12, 0.22, 0.28, 0.04), M.base, 0.21, -0.04));
    spine.add(mesh(rbox(0.4, 0.36, 0.5, 0.1), M.under, 0, 0.18));
    spine.add(mesh(rbox(0.54, 0.5, 0.68, 0.13), M.base, 0.03, 0.45));                      // chest
    spine.add(mesh(rbox(0.44, 0.16, 0.58, 0.06), M.trim, 0.04, 0.2));                      // abdomen plate
    spine.add(mesh(rbox(0.38, 0.05, 0.02, 0.01), M.energy, 0.04, 0.52, 0.345));            // energy seams along the chest
    spine.add(mesh(rbox(0.3, 0.04, 0.02, 0.01), M.energy, 0.06, 0.4, 0.345));
    spine.add(mesh(rbox(0.32, 0.13, 0.46, 0.05), M.trim, -0.02, 0.66));                    // high collar
    // Reactor pack and its exhaust stacks
    spine.add(mesh(rbox(0.26, 0.52, 0.52, 0.08), M.trim, -0.36, 0.44));
    extra.stacks = [];
    for (const z of [0.15, -0.15]) {
      const st = mesh(new THREE.CylinderGeometry(0.06, 0.07, 0.34, 12), M.under, -0.42, 0.78, z); st.rotation.z = 0.35; spine.add(st);
      const rim = mesh(new THREE.TorusGeometry(0.062, 0.016, 6, 14), M.energy, -0.48, 0.94, z); rim.rotation.x = Math.PI / 2; rim.rotation.y = 0.35; spine.add(rim);
      extra.stacks.push(rim);
    }
    // Pauldrons
    for (const z of [0.42, -0.42]) {
      spine.add(mesh(rbox(0.46, 0.26, 0.34, 0.11), M.base, 0, 0.62, z));
      spine.add(mesh(rbox(0.36, 0.06, 0.36, 0.03), M.trim, 0, 0.76, z));
      spine.add(mesh(rbox(0.3, 0.035, 0.02, 0.01), M.energy, 0, 0.62, z + Math.sign(z) * 0.172));
    }
    // Helmet: a heavy dark dome thrust forward between the pauldrons (head down, like a ram about to charge),
    // a visor slit glowing round to the sides, a jaw guard, and curled ram's horns
    head.position.set(0.13, 0.74, 0);
    head.add(mesh(new THREE.SphereGeometry(0.165, 20, 16), M.trim, 0, 0.06));
    head.add(mesh(rbox(0.22, 0.13, 0.27, 0.05), M.base, 0.07, -0.04));
    head.add(mesh(rbox(0.05, 0.04, 0.24, 0.012), M.energy, 0.15, 0.065));
    for (const z of [0.152, -0.152]) head.add(mesh(rbox(0.15, 0.036, 0.022, 0.01), M.energy, 0.07, 0.065, z));
    for (const z of [0.15, -0.15]) {
      const horn = mesh(new THREE.TorusGeometry(0.1, 0.038, 8, 20, Math.PI * 1.45), M.base, -0.05, 0.08, z);
      horn.rotation.set(0, 0, 0.9); horn.scale.set(1, 1, 1.3); head.add(horn);
      head.add(mesh(new THREE.SphereGeometry(0.03, 8, 6), M.energy, 0.0, -0.01, z * 1.06));
    }
    // Arms: heavy gauntlets; the right forearm carries the hydraulic pistons of the Piston Punch
    for (const a of [armN, armF]) { a.joint.add(mesh(rbox(0.24, 0.3, 0.26, 0.06), M.base, 0.01, -0.15)); a.end.add(mesh(new THREE.SphereGeometry(0.11, 12, 10), M.under, 0.01, -0.04)); }
    for (const z of [0.07, -0.07]) {
      armF.joint.add(mesh(new THREE.CylinderGeometry(0.03, 0.03, 0.3, 10), M.under, -0.1, -0.14, z));
      armF.joint.add(mesh(new THREE.TorusGeometry(0.036, 0.012, 6, 12), M.energy, -0.1, -0.2, z));
    }
    // Legs: thick plates, piston-braced shins, broad boots
    extra.pistons = [];
    for (const l of [legN, legF]) {
      l.top.add(mesh(rbox(0.28, 0.32, 0.28, 0.08), M.base, 0.02, -0.18));
      l.joint.add(mesh(rbox(0.26, 0.34, 0.26, 0.07), M.base, 0.04, -0.24));
      const pis = mesh(new THREE.CylinderGeometry(0.035, 0.035, 0.32, 10), M.trim, -0.1, -0.22, 0); l.joint.add(pis);
      const ring = mesh(new THREE.TorusGeometry(0.04, 0.012, 6, 12), M.energy, -0.1, -0.32, 0); ring.rotation.x = Math.PI / 2; l.joint.add(ring);
      extra.pistons.push(ring);
      l.end.add(mesh(rbox(0.36, 0.14, 0.26, 0.05), M.trim, 0.07, -0.03));
    }
    // The Rampart: a tower shield posed by the animation (body space), not hung off an arm, so it can stay
    // upright in front of him. Its face is toward the camera; a hard-light frame and a ram's-head crest glow.
    const shield = group(0.45, 0.95, 0.42); S.body.add(shield);
    shield.add(mesh(rbox(0.7, 1.12, 0.1, 0.06), M.trim, 0, 0, -0.02));
    shield.add(mesh(rbox(0.6, 1.0, 0.1, 0.05), M.base, 0, 0, 0.02));
    for (const y of [0.5, -0.5]) shield.add(mesh(rbox(0.56, 0.04, 0.02, 0.01), M.energy, 0, y, 0.075));
    for (const x of [0.33, -0.33]) shield.add(mesh(rbox(0.035, 0.9, 0.02, 0.01), M.energy, x, 0, 0.06));
    const crest = group(0, 0.12, 0.08); shield.add(crest);
    crest.add(mesh(new THREE.CylinderGeometry(0.1, 0.1, 0.03, 18), M.energy)).rotation.x = Math.PI / 2;
    for (const sx of [1, -1]) { const h = mesh(new THREE.TorusGeometry(0.11, 0.024, 6, 16, Math.PI * 1.3), M.energy, sx * 0.1, 0.06, 0); h.rotation.z = sx > 0 ? -0.4 : Math.PI + 0.4; crest.add(h); }
    const edge = group(0.32, 0.56, 0); shield.add(edge);   // the leading top corner (swing trails)
    extra.shield = shield; extra.shieldEdge = edge;
    // The Breach Cannon on the right shoulder: it turns to the aim
    const cannon = group(-0.08, 0.84, -0.3); S.spine.add(cannon);
    cannon.add(mesh(rbox(0.26, 0.18, 0.2, 0.05), M.trim));
    const barrel = mesh(new THREE.CylinderGeometry(0.06, 0.075, 0.5, 14), M.under, 0.3, 0.02, 0); barrel.rotation.z = -Math.PI / 2; cannon.add(barrel);
    const band = mesh(new THREE.TorusGeometry(0.075, 0.018, 6, 14), M.energy, 0.42, 0.02, 0); band.rotation.y = Math.PI / 2; cannon.add(band);
    const muzzle = group(0.58, 0.02, 0); cannon.add(muzzle);
    extra.cannon = cannon; extra.muzzle = muzzle;
    extra.edges = { shield: [shield, edge, 0.2], fistF: [armF.joint, armF.end, 0.5], fistN: [armN.joint, armN.end, 0.5] };
  } else {
    // Fix: a lighter frame in a work vest, welding goggles pushed up on her forehead, a ponytail, a tool pack
    // with a folding crane arm, a rivet gun on her right forearm, a welder on her left hand and a big wrench
    S = skeleton(charId, M, { arm: 0.06, leg: 0.08, shoulderZ: 0.28, hipZ: 0.125 });
    const { hips, spine, head, armN, armF, legN, legF } = S;
    const hazard = glowMat('#ffd23f', 0.35), skin = new THREE.MeshStandardMaterial({ color: 0xd2ab90, roughness: 0.82 });
    const hair = new THREE.MeshStandardMaterial({ color: 0x5a2d22, roughness: 0.75 }), lens = new THREE.MeshStandardMaterial({ color: 0xffb547, emissive: 0xff8a1a, emissiveIntensity: 0.6, roughness: 0.15, metalness: 0.3 });
    hips.add(mesh(rbox(0.32, 0.18, 0.36, 0.06), M.trim, 0, 0.02));
    for (const [x, z] of [[0.12, 0.2], [-0.08, 0.21]]) hips.add(mesh(rbox(0.09, 0.11, 0.07, 0.02), z > 0.2 ? hazard : M.under, x, 0.0, z));   // tool pouches
    spine.add(mesh(rbox(0.28, 0.3, 0.32, 0.08), M.under, 0, 0.18));
    spine.add(mesh(rbox(0.34, 0.32, 0.42, 0.09), M.base, 0.02, 0.42));                      // work vest
    spine.add(mesh(rbox(0.05, 0.05, 0.3, 0.02), M.energy, 0.19, 0.44));
    spine.add(mesh(rbox(0.36, 0.035, 0.02, 0.01), hazard, 0.02, 0.32, 0.215));             // hazard stripe
    for (const z of [0.26, -0.26]) spine.add(mesh(rbox(0.2, 0.12, 0.17, 0.06), M.trim, 0, 0.55, z));
    // Head: face, hair with a ponytail, goggles up on the forehead
    head.add(mesh(new THREE.SphereGeometry(0.13, 22, 16), skin, 0, 0.1));
    const eye = new THREE.MeshStandardMaterial({ color: 0x1a1a20, roughness: 0.4 });
    for (const z of [0.042, -0.042]) head.add(mesh(new THREE.SphereGeometry(0.016, 8, 6), eye, 0.118, 0.115, z));
    const cap = mesh(new THREE.SphereGeometry(0.138, 22, 12, 0, Math.PI * 2, 0, Math.PI * 0.46), hair, -0.01, 0.105); cap.rotation.z = 0.3; head.add(cap);
    const tail = group(-0.12, 0.17, 0); head.add(tail);
    tail.add(mesh(new THREE.CapsuleGeometry(0.045, 0.2, 4, 10), hair, -0.03, -0.12)).rotation.z = 0.5;
    extra.ponytail = tail;
    const strap = mesh(new THREE.TorusGeometry(0.134, 0.012, 6, 24), M.trim, 0, 0.17); strap.rotation.x = Math.PI / 2; strap.rotation.y = 0.25; head.add(strap);
    for (const z of [0.05, -0.05]) {
      const gg = mesh(new THREE.CylinderGeometry(0.042, 0.042, 0.05, 14), M.trim, 0.1, 0.205, z); gg.rotation.z = Math.PI / 2 - 0.6; head.add(gg);
      const ln = mesh(new THREE.CylinderGeometry(0.034, 0.034, 0.012, 14), lens, 0.128, 0.225, z); ln.rotation.z = Math.PI / 2 - 0.6; head.add(ln);
    }
    // Tool pack and its folding crane arm (it unfolds when she builds)
    spine.add(mesh(rbox(0.18, 0.34, 0.32, 0.05), M.trim, -0.24, 0.42));
    spine.add(mesh(rbox(0.04, 0.3, 0.02, 0.01), hazard, -0.33, 0.42, 0.162));
    const crane = group(-0.26, 0.62, -0.08); spine.add(crane);
    crane.add(mesh(rbox(0.06, 0.26, 0.06, 0.02), M.under, 0, 0.12));
    const fore = group(0, 0.24, 0); crane.add(fore);
    fore.add(mesh(rbox(0.05, 0.22, 0.05, 0.02), hazard, 0, 0.1));
    const claw = group(0, 0.22, 0); fore.add(claw);
    for (const sx of [1, -1]) { const f = mesh(rbox(0.02, 0.08, 0.03, 0.008), M.energy, sx * 0.025, 0.04); f.rotation.z = -sx * 0.4; claw.add(f); }
    crane.rotation.z = 1.9; fore.rotation.z = -2.6;
    extra.crane = crane; extra.craneFore = fore;
    // Gloves; the rivet gun on the right forearm (it aims like Nova's bracer); a welder on the left hand
    for (const a of [armN, armF]) a.end.add(mesh(new THREE.SphereGeometry(0.068, 12, 10), M.under, 0, -0.03));
    const gun = group(0, -0.14, 0); armN.joint.add(gun);
    gun.add(mesh(rbox(0.14, 0.3, 0.17, 0.04), M.trim, 0.02, 0));
    gun.add(mesh(rbox(0.06, 0.12, 0.11, 0.02), hazard, 0.1, 0.05));
    const gbar = mesh(new THREE.CylinderGeometry(0.032, 0.04, 0.16, 12), M.under, 0.03, -0.2, 0); gun.add(gbar);
    const gmuz = group(0.03, -0.29, 0); gun.add(gmuz);
    extra.muzzle = gmuz; extra.gun = gun;
    const welder = group(0, -0.08, 0); armF.end.add(welder);
    welder.add(mesh(new THREE.CylinderGeometry(0.03, 0.05, 0.14, 10), M.trim, 0, -0.04, 0));
    const tipMat = glowMat(c.energy, 2.6);
    welder.add(mesh(new THREE.ConeGeometry(0.03, 0.08, 10), tipMat, 0, -0.14, 0)).rotation.z = Math.PI;
    const weldTip = group(0, -0.19, 0); welder.add(weldTip);
    extra.weldTip = weldTip; extra.tipMat = tipMat;
    // The wrench: held in the right hand for swings, slung on the pack otherwise
    const steel = new THREE.MeshStandardMaterial({ color: 0xb8c0ca, roughness: 0.35, metalness: 0.6 });
    const wrench = group(0, -0.03, 0); armN.end.add(wrench);
    wrench.add(mesh(rbox(0.05, 0.62, 0.05, 0.02), steel, 0, -0.27));
    wrench.add(mesh(rbox(0.06, 0.12, 0.06, 0.02), M.trim, 0, -0.02));
    const jaw = group(0, -0.62, 0); wrench.add(jaw);
    const ring = mesh(new THREE.TorusGeometry(0.085, 0.032, 8, 18, Math.PI * 1.45), steel, 0, 0, 0); ring.rotation.z = Math.PI * 0.27; jaw.add(ring);
    jaw.add(mesh(rbox(0.12, 0.03, 0.035, 0.01), M.energy, 0, -0.05, 0.04));
    const wtip = group(0, -0.09, 0); jaw.add(wtip);
    const slung = mesh(rbox(0.04, 0.5, 0.04, 0.02), steel, -0.31, 0.38, 0.1); slung.rotation.z = 0.75; spine.add(slung);
    extra.wrench = wrench; extra.slung = slung;
    extra.edges = { wrench: [wrench, wtip, 0.45] };
    for (const l of [legN, legF]) {
      l.top.add(mesh(rbox(0.18, 0.24, 0.18, 0.06), M.base, 0.02, -0.18));
      l.joint.add(mesh(rbox(0.17, 0.28, 0.17, 0.05), M.trim, 0.03, -0.24));
      l.end.add(mesh(rbox(0.25, 0.11, 0.16, 0.04), M.under, 0.06, -0.02));
      l.joint.add(mesh(rbox(0.02, 0.2, 0.02, 0.006), hazard, 0.12, -0.24, 0.07));
    }
  }
  return finishRig({ ...S, extra, mats: M, char: charId, heads: {} });
}
