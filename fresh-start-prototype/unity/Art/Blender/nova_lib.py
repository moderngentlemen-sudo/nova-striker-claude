# Shared modelling helpers for the Nova Striker character scripts (Blender 4.2, run headless:
#   blender -b -P build_nova.py -- <out-dir>).
# Everything is built in world space at real scale (metres, Z up, the character faces -Y), with objects left at the
# origin so rigging and export see the same coordinates the scripts use.
import math, bpy, bmesh
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def mat(name, color, metal=0.0, rough=0.5, coat=0.0, emit=None, strength=0.0, sheen=0.0, sss=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*color, 1)
    b.inputs['Metallic'].default_value = metal
    b.inputs['Roughness'].default_value = rough
    b.inputs['Coat Weight'].default_value = coat
    b.inputs['Coat Roughness'].default_value = 0.08
    b.inputs['Sheen Weight'].default_value = sheen
    b.inputs['Subsurface Weight'].default_value = sss
    if emit:
        b.inputs['Emission Color'].default_value = (*emit, 1)
        b.inputs['Emission Strength'].default_value = strength
    return m

def srgb(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)

def link(obj, material=None, smooth=True):
    bpy.context.scene.collection.objects.link(obj)
    if material: obj.data.materials.append(material)
    if smooth and obj.type == 'MESH':
        for p in obj.data.polygons: p.use_smooth = True
    return obj

def mesh_obj(name, verts, faces, material=None, edges=()):
    me = bpy.data.meshes.new(name); me.from_pydata([tuple(v) for v in verts], list(edges), faces); me.update()
    return link(bpy.data.objects.new(name, me), material)

def mod(obj, kind, **kw):
    m = obj.modifiers.new(kind.lower(), kind)
    for k, v in kw.items(): setattr(m, k, v)
    return m

def spow(x, e): return math.copysign(abs(x) ** e, x)

def frame(origin, axis, side=Vector((1, 0, 0))):
    """A matrix whose local Z runs along `axis` (for patches built around a limb)."""
    z = Vector(axis).normalized(); x = (side - z * side.dot(z)).normalized(); y = z.cross(x)
    return Matrix.Translation(Vector(origin)) @ Matrix((x, y, z)).transposed().to_4x4()

def patch(name, material, center, radii, u=(-180, 180), v=(-90, 90), e=1.0, thick=0.012, res=(32, 16), M=None,
          shape=None, closed_u=None, subsurf=2):
    """A shell cut from a superellipsoid: theta (u) runs around Z from the front (-Y), phi (v) from the equator up.
    `shape(p, s, t)` may move each point (s, t in 0..1 across u and v). M places it (default: at `center`)."""
    rx, ry, rz = radii; nu, nv = res
    full = closed_u if closed_u is not None else (u[1] - u[0] >= 360)
    cols = nu if full else nu + 1
    verts = []
    for j in range(nv + 1):
        t = j / nv; phi = math.radians(v[0] + (v[1] - v[0]) * t)
        for i in range(cols):
            s = i / nu; th = math.radians(u[0] + (u[1] - u[0]) * s)
            cp = spow(math.cos(phi), e)
            p = Vector((rx * cp * spow(math.sin(th), e), -ry * cp * spow(math.cos(th), e), rz * spow(math.sin(phi), e)))
            if shape: p = shape(p, s, t)
            verts.append(p)
    faces = []
    for j in range(nv):
        for i in range(nu if full else nu):
            a = j * cols + i; b = j * cols + (i + 1) % cols
            faces.append((a, b, b + cols, a + cols))
    M = M if M is not None else Matrix.Translation(Vector(center))
    verts = [M @ p for p in verts]
    o = mesh_obj(name, verts, faces, material)
    if thick: mod(o, 'SOLIDIFY', thickness=thick, offset=1.0, use_even_offset=True)
    if subsurf: mod(o, 'SUBSURF', levels=subsurf, render_levels=subsurf)
    return o

def tube(name, material, pts, radius, radii=None, res=6, cyclic=False):
    """A smooth tube along points (a seam, a hair lock, a cable); radii taper it point by point."""
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'
    cu.bevel_depth = radius; cu.bevel_resolution = res; cu.use_fill_caps = True
    sp = cu.splines.new('NURBS'); sp.points.add(len(pts) - 1); sp.order_u = min(4, len(pts)); sp.use_endpoint_u = True
    sp.use_cyclic_u = cyclic
    for k, p in enumerate(pts):
        sp.points[k].co = (*p, 1); sp.points[k].radius = radii[k] if radii else 1
    o = bpy.data.objects.new(name, cu); link(o, material, smooth=False)
    return o

def bvh(obj):
    dg = bpy.context.evaluated_depsgraph_get(); return BVHTree.FromObject(obj, dg)

def project(tree, pts2d, plane='xz', origin_y=-1.0, direction=(0, 1, 0), lift=0.002):
    """Drops 2D points (x, z in the front view) onto a surface along `direction`; returns 3D points lifted off it."""
    out = []
    for a, b in pts2d:
        o = Vector((a, origin_y, b)) if plane == 'xz' else Vector(a)
        hit, n, _, _ = tree.ray_cast(o, Vector(direction))
        if hit is not None: out.append(hit + n * lift)
    return out

def to_mesh(obj):
    """Applies an object's modifiers (curves become meshes) so it can be joined, rigged and exported."""
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active = obj
    if obj.type == 'CURVE': bpy.ops.object.convert(target='MESH')
    else:
        for m in list(obj.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
    return obj
