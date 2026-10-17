# The swarmer: a small ceramic beetle-drone that scuttles in packs. A rounded, wedge-nosed shell (the rig's `core`,
# which bobs as it runs) under split wing-case plates with a glowing graphite spine between them, riveted flank
# armour, twin tail thrusters and whisker antennae, its magenta eye in a socket at the nose flanked by sensor pips,
# on four jointed spider legs with hip actuators, hydraulic rams and clawed feet (on the body).
# Run: blender -b -P build_enemy_swarmer.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs (the body's own meshes: they stand still while the shell bobs) ----
at('body')
for sx in (1, -1):
    for sz in (1, -1):
        hip, knee, foot = Vector((sx * 0.13, 0.36, sz * 0.15)), Vector((sx * 0.3, 0.34, sz * 0.3)), Vector((sx * 0.32, 0.02, sz * 0.27))
        cyl('hipdisc', 'j', hip - Vector((0, 0.035, 0)), hip + Vector((0, 0.035, 0)), 0.06, n=8)
        cyl('thigh', 'j', hip, knee, 0.04, 0.035, n=8)
        cyl('shin', 'j', knee, foot, 0.036, 0.016, n=8)
        sphere('knee', 'j', knee, 0.05, nu=8, nv=5)
        box('kneeguard', 'p', (knee[0] + sx * 0.015, knee[1] + 0.04, knee[2]), (0.1, 0.05, 0.09), 0.014, taper=(0.6, 0.6))
        # a ram along the thigh's top, and a pale guard down the front of the shin
        piston('ram', hip + Vector((sx * 0.02, 0.05, sz * 0.02)), knee + Vector((-sx * 0.03, 0.05, -sz * 0.03)), 0.014, n=6, collar=False)
        sh = knee.lerp(foot, 0.3)
        box('shinplate', 'p', (sh.x + sx * 0.03, sh.y, sh.z), (0.04, 0.14, 0.06), 0.01, rot=(0, 0, sx * 0.05), btaper=(0.6, 0.7))
        cyl('claw', 'p', foot + Vector((0, 0.02, 0)), foot + Vector((sx * 0.04, -0.02, sz * 0.03)), 0.022, 0.006, n=6)

# ---- The shell (core: centred 0.42 up) ----
node('core', 'body', (0, 0.42, 0))
box('belly', 'j', (-0.02, -0.12, 0), (0.5, 0.14, 0.42), 0.03, taper=(1.1, 1.08))
box('shell', 'p', (-0.01, 0.02, 0), (0.6, 0.26, 0.5), 0.06, taper=(0.82, 0.78), btaper=(1, 1))
box('nose', 'p', (0.24, -0.02, 0), (0.18, 0.22, 0.4), 0.04, taper=(0.6, 0.85), shift=(-0.03, 0))
box('seam', 'j', (-0.02, 0.14, 0), (0.44, 0.04, 0.34), 0.008)          # (the dark gap the wing cases sit in)
for sz in (1, -1):                                                         # split wing cases, sloping outward
    box('elytron', 'p', (-0.05, 0.172, sz * 0.092), (0.46, 0.06, 0.16), 0.022, rot=(sz * 0.2, 0, 0), taper=(0.78, 0.82))
box('spine', 'j', (-0.06, 0.2, 0), (0.36, 0.05, 0.06), 0.012, taper=(0.8, 0.6))
for k in range(3): box('spine_light', 'e', (-0.17 + k * 0.1, 0.228, 0), (0.05, 0.012, 0.03), 0.0)
for sz in (1, -1):
    box('flank', 'j', (-0.06, -0.01, sz * 0.25), (0.42, 0.1, 0.04), 0.012)
    box('flankplate', 'p', (0.02, -0.005, sz * 0.272), (0.2, 0.08, 0.016), 0.006, taper=(0.85, 1.0))
    rivets('p', [(x, -0.01, sz * 0.27) for x in (-0.12, -0.06)], (0, 0, sz), r=0.011, h=0.008)
    box('vent', 'e', (-0.2, 0.0, sz * 0.272), (0.08, 0.03, 0.01), 0.0)
    prism('mandible', 'j', [(0.0, 0.0), (0.12, -0.02), (0.15, -0.07), (0.05, -0.05)], -0.025, 0.025, 0.008,
          c=(0.27, -0.08, sz * 0.12), rot=(0, sz * 0.35, 0))
    box('pip', 'e', (0.296, -0.02, sz * 0.15), (0.02, 0.026, 0.026), 0.0)
    # whisker antennae swept back off the tail
    cyl('whisker', 'j', (-0.22, 0.15, sz * 0.07), (-0.42, 0.3, sz * 0.13), 0.009, 0.005, n=4)
    box('whisker_tip', 'e', (-0.42, 0.3, sz * 0.13), (0.02, 0.02, 0.02), 0.0)
    thruster('thruster', (-0.3, 0.0, sz * 0.075), 0.045, 0.08, rot=(0, 0, -math.pi / 2), n=8)
cyl('socket', 'j', (0.26, 0.03, 0), (0.32, 0.03, 0), 0.085, 0.09, n=14)
torus('socket_rim', 'p', (0.322, 0.03, 0), 0.086, 0.013, nu=14, nv=4, rot=(0, 0, math.pi / 2))
box('tail', 'j', (-0.31, 0.0, 0), (0.06, 0.12, 0.26), 0.015)

# ---- The eye (its own mesh, at the nose) ----
node('eye', 'core', (0.29, 0.03, 0))
sphere('lens', 'e', (0.02, 0, 0), (0.07, 0.075, 0.075), nu=14, nv=8)

save(out_dir(), 'swarmer')
