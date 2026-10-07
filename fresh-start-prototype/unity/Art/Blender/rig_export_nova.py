# Rigs Nova's model and exports it for the game.
# Run after build_nova.py: blender -b <out>/nova.blend -P rig_export_nova.py -- <path/to/nova_model.fbx>
# The skeleton's bones carry the names the game drives (Models.cs): root, hips, spine, head, upperarm/forearm/hand
# and thigh/shin/foot with .L/.R. The bodysuit is skinned to them; every armour piece follows one bone rigidly.
# Everything is joined into three meshes the game shows or hides: Nova_Body and Nova_FaceAndHair are skinned, and
# Nova_Helmet is a rigid piece hung on the head bone, so the game can knock it off whole.
import math, os, sys, bpy
from mathutils import Vector
OUT = sys.argv[sys.argv.index('--') + 1]
sc = bpy.context.scene

SH, SHX, ARM, UA, FA = 1.47, 0.195, math.radians(24), 0.29, 0.26
def arm_pt(side, d): return Vector((side * (SHX + math.sin(ARM) * d), 0.0, SH - math.cos(ARM) * d))

# ---- Skeleton ----
arm = bpy.data.armatures.new('Nova_Skeleton'); rig = bpy.data.objects.new('Nova_Rig', arm); sc.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig; rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
def bone(name, head, tail, parent=None, connect=False):
    b = arm.edit_bones.new(name); b.head = Vector(head); b.tail = Vector(tail)
    if parent: b.parent = arm.edit_bones[parent]; b.use_connect = connect
    return b
bone('root', (0, 0, 0), (0, 0.15, 0))
bone('hips', (0, 0, 0.97), (0, 0, 1.06), 'root')
bone('spine', (0, 0, 1.06), (0, 0, 1.56), 'hips')
bone('head', (0, 0, 1.58), (0, 0, 1.86), 'spine')
for s, x in ((1, 'L'), (-1, 'R')):
    bone(f'upperarm.{x}', arm_pt(s, 0), arm_pt(s, UA), 'spine')
    bone(f'forearm.{x}', arm_pt(s, UA), arm_pt(s, UA + FA), f'upperarm.{x}', True)
    bone(f'hand.{x}', arm_pt(s, UA + FA), arm_pt(s, UA + FA + 0.17), f'forearm.{x}', True)
    bone(f'thigh.{x}', (s * 0.095, 0, 0.92), (s * 0.1, 0, 0.5), 'hips')
    bone(f'shin.{x}', (s * 0.1, 0, 0.5), (s * 0.1, 0, 0.09), f'thigh.{x}', True)
    bone(f'foot.{x}', (s * 0.1, 0, 0.09), (s * 0.1, -0.13, 0.035), f'shin.{x}', True)
bpy.ops.object.mode_set(mode='OBJECT')

# ---- Which bone each rigid piece follows (by name prefix; {s} 1 is his left, -1 his right) ----
RULES = [
    (('Nova_Head', 'Nova_Hair', 'Nova_Lock', 'Nova_Side', 'Nova_Eye', 'Nova_Iris', 'Nova_Brow', 'Nova_Ear', 'Nova_Nose', 'Nova_Mouth', 'Nova_Helm', 'Nova_Neck'), 'head'),
    (('Nova_Collar', 'Nova_Breastplate', 'Nova_Backplate', 'Nova_Star', 'Nova_ChestSeam', 'Nova_ChestLine', 'Nova_ChestRib', 'Nova_CollarSeam'), 'spine'),
    (('Nova_Belt', 'Nova_Buckle'), 'hips'),
    (('Nova_Pauldron',), 'upperarm'), (('Nova_ForearmGuard',), 'forearm.L'), (('Nova_Bracer',), 'forearm.R'),
    (('Nova_Glove', 'Nova_Knuckle'), 'hand'), (('Nova_ThighPanel', 'Nova_ThighSeam'), 'thigh'),
    (('Nova_KneePad', 'Nova_KneeGem', 'Nova_Greave'), 'shin'), (('Nova_Boot', 'Nova_Sole'), 'foot'),
]
def bone_for(o):
    for prefixes, b in RULES:
        if o.name.startswith(prefixes):
            if '.' in b or b in ('head', 'spine', 'hips'): return b   # (only limbs come in left and right)
            sd = 'R' if o.name.endswith('-1') else 'L' if o.name.endswith('1') else None
            return f'{b}.{sd}' if sd else b
    return None

def apply_all(o):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    if o.type == 'CURVE':
        o.data.resolution_u = 4 if o.name.startswith(('Nova_Lock', 'Nova_Side', 'Nova_Brow')) else 3; o.data.bevel_resolution = 2 if o.name.startswith(('Nova_Lock', 'Nova_Side')) else 1; bpy.ops.object.convert(target='MESH')
    else:
        for m in list(o.modifiers):
            if m.type == 'SUBSURF': m.levels = min(m.levels, 1 if o.name == 'Nova_Body' else 0 if 'Seam' in o.name or 'Line' in o.name else 1)
            bpy.ops.object.modifier_apply(modifier=m.name)

meshes = {'Nova_Body': [], 'Nova_FaceAndHair': [], 'Nova_Helmet': []}
for o in list(bpy.data.objects):
    if o.type not in ('MESH', 'CURVE') or o == rig: continue
    look = 'Nova_FaceAndHair' if any(c.name == 'Nova_FaceAndHair' for c in o.users_collection) else \
           'Nova_Helmet' if any(c.name == 'Nova_Helmet' for c in o.users_collection) else 'Nova_Body'
    apply_all(o)
    if o.name == 'Nova_Body':
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
        bpy.ops.object.parent_set(type='ARMATURE_AUTO')
        o.parent = None
        if not o.vertex_groups or all(len(g.name) == 0 for g in o.vertex_groups): print('WARNING: automatic weights failed')
    else:
        b = bone_for(o)
        if b is None: print('WARNING: no bone for', o.name); b = 'spine'
        g = o.vertex_groups.new(name=b); g.add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')
    for m in list(o.modifiers): o.modifiers.remove(m)
    meshes[look].append(o)

# Bring each look within its triangle budget by simplifying only its large pieces (eyes, seams and other small
# details would vanish if simplified), then join it into one mesh skinned to the skeleton
BUDGET = {'Nova_Body': 30000, 'Nova_FaceAndHair': 18000, 'Nova_Helmet': 7000}
def tri_count(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)
for name, objs in meshes.items():
    big = [o for o in objs if tri_count(o) > 1500]
    small = sum(tri_count(o) for o in objs if o not in big); large = sum(tri_count(o) for o in big)
    if large and small + large > BUDGET[name]:
        r = max(0.05, (BUDGET[name] - small) / large)
        for o in big:
            d = o.modifiers.new('Decimate', 'DECIMATE'); d.ratio = r
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
            bpy.ops.object.modifier_apply(modifier=d.name)

for name, objs in meshes.items():
    if not objs: continue
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    j = bpy.context.view_layer.objects.active; j.name = name; j.data.name = name
    if name == 'Nova_Helmet':                      # rigid on the head bone (it keeps its place in the world)
        w = j.matrix_world.copy(); j.vertex_groups.clear(); j.parent = rig; j.parent_type = 'BONE'; j.parent_bone = 'head'; j.matrix_world = w
    else:
        j.parent = rig; m = j.modifiers.new('Armature', 'ARMATURE'); m.object = rig
    tris = sum(len(p.vertices) - 2 for p in j.data.polygons)
    print(f'{name}: {tris} triangles, {len(j.data.materials)} materials')

bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True)
for n in meshes:
    if n in bpy.data.objects: bpy.data.objects[n].select_set(True)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', use_armature_deform_only=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT.replace('.fbx', '_rigged.blend'))
print('exported', OUT, os.path.getsize(OUT) // 1024, 'KB')
