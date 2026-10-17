# The post: a sentry pylon that swings a barrier boom. An octagonal graphite plinth with a glowing ring, bolted feet
# and a riveted deck, a tapered ceramic column banded in graphite with a clamped cable conduit up its back, under a
# sensor head with ear sensors, a brow and a lit antenna (the body; its eye a mesh of its own) and, on the near side,
# a hinged boom with hazard chevrons, rivets, an under-slung ram, a counterweight and a bumpered, weighted striking tip
# (arm, which the rig swings). Run: blender -b -P build_enemy_post.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
at('body')
lathe('plinth', 'j', [(0.6, 0.0), (0.6, 0.06), (0.55, 0.2), (0.38, 0.26)], n=8, sharp=30, bev=0.015)
torus('ring', 'e', (0, 0.14, 0), 0.575, 0.022, nu=24, nv=4)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 8
    box('foot', 'p', (math.cos(a) * 0.5, 0.06, -math.sin(a) * 0.5), (0.2, 0.12, 0.16), 0.025, rot=(0, a, 0), taper=(0.7, 0.8))
    rivets('j', [(math.cos(a) * 0.53, 0.125, -math.sin(a) * 0.53)], (0, 1, 0), r=0.022, h=0.012)
for k in range(8):                                     # (deck bolts on the plinth's slope)
    a = k * math.pi / 4 + math.pi / 8
    rivets('p', [(math.cos(a) * 0.465, 0.229, -math.sin(a) * 0.465)], (math.cos(a) * 0.33, 0.94, -math.sin(a) * 0.33), r=0.018, h=0.01)
lathe('column', 'p', [(0.24, 0.2), (0.24, 0.3), (0.2, 0.34), (0.17, 1.62), (0.2, 1.66), (0.2, 1.72)], n=8, sharp=30, bev=0.012)
for y in (0.55, 0.95, 1.25): lathe('band', 'j', [(0.205 - y * 0.02, y - 0.03), (0.225 - y * 0.02, y - 0.02), (0.225 - y * 0.02, y + 0.02), (0.205 - y * 0.02, y + 0.03)], n=8, sharp=30)
box('slot', 'e', (0.18, 0.75, 0), (0.03, 0.26, 0.06), 0.008, rot=(0, 0, 0.02))
box('hatch', 'j', (0.178, 1.08, 0), (0.03, 0.16, 0.1), 0.006, rot=(0, 0, 0.02))
# the cable conduit up the back, clamped where it crosses the bands
tube('conduit', 'j', [(-0.27, 0.27, 0), (-0.235, 0.36, 0), (-0.21, 1.0, 0), (-0.195, 1.62, 0)], 0.026, n=6)
for y in (0.55, 0.95, 1.25): box('clamp', 'p', (-0.21 + (y - 1.0) * -0.03, y, 0), (0.07, 0.05, 0.07), 0.0)
cyl('hinge', 'j', (0, 1.45, 0.12), (0, 1.45, 0.2), 0.14, n=14)
lathe('neck', 'j', [(0.13, 1.68), (0.13, 1.76)], n=8, sharp=30)
box('head', 'p', (0.0, 1.84, 0), (0.36, 0.24, 0.3), 0.05, taper=(0.75, 0.8), btaper=(0.85, 0.9))
box('hood', 'p', (0.06, 1.97, 0), (0.3, 0.05, 0.26), 0.02, taper=(0.7, 0.8))
box('brow', 'j', (0.15, 1.93, 0), (0.06, 0.035, 0.24), 0.008, rot=(0, 0, -0.5))
for z in (0.15, -0.15): sensor('ear', (-0.02, 1.83, z), 0.035, rot=(0, -math.pi / 2 * (1 if z > 0 else -1), 0), n=8)
cyl('socket', 'j', (0.12, 1.8, 0), (0.2, 1.8, 0), 0.1, n=14)
box('lamp', 'e', (-0.06, 2.0, 0), (0.06, 0.03, 0.06), 0.008)
cyl('antenna', 'j', (-0.1, 1.98, -0.07), (-0.14, 2.28, -0.08), 0.012, 0.008, n=5)
box('antenna_tip', 'e', (-0.14, 2.28, -0.08), (0.03, 0.03, 0.03), 0.0)

node('eye', 'body', (0.18, 1.8, 0))
sphere('lens', 'e', (0.02, 0, 0), (0.07, 0.085, 0.085), nu=14, nv=8)

# ---- The boom (pivot on the near side of the column, reaching 1.25 forward) ----
node('arm', 'body', (0, 1.45, 0.3))
cyl('hub', 'j', (0, 0, -0.1), (0, 0, 0.08), 0.13, n=14)
cyl('hubcap', 'p', (0, 0, 0.08), (0, 0, 0.11), 0.09, n=14)
rivets('j', [(math.cos(a) * 0.055, math.sin(a) * 0.055, 0.11) for a in (0.5, 2.6, 4.7)], (0, 0, 1), r=0.014, h=0.01)
box('counterweight', 'j', (-0.2, 0, 0), (0.16, 0.2, 0.17), 0.03, taper=(0.85, 1.0), btaper=(0.85, 1.0))
box('counterweight_plate', 'p', (-0.2, 0, 0.088), (0.12, 0.15, 0.012), 0.0)
prism('beam', 'p', [(-0.08, -0.1), (1.12, -0.07), (1.12, 0.07), (-0.08, 0.1)], -0.08, 0.08, 0.02)
for k in range(5):
    x = 0.22 + k * 0.17
    prism('chevron', 'e', [(x, -0.06), (x + 0.05, -0.06), (x + 0.1, 0.0), (x + 0.05, 0.06), (x, 0.06), (x + 0.05, 0.0)], 0.08, 0.09, 0.0)
rivets('j', [(0.16 + k * 0.17, 0.0, 0.08) for k in range(6)], (0, 0, 1), r=0.012, h=0.008)
piston('strut', (0.06, -0.13, 0), (0.62, -0.085, 0), 0.024, n=6)
box('strut_lug', 'j', (0.62, -0.08, 0), (0.06, 0.04, 0.05), 0.0)
box('tip', 'j', (1.17, 0, 0), (0.16, 0.24, 0.22), 0.04, taper=(0.8, 0.9), btaper=(0.8, 0.9))
for y in (0.075, -0.075): box('bumper', 'p', (1.235, y, 0), (0.04, 0.05, 0.19), 0.0)
box('tip_light', 'e', (1.255, 0, 0), (0.012, 0.08, 0.12), 0.004)
box('spine', 'j', (0.5, 0.1, 0), (0.8, 0.04, 0.08), 0.012)

save(out_dir(), 'post')
