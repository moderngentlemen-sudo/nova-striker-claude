# Review render of an enemy .blend (build_enemy_<type>.py) as the game draws it: two angles, <out>_a.png and _b.png.
# Run: NS_TEX=<preview dir of make_textures.py> blender -b <out-dir>/enemy_<type>.blend -P render_enemy.py --
#   <out> [samples] [distance]      (NOVA=<nova.blend from build_nova.py>: side on beside Nova instead, <out>_s.png)
# Game look: the export's bevels (two segments at most), the in-game surface sets (paint colour detail only on the
# clear-coated plates, as the coat template takes no relief; worn metal, paint and rubber with relief on the rest,
# tiled every 0.9 m as the export's UVs are), the rigs' pink rim pass, the dark outline shells, and the energy at the
# game's glow.
import math, os, sys, bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_lib import surface, srgb
args = sys.argv[sys.argv.index('--') + 1:]
OUT = args[0]; S = int(args[1]) if len(args) > 1 else 24
TEX = os.environ['NS_TEX']
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = S; sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 640, 560
sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Medium High Contrast'
for o in sc.objects:
    for m in list(getattr(o, 'modifiers', [])):
        if m.type == 'BEVEL': m.segments = min(m.segments, 2)

# ---- materials as the game sets them up (EnemyRigs.Mats, EnemyModels: surface detail and rims) ----
SURF = {'Enemy_Plate': ('paint', False), 'Enemy_Joint': ('paint', True), 'Enemy_Metal': ('worn', True), 'Enemy_Rubber': ('rubber', True)}
RIM = {'Enemy_Plate': ('#ff9cc5', 0.35), 'Enemy_Joint': ('#ff7fb2', 0.3), 'Enemy_Metal': ('#ff7fb2', 0.3), 'Enemy_Rubber': ('#ff7fb2', 0.2), 'Enemy_Chrome': ('#ff7fb2', 0.25)}
for m in bpy.data.materials:
    if not m.name.startswith('Enemy_'): continue
    nt = m.node_tree; b = nt.nodes['Principled BSDF']
    if m.name in SURF:
        kind, relief = SURF[m.name]
        mul = surface(m, TEX, kind, 0.9, bump=0.3 if relief else 0.0)
        if not relief:                                 # (colour only: drop the roughness and bump links)
            for l in list(nt.links):
                if l.to_socket in (b.inputs['Roughness'], b.inputs['Normal']): nt.links.remove(l)
    if m.name == 'Enemy_Energy': b.inputs['Emission Strength'].default_value = 3.0
    if m.name in RIM:
        col, k = RIM[m.name]
        lw = nt.nodes.new('ShaderNodeLayerWeight'); lw.inputs['Blend'].default_value = 0.5
        pw = nt.nodes.new('ShaderNodeMath'); pw.operation = 'POWER'; pw.inputs[1].default_value = 2.4
        nt.links.new(lw.outputs['Facing'], pw.inputs[0])
        b.inputs['Emission Color'].default_value = (*srgb(col), 1)
        mk = nt.nodes.new('ShaderNodeMath'); mk.operation = 'MULTIPLY'; mk.inputs[1].default_value = k
        nt.links.new(pw.outputs[0], mk.inputs[0]); nt.links.new(mk.outputs[0], b.inputs['Emission Strength'])
# the outline shells (Look.AddOutlines: every part but the glowing ones, pushed out 0.018 along its normals)
ol = bpy.data.materials.new('Outline'); ol.use_nodes = True; ont = ol.node_tree; ont.nodes.clear()
geo = ont.nodes.new('ShaderNodeNewGeometry'); tr = ont.nodes.new('ShaderNodeBsdfTransparent'); em = ont.nodes.new('ShaderNodeEmission')
em.inputs['Color'].default_value = (*srgb('#12060c'), 1); mx = ont.nodes.new('ShaderNodeMixShader'); out = ont.nodes.new('ShaderNodeOutputMaterial')
ont.links.new(geo.outputs['Backfacing'], mx.inputs[0]); ont.links.new(em.outputs[0], mx.inputs[1]); ont.links.new(tr.outputs[0], mx.inputs[2]); ont.links.new(mx.outputs[0], out.inputs[0])
NOVA = os.environ.get('NOVA')
if NOVA:                                               # (Nova beside it at the same scale, for comparison)
    ms0 = [o for o in sc.objects if o.type == 'MESH']
    xmin = min((o.matrix_world @ Vector(c)).x for o in ms0 for c in o.bound_box)
    with bpy.data.libraries.load(NOVA) as (src, dst): dst.objects = src.objects
    for o in dst.objects:
        if o is None or o.type not in ('MESH', 'CURVE'): continue
        sc.collection.objects.link(o)
        if o.parent is None: o.location.x += xmin - 0.7
    sc.render.resolution_x = 1000
    bpy.context.view_layer.update()
gn = bpy.data.node_groups.new('outline', 'GeometryNodeTree')
gn.interface.new_socket('Geometry', in_out='INPUT', socket_type='NodeSocketGeometry'); gn.interface.new_socket('Geometry', in_out='OUTPUT', socket_type='NodeSocketGeometry')
N = gn.nodes; gi = N.new('NodeGroupInput'); go = N.new('NodeGroupOutput'); nrm = N.new('GeometryNodeInputNormal')
sca = N.new('ShaderNodeVectorMath'); sca.operation = 'SCALE'; sca.inputs[3].default_value = 0.018
sp = N.new('GeometryNodeSetPosition'); ff = N.new('GeometryNodeFlipFaces'); sm = N.new('GeometryNodeSetMaterial'); sm.inputs['Material'].default_value = ol
L = gn.links.new
L(nrm.outputs[0], sca.inputs[0]); L(sca.outputs['Vector'], sp.inputs['Offset']); L(gi.outputs[0], sp.inputs['Geometry'])
L(sp.outputs[0], ff.inputs[0]); L(ff.outputs[0], sm.inputs[0]); L(sm.outputs[0], go.inputs[0])
for o in list(sc.objects):
    if o.type != 'MESH' or not o.data.materials or o.data.materials[0].name == 'Enemy_Energy': continue
    d = o.copy(); sc.collection.objects.link(d); d.parent = o.parent; d.matrix_world = o.matrix_world.copy()
    d.data = o.data.copy(); d.data.materials.append(ol)
    g = d.modifiers.new('outline', 'NODES'); g.node_group = gn
    d.visible_shadow = d.visible_diffuse = d.visible_glossy = d.visible_transmission = d.visible_volume_scatter = False   # (seen by the camera only, as the game draws it)
sc.cycles.transparent_max_bounces = 64

w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (0.08, 0.1, 0.13, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 0.9
ms = [o for o in sc.objects if o.type == 'MESH']
pts = [o.matrix_world @ Vector(c) for o in ms for c in o.bound_box]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
ctr = (lo + hi) / 2; R = max((hi - lo).length / 2, 0.4)
def light(name, loc, energy, color=(1, 1, 1), size=1.5):
    l = bpy.data.lights.new(name, 'AREA'); l.energy = energy * R * R; l.color = color; l.size = size * R
    o = bpy.data.objects.new(name, l); o.location = ctr + Vector(loc) * R; sc.collection.objects.link(o)
    o.rotation_euler = (ctr - o.location).to_track_quat('-Z', 'Y').to_euler()
light('Key', (1.5, -2.2, 2.4), 260); light('Fill', (-2.4, -1.4, 0.8), 70, (0.8, 0.88, 1)); light('Rim', (-0.6, 2.6, 2.2), 200, (0.8, 0.85, 1))
fl = bpy.data.meshes.new('floor'); fl.from_pydata([(-20, -20, 0), (20, -20, 0), (20, 20, 0), (-20, 20, 0)], [], [(0, 1, 2, 3)])
fo = bpy.data.objects.new('floor', fl); sc.collection.objects.link(fo)
fm = bpy.data.materials.new('floor'); fm.use_nodes = True; fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.06, 0.07, 0.09, 1); fo.data.materials.append(fm)
cam = bpy.data.cameras.new('cam'); cam.lens = 60; co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
for name, ang, el in ((('s', 0, 6),) if NOVA else (('a', 30, 12), ('b', -128, 24))):
    a = math.radians(ang); e = math.radians(el); d = R * float(args[2] if len(args) > 2 else 3.6)
    co.location = ctr + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * d
    co.rotation_euler = (ctr - co.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = f'{OUT}_{name}.png'; bpy.ops.render.render(write_still=True)
