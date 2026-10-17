# The sniper: a tall, lean marksman on stilt legs. Reverse-jointed graphite legs with hydraulic rams, ceramic thigh
# plates and shin guards (the body), a narrow torso with a glowing chest slit, a riveted yoke and a vented power pack
# under a hooded sensor head with one magenta eye, a side rangefinder and a lit antenna (torso, eye), and a long
# rifle on its near side (gun: receiver, capped scope, finned shroud, laser module, folded bipod, fluted barrel and
# muzzle brake; muzzle: the glowing tip), which the rig aims. Run: blender -b -P build_enemy_sniper.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs (body: hips 0.86 up, feet on the ground) ----
at('body')
for z in (0.12, -0.12):
    s = 1 if z > 0 else -1
    hip, knee, ankle, foot = (0, 0.88, z), (0.12, 0.52, z), (-0.06, 0.18, z), (0.04, 0.02, z)
    cyl('thigh', 'j', hip, knee, 0.07, 0.055, n=10)
    cyl('shin', 'j', knee, ankle, 0.05, 0.04, n=10)
    cyl('ankle', 'j', ankle, foot, 0.04, 0.035, n=8)
    sphere('knee', 'j', knee, 0.065, nu=10, nv=5)
    box('thighplate', 'p', (0.075, 0.7, z + s * 0.055), (0.09, 0.24, 0.03), 0.01, rot=(0, 0, 0.32), btaper=(0.7, 1.0))
    piston('ram', (-0.03, 0.84, z + s * 0.07), (0.07, 0.56, z + s * 0.065), 0.016, n=6, collar=False)
    box('kneecap', 'p', (0.17, 0.53, z), (0.06, 0.15, 0.09), 0.018, rot=(0, 0, 0.32), taper=(0.7, 0.8))
    box('shinguard', 'p', (0.075, 0.35, z), (0.05, 0.28, 0.085), 0.015, rot=(0, 0, -0.49), taper=(1.2, 1.0), btaper=(0.7, 0.8))
    box('shinstripe', 'e', (0.099, 0.35, z), (0.012, 0.12, 0.03), 0.0, rot=(0, 0, -0.49))
    prism('foot', 'p', [(-0.1, 0), (0.16, 0), (0.12, 0.05), (-0.06, 0.06)], z - 0.055, z + 0.055, 0.015)
    box('heel', 'j', (-0.11, 0.03, z), (0.05, 0.05, 0.08), 0.0, taper=(0.6, 1.0))
box('hips', 'j', (0, 0.88, 0), (0.22, 0.12, 0.34), 0.03)
for z in (0.185, -0.185): box('tasset', 'p', (0.01, 0.85, z), (0.18, 0.13, 0.03), 0.01, btaper=(0.7, 1.0))

# ---- Torso (1.05 up): a slim chest under a hooded head ----
node('torso', 'body', (0, 1.05, 0))
box('waist', 'j', (0, -0.08, 0), (0.24, 0.14, 0.3), 0.025)
box('chest', 'p', (0, 0.16, 0), (0.36, 0.34, 0.42), 0.05, taper=(1.0, 1.0), btaper=(0.75, 0.8))
box('chestplate', 'p', (0.15, 0.17, 0), (0.08, 0.24, 0.3), 0.025, btaper=(0.6, 0.7))
box('chest_slit', 'e', (0.188, 0.2, 0), (0.012, 0.12, 0.03), 0.0)
box('neck', 'j', (0.02, 0.36, 0), (0.14, 0.08, 0.16), 0.02)
box('hood', 'p', (0.02, 0.5, 0), (0.3, 0.2, 0.26), 0.05, taper=(0.7, 0.75), shift=(-0.03, 0))
box('hood_fin', 'p', (-0.08, 0.6, 0), (0.18, 0.06, 0.05), 0.015, rot=(0, 0, 0.25))
box('faceplate', 'j', (0.14, 0.5, 0), (0.06, 0.14, 0.18), 0.02, taper=(0.8, 0.8))
sensor('rangefinder', (0.12, 0.46, 0.125), 0.024, n=8)
box('antenna', 'j', (-0.1, 0.62, -0.1), (0.02, 0.22, 0.02), 0.004, rot=(0, 0, 0.3))
box('antenna_tip', 'e', (-0.133, 0.73, -0.1), (0.025, 0.025, 0.025), 0.0, rot=(0, 0, 0.3))
box('pack', 'j', (-0.2, 0.18, 0), (0.08, 0.26, 0.3), 0.02)
grille('pack_vent', (-0.245, 0.18, 0), (0.02, 0.18, 0.22), n=3, rot=(0, math.pi, 0))
for z in (0.22, -0.22):
    box('shoulder', 'p', (0.0, 0.3, z), (0.24, 0.1, 0.1), 0.025, taper=(0.75, 0.8))
    rivets('j', [(x, 0.35, z) for x in (-0.05, 0.05)], (0, 1, 0), r=0.012, h=0.008)
cyl('arm', 'j', (0.03, 0.27, 0.24), (0.12, 0.1, 0.3), 0.045, n=8)
cyl('forearm', 'j', (0.12, 0.1, 0.3), (0.3, 0.3, 0.28), 0.04, n=8)

node('eye', 'torso', (0.2, 0.52, 0))
sphere('lens', 'e', (0, 0, 0), (0.05, 0.05, 0.05), nu=12, nv=8)

# ---- The rifle (gun: its pivot at the receiver; the barrel runs to 1.5 along x) ----
node('gun', 'torso', (0.05, 0.3, 0.26))
box('receiver', 'p', (0.1, 0, 0), (0.42, 0.13, 0.1), 0.025, taper=(0.85, 1.0))
box('receiver_seam', 'j', (0.12, 0.0, 0.0), (0.3, 0.02, 0.104), 0.0)
box('stock', 'j', (-0.16, -0.02, 0), (0.18, 0.1, 0.07), 0.02, taper=(0.7, 1.0))
box('buttpad', 'p', (-0.26, -0.02, 0), (0.03, 0.11, 0.075), 0.008)
box('grip', 'j', (0.02, -0.1, 0), (0.06, 0.12, 0.06), 0.012, rot=(0, 0, 0.3))
box('mag', 'p', (0.14, -0.1, 0), (0.08, 0.1, 0.06), 0.012)
cyl('scope', 'j', (0.02, 0.11, 0), (0.3, 0.11, 0), 0.035, n=10)
cyl('scope_cap', 'p', (0.25, 0.11, 0), (0.3, 0.11, 0), 0.044, n=10)
cyl('scope_lens', 'e', (0.3, 0.11, 0), (0.31, 0.11, 0), 0.03, n=10)
box('scope_mount', 'j', (0.16, 0.07, 0), (0.08, 0.04, 0.04), 0.008)
cyl('barrel', 'j', (0.3, 0, 0), (1.38, 0, 0), 0.04, 0.032, n=10)
box('shroud', 'p', (0.5, 0, 0), (0.3, 0.09, 0.08), 0.02, rot=(0, 0, 0))
fins('fin', 'j', (0.5, 0.06, 0), (0.07, 0.035, 0.26), 5, rot=(0, math.pi / 2, 0))
box('laser', 'j', (0.55, -0.065, 0), (0.1, 0.04, 0.04), 0.0)
box('laser_eye', 'e', (0.603, -0.065, 0), (0.008, 0.024, 0.024), 0.0)
box('bipod_hinge', 'j', (0.74, -0.04, 0), (0.04, 0.03, 0.07), 0.0)
for z in (0.025, -0.025): cyl('bipod', 'j', (0.74, -0.05, z), (1.04, -0.055, z * 1.2), 0.012, n=4)
for x in (0.75, 0.95, 1.15): cyl('ring', 'p', (x, 0, 0), (x + 0.04, 0, 0), 0.05, n=10)
box('brake', 'j', (1.42, 0, 0), (0.1, 0.08, 0.08), 0.015)
for k in range(2): box('brake_slot', 'e', (1.4 + k * 0.04, 0, 0), (0.015, 0.084, 0.06), 0.003)

node('muzzle', 'gun', (1.52, 0, 0))
sphere('tip', 'e', (0, 0, 0), 0.05, nu=10, nv=6)

save(out_dir(), 'sniper')
