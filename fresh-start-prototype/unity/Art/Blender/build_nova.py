# Nova, built from his concept art (portrait, turnaround and action sheets): a young man with swept-back dark hair in
# pearl-white armour over a navy bodysuit with glowing gold seams; a high collar, the gold four-point star on the
# chest, rounded pauldrons, a round gold buckle, white thigh panels, knee pads, greaves and boots, and the long white
# Sentinel Bracer on his right forearm. 1.85 m, facing -Y, arms in an A-pose.
# Run: blender -b -P build_nova.py -- <out-dir>   (writes nova.blend; render_turnaround.py makes the review sheet)
import math, os, sys, bpy
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_lib import *

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/tmp'
reset()

# ---- Materials ----
PEARL = mat('Nova_Armour', srgb('#eceef0'), metal=0.05, rough=0.32, coat=0.6, sheen=0.15)
NAVY = mat('Nova_Suit', srgb('#121a2e'), rough=0.55, sheen=0.12)
NAVY2 = mat('Nova_SuitPanel', srgb('#1c2a4a'), rough=0.45, coat=0.2)
GOLD = mat('Nova_GoldGlow', srgb('#ffbf5a'), metal=0.3, rough=0.25, emit=srgb('#ffad38'), strength=6)
GOLDM = mat('Nova_GoldMetal', srgb('#d9a54e'), metal=1.0, rough=0.22)
GLOVE = mat('Nova_Glove', srgb('#1a1d26'), rough=0.55, sheen=0.3)
SOLE = mat('Nova_Sole', srgb('#20232b'), rough=0.7)
SKIN = mat('Nova_Skin', srgb('#d39a74'), rough=0.5, sss=0.15)
HAIR = mat('Nova_Hair', srgb('#2b1a12'), rough=0.45, sheen=0.3)
EYEW = mat('Nova_EyeWhite', srgb('#f2eee8'), rough=0.2)
IRIS = mat('Nova_Iris', srgb('#3a2414'), rough=0.15, coat=1.0)
LIP = mat('Nova_Lip', srgb('#b77a62'), rough=0.45)

# ---- Landmarks (m) ----
SH = 1.47; SHX = 0.195                      # shoulder joint
ARM = math.radians(24)                      # A-pose: arms this far out from hanging straight
def arm_pt(side, d):                        # a point d metres down the arm from the shoulder
    return Vector((side * (SHX + math.sin(ARM) * d), 0.0, SH - math.cos(ARM) * d))
UA, FA = 0.29, 0.26                         # upper arm and forearm lengths
HX = 0.095                                  # hip joint
KNEE, ANK = 0.50, 0.09

# ---- Bodysuit: a skinned stick figure (Skin modifier), smoothed ----
V, E, R = [], [], []
def vtx(p, r): V.append(tuple(p)); R.append(r); return len(V) - 1
def chain(pts, start=None):
    prev = start
    for p, r in pts:
        k = vtx(p, r)
        if prev is not None: E.append((prev, k))
        prev = k
    return prev
pelvis = vtx((0, 0.005, 0.97), (0.15, 0.115))
spine = chain([((0, 0.0, 1.07), (0.13, 0.1)), ((0, 0.0, 1.2), (0.155, 0.11)), ((0, 0.005, 1.33), (0.175, 0.12)),
               ((0, 0.01, 1.43), (0.15, 0.105))], pelvis)
chain([((0, 0.0, 1.53), (0.052, 0.055)), ((0, -0.005, 1.6), (0.048, 0.05))], spine)
for s in (1, -1):
    sh = chain([((s * 0.12, 0.01, 1.455), (0.07, 0.07)), (tuple(arm_pt(s, 0)), (0.062, 0.062))], spine)
    el = chain([(tuple(arm_pt(s, 0.15)), (0.058, 0.056)), (tuple(arm_pt(s, UA)), (0.046, 0.046)),
                (tuple(arm_pt(s, UA + 0.12)), (0.048, 0.044)), (tuple(arm_pt(s, UA + FA)), (0.033, 0.026)),
                (tuple(arm_pt(s, UA + FA + 0.09)), (0.042, 0.022)), (tuple(arm_pt(s, UA + FA + 0.17)), (0.03, 0.016))], sh)
    chain([((s * HX, 0.0, 0.92), (0.085, 0.085)), ((s * 0.1, -0.005, 0.72), (0.075, 0.078)), ((s * 0.1, 0.0, KNEE), (0.055, 0.058)),
           ((s * 0.1, 0.012, 0.32), (0.055, 0.06)), ((s * 0.1, 0.0, ANK), (0.038, 0.04)), ((s * 0.1, -0.12, 0.035), (0.04, 0.025))], pelvis)
body = mesh_obj('Nova_Body', V, [], NAVY, edges=E)
sk = mod(body, 'SKIN', use_smooth_shade=True, branch_smoothing=0.6)
for k, r in enumerate(R): body.data.skin_vertices[0].data[k].radius = r
body.data.skin_vertices[0].data[pelvis].use_root = True
mod(body, 'SUBSURF', levels=2, render_levels=2)

# ---- Head: a shaped sphere with the face built on it ----
HC = Vector((0, -0.005, 1.715))
def head_shape(p, s, t):
    q = p.copy()
    if q.z < 0:                                  # narrower jaw and chin
        k = min(1, -q.z / 0.11); q.x *= 1 - 0.32 * k * k; q.y *= 1 - 0.12 * k
        if q.y < 0: q.y -= 0.012 * k             # the chin comes forward a little
    if q.y > 0: q.y *= 1.08                      # fuller back of the skull
    return q
head = patch('Nova_Head', SKIN, HC, (0.088, 0.1, 0.118), res=(40, 28), thick=0, shape=head_shape, subsurf=1)
neck_tree = None
ht = bvh(head)
def on_face(x, z, lift=0.0):
    hit, n, _, _ = ht.ray_cast(Vector((x, -1, z)), Vector((0, 1, 0)))
    return hit + n * lift, n
for s in (1, -1):
    p, n = on_face(s * 0.034, 1.722)
    ew = patch(f'Nova_Eye{s}', EYEW, p - n * 0.004, (0.0155, 0.009, 0.011), res=(16, 10), thick=0, subsurf=1)
    p2, n2 = on_face(s * 0.033, 1.721, 0.006)
    patch(f'Nova_Iris{s}', IRIS, p2, (0.0075, 0.003, 0.0085), res=(12, 8), thick=0, subsurf=1)
    brow = [on_face(s * x, z, 0.004)[0] for x, z in ((0.016, 1.744), (0.034, 1.749), (0.052, 1.743))]
    tube(f'Nova_Brow{s}', HAIR, brow, 0.0042, radii=[1.1, 1.0, 0.6])
    ep, _ = on_face(s * 0.0, 1.0)  if False else (None, None)
    patch(f'Nova_Ear{s}', SKIN, HC + Vector((s * 0.087, 0.012, -0.005)), (0.012, 0.022, 0.03), res=(12, 10), thick=0, subsurf=1)
p, n = on_face(0, 1.7)
patch('Nova_Nose', SKIN, p + Vector((0, 0.008, 0.004)), (0.013, 0.018, 0.022), v=(-90, 90), res=(14, 10), thick=0, subsurf=1)
mouth = [on_face(x, 1.668 + 0.002 * (1 - abs(x) / 0.02), 0.001)[0] for x in (-0.02, -0.01, 0, 0.01, 0.02)]
tube('Nova_Mouth', LIP, mouth, 0.0028, radii=[0.5, 1, 1, 1, 0.5])
# neck under the collar
patch('Nova_Neck', SKIN, (0, 0.005, 1.6), (0.048, 0.05, 0.06), res=(20, 8), v=(-60, 60), thick=0, subsurf=1)

# ---- Hair: a cap that hugs the skull down to the hairline, then thick locks that rise off the forehead in a quiff
# and sweep back over the top, laid along the head's own surface ----
def surf(th, ph, lift):
    """The point on the head at azimuth th (0 = front, + = his left) and elevation ph (degrees), lifted off it."""
    t, f = math.radians(th), math.radians(ph)
    d = Vector((math.sin(t) * math.cos(f), -math.cos(t) * math.cos(f), math.sin(f)))
    hit, n, _, _ = ht.ray_cast(HC + d * 0.5, -d)
    return hit + (hit - HC).normalized() * lift
def cap_shape(p, s, t):
    q = p.copy(); r = q.length
    th = math.degrees(math.atan2(q.x, -q.y)); ph = math.degrees(math.asin(max(-1, min(1, q.z / max(r, 1e-6)))))
    line = -30 + 58 * max(0.0, math.cos(math.radians(th))) ** 1.5      # the hairline: high at the brow, low at the nape
    if abs(th) < 100 and abs(th) > 55: line = max(line, 5)               # above the ears
    return head_shape(q, s, t) * (1.0 if ph > line else 0.9)           # below the line it tucks inside the head
patch('Nova_HairCap', HAIR, HC, (0.093, 0.105, 0.124), res=(48, 28), thick=0, shape=cap_shape, subsurf=1)
import random
random.seed(7)
for i in range(13):                                                      # the quiff
    a = i / 12 - 0.5; th = a * 80
    base = 28 + 30 * (1 - abs(a) * 1.6)
    pts = [surf(th, base - 4, 0.0), surf(th * 0.95, base + 6, 0.028 - abs(a) * 0.02), surf(th * 0.9, base + 22, 0.03 - abs(a) * 0.018),
           surf(th * 0.85 + 180 * 0, 80, 0.022), surf(180 - th * 0.6, 55, 0.014), surf(180 - th * 0.6 + random.uniform(-5, 5), 25, 0.006)]
    tube(f'Nova_Lock{i}', HAIR, pts, 0.021 - abs(a) * 0.008, radii=[0.4, 1.0, 1.0, 0.9, 0.7, 0.2], res=3)
for i in range(10):                                                      # the sides, swept back above the ears
    sd = 1 if i < 5 else -1; k = i % 5
    pts = [surf(sd * 60, 18 + k * 7, 0.004), surf(sd * 90, 22 + k * 7, 0.01), surf(sd * 125, 18 + k * 7, 0.009), surf(sd * 160, 8 + k * 7, 0.004)]
    tube(f'Nova_Side{i}', HAIR, pts, 0.013, radii=[0.4, 1.0, 0.9, 0.3], res=3)

# ---- Collar, breastplate, back plate, belt and buckle ----
collar = patch('Nova_Collar', PEARL, (0, 0.006, 1.583), (0.07, 0.068, 0.05), u=(18, 342), v=(-55, 55), e=0.45, thick=0.008, res=(40, 10), subsurf=1)
def chest_shape(p, s, t):
    q = p.copy()
    if q.z < -0.04: q.y -= 0.008                 # the lower plate sweeps forward over the ribs
    return q
chest = patch('Nova_Breastplate', PEARL, (0, 0.0, 1.345), (0.185, 0.128, 0.14), u=(-105, 105), v=(-58, 62), e=0.62,
              thick=0.01, res=(40, 22), shape=chest_shape, subsurf=1)
patch('Nova_Backplate', PEARL, (0, 0.006, 1.36), (0.17, 0.125, 0.12), u=(120, 240), v=(-45, 60), e=0.62, thick=0.01, res=(24, 16), subsurf=1)
patch('Nova_Belt', PEARL, (0, 0.006, 1.0), (0.154, 0.119, 0.026), v=(-80, 80), e=0.25, thick=0.006, res=(48, 6), subsurf=1)
bk = patch('Nova_Buckle', GOLDM, (0, -0.128, 1.0), (0.036, 0.01, 0.036), e=0.6, res=(24, 10), thick=0, subsurf=1)
patch('Nova_BuckleCore', GOLD, (0, -0.137, 1.0), (0.017, 0.004, 0.017), res=(16, 8), thick=0, subsurf=1)

# The star and the gold seams, projected onto the breastplate as the front view draws them
ct = bvh(chest)
star = []
for k in range(8):
    ang = math.pi / 2 + k * math.pi / 4; r = 0.075 if k % 2 == 0 else 0.016
    star.append((math.cos(ang) * r * (0.7 if k % 4 == 2 else 1), 1.35 + math.sin(ang) * r))
sp = project(ct, star, lift=0.004)
if len(sp) == 8:
    c3 = sum(sp, Vector()) / 8 + Vector((0, -0.006, 0))
    vs = [c3] + sp; fs = [(0, 1 + k, 1 + (k + 1) % 8) for k in range(8)]
    st = mesh_obj('Nova_Star', vs, fs, GOLD); mod(st, 'SOLIDIFY', thickness=0.004)
for s in (1, -1):
    seam = project(ct, [(s * 0.035, 1.47), (s * 0.08, 1.43), (s * 0.13, 1.4), (s * 0.165, 1.33)], lift=0.003)
    tube(f'Nova_ChestSeam{s}', GOLD, seam, 0.0028)
    tube(f'Nova_CollarSeam{s}', GOLD, project(bvh(collar), [(s * 0.026, 1.545), (s * 0.026, 1.585), (s * 0.028, 1.62)], lift=0.002), 0.0024)

# ---- Arms: pauldrons, forearm guard, gloves, and the Sentinel Bracer on the right ----
for s in (1, -1):
    sh = arm_pt(s, 0) + Vector((s * 0.01, 0.005, 0.02))
    patch(f'Nova_Pauldron{s}', PEARL, sh, (0.088, 0.085, 0.062), v=(-15, 90), e=0.8, thick=0.008, res=(28, 10), subsurf=1)
    patch(f'Nova_PauldronLip{s}', PEARL, sh + Vector((s * 0.012, 0, -0.04)), (0.078, 0.076, 0.024), v=(-60, 40), e=0.45, thick=0.006, res=(28, 6), subsurf=1)
    tube(f'Nova_PauldronSeam{s}', GOLD, [sh + Vector((s * 0.02, -0.088, 0.0)), sh + Vector((s * 0.062, -0.064, 0.008)), sh + Vector((s * 0.093, -0.01, 0.0))], 0.0025)
    axis = arm_pt(s, 1) - arm_pt(s, 0)
    hand = arm_pt(s, UA + FA + 0.08)
    patch(f'Nova_Glove{s}', GLOVE, None, (0.045, 0.03, 0.075), res=(18, 12), thick=0, M=frame(hand, axis))
    patch(f'Nova_Knuckle{s}', PEARL, None, (0.03, 0.012, 0.022), u=(-90, 90), res=(12, 8), thick=0.004,
          M=frame(hand + Vector((0, -0.028, -0.01)), axis))
    if s == 1:      # his left forearm guard
        patch('Nova_ForearmGuard', PEARL, None, (0.046, 0.046, 0.075), v=(-70, 70), e=0.5, thick=0.006, res=(24, 12), subsurf=1,
              M=frame(arm_pt(s, UA + 0.14), axis))
# The Sentinel Bracer: a long shell over the right forearm that tapers to a nose past the fist
s = -1; axis = (arm_pt(s, 1) - arm_pt(s, 0)).normalized()
def bracer_shape(p, ss, t):
    q = p.copy(); k = (0.5 - q.z / 0.44)                   # 0 at the elbow end, 1 at the nose
    k = max(0.0, min(1.0, k)); w = 1 - 0.55 * k ** 1.6
    q.x *= w * 0.88; q.y = q.y * w - 0.016 * k
    if q.y < 0: q.y -= 0.012 * (1 - abs(q.x) / 0.06) * (1 - k)            # a raised ridge along the top
    return q
BC = arm_pt(s, UA + FA - 0.04)
bracer = patch('Nova_Bracer', PEARL, None, (0.064, 0.07, 0.22), e=0.55, thick=0.008, res=(32, 24), shape=bracer_shape, subsurf=1,
               M=frame(BC, -axis))
bt = bvh(bracer)
tip = BC + axis * 0.22
patch('Nova_BracerEmitter', GOLD, None, (0.028, 0.028, 0.01), res=(20, 8), thick=0, M=frame(tip + axis * 0.004, axis))
edge = [BC + axis * d + Vector((0, -0.074, 0)) * (1 - 0.42 * max(0, min(1, 0.5 + d / 0.44)) ** 2) for d in (-0.17, -0.06, 0.06, 0.16)]
tube('Nova_BracerSeam', GOLD, [p + Vector((0, -0.006, 0)) for p in edge], 0.003)

# ---- Legs: thigh panels, knee pads, greaves and boots ----
for s in (1, -1):
    th = frame(Vector((s * 0.1, 0, 0.72)), (0, 0, 1))
    patch(f'Nova_ThighPanel{s}', PEARL, None, (0.083, 0.085, 0.17), u=(s * 45, s * 125) if s > 0 else (-125, -45), v=(-80, 80),
          e=0.6, thick=0.006, res=(10, 16), M=th, subsurf=1)
    patch(f'Nova_KneePad{s}', PEARL, (s * 0.1, -0.035, 0.505), (0.052, 0.032, 0.062), u=(-90, 90), e=0.6, thick=0.008, res=(18, 14), subsurf=1)
    patch(f'Nova_KneeGem{s}', GOLD, (s * 0.1, -0.074, 0.505), (0.011, 0.005, 0.011), res=(12, 8), thick=0, subsurf=1)
    def greave_shape(p, ss, t):
        q = p.copy(); q.y -= 0.012 * max(0, -q.y) / 0.07; return q          # a shin ridge at the front
    patch(f'Nova_Greave{s}', PEARL, (s * 0.1, 0.004, 0.27), (0.06, 0.066, 0.2), v=(-75, 85), e=0.55, thick=0.008, res=(28, 16), shape=greave_shape, subsurf=1)
    tube(f'Nova_GreaveStripe{s}', NAVY2, [(s * 0.158, -0.0, 0.41), (s * 0.161, 0.0, 0.3), (s * 0.155, 0.0, 0.2)], 0.007)
    def boot_shape(p, ss, t):
        q = p.copy(); q.z = max(q.z, -0.045); return q                      # a flat sole
    patch(f'Nova_Boot{s}', PEARL, (s * 0.1, -0.045, 0.055), (0.058, 0.125, 0.06), e=0.62, thick=0, res=(28, 14), shape=boot_shape)
    patch(f'Nova_Sole{s}', SOLE, (s * 0.1, -0.045, 0.012), (0.06, 0.128, 0.013), e=0.5, thick=0, res=(24, 6))
    patch(f'Nova_BootCuff{s}', NAVY2, (s * 0.1, 0.0, 0.125), (0.054, 0.058, 0.02), v=(-60, 60), thick=0.006, res=(24, 4))

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'nova.blend'))
print('built', len(bpy.data.objects), 'objects')
