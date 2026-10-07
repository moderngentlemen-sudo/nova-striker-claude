# Renders a review sheet of a character .blend: front, three-quarter, side, back and a face close-up (Cycles, CPU).
# Run: blender -b <file.blend> -P render_turnaround.py -- <out.png> [samples]
import math, os, sys, bpy
from mathutils import Vector
args = sys.argv[sys.argv.index('--') + 1:]
OUT = args[0]; SAMPLES = int(args[1]) if len(args) > 1 else 48
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 560, 1000
sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Medium High Contrast'
w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (0.06, 0.075, 0.1, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 0.8
def light(name, kind, loc, energy, color=(1, 1, 1), size=1.0):
    l = bpy.data.lights.new(name, kind); l.energy = energy; l.color = color
    if kind == 'AREA': l.size = size
    o = bpy.data.objects.new(name, l); o.location = loc; sc.collection.objects.link(o)
    o.rotation_euler = (Vector((0, 0, 1.2)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
light('Key', 'AREA', (1.6, -2.4, 2.6), 600, size=1.6)
light('Fill', 'AREA', (-2.2, -1.6, 1.4), 180, (0.8, 0.88, 1.0), size=2)
light('Rim', 'AREA', (-0.8, 2.6, 2.4), 450, (0.75, 0.85, 1.0), size=1.2)
floor = bpy.data.meshes.new('floor'); floor.from_pydata([(-3, -3, 0), (3, -3, 0), (3, 3, 0), (-3, 3, 0)], [], [(0, 1, 2, 3)])
fo = bpy.data.objects.new('floor', floor); sc.collection.objects.link(fo)
fm = bpy.data.materials.new('floor'); fm.use_nodes = True; fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.05, 0.06, 0.08, 1)
fo.data.materials.append(fm)
cam = bpy.data.cameras.new('cam'); cam.lens = 85; co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
views = [('front', 0, 0.95, 6.2), ('three_quarter', 40, 0.95, 6.2), ('side', 90, 0.95, 6.2), ('back', 180, 0.95, 6.2), ('face', 25, 1.7, 1.25)]
files = []
for name, ang, tz, dist in views:
    a = math.radians(ang)
    co.location = (math.sin(a) * dist, -math.cos(a) * dist, tz + (0.05 if name != 'face' else 0.0))
    co.rotation_euler = (Vector((0, 0, tz)) - co.location).to_track_quat('-Z', 'Y').to_euler()
    f = OUT.replace('.png', f'_{name}.png'); sc.render.filepath = f; bpy.ops.render.render(write_still=True); files.append(f)
print('rendered', files)
