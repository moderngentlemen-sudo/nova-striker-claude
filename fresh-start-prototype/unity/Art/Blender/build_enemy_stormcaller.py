# The Stormcaller: the floating storm-engine boss. A sleek armoured ceramic fuselage over a graphite keel, with a
# dorsal missile pod, tail fins and arcing magenta storm coils, its great eye at the nose (hull, eye), two rotor
# nacelles on stub wings (hull; the ducted rotors rotors[0] and [1], which the rig spins) and a chin cannon that
# tracks its target (cannon, muzzle). The rig's shield bubble is left as it is.
# Run: blender -b -P build_enemy_stormcaller.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
node('hull', 'body', (0, 0.75, 0))
sphere('fuselage', 'p', (0, 0.02, 0), (1.4, 0.42, 0.74), nu=28, nv=14, sharp=55)
sphere('keel', 'j', (-0.1, -0.2, 0), (1.12, 0.28, 0.56), nu=24, nv=10, sharp=55)
box('spine', 'p', (-0.2, 0.42, 0), (1.3, 0.1, 0.36), 0.04, taper=(0.85, 0.7))
box('canopy', 'j', (0.62, 0.3, 0), (0.6, 0.12, 0.44), 0.05, taper=(0.6, 0.7), rot=(0, 0, -0.18))
cyl('eye_socket', 'j', (1.12, 0.02, 0), (1.32, 0.02, 0), 0.26, 0.24, n=24)
box('brow', 'p', (1.14, 0.22, 0), (0.32, 0.08, 0.5), 0.03, rot=(0, 0, -0.35), taper=(0.7, 0.85))
for z in (0.62, -0.62):
    box('strake', 'e', (0.1, 0.18, z), (1.8, 0.045, 0.045), 0.012)
    box('cheek', 'p', (0.5, 0.0, z * 0.96), (0.9, 0.2, 0.1), 0.03, taper=(0.8, 1.0))
    # stub wing out to the nacelle
    box('wing', 'p', (-0.15, 0.05, z * 1.32), (0.5, 0.1, 0.42), 0.03, taper=(0.8, 1.0))
    # the nacelle (the rig's own housing and glowing underside disc)
    lathe('nacelle', 'j', [(0.0, -0.17), (0.26, -0.17), (0.34, -0.1), (0.34, 0.1), (0.3, 0.16)], n=20, sharp=40,
          c=(-0.15, 0.05, z * 1.774), bev=0.01)
    lathe('intake', 'p', [(0.3, 0.13), (0.33, 0.17), (0.12, 0.17), (0.1, 0.13)], n=20, sharp=40, c=(-0.15, 0.05, z * 1.774))
    lathe('thrust', 'e', [(0.0, -0.21), (0.21, -0.21), (0.21, -0.17)], n=20, sharp=40, c=(-0.15, 0.05, z * 1.774))
    box('nacelle_light', 'e', (0.2, 0.05, z * 1.774), (0.02, 0.1, 0.12), 0.006)
for z in (0.25, -0.25):
    prism('fin', 'p', [(-0.28, -0.16), (0.24, -0.16), (0.1, 0.06), (-0.22, 0.2)], -0.03, 0.03, 0.012, c=(-1.25, 0.28, z), rot=(0, 0, 0.5))
    box('fin_light', 'e', (-1.25, 0.28, z * 1.2), (0.3, 0.025, 0.012), 0.004, rot=(0, 0, 0.5))
# dorsal missile pod
box('pod', 'p', (-0.45, 0.52, 0), (0.62, 0.22, 0.5), 0.05, taper=(0.85, 0.9))
for k in range(6):
    x, z = -0.62 + (k % 3) * 0.17, (0.11 if k < 3 else -0.11)
    cyl('pod_tube', 'j', (x, 0.6, z), (x, 0.65, z), 0.06, n=10)
    cyl('pod_tip', 'e', (x, 0.65, z), (x, 0.66, z), 0.04, n=10)
# arcing storm coils: two rings round the tail and pylons that carry them
for x, r in ((-1.0, 0.57), (-1.18, 0.5)):
    torus('coil', 'e', (x, 0.0, 0), r, 0.025, nu=28, nv=4, rot=(0, 0, math.pi / 2))
for a in (0.6, 2.54, 3.74, 5.68):
    box('coil_pylon', 'j', (-1.09, math.sin(a) * 0.53, math.cos(a) * 0.53), (0.28, 0.06, 0.06), 0.015, rot=(-a, 0, 0))
    box('coil_strut', 'j', (-1.09, math.sin(a) * 0.4, math.cos(a) * 0.4), (0.06, 0.24, 0.04), 0.01, rot=(math.pi / 2 - a, 0, 0))
cyl('tail', 'j', (-1.3, 0.02, 0), (-1.52, 0.02, 0), 0.16, 0.1, n=16)
cyl('tail_glow', 'e', (-1.52, 0.02, 0), (-1.54, 0.02, 0), 0.1, n=16)

node('eye', 'hull', (1.28, 0.02, 0))
sphere('lens', 'e', (0.02, 0, 0), (0.14, 0.18, 0.18), nu=20, nv=12)

node('cannon', 'hull', (0.8, -0.38, 0))
sphere('turret', 'j', (0, 0, 0), (0.2, 0.16, 0.2), nu=18, nv=8)
box('mantlet', 'p', (0.16, 0, 0), (0.2, 0.18, 0.24), 0.04, taper=(0.8, 0.85))
cyl('barrel', 'j', (0.2, 0, 0), (0.86, 0, 0), 0.1, 0.075, n=14)
for x in (0.4, 0.58): cyl('band', 'p', (x, 0, 0), (x + 0.05, 0, 0), 0.11, n=14)
cyl('choke', 'p', (0.78, 0, 0), (0.88, 0, 0), 0.095, n=14)
node('muzzle', 'cannon', (0.92, 0, 0))
sphere('tip', 'e', (0, 0, 0), 0.065, nu=12, nv=8)

for i, z in enumerate((1.1, -1.1)):
    node(f'rotors[{i}]', 'hull', (-0.15, 0.25, z))
    torus('duct', 'j', (0, 0, 0), 0.62, 0.045, nu=36, nv=6)
    lathe('hub', 'p', [(0.0, 0.07), (0.08, 0.06), (0.11, 0.0), (0.09, -0.04)], n=14, sharp=40)
    for k in range(3):
        a = k * math.pi / 3
        for s in (1, -1):
            box('blade', 'p', (math.cos(a) * s * 0.33, 0.005, -math.sin(a) * s * 0.33), (0.52, 0.022, 0.11), 0.008, rot=(s * 0.2, a, 0), taper=(1, 0.75))
    for k in range(4):
        a = k * TAU / 4 + 0.4
        box('tip_light', 'e', (math.cos(a) * 0.62, 0.0, -math.sin(a) * 0.62), (0.08, 0.1, 0.1), 0.01, rot=(0, a, 0))

save(out_dir(), 'stormcaller')
