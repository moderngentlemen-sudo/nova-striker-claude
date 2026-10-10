# The charger: an armoured bull-like quadruped that rams. Four pistoned legs with ceramic thigh plates and hooves
# (legsC[0..3], which the rig swings as it gallops), a deep graphite barrel body with ribs and exhausts (torso), a
# great ceramic ram plate for a head with glowing slits (ram), two magenta horns that flare from it (horns[0], [1])
# and a ceramic back plate the fight breaks off (plates[0]). Run: blender -b -P build_enemy_charger.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs (each from its hip pivot down to the hoof 0.6 below) ----
for i, (x, z) in enumerate(((0.38, 0.3), (-0.38, 0.3), (0.38, -0.3), (-0.38, -0.3))):
    node(f'legsC[{i}]', 'body', (x, 0.62, z))
    front = x > 0
    sphere('hip', 'j', (0, 0, 0), 0.11, nu=12, nv=6)
    box('thigh', 'p', (0.02 if front else -0.02, -0.12, 0), (0.22, 0.3, 0.16), 0.04, taper=(1.1, 1.0), btaper=(0.6, 0.8))
    cyl('shank', 'j', (0, -0.18, 0), (0, -0.5, 0), 0.07, 0.06, n=10)
    cyl('piston', 'p', (-0.07 if front else 0.07, -0.08, 0), (-0.05 if front else 0.05, -0.4, 0), 0.025, n=8)
    box('hoof', 'p', (0.03, -0.54, 0), (0.2, 0.12, 0.17), 0.03, taper=(0.75, 0.85), shift=(-0.02, 0))
    box('sole', 'j', (0.03, -0.6, 0), (0.21, 0.03, 0.18), 0.008)

# ---- The barrel body ----
node('torso', 'body', (0, 0.9, 0))
box('barrel', 'j', (-0.06, 0.0, 0), (1.0, 0.6, 0.76), 0.12, taper=(0.9, 0.9), btaper=(0.85, 0.9))
box('chest', 'j', (0.32, -0.06, 0), (0.3, 0.5, 0.7), 0.08, btaper=(0.8, 0.85))
box('rump', 'p', (-0.5, 0.06, 0), (0.18, 0.42, 0.62), 0.05, taper=(0.8, 0.85))
for z in (0.39, -0.39):
    for k in range(3): box('rib', 'p', (-0.28 + k * 0.2, 0.0, z), (0.1, 0.4, 0.04), 0.015, taper=(0.8, 1.0), btaper=(0.7, 1.0))
    box('flank_light', 'e', (0.12, -0.12, z * 1.03), (0.24, 0.03, 0.012), 0.004)
for z in (0.18, -0.18): cyl('exhaust', 'j', (-0.42, 0.24, z), (-0.6, 0.34, z), 0.055, 0.05, n=10)
box('tail', 'j', (-0.6, 0.05, 0), (0.08, 0.14, 0.14), 0.02)

# ---- The ram (a mesh of its own: the head-plate) ----
node('ram', 'torso', (0.56, 0.02, 0))
box('plate', 'p', (0.0, 0.0, 0), (0.28, 0.76, 0.9), 0.06, taper=(0.75, 0.85), btaper=(0.85, 0.9), shift=(-0.04, 0))
box('brow', 'p', (0.06, 0.24, 0), (0.2, 0.12, 0.8), 0.04, taper=(0.6, 0.85))
box('jaw', 'j', (0.08, -0.32, 0), (0.18, 0.14, 0.62), 0.035, btaper=(0.7, 0.85))
for y in (-0.12, 0.08): box('slit', 'e', (0.15, y, 0), (0.03, 0.045, 0.56), 0.008, taper=(1, 0.9))
for z in (0.3, -0.3): box('cheek', 'j', (0.1, -0.02, z), (0.1, 0.28, 0.14), 0.025)

# ---- Horns (each centred on its own mesh, along its local y; the rig tips them forward) ----
for i, z in enumerate((0.3, -0.3)):
    node(f'horns[{i}]', 'torso', (0.66, 0.46, z), (0, 0, -1.0))
    cyl('base', 'e', (0.01, -0.2, 0), (0.0, 0.02, 0), 0.08, 0.06, n=8)
    cyl('tip', 'e', (0.0, 0.02, 0), (-0.06, 0.21, 0), 0.06, 0.008, n=8)

# ---- The back plate ----
node('plates[0]', 'torso', (-0.12, 0.4, 0))
box('shell', 'p', (0, 0, 0), (0.72, 0.12, 0.84), 0.05, taper=(0.85, 0.75))
for x in (-0.2, 0.05, 0.28): box('spine', 'p', (x, 0.08, 0), (0.14, 0.06, 0.2), 0.02, taper=(0.5, 0.6))

save(out_dir(), 'charger')
