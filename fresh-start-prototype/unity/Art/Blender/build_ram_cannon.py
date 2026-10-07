# RAM's Breach Cannon, after his concept art: a heavy shoulder-mounted rail cannon in battle-worn gunmetal. An
# armoured breech on a pivot yoke, a power cell behind it with a blue window and a cable into the breech, a dark
# barrel in two armour shrouds with heat-sink fins between them, a muzzle brake with side ports and a blue ring
# behind it, and a targeting scope on top (it shares the helmet fins' sensors). Built in the cannon's own space in
# the rig's units (Rigs.BuildRamRig's `cannon` group: the barrel along +x, z up, his far side +Y here, -z in the
# game), so it lands where the rig's cannon is and turns to the aim as before; the muzzle sits at the rig's muzzle
# point (0.58, 0.02). Run: blender -b -P build_ram_cannon.py -- <out-dir>   (writes ram_cannon.blend;
# export_ram_gear.py writes it for the game). NS_TEX adds the battle wear for renders.
import math, os, sys, bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_lib import *

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/tmp'
reset()
ARMOUR = mat('Ram_Armour', srgb('#5f6771'), metal=0.7, rough=0.4, coat=0.15)
TRIM = mat('Ram_Trim', srgb('#2a2f37'), metal=0.5, rough=0.45)
SUIT = mat('Ram_Suit', srgb('#15181d'), metal=0.3, rough=0.6)
GLOW = mat('Ram_Glow', srgb('#58a6ff'), emit=srgb('#3f8cff'), strength=12)
TEX = os.environ.get('NS_TEX')
if TEX and os.path.exists(os.path.join(TEX, 'worn_albedo.png')):
    for m, a in ((ARMOUR, 0.55), (TRIM, 0.35)): edge_wear(m, surface(m, TEX, 'worn', 0.9), a)
def P(x, y, z=0.0): return Vector((x, -z, y))       # (the rig's cannon space, x along the barrel, y up, z out, to Blender's)
def X(x0, x1, y=0.02, z=0.0): return P(x0, y, z), P(x1, y, z)   # (two points along the barrel's axis)

# ---- Pivot yoke on the shoulder, under the breech ----
cyl('Ram_CannonPivot', TRIM, P(-0.02, -0.12, -0.1), P(-0.02, -0.12, 0.1), 0.065, 20)
for z in (0.09, -0.09): box('Ram_CannonYoke', ARMOUR, P(-0.02, -0.06, z), (0.16, 0.035, 0.16), 0.02, 2)
cyl('Ram_CannonPivotGlow', GLOW, P(-0.02, -0.12, 0.1), P(-0.02, -0.12, 0.106), 0.03, 16)

# ---- The breech: an armoured block, a top plate, vent slits on the side facing the camera ----
box('Ram_CannonBreech', TRIM, P(0, 0.02), (0.3, 0.2, 0.2), 0.03, 2)
box('Ram_CannonPlateTop', ARMOUR, P(0.0, 0.115), (0.32, 0.22, 0.05), 0.02, 3)
box('Ram_CannonPlateSide', ARMOUR, P(-0.01, 0.02, 0.105), (0.26, 0.03, 0.16), 0.015, 3)
box('Ram_CannonPlateFar', ARMOUR, P(-0.01, 0.02, -0.105), (0.26, 0.03, 0.16), 0.015, 3)
for k in range(3): box('Ram_CannonVent', GLOW, P(-0.08 + 0.055 * k, 0.0, 0.122), (0.035, 0.006, 0.08), 0.003)

# ---- Power cell behind, with a blue window, and the cable into the breech ----
cyl('Ram_CannonCell', TRIM, P(-0.27, 0.02), P(-0.15, 0.02), 0.085, 20)
cyl('Ram_CannonCellCap', ARMOUR, P(-0.29, 0.02), P(-0.26, 0.02), 0.09, 20)
box('Ram_CannonCellGlow', GLOW, P(-0.21, 0.02, 0.083), (0.07, 0.008, 0.05), 0.004)
tube('Ram_CannonCable', SUIT, [P(-0.2, -0.06, -0.04), P(-0.18, -0.12, -0.05), P(-0.08, -0.11, -0.06), P(-0.04, -0.06, -0.06)], 0.018)

# ---- The barrel: a dark rail tube in two armour shrouds, heat-sink fins between, a muzzle brake ----
cyl('Ram_CannonBarrel', SUIT, *X(0.13, 0.56), 0.058, 20)
cyl('Ram_CannonShroud', ARMOUR, *X(0.13, 0.27), 0.088, 24)
cyl('Ram_CannonShroud', ARMOUR, *X(0.37, 0.47), 0.082, 24, r2=0.076)
for k in range(4):
    x = 0.29 + 0.022 * k; cyl('Ram_CannonFin', TRIM, *X(x, x + 0.01), 0.078, 20)
for x in (0.14, 0.26): cyl('Ram_CannonBand', TRIM, *X(x, x + 0.015), 0.092, 24)
box('Ram_CannonRail', TRIM, P(0.3, -0.05), (0.32, 0.03, 0.02), 0.006)
tube('Ram_CannonRing', GLOW, [P(0.49, 0.02 + 0.07 * math.sin(a), 0.07 * math.cos(a)) for a in [k * math.pi / 10 for k in range(20)]], 0.012, cyclic=True)
box('Ram_CannonBrake', TRIM, P(0.55, 0.02), (0.1, 0.13, 0.15), 0.025, 2)
for z in (0.077, -0.077):
    for x in (0.53, 0.57): box('Ram_CannonPort', SUIT, P(x, 0.02, z), (0.022, 0.012, 0.08), 0.004)
cyl('Ram_CannonMuzzle', TRIM, *X(0.6, 0.615), 0.055, 20)
cyl('Ram_CannonBore', GLOW, *X(0.612, 0.616), 0.035, 16)

# ---- Targeting scope on top, its lens facing down the barrel ----
box('Ram_CannonScope', TRIM, P(0.1, 0.17), (0.16, 0.06, 0.06), 0.015, 2)
for x in (0.04, 0.14): box('Ram_CannonScopeMount', ARMOUR, P(x, 0.14), (0.02, 0.03, 0.05), 0.005)
cyl('Ram_CannonLens', GLOW, P(0.18, 0.17), P(0.186, 0.17), 0.022, 16)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'ram_cannon.blend'))
print('built', len(bpy.data.objects), 'objects')
