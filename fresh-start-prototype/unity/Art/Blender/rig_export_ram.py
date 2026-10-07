# Rigs RAM's model and exports it for the game, as rig_export_nova.py does for Nova.
# Run after build_ram.py (without NS_TEX, so no preview textures go into the file):
#   blender -b <out>/ram.blend -P rig_export_ram.py -- <path/to/ram_model.fbx>
# The bones carry the names Models.cs drives (root, hips, spine, head, upperarm/forearm/hand, thigh/shin/foot with
# .L/.R), placed on build_ram.py's landmarks. The undersuit is skinned to them; every armour piece follows one bone
# rigidly. Everything is joined into one mesh, Ram_Body. The Rampart and the Breach Cannon stay the game's own.
# The mesh gets box-projected UVs (one tile every UV_TILE m) for the game's battle-worn texture ("worn").
import math, os, sys, bpy, bmesh
from mathutils import Vector
OUT = sys.argv[sys.argv.index('--') + 1]
sc = bpy.context.scene

K = 1.34                                           # (as build_ram.py)
HIP_Z, HIP_X = 0.95 * K, 0.16 * K
KNEE_Z, ANK_Z = (0.95 - 0.46) * K, (0.95 - 0.92) * K + 0.04
SPINE_Z, SH_Z, SH_X = 1.03 * K, (1.03 + 0.53) * K, 0.42 * K
UA, FA, ARM = 0.31 * K, 0.29 * K, math.radians(14)
UV_TILE = 1.2
def arm_pt(s, d): return Vector((s * (SH_X + math.sin(ARM) * d), 0.0, SH_Z - math.cos(ARM) * d))

# ---- Skeleton ----
arm = bpy.data.armatures.new('Ram_Skeleton'); rig = bpy.data.objects.new('Ram_Rig', arm); sc.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig; rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
def bone(name, head, tail, parent=None, connect=False):
    b = arm.edit_bones.new(name); b.head = Vector(head); b.tail = Vector(tail)
    if parent: b.parent = arm.edit_bones[parent]; b.use_connect = connect
    return b
bone('root', (0, 0, 0), (0, 0.2, 0))
bone('hips', (0, 0, HIP_Z), (0, 0, SPINE_Z), 'root')
bone('spine', (0, 0, SPINE_Z), (0, 0, SH_Z + 0.05), 'hips')
bone('head', (0, -0.05, SH_Z + 0.08), (0, -0.17, SH_Z + 0.5), 'spine')
for s, x in ((1, 'L'), (-1, 'R')):
    bone(f'upperarm.{x}', arm_pt(s, 0), arm_pt(s, UA), 'spine')
    bone(f'forearm.{x}', arm_pt(s, UA), arm_pt(s, UA + FA), f'upperarm.{x}', True)
    bone(f'hand.{x}', arm_pt(s, UA + FA), arm_pt(s, UA + FA + 0.26), f'forearm.{x}', True)
    bone(f'thigh.{x}', (s * HIP_X, 0, HIP_Z - 0.03), (s * HIP_X, 0, KNEE_Z), 'hips')
    bone(f'shin.{x}', (s * HIP_X, 0, KNEE_Z), (s * HIP_X, 0, ANK_Z), f'thigh.{x}', True)
    bone(f'foot.{x}', (s * HIP_X, 0, ANK_Z), (s * HIP_X, -0.4, 0.05), f'shin.{x}', True)
bpy.ops.object.mode_set(mode='OBJECT')

# ---- Which bone each rigid piece follows (by name prefix; a trailing 1 is his left, -1 his right) ----
RULES = [
    (('Ram_Fist', 'Ram_Finger', 'Ram_Thumb'), 'hand'),            # (first: Ram_Finger would match the fins' Ram_Fin)
    (('Ram_Helm', 'Ram_Crest', 'Ram_Face', 'Ram_Visor', 'Ram_Jaw', 'Ram_Horn', 'Ram_Fin', 'Ram_Sensor'), 'head'),
    (('Ram_Chest', 'Ram_Pec', 'Ram_Rib', 'Ram_Core', 'Ram_Backplate', 'Ram_Pack', 'Ram_Stack', 'Ram_Collar', 'Ram_AbBand', 'Ram_AbGlow'), 'spine'),
    (('Ram_Pelvis', 'Ram_Cod'), 'hips'),
    (('Ram_Pauldron', 'Ram_Sleeve'), 'upperarm'), (('Ram_Elbow', 'Ram_Gauntlet'), 'forearm'),
    (('Ram_HipDisc', 'Ram_HipGlow', 'Ram_Thigh'), 'thigh'),
    (('Ram_Knee', 'Ram_Shin', 'Ram_Piston', 'Ram_Ankle'), 'shin'), (('Ram_Boot', 'Ram_Toe', 'Ram_Sole'), 'foot'),
]
def bone_for(o):
    n = o.name.split('.')[0]                       # (Blender numbers repeated names: Ram_PackVent.001)
    for prefixes, b in RULES:
        if n.startswith(prefixes):
            if b in ('head', 'spine', 'hips'): return b   # (only limbs come in left and right)
            sd = 'R' if n.endswith('-1') else 'L' if n.endswith('1') else None
            return f'{b}.{sd}' if sd else None
    return None

def apply_all(o):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    if o.type == 'CURVE':
        o.data.resolution_u = 3; o.data.bevel_resolution = 1; bpy.ops.object.convert(target='MESH')
    else:
        for m in list(o.modifiers):
            if m.type == 'SUBSURF': m.levels = 1
            bpy.ops.object.modifier_apply(modifier=m.name)

parts = []
for o in list(bpy.data.objects):
    if o.type not in ('MESH', 'CURVE') or o == rig: continue
    apply_all(o)
    if o.name == 'Ram_Body':
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
        bpy.ops.object.parent_set(type='ARMATURE_AUTO')
        o.parent = None
        if not o.vertex_groups or all(len(g.name) == 0 for g in o.vertex_groups): print('WARNING: automatic weights failed')
    else:
        b = bone_for(o)
        if b is None: print('WARNING: no bone for', o.name); b = 'spine'
        g = o.vertex_groups.new(name=b); g.add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')
    for m in list(o.modifiers): o.modifiers.remove(m)
    parts.append(o)

# Within the triangle budget by simplifying only the large pieces (small details would vanish)
BUDGET = 40000
def tri_count(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)
big = [o for o in parts if tri_count(o) > 1500]
small = sum(tri_count(o) for o in parts if o not in big); large = sum(tri_count(o) for o in big)
if large and small + large > BUDGET:
    r = max(0.05, (BUDGET - small) / large)
    for o in big:
        d = o.modifiers.new('Decimate', 'DECIMATE'); d.ratio = r
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=d.name)

bpy.ops.object.select_all(action='DESELECT')
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
j = bpy.context.view_layer.objects.active; j.name = 'Ram_Body'; j.data.name = 'Ram_Body'

# Box-projected UVs in world space: each face takes the plane its normal faces most
bm = bmesh.new(); bm.from_mesh(j.data)
uv = bm.loops.layers.uv.verify()
for f in bm.faces:
    n = f.normal; ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
    for l in f.loops:
        p = j.matrix_world @ l.vert.co
        u, v = (p.x, p.y) if az >= ax and az >= ay else (p.y, p.z) if ax >= ay else (p.x, p.z)
        l[uv].uv = (u / UV_TILE, v / UV_TILE)
bm.to_mesh(j.data); bm.free()

j.parent = rig; m = j.modifiers.new('Armature', 'ARMATURE'); m.object = rig
print(f'Ram_Body: {tri_count(j)} triangles, {len(j.data.materials)} materials')

bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); j.select_set(True)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', use_armature_deform_only=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT.replace('.fbx', '_rigged.blend'))
print('exported', OUT, os.path.getsize(OUT) // 1024, 'KB')
