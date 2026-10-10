# The mortar: a squat artillery walker. A graphite turntable on two short track pods (the body), an armoured ceramic
# hull with a sloped glacis, ammunition drums and a visor slit (torso, eye), and a heavy banded launch tube in a
# recoil cradle (tube, which the rig raises and squashes as it fires; muzzle: the glowing ring at its mouth).
# Run: blender -b -P build_enemy_mortar.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
at('body')
lathe('turntable', 'j', [(0.5, 0.3), (0.56, 0.26), (0.56, 0.18), (0.44, 0.14)], n=16, sharp=30, bev=0.01)
for z in (0.38, -0.38):
    prism('track', 'j', [(-0.6, 0.06), (-0.5, 0.0), (0.5, 0.0), (0.62, 0.08), (0.52, 0.2), (-0.52, 0.2)], z - 0.12, z + 0.12, 0.02)
    prism('trackguard', 'p', [(-0.56, 0.16), (0.56, 0.16), (0.6, 0.22), (0.5, 0.26), (-0.5, 0.26), (-0.6, 0.22)], z - 0.13, z + 0.13, 0.02)
    for x in (-0.36, 0.0, 0.36): cyl('wheel', 'p', (x, 0.1, z + 0.12), (x, 0.1, z + 0.135), 0.07, n=12)
    for k in range(8): box('tread', 'j', (-0.42 + k * 0.12, 0.01, z), (0.05, 0.03, 0.25), 0.006)
box('axle', 'j', (0, 0.12, 0), (0.5, 0.12, 0.56), 0.02)

node('torso', 'body', (0, 0.55, 0))
box('hull', 'p', (-0.02, 0.0, 0), (0.76, 0.46, 0.8), 0.06, taper=(0.86, 0.9))
box('glacis', 'p', (0.34, -0.02, 0), (0.12, 0.38, 0.72), 0.04, taper=(0.4, 0.95), shift=(-0.05, 0))
box('skirt', 'j', (0, -0.21, 0), (0.8, 0.08, 0.84), 0.02)
box('roof', 'j', (-0.06, 0.24, 0), (0.5, 0.04, 0.6), 0.015)
for z in (0.44, -0.44):
    cyl('drum', 'j', (-0.25, 0.02, z), (0.15, 0.02, z), 0.14, n=14)
    cyl('drumcap', 'p', (-0.27, 0.02, z), (-0.21, 0.02, z), 0.15, n=14)
    for x in (-0.08, 0.06): cyl('drumband', 'e', (x, 0.02, z), (x + 0.02, 0.02, z), 0.142, n=14)
box('exhaust', 'j', (-0.42, 0.08, 0.2), (0.08, 0.2, 0.12), 0.02)
for k in range(3): box('grille', 'e', (-0.4, -0.08 + k * 0.07, -0.18), (0.012, 0.025, 0.2), 0.004)

node('eye', 'torso', (0.41, 0.06, 0))
box('visor', 'e', (-0.02, 0, 0), (0.04, 0.05, 0.44), 0.01, taper=(1, 0.85))

node('tube', 'torso', (0.05, 0.22, 0), (0, 0, -0.45))
box('cradle', 'j', (0, 0.06, 0), (0.36, 0.16, 0.5), 0.03)
cyl('trunnion', 'p', (0, 0.06, -0.3), (0, 0.06, 0.3), 0.08, n=12)
lathe('barrel', 'p', [(0.22, 0.0), (0.22, 0.12), (0.2, 0.16), (0.18, 0.8), (0.2, 0.84), (0.2, 0.93), (0.15, 0.93)], n=16, sharp=40, bev=0.008)
for y in (0.32, 0.52, 0.7):
    lathe('band', 'j', [(0.19 - y * 0.02, y - 0.03), (0.21 - y * 0.02, y - 0.02), (0.21 - y * 0.02, y + 0.02), (0.19 - y * 0.02, y + 0.03)], n=16, sharp=30)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    box('fin', 'j', (math.cos(a) * 0.2, 0.42, -math.sin(a) * 0.2), (0.05, 0.22, 0.02), 0.006, rot=(0, a, 0))
for k in range(3): box('slit', 'e', (0.19 - 0.42 * 0.02 + 0.01, 0.38 + k * 0.08 - 0.08, 0), (0.012, 0.04, 0.06), 0.004)

node('muzzle', 'tube', (0, 0.95, 0), (math.pi / 2, 0, 0))
torus('ring', 'e', (0, 0, 0), 0.17, 0.04, nu=20, nv=6, rot=(math.pi / 2, 0, 0))

save(out_dir(), 'mortar')
