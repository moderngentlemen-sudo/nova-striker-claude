# The shield guard: a squat, broad-shouldered sentinel behind a tall tower shield. Thick gunmetal legs on hinge drums
# with exposed rams, armoured thighs, knee guards, greaves and rubber-soled boots (the body), a hunched armoured torso
# with a glowing chest plate, layered pauldrons, a louvred back pack with cables up to the helmet, a helmet brow over
# its visor and an ear lens cluster (torso, eye) and a near arm on a hydraulic ram and an elbow drum braced against
# the shield (shield: a faceted ceramic slab faced with two layered plates and panels split by a riveted seam, a
# vision slit, glowing edge rails, a crest and a rubber grip behind). Run: blender -b -P
# build_enemy_shield.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs (on the body: from the hips at 0.82 to the ground) ----
at('body')
for z in (0.16, -0.16):
    s = 1 if z > 0 else -1
    cyl('thigh', 'm', (0, 0.84, z), (0.02, 0.46, z), 0.09, 0.075, n=10)
    cyl('shin', 'm', (0.02, 0.46, z), (0, 0.1, z), 0.075, 0.065, n=10)
    joint('knee', (0.02, 0.46, z), 0.095, 0.17, bolts=6 if s > 0 else 0)
    piston('ram', (-0.1, 0.78, z + s * 0.02), (-0.08, 0.2, z + s * 0.02), 0.026, n=8)
    armour('thighplate', (0.04, 0.66, z + s * 0.098), (0.16, 0.26, 0.035), out=(0, 0, s), bev=0.012, lip=0.01, btaper=(0.7, 1.0))
    armour('thighfront', (0.1, 0.68, z), (0.04, 0.22, 0.15), out=(1, 0, 0), bev=0.012, lip=0.01, btaper=(0.7, 0.9))
    box('kneeguard', 'p', (0.09, 0.47, z), (0.1, 0.18, 0.15), 0.025, taper=(0.7, 0.8))
    armour('greave', (0.075, 0.27, z), (0.08, 0.24, 0.14), out=(1, 0, 0), bev=0.02, lip=0.012, taper=(1.2, 1.0))
    box('greave_slit', 'e', (0.119, 0.28, z), (0.012, 0.1, 0.03), 0.0)
    box('boot', 'p', (0.05, 0.06, z), (0.32, 0.12, 0.17), 0.03, taper=(0.75, 0.9), shift=(-0.03, 0))
    box('sole', 'r', (0.05, 0.012, z), (0.34, 0.03, 0.19), 0.008)
    box('boot_strap', 'm', (0.0, 0.1, z), (0.04, 0.06, 0.175), 0.006)
box('hips', 'j', (0, 0.84, 0), (0.3, 0.14, 0.44), 0.03)

# ---- Torso (1.0 up): hunched block, shoulders, brow, the near arm bracing the shield ----
node('torso', 'body', (0, 1.0, 0))
box('waist', 'j', (-0.02, -0.08, 0), (0.4, 0.16, 0.46), 0.03, taper=(1.1, 1.1))
for y in (-0.12, -0.06): box('waist_rib', 'm', (-0.02, y, 0), (0.42, 0.02, 0.48), 0.006)
box('chest', 'p', (0.0, 0.22, 0), (0.52, 0.44, 0.58), 0.06, taper=(1.04, 1.0), btaper=(0.85, 0.85))
armour('chestplate', (0.225, 0.2, 0), (0.12, 0.34, 0.42), out=(1, 0, 0), bev=0.035, lip=0.016, taper=(1.2, 1.0), btaper=(0.7, 0.8))
box('chest_light', 'e', (0.29, 0.26, 0), (0.008, 0.03, 0.26), 0.0, taper=(1.0, 1.0))
box('collar', 'm', (-0.02, 0.46, 0), (0.44, 0.08, 0.46), 0.02)
box('helm', 'p', (0.06, 0.57, 0), (0.36, 0.16, 0.32), 0.04, taper=(0.75, 0.8))
box('brow', 'p', (0.22, 0.63, 0), (0.1, 0.05, 0.34), 0.015, rot=(0, 0, -0.35))
box('jaw', 'm', (0.2, 0.52, 0), (0.08, 0.06, 0.26), 0.015)
lenses('ear', (0.04, 0.56, 0.16), [(0.0, 0.0, 0.03), (0.04, -0.04, 0.016)], rot=(0, -math.pi / 2, 0))
box('backpack', 'j', (-0.28, 0.3, 0), (0.1, 0.36, 0.38), 0.025)
grille('backvent', (-0.335, 0.3, 0), (0.02, 0.28, 0.24), n=4, rot=(0, math.pi, 0))
for zz in (0.15, -0.15): cables('backcable', (-0.3, 0.46, zz), (-0.26, 0.56, zz * 1.2), (-0.12, 0.6, zz * 0.9), 0.016, k=1, clamps=(0.45,))
for z in (0.33, -0.33):
    armour('pauldron', (0.0, 0.43, z), (0.4, 0.14, 0.18), out=(0, 1, 0), bev=0.04, lip=0.016, taper=(0.8, 0.7))
    box('pauldron_rim', 'm', (0.0, 0.34, z * 1.04), (0.38, 0.04, 0.17), 0.012)
    rivets('j', [(x, 0.49, z) for x in (-0.1, 0.1)], (0, 1, 0), r=0.014, h=0.01)
for z in (0.26,):
    cyl('upperarm', 'm', (0.04, 0.32, z + 0.06), (0.28, 0.1, z + 0.08), 0.065, n=10)
    piston('ram', (0.07, 0.36, z + 0.14), (0.25, 0.18, z + 0.15), 0.024, n=8)
    joint('elbow', (0.28, 0.1, z + 0.08), 0.075, 0.13, bolts=5)
    cyl('forearm', 'm', (0.28, 0.1, z + 0.08), (0.48, 0.12, 0.14), 0.065, n=10)
    armour('bracer', (0.38, 0.12, z + 0.01), (0.2, 0.11, 0.1), out=(0, 0.5, 0.86), bev=0.02, lip=0.01, rot=(0, 0.4, 0))

node('eye', 'torso', (0.28, 0.58, 0))
box('visor', 'e', (-0.005, 0, 0), (0.05, 0.05, 0.28), 0.01, taper=(1, 0.8))

# ---- The tower shield (0.55 ahead of the torso centre) ----
node('shield', 'torso', (0.55, 0.1, 0))
prism('slab', 'p', [(-0.4, -0.78), (0.4, -0.78), (0.52, -0.6), (0.52, 0.64), (0.42, 0.78), (-0.42, 0.78), (-0.52, 0.64), (-0.52, -0.6)],
      -0.07, 0.07, 0.03, rot=(0, math.pi / 2, 0))   # (outline in y and z, extruded along x: the shield's thickness)
armour('face', (0.07, 0.27, 0), (0.06, 0.74, 0.8), out=(1, 0, 0), bev=0.03, lip=0.012, taper=(0.8, 1.0))
armour('face_low', (0.07, -0.39, 0), (0.06, 0.5, 0.8), out=(1, 0, 0), bev=0.03, lip=0.012, taper=(0.8, 1.0))
for z in (0.2, -0.2): panel('face_panel', (0.1, -0.39, z), (0.012, 0.32, 0.26))
box('seam', 'm', (0.065, -0.12, 0), (0.06, 0.04, 0.8), 0.006)
rivets('c', [(0.099, y, z) for y in (0.56, -0.58) for z in (0.33, -0.33)], (1, 0, 0), r=0.016, h=0.012)
box('slit', 'm', (0.098, 0.52, 0), (0.02, 0.05, 0.3), 0.006)
box('slit_glow', 'e', (0.106, 0.52, 0), (0.012, 0.022, 0.24), 0.0)
box('boss', 'm', (0.1, 0.12, 0), (0.06, 0.34, 0.3), 0.02, taper=(0.6, 0.7))
box('crest', 'e', (0.135, 0.12, 0), (0.02, 0.2, 0.06), 0.006)
for y in (0.12 - 0.11, 0.12 + 0.11): box('crest_bar', 'e', (0.13, y, 0), (0.02, 0.03, 0.16), 0.006)
for y in (-0.7, 0.7): box('rail_h', 'e', (0.04, y, 0), (0.07, 0.04, 0.9), 0.008)
for z in (-0.46, 0.46): box('rail_v', 'e', (0.04, 0, z), (0.07, 1.26, 0.04), 0.008)
box('grip', 'r', (-0.1, 0, 0.14), (0.08, 0.36, 0.08), 0.02)
box('backplate', 'm', (-0.085, 0, 0), (0.04, 1.2, 0.7), 0.012)
for y in (-0.4, 0.4): box('brace', 'm', (-0.11, y, 0), (0.03, 0.08, 0.6), 0.008)

save(out_dir(), 'shield')
