# The sniper: a tall, lean marksman on stilt legs. Reverse-jointed gunmetal legs on hinge drums with exposed rams,
# ceramic thigh plates and shin guards over undercuts and rubber-soled feet (the body), a narrow torso with a glowing
# chest slit, a riveted yoke, a louvred power pack with cable runs up to a hooded sensor head with one magenta eye, a
# side lens cluster and a lit antenna (torso, eye), and a long rifle on its near side (gun: receiver with a chrome
# bolt, a glass-capped scope, a finned shroud, a laser module, a folded bipod, a fluted barrel and a louvred muzzle
# brake; muzzle: the glowing tip), which the rig aims. Run: blender -b -P build_enemy_sniper.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs (body: hips 0.86 up, feet on the ground) ----
at('body')
for z in (0.12, -0.12):
    s = 1 if z > 0 else -1
    hip, knee, ankle, foot = (0, 0.88, z), (0.12, 0.52, z), (-0.06, 0.18, z), (0.04, 0.02, z)
    cyl('thigh', 'm', hip, knee, 0.06, 0.048, n=10)
    cyl('shin', 'm', knee, ankle, 0.045, 0.036, n=10)
    cyl('ankle', 'm', ankle, (0.03, 0.06, z), 0.034, 0.03, n=8)
    joint('hip', hip, 0.08, 0.14, bolts=0)
    joint('knee', knee, 0.065, 0.13, bolts=0)
    cyl('ankle_drum', 'm', (ankle[0], ankle[1], z - 0.05), (ankle[0], ankle[1], z + 0.05), 0.045, n=10)
    armour('thighplate', (0.08, 0.7, z + s * 0.06), (0.1, 0.26, 0.03), out=(0, 0, s), bev=0.01, lip=0.008, rot=(0, 0, 0.32), btaper=(0.7, 1.0))
    piston('ram', (-0.04, 0.84, z + s * 0.075), (0.06, 0.55, z + s * 0.07), 0.021, n=8)
    box('kneecap', 'p', (0.17, 0.53, z), (0.07, 0.16, 0.1), 0.02, rot=(0, 0, 0.32), taper=(0.7, 0.8))
    armour('shinguard', (0.08, 0.35, z), (0.055, 0.3, 0.09), out=(0.9, 0.44, 0), bev=0.016, lip=0.01, rot=(0, 0, -0.49), taper=(1.2, 1.0), btaper=(0.7, 0.8))
    box('shinstripe', 'e', (0.107, 0.35, z), (0.012, 0.13, 0.03), 0.0, rot=(0, 0, -0.49))
    prism('foot', 'p', [(-0.1, 0.02), (0.16, 0.02), (0.13, 0.06), (-0.06, 0.07)], z - 0.055, z + 0.055, 0.015)
    prism('sole', 'r', [(-0.11, 0.0), (0.17, 0.0), (0.165, 0.025), (-0.105, 0.025)], z - 0.06, z + 0.06, 0.006)
    box('toe_light', 'e', (0.12, 0.05, z + s * 0.056), (0.05, 0.01, 0.006), 0.0, rot=(0, 0, -0.3))
    box('heel', 'm', (-0.11, 0.04, z), (0.05, 0.05, 0.08), 0.0, taper=(0.6, 1.0))
box('hips', 'j', (0, 0.88, 0), (0.22, 0.12, 0.34), 0.03)
for z in (0.19, -0.19): armour('tasset', (0.01, 0.85, z), (0.18, 0.13, 0.03), out=(0, 0, 1 if z > 0 else -1), bev=0.01, lip=0.008, btaper=(0.7, 1.0))

# ---- Torso (1.05 up): a slim chest under a hooded head ----
node('torso', 'body', (0, 1.05, 0))
box('waist', 'j', (0, -0.08, 0), (0.24, 0.14, 0.3), 0.025)
for y in (-0.11, -0.05): box('waist_rib', 'm', (0, y, 0), (0.26, 0.02, 0.32), 0.006)
box('chest', 'p', (0, 0.16, 0), (0.36, 0.34, 0.42), 0.05, taper=(1.0, 1.0), btaper=(0.75, 0.8))
box('chest_seam', 'j', (0.0, 0.07, 0), (0.33, 0.015, 0.4), 0.0, btaper=(0.9, 0.95))
armour('chestplate', (0.155, 0.17, 0), (0.08, 0.24, 0.3), out=(1, 0, 0), bev=0.025, lip=0.012, btaper=(0.6, 0.7))
box('chest_slit', 'e', (0.197, 0.2, 0), (0.012, 0.12, 0.03), 0.0)
rivets('m', [(0.19, y, zz) for y in (0.26,) for zz in (0.1, -0.1)], (1, 0, 0), r=0.011, h=0.008)
cyl('neck', 'm', (0.02, 0.32, 0), (0.02, 0.42, 0), 0.06, n=12)
for y in (0.35, 0.39): cyl('neck_ring', 'j', (0.02, y - 0.008, 0), (0.02, y + 0.008, 0), 0.068, n=12)
box('hood', 'p', (0.02, 0.5, 0), (0.3, 0.2, 0.26), 0.055, taper=(0.7, 0.75), shift=(-0.03, 0))
box('hood_under', 'm', (0.02, 0.42, 0), (0.26, 0.04, 0.22), 0.01)
box('hood_fin', 'p', (-0.08, 0.6, 0), (0.18, 0.06, 0.05), 0.015, rot=(0, 0, 0.25))
box('faceplate', 'm', (0.14, 0.5, 0), (0.06, 0.14, 0.18), 0.02, taper=(0.8, 0.8))
lenses('rangefinder', (0.12, 0.47, 0.13), [(0.0, 0.0, 0.024), (-0.04, -0.012, 0.014)], plate=(0.04, 0.1, 0.06), rot=(0, -0.6, 0))
cyl('antenna_base', 'm', (-0.1, 0.58, -0.1), (-0.105, 0.62, -0.1), 0.02, n=8)
cyl('antenna', 'm', (-0.1, 0.6, -0.1), (-0.165, 0.83, -0.1), 0.01, 0.006, n=5)
box('antenna_tip', 'e', (-0.165, 0.83, -0.1), (0.025, 0.025, 0.025), 0.0)
box('pack', 'j', (-0.2, 0.18, 0), (0.08, 0.26, 0.3), 0.02)
grille('pack_vent', (-0.245, 0.18, 0), (0.02, 0.18, 0.22), n=4, rot=(0, math.pi, 0))
for z in (0.11, -0.11):                                # cable runs from the pack up into the hood
    cables('pack_cable', (-0.22, 0.31, z), (-0.2, 0.44, z * 1.1), (-0.1, 0.48, z * 0.7), 0.014, k=1, clamps=(0.5,))
for z in (0.22, -0.22):
    armour('shoulder', (0.0, 0.3, z), (0.24, 0.1, 0.1), out=(0, 1, 0), bev=0.025, lip=0.012, taper=(0.75, 0.8))
    rivets('m', [(x, 0.35, z) for x in (-0.05, 0.05)], (0, 1, 0), r=0.012, h=0.008)
joint('shoulder_drum', (0.03, 0.27, 0.25), 0.05, 0.06, bolts=0)
cyl('arm', 'm', (0.03, 0.27, 0.24), (0.12, 0.1, 0.3), 0.04, n=10)
joint('elbow', (0.12, 0.1, 0.3), 0.04, 0.06, bolts=0)
cyl('forearm', 'm', (0.12, 0.1, 0.3), (0.3, 0.3, 0.28), 0.036, n=10)
box('bracer', 'p', (0.2, 0.19, 0.3), (0.14, 0.06, 0.07), 0.015, rot=(0, 0, 0.83))

node('eye', 'torso', (0.2, 0.52, 0))
sphere('lens', 'e', (0, 0, 0), (0.05, 0.05, 0.05), nu=12, nv=8)

# ---- The rifle (gun: its pivot at the receiver; the barrel runs to 1.5 along x) ----
node('gun', 'torso', (0.05, 0.3, 0.26))
box('receiver', 'p', (0.1, 0, 0), (0.42, 0.13, 0.1), 0.028, taper=(0.85, 1.0))
box('receiver_seam', 'j', (0.12, 0.0, 0.0), (0.3, 0.02, 0.104), 0.0)
box('ejection', 'm', (0.14, 0.03, 0.05), (0.12, 0.035, 0.012), 0.004)
cyl('bolt', 'c', (0.06, 0.03, 0.05), (0.06, 0.03, 0.09), 0.012, n=8)
sphere('bolt_knob', 'c', (0.06, 0.03, 0.095), 0.018, nu=8, nv=5)
box('stock', 'j', (-0.16, -0.02, 0), (0.18, 0.1, 0.07), 0.02, taper=(0.7, 1.0))
box('stock_frame', 'm', (-0.16, 0.035, 0), (0.18, 0.02, 0.05), 0.004)
box('buttpad', 'r', (-0.26, -0.02, 0), (0.03, 0.11, 0.075), 0.008)
box('grip', 'r', (0.02, -0.1, 0), (0.06, 0.12, 0.06), 0.014, rot=(0, 0, 0.3))
box('mag', 'p', (0.14, -0.1, 0), (0.08, 0.1, 0.06), 0.014)
box('mag_light', 'e', (0.14, -0.1, 0.031), (0.05, 0.012, 0.004), 0.0)
cyl('scope', 'm', (0.02, 0.11, 0), (0.3, 0.11, 0), 0.035, n=12)
cyl('scope_eyecup', 'r', (0.0, 0.11, 0), (0.04, 0.11, 0), 0.042, 0.038, n=12)
for x in (0.11, 0.16): cyl('scope_turret', 'm', (x, 0.14, 0), (x, 0.16, 0), 0.016, n=8)
cyl('scope_cap', 'p', (0.25, 0.11, 0), (0.3, 0.11, 0), 0.046, n=12)
cyl('scope_glass', 'l', (0.3, 0.11, 0), (0.306, 0.11, 0), 0.036, n=12)
cyl('scope_lens', 'e', (0.306, 0.11, 0), (0.309, 0.11, 0), 0.012, n=8)
for x in (0.08, 0.22): box('scope_mount', 'm', (x, 0.07, 0), (0.04, 0.04, 0.05), 0.008)
cyl('barrel', 'm', (0.3, 0, 0), (1.38, 0, 0), 0.04, 0.032, n=12)
armour('shroud', (0.5, 0, 0), (0.3, 0.09, 0.08), out=(0, 1, 0), bev=0.02, lip=0.01)
fins('fin', 'm', (0.5, 0.06, 0), (0.07, 0.035, 0.26), 5, rot=(0, math.pi / 2, 0))
box('laser', 'm', (0.55, -0.065, 0), (0.1, 0.04, 0.04), 0.008)
box('laser_eye', 'e', (0.603, -0.065, 0), (0.008, 0.024, 0.024), 0.0)
box('bipod_hinge', 'm', (0.74, -0.04, 0), (0.04, 0.03, 0.07), 0.0)
for z in (0.025, -0.025):
    cyl('bipod', 'm', (0.74, -0.05, z), (1.04, -0.055, z * 1.2), 0.012, n=6)
    box('bipod_foot', 'r', (1.05, -0.055, z * 1.2), (0.03, 0.025, 0.02), 0.0)
for x in (0.75, 0.95): cyl('ring', 'p', (x, 0, 0), (x + 0.04, 0, 0), 0.05, n=12)
cyl('brake_base', 'm', (1.34, 0, 0), (1.38, 0, 0), 0.046, n=12)
grille('brake', (1.425, 0, 0), (0.1, 0.085, 0.085), n=2, rot=(0, 0, 0), m='m', frame=False)
for z in (0.044, -0.044): box('brake_side', 'm', (1.425, 0, z), (0.1, 0.09, 0.012), 0.004)

node('muzzle', 'gun', (1.52, 0, 0))
sphere('tip', 'e', (0, 0, 0), 0.05, nu=10, nv=6)

save(out_dir(), 'sniper')
