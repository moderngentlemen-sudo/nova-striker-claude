# Writes an enemy or boss model (build_enemy_<type>.py) for the game, the way export_ram_gear.py writes RAM's gear:
# modifiers evaluated at game resolution (one bevel segment, no subdivision), triangulated, one part per rig node
# and material, vertices in three.js space relative to that node (three = (bx, bz, -by)), the shading's own split
# normals, and box-projected UVs. A part's node is its object's name before `__` (body, torso, legsW[0].knee,
# plates[1], ...); EnemyModels.cs hangs each part on that node of the rig, or swaps a state mesh's geometry.
# Run: blender -b <out>/enemy_<type>.blend -P export_enemy.py -- <path/to/enemy_<type>.json>
import json, os, sys, bpy
OUT = sys.argv[sys.argv.index('--') + 1]
UV_TILE = 0.9
for o in bpy.data.objects:
    if o.type == 'CURVE': o.data.resolution_u = 3; o.data.bevel_resolution = 1
    for m in list(getattr(o, 'modifiers', [])):
        if m.type == 'SUBSURF': o.modifiers.remove(m)      # (removed, not applied: a zero-level one errors on apply)
        elif m.type == 'BEVEL': m.segments = 1
dg = bpy.context.evaluated_depsgraph_get()
parts = {}
for o in sorted(bpy.data.objects, key=lambda o: o.name):
    if o.type not in ('MESH', 'CURVE') or '__' not in o.name: continue
    node = o.name.split('__')[0]
    ev = o.evaluated_get(dg); me = ev.to_mesh()
    me.calc_loop_triangles()
    mw = o.matrix_parent_inverse @ o.matrix_basis if o.parent else o.matrix_world   # (in its node's own space)
    nmw = mw.to_3x3().inverted().transposed()
    mats = [m.name for m in me.materials if m] or ['Enemy_Plate']
    cn = me.corner_normals
    for tri in me.loop_triangles:
        poly = me.polygons[tri.polygon_index]
        key = (node, mats[min(poly.material_index, len(mats) - 1)])
        P = parts.setdefault(key, {'p': [], 'n': [], 'uv': [], 'i': [], 'seen': {}})
        fn = (nmw @ poly.normal).normalized(); ax, ay, az = abs(fn.x), abs(fn.y), abs(fn.z)
        for li in tri.loops:
            v = mw @ me.vertices[me.loops[li].vertex_index].co
            n = (nmw @ cn[li].vector).normalized()
            u, w = (v.x, v.y) if az >= ax and az >= ay else (v.y, v.z) if ax >= ay else (v.x, v.z)
            vert = (round(v.x, 4), round(v.z, 4), round(-v.y, 4), round(n.x, 3), round(n.z, 3), round(-n.y, 3), round(u / UV_TILE, 3), round(w / UV_TILE, 3))
            k = P['seen'].get(vert)
            if k is None:
                k = P['seen'][vert] = len(P['p']) // 3
                P['p'] += vert[0:3]; P['n'] += vert[3:6]; P['uv'] += vert[6:8]
            P['i'].append(k)
    ev.to_mesh_clear()
data = {'parts': [{'node': nd, 'mat': m, 'p': P['p'], 'n': P['n'], 'uv': P['uv'], 'i': P['i']} for (nd, m), P in sorted(parts.items())]}
os.makedirs(os.path.dirname(os.path.abspath(OUT)), exist_ok=True)
with open(OUT, 'w') as f: json.dump(data, f, separators=(',', ':'))
tris = sum(len(P['i']) // 3 for P in parts.values())
print(f'exported {len(parts)} parts ({tris} triangles), {os.path.getsize(OUT) // 1024} KB')
for (nd, m), P in sorted(parts.items()): print(f'  {nd:16s} {m:13s} {len(P["i"]) // 3}')
