# The shield guard: a squat, broad-shouldered sentinel behind a tall tower shield. Thick graphite legs with ceramic
# knee guards and boots (the body), a hunched armoured torso with a helmet brow over its visor (torso, eye) and a
# near arm braced against the shield (shield: a faceted ceramic slab with glowing edge rails and a crest).
# Run: blender -b -P build_enemy_shield.py -- <out-dir>
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_enemy_lib import *

setup()
# ---- Legs (on the body: from the hips at 0.82 to the ground) ----
at('body')
for z in (0.16, -0.16):
    cyl('thigh', 'j', (0, 0.84, z), (0.02, 0.46, z), 0.1, 0.085, n=12)
    cyl('shin', 'j', (0.02, 0.46, z), (0, 0.1, z), 0.085, 0.075, n=12)
    sphere('knee', 'j', (0.02, 0.46, z), 0.095, nu=12, nv=6)
    box('kneeguard', 'p', (0.09, 0.47, z), (0.1, 0.18, 0.15), 0.025, taper=(0.7, 0.8))
    box('greave', 'p', (0.07, 0.27, z), (0.08, 0.24, 0.14), 0.02, taper=(1.2, 1.0))
    box('boot', 'p', (0.05, 0.06, z), (0.32, 0.12, 0.17), 0.03, taper=(0.75, 0.9), shift=(-0.03, 0))
    box('sole', 'j', (0.05, 0.01, z), (0.33, 0.03, 0.18), 0.008)
box('hips', 'j', (0, 0.84, 0), (0.3, 0.14, 0.44), 0.03)

# ---- Torso (1.0 up): hunched block, shoulders, brow, the near arm bracing the shield ----
node('torso', 'body', (0, 1.0, 0))
box('waist', 'j', (-0.02, -0.08, 0), (0.4, 0.16, 0.46), 0.03, taper=(1.1, 1.1))
box('chest', 'p', (0.0, 0.22, 0), (0.52, 0.44, 0.58), 0.06, taper=(1.04, 1.0), btaper=(0.85, 0.85))
box('chestplate', 'p', (0.22, 0.2, 0), (0.12, 0.34, 0.42), 0.035, taper=(1.2, 1.0), btaper=(0.7, 0.8))
box('collar', 'j', (-0.02, 0.46, 0), (0.44, 0.08, 0.46), 0.02)
box('helm', 'p', (0.06, 0.57, 0), (0.36, 0.16, 0.32), 0.04, taper=(0.75, 0.8))
box('brow', 'p', (0.22, 0.63, 0), (0.1, 0.05, 0.34), 0.015, rot=(0, 0, -0.35))
box('jaw', 'j', (0.2, 0.52, 0), (0.08, 0.06, 0.26), 0.015)
box('backpack', 'j', (-0.28, 0.3, 0), (0.1, 0.36, 0.38), 0.025)
for k in range(3): box('backlight', 'e', (-0.335, 0.2 + k * 0.1, 0), (0.012, 0.03, 0.22), 0.004)
for z in (0.33, -0.33):
    box('pauldron', 'p', (0.0, 0.42, z), (0.4, 0.14, 0.18), 0.04, taper=(0.8, 0.7))
    box('pauldron_rim', 'j', (0.0, 0.34, z * 1.04), (0.38, 0.04, 0.17), 0.012)
for z in (0.26,):
    cyl('upperarm', 'j', (0.04, 0.32, z + 0.06), (0.28, 0.1, z + 0.08), 0.07, n=10)
    sphere('elbow', 'j', (0.28, 0.1, z + 0.08), 0.075, nu=10, nv=6)
    cyl('forearm', 'j', (0.28, 0.1, z + 0.08), (0.48, 0.12, 0.14), 0.065, n=10)
    box('bracer', 'p', (0.38, 0.11, z), (0.2, 0.11, 0.1), 0.02, rot=(0, 0.4, 0))

node('eye', 'torso', (0.28, 0.58, 0))
box('visor', 'e', (-0.005, 0, 0), (0.05, 0.05, 0.28), 0.01, taper=(1, 0.8))

# ---- The tower shield (0.55 ahead of the torso centre) ----
node('shield', 'torso', (0.55, 0.1, 0))
prism('slab', 'p', [(-0.4, -0.78), (0.4, -0.78), (0.52, -0.6), (0.52, 0.64), (0.42, 0.78), (-0.42, 0.78), (-0.52, 0.64), (-0.52, -0.6)],
      -0.07, 0.07, 0.03, rot=(0, math.pi / 2, 0))   # (outline in y and z, extruded along x: the shield's thickness)
box('face', 'p', (0.065, 0, 0), (0.06, 1.3, 0.8), 0.03, taper=(0.8, 1.0))
box('boss', 'j', (0.1, 0.12, 0), (0.06, 0.34, 0.3), 0.02, taper=(0.6, 0.7))
box('crest', 'e', (0.135, 0.12, 0), (0.02, 0.2, 0.06), 0.006)
for y in (0.12 - 0.11, 0.12 + 0.11): box('crest_bar', 'e', (0.13, y, 0), (0.02, 0.03, 0.16), 0.006)
for y in (-0.7, 0.7): box('rail_h', 'e', (0.04, y, 0), (0.07, 0.04, 0.9), 0.008)
for z in (-0.46, 0.46): box('rail_v', 'e', (0.04, 0, z), (0.07, 1.26, 0.04), 0.008)
for y in (-0.52, -0.32): box('vent', 'j', (0.1, y, 0), (0.03, 0.06, 0.5), 0.01)
box('grip', 'j', (-0.1, 0, 0.14), (0.08, 0.36, 0.08), 0.02)
box('backplate', 'j', (-0.085, 0, 0), (0.04, 1.2, 0.7), 0.012)

save(out_dir(), 'shield')
