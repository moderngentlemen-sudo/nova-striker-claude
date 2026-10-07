# RAM, built from his concept art: a towering armoured frame in battle-worn gunmetal over dark joints and undersuit,
# electric-blue light in the abdomen's bands, the joint discs, the boots and the T visor; a rounded helm with its
# horns made into armoured sensor fins, huge rounded pauldrons, a layered chest with a glowing core, a back pack with exhaust stacks, big
# blocky fists and segmented legs on round joint discs. His proportions follow his in-game skeleton (Rigs.Skeleton
# for "ram", drawn 1.34 times life size), so the model lines up with the rig that drives it. The Rampart and the
# Breach Cannon stay the game's own (it poses them separately), so they are not modelled here.
# Run: blender -b -P build_ram.py -- <out-dir>   (writes ram.blend)
import math, os, sys, bpy
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_lib import *

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/tmp'
reset()
K = 1.34                                           # the rig's size for RAM (CHARS["ram"].scale)
ARMOUR = mat('Ram_Armour', srgb('#5f6771'), metal=0.7, rough=0.4, coat=0.15)
TRIM = mat('Ram_Trim', srgb('#2a2f37'), metal=0.5, rough=0.45)
SUIT = mat('Ram_Suit', srgb('#15181d'), metal=0.3, rough=0.6)
GLOW = mat('Ram_Glow', srgb('#58a6ff'), emit=srgb('#3f8cff'), strength=12)
# Battle wear for the review renders (the game wears it through Look's "worn" texture set): scratches, chipped
# paint, grime, and paint rubbed off the edges. NS_TEX names the folder make_textures.py wrote its PNGs to.
TEX = os.environ.get('NS_TEX')
if TEX and os.path.exists(os.path.join(TEX, 'worn_albedo.png')):
    for m, tile in ((ARMOUR, 1.2), (TRIM, 1.2)): edge_wear(m, surface(m, TEX, 'worn', tile), 0.55 if m == ARMOUR else 0.35)

# ---- Landmarks (m): the rig's joints times K ----
HIP_Z, HIP_X = 0.95 * K, 0.16 * K
KNEE_Z, ANK_Z = (0.95 - 0.46) * K, (0.95 - 0.92) * K + 0.04
SPINE_Z = 1.03 * K
SH_Z, SH_X = (1.03 + 0.53) * K, 0.42 * K
HEAD = Vector((0, -0.13 * K, (1.03 + 0.74) * K))
UA, FA = 0.31 * K, 0.29 * K
ARM = math.radians(14)
def arm_pt(s, d): return Vector((s * (SH_X + math.sin(ARM) * d), 0.0, SH_Z - math.cos(ARM) * d))
def leg_pt(s, z): return Vector((s * HIP_X, 0, z))

# ---- Undersuit: a thick skinned frame (dark), most of it under armour ----
V, E, R = [], [], []
def vtx(p, r): V.append(tuple(p)); R.append(r); return len(V) - 1
def chain(pts, start=None):
    prev = start
    for p, r in pts:
        k = vtx(p, r)
        if prev is not None: E.append((prev, k))
        prev = k
    return prev
pelvis = vtx((0, 0.0, HIP_Z), (0.3, 0.23))
spine = chain([((0, 0, SPINE_Z + 0.08), (0.27, 0.21)), ((0, 0.0, SPINE_Z + 0.3), (0.34, 0.25)), ((0, 0.02, SPINE_Z + 0.55), (0.42, 0.28)),
               ((0, 0.02, SH_Z), (0.36, 0.26))], pelvis)
chain([((0, -0.08, SH_Z + 0.12), (0.13, 0.13)), (tuple(HEAD + Vector((0, 0.02, -0.06))), (0.12, 0.12))], spine)
for s in (1, -1):
    sh = chain([((s * 0.3, 0.0, SH_Z), (0.15, 0.15)), (tuple(arm_pt(s, 0)), (0.15, 0.15))], spine)
    chain([(tuple(arm_pt(s, UA * 0.5)), (0.13, 0.13)), (tuple(arm_pt(s, UA)), (0.12, 0.12)), (tuple(arm_pt(s, UA + FA)), (0.11, 0.1)),
           (tuple(arm_pt(s, UA + FA + 0.12)), (0.12, 0.1))], sh)
    chain([(tuple(leg_pt(s, HIP_Z - 0.02)), (0.17, 0.17)), (tuple(leg_pt(s, (HIP_Z + KNEE_Z) / 2)), (0.16, 0.16)), (tuple(leg_pt(s, KNEE_Z)), (0.14, 0.14)),
           (tuple(leg_pt(s, (KNEE_Z + ANK_Z) / 2)), (0.15, 0.15)), (tuple(leg_pt(s, ANK_Z + 0.08)), (0.12, 0.12))], pelvis)
body = mesh_obj('Ram_Body', V, [], SUIT, edges=E)
mod(body, 'SKIN', use_smooth_shade=True, branch_smoothing=0.5)
for k, r in enumerate(R): body.data.skin_vertices[0].data[k].radius = r
body.data.skin_vertices[0].data[pelvis].use_root = True
mod(body, 'SUBSURF', levels=2, render_levels=2)

# ---- Pelvis, abdomen bands with light between, the chest, the back pack and stacks, the collar ----
patch('Ram_Pelvis', ARMOUR, (0, -0.01, HIP_Z + 0.02), (0.36, 0.28, 0.16), v=(-65, 60), e=0.4, thick=0.03, res=(32, 10), subsurf=1)
box('Ram_Cod', ARMOUR, (0, -0.3, HIP_Z - 0.06), (0.2, 0.08, 0.26), 0.04, 3)
for k in range(3):
    z = SPINE_Z + 0.1 + k * 0.12
    patch(f'Ram_AbBand{k}', TRIM, (0, 0.0, z), (0.31 + k * 0.03, 0.245 + k * 0.012, 0.05), v=(-70, 70), e=0.5, thick=0.02, res=(32, 6), subsurf=1)
    if k < 2: patch(f'Ram_AbGlow{k}', GLOW, (0, 0.0, z + 0.06), (0.3 + k * 0.03, 0.235 + k * 0.012, 0.012), v=(-60, 60), e=0.5, thick=0, res=(32, 4), subsurf=0)
chest = patch('Ram_Chest', ARMOUR, (0, -0.02, SPINE_Z + 0.58), (0.47, 0.33, 0.3), u=(-118, 118), v=(-50, 70), e=0.42, thick=0.035, res=(40, 18), subsurf=1)
for s in (1, -1):
    box(f'Ram_Pec{s}', ARMOUR, (s * 0.21, -0.37, SPINE_Z + 0.62), (0.36, 0.1, 0.3), 0.04, 3, rot=(math.radians(-10), 0, s * math.radians(14)))
    box(f'Ram_PecEdge{s}', TRIM, (s * 0.21, -0.37, SPINE_Z + 0.45), (0.33, 0.07, 0.045), 0.012, rot=(0, 0, s * math.radians(14)))
    box(f'Ram_Rib{s}', ARMOUR, (s * 0.3, -0.27, SPINE_Z + 0.36), (0.2, 0.1, 0.12), 0.03, 3, rot=(0, 0, s * math.radians(25)))
    tube(f'Ram_ChestSeam{s}', GLOW, [(s * 0.44, -0.1, SPINE_Z + 0.42), (s * 0.47, 0.0, SPINE_Z + 0.55), (s * 0.45, 0.1, SPINE_Z + 0.68)], 0.012)
    box(f'Ram_PecGlow{s}', GLOW, (s * 0.06, -0.425, SPINE_Z + 0.6), (0.02, 0.02, 0.16), 0.005)
box('Ram_Core', GLOW, (0, -0.43, SPINE_Z + 0.5), (0.16, 0.03, 0.05), 0.01)
box('Ram_CoreFrame', TRIM, (0, -0.415, SPINE_Z + 0.5), (0.22, 0.04, 0.1), 0.015)
patch('Ram_Backplate', ARMOUR, (0, 0.02, SPINE_Z + 0.58), (0.44, 0.31, 0.28), u=(122, 238), v=(-45, 60), e=0.6, thick=0.03, res=(24, 14), subsurf=1)
box('Ram_Pack', TRIM, (0, 0.42, SPINE_Z + 0.58), (0.66, 0.3, 0.72), 0.06, 3)
for z in (-0.1, 0.0, 0.1): box('Ram_PackVent', GLOW, (0, 0.575, SPINE_Z + 0.58 + z), (0.4, 0.015, 0.025), 0.004)
for s in (1, -1):
    cyl(f'Ram_Stack{s}', SUIT, (s * 0.2, 0.5, SPINE_Z + 0.9), (s * 0.2, 0.66, SPINE_Z + 1.32), 0.085, 16, r2=0.095)
    tube(f'Ram_StackRim{s}', GLOW, [Vector((s * 0.2 + 0.1 * math.cos(a), 0.66 + 0.1 * math.sin(a) * 0.35, SPINE_Z + 1.33 - 0.035 * math.sin(a))) for a in [k * math.pi / 8 for k in range(16)]], 0.018, cyclic=True)
tube('Ram_Collar', TRIM, [Vector((0.27 * math.cos(a), 0.22 * math.sin(a) - 0.04, SH_Z + 0.1)) for a in [k * math.pi / 10 for k in range(20)]], 0.07, cyclic=True)

# ---- Head: a rounded helm, crest, dark faceplate with the T visor, jaw guard, and curled ram's horns ----
helm = patch('Ram_Helm', ARMOUR, HEAD + Vector((0, 0.02, 0.1)), (0.21, 0.23, 0.22), v=(-35, 90), e=0.85, thick=0.02, res=(28, 18), subsurf=1)
box('Ram_Crest', ARMOUR, HEAD + Vector((0, 0.0, 0.33)), (0.07, 0.3, 0.06), 0.02, 3)
box('Ram_Face', TRIM, HEAD + Vector((0, -0.19, 0.06)), (0.3, 0.06, 0.26), 0.05, 3)
box('Ram_VisorH', GLOW, HEAD + Vector((0, -0.225, 0.12)), (0.26, 0.02, 0.045), 0.01)
box('Ram_VisorV', GLOW, HEAD + Vector((0, -0.225, 0.04)), (0.045, 0.02, 0.13), 0.01)
box('Ram_Jaw', ARMOUR, HEAD + Vector((0, -0.17, -0.08)), (0.26, 0.12, 0.12), 0.04, 3)
# The horns, made to work: armoured sensor fins that keep a ram's sweep back and down round the helm. Faceted
# gunmetal blade plates overlapping like layered armour, a blue light channel along the outer face, a glowing sensor pod at the
# tip (the Breach Cannon's targeting array), and a powered actuator disc at the temple.
for s in (1, -1):
    base = HEAD + Vector((s * 0.2, 0.02, 0.18))
    cyl(f'Ram_HornBase{s}', TRIM, base, base + Vector((s * 0.06, 0, 0)), 0.08, 16)
    tube(f'Ram_HornBaseGlow{s}', GLOW, [base + Vector((s * 0.062, 0.05 * math.cos(a), 0.05 * math.sin(a))) for a in [k * math.pi / 8 for k in range(16)]], 0.008, cyclic=True)
    pts = []
    for k in range(9):           # the fin's spine: up from the temple, back over, and down behind the helm
        t = k / 8; a = math.pi * 0.42 + t * math.pi * 1.05; r = 0.19 * (1 - 0.25 * t)
        pts.append(base + Vector((s * (0.06 + 0.07 * t), math.cos(a) * r * 1.05 + 0.08, math.sin(a) * r - 0.04)))
    # four long blade plates, each overlapping the next like layered armour, over a dark core
    for k in range(4):
        a, b = pts[2 * k], pts[2 * k + 2]; ext = (b - a) * 0.18
        h0, h1 = 0.135 - k * 0.022, 0.115 - k * 0.022
        blade(f'Ram_Fin{k}{s}', ARMOUR, a - ext * 0.3, b + ext, (1, 0, 0), 0.05 - k * 0.006, h0, 0.046 - k * 0.006, h1, 0.008)
        blade(f'Ram_FinCore{k}{s}', TRIM, a, b, (1, 0, 0), 0.03, h0 * 0.7, 0.03, h1 * 0.7, 0.003)
    # the light channel along the outer face
    outer = [p + Vector((s * (0.03 - 0.004 * n), 0, 0)) for n, p in enumerate(pts[1:-1])]
    tube(f'Ram_FinGlow{s}', GLOW, outer, 0.008)
    tip = pts[-1] + (pts[-1] - pts[-2]).normalized() * 0.03
    dirn = (pts[-1] - pts[-2]).normalized()
    cyl(f'Ram_SensorPod{s}', TRIM, pts[-1], tip + dirn * 0.03, 0.045, 12, r2=0.04)
    patch(f'Ram_SensorLens{s}', GLOW, tip + dirn * 0.035, (0.03, 0.03, 0.03), res=(12, 8), thick=0, subsurf=1)

# ---- Arms: huge rounded pauldrons, armoured sleeves, elbow discs, gauntlets and blocky fists ----
for s in (1, -1):
    sh = arm_pt(s, 0) + Vector((s * 0.02, 0, 0.06))
    patch(f'Ram_Pauldron{s}', ARMOUR, sh, (0.33, 0.31, 0.25), v=(-25, 90), e=0.78, thick=0.03, res=(30, 12), subsurf=1)
    tube(f'Ram_PauldronLip{s}', TRIM, [sh + Vector((0.31 * math.cos(a), 0.29 * math.sin(a), -0.1)) for a in [k * math.pi / 10 for k in range(20)]], 0.04, cyclic=True)
    cyl(f'Ram_PauldronBolt{s}', TRIM, sh + Vector((s * 0.3, -0.12, 0.02)), sh + Vector((s * 0.34, -0.12, 0.02)), 0.07, 16)
    tube(f'Ram_PauldronGlow{s}', GLOW, [sh + Vector((s * 0.31, -0.18, -0.06)), sh + Vector((s * 0.33, 0, -0.04)), sh + Vector((s * 0.31, 0.18, -0.06))], 0.012)
    axis = (arm_pt(s, 1) - arm_pt(s, 0)).normalized()
    ua = arm_pt(s, UA * 0.5); box(f'Ram_Sleeve{s}', ARMOUR, ua, (0.3, 0.3, 0.26), 0.04, 3, rot=(0, -s * ARM, 0))
    el = arm_pt(s, UA)
    cyl(f'Ram_Elbow{s}', TRIM, el - Vector((s * 0.12, 0, 0)), el + Vector((s * 0.12, 0, 0)), 0.13, 20)
    cyl(f'Ram_ElbowGlow{s}', GLOW, el + Vector((s * 0.12, 0, 0)), el + Vector((s * 0.125, 0, 0)), 0.06, 16)
    fa = arm_pt(s, UA + FA * 0.5); box(f'Ram_Gauntlet{s}', ARMOUR, fa, (0.36, 0.38, 0.36), 0.05, 3, rot=(0, -s * ARM, 0))
    box(f'Ram_GauntletCuff{s}', TRIM, arm_pt(s, UA + FA * 0.92), (0.33, 0.35, 0.07), 0.02, 2, rot=(0, -s * ARM, 0))
    box(f'Ram_GauntletGlow{s}', GLOW, arm_pt(s, UA + FA * 0.5) + Vector((s * 0.0, -0.205, 0)), (0.03, 0.01, 0.24), 0.004)
    fist = arm_pt(s, UA + FA + 0.13)
    box(f'Ram_Fist{s}', TRIM, fist, (0.32, 0.32, 0.3), 0.05, 3)
    for k in range(4): box(f'Ram_Finger{k}{s}', SUIT, fist + Vector((-0.11 + k * 0.073, -0.17, -0.06)), (0.064, 0.06, 0.13), 0.02, 2)
    box(f'Ram_Thumb{s}', SUIT, fist + Vector((-s * 0.17, -0.08, 0.0)), (0.07, 0.12, 0.1), 0.02, 2)

# ---- Legs: hip discs, segmented thigh plates, knee discs and caps, shin armour, pistons, heavy boots ----
for s in (1, -1):
    hip = leg_pt(s, HIP_Z - 0.03)
    cyl(f'Ram_HipDisc{s}', TRIM, hip + Vector((s * 0.13, 0, 0)), hip + Vector((s * 0.2, 0, 0)), 0.15, 20)
    cyl(f'Ram_HipGlow{s}', GLOW, hip + Vector((s * 0.2, 0, 0)), hip + Vector((s * 0.205, 0, 0)), 0.065, 16)
    for k, (z, h) in enumerate(((HIP_Z - 0.16, 0.14), (HIP_Z - 0.4, 0.13))):
        box(f'Ram_Thigh{k}{s}', ARMOUR, leg_pt(s, z), (0.38, 0.4, 2 * h - 0.03), 0.05, 3)
    kn = leg_pt(s, KNEE_Z)
    cyl(f'Ram_Knee{s}', TRIM, kn - Vector((s * 0.15, 0, 0)), kn + Vector((s * 0.15, 0, 0)), 0.15, 20)
    cyl(f'Ram_KneeGlow{s}', GLOW, kn + Vector((s * 0.15, 0, 0)), kn + Vector((s * 0.155, 0, 0)), 0.065, 16)
    box(f'Ram_KneeCap{s}', ARMOUR, kn + Vector((0, -0.17, 0.02)), (0.24, 0.1, 0.26), 0.04, 3, rot=(math.radians(-10), 0, 0))
    sh_ = leg_pt(s, (KNEE_Z + ANK_Z) / 2 + 0.06)
    box(f'Ram_Shin{s}', ARMOUR, sh_, (0.36, 0.38, 0.4), 0.05, 3)
    box(f'Ram_ShinGuard{s}', ARMOUR, sh_ + Vector((0, -0.2, 0.02)), (0.24, 0.06, 0.36), 0.03, 3)
    cyl(f'Ram_Piston{s}', TRIM, leg_pt(s, KNEE_Z - 0.06) + Vector((0, 0.22, 0)), leg_pt(s, ANK_Z + 0.2) + Vector((0, 0.22, 0)), 0.045, 12)
    tube(f'Ram_PistonGlow{s}', GLOW, [leg_pt(s, KNEE_Z - 0.3) + Vector((0.055 * math.cos(a), 0.22 + 0.055 * math.sin(a), 0)) for a in [k * math.pi / 8 for k in range(16)]], 0.014, cyclic=True)
    foot = leg_pt(s, 0.12) + Vector((0, -0.12, 0))
    box(f'Ram_Boot{s}', TRIM, foot, (0.36, 0.62, 0.24), 0.06, 3)
    box(f'Ram_Toe{s}', ARMOUR, foot + Vector((0, -0.24, 0.04)), (0.32, 0.16, 0.16), 0.05, 3)
    box(f'Ram_BootGlow{s}', GLOW, foot + Vector((0, -0.322, 0.02)), (0.24, 0.01, 0.035), 0.005)
    box(f'Ram_Sole{s}', SUIT, foot + Vector((0, 0, -0.11)), (0.38, 0.64, 0.04), 0.01)
    box(f'Ram_Ankle{s}', ARMOUR, leg_pt(s, ANK_Z + 0.24), (0.3, 0.3, 0.12), 0.04, 3)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'ram.blend'))
print('built', len(bpy.data.objects), 'objects')
