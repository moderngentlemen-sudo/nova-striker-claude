# The brute: a hulking gorilla-built bruiser. Stumpy graphite legs with riveted ceramic thigh plates, shin guards
# and hydraulic knee rams (the body), a massive hunched graphite torso round its magenta core, with riveted flank
# armour, a finned heat sink on its hump, a back louvre, twin exhaust thrusters and feed hoses to the neck (torso,
# core), under three armour plates the fight breaks off one by one (plates[0] the riveted chest frame, plates[1] and
# [2] the lipped shoulders with their glowing marks), a small low-set head with a visor slit, a jaw grille and an
# ear sensor (head), and long arms with elbow rams, hoses and huge ceramic fists (armN, armF).
# Run: blender -b -P build_enemy_brute.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs (body: stumpy columns from the hips at 1.2 to the ground) ----
at('body')
for z in (0.36, -0.36):
    s = 1 if z > 0 else -1
    cyl('thigh', 'j', (0, 1.2, z), (0.05, 0.62, z), 0.2, 0.17, n=14)
    cyl('shin', 'j', (0.05, 0.62, z), (0, 0.14, z), 0.17, 0.19, n=14)
    sphere('knee', 'j', (0.05, 0.62, z), 0.18, nu=14, nv=8)
    box('thighplate', 'p', (0.12, 0.92, z), (0.2, 0.42, 0.36), 0.05, taper=(0.8, 0.85), btaper=(0.9, 0.9))
    rivets('j', [(0.215, y, z + t) for y in (1.0, 0.82) for t in (0.11, -0.11)], (1, 0, 0), r=0.018, h=0.012)
    box('kneecap', 'p', (0.2, 0.62, z), (0.1, 0.22, 0.26), 0.035, taper=(0.6, 0.7), btaper=(0.6, 0.7))
    box('shinguard', 'p', (0.2, 0.36, z), (0.08, 0.3, 0.26), 0.03, taper=(1.0, 0.9), btaper=(0.8, 0.8))
    box('shin_light', 'e', (0.242, 0.38, z), (0.01, 0.14, 0.05), 0.0)
    piston('knee_ram', (0.02, 1.02, z + s * 0.2), (0.06, 0.38, z + s * 0.2), 0.04, n=8)
    box('ram_lug', 'j', (0.06, 0.36, z + s * 0.17), (0.1, 0.08, 0.08), 0.0)
    box('foot', 'p', (0.08, 0.09, z), (0.56, 0.18, 0.42), 0.05, taper=(0.7, 0.85), shift=(-0.05, 0))
    box('sole', 'j', (0.08, 0.02, z), (0.58, 0.04, 0.44), 0.0)
    for t in (-0.12, 0, 0.12): box('toe', 'j', (0.34, 0.06, z + t), (0.06, 0.1, 0.1), 0.02)
    box('heel', 'j', (-0.2, 0.1, z), (0.08, 0.12, 0.3), 0.02, taper=(0.8, 0.9))

# ---- Torso (1.4 up): the graphite bulk, belly, back hump and vents ----
node('torso', 'body', (0, 1.4, 0))
box('pelvis', 'j', (-0.02, -0.1, 0), (0.7, 0.24, 0.82), 0.05, taper=(1.1, 1.1))
box('bulk', 'j', (-0.04, 0.38, 0), (1.0, 0.82, 1.04), 0.12, taper=(1.0, 1.0), btaper=(0.78, 0.82))
box('hump', 'j', (-0.22, 0.78, 0), (0.6, 0.22, 0.78), 0.08, taper=(0.7, 0.8))
fins('sink', 'j', (-0.22, 0.925, 0), (0.3, 0.07, 0.48), 6)
box('sink_glow', 'e', (-0.22, 0.895, 0), (0.26, 0.02, 0.46), 0.0)
box('belly', 'j', (0.36, 0.08, 0), (0.18, 0.3, 0.6), 0.05, taper=(0.9, 0.9), btaper=(0.7, 0.8))
box('backplate', 'p', (-0.5, 0.42, 0), (0.12, 0.5, 0.6), 0.04, taper=(0.8, 0.8))
grille('louvre', (-0.565, 0.4, 0), (0.03, 0.3, 0.38), n=4, rot=(0, math.pi, 0))
rivets('j', [(-0.565, y, z) for y in (0.62, 0.2) for z in (0.24, -0.24)], (-1, 0, 0), r=0.016, h=0.01)
for z in (0.28, -0.28): thruster('exhaust', (-0.4, 0.84, z), 0.08, 0.22, rot=(0, 0, -2.553), n=10)
for z in (0.5, -0.5):                                  # flank armour between the arms
    s = 1 if z > 0 else -1
    box('flank', 'p', (-0.06, 0.5, z), (0.5, 0.34, 0.04), 0.012, btaper=(0.9, 1.0))
    rivets('j', [(x, y, z + s * 0.02) for x in (-0.25, 0.13) for y in (0.62, 0.38)], (0, 0, s), r=0.014, h=0.01)
    box('flank_light', 'e', (-0.06, 0.5, z + s * 0.022), (0.24, 0.025, 0.008), 0.0)
for z in (0.13, -0.13):                                # feed hoses from the hump to the neck
    tube('hose', 'j', bez((-0.3, 0.86, z), (-0.06, 1.08, z), (0.16, 0.92, z * 0.8), 5), 0.035, n=6)
cyl('socket', 'j', (0.38, 0.4, 0), (0.56, 0.4, 0), 0.26, 0.24, n=18)
cyl('neck', 'j', (0.12, 0.9, 0), (0.24, 1.0, 0), 0.13, 0.11, n=12)
for z in (0.62, -0.62): sphere('shoulder', 'j', (0, 0.78, z), 0.16, nu=14, nv=8)

node('core', 'torso', (0.5, 0.4, 0))
sphere('orb', 'e', (0.02, 0, 0), (0.17, 0.2, 0.2), nu=16, nv=10)

# ---- Armour plates (each centred on its own mesh) ----
node('plates[0]', 'torso', (0.52, 0.35, 0))          # the chest frame round the core
box('top', 'p', (0.0, 0.3, 0), (0.28, 0.22, 0.92), 0.05, taper=(0.75, 0.9))
box('top_ridge', 'p', (-0.02, 0.43, 0), (0.16, 0.05, 0.7), 0.015, taper=(0.7, 0.9))
box('bottom', 'p', (-0.02, -0.3, 0), (0.24, 0.2, 0.84), 0.05, btaper=(0.8, 0.9))
for z in (0.34, -0.34): box('side', 'p', (0.0, 0.0, z), (0.28, 0.46, 0.26), 0.05, taper=(1, 1))
rivets('j', [(0.14, y, z) for y in (0.17, -0.17) for z in (0.36, -0.36)], (1, 0, 0), r=0.02, h=0.012)
for y in (0.3, -0.3): box('frame_light', 'e', (0.13, y, 0), (0.012, 0.03, 0.3), 0.0, rot=(0, 0, 0.3 if y > 0 else 0))
for i, s in ((1, 1), (2, -1)):
    node(f'plates[{i}]', 'torso', (0, 0.98, s * 0.42))
    box('pauldron', 'p', (0, 0, s * 0.04), (0.72, 0.24, 0.44), 0.06, taper=(0.8, 0.7))
    box('ridge', 'p', (-0.02, 0.13, s * 0.06), (0.5, 0.08, 0.14), 0.025, taper=(0.7, 0.6))
    box('lip', 'j', (0, -0.13, s * 0.06), (0.7, 0.05, 0.42), 0.012)
    box('mark', 'e', (0.326, 0.0, s * 0.04), (0.012, 0.05, 0.25), 0.0, rot=(0, 0, 0.29))
    rivets('j', [(x, 0.1, s * 0.2) for x in (-0.22, 0.0, 0.22)], (0, 0.6, s), r=0.016, h=0.012)

# ---- Head (a mesh of its own, low between the shoulders) ----
node('head', 'torso', (0.3, 1.02, 0))
box('skull', 'p', (0, 0.01, 0), (0.34, 0.26, 0.34), 0.06, taper=(0.8, 0.85))
box('brow', 'p', (0.12, 0.08, 0), (0.12, 0.06, 0.36), 0.02, rot=(0, 0, -0.3))
box('jaw', 'j', (0.1, -0.1, 0), (0.18, 0.08, 0.26), 0.02, btaper=(0.8, 0.8))
grille('jaw_grille', (0.19, -0.1, 0), (0.02, 0.06, 0.16), n=3, glow=None)
box('visor', 'e', (0.17, 0.0, 0), (0.04, 0.045, 0.26), 0.008)
sensor('ear', (-0.02, 0.0, 0.17), 0.035, rot=(0, -math.pi / 2, 0), n=8)
cyl('antenna', 'j', (-0.1, 0.12, -0.08), (-0.2, 0.36, -0.1), 0.012, 0.008, n=5)
box('antenna_tip', 'e', (-0.2, 0.36, -0.1), (0.03, 0.03, 0.03), 0.0)

# ---- Arms (pivot at the shoulder; fist 1.0 below) ----
for nm, s in (('armN', 1), ('armF', -1)):
    node(nm, 'torso', (0, 0.8, s * 0.72))
    cyl('upper', 'j', (0, 0.02, 0), (0.06, -0.48, 0), 0.17, 0.14, n=12)
    sphere('elbow', 'j', (0.06, -0.48, 0), 0.15, nu=12, nv=6)
    cyl('fore', 'j', (0.06, -0.48, 0), (0.05, -0.8, 0), 0.15, 0.17, n=12)
    box('bicep', 'p', (0.08, -0.22, s * 0.03), (0.2, 0.34, 0.26), 0.04, taper=(0.85, 0.85))
    piston('elbow_ram', (0.03, -0.12, s * 0.16), (0.07, -0.6, s * 0.17), 0.032, n=8)
    tube('hose', 'j', bez((-0.1, -0.08, 0), (-0.26, -0.45, 0), (-0.12, -0.7, 0), 5), 0.03, n=6)
    box('gauntlet', 'p', (0.08, -0.66, 0), (0.32, 0.3, 0.34), 0.05, btaper=(1.1, 1.1))
    box('gauntlet_light', 'e', (0.08, -0.64, s * 0.178), (0.2, 0.03, 0.01), 0.0)
    rivets('j', [(0.08 + x, -0.56, s * 0.17) for x in (-0.1, 0.1)], (0, 0, s), r=0.016, h=0.01)
    box('fist', 'p', (0.05, -1.0, 0), (0.5, 0.42, 0.46), 0.08, taper=(0.9, 0.92))
    for t in (-0.08, 0.08): box('finger_seam', 'j', (0.06, -1.0 + t, s * 0.226), (0.36, 0.02, 0.02), 0.0)
    box('knuckles', 'j', (0.28, -1.0, 0), (0.08, 0.32, 0.4), 0.03)
    for k in (-0.12, 0.0, 0.12): box('knuckle_light', 'e', (0.325, -1.0 + k, 0), (0.012, 0.05, 0.3), 0.004)

save(out_dir(), 'brute')
