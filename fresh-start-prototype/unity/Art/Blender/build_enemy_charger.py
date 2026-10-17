# The charger: an armoured bull-like quadruped that rams. Four gunmetal legs on glowing hinge drums with exposed
# rams, ceramic thigh plates, knee rings and rubber-soled split hooves (legsC[0..3], which the rig swings as it
# gallops), a deep graphite barrel body with layered ribs, a gunmetal chest with vents, a riveted rump and two exhaust
# thrusters (torso), a great riveted ceramic ram plate for a head with glowing slits and a vented jaw (ram), two
# magenta horns that flare from collared sockets (horns[0], [1]) and a riveted ceramic back plate over a gunmetal
# undercut with a glowing spine, which the fight breaks off (plates[0]). Run: blender -b -P build_enemy_charger.py --
# <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs (each from its hip pivot down to the hoof 0.6 below) ----
for i, (x, z) in enumerate(((0.38, 0.3), (-0.38, 0.3), (0.38, -0.3), (-0.38, -0.3))):
    node(f'legsC[{i}]', 'body', (x, 0.62, z))
    front = x > 0; s = 1 if z > 0 else -1
    joint('hip', (0, 0, 0), 0.11, 0.2, bolts=6 if s > 0 else 0, glow=s > 0)
    box('thigh', 'p', (0.02 if front else -0.02, -0.12, 0), (0.22, 0.3, 0.16), 0.04, taper=(1.1, 1.0), btaper=(0.6, 0.8))
    box('thigh_stripe', 'e', (0.02 if front else -0.02, -0.08, s * 0.081), (0.12, 0.018, 0.01), 0.0)
    cyl('shank', 'm', (0, -0.18, 0), (0, -0.5, 0), 0.07, 0.06, n=8)
    cyl('knee_ring', 'p', (0, -0.3, 0), (0, -0.26, 0), 0.078, n=8, bev=0.01)
    piston('piston', (-0.075 if front else 0.075, -0.06, s * 0.02), (-0.055 if front else 0.055, -0.4, s * 0.02), 0.026, n=8, collar=False, line=False)
    box('hoof', 'p', (0.03, -0.54, 0), (0.2, 0.12, 0.17), 0.03, taper=(0.75, 0.85), shift=(-0.02, 0))
    box('hoof_split', 'm', (0.12, -0.56, 0), (0.06, 0.09, 0.025), 0.0)
    box('sole', 'r', (0.03, -0.6, 0), (0.22, 0.03, 0.19), 0.008)

# ---- The barrel body ----
node('torso', 'body', (0, 0.9, 0))
box('barrel', 'j', (-0.06, 0.0, 0), (1.0, 0.6, 0.76), 0.12, taper=(0.9, 0.9), btaper=(0.85, 0.9))
box('chest', 'm', (0.32, -0.06, 0), (0.3, 0.5, 0.7), 0.08, btaper=(0.8, 0.85))
box('rump', 'p', (-0.5, 0.06, 0), (0.18, 0.42, 0.62), 0.05, taper=(0.8, 0.85))
rivets('c', [(-0.59, y, z) for y in (0.17, -0.06) for z in (0.2, -0.2)], (-1, 0, 0), r=0.016, h=0.012)
for z in (0.39, -0.39):
    for k in range(3): armour('rib',  (-0.28 + k * 0.2, 0.0, z), (0.1, 0.4, 0.04), out=(0, 0, 1 if z > 0 else -1), bev=0.015, lip=0.01, taper=(0.8, 1.0), btaper=(0.7, 1.0))
    box('flank_light', 'e', (0.12, -0.12, z * 1.03), (0.24, 0.03, 0.012), 0.0)
    grille('chest_vent', (0.36, 0.04, z * 0.9), (0.02, 0.16, 0.14), n=3, rot=(0, -math.pi / 2 if z > 0 else math.pi / 2, 0))
for z in (0.18, -0.18): thruster('exhaust', (-0.44, 0.24, z), 0.06, 0.15, rot=(0, 0, -2.08), n=8, rings=False)
box('tail', 'm', (-0.6, 0.05, 0), (0.08, 0.14, 0.14), 0.02)
box('tail_light', 'e', (-0.642, 0.05, 0), (0.008, 0.05, 0.08), 0.0)

# ---- The ram (a mesh of its own: the head-plate) ----
node('ram', 'torso', (0.56, 0.02, 0))
box('plate', 'p', (0.0, 0.0, 0), (0.28, 0.76, 0.9), 0.06, taper=(0.75, 0.85), btaper=(0.85, 0.9), shift=(-0.04, 0))
box('brow', 'p', (0.06, 0.24, 0), (0.2, 0.12, 0.8), 0.04, taper=(0.6, 0.85))
box('jaw', 'm', (0.08, -0.32, 0), (0.18, 0.14, 0.62), 0.035, btaper=(0.7, 0.85))
grille('jaw_vent', (0.17, -0.32, 0), (0.02, 0.08, 0.4), n=3, frame=False)
for y in (-0.12, 0.08): box('slit', 'e', (0.15, y, 0), (0.03, 0.045, 0.56), 0.008, taper=(1, 0.9))
for z in (0.3, -0.3): box('cheek', 'm', (0.1, -0.02, z), (0.1, 0.28, 0.14), 0.025)
rivets('c', [(x, y, z) for x, y in ((0.108, -0.22), (0.082, 0.16)) for z in (0.37, -0.37)], (1, 0.07, 0), r=0.016, h=0.012)

# ---- Horns (each centred on its own mesh, along its local y; the rig tips them forward) ----
for i, z in enumerate((0.3, -0.3)):
    node(f'horns[{i}]', 'torso', (0.66, 0.46, z), (0, 0, -1.0))
    cyl('base', 'e', (0.01, -0.2, 0), (0.0, 0.02, 0), 0.08, 0.06, n=8)
    cyl('tip', 'e', (0.0, 0.02, 0), (-0.06, 0.21, 0), 0.06, 0.008, n=8)
    cyl('collar', 'm', (0.012, -0.24, 0), (0.008, -0.14, 0), 0.098, 0.09, n=8)

# ---- The back plate ----
node('plates[0]', 'torso', (-0.12, 0.4, 0))
box('shell', 'p', (0, 0, 0), (0.72, 0.12, 0.84), 0.05, taper=(0.85, 0.75))
for x in (-0.2, 0.05, 0.28): box('spine', 'p', (x, 0.08, 0), (0.14, 0.06, 0.2), 0.02, taper=(0.5, 0.6))
for x in (-0.075, 0.165): box('spine_light', 'e', (x, 0.065, 0), (0.08, 0.012, 0.03), 0.0)
rivets('c', [(x, 0.06, z) for x in (-0.24, 0.24) for z in (0.25, -0.25)], (0, 1, 0), r=0.016, h=0.01)
box('shell_under', 'm', (0, -0.05, 0), (0.66, 0.04, 0.78), 0.01)

save(out_dir(), 'charger')
