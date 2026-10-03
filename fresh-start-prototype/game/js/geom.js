// Small vector helpers for the 3D simulation. Sim space: x along the path, y up, z across the path. Bodies face a
// horizontal unit vector (facing, facingZ); aims and shots are 3D unit vectors.

export const sign = v => (v > 0 ? 1 : v < 0 ? -1 : 0);
export const approach = (v, t, d) => (v < t ? Math.min(v + d, t) : Math.max(v - d, t));

// Turn something to face a horizontal direction (ignored when the direction is too short to read)
export function setFacing(o, dx, dz) {
  const m = Math.hypot(dx, dz || 0);
  if (m < 1e-4) return false;
  o.facing = dx / m; o.facingZ = (dz || 0) / m;
  return true;
}
export const fz = o => o.facingZ || 0;

// The horizontal unit vector from a to b ({x, z}); when they stand on top of each other, `fb`'s facing (or +x)
export function away(a, b, fb = null) {
  const dx = b.x - a.x, dz = (b.z || 0) - (a.z || 0), m = Math.hypot(dx, dz);
  if (m > 1e-4) return { x: dx / m, z: dz / m };
  if (fb) return { x: fb.facing || 1, z: fb.facingZ || 0 };
  return { x: 1, z: 0 };
}
export const hdist = (a, b) => Math.hypot(b.x - a.x, (b.z || 0) - (a.z || 0));

// A vector approach on the horizontal plane: (vx, vz) toward (tx, tz) by at most d
export function approach2(o, tx, tz, d) {
  const ex = tx - o.vx, ez = tz - (o.vz || 0), m = Math.hypot(ex, ez);
  if (m <= d) { o.vx = tx; o.vz = tz; } else { o.vx += ex / m * d; o.vz = (o.vz || 0) + ez / m * d; }
}

// Turns a velocity toward a direction by at most maxAng radians, keeping its speed
export function turnToward(vx, vy, vz, tx, ty, tz, maxAng) {
  const sp = Math.hypot(vx, vy, vz) || 1, tm = Math.hypot(tx, ty, tz) || 1;
  let ax = vx / sp, ay = vy / sp, az = vz / sp; const bx = tx / tm, by = ty / tm, bz = tz / tm;
  const dot = Math.max(-1, Math.min(1, ax * bx + ay * by + az * bz)), ang = Math.acos(dot);
  if (ang <= maxAng || ang < 1e-6) return [bx * sp, by * sp, bz * sp];
  // the part of b perpendicular to a, then rotate a toward it by maxAng
  let px = bx - ax * dot, py = by - ay * dot, pz = bz - az * dot; const pm = Math.hypot(px, py, pz);
  if (pm < 1e-6) { px = -az; py = 0; pz = ax; } else { px /= pm; py /= pm; pz /= pm; }
  const c = Math.cos(maxAng), s = Math.sin(maxAng);
  return [(ax * c + px * s) * sp, (ay * c + py * s) * sp, (az * c + pz * s) * sp];
}

// Rotates a horizontal direction (x, z) by angle a about the vertical
export function yawRot(x, z, a) { const c = Math.cos(a), s = Math.sin(a); return [x * c - z * s, x * s + z * c]; }

// A direction with its horizontal part turned by `yaw` and its pitch raised by `pitch` (fans and spreads)
export function spreadDir(dx, dy, dz, yaw, pitch = 0) {
  const h = Math.hypot(dx, dz);
  let [hx, hz] = h > 1e-6 ? [dx / h, dz / h] : [1, 0];
  [hx, hz] = yawRot(hx, hz, yaw);
  const el = Math.atan2(dy, h) + pitch;
  return [Math.cos(el) * hx, Math.sin(el), Math.cos(el) * hz];
}

// A strike's box in front of something: its centre `off` m along the facing, `len` m long along it and `depth` m
// wide across it (the box that holds that rectangle turned to the facing), from y0 to y1 above the feet
export function fwdBox(o, off, len, y0, y1, depth = len) {
  const fx = o.facing, fzz = fz(o), cx = o.x + fx * off, cz = (o.z || 0) + fzz * off;
  const hx = Math.abs(fx) * len / 2 + Math.abs(fzz) * depth / 2, hz = Math.abs(fzz) * len / 2 + Math.abs(fx) * depth / 2;
  return { x0: cx - hx, x1: cx + hx, z0: cz - hz, z1: cz + hz, y0: o.y + y0, y1: o.y + y1, cx: o.x, cz: o.z || 0 };
}
// A box round a point
export const boxAt = (x, z, r, y0, y1) => ({ x0: x - r, x1: x + r, z0: z - r, z1: z + r, y0, y1 });

// Distance from a point to a segment g = { x0, y0, z0, x1, y1, z1 }
export function distToSeg(x, y, z, g) {
  const vx = g.x1 - g.x0, vy = g.y1 - g.y0, vz = (g.z1 || 0) - (g.z0 || 0), L = vx * vx + vy * vy + vz * vz;
  const t = L > 1e-9 ? Math.max(0, Math.min(1, ((x - g.x0) * vx + (y - g.y0) * vy + (z - (g.z0 || 0)) * vz) / L)) : 0;
  return Math.hypot(x - (g.x0 + vx * t), y - (g.y0 + vy * t), z - ((g.z0 || 0) + vz * t));
}
// Nearest point of a box (hurtbox) to a point
export function nearestOnBox(b, x, y, z) {
  return { x: Math.max(b.x0, Math.min(x, b.x1)), y: Math.max(b.y0, Math.min(y, b.y1)), z: Math.max(b.z0, Math.min(z, b.z1)) };
}
