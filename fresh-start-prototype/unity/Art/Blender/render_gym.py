# Renders the Movement Gym in Blender, without and with the dressing kit, from gameplay-like cameras, for review.
# The level's boxes and the existing props are rebuilt roughly as the game draws them (View.BuildLevel/BuildProps).
# Run: blender -b -P render_gym.py -- <dir holding gym_kit.blend> <out-prefix> [samples]
import math, os, sys, bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_lib import *
import gym_layout

args = sys.argv[sys.argv.index('--') + 1:]
KITDIR, OUT = args[0], args[1]; SAMPLES = int(args[2]) if len(args) > 2 else 32
reset()
sc = bpy.context.scene
def B(x, y, dz): return Vector((x, -dz, y))          # route coordinates (x, y up, dz toward the camera) to Blender's

# ---- The level as the game draws it now ----
CAPM = mat('cap', srgb('#d9dfe7'), rough=0.82); BODY = mat('body', srgb('#5f7897'), rough=0.72); DARKM = mat('dark', srgb('#46596f'), rough=0.7)
TRIM = mat('trim', srgb('#7fe3ff'), emit=srgb('#4fd6ff'), strength=4)
def level_box(x0, x1, y0, y1, depth, kind='s'):
    h = y1 - y0; capH = min(0.22, h * 0.4)
    box('lvl', DARKM if kind == 'o' else BODY, B((x0 + x1) / 2, y0 + (h - capH) / 2, 0), (x1 - x0, depth, h - capH), 0.08)
    box('cap', CAPM, B((x0 + x1) / 2, y1 - capH / 2, 0), (x1 - x0 + 0.08, depth + 0.1, capH), 0.04)
    if kind != 'tunnel': box('trim', TRIM, B((x0 + x1) / 2, y1 - capH - 0.05, depth / 2 + 0.02), (x1 - x0, 0.06, 0.06), 0)
for x0, x1, y0, y1 in gym_layout.GROUND: level_box(x0, x1, y0, y1, 4.4)
level_box(28.5, 29.3, 2.1, 8.6, 1.8); level_box(44, 50, 1.0, 6.5, 4.4, 'tunnel'); level_box(57.5, 58.5, 0, 3, 1.8)
# the back terrace and its props
box('terrace', mat('terr', srgb('#55708f'), rough=0.75), B(43.5, -3.6, -6.4), (108, 8, 5), 0.05)
box('terrtop', mat('terrtop', srgb('#c9d2dd'), rough=0.85), B(43.5, -1.2, -6.4), (108, 8.1, 0.2), 0.02)
WHITE = mat('white', srgb('#f1f4f7'), rough=0.5); NAVYP = mat('navyp', srgb('#2b4f7e'), rough=0.6)
LEAF = mat('leaf', srgb('#5fb36a'), rough=0.9); LAMP = mat('lamp', srgb('#9ff0ff'), emit=srgb('#5fe0ff'), strength=6)
CLOTH = mat('cloth', srgb('#2fb5c9'), rough=0.8)
for x in range(-4, 96, 11):
    box('pot', WHITE, B(x, -1.1 + 0.45, -5.2), (1.4, 1.4, 0.9), 0.2, 3)
    patch('leaves', LEAF, B(x - 0.2, -1.1 + 1.25, -5.2), (0.75, 0.75, 0.6), res=(12, 8), thick=0, subsurf=1)
    cyl('pole', NAVYP, B(x + 5, -1.1, -7.2), B(x + 5, -1.1 + 5.5, -7.2), 0.08, 10)
    patch('lampball', LAMP, B(x + 5, -1.1 + 5.6, -7.2), (0.22, 0.22, 0.22), res=(12, 8), thick=0, subsurf=0)
    box('banner', CLOTH, B(x + 5.62, -1.1 + 3.9, -7.2), (1.1, 0.02, 2.6), 0)
for x in (8, 48, 88):
    tube('arch', WHITE, [B(x + 9 * math.cos(a), -1 + 9 * math.sin(a), -16) for a in [k * math.pi / 12 for k in range(13)]], 0.55)
# the sky city beyond: a cloud sea and distant spires
sea = box('sea', mat('sea', srgb('#eef6fb'), rough=1), B(40, -34, -100), (1200, 1200, 0.2), 0)
SP = mat('spire', srgb('#c3d7ea'), rough=0.55)
import random; random.seed(3)
for x, z in ((-30, -120), (20, -150), (55, -105), (85, -175), (130, -135), (-70, -170), (10, -210), (110, -220)):
    hh = 45 + random.random() * 65; ww = 4 + random.random() * 6
    box('spire', SP, B(x, hh / 2 - 34, z), (ww, ww, hh), 1.0)

# ---- World, sun, mist ----
w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (*srgb('#a9d4f2'), 1); w.node_tree.nodes['Background'].inputs[1].default_value = 1.2
sun = bpy.data.lights.new('sun', 'SUN'); sun.energy = 4.0; sun.angle = math.radians(3); so = bpy.data.objects.new('sun', sun); sc.collection.objects.link(so)
so.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 1600, 900
sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Medium High Contrast'
sc.view_layers[0].use_pass_mist = True; sc.world.mist_settings.start = 12; sc.world.mist_settings.depth = 90; sc.world.mist_settings.falloff = 'QUADRATIC'
sc.use_nodes = True; nt = sc.node_tree; nt.nodes.clear()
rl = nt.nodes.new('CompositorNodeRLayers'); mix = nt.nodes.new('CompositorNodeMixRGB'); comp = nt.nodes.new('CompositorNodeComposite')
mix.blend_type = 'MIX'; mix.inputs[2].default_value = (*srgb('#e4f1fa'), 1)
mp = nt.nodes.new('CompositorNodeMath'); mp.operation = 'MULTIPLY'; mp.inputs[1].default_value = 0.55
nt.links.new(rl.outputs['Mist'], mp.inputs[0]); nt.links.new(mp.outputs[0], mix.inputs[0])
nt.links.new(rl.outputs['Image'], mix.inputs[1]); nt.links.new(mix.outputs[0], comp.inputs[0])

cam = bpy.data.cameras.new('cam'); cam.lens = 32; co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
SHOTS = [('start', 6, 2.6, 0.9), ('wall', 27, 4.2, 3.0), ('tunnel', 46, 2.6, 0.9)]
def shoot(tag):
    for name, x, cy, ly in SHOTS:
        co.location = B(x, cy, 11.5); co.rotation_euler = (B(x, ly, 0) - co.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = f'{OUT}_{tag}_{name}.png'; bpy.ops.render.render(write_still=True)
shoot('before')

# ---- The kit, placed by the layout ----
with bpy.data.libraries.load(os.path.join(KITDIR, 'gym_kit.blend'), link=False) as (src, dst):
    dst.collections = [c for c in src.collections if c.startswith('Kit_')]
kits = {c.name[4:]: c for c in dst.collections}
TEX = os.path.join(KITDIR, 'tex')
SURF = {'Kit_Hull': ('paint', 2.5), 'Kit_HullB': ('paint', 2.5), 'Kit_HullDark': ('paint', 2.5), 'Kit_Navy': ('paint', 2.5),
        'Kit_Metal': ('paint', 1.5), 'Kit_Pad': ('rubber', 0.8), 'Kit_PadOrange': ('rubber', 0.8), 'Kit_Grip': ('tread', 0.5)}
def surface(m, kind, tile):
    nt = m.node_tree; bsdf = nt.nodes['Principled BSDF']
    geo = nt.nodes.new('ShaderNodeNewGeometry'); mp = nt.nodes.new('ShaderNodeVectorMath'); mp.operation = 'SCALE'; mp.inputs[3].default_value = 1 / tile
    nt.links.new(geo.outputs['Position'], mp.inputs[0])
    def img(nm, colour):
        t = nt.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(os.path.join(TEX, f'{kind}_{nm}.png'))
        t.image.colorspace_settings.name = 'sRGB' if colour else 'Non-Color'; t.projection = 'BOX'; t.projection_blend = 0.3
        nt.links.new(mp.outputs[0], t.inputs['Vector']); return t
    base = bsdf.inputs['Base Color'].default_value[:]
    a = img('albedo', False); mul = nt.nodes.new('ShaderNodeMixRGB'); mul.blend_type = 'MULTIPLY'; mul.inputs[0].default_value = 1
    mul.inputs[1].default_value = base; nt.links.new(a.outputs['Color'], mul.inputs[2]); nt.links.new(mul.outputs[0], bsdf.inputs['Base Color'])
    r = img('rough', False); rm = nt.nodes.new('ShaderNodeMath'); rm.operation = 'MULTIPLY'; rm.inputs[1].default_value = bsdf.inputs['Roughness'].default_value / 0.85
    nt.links.new(r.outputs['Color'], rm.inputs[0]); nt.links.new(rm.outputs[0], bsdf.inputs['Roughness'])
    h = img('height', False); bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.35; bump.inputs['Distance'].default_value = 0.003
    nt.links.new(h.outputs['Color'], bump.inputs['Height']); nt.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
for m in bpy.data.materials:
    if m.name in SURF: surface(m, *SURF[m.name])
sm = bpy.data.materials.get('Kit_Shadow')
if sm:          # the contact shadow: black, its alpha from the falloff texture
    nt = sm.node_tree; bsdf = nt.nodes['Principled BSDF']; t = nt.nodes.new('ShaderNodeTexImage')
    t.image = bpy.data.images.load(os.path.join(TEX, 'shadow.png')); t.image.colorspace_settings.name = 'Non-Color'
    k = nt.nodes.new('ShaderNodeMath'); k.operation = 'MULTIPLY'; k.inputs[1].default_value = 0.6
    nt.links.new(t.outputs['Color'], k.inputs[0]); nt.links.new(k.outputs[0], bsdf.inputs['Alpha'])
for name, x, y, dz, yaw, sx, sy, sz in gym_layout.placements():
    e = bpy.data.objects.new(name, None); e.instance_type = 'COLLECTION'; e.instance_collection = kits[name]
    e.location = B(x, y, dz); e.rotation_euler = (0, 0, math.radians(yaw)); e.scale = (sx, sz, sy)
    sc.collection.objects.link(e)
shoot('after')
print('rendered')
