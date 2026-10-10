# The swarmer: a small ceramic beetle-drone that scuttles in packs. A rounded, wedge-nosed shell (the rig's `core`,
# which bobs as it runs) with a graphite spine and mandibles, its magenta eye in a socket at the nose, on four
# jointed spider legs (on the body). Run: blender -b -P build_enemy_swarmer.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs (the body's own meshes: they stand still while the shell bobs) ----
at('body')
for sx in (1, -1):
    for sz in (1, -1):
        hip, knee, foot = (sx * 0.13, 0.36, sz * 0.15), (sx * 0.3, 0.34, sz * 0.3), (sx * 0.32, 0.02, sz * 0.27)
        cyl('thigh', 'j', hip, knee, 0.04, 0.035, n=8)
        cyl('shin', 'j', knee, foot, 0.036, 0.012, n=8)
        sphere('knee', 'j', knee, 0.05, nu=10, nv=6)
        box('kneeguard', 'p', (knee[0] + sx * 0.015, knee[1] + 0.04, knee[2]), (0.1, 0.05, 0.09), 0.014, taper=(0.6, 0.6))

# ---- The shell (core: centred 0.42 up) ----
node('core', 'body', (0, 0.42, 0))
box('belly', 'j', (-0.02, -0.12, 0), (0.5, 0.14, 0.42), 0.03, taper=(1.1, 1.08))
box('shell', 'p', (-0.01, 0.02, 0), (0.6, 0.26, 0.5), 0.06, taper=(0.82, 0.78), btaper=(1, 1))
box('nose', 'p', (0.24, -0.02, 0), (0.18, 0.22, 0.4), 0.04, taper=(0.6, 0.85), shift=(-0.03, 0))
box('carapace', 'p', (-0.05, 0.17, 0), (0.44, 0.07, 0.36), 0.025, taper=(0.75, 0.7))
box('spine', 'j', (-0.06, 0.205, 0), (0.34, 0.05, 0.12), 0.012, taper=(0.8, 0.6))
for k in range(3): box('spine_light', 'e', (-0.16 + k * 0.1, 0.235, 0), (0.05, 0.012, 0.05), 0.004)
for sz in (1, -1):
    box('flank', 'j', (-0.06, -0.01, sz * 0.25), (0.42, 0.1, 0.04), 0.012)
    box('vent', 'e', (-0.2, 0.0, sz * 0.272), (0.1, 0.03, 0.01), 0.004)
    prism('mandible', 'j', [(0.0, 0.0), (0.12, -0.02), (0.15, -0.07), (0.05, -0.05)], -0.025, 0.025, 0.008,
          c=(0.27, -0.08, sz * 0.12), rot=(0, sz * 0.35, 0))
cyl('socket', 'j', (0.26, 0.03, 0), (0.32, 0.03, 0), 0.085, 0.09, n=14)
box('tail', 'j', (-0.31, 0.0, 0), (0.06, 0.12, 0.24), 0.015)

# ---- The eye (its own mesh, at the nose) ----
node('eye', 'core', (0.29, 0.03, 0))
sphere('lens', 'e', (0.02, 0, 0), (0.07, 0.075, 0.075), nu=14, nv=8)

save(out_dir(), 'swarmer')
