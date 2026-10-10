# The turret: a squat armoured dome bolted to the deck. A faceted ceramic cupola on a graphite base ring with vents
# (the body), and a gun mantlet with twin barrels and the magenta sight at the muzzle (gun, eye), which the rig
# turns toward its target. Run: blender -b -P build_enemy_turret.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
at('body')
lathe('base', 'j', [(0.46, 0.0), (0.46, 0.05), (0.42, 0.08)], n=12, sharp=30, bev=0.01)
lathe('dome', 'p', [(0.41, 0.06), (0.41, 0.14), (0.38, 0.26), (0.3, 0.36), (0.17, 0.42), (0.0, 0.43)], n=12, sharp=30, bev=0.012)
lathe('band', 'j', [(0.405, 0.15), (0.425, 0.16), (0.425, 0.2), (0.4, 0.21)], n=12, sharp=30)
for k in range(6):
    a = math.pi / 2 + (k - 2.5) * 0.42
    box('vent', 'e', (math.cos(a) * 0.405, 0.12, -math.sin(a) * 0.405), (0.02, 0.025, 0.09), 0.004, rot=(0, a, 0))
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    box('bolt', 'p', (math.cos(a) * 0.44, 0.06, -math.sin(a) * 0.44), (0.07, 0.05, 0.07), 0.012, rot=(0, a, 0))
box('hatch', 'j', (-0.12, 0.395, 0), (0.18, 0.04, 0.2), 0.015, rot=(0, 0, 0.35))

node('gun', 'body', (0, 0.25, 0))
box('mantlet', 'p', (0.36, 0, 0), (0.2, 0.22, 0.3), 0.04, taper=(0.75, 0.8), btaper=(0.75, 0.8))
box('cheek', 'j', (0.28, 0, 0), (0.12, 0.16, 0.36), 0.025)
for z in (0.065, -0.065):
    cyl('barrel', 'j', (0.38, 0, z), (0.78, 0, z), 0.045, 0.038, n=10)
    cyl('collar', 'p', (0.6, 0, z), (0.66, 0, z), 0.052, n=10)
box('bridge', 'j', (0.72, 0, 0), (0.06, 0.05, 0.13), 0.01)

node('eye', 'gun', (0.82, 0, 0))
sphere('sight', 'e', (0, 0, 0), (0.04, 0.06, 0.06), nu=12, nv=8)

save(out_dir(), 'turret')
