# The brute: a hulking gorilla-built bruiser. Stumpy gunmetal legs on glowing knee drums with layered ceramic thigh
# plates, shin guards, exposed knee rams and rubber-soled feet (the body), a massive hunched graphite torso round its
# magenta core in a chrome-rimmed socket, with layered flank armour and louvres, a finned heat sink on its hump, a back
# louvre under a riveted plate, twin exhaust thrusters and clamped feed hoses to the neck (torso, core), under three
# armour plates the fight breaks off one by one (plates[0] the riveted chest frame, plates[1] and [2] the lipped
# shoulders with their glowing marks), a small low-set head with a visor slit, a jaw grille and an ear lens cluster
# (head), and long gunmetal arms on shoulder and elbow drums with elbow rams, clamped hoses and huge ceramic fists
# (armN, armF). Run: blender -b -P build_enemy_brute.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs (body: stumpy columns from the hips at 1.2 to the ground) ----
at('body')
for z in (0.36, -0.36):
    s = 1 if z > 0 else -1
    cyl('thigh', 'm', (0, 1.2, z), (0.05, 0.62, z), 0.2, 0.17, n=14)
    cyl('shin', 'm', (0.05, 0.62, z), (0, 0.14, z), 0.17, 0.19, n=14)
    joint('knee', (0.05, 0.62, z), 0.19, 0.4, bolts=6 if s > 0 else 0, glow=s > 0)
    armour('thighplate', (0.13, 0.92, z), (0.2, 0.42, 0.36), out=(1, 0, 0), bev=0.05, lip=0.02, taper=(0.8, 0.85), btaper=(0.9, 0.9))
    rivets('c', [(0.215, y, z + t) for y in (1.0, 0.82) for t in (0.11, -0.11)], (1, 0, 0), r=0.018, h=0.012)
    box('kneecap', 'p', (0.2, 0.62, z), (0.1, 0.22, 0.26), 0.035, taper=(0.6, 0.7), btaper=(0.6, 0.7))
    armour('shinguard', (0.205, 0.36, z), (0.08, 0.3, 0.26), out=(1, 0, 0), bev=0.03, lip=0.014, taper=(1.0, 0.9), btaper=(0.8, 0.8))
    box('shin_light', 'e', (0.248, 0.38, z), (0.01, 0.14, 0.05), 0.0)
    piston('knee_ram', (0.02, 1.02, z + s * 0.2), (0.06, 0.38, z + s * 0.2), 0.04, n=8, line=s > 0)
    box('ram_lug', 'm', (0.06, 0.36, z + s * 0.17), (0.1, 0.08, 0.08), 0.0)
    box('foot', 'p', (0.08, 0.09, z), (0.56, 0.18, 0.42), 0.05, taper=(0.7, 0.85), shift=(-0.05, 0))
    box('sole', 'r', (0.08, 0.02, z), (0.6, 0.045, 0.45), 0.012)
    for t in (-0.13, 0, 0.13): box('toe', 'm', (0.35, 0.06, z + t), (0.07, 0.1, 0.11), 0.008)
    box('heel', 'm', (-0.2, 0.1, z), (0.08, 0.12, 0.3), 0.02, taper=(0.8, 0.9))

# ---- Torso (1.4 up): the graphite bulk, belly, back hump and vents ----
node('torso', 'body', (0, 1.4, 0))
box('pelvis', 'm', (-0.02, -0.1, 0), (0.7, 0.24, 0.82), 0.05, taper=(1.1, 1.1))
box('bulk', 'j', (-0.04, 0.38, 0), (1.0, 0.82, 1.04), 0.12, taper=(1.0, 1.0), btaper=(0.78, 0.82))
box('hump', 'j', (-0.22, 0.78, 0), (0.6, 0.22, 0.78), 0.08, taper=(0.7, 0.8))
fins('sink', 'm', (-0.22, 0.925, 0), (0.3, 0.07, 0.48), 6)
box('sink_glow', 'e', (-0.22, 0.895, 0), (0.26, 0.02, 0.46), 0.0)
box('belly', 'j', (0.36, 0.08, 0), (0.18, 0.3, 0.6), 0.05, taper=(0.9, 0.9), btaper=(0.7, 0.8))
armour('backplate', (-0.5, 0.42, 0), (0.12, 0.5, 0.6), out=(-1, 0, 0), bev=0.04, lip=0.016, taper=(0.8, 0.8))
grille('louvre', (-0.565, 0.4, 0), (0.03, 0.3, 0.38), n=4, rot=(0, math.pi, 0))
rivets('c', [(-0.565, y, z) for y in (0.62, 0.2) for z in (0.24, -0.24)], (-1, 0, 0), r=0.016, h=0.01)
for z in (0.28, -0.28): thruster('exhaust', (-0.4, 0.84, z), 0.08, 0.22, rot=(0, 0, -2.553), n=10, rings=False)
for z in (0.5, -0.5):                                  # flank armour between the arms
    s = 1 if z > 0 else -1
    armour('flank', (-0.06, 0.5, z + s * 0.01), (0.5, 0.34, 0.04), out=(0, 0, s), bev=0.014, lip=0.012, btaper=(0.9, 1.0))
    grille('flank_vent', (-0.4, 0.42, z * 0.98), (0.02, 0.2, 0.12), n=3, rot=(0, -s * math.pi / 2, 0), frame=False)
    box('flank_light', 'e', (-0.06, 0.5, z + s * 0.032), (0.24, 0.025, 0.008), 0.0)
for z in (0.13, -0.13):                                # feed hoses from the hump to the neck
    cables('hose', (-0.3, 0.86, z), (-0.06, 1.08, z), (0.16, 0.92, z * 0.8), 0.034, k=1, clamps=(0.5,))
cyl('socket', 'm', (0.38, 0.4, 0), (0.56, 0.4, 0), 0.26, 0.24, n=18, bev=0.016)
torus('socket_rim', 'c', (0.56, 0.4, 0), 0.235, 0.02, nu=20, nv=4, rot=(0, 0, math.pi / 2))
cyl('neck', 'm', (0.12, 0.9, 0), (0.24, 1.0, 0), 0.13, 0.11, n=12)
for z in (0.62, -0.62): joint('shoulder', (0, 0.8, z), 0.17, 0.2, axis=(0, 0, 1 if z > 0 else -1), bolts=0)

node('core', 'torso', (0.5, 0.4, 0))
sphere('orb', 'e', (0.02, 0, 0), (0.17, 0.2, 0.2), nu=24, nv=12, q=False)

# ---- Armour plates (each centred on its own mesh) ----
node('plates[0]', 'torso', (0.52, 0.35, 0))          # the chest frame round the core
box('top', 'p', (0.0, 0.3, 0), (0.28, 0.22, 0.92), 0.05, taper=(0.75, 0.9))
box('top_ridge', 'p', (-0.02, 0.43, 0), (0.16, 0.05, 0.7), 0.015, taper=(0.7, 0.9))
box('bottom', 'p', (-0.02, -0.3, 0), (0.24, 0.2, 0.84), 0.05, btaper=(0.8, 0.9))
for z in (0.34, -0.34): box('side', 'p', (0.0, 0.0, z), (0.28, 0.46, 0.26), 0.05, taper=(1, 1))
rivets('c', [(0.14, y, z) for y in (0.17, -0.17) for z in (0.36, -0.36)], (1, 0, 0), r=0.02, h=0.012)
for y in (0.3, -0.3): box('frame_light', 'e', (0.13, y, 0), (0.012, 0.03, 0.3), 0.0, rot=(0, 0, 0.3 if y > 0 else 0))
for i, s in ((1, 1), (2, -1)):
    node(f'plates[{i}]', 'torso', (0, 0.98, s * 0.42))
    armour('pauldron', (0, 0.01, s * 0.04), (0.72, 0.24, 0.44), out=(0, 1, 0), bev=0.06, lip=0.02, taper=(0.8, 0.7))
    box('ridge', 'p', (-0.02, 0.13, s * 0.06), (0.5, 0.08, 0.14), 0.025, taper=(0.7, 0.6))
    box('lip', 'm', (0, -0.13, s * 0.06), (0.7, 0.05, 0.42), 0.012)
    box('mark', 'e', (0.326, 0.0, s * 0.04), (0.012, 0.05, 0.25), 0.0, rot=(0, 0, 0.29))
    rivets('c', [(x, 0.1, s * 0.2) for x in (-0.22, 0.0, 0.22)], (0, 0.6, s), r=0.016, h=0.012)

# ---- Head (a mesh of its own, low between the shoulders) ----
node('head', 'torso', (0.3, 1.02, 0))
box('skull', 'p', (0, 0.01, 0), (0.34, 0.26, 0.34), 0.06, taper=(0.8, 0.85))
box('brow', 'p', (0.12, 0.08, 0), (0.12, 0.06, 0.36), 0.02, rot=(0, 0, -0.3))
box('jaw', 'm', (0.1, -0.1, 0), (0.18, 0.08, 0.26), 0.02, btaper=(0.8, 0.8))
grille('jaw_grille', (0.19, -0.1, 0), (0.02, 0.06, 0.16), n=3, glow=None)
box('visor', 'e', (0.17, 0.0, 0), (0.04, 0.045, 0.26), 0.008)
lenses('ear', (-0.02, 0.0, 0.175), [(0.0, 0.0, 0.035)], rot=(0, -math.pi / 2, 0))
cyl('antenna', 'm', (-0.1, 0.12, -0.08), (-0.2, 0.36, -0.1), 0.012, 0.008, n=5)
box('antenna_tip', 'e', (-0.2, 0.36, -0.1), (0.03, 0.03, 0.03), 0.0)

# ---- Arms (pivot at the shoulder; fist 1.0 below) ----
for nm, s in (('armN', 1), ('armF', -1)):
    node(nm, 'torso', (0, 0.8, s * 0.72))
    cyl('upper', 'm', (0, 0.02, 0), (0.06, -0.48, 0), 0.15, 0.13, n=12)
    joint('elbow', (0.06, -0.48, 0), 0.15, 0.3, bolts=6 if s > 0 else 0)
    cyl('fore', 'm', (0.06, -0.48, 0), (0.05, -0.8, 0), 0.15, 0.17, n=12)
    armour('bicep', (0.1, -0.22, s * 0.03), (0.2, 0.34, 0.26), out=(1, 0, 0), bev=0.04, lip=0.016, taper=(0.85, 0.85))
    piston('elbow_ram', (0.03, -0.12, s * 0.16), (0.07, -0.6, s * 0.17), 0.032, n=8)
    tube('hose', 'r', bez((-0.1, -0.08, 0), (-0.26, -0.45, 0), (-0.12, -0.7, 0), 8), 0.03, n=8)
    for f in (0.3, 0.7): cyl('hose_clamp', 'm', *[Vector(p) for p in bez((-0.1, -0.08, 0), (-0.26, -0.45, 0), (-0.12, -0.7, 0), 20)[int(f * 20):int(f * 20) + 2]], 0.045, n=10)
    box('gauntlet', 'p', (0.08, -0.66, 0), (0.32, 0.3, 0.34), 0.05, btaper=(1.1, 1.1))
    box('gauntlet_light', 'e', (0.08, -0.64, s * 0.178), (0.2, 0.03, 0.01), 0.0)
    box('fist', 'p', (0.05, -1.0, 0), (0.5, 0.42, 0.46), 0.08, taper=(0.9, 0.92))
    for t in (-0.08, 0.08): box('finger_seam', 'm', (0.06, -1.0 + t, s * 0.226), (0.36, 0.02, 0.02), 0.0)
    box('knuckles', 'm', (0.28, -1.0, 0), (0.08, 0.32, 0.4), 0.03)
    for k in (-0.12, 0.0, 0.12): box('knuckle_light', 'e', (0.325, -1.0 + k, 0), (0.012, 0.05, 0.3), 0.004)

save(out_dir(), 'brute')
