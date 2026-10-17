# The drone: a hovering saucer scout. A ridged ceramic hull split by a graphite seam ring, a graphite skirt with a
# glowing belly ring, a lift thruster underneath and three folded landing claws, side fins with trim thrusters, a
# sensor pip either side of its eye (core, which banks as it flies; its eye a mesh of its own at the nose), and a
# ducted rotor with lit duct tips on a mast above (rotor, which the rig spins).
# Run: blender -b -P build_enemy_drone.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
node('core', 'body', (0, 0.3, 0))
lathe('hull', 'p', [(0.0, 0.21), (0.16, 0.2), (0.29, 0.14), (0.36, 0.04), (0.36, 0.0)], n=16, sharp=40, bev=0.01)
lathe('seam', 'j', [(0.28, 0.125), (0.305, 0.135), (0.26, 0.17), (0.22, 0.176)], n=16, sharp=30)
lathe('skirt', 'j', [(0.36, 0.0), (0.37, -0.02), (0.3, -0.12), (0.16, -0.18), (0.0, -0.19)], n=16, sharp=40)
lathe('ridge', 'j', [(0.36, 0.03), (0.385, 0.03), (0.385, -0.01), (0.36, -0.01)], n=16, sharp=30)
torus('belly', 'e', (0, -0.14, 0), 0.22, 0.018, nu=20, nv=4)
thruster('lift', (0, -0.16, 0), 0.085, 0.08, n=10)
lathe('mast', 'j', [(0.07, 0.18), (0.06, 0.26), (0.0, 0.26)], n=10, sharp=40)
cyl('socket', 'j', (0.24, -0.02, 0), (0.33, -0.02, 0), 0.11, 0.12, n=14)
for z in (0.13, -0.13): sensor('pip', (0.29, -0.075, z), 0.02, rot=(0, -0.42 if z > 0 else 0.42, 0), n=6)
for z in (0.26, -0.26):
    prism('fin', 'j', [(-0.3, -0.04), (0.02, -0.06), (-0.04, 0.04), (-0.26, 0.05)], z - 0.025, z + 0.025, 0.012)
    prism('fin_cap', 'p', [(-0.28, 0.01), (-0.05, 0.02), (-0.04, 0.04), (-0.26, 0.05)], z - 0.028, z + 0.028, 0.0)
    box('fin_light', 'e', (-0.16, -0.01, z * 1.11), (0.14, 0.02, 0.01), 0.0)
    thruster('trim', (-0.27, 0.0, z), 0.032, 0.06, rot=(0, 0, -math.pi / 2), n=8)
for k in range(3):
    a = math.pi + (k - 1) * 0.5
    box('vent', 'e', (math.cos(a) * 0.3, 0.12, -math.sin(a) * 0.3), (0.02, 0.02, 0.07), 0.0, rot=(0, a, 0.6))
for k in range(3):                                     # landing claws, folded up under the skirt
    a = k * TAU / 3 + math.pi / 3
    c, s = math.cos(a), -math.sin(a)
    tube('claw', 'j', [(c * 0.22, -0.13, s * 0.22), (c * 0.3, -0.17, s * 0.3), (c * 0.32, -0.25, s * 0.32)], 0.018, n=5)
    box('claw_pad', 'p', (c * 0.32, -0.26, s * 0.32), (0.06, 0.025, 0.04), 0.0, rot=(0, a, 0))

node('eye', 'core', (0.31, -0.02, 0))
sphere('lens', 'e', (0.01, 0, 0), (0.06, 0.085, 0.085), nu=14, nv=8)

node('rotor', 'core', (0, 0.24, 0))
torus('duct', 'j', (0, 0, 0), 0.44, 0.035, nu=28, nv=6)
lathe('hub', 'p', [(0.0, 0.05), (0.06, 0.04), (0.08, 0.0), (0.06, -0.03)], n=10, sharp=40)
cyl('hub_light', 'e', (0, 0.045, 0), (0, 0.055, 0), 0.03, n=8)
for k in range(3):
    a = k * math.pi / 3
    for s in (1, -1):
        box('blade', 'p', (math.cos(a) * s * 0.23, 0.005, -math.sin(a) * s * 0.23), (0.36, 0.018, 0.075), 0.006, rot=(s * 0.18, a, 0), taper=(1, 0.8))
for k in range(3):
    a = k * TAU / 3 + 0.5
    box('strut', 'j', (math.cos(a) * 0.4, 0, -math.sin(a) * 0.4), (0.1, 0.03, 0.04), 0.008, rot=(0, a, 0))
    box('duct_light', 'e', (math.cos(a + 1.05) * 0.44, 0, -math.sin(a + 1.05) * 0.44), (0.05, 0.08, 0.08), 0.0, rot=(0, a + 1.05, 0))

save(out_dir(), 'drone')
