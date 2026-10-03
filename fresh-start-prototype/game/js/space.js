// Gameplay space to world space. The sim's x runs along the path (which curves round the Storm Spire and winds
// through the Version 12 levels), y is up and z is the offset across the path (pathFrame's normal, positive
// toward the side the old side-on camera sat on).
//   toWorldZ(x, y, z): where a sim point is in the world.
//   toWorld(x, y, dz): the same, with z taken from the current LANE plus dz. The effects code was written for the
//     side view, where every effect sat on the one plane and dz nudged it toward the camera; now an effect's lane
//     is set from the event (or the entity) it belongs to (laneOf), so all of that code places things in 3D.
import * as THREE from 'three';
import { pathFrame } from './level.js';

export const LANE = { z: 0 };
export function toWorldZ(x, y, z = 0, out = new THREE.Vector3()) {
  const f = pathFrame(x);
  return out.set(f.px + f.nx * z, y, f.pz + f.nz * z);
}
export function toWorld(x, y, depth = 0, out = new THREE.Vector3()) { return toWorldZ(x, y, LANE.z + depth, out); }
// A sim direction (along the path dx, up dy, across dz) as a world vector
export function planeDir(x, dx, dy, out = new THREE.Vector3(), dz = 0) {
  const f = pathFrame(x);
  return out.set(f.tx * dx + f.nx * dz, dy, f.tz * dx + f.nz * dz);
}
export const planeDir3 = (x, dx, dy, dz, out = new THREE.Vector3()) => planeDir(x, dx, dy, out, dz);
// World direction -> sim direction at sim x (the inverse of planeDir)
export function simDir(x, v) {
  const f = pathFrame(x);
  return [v.x * f.tx + v.z * f.tz, v.y, v.x * f.nx + v.z * f.nz];
}
// World point -> sim point near sim x (exact on straights; on a curve, close to the path line)
export function toSim(v, nearX) {
  let x = nearX;
  for (let i = 0; i < 3; i++) {
    const f = pathFrame(x);
    x += (v.x - f.px) * f.tx + (v.z - f.pz) * f.tz;
  }
  const f = pathFrame(x);
  return { x, y: v.y, z: (v.x - f.px) * f.nx + (v.z - f.pz) * f.nz };
}
// The world yaw (rotation about the vertical) that turns a model's +x to face sim direction (fx, fz) at sim x
export function yawOf(x, fx = 1, fz = 0) {
  const d = planeDir(x, fx, 0, new THREE.Vector3(), fz);
  return Math.atan2(-d.z, d.x);
}
// Which lane an event belongs to: its own z, or that of the player, enemy, gadget or piece it is about
export function laneOf(ev) {
  if (ev.z !== undefined) return ev.z;
  for (const k of ['e', 'g', 'b', 'p', 'q']) if (ev[k] && ev[k].z !== undefined) return ev[k].z;
  if (ev.members && ev.members[0]) return ev.members[0].z || 0;
  return 0;
}
