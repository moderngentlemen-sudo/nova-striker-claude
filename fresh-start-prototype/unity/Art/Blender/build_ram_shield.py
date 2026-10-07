# RAM's Rampart, from his concept art: a heavy stone-grey frame of chamfered armour bars, corner blocks and side
# clamps, round the game's own glowing hexagonal hard-light panel (the rig keeps drawing it, so it still pulses and
# fades as before), a dark backing plate with a forearm mount behind, and the ram's-head emblem at its heart with
# great curled horns and glowing eyes. Built in the shield's own space in the rig's units (Rigs.BuildRamRig's
# `shield` group: x across, z up, facing -Y here, which is +z, toward the camera, in the game), so it lands where
# the rig's shield is and moves as the game poses it. Run: blender -b -P build_ram_shield.py -- <out-dir>
# (writes ram_shield.blend; export_ram_gear.py writes it for the game). NS_TEX adds the battle wear for renders.
import math, os, sys, bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_lib import *

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/tmp'
reset()
FRAME = mat('Ram_Frame', srgb('#5a6068'), metal=0.6, rough=0.45, coat=0.1)
TRIM = mat('Ram_Trim', srgb('#2a2f37'), metal=0.5, rough=0.45)
SUIT = mat('Ram_Suit', srgb('#15181d'), metal=0.3, rough=0.6)
GLOW = mat('Ram_Glow', srgb('#58a6ff'), emit=srgb('#3f8cff'), strength=12)
TEX = os.environ.get('NS_TEX')
if TEX and os.path.exists(os.path.join(TEX, 'worn_albedo.png')):
    for m in (FRAME, TRIM): edge_wear(m, surface(m, TEX, 'worn', 0.9), 0.5)
def P(x, y, z): return Vector((x, -z, y))           # (the rig's shield space, x across, y up, z out, to Blender's)

# ---- Backing plate and the forearm mount behind it ----
box('Ram_ShieldBack', SUIT, P(0, 0, -0.015), (0.62, 0.05, 1.02), 0.015, 2)
for y in (-0.3, 0.0, 0.3): box('Ram_ShieldRib', TRIM, P(0, y, -0.055), (0.56, 0.04, 0.05), 0.012, 2)
box('Ram_ShieldMount', TRIM, P(0, 0.05, -0.1), (0.14, 0.08, 0.42), 0.025, 2)
for y in (-0.12, 0.2): cyl('Ram_ShieldGrip', SUIT, P(-0.1, y, -0.14), P(0.1, y, -0.14), 0.025, 12)

# ---- The frame: chamfered bars, corner blocks with bolts and lights, side clamps ----
for y in (0.535, -0.535):
    box('Ram_ShieldBarH', FRAME, P(0, y, 0.02), (0.6, 0.15, 0.11), 0.03, 3)
    box('Ram_ShieldLipH', TRIM, P(0, y - math.copysign(0.07, y), 0.06), (0.58, 0.035, 0.025), 0.008, 2)
for x in (0.355, -0.355):
    box('Ram_ShieldBarV', FRAME, P(x, 0, 0.02), (0.11, 0.15, 0.94), 0.03, 3)
    box('Ram_ShieldLipV', TRIM, P(x - math.copysign(0.065, x), 0, 0.06), (0.025, 0.035, 0.96), 0.008, 2)
    for y in (0.22, -0.22):                              # side clamps, with a bolt and a light slit
        box('Ram_ShieldClamp', FRAME, P(x + math.copysign(0.01, x), y, 0.035), (0.15, 0.17, 0.14), 0.03, 3)
        cyl('Ram_ShieldBolt', TRIM, P(x + math.copysign(0.03, x), y + 0.035, 0.1), P(x + math.copysign(0.03, x), y + 0.035, 0.12), 0.022, 10)
        box('Ram_ShieldSlit', GLOW, P(x + math.copysign(0.03, x), y - 0.035, 0.108), (0.05, 0.012, 0.01), 0.003)
for x in (0.36, -0.36):
    for y in (0.54, -0.54):                              # corner blocks: a heavy angled cap over a dark core
        box('Ram_ShieldCorner', TRIM, P(x, y, 0.02), (0.17, 0.18, 0.17), 0.03, 2)
        box('Ram_ShieldCornerCap', FRAME, P(x + math.copysign(0.01, x), y + math.copysign(0.01, y), 0.06), (0.17, 0.17, 0.12), 0.035, 3,
            rot=(0, 0, 0))
        for k in range(2):
            bx, by = x - math.copysign(0.035 - 0.07 * k, x), y - math.copysign(0.04, y)
            cyl('Ram_ShieldBolt', TRIM, P(bx, by, 0.12), P(bx, by, 0.135), 0.018, 10)
        box('Ram_ShieldCornerGlow', GLOW, P(x, y + math.copysign(0.03, y), 0.13), (0.08, 0.012, 0.006), 0.003)

# ---- The ram's-head emblem (the rig's `crest`: centred at (0, 0.06) a little in front of the panel) ----
E = (0, 0.06, 0.1)
def EP(x, y, z=0.0): return P(E[0] + x, E[1] + y, E[2] + z)
patch('Ram_EmblemHead', FRAME, EP(0, 0.01), (0.1, 0.03, 0.17), v=(-90, 90), e=0.7, thick=0, res=(24, 16), subsurf=1,
      shape=lambda p, s, t: Vector((p.x * (0.55 + 0.45 * min(1, (p.z + 0.17) / 0.22)), p.y, p.z)))   # tapering to the muzzle
box('Ram_EmblemBrow', FRAME, EP(0, 0.1, 0.02), (0.24, 0.04, 0.07), 0.02, 3)
box('Ram_EmblemMuzzle', TRIM, EP(0, -0.13, 0.025), (0.09, 0.03, 0.06), 0.02, 2)
for s in (1, -1):
    box('Ram_EmblemEye', GLOW, EP(s * 0.055, 0.06, 0.045), (0.045, 0.01, 0.018), 0.004, rot=(0, s * math.radians(-12), 0))
    pts = []                                             # a horn curling out, back and down round on itself
    for k in range(14):
        t = k / 13; a = math.pi * (0.55 - 1.55 * t); r = 0.13 * (1 - 0.45 * t)
        pts.append(EP(s * (0.15 + r * math.cos(a) - 0.02), 0.06 + r * math.sin(a), 0.005 + 0.02 * t))
    tube(f'Ram_EmblemHorn{s}', FRAME, pts, 0.038, radii=[1.0 - 0.6 * k / 13 for k in range(14)], res=4)
    for k in range(3, 12, 3): tube(f'Ram_EmblemRidge{s}', TRIM, [pts[k] + (pts[k + 1] - pts[k - 1]).cross(Vector((0, 1, 0))).normalized() * 0.03 * m for m in (-1, 1)], 0.008)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'ram_shield.blend'))
print('built', len(bpy.data.objects), 'objects')
