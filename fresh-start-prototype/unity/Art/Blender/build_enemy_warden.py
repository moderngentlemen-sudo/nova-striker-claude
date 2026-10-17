# The Lockwarden: the heavy gatekeeper boss. A towering ceramic-and-graphite golem with a lock-and-key motif:
# gunmetal legs on hinge drums (the near hip glowing), layered cuisses, poleyns and greaves standing off gunmetal
# undercuts, side plates, knee actuator discs, hydraulic rams down the back and layered sabatons on lugged rubber soles
# (legsW[i].hip and .knee), a gunmetal pelvis with a riveted belt, two-lame tassets and a glowing keyhole on the fauld
# (torso), a massive chest round its magenta lock-core in a chrome-rimmed socket, with a layered breastplate, angled
# collar plates, layered flank armour and vents, lift rams up its flanks, a sensor cluster, a back louvre and clamped
# cable bundles to the shoulder drums (chest, core) behind armour the fight breaks away (plates[0] the core frame with
# its key-bit teeth and glow channels, [1] and [2] the two-lame pauldrons, [3] the lit helmet crest), a low helmed head
# with a T-visor, a brow, ear lens clusters, a chin grille and twin antennae (head, eye), a missile pod on its back
# with hazard chevrons, rails, targeting sensors, a louvre, a heat sink and two lift thrusters (pod; tubes[0..4] the
# glowing tube caps the rig extends when it fires), a great lock-hammer with keyhole emblems, glowing bands and a
# studded face on its near arm (armN, hammer) and a key-blade with a crossguard and a ringed shield emitter on its far
# arm (armF, blade), each arm on a gunmetal elbow drum with layered armour and rubber hoses.
# Run: blender -b -P build_enemy_warden.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs ----
for i, z in enumerate((0.5, -0.5)):
    s = 1 if z > 0 else -1
    node(f'legsW[{i}].hip', 'body', (0, 1.55, z))
    joint('hip', (0, 0, 0), 0.25, 0.44, bolts=8 if s > 0 else 0, glow=s > 0)
    cyl('thigh', 'm', (0, -0.05, 0), (0.04, -0.72, 0), 0.22, 0.18, n=16)
    armour('cuisse', (0.11, -0.3, 0), (0.42, 0.56, 0.46), out=(1, 0, 0), bev=0.06, lip=0.025, taper=(1.05, 1.0), btaper=(0.7, 0.8))
    box('cuisse_rim', 'm', (0.12, -0.04, 0), (0.42, 0.06, 0.48), 0.02)
    box('thigh_light', 'e', (0.33, -0.34, 0), (0.02, 0.24, 0.05), 0.006, rot=(0, 0, 0.12))
    rivets('j', [(0.304, -0.15, t) for t in (0.15, -0.15)] + [(0.265, -0.45, t) for t in (0.12, -0.12)], (1, -0.13, 0), r=0.02, h=0.014)
    box('sideplate', 'p', (0.04, -0.32, s * 0.245), (0.34, 0.4, 0.04), 0.015, btaper=(0.8, 1.0))
    rivets('j', [(x, -0.2, s * 0.265) for x in (-0.06, 0.14)], (0, 0, s), r=0.018, h=0.012)
    piston('ram', (-0.17, -0.06, s * 0.18), (-0.13, -0.68, s * 0.16), 0.045, n=10)

    node(f'legsW[{i}].knee', f'legsW[{i}].hip', (0.05, -0.75, 0))
    joint('knee', (0, 0, 0), 0.21, 0.36, bolts=0)
    cyl('kneedisc', 'p', (0, 0, s * 0.18), (0, 0, s * 0.245), 0.13, n=12, bev=0.012)
    cyl('kneedisc_core', 'e', (0, 0, s * 0.245), (0, 0, s * 0.255), 0.05, n=8)
    if s > 0: bolt_ring((0, 0, 0.245), 0.095, 6, (0, 0, 1), r=0.012, h=0.008, m='c')
    cyl('shin', 'm', (0, -0.05, 0), (0.02, -0.62, 0), 0.18, 0.16, n=16)
    armour('poleyn', (0.21, 0.0, 0), (0.18, 0.3, 0.34), out=(1, 0, 0), bev=0.05, lip=0.02, taper=(0.6, 0.75), btaper=(0.6, 0.75))
    armour('greave', (0.13, -0.36, 0), (0.38, 0.48, 0.4), out=(1, 0, 0), bev=0.06, lip=0.025, taper=(1.05, 1.0), btaper=(0.8, 0.85))
    box('greave_seam', 'j', (0.3, -0.22, 0), (0.035, 0.03, 0.37), 0.0)
    rivets('j', [(0.302, -0.16, t) for t in (0.13, -0.13)] + [(0.275, -0.54, t) for t in (0.12, -0.12)], (1, -0.1, 0), r=0.018, h=0.012)
    box('greave_slit', 'e', (0.315, -0.38, 0), (0.02, 0.2, 0.05), 0.006)
    piston('ram', (-0.2, -0.08, 0), (-0.13, -0.56, 0), 0.04, n=10)
    box('ankle', 'm', (0.06, -0.62, 0), (0.3, 0.12, 0.34), 0.03)
    armour('sabaton', (0.14, -0.7, 0), (0.78, 0.2, 0.5), out=(0, 1, 0), bev=0.06, lip=0.025, taper=(0.75, 0.85), shift=(0.02, 0))
    for x in (0.28, 0.38): box('lame', 'p', (x, -0.605, 0), (0.1, 0.05, 0.4), 0.015, rot=(0, 0, -0.3), btaper=(1.0, 1.05))
    box('sabaton_light', 'e', (0.12, -0.69, s * 0.229), (0.3, 0.025, 0.01), 0.0)
    box('toecap', 'p', (0.5, -0.74, 0), (0.18, 0.14, 0.46), 0.04, taper=(0.5, 0.8))
    box('heel', 'm', (-0.22, -0.73, 0), (0.12, 0.16, 0.36), 0.03)
    box('sole', 'r', (0.14, -0.815, 0), (0.82, 0.04, 0.53), 0.012)
    for x in (-0.12, 0.08, 0.28): box('sole_lug', 'r', (x, -0.835, 0), (0.1, 0.02, 0.5), 0.006)

# ---- Pelvis ----
node('torso', 'body', (0, 1.65, 0))
box('pelvis', 'm', (0, 0, 0), (1.15, 0.38, 0.92), 0.08, btaper=(0.85, 0.9))
box('belt', 'p', (0, 0.12, 0), (1.22, 0.12, 1.0), 0.03)
for s in (1, -1): rivets('j', [(x, 0.12, s * 0.5) for x in (-0.4, -0.2, 0.0, 0.2, 0.4)], (0, 0, s), r=0.018, h=0.012)
box('buckle', 'e', (0.62, 0.12, 0), (0.03, 0.08, 0.2), 0.008)
box('fauld', 'p', (0.5, -0.14, 0), (0.16, 0.3, 0.5), 0.04, btaper=(0.7, 0.7), taper=(1.0, 1.0))
cyl('fauld_key', 'e', (0.565, -0.09, 0), (0.59, -0.09, 0), 0.035, n=8)
box('fauld_key_bit', 'e', (0.572, -0.15, 0), (0.03, 0.08, 0.028), 0.0)
for z in (0.42, -0.42):
    s = 1 if z > 0 else -1
    box('tasset', 'p', (0.12, -0.16, z), (0.62, 0.34, 0.1), 0.03, btaper=(0.8, 1.0))
    box('tasset_lame', 'p', (0.12, -0.38, z * 1.04), (0.52, 0.16, 0.08), 0.025, btaper=(0.85, 1.0))
    rivets('j', [(x, -0.06, z + s * 0.05) for x in (-0.06, 0.3)], (0, 0, s), r=0.018, h=0.012)
box('backguard', 'p', (-0.5, -0.1, 0), (0.12, 0.32, 0.6), 0.03, btaper=(0.8, 0.8))

# ---- Chest ----
node('chest', 'torso', (0, 0.3, 0))
box('waist', 'm', (0, 0.12, 0), (1.0, 0.28, 0.9), 0.06)
for y in (0.06, 0.18): box('waist_rib', 'j', (0.0, y, 0), (1.02, 0.03, 0.92), 0.008)
for s in (1, -1): box('waistplate', 'p', (0.1, 0.1, s * 0.46), (0.5, 0.16, 0.04), 0.012, btaper=(0.85, 1.0))
box('bulk', 'j', (-0.04, 0.6, 0), (1.36, 0.9, 1.22), 0.16, btaper=(0.8, 0.85))
armour('breast', (0.24, 0.62, 0), (0.86, 0.86, 1.1), out=(1, 0, 0), bev=0.12, lip=0.03, taper=(0.9, 0.95), btaper=(0.75, 0.85))
box('gorget', 'm', (0.24, 1.08, 0), (0.62, 0.12, 0.62), 0.04)
armour('backplate', (-0.57, 0.5, 0), (0.24, 0.72, 0.9), out=(-1, 0, 0), bev=0.06, lip=0.025, btaper=(0.8, 0.9))
grille('louvre', (-0.685, 0.44, 0), (0.03, 0.4, 0.56), n=5, rot=(0, math.pi, 0))
rivets('j', [(-0.69, y, z) for y in (0.75, 0.16) for z in (0.36, -0.36)], (-1, 0, 0), r=0.022, h=0.014)
cyl('core_socket', 'm', (0.56, 0.55, 0), (0.78, 0.55, 0), 0.3, 0.28, n=20, bev=0.016)
torus('core_rim', 'c', (0.78, 0.55, 0), 0.27, 0.022, nu=24, nv=4, rot=(0, 0, math.pi / 2))
for z in (0.36, -0.36):
    box('rib', 'p', (0.6, 0.12, z), (0.14, 0.18, 0.3), 0.03, taper=(0.8, 0.9))
for s in (1, -1):
    box('collar', 'p', (0.16, 1.07, s * 0.38), (0.5, 0.1, 0.3), 0.03, rot=(s * 0.3, 0, 0), taper=(0.85, 0.85))
    rivets('j', [(x, 1.13, s * 0.4) for x in (0.0, 0.3)], (0, 1, s * 0.3), r=0.018, h=0.012)
    # flank armour, leaning with the chest's sides, with a grille and a glowing mark
    armour('flank', (-0.12, 0.62, s * 0.6), (0.66, 0.5, 0.05), out=(0, 0, s), bev=0.018, lip=0.014, rot=(s * 0.1, 0, 0))
    rivets('j', [(x, y, s * 0.62) for x in (-0.4, 0.16) for y in (0.8, 0.44)], (0, 0, s), r=0.018, h=0.012)
    grille('flank_vent', (-0.12, 0.62, s * 0.62), (0.025, 0.24, 0.3), n=3, rot=(0, -s * math.pi / 2, 0))
    box('flank_light', 'e', (0.1, 0.62, s * 0.62), (0.018, 0.22, 0.012), 0.0)
    # armoured cables from under the pod round to the shoulders
    cables('cable', (-0.56, 0.8, s * 0.42), (-0.46, 1.0, s * 0.7), (-0.12, 0.9, s * 0.8), 0.03, k=3, clamps=(0.35, 0.65))
for k in range(3): rivets('j', [(0.24 + (k - 1) * 0.2, 1.14, t) for t in (0.2, -0.2)], (0, 1, 0), r=0.016, h=0.01)
for z in (0.78, -0.78): joint('shoulder', (0.05, 0.95, z), 0.25, 0.26, axis=(0, 0, 1 if z > 0 else -1), bolts=0)
for s in (1, -1):                                       # lift rams up the chest's flanks to the shoulders
    piston('lift_ram', (-0.34, 0.22, s * 0.66), (-0.16, 0.8, s * 0.72), 0.045, n=10)
lenses('chest_sensor', (0.45, 1.0, 0.3), [(0.0, 0.0, 0.035), (-0.05, 0.06, 0.02)], rot=(0, -0.5, 0.2), plate=(0.05, 0.12, 0.12))

node('core', 'chest', (0.72, 0.55, 0))
sphere('orb', 'e', (0.04, 0, 0), (0.2, 0.24, 0.24), nu=28, nv=14, q=False)
lathe('keyhole', 'e', [(0.0, 0.02), (0.06, 0.02), (0.06, -0.02), (0.0, -0.02)], n=12, c=(0.27, 0.05, 0), rot=(0, 0, math.pi / 2))

node('plates[0]', 'chest', (0.72, 0.45, 0))           # the frame round the lock-core
box('lintel', 'p', (0.0, 0.42, 0), (0.3, 0.2, 1.0), 0.06, taper=(0.75, 0.9))
box('lintel_ridge', 'p', (-0.02, 0.535, 0), (0.18, 0.04, 0.8), 0.015, taper=(0.7, 0.9))
for z in (-0.13, 0.0, 0.13): box('tooth', 'j', (0.1, 0.3, z), (0.08, 0.07, 0.06), 0.01, btaper=(0.7, 0.7))
box('sill', 'p', (-0.02, -0.25, 0), (0.28, 0.24, 0.9), 0.06, btaper=(0.75, 0.85))
rivets('j', [(0.12, -0.2, z) for z in (0.3, 0.12, -0.12, -0.3)], (1, 0, 0), r=0.02, h=0.012)
for z in (0.4, -0.4):
    box('jamb', 'p', (0.0, 0.1, z), (0.3, 0.56, 0.22), 0.05)
    box('channel', 'e', (0.152, 0.1, z), (0.012, 0.38, 0.03), 0.0)
for z in (0.25, -0.25): box('bolt', 'p', (0.16, 0.42, z), (0.06, 0.1, 0.1), 0.02, taper=(0.6, 0.6))
for i, z in ((1, 0.62), (2, -0.62)):
    node(f'plates[{i}]', 'chest', (0, 1.12, z))
    s = 1 if z > 0 else -1
    box('pauldron', 'p', (0, 0, s * 0.04), (0.8, 0.3, 0.54), 0.08, taper=(0.75, 0.65))
    box('rim', 'p', (0, -0.17, s * 0.1), (0.74, 0.1, 0.42), 0.03, btaper=(1.0, 1.0))
    box('lame', 'p', (0.02, -0.27, s * 0.16), (0.66, 0.12, 0.34), 0.03, btaper=(0.9, 0.9))
    box('lame_seam', 'j', (0.02, -0.215, s * 0.14), (0.68, 0.02, 0.38), 0.0)
    box('fin', 'p', (-0.08, 0.2, s * 0.06), (0.5, 0.12, 0.08), 0.025, taper=(0.5, 0.6))
    box('light', 'e', (0.38, 0.0, s * 0.04), (0.02, 0.07, 0.3), 0.006)
    box('stripe', 'e', (0.0, 0.0, s * 0.265), (0.4, 0.03, 0.01), 0.0, rot=(-s * 0.3, 0, 0))
    rivets('j', [(x, 0.15, s * 0.16) for x in (-0.2, 0.06, 0.24)], (0, 1, 0), r=0.018, h=0.012)

# ---- Head ----
node('head', 'chest', (0.42, 1.2, 0))
box('helm', 'p', (0, 0.01, 0), (0.54, 0.36, 0.48), 0.08, taper=(0.8, 0.85), btaper=(0.9, 0.9))
box('brow', 'p', (0.22, 0.15, 0), (0.14, 0.05, 0.44), 0.015, rot=(0, 0, -0.3))
box('faceplate', 'm', (0.24, -0.02, 0), (0.1, 0.3, 0.4), 0.03, btaper=(0.7, 0.75))
box('chin', 'p', (0.22, -0.16, 0), (0.16, 0.08, 0.3), 0.025, btaper=(0.8, 0.8))
grille('chin_grille', (0.3, -0.16, 0), (0.02, 0.06, 0.2), n=3)
box('neckguard', 'm', (-0.26, -0.12, 0), (0.12, 0.16, 0.4), 0.025, btaper=(0.8, 0.9))
for z in (0.25, -0.25):
    s = 1 if z > 0 else -1
    box('cheek', 'p', (0.08, -0.04, z), (0.34, 0.26, 0.06), 0.02, btaper=(0.7, 1.0))
    lenses('ear', (-0.14, 0.03, s * 0.245), [(0.0, 0.0, 0.05), (0.07, 0.06, 0.022)], rot=(0, -s * math.pi / 2, 0))
    cyl('antenna', 'm', (-0.2, 0.12, s * 0.12), (-0.48, 0.42, s * 0.2), 0.016, 0.01, n=6)
    box('antenna_tip', 'e', (-0.48, 0.42, s * 0.2), (0.04, 0.04, 0.04), 0.0)
node('eye', 'head', (0.28, 0.02, 0))
box('visor', 'e', (0.0, 0, 0), (0.05, 0.07, 0.42), 0.01, taper=(1, 0.9))
box('visor_stem', 'e', (0.0, -0.08, 0), (0.05, 0.12, 0.06), 0.008)
node('plates[3]', 'head', (-0.05, 0.25, 0))
box('crest', 'p', (0, 0, 0), (0.42, 0.12, 0.3), 0.04, taper=(0.7, 0.6))
box('ridge', 'p', (0.04, 0.08, 0), (0.36, 0.06, 0.08), 0.02, taper=(0.6, 0.6))
box('ridge_light', 'e', (0.04, 0.112, 0), (0.18, 0.01, 0.02), 0.0)
for z in (0.12, -0.12): rivets('j', [(x, 0.06, z) for x in (-0.1, 0.1)], (0, 1, 0), r=0.016, h=0.01)

# ---- Missile pod (on its back) ----
node('pod', 'chest', (-0.7, 1.05, 0))
box('housing', 'p', (0, -0.02, 0), (0.58, 0.48, 0.88), 0.07, taper=(0.9, 0.95))
box('deck', 'm', (0.02, 0.24, 0), (0.42, 0.06, 0.82), 0.02)
for z in (0.43, -0.43): box('rail', 'j', (0.02, 0.29, z), (0.46, 0.05, 0.04), 0.0)
box('mount', 'm', (0.2, -0.2, 0), (0.3, 0.16, 0.5), 0.03)
for z in (0.36, -0.36): lenses('targeting', (0.29, 0.2, z), [(0.0, 0.0, 0.03)], plate=(0.05, 0.08, 0.08))
grille('pod_louvre', (-0.29, -0.16, 0), (0.03, 0.12, 0.6), n=3, rot=(0, math.pi, 0))
for z in (0.46, -0.46):
    s = 1 if z > 0 else -1
    box('cheekplate', 'm', (0, 0, z), (0.5, 0.36, 0.04), 0.012)
    for k in range(3):                                 # hazard chevrons, pointing up
        x = -0.15 + k * 0.15
        prism('chevron', 'e', [(x - 0.05, -0.08), (x, -0.03), (x + 0.05, -0.08), (x + 0.05, -0.03), (x, 0.02), (x - 0.05, -0.03)],
              0, 0.012, 0.0, c=(0, 0, z + s * 0.02), rot=(0, 0 if s > 0 else math.pi, 0))
    rivets('j', [(x, y, z + s * 0.02) for x in (-0.2, 0.2) for y in (0.13, -0.13)], (0, 0, s), r=0.016, h=0.01)
fins('sink', 'm', (-0.285, 0.0, 0), (0.28, 0.06, 0.6), 6, rot=(0, 0, math.pi / 2))
for z in (0.24, -0.24): thruster('lift', (-0.1, -0.25, z), 0.085, 0.18, n=12)
for i in range(5):
    cyl('bore', 'm', (-0.05 + (i % 2) * 0.12, 0.2, -0.3 + i * 0.15), (-0.05 + (i % 2) * 0.12, 0.27, -0.3 + i * 0.15), 0.085, n=12)
for i in range(5):
    node(f'tubes[{i}]', 'pod', (-0.05 + (i % 2) * 0.12, 0.29, -0.3 + i * 0.15))
    lathe('cap', 'e', [(0.07, -0.03), (0.07, 0.015), (0.045, 0.03), (0.0, 0.03)], n=12, sharp=40)

# ---- Near arm: the lock-hammer ----
node('armN', 'chest', (0.05, 0.95, 0.95))
cyl('upper', 'm', (0, 0, 0), (0.02, -0.5, 0), 0.19, 0.16, n=16)
joint('elbow', (0.02, -0.52, 0), 0.18, 0.36, bolts=6, glow=True)
cyl('fore', 'm', (0.02, -0.52, 0), (0.05, -0.85, 0), 0.17, 0.19, n=16)
armour('rerebrace', (0.08, -0.25, 0.03), (0.34, 0.36, 0.36), out=(1, 0, 0), bev=0.05, lip=0.02, taper=(0.9, 0.9))
rivets('j', [(x, -0.3, 0.21) for x in (-0.04, 0.16)], (0, 0, 1), r=0.018, h=0.012)
piston('ram', (-0.02, -0.08, 0.25), (0.04, -0.62, 0.24), 0.04, n=10)
cables('hose', (-0.15, -0.08, 0), (-0.3, -0.5, 0), (-0.14, -0.82, 0), 0.026, k=2, clamps=(0.5,))
armour('vambrace', (0.07, -0.72, 0), (0.4, 0.34, 0.42), out=(1, 0, 0), bev=0.05, lip=0.02, btaper=(1.08, 1.08))
box('vambrace_light', 'e', (0.06, -0.7, 0.222), (0.24, 0.03, 0.012), 0.0)
rivets('j', [(x, -0.8, 0.222) for x in (-0.06, 0.18)], (0, 0, 1), r=0.018, h=0.012)
node('hammer', 'armN', (0.05, -1.05, 0))
prism('head', 'p', [(-0.42, -0.22), (-0.3, -0.36), (0.3, -0.36), (0.42, -0.22), (0.42, 0.22), (0.3, 0.36), (-0.3, 0.36), (-0.42, 0.22)],
      -0.34, 0.34, 0.05)
for x in (-0.24, 0.24):
    box('band', 'm', (x, 0, 0), (0.1, 0.76, 0.72), 0.025)
    for z in (0.36, -0.36): rivets('p', [(x, y, z) for y in (0.24, -0.24)], (0, 0, 1 if z > 0 else -1), r=0.018, h=0.012)
for z in (0.34, -0.34):                                # the keyhole emblem on each flat
    s = 1 if z > 0 else -1
    torus('emblem', 'p', (0, 0.0, z + s * 0.012), 0.15, 0.028, nu=16, nv=4, rot=(math.pi / 2, 0, 0))
    cyl('emblem_key', 'e', (0, 0.04, z), (0, 0.04, z + s * 0.022), 0.045, n=10)
    box('emblem_bit', 'e', (0, -0.04, z + s * 0.01), (0.045, 0.1, 0.022), 0.0)
box('face', 'm', (0.44, 0, 0), (0.06, 0.6, 0.62), 0.02)
box('strike', 'e', (0.475, 0, 0), (0.03, 0.48, 0.56), 0.01)
for x in (-0.24, 0.24): box('band_glow', 'e', (x, 0.0, 0.365), (0.03, 0.5, 0.008), 0.0)
rivets('p', [(0.47, y, z) for y in (0.27, -0.27) for z in (0.29, -0.29)], (1, 0, 0), r=0.022, h=0.016)
box('keyhole', 'j', (0.49, 0.04, 0), (0.02, 0.14, 0.08), 0.006)
box('back_spike', 'm', (-0.5, 0, 0), (0.14, 0.3, 0.3), 0.03, rot=(0, 0, math.pi / 2), taper=(0.3, 0.3))
box('grip', 'r', (0, 0.36, 0), (0.24, 0.12, 0.3), 0.03)
cyl('haft_collar', 'p', (0, 0.4, 0), (0, 0.5, 0), 0.11, n=12)

# ---- Far arm: the key-blade and its shield emitter ----
node('armF', 'chest', (0.05, 0.95, -0.95))
cyl('upper', 'm', (0, 0, 0), (0.02, -0.48, 0), 0.17, 0.15, n=16)
joint('elbow', (0.02, -0.5, 0), 0.16, 0.32, bolts=0)
cyl('fore', 'm', (0.02, -0.5, 0), (0.06, -0.8, 0), 0.16, 0.15, n=16)
armour('rerebrace', (0.08, -0.24, -0.03), (0.3, 0.34, 0.32), out=(1, 0, 0), bev=0.05, lip=0.02, taper=(0.9, 0.9))
rivets('j', [(x, -0.28, -0.19) for x in (-0.03, 0.15)], (0, 0, -1), r=0.018, h=0.012)
piston('ram', (-0.02, -0.08, -0.23), (0.05, -0.58, -0.22), 0.038, n=10)
tube('hose', 'r', bez((-0.14, -0.08, 0), (-0.28, -0.46, 0), (-0.12, -0.78, 0), 8), 0.034, n=8)
box('vambrace', 'p', (0.06, -0.68, 0), (0.36, 0.3, 0.36), 0.05)
box('vambrace_light', 'e', (0.06, -0.62, 0.182), (0.22, 0.03, 0.012), 0.0)
cyl('emitter', 'm', (0.06, -0.68, -0.18), (0.06, -0.68, -0.24), 0.16, n=18)
torus('emitter_ring', 'p', (0.06, -0.68, -0.245), 0.135, 0.025, nu=18, nv=4, rot=(math.pi / 2, 0, 0))
cyl('emitter_lens', 'e', (0.06, -0.68, -0.24), (0.06, -0.68, -0.26), 0.11, n=18)
box('guard', 'm', (0.1, -0.86, 0), (0.36, 0.08, 0.18), 0.02)
node('blade', 'armF', (0.1, -1.4, 0))
prism('blade', 'p', [(-0.1, 0.58), (0.1, 0.58), (0.12, -0.6), (0.0, -0.86), (-0.1, -0.72), (-0.1, -0.42), (-0.16, -0.36), (-0.16, -0.22), (-0.1, -0.16)],
      -0.05, 0.05, 0.015)
box('fuller', 'm', (-0.01, 0.02, 0), (0.06, 1.0, 0.105), 0.01)
box('fuller_light', 'e', (-0.01, 0.06, 0), (0.016, 0.72, 0.11), 0.0)
prism('edge', 'e', [(0.1, 0.56), (0.13, 0.56), (0.15, -0.6), (0.03, -0.84), (0.0, -0.84), (0.11, -0.6)], -0.03, 0.03, 0.0)
box('crossguard', 'p', (0.0, 0.57, 0), (0.36, 0.07, 0.16), 0.02, taper=(0.9, 0.8))
for x in (-0.19, 0.19): box('quillon', 'm', (x, 0.55, 0), (0.05, 0.1, 0.1), 0.012, btaper=(0.5, 0.5))
box('ricasso', 'r', (0, 0.66, 0), (0.24, 0.16, 0.14), 0.03)
rivets('p', [(x, 0.66, 0.07) for x in (-0.06, 0.06)], (0, 0, 1), r=0.016, h=0.01)

save(out_dir(), 'warden')
