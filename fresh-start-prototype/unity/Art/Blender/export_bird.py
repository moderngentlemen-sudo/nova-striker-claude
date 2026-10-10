# Writes the gull (build_bird.py) for the game as one mesh in JSON (Resources/NovaStriker/Models/bird.json): three.js
# space (x, z, -y from Blender, so its +Y forward becomes three.js -z and Unity +z), a vertex colour per material,
# and in uv2 each vertex's flap weight (0 on the body, 1 at the wingtip) and side (-1 left wing, +1 right, 0 body),
# which Bird.shader uses to beat the wings.
# Run: blender -b <out>/bird.blend -P export_bird.py -- <path/to/bird.json>
import json, os, sys, bpy, bmesh
OUT = sys.argv[sys.argv.index('--') + 1]
COL = {'Bird_White': (0.95, 0.96, 0.97), 'Bird_Grey': (0.62, 0.67, 0.72), 'Bird_Dark': (0.12, 0.13, 0.15), 'Bird_Beak': (0.95, 0.72, 0.25)}
dg = bpy.context.evaluated_depsgraph_get()
P, N, C, U2, I = [], [], [], [], []
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    me = o.evaluated_get(dg).to_mesh()
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
    wing = o.name.startswith('Bird_Wing')
    for poly in me.polygons:
        col = COL.get(me.materials[poly.material_index].name if me.materials else 'Bird_White', (1, 1, 1))
        for li in poly.loop_indices:
            v = o.matrix_world @ me.vertices[me.loops[li].vertex_index].co
            n = (o.matrix_world.to_3x3() @ poly.normal).normalized()
            P += [round(v.x, 4), round(v.z, 4), round(-v.y, 4)]; N += [round(n.x, 3), round(n.z, 3), round(-n.y, 3)]
            C += list(col)
            w = max(0.0, min(1.0, (abs(v.x) - 0.06) / 0.59)) if wing else 0.0
            U2 += [round(w, 3), (1 if v.x > 0 else -1) if wing else 0]
            I.append(len(I))
    o.evaluated_get(dg).to_mesh_clear()
os.makedirs(os.path.dirname(OUT), exist_ok=True)
json.dump({'p': P, 'n': N, 'c': C, 'uv2': U2, 'i': I}, open(OUT, 'w'), separators=(',', ':'))
print('exported', len(I) // 3, 'triangles')
