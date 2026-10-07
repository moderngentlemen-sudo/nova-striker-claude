# Writes a piece of RAM's gear for the game as JSON, the way export_kit.py writes the gym's kit: the Rampart
# (build_ram_shield.py) or the Breach Cannon (build_ram_cannon.py), each built without NS_TEX. One part per
# material, triangles in three.js space (the rig's shield or cannon group: x across or along the barrel, y up, z
# toward the camera), with box-projected UVs (one tile every UV_TILE) for the battle-worn texture. Models.cs reads
# Resources/NovaStriker/Models/ram_<gear>.json and hangs it on the rig's own group.
# Run: blender -b <out>/ram_shield.blend -P export_ram_gear.py -- <path/to/ram_shield.json>   (likewise ram_cannon)
import json, os, sys, bpy, bmesh
OUT = sys.argv[sys.argv.index('--') + 1]
UV_TILE = 0.9
for o in bpy.data.objects:                       # (game resolution: fewer curve and rounding segments)
    if o.type == 'CURVE': o.data.resolution_u = 4; o.data.bevel_resolution = 2
    for m in getattr(o, 'modifiers', []):
        if m.type == 'SUBSURF': m.levels = 1
        if m.type == 'BEVEL': m.segments = min(m.segments, 2)
dg = bpy.context.evaluated_depsgraph_get()
parts = {}
for o in bpy.data.objects:
    if o.type not in ('MESH', 'CURVE'): continue
    ev = o.evaluated_get(dg); me = ev.to_mesh()
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
    mw = o.matrix_world; nmw = mw.to_3x3().inverted().transposed()
    mats = [m.name for m in me.materials if m] or ['Ram_Frame']
    for poly in me.polygons:
        P = parts.setdefault(mats[min(poly.material_index, len(mats) - 1)], {'p': [], 'n': [], 'uv': [], 'i': []})
        fn = (nmw @ poly.normal).normalized(); ax, ay, az = abs(fn.x), abs(fn.y), abs(fn.z)
        for li in poly.loop_indices:
            lp = me.loops[li]; v = mw @ me.vertices[lp.vertex_index].co
            n = (nmw @ (me.vertices[lp.vertex_index].normal if poly.use_smooth else poly.normal)).normalized()
            P['p'] += [round(v.x, 4), round(v.z, 4), round(-v.y, 4)]
            P['n'] += [round(n.x, 3), round(n.z, 3), round(-n.y, 3)]
            u, w = (v.x, v.y) if az >= ax and az >= ay else (v.y, v.z) if ax >= ay else (v.x, v.z)
            P['uv'] += [round(u / UV_TILE, 3), round(w / UV_TILE, 3)]
            P['i'].append(len(P['i']))
    ev.to_mesh_clear()
data = {'parts': [{'mat': m, 'p': P['p'], 'n': P['n'], 'uv': P['uv'], 'i': P['i']} for m, P in sorted(parts.items())]}
os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, 'w') as f: json.dump(data, f, separators=(',', ':'))
print(f'exported {len(parts)} parts ({sum(len(P["i"]) // 3 for P in parts.values())} triangles), {os.path.getsize(OUT) // 1024} KB')
