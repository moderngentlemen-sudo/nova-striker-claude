# The turret: a squat armoured dome bolted to the deck. A faceted ceramic cupola in two tiers split by a graphite
# seam ring, on a riveted graphite base ring with glowing vents and anchor clamps, a hatch, a periscope sensor, a rear
# louvre, an ammunition hopper and a whip antenna (the body), and a gun mantlet with twin shrouded barrels, muzzle
# brakes, recoil buffers, a side drum and feed chute and the magenta sight at the muzzle (gun, eye), which the rig
# turns toward its target. Run: blender -b -P build_enemy_turret.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
at('body')
lathe('base', 'j', [(0.46, 0.0), (0.46, 0.05), (0.42, 0.08)], n=12, sharp=30, bev=0.01)
rivets('p', [(math.cos(a) * 0.44, 0.062, -math.sin(a) * 0.44) for a in (k * TAU / 12 for k in range(12)) if abs(math.cos(a - math.pi / 4)) < 0.94 and abs(math.sin(a - math.pi / 4)) < 0.94], (0, 1, 0), r=0.013, h=0.01)
lathe('dome', 'p', [(0.41, 0.06), (0.41, 0.14), (0.38, 0.26), (0.3, 0.36), (0.17, 0.42), (0.0, 0.43)], n=12, sharp=30, bev=0.012)
lathe('band', 'j', [(0.405, 0.15), (0.425, 0.16), (0.425, 0.2), (0.4, 0.21)], n=12, sharp=30)
lathe('seam', 'j', [(0.34, 0.272), (0.37, 0.286), (0.352, 0.316), (0.318, 0.33)], n=12, sharp=30)
for k in range(6):
    a = math.pi / 2 + (k - 2.5) * 0.42
    box('vent', 'e', (math.cos(a) * 0.405, 0.12, -math.sin(a) * 0.405), (0.02, 0.025, 0.09), 0.0, rot=(0, a, 0))
for k in range(4):                                     # anchor clamps, each with its bolt
    a = k * math.pi / 2 + math.pi / 4
    box('clamp', 'p', (math.cos(a) * 0.45, 0.05, -math.sin(a) * 0.45), (0.11, 0.06, 0.09), 0.012, rot=(0, a, 0), taper=(0.7, 0.9))
    rivets('j', [(math.cos(a) * 0.465, 0.08, -math.sin(a) * 0.465)], (0, 1, 0), r=0.018, h=0.012)
box('hatch', 'j', (-0.12, 0.395, 0), (0.18, 0.04, 0.2), 0.015, rot=(0, 0, 0.35))
box('hatch_lid', 'p', (-0.115, 0.413, 0), (0.13, 0.02, 0.15), 0.006, rot=(0, 0, 0.35))
box('hatch_handle', 'j', (-0.11, 0.43, 0), (0.02, 0.02, 0.08), 0.0, rot=(0, 0, 0.35))
# the periscope on the near side of the roof
box('periscope', 'j', (0.05, 0.44, 0.12), (0.12, 0.08, 0.09), 0.012, taper=(0.8, 0.9))
sensor('periscope_eye', (0.11, 0.45, 0.12), 0.024, n=8)
# the rear louvre, the hopper on the far side, the antenna
grille('louvre', (-0.392, 0.21, 0), (0.03, 0.08, 0.2), n=3, rot=(0, math.pi, -0.25))
box('hopper', 'j', (-0.12, 0.2, -0.38), (0.22, 0.16, 0.08), 0.02, taper=(0.9, 0.8))
box('hopper_lid', 'p', (-0.12, 0.285, -0.375), (0.2, 0.02, 0.07), 0.006)
box('status', 'e', (-0.14, 0.26, 0.364), (0.12, 0.018, 0.012), 0.0, rot=(-0.4, 0, 0))
cyl('antenna', 'j', (-0.25, 0.3, -0.12), (-0.33, 0.66, -0.15), 0.01, 0.006, n=5)
box('antenna_tip', 'e', (-0.33, 0.66, -0.15), (0.025, 0.025, 0.025), 0.0)

node('gun', 'body', (0, 0.25, 0))
box('mantlet', 'p', (0.36, 0, 0), (0.2, 0.22, 0.3), 0.04, taper=(0.75, 0.8), btaper=(0.75, 0.8))
box('mantlet_brow', 'p', (0.34, 0.12, 0), (0.18, 0.04, 0.26), 0.012, taper=(0.8, 0.9))
rivets('j', [(0.46, y, z) for y in (0.07, -0.07) for z in (0.11, -0.11)], (1, 0, 0), r=0.012, h=0.008)
box('cheek', 'j', (0.28, 0, 0), (0.12, 0.16, 0.36), 0.025)
for z in (0.065, -0.065):
    cyl('barrel', 'j', (0.38, 0, z), (0.78, 0, z), 0.045, 0.038, n=10)
    cyl('shroud', 'p', (0.46, 0, z), (0.56, 0, z), 0.054, n=10)
    for x in (0.475, 0.505, 0.535): cyl('shroud_vent', 'j', (x, 0, z), (x + 0.012, 0, z), 0.057, n=10)
    cyl('collar', 'p', (0.6, 0, z), (0.66, 0, z), 0.052, n=10)
    box('brake', 'j', (0.765, 0, z), (0.05, 0.075, 0.06), 0.008)
    box('brake_slot', 'e', (0.765, 0, z), (0.014, 0.078, 0.064), 0.0)
    piston('buffer', (0.42, 0.075, z * 0.6), (0.6, 0.06, z * 0.6), 0.018, n=6, collar=False)
box('bridge', 'j', (0.72, 0, 0), (0.06, 0.05, 0.13), 0.01)
# the drum on the near cheek and the chute that feeds the barrels
cyl('drum', 'j', (0.26, -0.02, 0.18), (0.26, -0.02, 0.27), 0.085, n=12)
cyl('drum_cap', 'p', (0.26, -0.02, 0.27), (0.26, -0.02, 0.285), 0.07, n=12)
box('drum_light', 'e', (0.26, -0.02, 0.288), (0.07, 0.014, 0.008), 0.0)
tube('chute', 'j', bez((0.3, -0.08, 0.2), (0.38, -0.12, 0.17), (0.4, -0.08, 0.12), 3), 0.022, n=5)

node('eye', 'gun', (0.82, 0, 0))
sphere('sight', 'e', (0, 0, 0), (0.04, 0.06, 0.06), nu=12, nv=8)

save(out_dir(), 'turret')
