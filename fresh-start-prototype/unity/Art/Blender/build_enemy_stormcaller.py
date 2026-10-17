# The Stormcaller: the floating storm-engine boss. A sleek armoured ceramic fuselage over a graphite keel, banded by
# gunmetal seam rings and glowing flank stripes, with a tinted glass canopy, a riveted dorsal missile pod, a storm mast
# of stacked glowing rings on its back, tail fins, whip antennae, a main engine and two arcing magenta storm coils
# strung with insulators and chrome arc spikes, its great ringed eye at the nose in a chrome bezel with a sensor pip
# either side (hull, eye), two banded, vented gunmetal rotor nacelles on lit stub wings with clamped feed cables and
# exhaust vanes (hull; the ducted rotors rotors[0] and [1], with chrome hubs, which the rig spins), belly lift
# thrusters and a chin cannon with cooling rings, a muzzle brake, a rubber feed hose and a sight that tracks its target
# (cannon, muzzle). The rig's shield bubble is left as it is. Run: blender -b -P build_enemy_stormcaller.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

FUS = ((0, 0.02, 0), (1.4, 0.42, 0.74))               # (the fuselage and keel ellipsoids, for what lies on them)
KEEL = ((-0.1, -0.2, 0), (1.12, 0.28, 0.56))

setup()
node('hull', 'body', (0, 0.75, 0))
sphere('fuselage', 'p', FUS[0], FUS[1], nu=32, nv=16, sharp=55)
sphere('keel', 'j', KEEL[0], KEEL[1], nu=28, nv=12, sharp=55)
for s in (0.3, -0.33, -0.62): patch('seam', 'm', *FUS, s - 0.014, s + 0.014, 0, TAU, ns=1, nw=32, ring=True)
for sd in (1, -1):                                     # glowing flank stripes that follow the hull
    w = 0.36 if sd > 0 else math.pi - 0.36
    patch('stripe', 'e', *FUS, -0.56, 0.74, w - 0.03, w + 0.03, ns=12, nw=1)
patch('belly_stripe', 'e', *KEEL, -0.62, 0.55, -math.pi / 2 - 0.05, -math.pi / 2 + 0.05, ns=10, nw=1, k=1.01)
box('spine', 'p', (-0.2, 0.42, 0), (1.3, 0.1, 0.36), 0.04, taper=(0.85, 0.7))
rivets('j', [(x, 0.47, z) for x in (-0.05, 0.2) for z in (0.09, -0.09)], (0, 1, 0), r=0.018, h=0.012)
box('canopy', 'l', (0.62, 0.3, 0), (0.6, 0.12, 0.44), 0.05, taper=(0.6, 0.7), rot=(0, 0, -0.18))
box('canopy_frame', 'p', (0.6, 0.358, 0), (0.06, 0.03, 0.34), 0.008, rot=(0, 0, -0.18))
cyl('eye_socket', 'm', (1.12, 0.02, 0), (1.32, 0.02, 0), 0.26, 0.24, n=24)
torus('eye_ring', 'c', (1.31, 0.02, 0), 0.235, 0.032, nu=24, nv=5, rot=(0, 0, math.pi / 2))
box('brow', 'p', (1.14, 0.22, 0), (0.32, 0.08, 0.5), 0.03, rot=(0, 0, -0.35), taper=(0.7, 0.85))
for z in (0.33, -0.33): sensor('pip', (1.17, -0.1, z), 0.04, rot=(0, -0.5 if z > 0 else 0.5, 0), n=8)
for z in (0.62, -0.62):
    sd = 1 if z > 0 else -1
    box('cheek', 'p', (0.5, 0.0, z * 0.96), (0.9, 0.2, 0.1), 0.03, taper=(0.8, 1.0))
    # stub wing out to the nacelle: a leading-edge cap, a stripe, rivets and the feed pipe beneath
    box('wing', 'p', (-0.15, 0.05, z * 1.32), (0.5, 0.1, 0.42), 0.03, taper=(0.8, 1.0))
    box('wing_edge', 'm', (0.11, 0.05, z * 1.32), (0.04, 0.08, 0.4), 0.0)
    box('wing_light', 'e', (-0.15, 0.102, z * 1.32), (0.26, 0.008, 0.03), 0.0)
    rivets('j', [(x, 0.1, z * 1.32 + t) for x in (-0.3, 0.0) for t in (0.13, -0.13)], (0, 1, 0), r=0.016, h=0.01)
    cables('wing_pipe', (-0.05, -0.05, z * 1.0), (-0.08, -0.07, z * 1.3), (-0.12, -0.05, z * 1.6), 0.022, k=2, clamps=(0.5,))
    # the nacelle (the rig's own housing and glowing underside disc), banded, riveted, vented and lit
    nc = (-0.15, 0.05, z * 1.774)
    lathe('nacelle', 'm', [(0.0, -0.17), (0.26, -0.17), (0.34, -0.1), (0.34, 0.1), (0.3, 0.16)], n=20, sharp=40, c=nc, bev=0.01)
    lathe('intake', 'p', [(0.3, 0.13), (0.33, 0.17), (0.12, 0.17), (0.1, 0.13)], n=20, sharp=40, c=nc)
    lathe('band', 'p', [(0.335, -0.05), (0.355, -0.04), (0.355, 0.04), (0.335, 0.05)], n=20, sharp=30, c=nc)
    for a in (k * TAU / 10 + 0.3 for k in range(10)):
        if -math.sin(a) * sd > -0.5: rivets('j', [(nc[0] + math.cos(a) * 0.356, 0.05, nc[2] - math.sin(a) * 0.356)], (math.cos(a), 0, -math.sin(a)), r=0.014, h=0.01)
    lathe('thrust', 'e', [(0.0, -0.21), (0.21, -0.21), (0.21, -0.17)], n=20, sharp=40, c=nc)
    for k in range(3): box('vane', 'm', (nc[0], -0.225, nc[2]), (0.4, 0.02, 0.03), 0.0, rot=(0, k * math.pi / 3, 0))
    box('nacelle_light', 'e', (0.2, 0.05, z * 1.774), (0.02, 0.1, 0.12), 0.006)
    grille('nacelle_vent', (nc[0], 0.0, nc[2] + sd * 0.335), (0.03, 0.12, 0.16), n=3, rot=(0, -sd * math.pi / 2, 0))
for z in (0.25, -0.25):
    prism('fin', 'p', [(-0.28, -0.16), (0.24, -0.16), (0.1, 0.06), (-0.22, 0.2)], -0.03, 0.03, 0.012, c=(-1.25, 0.28, z), rot=(0, 0, 0.5))
    box('fin_light', 'e', (-1.25, 0.28, z * 1.2), (0.3, 0.025, 0.012), 0.004, rot=(0, 0, 0.5))
    box('fin_tip', 'e', (-1.395, 0.385, z), (0.04, 0.04, 0.07), 0.0, rot=(0, 0, 0.5))
    cyl('whip', 'm', (-0.75, 0.38, z * 0.6), (-1.32, 0.92, z), 0.014, 0.008, n=5)
    box('whip_tip', 'e', (-1.32, 0.92, z), (0.035, 0.035, 0.035), 0.0)
# dorsal missile pod, riveted, with chevrons on its flanks
box('pod', 'p', (-0.45, 0.52, 0), (0.62, 0.22, 0.5), 0.05, taper=(0.85, 0.9))
box('pod_skirt', 'm', (-0.45, 0.43, 0), (0.66, 0.05, 0.54), 0.0)
for k in range(6):
    x, z = -0.62 + (k % 3) * 0.17, (0.11 if k < 3 else -0.11)
    cyl('pod_tube', 'm', (x, 0.6, z), (x, 0.65, z), 0.06, n=10)
    cyl('pod_tip', 'e', (x, 0.65, z), (x, 0.66, z), 0.04, n=10)
for z in (0.25, -0.25):
    sd = 1 if z > 0 else -1
    for k in range(3):
        x = -0.6 + k * 0.13
        prism('pod_chevron', 'e', [(x - 0.04, -0.035), (x, 0.005), (x + 0.04, -0.035), (x + 0.04, 0.0), (x, 0.04), (x - 0.04, 0.0)],
              0, 0.01, 0.0, c=(0, 0.51, z - sd * 0.01), rot=(0, 0 if sd > 0 else math.pi, 0))
    rivets('j', [(-0.2, 0.51, z)], (0, 0, sd), r=0.016, h=0.01)
# the storm mast: a tower of glowing rings ahead of the pod, a capacitor sphere at its crown
lathe('mast_base', 'm', [(0.17, 0.4), (0.15, 0.5), (0.08, 0.56), (0.0, 0.56)], n=12, sharp=40, c=(0.05, 0, 0))
cyl('mast', 'm', (0.05, 0.5, 0), (0.05, 1.02, 0), 0.05, 0.035, n=8)
for k, (y, R) in enumerate(((0.62, 0.15), (0.74, 0.125), (0.85, 0.1))):
    torus('mast_ring', 'e' if k % 2 == 0 else 'p', (0.05, y, 0), R, 0.024, nu=16, nv=4)
    for a in (0.0, math.pi / 2, math.pi, 3 * math.pi / 2):
        box('mast_spoke', 'm', (0.05 + math.cos(a) * R / 2, y, -math.sin(a) * R / 2), (R, 0.02, 0.02), 0.0, rot=(0, a, 0))
cyl('mast_collar', 'p', (0.05, 0.94, 0), (0.05, 0.99, 0), 0.06, n=10)
sphere('mast_orb', 'e', (0.05, 1.06, 0), 0.075, nu=12, nv=6)
# arcing storm coils round the tail, strung with insulators and arc spikes, and the pylons that carry them
for x, r in ((-1.0, 0.57), (-1.18, 0.5)):
    torus('coil', 'e', (x, 0.0, 0), r, 0.025, nu=32, nv=4, rot=(0, 0, math.pi / 2))
    for a in (k * TAU / 8 + 0.2 for k in range(8)):
        c = Vector((x, math.sin(a) * r, math.cos(a) * r))
        cyl('insulator', 'p', c - Vector((0.03, 0, 0)), c + Vector((0.03, 0, 0)), 0.045, n=8)
for a in (0.6, 2.54, 3.74, 5.68):
    box('coil_pylon', 'm', (-1.09, math.sin(a) * 0.53, math.cos(a) * 0.53), (0.28, 0.06, 0.06), 0.015, rot=(-a, 0, 0))
    box('coil_strut', 'm', (-1.09, math.sin(a) * 0.4, math.cos(a) * 0.4), (0.06, 0.24, 0.04), 0.01, rot=(math.pi / 2 - a, 0, 0))
for a in (1.57, 3.14, 4.71, 0.0):                      # arc spikes trailing off the rear coil
    b = Vector((-1.2, math.sin(a) * 0.5, math.cos(a) * 0.5))
    cyl('arc_spike', 'c', b, b + Vector((-0.2, math.sin(a) * 0.05, math.cos(a) * 0.05)), 0.03, 0.004, n=6)
cyl('tail', 'm', (-1.3, 0.02, 0), (-1.5, 0.02, 0), 0.16, 0.13, n=16)
thruster('engine', (-1.48, 0.02, 0), 0.15, 0.2, rot=(0, 0, -math.pi / 2), n=16)
# lift thrusters under the keel
for z in (0.2, -0.2): thruster('lift', (-0.6, -0.4, z), 0.09, 0.14, n=12, rings=False)

node('eye', 'hull', (1.28, 0.02, 0))
sphere('lens', 'e', (0.02, 0, 0), (0.14, 0.18, 0.18), nu=28, nv=14, q=False)

node('cannon', 'hull', (0.8, -0.38, 0))
sphere('turret', 'm', (0, 0, 0), (0.2, 0.16, 0.2), nu=24, nv=10, q=False)
box('mantlet', 'p', (0.16, 0, 0), (0.2, 0.18, 0.24), 0.04, taper=(0.8, 0.85))
for z in (0.122, -0.122): rivets('j', [(0.16 + x, 0.0, z) for x in (-0.05, 0.05)], (0, 0, 1 if z > 0 else -1), r=0.014, h=0.01)
box('sight', 'm', (0.14, 0.12, 0.06), (0.1, 0.06, 0.06), 0.01)
sensor('sight_eye', (0.19, 0.12, 0.06), 0.02, n=8)
tube('feed', 'r', bez((-0.05, -0.08, 0.17), (0.1, -0.16, 0.2), (0.2, -0.08, 0.12), 8), 0.03, n=8)
cyl('barrel', 'm', (0.2, 0, 0), (0.86, 0, 0), 0.1, 0.075, n=14)
for x in (0.4, 0.58): cyl('band', 'p', (x, 0, 0), (x + 0.05, 0, 0), 0.11, n=14)
for x in (0.66, 0.7, 0.74): cyl('cooler', 'm', (x, 0, 0), (x + 0.015, 0, 0), 0.098, n=14)
cyl('choke', 'p', (0.78, 0, 0), (0.88, 0, 0), 0.095, n=14)
box('brake', 'm', (0.86, 0, 0), (0.08, 0.1, 0.22), 0.015)
box('brake_slot', 'e', (0.86, 0, 0), (0.02, 0.104, 0.224), 0.0)
node('muzzle', 'cannon', (0.92, 0, 0))
sphere('tip', 'e', (0, 0, 0), 0.065, nu=12, nv=8)

for i, z in enumerate((1.1, -1.1)):
    node(f'rotors[{i}]', 'hull', (-0.15, 0.25, z))
    torus('duct', 'm', (0, 0, 0), 0.62, 0.045, nu=36, nv=6)
    torus('duct_rim', 'p', (0, 0.0, 0), 0.565, 0.018, nu=36, nv=4)
    lathe('hub', 'c', [(0.0, 0.07), (0.08, 0.06), (0.11, 0.0), (0.09, -0.04)], n=14, sharp=40)
    cyl('hub_light', 'e', (0, 0.06, 0), (0, 0.075, 0), 0.04, n=10)
    for k in range(3):
        a = k * math.pi / 3
        for s in (1, -1):
            box('blade', 'p', (math.cos(a) * s * 0.33, 0.005, -math.sin(a) * s * 0.33), (0.52, 0.022, 0.11), 0.008, rot=(s * 0.2, a, 0), taper=(1, 0.75))
            box('cuff', 'm', (math.cos(a) * s * 0.13, 0.005, -math.sin(a) * s * 0.13), (0.06, 0.05, 0.12), 0.0, rot=(0, a, 0))
    for k in range(4):
        a = k * TAU / 4 + 0.4
        box('tip_light', 'e', (math.cos(a) * 0.62, 0.0, -math.sin(a) * 0.62), (0.08, 0.1, 0.1), 0.01, rot=(0, a, 0))
        a2 = a + math.pi / 4
        box('duct_strut', 'm', (math.cos(a2) * 0.6, -0.03, -math.sin(a2) * 0.6), (0.1, 0.03, 0.05), 0.0, rot=(0, a2, 0))

save(out_dir(), 'stormcaller')
