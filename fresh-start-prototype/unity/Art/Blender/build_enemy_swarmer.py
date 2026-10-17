# The swarmer: a small ceramic beetle-drone that scuttles in packs. A rounded, wedge-nosed shell (the rig's `core`,
# which bobs as it runs) under split wing-case plates standing off a gunmetal undercut, with a glowing graphite spine
# between them, flank armour with louvred vents, twin tail thrusters, rubber feed lines and whisker antennae, its
# magenta eye in a chrome-rimmed socket at the nose between sensor clusters and mandibles, on four jointed gunmetal
# spider legs with hinge drums, exposed rams with chrome rods, armoured knees and clawed feet (on the body).
# Run: blender -b -P build_enemy_swarmer.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs (the body's own meshes: they stand still while the shell bobs) ----
at('body')
for sx in (1, -1):
    for sz in (1, -1):
        hip, knee, foot = Vector((sx * 0.13, 0.36, sz * 0.15)), Vector((sx * 0.3, 0.34, sz * 0.3)), Vector((sx * 0.32, 0.02, sz * 0.27))
        cyl('hipmount', 'j', hip - Vector((0, 0.05, 0)), hip + Vector((0, 0.03, 0)), 0.07, 0.062, n=10)
        joint('hip', hip, 0.055, 0.07, axis=(0, 1, 0), bolts=0)
        cyl('thigh', 'm', hip, knee, 0.04, 0.034, n=10)
        box('thighplate', 'p', hip.lerp(knee, 0.5) + Vector((0, 0.04, 0)), (0.16, 0.04, 0.07), 0.012,
            rot=(0, math.atan2(-(knee - hip).z, (knee - hip).x), 0), taper=(0.8, 0.7))
        cyl('shin', 'm', knee, foot, 0.034, 0.018, n=10)
        joint('knee', knee, 0.045, 0.07, axis=(knee - hip).cross(Vector((0, 1, 0))), bolts=0)
        box('kneeguard', 'p', (knee[0] + sx * 0.02, knee[1] + 0.045, knee[2]), (0.11, 0.05, 0.1), 0.016, taper=(0.6, 0.6))
        piston('ram', hip + Vector((sx * 0.02, 0.06, sz * 0.02)), knee + Vector((-sx * 0.03, 0.06, -sz * 0.03)), 0.016, n=8, line=False)
        sh = knee.lerp(foot, 0.32)
        armour('shinplate', (sh.x + sx * 0.032, sh.y, sh.z), (0.045, 0.16, 0.065), out=(sx, 0, 0), bev=0.012, lip=0.01,
               rot=(0, 0, sx * 0.05), btaper=(0.6, 0.7))
        box('shin_light', 'e', (sh.x + sx * 0.056, sh.y + 0.02, sh.z), (0.006, 0.07, 0.016), 0.0, rot=(0, 0, sx * 0.05))
        cyl('ankle', 'c', foot + Vector((0, 0.07, 0)), foot + Vector((0, 0.1, 0)), 0.026, n=8)
        for t in (-0.6, 0.6):                          # a split claw: two talons splayed either side
            cyl('claw', 'p', foot + Vector((0, 0.04, 0)), foot + Vector((sx * 0.05, -0.02, sz * 0.02 + t * 0.04)), 0.02, 0.005, n=8)

# ---- The shell (core: centred 0.42 up) ----
node('core', 'body', (0, 0.42, 0))
box('belly', 'j', (-0.02, -0.12, 0), (0.5, 0.14, 0.42), 0.03, taper=(1.1, 1.08))
box('shell', 'p', (-0.01, 0.02, 0), (0.6, 0.26, 0.5), 0.07, taper=(0.82, 0.78), btaper=(1, 1))
box('shell_seam', 'j', (-0.01, -0.06, 0), (0.6, 0.02, 0.51), 0.0)
box('nose', 'p', (0.24, -0.02, 0), (0.18, 0.22, 0.4), 0.045, taper=(0.6, 0.85), shift=(-0.03, 0))
box('seam', 'j', (-0.02, 0.14, 0), (0.44, 0.04, 0.34), 0.008)          # (the dark gap the wing cases sit in)
for sz in (1, -1):                                                         # split wing cases, sloping outward
    armour('elytron', (-0.05, 0.176, sz * 0.094), (0.46, 0.06, 0.16), out=(0, 1, 0), bev=0.024, lip=0.012,
           rot=(sz * 0.2, 0, 0), taper=(0.78, 0.82))
    box('elytron_edge', 'e', (-0.05, 0.19, sz * 0.172), (0.36, 0.01, 0.008), 0.0, rot=(sz * 0.2, 0, 0))
    rivets('m', [(x, 0.21, sz * 0.12) for x in (-0.2, 0.1)], (0, 1, sz * 0.2), r=0.012, h=0.008)
box('spine', 'j', (-0.06, 0.2, 0), (0.36, 0.05, 0.06), 0.012, taper=(0.8, 0.6))
for k in range(3): box('spine_light', 'e', (-0.17 + k * 0.1, 0.228, 0), (0.05, 0.012, 0.03), 0.0)
for sz in (1, -1):
    box('flank', 'j', (-0.06, -0.01, sz * 0.25), (0.42, 0.1, 0.04), 0.012)
    armour('flankplate', (0.04, -0.005, sz * 0.276), (0.18, 0.085, 0.02), out=(0, 0, sz), bev=0.008, lip=0.008, taper=(0.85, 1.0))
    rivets('p', [(x, -0.01, sz * 0.272) for x in (-0.12, -0.07)], (0, 0, sz), r=0.011, h=0.008)
    grille('vent', (-0.19, 0.0, sz * 0.272), (0.02, 0.06, 0.09), n=3, rot=(0, -sz * math.pi / 2, 0))
    prism('mandible', 'm', [(0.0, 0.0), (0.12, -0.02), (0.16, -0.075), (0.11, -0.06), (0.05, -0.05)], -0.025, 0.025, 0.008,
          c=(0.27, -0.08, sz * 0.12), rot=(0, sz * 0.35, 0))
    cyl('mandible_pivot', 'c', (0.27, -0.09, sz * 0.12 - 0.03), (0.27, -0.09, sz * 0.12 + 0.03), 0.018, n=8)
    sensor('pip', (0.3, -0.02, sz * 0.15), 0.018, rot=(0, -sz * 0.4, 0), n=10)
    # whisker antennae swept back off the tail, from gunmetal sockets
    cyl('whisker_base', 'm', (-0.22, 0.13, sz * 0.07), (-0.23, 0.17, sz * 0.075), 0.02, n=8)
    cyl('whisker', 'm', (-0.22, 0.15, sz * 0.07), (-0.42, 0.3, sz * 0.13), 0.009, 0.005, n=5)
    box('whisker_tip', 'e', (-0.42, 0.3, sz * 0.13), (0.02, 0.02, 0.02), 0.0)
    thruster('thruster', (-0.3, 0.0, sz * 0.075), 0.045, 0.08, rot=(0, 0, -math.pi / 2), n=10)
    tube('feed', 'r', bez((-0.26, 0.06, sz * 0.2), (-0.3, 0.06, sz * 0.14), (-0.31, 0.04, sz * 0.1), 5), 0.012, n=6)
cyl('socket', 'm', (0.26, 0.03, 0), (0.32, 0.03, 0), 0.085, 0.09, n=14)
torus('socket_rim', 'c', (0.322, 0.03, 0), 0.086, 0.013, nu=14, nv=4, rot=(0, 0, math.pi / 2))
box('tail', 'j', (-0.31, 0.0, 0), (0.06, 0.12, 0.26), 0.015)
box('tail_cap', 'p', (-0.34, 0.0, 0), (0.02, 0.09, 0.2), 0.006)

# ---- The eye (its own mesh, at the nose) ----
node('eye', 'core', (0.29, 0.03, 0))
sphere('lens', 'e', (0.02, 0, 0), (0.07, 0.075, 0.075), nu=14, nv=8)

save(out_dir(), 'swarmer')
