# Writes a dressing kit and its layout for the game, as JSON the game reads with no import settings involved
# (Resources/NovaStriker/Env/<kit>.json; GymDressing.cs builds the meshes and places them).
# Vertices are written in three.js space (x along the route, y up, z toward the camera), the space the game's own
# geometry is made in, so pieces land and face exactly as the layout says.
# Run: blender -b <out>/gym_kit.blend -P export_kit.py -- <path/to/gym_kit.json>
import json, math, os, sys, bpy, bmesh
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gym_layout
OUT = sys.argv[sys.argv.index('--') + 1]
dg = bpy.context.evaluated_depsgraph_get()
parts = {}
for col in bpy.data.collections:
    if not col.name.startswith('Kit_'): continue
    asset = col.name[4:]
    for o in col.objects:
        if o.type not in ('MESH', 'CURVE', 'FONT'): continue
        ev = o.evaluated_get(dg); me = ev.to_mesh()
        bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
        me.calc_loop_triangles() if hasattr(me, 'calc_loop_triangles') else None
        mw = o.matrix_world; nmw = mw.to_3x3().inverted().transposed()
        mats = [m.name if m else 'Kit_Hull' for m in me.materials] or ['Kit_Hull']
        for poly in me.polygons:
            mname = mats[poly.material_index] if poly.material_index < len(mats) else mats[0]
            P = parts.setdefault((asset, mname), {'p': [], 'n': [], 'uv': [], 'i': []})
            for li in poly.loop_indices:
                lp = me.loops[li]; v = mw @ me.vertices[lp.vertex_index].co
                n = (nmw @ (lp.normal if not poly.use_smooth else me.vertices[lp.vertex_index].normal)).normalized() if poly.use_smooth else (nmw @ poly.normal).normalized()
                P['p'] += [round(v.x, 4), round(v.z, 4), round(-v.y, 4)]
                P['n'] += [round(n.x, 3), round(n.z, 3), round(-n.y, 3)]
                P['uv'] += [round(v.x + 0.5, 4), round(v.y + 0.5, 4)] if asset == 'Shadow' else [0, 0]
                P['i'].append(len(P['i']))
        ev.to_mesh_clear()
data = {'parts': [{'asset': a, 'mat': m, 'p': P['p'], 'n': P['n'], 'uv': P['uv'], 'i': P['i']} for (a, m), P in sorted(parts.items())],
        'place': [{'asset': n, 'x': x, 'y': y, 'dz': dz, 'yaw': yaw, 'sx': sx, 'sy': sy, 'sz': sz} for (n, x, y, dz, yaw, sx, sy, sz) in gym_layout.placements()]}
os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, 'w') as f: json.dump(data, f, separators=(',', ':'))
tris = sum(len(P['i']) // 3 for P in parts.values())
print(f'exported {len(data["parts"])} parts ({tris} triangles), {len(data["place"])} placements, {os.path.getsize(OUT) // 1024} KB')
