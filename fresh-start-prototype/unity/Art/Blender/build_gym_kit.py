# The Movement Gym's dressing kit: modelled pieces the game lays over its level boxes and on the back terrace.
# Each piece is built at the origin in its own collection (Kit_<Name>); X is along the route, Z up, and its front
# faces -Y (toward the camera). Units are metres. export_kit.py writes them out for the game.
# Run: blender -b -P build_gym_kit.py -- <out-dir>   (writes gym_kit.blend)
import math, os, sys, bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_lib import *

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/tmp'
reset()
HULL = mat('Kit_Hull', srgb('#e9eef3'), metal=0.1, rough=0.42, coat=0.3)
HULLB = mat('Kit_HullB', srgb('#dce3eb'), metal=0.1, rough=0.46, coat=0.25)   # (a second paint batch, so panels vary)
STENCIL = mat('Kit_Stencil', srgb('#5d6b7d'), rough=0.6)
SHADOW = mat('Kit_Shadow', (0, 0, 0), rough=1)
DARK = mat('Kit_HullDark', srgb('#3d4f66'), metal=0.2, rough=0.5)
NAVY = mat('Kit_Navy', srgb('#2b4f7e'), metal=0.15, rough=0.45, coat=0.2)
METAL = mat('Kit_Metal', srgb('#9aa6b2'), metal=0.85, rough=0.3)
BLACK = mat('Kit_Black', srgb('#14181f'), rough=0.6)
LIGHT = mat('Kit_Light', srgb('#8fecff'), emit=srgb('#4fd6ff'), strength=14)
HAZ = mat('Kit_Hazard', srgb('#f2c230'), rough=0.5)
GLASS = mat('Kit_Glass', srgb('#cfeeff'), rough=0.05)
GLASS.node_tree.nodes['Principled BSDF'].inputs['Transmission Weight'].default_value = 0.9
PAD = mat('Kit_Pad', srgb('#2fb5c9'), rough=0.75, sheen=0.3)
PADO = mat('Kit_PadOrange', srgb('#f08a3c'), rough=0.75, sheen=0.3)
HOLO = mat('Kit_Holo', srgb('#7fe3ff'), emit=srgb('#5fd8ff'), strength=4)
SIGN = mat('Kit_Sign', srgb('#ffffff'), emit=srgb('#eaf8ff'), strength=5)

def kit(name):
    col = bpy.data.collections.new('Kit_' + name); bpy.context.scene.collection.children.link(col)
    before = set(bpy.context.scene.collection.objects)
    return col, before
def done(col, before):
    for o in list(bpy.context.scene.collection.objects):
        if o not in before:
            bpy.context.scene.collection.objects.unlink(o); col.objects.link(o)

def stripes(name, x0, x1, z0, z1, y, w=0.18, plane='xz'):
    """Diagonal yellow and black bands filling a rectangle on a face (xz: a front face at depth y; xy: a floor at height y)."""
    vs, fy, fb = [], [], []
    h = z1 - z0; n = int((x1 - x0 + h) / w) + 2
    for k in range(n):
        a = x0 - h + k * w
        quad = [(max(x0, min(x1, a)), z0), (max(x0, min(x1, a + w)), z0), (max(x0, min(x1, a + w + h)), z1), (max(x0, min(x1, a + h)), z1)]
        if quad[0][0] == quad[1][0] and quad[3][0] == quad[2][0]: continue
        base = len(vs)
        for (u, v) in quad: vs.append((u, y, v) if plane == 'xz' else (u, v, y))
        (fy if k % 2 == 0 else fb).append((base, base + 1, base + 2, base + 3))
    def make(nm, faces, m):
        if not faces: return
        used = sorted({i for f in faces for i in f}); remap = {i: j for j, i in enumerate(used)}
        o = mesh_obj(nm, [vs[i] for i in used], [tuple(remap[i] for i in f) for f in faces], m)
        for p in o.data.polygons: p.use_smooth = False
    make(name + 'Y', fy, HAZ); make(name + 'B', fb, BLACK)

# ---- Deck facade: the top band (nosing, recessed light, upper panels), origin at the top front edge ----
c, b = kit('DeckFaceTop')
box('nose', METAL, (0, -0.03, -0.06), (2.0, 0.12, 0.12), 0.03, 3)
box('recess', BLACK, (0, 0.0, -0.2), (2.0, 0.04, 0.09), 0)
box('strip', LIGHT, (0, -0.012, -0.2), (1.98, 0.02, 0.045), 0.004)
for s in (-1, 1):
    box('panel', HULL if s < 0 else HULLB, (s * 0.495, -0.02, -0.72), (0.97, 0.05, 0.86), 0.02, 3)
    for bx, bz in ((0.42, -0.34), (0.42, -1.1)):
        cyl('bolt', METAL, (s * 0.495 + s * bx * (1 if bz > -1 else -1) * 0.9, -0.05, -0.72 + (0.36 if bz > -1 else -0.36)), (s * 0.495 + s * bx * 0.9 * (1 if bz > -1 else -1), -0.06, -0.72 + (0.36 if bz > -1 else -0.36)), 0.018, 8)
box('band', DARK, (0, 0.0, -1.18), (2.0, 0.04, 0.05), 0)
# a pipe run along the band (it continues from module to module), held by brackets
for z, r in ((-1.05, 0.02), (-1.105, 0.013)): cyl('pipe', METAL, (-1.0, -0.075, z), (1.0, -0.075, z), r, 12)
for x in (-0.5, 0.5): box('bracket', DARK, (x, -0.06, -1.075), (0.05, 0.05, 0.11), 0.01)
done(c, b)

# ---- Deck facade: 2 x 2 m middle sections, origin at the top centre: vents, a hatch, a rib ----
for variant in ('Vent', 'Hatch', 'Rib'):
    c, b = kit('DeckFace' + variant)
    for s in (-1, 1):
        box('panel', HULLB if (s > 0) != (variant == 'Rib') else HULL, (s * 0.495, -0.02, -1.0), (0.97, 0.05, 1.94), 0.02, 3)
    box('seam', DARK, (0, 0.005, -1.0), (2.0, 0.03, 2.0), 0)
    if variant == 'Vent':
        box('ventframe', DARK, (0.495, -0.05, -0.75), (0.7, 0.03, 0.5), 0.01)
        for k in range(6): box('slat', METAL, (0.495, -0.07, -0.55 - k * 0.08), (0.64, 0.02, 0.025), 0.005, 1, rot=(math.radians(30), 0, 0))
        box('sticker', HAZ, (0.14, -0.047, -0.3), (0.12, 0.004, 0.12), 0.002)
        text('stickermark', BLACK, '!', (0.14, -0.05, -0.305), 0.09, depth=0.001)
        box('ventframe', DARK, (-0.495, -0.05, -1.45), (0.7, 0.03, 0.5), 0.01)
        for k in range(6): box('slat', METAL, (-0.495, -0.07, -1.25 - k * 0.08), (0.64, 0.02, 0.025), 0.005, 1, rot=(math.radians(30), 0, 0))
    elif variant == 'Hatch':
        box('hatch', NAVY, (0.495, -0.055, -1.0), (0.66, 0.03, 1.3), 0.04, 3)
        box('handle', METAL, (0.72, -0.08, -1.0), (0.04, 0.03, 0.3), 0.01)
        box('status', LIGHT, (0.3, -0.075, -0.45), (0.08, 0.01, 0.03), 0.004)
        stripes('hz', -0.9, -0.1, -1.92, -1.78, -0.05)
        t = text('label', STENCIL, 'G-2', (0.495, -0.072, -0.2), 0.12, depth=0.002)
        t = text('label2', STENCIL, 'ACCESS', (0.495, -0.072, -1.78), 0.07, depth=0.002)
    else:
        box('rib', METAL, (0, -0.07, -1.0), (0.18, 0.1, 2.0), 0.02)
        box('ribcore', DARK, (0, -0.12, -1.0), (0.06, 0.02, 1.9), 0.005)
        for z in (-0.25, -1.75): box('clamp', HULL, (0, -0.13, z), (0.26, 0.04, 0.14), 0.02)
    done(c, b)

# ---- Floor markings (flat, a hair above the deck): start line, chevrons, hazard edge ----
c, b = kit('StartLine'); box('line', LIGHT, (0, 0, 0.004), (0.14, 4.2, 0.008), 0)
for y in (-1.6, 0, 1.6): box('tick', SIGN, (0.3, y, 0.004), (0.4, 0.06, 0.008), 0)
done(c, b)
c, b = kit('Chevrons')
for k in range(3):
    x = -0.6 + k * 0.6
    for s in (-1, 1):
        o = box('chev', HAZ, (x + 0.16, s * 0.3, 0.004), (0.7, 0.16, 0.008), 0, rot=(0, 0, -s * math.radians(40)))
done(c, b)
c, b = kit('HazardEdge'); stripes('he', -1.0, 1.0, -0.12, 0.12, 0.004, 0.2, plane='xy'); done(c, b)

# ---- Deck floor plating, 2 m along the route by the deck's 4.4 m depth: seams, grip strips, a lit lane edge ----
c, b = kit('DeckFloor')
SEAM = mat('Kit_Seam', srgb('#9aa6b4'), rough=0.6)
GRIP = mat('Kit_Grip', srgb('#7d8794'), rough=0.95)
for y in (-1.1, 0.0, 1.1): box('seam', SEAM, (0, y, 0.002), (2.0, 0.025, 0.004), 0)
box('seamx', SEAM, (-0.995, 0, 0.002), (0.025, 4.4, 0.004), 0)
for s in (-1, 1): box('grip', GRIP, (s * 0.5, -1.75, 0.003), (0.7, 0.32, 0.006), 0)
box('lane', LIGHT, (0, -2.05, 0.003), (2.0, 0.05, 0.006), 0)
box('floorlight', LIGHT, (-0.995, 0, 0.004), (0.06, 0.06, 0.008), 0)
done(c, b)
c, b = kit('DeckFloorDrain')      # the same plating with a drain grate set into it
for y in (-1.1, 0.0, 1.1): box('seam', SEAM, (0, y, 0.002), (2.0, 0.025, 0.004), 0)
box('seamx', SEAM, (-0.995, 0, 0.002), (0.025, 4.4, 0.004), 0)
for s in (-1, 1): box('grip', GRIP, (s * 0.5, -1.75, 0.003), (0.7, 0.32, 0.006), 0)
box('lane', LIGHT, (0, -2.05, 0.003), (2.0, 0.05, 0.006), 0)
box('grate', DARK, (0.3, 0.55, 0.003), (0.9, 0.3, 0.006), 0.004)
for k in range(8): box('slot', BLACK, (-0.08 + k * 0.11, 0.55, 0.0065), (0.05, 0.24, 0.002), 0)
done(c, b)
c, b = kit('Shadow')              # a soft contact shadow, 1 x 1 m, scaled to what it sits under
mesh_obj('shadow', [(-0.5, -0.5, 0.004), (0.5, -0.5, 0.004), (0.5, 0.5, 0.004), (-0.5, 0.5, 0.004)], [(0, 1, 2, 3)], SHADOW)
done(c, b)

# ---- Terrace railing, 2 m ----
c, b = kit('Railing')
box('plinth', DARK, (0, 0, 0.06), (2.0, 0.16, 0.12), 0.02)
for s in (-1, 1): cyl('post', METAL, (s * 0.97, 0, 0.1), (s * 0.97, 0, 1.08), 0.03, 12)
cyl('rail', HULL, (-1.0, 0, 1.08), (1.0, 0, 1.08), 0.04, 12)
cyl('midrail', METAL, (-1.0, 0.03, 0.55), (1.0, 0.03, 0.55), 0.015, 10)
box('glass', GLASS, (0, 0, 0.6), (1.88, 0.02, 0.9), 0.005)
box('glow', LIGHT, (0, -0.045, 1.06), (1.9, 0.01, 0.015), 0)
done(c, b)

# ---- Floodlight mast ----
c, b = kit('FloodMast')
box('base', DARK, (0, 0, 0.1), (0.9, 0.9, 0.2), 0.04)
cyl('pole', HULL, (0, 0, 0.2), (0, 0, 9.0), 0.13, 16, r2=0.09)
for z in (3.0, 6.0): cyl('ring', NAVY, (0, 0, z), (0, 0, z + 0.12), 0.15, 16)
box('arm', METAL, (0, -0.25, 9.05), (1.6, 0.12, 0.12), 0.02)
for x in (-0.6, -0.2, 0.2, 0.6):
    box('lamp', DARK, (x, -0.45, 8.85), (0.34, 0.24, 0.3), 0.03, rot=(math.radians(-25), 0, 0))
    box('lens', LIGHT, (x, -0.58, 8.8), (0.28, 0.02, 0.24), 0.01, rot=(math.radians(-25), 0, 0))
done(c, b)

# ---- Training gear ----
c, b = kit('Hurdle')
for s in (-1, 1):
    box('foot', DARK, (s * 0.6, 0, 0.03), (0.08, 0.5, 0.06), 0.01)
    cyl('leg', METAL, (s * 0.6, 0, 0.06), (s * 0.6, 0, 0.82), 0.025, 10)
box('bar', HAZ, (0, 0, 0.82), (1.3, 0.06, 0.1), 0.02)
for x in (-0.4, 0, 0.4): box('band', BLACK, (x, -0.001, 0.82), (0.12, 0.062, 0.102), 0)
done(c, b)
c, b = kit('AgilityRing')
cyl('base', METAL, (0, 0, 0), (0, 0, 0.08), 0.35, 24)
cyl('stem', DARK, (0, 0, 0.08), (0, 0, 0.6), 0.04, 12)
tube('ringtube', HOLO, [Vector((math.sin(a) * 0.7, 0, 1.3 + math.cos(a) * 0.7)) for a in [k * math.pi / 8 for k in range(16)]], 0.035, cyclic=True)
tube('ringcore', METAL, [Vector((math.sin(a) * 0.7, 0.03, 1.3 + math.cos(a) * 0.7)) for a in [k * math.pi / 8 for k in range(16)]], 0.018, cyclic=True)
done(c, b)
c, b = kit('CrashMat')
box('mat', PAD, (0, 0, 0.18), (2.2, 1.3, 0.36), 0.08, 4)
box('band', PADO, (0, 0, 0.18), (2.21, 0.25, 0.362), 0.06, 3)
done(c, b)
c, b = kit('Lockers')
for k in range(3):
    x = -0.56 + k * 0.56
    box('locker', DARK, (x, 0, 0.95), (0.54, 0.5, 1.9), 0.02)
    box('door', NAVY, (x, -0.255, 0.95), (0.46, 0.02, 1.78), 0.01)
    for z in (1.6, 1.68, 1.76): box('vent', BLACK, (x, -0.27, z), (0.3, 0.01, 0.025), 0)
    box('handle', METAL, (x + 0.16, -0.275, 1.0), (0.03, 0.03, 0.2), 0.008)
    box('led', LIGHT, (x - 0.15, -0.268, 1.45), (0.06, 0.006, 0.02), 0)
box('top', HULL, (0, 0, 1.92), (1.7, 0.54, 0.05), 0.015)
done(c, b)
c, b = kit('Bench')
box('seat', PAD, (0, 0, 0.45), (1.6, 0.42, 0.1), 0.04, 3)
for s in (-1, 1): box('leg', METAL, (s * 0.65, 0, 0.2), (0.06, 0.36, 0.4), 0.01)
done(c, b)
c, b = kit('TargetStand')
box('foot', DARK, (0, 0, 0.04), (0.7, 0.7, 0.08), 0.02)
cyl('post', METAL, (0, 0, 0.08), (0, 0, 1.2), 0.035, 10)
for r, m in ((0.45, HULL), (0.33, HAZ), (0.21, HULL), (0.09, LIGHT)):
    cyl('disc', m, (0, -0.02 - (0.45 - r) * 0.05, 1.55), (0, -0.04 - (0.45 - r) * 0.05, 1.55), r, 32)
done(c, b)
c, b = kit('HoloDisplay')
box('pedestal', DARK, (0, 0, 0.45), (0.5, 0.4, 0.9), 0.03)
box('cap', HULL, (0, 0, 0.92), (0.56, 0.46, 0.05), 0.015)
box('frame', METAL, (0, 0.05, 1.35), (0.9, 0.04, 0.6), 0.02, rot=(math.radians(-15), 0, 0))
box('screen', HOLO, (0, 0.025, 1.35), (0.82, 0.01, 0.52), 0, rot=(math.radians(-15), 0, 0))
done(c, b)

# ---- The sign ----
c, b = kit('GymSign')
box('board', NAVY, (0, 0.05, 0), (12.4, 0.2, 1.9), 0.06, 3)
box('edge', LIGHT, (0, -0.06, -0.92), (12.2, 0.02, 0.05), 0)
box('edge', LIGHT, (0, -0.06, 0.92), (12.2, 0.02, 0.05), 0)
t = text('words', SIGN, 'MOVEMENT GYM', (0, -0.06, 0.0), 1.15, depth=0.03)
for s in (-1, 1): cyl('pylon', HULL, (s * 4.5, 0.15, -9), (s * 4.5, 0.15, -0.9), 0.18, 16)
done(c, b)

# ---- The wall-jump panel (0.8 x 6.5 m face) and the slide tunnel's lower edge (6 m) ----
c, b = kit('WallPanelDress')
stripes('top', -0.4, 0.4, 2.95, 3.25, -0.02); stripes('bot', -0.4, 0.4, -3.25, -2.95, -0.02)
for k in range(7):
    box('grip', PADO if k % 2 else PAD, (0.12 if k % 2 else -0.12, -0.04, -2.4 + k * 0.8), (0.24, 0.06, 0.18), 0.03, 3)
for s in (-1, 1): box('edge', LIGHT, (s * 0.39, -0.03, 0), (0.025, 0.02, 5.8), 0)
done(c, b)
c, b = kit('TunnelEdge')
stripes('band', -3.0, 3.0, 0.0, 0.3, -0.02)
box('underlight', LIGHT, (0, 0.6, -0.012), (5.8, 0.06, 0.02), 0)
done(c, b)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'gym_kit.blend'))
print('built kit:', [c.name for c in bpy.data.collections])
