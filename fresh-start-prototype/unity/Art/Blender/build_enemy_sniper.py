# The sniper: a tall, lean marksman on stilt legs. Reverse-jointed graphite legs with ceramic shin guards (the body),
# a narrow torso under a hooded sensor head with one magenta eye (torso, eye), and a long rifle on its near side
# (gun: receiver, scope, fluted barrel and muzzle brake; muzzle: the glowing tip), which the rig aims.
# Run: blender -b -P build_enemy_sniper.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs (body: hips 0.86 up, feet on the ground) ----
at('body')
for z in (0.12, -0.12):
    hip, knee, ankle, foot = (0, 0.88, z), (0.12, 0.52, z), (-0.06, 0.18, z), (0.04, 0.02, z)
    cyl('thigh', 'j', hip, knee, 0.07, 0.055, n=10)
    cyl('shin', 'j', knee, ankle, 0.05, 0.04, n=10)
    cyl('ankle', 'j', ankle, foot, 0.04, 0.035, n=8)
    sphere('knee', 'j', knee, 0.065, nu=10, nv=6)
    box('kneecap', 'p', (0.17, 0.53, z), (0.06, 0.15, 0.09), 0.018, rot=(0, 0, 0.32), taper=(0.7, 0.8))
    box('shinguard', 'p', (0.075, 0.35, z), (0.05, 0.28, 0.085), 0.015, rot=(0, 0, -0.49), taper=(1.2, 1.0), btaper=(0.7, 0.8))
    prism('foot', 'p', [(-0.1, 0), (0.16, 0), (0.12, 0.05), (-0.06, 0.06)], z - 0.055, z + 0.055, 0.015)
box('hips', 'j', (0, 0.88, 0), (0.22, 0.12, 0.34), 0.03)

# ---- Torso (1.05 up): a slim chest under a hooded head ----
node('torso', 'body', (0, 1.05, 0))
box('waist', 'j', (0, -0.08, 0), (0.24, 0.14, 0.3), 0.025)
box('chest', 'p', (0, 0.16, 0), (0.36, 0.34, 0.42), 0.05, taper=(1.0, 1.0), btaper=(0.75, 0.8))
box('chestplate', 'p', (0.15, 0.17, 0), (0.08, 0.24, 0.3), 0.025, btaper=(0.6, 0.7))
box('neck', 'j', (0.02, 0.36, 0), (0.14, 0.08, 0.16), 0.02)
box('hood', 'p', (0.02, 0.5, 0), (0.3, 0.2, 0.26), 0.05, taper=(0.7, 0.75), shift=(-0.03, 0))
box('hood_fin', 'p', (-0.08, 0.6, 0), (0.18, 0.06, 0.05), 0.015, rot=(0, 0, 0.25))
box('faceplate', 'j', (0.14, 0.5, 0), (0.06, 0.14, 0.18), 0.02, taper=(0.8, 0.8))
box('antenna', 'j', (-0.1, 0.62, -0.1), (0.02, 0.22, 0.02), 0.004, rot=(0, 0, 0.3))
box('pack', 'j', (-0.2, 0.18, 0), (0.08, 0.26, 0.3), 0.02)
for z in (0.22, -0.22):
    box('shoulder', 'p', (0.0, 0.3, z), (0.24, 0.1, 0.1), 0.025, taper=(0.75, 0.8))
cyl('arm', 'j', (0.03, 0.27, 0.24), (0.12, 0.1, 0.3), 0.045, n=8)
cyl('forearm', 'j', (0.12, 0.1, 0.3), (0.3, 0.3, 0.28), 0.04, n=8)

node('eye', 'torso', (0.2, 0.52, 0))
sphere('lens', 'e', (0, 0, 0), (0.05, 0.05, 0.05), nu=12, nv=8)

# ---- The rifle (gun: its pivot at the receiver; the barrel runs to 1.5 along x) ----
node('gun', 'torso', (0.05, 0.3, 0.26))
box('receiver', 'p', (0.1, 0, 0), (0.42, 0.13, 0.1), 0.025, taper=(0.85, 1.0))
box('stock', 'j', (-0.16, -0.02, 0), (0.18, 0.1, 0.07), 0.02, taper=(0.7, 1.0))
box('grip', 'j', (0.02, -0.1, 0), (0.06, 0.12, 0.06), 0.012, rot=(0, 0, 0.3))
box('mag', 'p', (0.14, -0.1, 0), (0.08, 0.1, 0.06), 0.012)
cyl('scope', 'j', (0.02, 0.11, 0), (0.3, 0.11, 0), 0.035, n=10)
cyl('scope_lens', 'e', (0.3, 0.11, 0), (0.31, 0.11, 0), 0.03, n=10)
box('scope_mount', 'j', (0.16, 0.07, 0), (0.08, 0.04, 0.04), 0.008)
cyl('barrel', 'j', (0.3, 0, 0), (1.38, 0, 0), 0.04, 0.032, n=10)
box('shroud', 'p', (0.5, 0, 0), (0.3, 0.09, 0.08), 0.02, rot=(0, 0, 0))
for x in (0.75, 0.95, 1.15): cyl('ring', 'p', (x, 0, 0), (x + 0.04, 0, 0), 0.05, n=10)
box('brake', 'j', (1.42, 0, 0), (0.1, 0.08, 0.08), 0.015)
for k in range(2): box('brake_slot', 'e', (1.4 + k * 0.04, 0, 0), (0.015, 0.084, 0.06), 0.003)

node('muzzle', 'gun', (1.52, 0, 0))
sphere('tip', 'e', (0, 0, 0), 0.05, nu=10, nv=6)

save(out_dir(), 'sniper')
