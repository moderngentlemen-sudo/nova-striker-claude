// Third-person cameras (Version 13). Every person playing has a camera of their own that orbits their character,
// in the manner of a third-person action platformer: the mouse (pointer lock) or the right stick turns it, it
// pulls in rather than pass through a wall, and while its player is locked on it eases round to keep the target
// in view (until the player turns it themselves). The camera is also how they play:
//   command(cmd): the move stick becomes a move along the ground relative to the camera (sim mx, mz), and the aim
//     runs from the character's chest to whatever sits under the crosshair at the centre of the view (cmd.camAim;
//     the sim pulls it onto an enemy within cmd.assist radians of it).
//   Looking well up counts as "up" for the moves that used to read up on the stick (a rising attack, an upward
//     dash); the crouch button (C, or holding L3) is "down".
// TeamCam is the classic side-on view of the whole team, for the spare quarter of a three-player split screen.
import * as THREE from 'three';
import { BOXES, rayBoxT, rayCast, pathFrame } from './level.js';
import { toWorldZ, simDir, toSim } from './space.js';
import { hurtbox } from './combat.js';
import { chest } from './player.js';
import { SETTINGS } from './config.js';

export const CAM = {
  dist: 5.4,          // m behind the pivot
  height: 1.5,        // the pivot, above the feet
  shoulder: 0.55,     // m to the right: an over-the-shoulder view, so the crosshair is never on the character
  pitch0: -0.2,       // a fresh camera looks slightly down
  pitchMin: -1.2, pitchMax: 0.85,
  near: 0.75,         // the closest it pulls in to the pivot
  lockEase: 3.2,      // how fast it turns to a locked-on target (per second)
  lockWait: 0.6,      // s after the player last turned it before it starts helping again
  autoFollow: 0.35,   // an automatic lock-on is followed only within about 70 degrees of the view
  upPitch: 0.62,      // looking up more than this counts as holding up
  aimRange: 90,
  assistPad: 0.16, assistMouse: 0.035,   // radians of aim assist round the crosshair
};

const wrap = a => Math.atan2(Math.sin(a), Math.cos(a));
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));

export class PlayerCam {
  constructor(p) {
    this.p = p;
    this.cam = new THREE.PerspectiveCamera(SETTINGS.fov, 16 / 9, 0.1, 700);
    this.cam.rotation.order = 'YXZ';
    this.yaw = null; this.pitch = CAM.pitch0; this.dist = CAM.dist; this.cur = CAM.dist; this.lookT = 99;
    this.pivot = new THREE.Vector3(); this.eye = new THREE.Vector3(); this.ready = false; this.close = false;
    this.fwd = new THREE.Vector3(); this.right = new THREE.Vector3();
    this.v = new THREE.Vector3(); this.w = new THREE.Vector3(); this.off = new THREE.Vector3();
    this.vp = { x: 0, y: 0, w: 1, h: 1 };
  }
  // Facing straight along the path at sim x
  reset(x) { const f = pathFrame(x); this.yaw = Math.atan2(-f.tx, -f.tz); this.pitch = CAM.pitch0; }
  turn(dyaw, dpitch) {
    if (!dyaw && !dpitch) return;
    if (this.yaw === null) this.reset(this.p.x);
    this.yaw = wrap(this.yaw - dyaw); this.pitch = clamp(this.pitch + dpitch, CAM.pitchMin, CAM.pitchMax); this.lookT = 0;
  }
  // The view's forward (three.js: rotation.y = yaw, rotation.x = pitch) and right vectors in the world
  dirs() {
    const cy = Math.cos(this.yaw), sy = Math.sin(this.yaw), cp = Math.cos(this.pitch), sp = Math.sin(this.pitch);
    this.fwd.set(-sy * cp, sp, -cy * cp); this.right.set(cy, 0, -sy);
  }

  update(world, dt, alpha, shake = 0, punch = 0) {
    const p = this.p, a = alpha;
    const x = p.prevX + (p.x - p.prevX) * a, y = p.prevY + (p.y - p.prevY) * a, z = (p.prevZ ?? p.z) + (p.z - (p.prevZ ?? p.z)) * a;
    const target = toWorldZ(x, y + CAM.height, z, this.v);
    // A jump in position (a recall, a respawn, a checkpoint) takes the camera with it
    if (!this.ready || this.yaw === null || target.distanceTo(this.pivot) > 12) { this.pivot.copy(target); if (this.yaw === null || this.ready) this.reset(x); this.ready = true; }
    else {
      const kxz = 1 - Math.exp(-dt * 18), dy = target.y - this.pivot.y, ky = 1 - Math.exp(-dt * (7 + Math.max(0, Math.abs(dy) - 1) * 6));
      this.pivot.x += (target.x - this.pivot.x) * kxz; this.pivot.z += (target.z - this.pivot.z) * kxz; this.pivot.y += dy * ky;
    }
    this.lookT += dt;
    // Locked on: ease round to keep the target in view, unless the player is turning the camera themselves. A
    // target the player picked is followed wherever it goes; one automatic lock-on picked only while it is
    // already in front of the camera (it never swings the view round to something behind)
    const t = p.lockT;
    if (t && !t.dead && this.lookT > CAM.lockWait && p.state !== 'dead') {
      const tw = toWorldZ(t.x, t.y + t.h * 0.5, t.z, this.w), dx = tw.x - this.pivot.x, dz = tw.z - this.pivot.z, hd = Math.hypot(dx, dz);
      const ahead = hd > 1e-3 ? (dx * -Math.sin(this.yaw) + dz * -Math.cos(this.yaw)) / hd : 1;
      if (hd > 1.2 && (p.lockPicked || ahead > CAM.autoFollow)) {
        const k = 1 - Math.exp(-dt * CAM.lockEase);
        this.yaw = wrap(this.yaw + wrap(Math.atan2(-dx, -dz) - this.yaw) * k);
        const wantP = clamp(Math.atan2(tw.y - this.pivot.y, hd) - 0.12, -0.55, 0.3);
        this.pitch += (wantP - this.pitch) * k * 0.5;
      }
    }
    // An ultimate's call pushes in on whoever is calling it; RAM's camera sits a little further back
    const U = world.ultCast;
    let want = CAM.dist + (p.char === 'ram' ? 0.7 : 0);
    if (U && U.phase === 'cast' && U.members.includes(p)) want *= 0.62;
    this.dist += (want - this.dist) * (1 - Math.exp(-dt * 5));
    this.dirs();
    // Behind the pivot along the view, over the right shoulder; pulled in at once by a wall, easing back out
    const off = this.off.copy(this.fwd).multiplyScalar(-this.dist).addScaledVector(this.right, CAM.shoulder);
    const L = off.length(), free = this.clearance(x, y + CAM.height, z, off, L);
    const room = Math.max(CAM.near, free - 0.3);
    this.cur = room < this.cur ? room : this.cur + (room - this.cur) * (1 - Math.exp(-dt * 3));
    this.close = this.cur < 1.1;
    this.eye.copy(this.pivot).addScaledVector(off, this.cur / L);
    const c = this.cam;
    c.position.copy(this.eye); c.position.y += punch;
    if (shake > 0) { c.position.x += (Math.random() - 0.5) * shake; c.position.y += (Math.random() - 0.5) * shake; c.position.z += (Math.random() - 0.5) * shake; }
    c.rotation.set(this.pitch, this.yaw, 0);
    if (c.fov !== SETTINGS.fov) { c.fov = SETTINGS.fov; c.updateProjectionMatrix(); }
  }
  // How far the camera can sit from the pivot along `off` before something solid is in the way. Only walls,
  // floors and ceilings count: the camera passes through rails (waist-high), gates, breakable pieces and anything
  // thin (a pillar, a column) rather than lurch in and out behind them; nor does a box the pivot is inside count.
  clearance(x, y, z, off, L) {
    const [dx, dy, dz] = simDir(x, off), m = Math.hypot(dx, dy, dz) || 1;
    let best = L;
    for (const b of BOXES) {
      if (b.type !== 's' || b.tag === 'rail' || Math.min(b.x1 - b.x0, b.z1 - b.z0) < 1.6) continue;
      if (x > b.x0 && x < b.x1 && y > b.y0 && y < b.y1 && z > b.z0 && z < b.z1) continue;
      const h = rayBoxT(x, y, z, dx / m, dy / m, dz / m, b);
      if (h && h.t < best) best = h.t;
    }
    return best;
  }

  // Fills in a person's command from their camera: a camera-relative move and the crosshair's aim
  command(cmd, world) {
    const p = this.p;
    if (this.yaw === null) this.reset(p.x);
    this.dirs();
    const sx = cmd.sx || 0, sy = cmd.sy || 0;
    if (sx || sy) {
      const fh = Math.hypot(this.fwd.x, this.fwd.z) || 1;
      this.v.set(this.fwd.x / fh * sy + this.right.x * sx, 0, this.fwd.z / fh * sy + this.right.z * sx);
      const [dx, , dz] = simDir(p.x, this.v);
      cmd.mx = dx; cmd.mz = dz;
    } else { cmd.mx = 0; cmd.mz = 0; }
    // Aim: from the chest at what is under the crosshair. The ray starts level with the character, so nothing
    // between the camera and them counts.
    // (the eye is worked out from where he is this tick, not where the drawn camera has smoothed to)
    const off = this.off.copy(this.fwd).multiplyScalar(-this.dist).addScaledVector(this.right, CAM.shoulder);
    const eye = toWorldZ(p.x, p.y + CAM.height, p.z, this.w).addScaledVector(off, Math.min(this.cur, this.dist) / off.length());
    const c = chest(p), O = toSim(eye, p.x), D = simDir(p.x, this.fwd), dm = Math.hypot(D[0], D[1], D[2]) || 1;
    const d0 = D[0] / dm, d1 = D[1] / dm, d2 = D[2] / dm;
    const t0 = Math.max(0, (c.x - O.x) * d0 + (c.y - O.y) * d1 + (c.z - O.z) * d2);
    const ox = O.x + d0 * t0, oy = O.y + d1 * t0, oz = O.z + d2 * t0;
    let t = rayCast(ox, oy, oz, d0, d1, d2, CAM.aimRange).t;
    for (const e of world.enemies) {
      if (e.dead) continue;
      const h = rayBoxT(ox, oy, oz, d0, d1, d2, hurtbox(e));
      if (h && h.t < t) t = h.t;
    }
    let ax = ox + d0 * t - c.x, ay = oy + d1 * t - c.y, az = oz + d2 * t - c.z;
    const am = Math.hypot(ax, ay, az);
    if (am < 1.5) { ax = d0; ay = d1; az = d2; } else { ax /= am; ay /= am; az /= am; }
    Object.assign(cmd, { ax, ay, az, aimFree: true, camAim: true, assist: p.device === 'kbm' ? CAM.assistMouse : SETTINGS.aimAssist ? CAM.assistPad : 0 });
    cmd.my = cmd.crouch ? -1 : this.pitch > CAM.upPitch ? 1 : 0;
    return cmd;
  }
}

// The whole team from the side, as the game used to look (the spare quarter of a three-way split)
export class TeamCam {
  constructor() { this.cam = new THREE.PerspectiveCamera(34, 16 / 9, 0.5, 700); this.at = null; this.vp = { x: 0, y: 0, w: 1, h: 1 }; this.close = false; this.p = null; }
  update(world, dt) {
    const C = world.cam, f = pathFrame(C.x);
    const want = new THREE.Vector3(f.px + f.nx * (C.z || 0), C.y, f.pz + f.nz * (C.z || 0));
    if (!this.at) this.at = want.clone(); else this.at.lerp(want, 1 - Math.exp(-dt * 4));
    this.cam.position.set(this.at.x + f.nx * 22, this.at.y + 3, this.at.z + f.nz * 22);
    this.cam.lookAt(this.at);
  }
}

// Split-screen layout for n people: whole, side by side, or quarters (CSS px, top-left origin)
export function layout(n, W, H) {
  const hw = Math.floor(W / 2), hh = Math.floor(H / 2);
  if (n <= 1) return [{ x: 0, y: 0, w: W, h: H }];
  if (n === 2) return [{ x: 0, y: 0, w: hw, h: H }, { x: W - hw, y: 0, w: hw, h: H }];
  return [{ x: 0, y: 0, w: hw, h: hh }, { x: W - hw, y: 0, w: hw, h: hh }, { x: 0, y: H - hh, w: hw, h: hh }, { x: W - hw, y: H - hh, w: hw, h: hh }];
}
