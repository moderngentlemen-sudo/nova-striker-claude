# The Lockwarden: the heavy gatekeeper boss. A towering ceramic-and-graphite golem: armoured legs with greaves and
# broad sabatons (legsW[i].hip and .knee), a pelvis with tassets (torso), a massive chest round its magenta lock-core
# (chest, core) behind armour the fight breaks away (plates[0] the core frame, [1] and [2] the pauldrons, [3] the
# helmet crest), a low helmed head with a T-visor (head, eye), a missile pod on its back (pod; tubes[0..4] the
# glowing tube caps the rig extends when it fires), a great lock-hammer on its near arm (armN, hammer) and a
# key-blade with a shield emitter on its far arm (armF, blade). Run: blender -b -P build_enemy_warden.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs ----
for i, z in enumerate((0.5, -0.5)):
    node(f'legsW[{i}].hip', 'body', (0, 1.55, z))
    sphere('hipball', 'j', (0, 0, 0), 0.25, nu=16, nv=8)
    cyl('thigh', 'j', (0, -0.05, 0), (0.04, -0.72, 0), 0.22, 0.18, n=16)
    box('cuisse', 'p', (0.1, -0.3, 0), (0.42, 0.56, 0.46), 0.06, taper=(1.05, 1.0), btaper=(0.7, 0.8))
    box('cuisse_rim', 'j', (0.12, -0.04, 0), (0.42, 0.06, 0.48), 0.02)
    box('thigh_light', 'e', (0.33, -0.34, 0), (0.02, 0.24, 0.05), 0.006, rot=(0, 0, 0.12))
    node(f'legsW[{i}].knee', f'legsW[{i}].hip', (0.05, -0.75, 0))
    sphere('kneeball', 'j', (0, 0, 0), 0.21, nu=16, nv=8)
    cyl('shin', 'j', (0, -0.05, 0), (0.02, -0.62, 0), 0.18, 0.16, n=16)
    box('poleyn', 'p', (0.2, 0.0, 0), (0.18, 0.3, 0.34), 0.05, taper=(0.6, 0.75), btaper=(0.6, 0.75))
    box('greave', 'p', (0.12, -0.36, 0), (0.38, 0.48, 0.4), 0.06, taper=(1.05, 1.0), btaper=(0.8, 0.85))
    box('greave_slit', 'e', (0.315, -0.38, 0), (0.02, 0.2, 0.05), 0.006)
    box('ankle', 'j', (0.06, -0.62, 0), (0.3, 0.12, 0.34), 0.03)
    box('sabaton', 'p', (0.14, -0.71, 0), (0.78, 0.2, 0.5), 0.06, taper=(0.75, 0.85), shift=(0.02, 0))
    box('toecap', 'p', (0.5, -0.74, 0), (0.18, 0.14, 0.46), 0.04, taper=(0.5, 0.8))
    box('heel', 'j', (-0.22, -0.73, 0), (0.12, 0.16, 0.36), 0.03)
    box('sole', 'j', (0.14, -0.815, 0), (0.8, 0.03, 0.52), 0.01)

# ---- Pelvis ----
node('torso', 'body', (0, 1.65, 0))
box('pelvis', 'j', (0, 0, 0), (1.15, 0.38, 0.92), 0.08, btaper=(0.85, 0.9))
box('belt', 'p', (0, 0.12, 0), (1.22, 0.12, 1.0), 0.03)
box('buckle', 'e', (0.62, 0.12, 0), (0.03, 0.08, 0.2), 0.008)
box('fauld', 'p', (0.5, -0.14, 0), (0.16, 0.3, 0.5), 0.04, btaper=(0.7, 0.7), taper=(1.0, 1.0))
for z in (0.42, -0.42):
    box('tasset', 'p', (0.12, -0.16, z), (0.62, 0.34, 0.1), 0.03, btaper=(0.8, 1.0))
box('backguard', 'p', (-0.5, -0.1, 0), (0.12, 0.32, 0.6), 0.03, btaper=(0.8, 0.8))

# ---- Chest ----
node('chest', 'torso', (0, 0.3, 0))
box('waist', 'j', (0, 0.12, 0), (1.0, 0.28, 0.9), 0.06)
box('bulk', 'j', (-0.04, 0.6, 0), (1.36, 0.9, 1.22), 0.16, btaper=(0.8, 0.85))
box('breast', 'p', (0.22, 0.62, 0), (0.86, 0.86, 1.1), 0.12, taper=(0.9, 0.95), btaper=(0.75, 0.85))
box('gorget', 'j', (0.24, 1.08, 0), (0.62, 0.12, 0.62), 0.04)
box('backplate', 'p', (-0.56, 0.5, 0), (0.24, 0.72, 0.9), 0.06, btaper=(0.8, 0.9))
cyl('core_socket', 'j', (0.56, 0.55, 0), (0.78, 0.55, 0), 0.3, 0.28, n=20)
for z in (0.36, -0.36):
    box('rib', 'p', (0.6, 0.12, z), (0.14, 0.18, 0.3), 0.03, taper=(0.8, 0.9))
for k in range(4): box('vent', 'e', (-0.685, 0.28 + k * 0.1, 0), (0.012, 0.035, 0.5), 0.004)
for z in (0.78, -0.78): sphere('shoulder', 'j', (0.05, 0.95, z), 0.24, nu=16, nv=8)

node('core', 'chest', (0.72, 0.55, 0))
sphere('orb', 'e', (0.04, 0, 0), (0.2, 0.24, 0.24), nu=20, nv=12)
lathe('keyhole', 'e', [(0.0, 0.02), (0.06, 0.02), (0.06, -0.02), (0.0, -0.02)], n=12, c=(0.27, 0.05, 0), rot=(0, 0, math.pi / 2))

node('plates[0]', 'chest', (0.72, 0.45, 0))           # the frame round the lock-core
box('lintel', 'p', (0.0, 0.42, 0), (0.3, 0.2, 1.0), 0.06, taper=(0.75, 0.9))
box('sill', 'p', (-0.02, -0.25, 0), (0.28, 0.24, 0.9), 0.06, btaper=(0.75, 0.85))
for z in (0.4, -0.4): box('jamb', 'p', (0.0, 0.1, z), (0.3, 0.56, 0.22), 0.05)
for z in (0.25, -0.25): box('bolt', 'p', (0.16, 0.42, z), (0.06, 0.1, 0.1), 0.02, taper=(0.6, 0.6))
for i, z in ((1, 0.62), (2, -0.62)):
    node(f'plates[{i}]', 'chest', (0, 1.12, z))
    s = 1 if z > 0 else -1
    box('pauldron', 'p', (0, 0, s * 0.04), (0.8, 0.3, 0.54), 0.08, taper=(0.75, 0.65))
    box('rim', 'p', (0, -0.17, s * 0.1), (0.74, 0.1, 0.42), 0.03, btaper=(1.0, 1.0))
    box('fin', 'p', (-0.08, 0.2, s * 0.06), (0.5, 0.12, 0.08), 0.025, taper=(0.5, 0.6))
    box('light', 'e', (0.38, 0.0, s * 0.04), (0.02, 0.07, 0.3), 0.006)

# ---- Head ----
node('head', 'chest', (0.42, 1.2, 0))
box('helm', 'p', (0, 0.01, 0), (0.54, 0.36, 0.48), 0.08, taper=(0.8, 0.85), btaper=(0.9, 0.9))
box('faceplate', 'j', (0.24, -0.02, 0), (0.1, 0.3, 0.4), 0.03, btaper=(0.7, 0.75))
box('chin', 'p', (0.22, -0.16, 0), (0.16, 0.08, 0.3), 0.025, btaper=(0.8, 0.8))
for z in (0.25, -0.25): box('cheek', 'p', (0.08, -0.04, z), (0.34, 0.26, 0.06), 0.02, btaper=(0.7, 1.0))
node('eye', 'head', (0.28, 0.02, 0))
box('visor', 'e', (0.0, 0, 0), (0.05, 0.07, 0.42), 0.01, taper=(1, 0.9))
box('visor_stem', 'e', (0.0, -0.08, 0), (0.05, 0.12, 0.06), 0.008)
node('plates[3]', 'head', (-0.05, 0.25, 0))
box('crest', 'p', (0, 0, 0), (0.42, 0.12, 0.3), 0.04, taper=(0.7, 0.6))
box('ridge', 'p', (0.04, 0.08, 0), (0.36, 0.06, 0.08), 0.02, taper=(0.6, 0.6))

# ---- Missile pod (on its back) ----
node('pod', 'chest', (-0.7, 1.05, 0))
box('housing', 'p', (0, -0.02, 0), (0.58, 0.48, 0.88), 0.07, taper=(0.9, 0.95))
box('deck', 'j', (0.02, 0.24, 0), (0.42, 0.06, 0.82), 0.02)
box('mount', 'j', (0.2, -0.2, 0), (0.3, 0.16, 0.5), 0.03)
for z in (0.46, -0.46): box('cheekplate', 'j', (0, 0, z), (0.5, 0.36, 0.04), 0.012)
for i in range(5):
    cyl('bore', 'j', (-0.05 + (i % 2) * 0.12, 0.2, -0.3 + i * 0.15), (-0.05 + (i % 2) * 0.12, 0.27, -0.3 + i * 0.15), 0.085, n=12)
for i in range(5):
    node(f'tubes[{i}]', 'pod', (-0.05 + (i % 2) * 0.12, 0.29, -0.3 + i * 0.15))
    lathe('cap', 'e', [(0.07, -0.03), (0.07, 0.015), (0.045, 0.03), (0.0, 0.03)], n=12, sharp=40)

# ---- Near arm: the lock-hammer ----
node('armN', 'chest', (0.05, 0.95, 0.95))
cyl('upper', 'j', (0, 0, 0), (0.02, -0.5, 0), 0.2, 0.17, n=16)
sphere('elbow', 'j', (0.02, -0.52, 0), 0.18, nu=16, nv=8)
cyl('fore', 'j', (0.02, -0.52, 0), (0.05, -0.85, 0), 0.17, 0.19, n=16)
box('rerebrace', 'p', (0.06, -0.25, 0.03), (0.34, 0.36, 0.36), 0.05, taper=(0.9, 0.9))
box('vambrace', 'p', (0.06, -0.72, 0), (0.4, 0.34, 0.42), 0.05, btaper=(1.08, 1.08))
node('hammer', 'armN', (0.05, -1.05, 0))
prism('head', 'p', [(-0.42, -0.22), (-0.3, -0.36), (0.3, -0.36), (0.42, -0.22), (0.42, 0.22), (0.3, 0.36), (-0.3, 0.36), (-0.42, 0.22)],
      -0.34, 0.34, 0.05)
for x in (-0.24, 0.24): box('band', 'j', (x, 0, 0), (0.1, 0.76, 0.72), 0.025)
box('face', 'j', (0.44, 0, 0), (0.06, 0.6, 0.62), 0.02)
box('strike', 'e', (0.475, 0, 0), (0.03, 0.48, 0.56), 0.01)
box('keyhole', 'j', (0.49, 0.04, 0), (0.02, 0.14, 0.08), 0.006)
box('back_spike', 'j', (-0.5, 0, 0), (0.14, 0.3, 0.3), 0.03, rot=(0, 0, math.pi / 2), taper=(0.3, 0.3))
box('grip', 'j', (0, 0.36, 0), (0.24, 0.12, 0.3), 0.03)

# ---- Far arm: the key-blade and its shield emitter ----
node('armF', 'chest', (0.05, 0.95, -0.95))
cyl('upper', 'j', (0, 0, 0), (0.02, -0.48, 0), 0.18, 0.16, n=16)
sphere('elbow', 'j', (0.02, -0.5, 0), 0.16, nu=16, nv=8)
cyl('fore', 'j', (0.02, -0.5, 0), (0.06, -0.8, 0), 0.16, 0.15, n=16)
box('rerebrace', 'p', (0.06, -0.24, -0.03), (0.3, 0.34, 0.32), 0.05, taper=(0.9, 0.9))
box('vambrace', 'p', (0.06, -0.68, 0), (0.36, 0.3, 0.36), 0.05)
cyl('emitter', 'j', (0.06, -0.68, -0.18), (0.06, -0.68, -0.24), 0.16, n=18)
cyl('emitter_lens', 'e', (0.06, -0.68, -0.24), (0.06, -0.68, -0.26), 0.11, n=18)
box('guard', 'j', (0.1, -0.86, 0), (0.36, 0.08, 0.18), 0.02)
node('blade', 'armF', (0.1, -1.4, 0))
prism('blade', 'p', [(-0.1, 0.58), (0.1, 0.58), (0.12, -0.6), (0.0, -0.86), (-0.1, -0.72), (-0.1, -0.42), (-0.16, -0.36), (-0.16, -0.22), (-0.1, -0.16)],
      -0.05, 0.05, 0.015)
box('fuller', 'j', (-0.01, 0.02, 0), (0.06, 1.0, 0.105), 0.01)
prism('edge', 'e', [(0.1, 0.56), (0.13, 0.56), (0.15, -0.6), (0.03, -0.84), (0.0, -0.84), (0.11, -0.6)], -0.03, 0.03, 0.0)
box('ricasso', 'j', (0, 0.66, 0), (0.24, 0.16, 0.14), 0.03)

save(out_dir(), 'warden')
