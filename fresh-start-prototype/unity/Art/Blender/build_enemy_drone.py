# The drone: a hovering saucer scout. A ridged ceramic hull split by a gunmetal seam ring and a bolted ridge, power
# leads up into its rotor mast, louvred vents, a graphite skirt with a glowing belly ring, a lift thruster underneath
# and three folded, hinged landing claws on rubber pads, riveted side fins with trim thrusters, a chin lens cluster and
# a sensor pip either side of its chrome-rimmed eye (core, which banks as it flies; its eye a mesh of its own at the
# nose), and a ducted rotor with a rubber-banded duct, a chrome hub and lit duct tips on a mast above (rotor, which
# the rig spins). Run: blender -b -P build_enemy_drone.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
node('core', 'body', (0, 0.3, 0))
lathe('hull', 'p', [(0.0, 0.21), (0.16, 0.2), (0.29, 0.14), (0.36, 0.04), (0.36, 0.0)], n=16, sharp=40, bev=0.01)
lathe('seam', 'm', [(0.28, 0.125), (0.305, 0.135), (0.26, 0.17), (0.22, 0.176)], n=16, sharp=30)
lathe('skirt', 'j', [(0.36, 0.0), (0.37, -0.02), (0.3, -0.12), (0.16, -0.18), (0.0, -0.19)], n=16, sharp=40)
lathe('ridge', 'm', [(0.36, 0.03), (0.385, 0.03), (0.385, -0.01), (0.36, -0.01)], n=16, sharp=30)
bolt_ring((0, 0.031, 0), 0.372, 12, (0, 1, 0), r=0.009, h=0.007, a0=0.13)
torus('belly', 'e', (0, -0.14, 0), 0.22, 0.018, nu=20, nv=4)
thruster('lift', (0, -0.16, 0), 0.085, 0.08, n=10)
lathe('mast', 'm', [(0.09, 0.17), (0.08, 0.19), (0.065, 0.2), (0.06, 0.26), (0.0, 0.26)], n=10, sharp=40)
for k in range(3):                                     # power leads from the hull up into the mast
    a = k * TAU / 3 + 0.9
    tube('lead', 'r', bez((math.cos(a) * 0.17, 0.19, -math.sin(a) * 0.17), (math.cos(a) * 0.1, 0.25, -math.sin(a) * 0.1), (math.cos(a) * 0.055, 0.25, -math.sin(a) * 0.055), 6), 0.012, n=6)
cyl('socket', 'm', (0.24, -0.02, 0), (0.33, -0.02, 0), 0.11, 0.12, n=14, bev=0.01)
torus('socket_rim', 'c', (0.33, -0.02, 0), 0.116, 0.012, nu=16, nv=4, rot=(0, 0, math.pi / 2))
for z in (0.13, -0.13): sensor('pip', (0.29, -0.075, z), 0.022, rot=(0, -0.42 if z > 0 else 0.42, 0), n=10)
lenses('chin', (0.24, -0.15, 0), [(0.0, 0.03, 0.02), (0.0, -0.03, 0.02)], rot=(0, 0, -0.6), plate=(0.04, 0.05, 0.11))
for z in (0.26, -0.26):
    prism('fin', 'm', [(-0.3, -0.04), (0.02, -0.06), (-0.04, 0.04), (-0.26, 0.05)], z - 0.025, z + 0.025, 0.012)
    prism('fin_cap', 'p', [(-0.28, 0.01), (-0.05, 0.02), (-0.04, 0.045), (-0.26, 0.055)], z - 0.03, z + 0.03, 0.008)
    rivets('m', [(x, -0.025, z + (0.026 if z > 0 else -0.026)) for x in (-0.24, -0.08)], (0, 0, 1 if z > 0 else -1), r=0.009, h=0.006)
    box('fin_light', 'e', (-0.16, -0.01, z * 1.11), (0.14, 0.02, 0.01), 0.0)
    thruster('trim', (-0.27, 0.0, z), 0.032, 0.06, rot=(0, 0, -math.pi / 2), n=8)
for k in range(3):
    a = math.pi + (k - 1) * 0.5
    grille('vent', (math.cos(a) * 0.3, 0.12, -math.sin(a) * 0.3), (0.02, 0.05, 0.08), n=2, rot=(0, a, 0.6), frame=False)
for k in range(3):                                     # landing claws, folded up under the skirt
    a = k * TAU / 3 + math.pi / 3
    c, s = math.cos(a), -math.sin(a)
    cyl('claw_hinge', 'm', (c * 0.22 - s * 0.03, -0.13, s * 0.22 + c * 0.03), (c * 0.22 + s * 0.03, -0.13, s * 0.22 - c * 0.03), 0.026, n=10)
    cyl('claw', 'm', (c * 0.22, -0.13, s * 0.22), (c * 0.3, -0.18, s * 0.3), 0.018, 0.016, n=8)
    cyl('claw_shin', 'c', (c * 0.3, -0.18, s * 0.3), (c * 0.32, -0.25, s * 0.32), 0.011, n=8)
    sphere('claw_knee', 'm', (c * 0.3, -0.18, s * 0.3), 0.022, nu=8, nv=5)
    box('claw_pad', 'r', (c * 0.32, -0.26, s * 0.32), (0.06, 0.025, 0.045), 0.008, rot=(0, a, 0))

node('eye', 'core', (0.31, -0.02, 0))
sphere('lens', 'e', (0.01, 0, 0), (0.06, 0.085, 0.085), nu=14, nv=8)

node('rotor', 'core', (0, 0.24, 0))
torus('duct', 'm', (0, 0, 0), 0.44, 0.035, nu=28, nv=6)
torus('duct_band', 'r', (0, 0, 0), 0.475, 0.012, nu=28, nv=4)
lathe('hub', 'c', [(0.0, 0.05), (0.06, 0.04), (0.08, 0.0), (0.06, -0.03)], n=10, sharp=40)
cyl('hub_light', 'e', (0, 0.045, 0), (0, 0.055, 0), 0.03, n=8)
for k in range(3):
    a = k * math.pi / 3
    for s in (1, -1):
        box('blade', 'p', (math.cos(a) * s * 0.23, 0.005, -math.sin(a) * s * 0.23), (0.36, 0.018, 0.075), 0.006, rot=(s * 0.18, a, 0), taper=(1, 0.8))
for k in range(3):
    a = k * TAU / 3 + 0.5
    box('strut', 'm', (math.cos(a) * 0.4, 0, -math.sin(a) * 0.4), (0.1, 0.03, 0.04), 0.008, rot=(0, a, 0))
    box('duct_light', 'e', (math.cos(a + 1.05) * 0.44, 0, -math.sin(a + 1.05) * 0.44), (0.05, 0.08, 0.08), 0.0, rot=(0, a + 1.05, 0))

save(out_dir(), 'drone')
