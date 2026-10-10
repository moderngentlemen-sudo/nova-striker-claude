# The post: a sentry pylon that swings a barrier boom. An octagonal graphite plinth with a glowing ring, a tapered
# ceramic column banded in graphite under a sensor head (the body; its eye a mesh of its own) and, on the near side,
# a hinged boom with hazard chevrons and a weighted striking tip (arm, which the rig swings).
# Run: blender -b -P build_enemy_post.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
at('body')
lathe('plinth', 'j', [(0.6, 0.0), (0.6, 0.06), (0.55, 0.2), (0.38, 0.26)], n=8, sharp=30, bev=0.015)
torus('ring', 'e', (0, 0.14, 0), 0.575, 0.022, nu=24, nv=4)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 8
    box('foot', 'p', (math.cos(a) * 0.5, 0.06, -math.sin(a) * 0.5), (0.2, 0.12, 0.16), 0.025, rot=(0, a, 0), taper=(0.7, 0.8))
lathe('column', 'p', [(0.24, 0.2), (0.24, 0.3), (0.2, 0.34), (0.17, 1.62), (0.2, 1.66), (0.2, 1.72)], n=8, sharp=30, bev=0.012)
for y in (0.55, 0.95, 1.25): lathe('band', 'j', [(0.205 - y * 0.02, y - 0.03), (0.225 - y * 0.02, y - 0.02), (0.225 - y * 0.02, y + 0.02), (0.205 - y * 0.02, y + 0.03)], n=8, sharp=30)
box('slot', 'e', (0.18, 0.75, 0), (0.03, 0.26, 0.06), 0.008, rot=(0, 0, 0.02))
cyl('hinge', 'j', (0, 1.45, 0.12), (0, 1.45, 0.2), 0.14, n=14)
lathe('neck', 'j', [(0.13, 1.68), (0.13, 1.76)], n=8, sharp=30)
box('head', 'p', (0.0, 1.84, 0), (0.36, 0.24, 0.3), 0.05, taper=(0.75, 0.8), btaper=(0.85, 0.9))
box('hood', 'p', (0.06, 1.97, 0), (0.3, 0.05, 0.26), 0.02, taper=(0.7, 0.8))
cyl('socket', 'j', (0.12, 1.8, 0), (0.2, 1.8, 0), 0.1, n=14)
box('lamp', 'e', (-0.06, 2.0, 0), (0.06, 0.03, 0.06), 0.008)

node('eye', 'body', (0.18, 1.8, 0))
sphere('lens', 'e', (0.02, 0, 0), (0.07, 0.085, 0.085), nu=14, nv=8)

# ---- The boom (pivot on the near side of the column, reaching 1.25 forward) ----
node('arm', 'body', (0, 1.45, 0.3))
cyl('hub', 'j', (0, 0, -0.1), (0, 0, 0.08), 0.13, n=14)
cyl('hubcap', 'p', (0, 0, 0.08), (0, 0, 0.11), 0.09, n=14)
prism('beam', 'p', [(-0.08, -0.1), (1.12, -0.07), (1.12, 0.07), (-0.08, 0.1)], -0.08, 0.08, 0.02)
for k in range(5):
    x = 0.22 + k * 0.17
    prism('chevron', 'e', [(x, -0.06), (x + 0.05, -0.06), (x + 0.1, 0.0), (x + 0.05, 0.06), (x, 0.06), (x + 0.05, 0.0)], 0.08, 0.09, 0.0)
box('tip', 'j', (1.17, 0, 0), (0.16, 0.24, 0.22), 0.04, taper=(0.8, 0.9), btaper=(0.8, 0.9))
box('tip_light', 'e', (1.255, 0, 0), (0.012, 0.12, 0.12), 0.004)
box('spine', 'j', (0.5, 0.1, 0), (0.8, 0.04, 0.08), 0.012)

save(out_dir(), 'post')
