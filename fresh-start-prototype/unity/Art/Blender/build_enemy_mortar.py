# The mortar: a squat artillery walker. A riveted graphite turntable on two short track pods with road wheels,
# drive sprockets and riveted, lit track guards (the body), an armoured ceramic hull with a riveted sloped glacis,
# bolted ammunition drums, a visor slit, a sensor mast, a whip antenna and an engine grille with an exhaust stack
# (torso, eye), and a heavy banded launch tube with cooling rings, recoil rams and a side sight in a recoil cradle
# (tube, which the rig raises and squashes as it fires; muzzle: the glowing ring at its mouth).
# Run: blender -b -P build_enemy_mortar.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
at('body')
lathe('turntable', 'j', [(0.5, 0.3), (0.56, 0.26), (0.56, 0.18), (0.44, 0.14)], n=16, sharp=30, bev=0.01)
rivets('p', [(math.cos(a) * 0.47, 0.3, -math.sin(a) * 0.47) for a in (k * TAU / 8 + math.pi / 8 for k in range(8))], (0, 1, 0), r=0.016, h=0.01)
for z in (0.38, -0.38):
    s = 1 if z > 0 else -1
    prism('track', 'j', [(-0.6, 0.06), (-0.5, 0.0), (0.5, 0.0), (0.62, 0.08), (0.52, 0.2), (-0.52, 0.2)], z - 0.12, z + 0.12, 0.02)
    prism('trackguard', 'p', [(-0.56, 0.16), (0.56, 0.16), (0.6, 0.22), (0.5, 0.26), (-0.5, 0.26), (-0.6, 0.22)], z - 0.13, z + 0.13, 0.02)
    rivets('j', [(x, 0.26, z + t) for x in (-0.36, 0.0, 0.36) for t in (0.08, -0.08)], (0, 1, 0), r=0.014, h=0.01)
    box('guard_light', 'e', (0.0, 0.205, z + s * 0.131), (0.5, 0.02, 0.008), 0.0)
    for x in (-0.39, -0.13, 0.13, 0.39):
        cyl('wheel', 'p', (x, 0.09, z + s * 0.12), (x, 0.09, z + s * 0.135), 0.062, n=10)
        cyl('hub', 'j', (x, 0.09, z + s * 0.135), (x, 0.09, z + s * 0.145), 0.026, n=6)
    for x in (-0.53, 0.53): cyl('sprocket', 'j', (x, 0.1, z + s * 0.11), (x, 0.1, z + s * 0.14), 0.07, n=8)
    for k in range(8): box('tread', 'j', (-0.42 + k * 0.12, 0.01, z), (0.05, 0.03, 0.25), 0.0)
box('axle', 'j', (0, 0.12, 0), (0.5, 0.12, 0.56), 0.02)

node('torso', 'body', (0, 0.55, 0))
box('hull', 'p', (-0.02, 0.0, 0), (0.76, 0.46, 0.8), 0.06, taper=(0.86, 0.9))
box('glacis', 'p', (0.34, -0.02, 0), (0.12, 0.38, 0.72), 0.04, taper=(0.4, 0.95), shift=(-0.05, 0))
rivets('j', [(x, y, z) for x, y in ((0.381, -0.12), (0.356, 0.08)) for z in (0.29, -0.29)], (0.975, 0.22, 0), r=0.016, h=0.012)
box('skirt', 'j', (0, -0.21, 0), (0.8, 0.08, 0.84), 0.02)
box('roof', 'j', (-0.06, 0.24, 0), (0.5, 0.04, 0.6), 0.015)
for z in (0.44, -0.44):
    s = 1 if z > 0 else -1
    cyl('drum', 'j', (-0.25, 0.02, z), (0.15, 0.02, z), 0.14, n=14)
    cyl('drumcap', 'p', (-0.27, 0.02, z), (-0.21, 0.02, z), 0.15, n=14)
    cyl('drumcap_f', 'p', (0.12, 0.02, z), (0.17, 0.02, z), 0.15, n=14)
    rivets('j', [(-0.27, 0.02 + math.sin(a) * 0.09, z + math.cos(a) * 0.09) for a in (0.785, 2.356, 3.927, 5.498)], (-1, 0, 0), r=0.016, h=0.01)
    for x in (-0.08, 0.06): cyl('drumband', 'e', (x, 0.02, z), (x + 0.02, 0.02, z), 0.142, n=14)
    box('drum_strap', 'j', (-0.15, 0.02, z), (0.04, 0.3, 0.3), 0.0)
# the engine at the back: a grille, an exhaust box and its stack
box('exhaust', 'j', (-0.42, 0.08, 0.2), (0.08, 0.2, 0.12), 0.02)
thruster('stack', (-0.42, 0.17, 0.2), 0.05, 0.12, rot=(0, 0, math.pi), n=10)
grille('engine', (-0.395, -0.02, -0.14), (0.03, 0.2, 0.24), n=4, rot=(0, math.pi, 0))
# the sensor mast and the antenna on the roof's back corners
box('mast', 'j', (-0.24, 0.32, -0.22), (0.05, 0.16, 0.05), 0.0)
box('mast_head', 'p', (-0.22, 0.42, -0.22), (0.12, 0.07, 0.09), 0.015, taper=(0.8, 0.9))
sensor('mast_eye', (-0.16, 0.42, -0.22), 0.026, n=8)
cyl('antenna', 'j', (-0.3, 0.24, 0.24), (-0.38, 0.75, 0.27), 0.012, 0.007, n=5)
box('antenna_tip', 'e', (-0.38, 0.75, 0.27), (0.03, 0.03, 0.03), 0.0)

node('eye', 'torso', (0.41, 0.06, 0))
box('visor', 'e', (-0.02, 0, 0), (0.04, 0.05, 0.44), 0.01, taper=(1, 0.85))

node('tube', 'torso', (0.05, 0.22, 0), (0, 0, -0.45))
box('cradle', 'j', (0, 0.06, 0), (0.36, 0.16, 0.5), 0.03)
cyl('trunnion', 'p', (0, 0.06, -0.3), (0, 0.06, 0.3), 0.08, n=12)
for z in (0.31, -0.31): cyl('trunnion_cap', 'j', (0, 0.06, z), (0, 0.06, z * 1.06), 0.05, n=8)
lathe('barrel', 'p', [(0.22, 0.0), (0.22, 0.12), (0.2, 0.16), (0.18, 0.8), (0.2, 0.84), (0.2, 0.93), (0.15, 0.93)], n=16, sharp=40, bev=0.008)
for y in (0.2, 0.245): lathe('cooler', 'j', [(0.2, y - 0.012), (0.232, y - 0.006), (0.232, y + 0.006), (0.2, y + 0.012)], n=16, sharp=30)
for y in (0.32, 0.52, 0.7):
    lathe('band', 'j', [(0.19 - y * 0.02, y - 0.03), (0.21 - y * 0.02, y - 0.02), (0.21 - y * 0.02, y + 0.02), (0.19 - y * 0.02, y + 0.03)], n=16, sharp=30)
for z in (0.25, -0.25):                                # recoil rams from the cradle to the middle band
    piston('recoil', (0.0, 0.13, z), (0.0, 0.5, z * 0.9), 0.03, n=8)
    box('recoil_lug', 'j', (0.0, 0.52, z * 0.86), (0.07, 0.06, 0.08), 0.0)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    box('fin', 'j', (math.cos(a) * 0.2, 0.42, -math.sin(a) * 0.2), (0.05, 0.22, 0.02), 0.006, rot=(0, a, 0))
for k in range(3): box('slit', 'e', (0.19 - 0.42 * 0.02 + 0.01, 0.38 + k * 0.08 - 0.08, 0), (0.012, 0.04, 0.06), 0.004)
box('sight', 'j', (-0.06, 0.62, 0.2), (0.08, 0.14, 0.06), 0.012)
sensor('sight_eye', (-0.06, 0.7, 0.2), 0.022, rot=(0, 0, math.pi / 2), n=8)

node('muzzle', 'tube', (0, 0.95, 0), (math.pi / 2, 0, 0))
torus('ring', 'e', (0, 0, 0), 0.17, 0.04, nu=20, nv=6, rot=(math.pi / 2, 0, 0))

save(out_dir(), 'mortar')
