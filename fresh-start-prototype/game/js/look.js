// The look (Version 13): what makes surfaces read like painted metal and gives each route its own mood.
//   LOOK: per route lighting: the key (sun), fill (sky) and rim lights, how much the environment reflects, the
//     exposure, and the colour grade (lift, gamma, gain, saturation, contrast, vignette).
//   buildEnvMaps: an environment map per route for image-based lighting (reflections and soft ambient): the
//     route's sky gradient with a bright key panel where the sun is, a cool fill panel opposite, and strips of the
//     route's neon, rendered once and prefiltered (PMREM) so rough surfaces get a soft blur and glossy ones a
//     sharp one. Armour then reflects its surroundings instead of looking flat.
//   GradeShader: the last full-screen pass: up to four screen-space shockwaves (a ring that bends the picture,
//     from the biggest hits), then the colour grade and vignette.
//   outline: an inverted-hull outline for character meshes (a back-face shell pushed out along the normals),
//     the comic-book edge that separates characters from busy backgrounds.
import * as THREE from 'three';

export const LOOK = {
  skyport: {
    key: 2.3, fill: 0.95, rim: 1.6, rimColor: 0xbfe6ff, env: 0.8, exposure: 0.98,
    lift: [0.0, 0.01, 0.025], gamma: [1.0, 1.0, 0.98], gain: [1.02, 1.0, 0.98], sat: 1.08, contrast: 1.06, vignette: 0.28,
    neon: [0x5fd8ff, 0xffffff], keyPanel: 0xfff1dc, fillPanel: 0x9fcfff,
  },
  foundry: {
    key: 2.4, fill: 0.85, rim: 1.9, rimColor: 0x8fb8ff, env: 0.75, exposure: 0.95,
    lift: [0.01, 0.0, 0.0], gamma: [0.99, 1.0, 1.02], gain: [1.03, 1.0, 0.97], sat: 1.06, contrast: 1.1, vignette: 0.34,
    neon: [0xff8a2a, 0x3fc8ff], keyPanel: 0xffc690, fillPanel: 0x5f86d0,
  },
  undercity: {
    key: 2.1, fill: 0.8, rim: 2.2, rimColor: 0xff5fd2, env: 0.75, exposure: 0.95,
    lift: [0.005, 0.0, 0.02], gamma: [1.0, 1.01, 0.99], gain: [1.0, 0.98, 1.03], sat: 1.06, contrast: 1.1, vignette: 0.4,
    neon: [0xff3fb4, 0x4fe0ff, 0xb070ff], keyPanel: 0xffb7c9, fillPanel: 0x6f7dff,
  },
};

// One environment map per route (built once, at load)
export function buildEnvMaps(renderer, ATMOS) {
  const pmrem = new THREE.PMREMGenerator(renderer);
  const out = {};
  for (const id of Object.keys(LOOK)) {
    const A = ATMOS[id], L = LOOK[id], scene = new THREE.Scene();
    const soft = hex => new THREE.Color(hex).lerp(new THREE.Color(0xe4e8ee), 0.45).multiplyScalar(1.25);   // the sky, softened and bright (it is most of the fill in shade)
    const sky = new THREE.Mesh(new THREE.SphereGeometry(50, 32, 16), new THREE.ShaderMaterial({
      side: THREE.BackSide, depthWrite: false,
      uniforms: { top: { value: soft(A.top) }, mid: { value: soft(A.mid) }, bot: { value: soft(A.bot) } },
      vertexShader: 'varying vec3 vP; void main(){ vP = normalize(position); gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
      // (the floor half darkens toward the nadir, so undersides pick up a grounded, darker bounce)
      fragmentShader: 'uniform vec3 top, mid, bot; varying vec3 vP; void main(){ float h = vP.y; vec3 c = h > 0.0 ? mix(mid, top, smoothstep(0.0, 0.7, h)) : mix(mid, bot * 0.6, smoothstep(0.0, -0.6, h)); gl_FragColor = vec4(c, 1.0); }',
    }));
    scene.add(sky);
    // (the key and fill panels are mostly white with a hint of the route's tint, so characters keep their own
    // colours and the route shows in the glints; the neon strips keep their full colour)
    const panel = (color, intensity, w, h, pos, tint = 1) => {
      const c = new THREE.Color(1, 1, 1).lerp(new THREE.Color(color), tint).multiplyScalar(intensity);
      const m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), new THREE.MeshBasicMaterial({ color: c, side: THREE.DoubleSide }));
      m.position.copy(pos); m.lookAt(0, 0, 0); scene.add(m);
    };
    // the key where the sun sits (above, behind the camera to the left), a dimmer fill opposite
    panel(L.keyPanel, 4.5, 26, 18, new THREE.Vector3(-22, 30, 26), 0.35);
    panel(L.fillPanel, 1.4, 30, 14, new THREE.Vector3(30, 8, -18), 0.35);
    // neon strips all round at mid height: they glint along armour edges
    L.neon.forEach((c, i) => {
      for (let k = 0; k < 4; k++) {
        const a = (k / 4 + i * 0.13) * Math.PI * 2;
        panel(c, 3, 1.2, 9 + (k % 2) * 6, new THREE.Vector3(Math.cos(a) * 40, 4 + (k % 2) * 6, Math.sin(a) * 40));
      }
    });
    out[id] = pmrem.fromScene(scene, 0.02).texture;
    scene.traverse(o => { if (o.geometry) o.geometry.dispose(); if (o.material) o.material.dispose(); });
  }
  pmrem.dispose();
  return out;
}

// The final pass: shockwaves, then the grade (in display space, after tone mapping)
export const GradeShader = {
  uniforms: {
    tDiffuse: { value: null }, res: { value: new THREE.Vector2(1280, 720) },
    lift: { value: new THREE.Vector3() }, gamma: { value: new THREE.Vector3(1, 1, 1) }, gain: { value: new THREE.Vector3(1, 1, 1) },
    sat: { value: 1 }, contrast: { value: 1 }, vignette: { value: 0.3 },
    waves: { value: [new THREE.Vector4(), new THREE.Vector4(), new THREE.Vector4(), new THREE.Vector4()] },   // x, y (uv), radius, strength
  },
  vertexShader: 'varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
  fragmentShader: `
    uniform sampler2D tDiffuse; uniform vec2 res; uniform vec3 lift, gamma, gain; uniform float sat, contrast, vignette;
    uniform vec4 waves[4];
    varying vec2 vUv;
    void main() {
      vec2 uv = vUv, asp = vec2(res.x / res.y, 1.0);
      float glint = 0.0;
      for (int i = 0; i < 4; i++) {
        vec4 w = waves[i];
        if (w.w <= 0.0) continue;
        vec2 d = (uv - w.xy) * asp; float r = length(d);
        float band = exp(-pow((r - w.z) / 0.035, 2.0));   // a thin ring at the wave's radius
        uv -= normalize(d + 1e-5) / asp * band * w.w * 0.03;
        glint += band * w.w;
      }
      vec3 c = texture2D(tDiffuse, uv).rgb + glint * 0.06;
      c = pow(max(c * gain + lift * (1.0 - c), 0.0), 1.0 / gamma);
      float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
      c = mix(vec3(l), c, sat);
      c = (c - 0.5) * contrast + 0.5;
      vec2 v = (vUv - 0.5) * asp;
      c *= 1.0 - vignette * smoothstep(0.35, 1.0, length(v));
      gl_FragColor = vec4(clamp(c, 0.0, 1.0), 1.0);
    }`,
};

// Inverted-hull outline: a back-face shell of the mesh, pushed out along its normals (`width` m), in a dark
// shade. One material per colour, shared (and compiled once).
// (The width is a uniform, so every outline shares one shader program whatever its width.)
const outlineMats = new Map();
export function outlineMaterial(color = 0x0b0f18, width = 0.022) {
  const key = color + ':' + width.toFixed(5);
  if (outlineMats.has(key)) return outlineMats.get(key);
  const m = new THREE.MeshBasicMaterial({ color, side: THREE.BackSide });
  const u = { outlineW: { value: width } };
  m.onBeforeCompile = s => {
    s.uniforms.outlineW = u.outlineW;
    s.vertexShader = 'uniform float outlineW;\n' + s.vertexShader.replace('#include <begin_vertex>', '#include <begin_vertex>\n transformed += normalize(normal) * outlineW;');
  };
  m.customProgramCacheKey = () => 'outline';
  m.userData.sharedOutline = true;
  outlineMats.set(key, m);
  return m;
}
// Outlines on or off everywhere at once (off on Low quality: they add a draw for every body part)
export function showOutlines(on) { for (const m of outlineMats.values()) m.visible = on; }
// Gives every visible, shadow-casting mesh under `root` an outline shell (skips glows, sprites and see-through
// parts); returns the shells. (The width is in each mesh's own space, so a rig drawn bigger gets a bolder line.)
export function addOutlines(root, color, width) {
  const mat = outlineMaterial(color, width), add = [];
  root.traverse(o => {
    if (!o.isMesh || o.userData.outline || o.userData.noOutline || !o.castShadow) return;
    const m = o.material;
    if (!m || m.transparent || (m.emissiveIntensity && m.emissiveIntensity > 1) || m.isMeshBasicMaterial) return;
    add.push(o);
  });
  return add.map(o => {
    const shell = new THREE.Mesh(o.geometry, mat); shell.userData.outline = true; shell.castShadow = false;
    shell.raycast = () => {}; o.add(shell); return shell;
  });
}

// ---- Surface detail: procedural texture sets for the level (colour detail, relief and roughness) ----
// Each set is drawn once on canvases: a height field (panel seams, bolts, grating, wear and grain), turned into
// a normal map by its slopes, a light detail map the material's colour tints, and a roughness map (worn edges
// shine, grime is dull). Level geometry is baked in world space, so its UVs are projected from the world
// (worldUV) and the detail tiles evenly across every box and round every curve.
let seed = 777; const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
function canvas(n) { const c = document.createElement('canvas'); c.width = c.height = n; return c; }
function heightToNormal(h, n, strength) {
  const src = h.getContext('2d').getImageData(0, 0, n, n).data, out = canvas(n), g = out.getContext('2d'), img = g.createImageData(n, n);
  const H = (x, y) => src[(((y + n) % n) * n + ((x + n) % n)) * 4] / 255;
  for (let y = 0; y < n; y++) for (let x = 0; x < n; x++) {
    const dx = (H(x + 1, y) - H(x - 1, y)) * strength, dy = (H(x, y + 1) - H(x, y - 1)) * strength;
    const l = Math.hypot(dx, dy, 1), i = (y * n + x) * 4;
    img.data[i] = (-dx / l * 0.5 + 0.5) * 255; img.data[i + 1] = (dy / l * 0.5 + 0.5) * 255; img.data[i + 2] = (1 / l * 0.5 + 0.5) * 255; img.data[i + 3] = 255;
  }
  g.putImageData(img, 0, 0); return out;
}
function grain(g, n, count, alpha, light) {
  for (let i = 0; i < count; i++) {
    const v = light ? 200 + rnd() * 55 : rnd() * 70;
    g.fillStyle = `rgba(${v},${v},${v},${alpha * rnd()})`; g.fillRect(rnd() * n, rnd() * n, 1 + rnd() * 3, 1 + rnd() * 3);
  }
}
// kind: 'panel' (riveted wall plating), 'floor' (deck plates with a grip pattern), 'concrete' (poured, worn)
function drawSurface(kind, n) {
  const h = canvas(n), hg = h.getContext('2d'), d = canvas(n), dg = d.getContext('2d'), r = canvas(n), rg = r.getContext('2d');
  hg.fillStyle = '#808080'; hg.fillRect(0, 0, n, n);
  dg.fillStyle = '#ffffff'; dg.fillRect(0, 0, n, n);
  rg.fillStyle = '#909090'; rg.fillRect(0, 0, n, n);   // roughness ~0.56; the material's roughness scales it
  const cells = kind === 'concrete' ? 2 : 2, cs = n / cells;
  for (let cy = 0; cy < cells; cy++) for (let cx = 0; cx < cells; cx++) {
    const x0 = cx * cs, y0 = cy * cs, tone = 236 + rnd() * 19;
    dg.fillStyle = `rgb(${tone},${tone},${tone})`; dg.fillRect(x0 + 2, y0 + 2, cs - 4, cs - 4);
    if (kind !== 'concrete') {
      // a raised plate with a bevelled rim and a recessed seam round it
      hg.fillStyle = '#9a9a9a'; hg.fillRect(x0 + 6, y0 + 6, cs - 12, cs - 12);
      hg.fillStyle = '#8d8d8d'; hg.fillRect(x0 + 10, y0 + 10, cs - 20, cs - 20);
      hg.fillStyle = '#3a3a3a'; hg.fillRect(x0, y0, cs, 3); hg.fillRect(x0, y0, 3, cs);
      dg.fillStyle = 'rgba(40,46,56,0.55)'; dg.fillRect(x0, y0, cs, 3); dg.fillRect(x0, y0, 3, cs);
      // bolts in the corners
      for (const [bx, by] of [[14, 14], [cs - 14, 14], [14, cs - 14], [cs - 14, cs - 14]]) {
        const grd = hg.createRadialGradient(x0 + bx, y0 + by, 0, x0 + bx, y0 + by, 5);
        grd.addColorStop(0, '#e0e0e0'); grd.addColorStop(1, '#8d8d8d'); hg.fillStyle = grd; hg.beginPath(); hg.arc(x0 + bx, y0 + by, 5, 0, 7); hg.fill();
        dg.fillStyle = 'rgba(90,96,110,0.6)'; dg.beginPath(); dg.arc(x0 + bx, y0 + by, 3.5, 0, 7); dg.fill();
        rg.fillStyle = '#505050'; rg.beginPath(); rg.arc(x0 + bx, y0 + by, 4, 0, 7); rg.fill();
      }
      if (kind === 'floor') {
        // diamond grip pattern on the plates
        hg.fillStyle = 'rgba(200,200,200,0.35)';
        for (let yy = y0 + 22; yy < y0 + cs - 22; yy += 12) for (let xx = x0 + 22 + ((yy / 12) % 2) * 6; xx < x0 + cs - 22; xx += 12) {
          hg.save(); hg.translate(xx, yy); hg.rotate(Math.PI / 4); hg.fillRect(-3.5, -1.2, 7, 2.4); hg.restore();
        }
      } else if (rnd() < 0.5) {
        // a vent slot on some wall plates
        const vx = x0 + cs * 0.3, vy = y0 + cs * 0.62;
        for (let k = 0; k < 5; k++) { hg.fillStyle = '#4a4a4a'; hg.fillRect(vx, vy + k * 7, cs * 0.4, 3); dg.fillStyle = 'rgba(30,34,42,0.5)'; dg.fillRect(vx, vy + k * 7, cs * 0.4, 3); }
      }
    } else {
      hg.fillStyle = '#6a6a6a'; hg.fillRect(x0, y0, cs, 2); hg.fillRect(x0, y0, 2, cs);   // form-work seams
      dg.fillStyle = 'rgba(60,60,70,0.35)'; dg.fillRect(x0, y0, cs, 2); dg.fillRect(x0, y0, 2, cs);
    }
  }
  // grime, wear and grain over everything
  for (let i = 0; i < (kind === 'concrete' ? 26 : 14); i++) {
    const x = rnd() * n, y = rnd() * n, rad = 20 + rnd() * 70, grd = dg.createRadialGradient(x, y, 0, x, y, rad);
    grd.addColorStop(0, `rgba(70,74,84,${0.08 + rnd() * 0.1})`); grd.addColorStop(1, 'rgba(70,74,84,0)'); dg.fillStyle = grd; dg.fillRect(x - rad, y - rad, rad * 2, rad * 2);
    const rr = rg.createRadialGradient(x, y, 0, x, y, rad); rr.addColorStop(0, 'rgba(220,220,220,0.4)'); rr.addColorStop(1, 'rgba(220,220,220,0)'); rg.fillStyle = rr; rg.fillRect(x - rad, y - rad, rad * 2, rad * 2);
  }
  grain(hg, n, kind === 'concrete' ? 9000 : 3000, 0.25, rnd() < 0.5); grain(dg, n, 4000, 0.08, false);
  if (kind === 'concrete') for (let i = 0; i < 6; i++) {   // hairline cracks
    hg.strokeStyle = 'rgba(60,60,60,0.7)'; hg.lineWidth = 1; hg.beginPath(); let x = rnd() * n, y = rnd() * n; hg.moveTo(x, y);
    for (let k = 0; k < 8; k++) { x += (rnd() - 0.5) * 40; y += (rnd() - 0.5) * 40; hg.lineTo(x, y); } hg.stroke();
  }
  const tex = (c, srgb) => { const t = new THREE.CanvasTexture(c); t.wrapS = t.wrapT = THREE.RepeatWrapping; t.anisotropy = 8; if (srgb) t.colorSpace = THREE.SRGBColorSpace; return t; };
  return { map: tex(d, true), normalMap: tex(heightToNormal(h, n, kind === 'concrete' ? 3 : 6), false), roughnessMap: tex(r, false) };
}
const sets = {};
export function surface(kind) { return sets[kind] || (sets[kind] = drawSurface(kind, 512)); }
const surfaced = new Set();
// Low quality keeps the colour detail but drops the relief and roughness maps (two texture reads per pixel)
export function surfaceRelief(on) {
  for (const m of surfaced) {
    const S = surface(m.userData.surfaceKind), nm = on ? S.normalMap : null;
    if (m.normalMap !== nm) { m.normalMap = nm; m.roughnessMap = on ? S.roughnessMap : null; m.needsUpdate = true; }
  }
}
// Gives a level material a surface set, tiled every `tile` m in world space
export function applySurface(mat, kind, tile = 2.5, normal = 0.7) {
  const S = surface(kind);
  mat.map = S.map; mat.normalMap = S.normalMap; mat.roughnessMap = S.roughnessMap;
  surfaced.add(mat);
  mat.normalScale = new THREE.Vector2(normal, normal); mat.userData.worldUV = tile; mat.userData.surfaceKind = kind; mat.needsUpdate = true;
  return mat;
}
// Box-projected UVs from world positions (for baked geometry, whose positions are already in world space)
export function worldUVs(geo, tile) {
  const p = geo.attributes.position, nrm = geo.attributes.normal, uv = new Float32Array(p.count * 2);
  for (let i = 0; i < p.count; i++) {
    const ax = Math.abs(nrm.getX(i)), ay = Math.abs(nrm.getY(i)), az = Math.abs(nrm.getZ(i));
    let u, v;
    if (ay >= ax && ay >= az) { u = p.getX(i); v = p.getZ(i); } else if (ax >= az) { u = p.getZ(i); v = p.getY(i); } else { u = p.getX(i); v = p.getY(i); }
    uv[i * 2] = u / tile; uv[i * 2 + 1] = v / tile;
  }
  geo.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
}
