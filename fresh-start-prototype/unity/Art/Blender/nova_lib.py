# Shared modelling helpers for the Nova Striker character scripts (Blender 4.2, run headless:
#   blender -b -P build_nova.py -- <out-dir>).
# Everything is built in world space at real scale (metres, Z up, the character faces -Y), with objects left at the
# origin so rigging and export see the same coordinates the scripts use.
import math, os, bpy, bmesh
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
    z = Vector(axis).normalized()
    if abs(Vector(side).normalized().dot(z)) > 0.999: side = Vector((0, 1, 0))   # (an axis along `side` has no frame of its own)
    x = (side - z * side.dot(z)).normalized(); y = z.cross(x)
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

def box(name, material, center, size, bevel=0.01, segments=2, rot=(0, 0, 0)):
    """A box (metres, centred on `center`, Blender axes) with rounded edges from a Bevel modifier."""
    sx, sy, sz = (s / 2 for s in size)
    v = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz), (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz)]
    f = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    M = Matrix.Translation(Vector(center)) @ Matrix.Rotation(rot[2], 4, 'Z') @ Matrix.Rotation(rot[1], 4, 'Y') @ Matrix.Rotation(rot[0], 4, 'X')
    o = mesh_obj(name, [M @ Vector(p) for p in v], f, material)
    for p in o.data.polygons: p.use_smooth = False
    if bevel: mod(o, 'BEVEL', width=bevel, segments=segments, limit_method='ANGLE', harden_normals=False)
    mod(o, 'WEIGHTED_NORMAL', keep_sharp=True)
    return o

def blade(name, material, a, b, side, w0, h0, w1=None, h1=None, bevel=0.006):
    """A bevelled plate from point a to point b: `w` thick along `side`, `h` deep across it (tapering to w1, h1)."""
    a, b = Vector(a), Vector(b); d = (b - a).normalized(); x = Vector(side); x = (x - d * x.dot(d)).normalized(); y = d.cross(x)
    w1 = w0 if w1 is None else w1; h1 = h0 if h1 is None else h1
    vs = []
    for p, w, h in ((a, w0, h0), (b, w1, h1)):
        for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)): vs.append(p + x * sx * w / 2 + y * sy * h / 2)
    fs = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    o = mesh_obj(name, vs, fs, material)
    for p in o.data.polygons: p.use_smooth = False
    if bevel: mod(o, 'BEVEL', width=bevel, segments=2, limit_method='ANGLE')
    return o

def cyl(name, material, a, b, r, verts=16, r2=None, cap=True):
    """A cylinder (or cone, with r2) from point a to point b."""
    a, b = Vector(a), Vector(b); d = b - a; L = d.length
    M = frame(a, d)
    r2 = r if r2 is None else r2
    vs, fs = [], []
    for k in range(verts):
        t = 2 * math.pi * k / verts; c, s = math.cos(t), math.sin(t)
        vs += [M @ Vector((c * r, s * r, 0)), M @ Vector((c * r2, s * r2, L))]
    for k in range(verts):
        i, j = 2 * k, 2 * ((k + 1) % verts); fs.append((i, j, j + 1, i + 1))
    if cap:
        fs.append(tuple(2 * k for k in reversed(range(verts)))); fs.append(tuple(2 * k + 1 for k in range(verts)))
    o = mesh_obj(name, vs, fs, material)
    return o

def text(name, material, body, center, size, depth=0.02, align='CENTER'):
    cu = bpy.data.curves.new(name, 'FONT'); cu.body = body; cu.size = size; cu.extrude = depth; cu.align_x = align; cu.align_y = 'CENTER'
    o = bpy.data.objects.new(name, cu); o.location = Vector(center); o.rotation_euler = (math.pi / 2, 0, 0)
    link(o, material, smooth=False); return o

def surface(m, texdir, kind, tile, bump=0.35):
    """Preview only: a texture set from make_textures.py (<kind>_albedo/_rough/_height.png in texdir), box-projected
    in world space every `tile` m: the albedo multiplies the colour, the roughness scales it, the height bumps it."""
    nt = m.node_tree; bsdf = nt.nodes['Principled BSDF']
    geo = nt.nodes.new('ShaderNodeNewGeometry'); sc = nt.nodes.new('ShaderNodeVectorMath'); sc.operation = 'SCALE'; sc.inputs[3].default_value = 1 / tile
    nt.links.new(geo.outputs['Position'], sc.inputs[0])
    def img(nm):
        t = nt.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(os.path.join(texdir, f'{kind}_{nm}.png'))
        t.image.colorspace_settings.name = 'Non-Color'; t.projection = 'BOX'; t.projection_blend = 0.3
        nt.links.new(sc.outputs[0], t.inputs['Vector']); return t
    base = bsdf.inputs['Base Color'].default_value[:]
    mul = nt.nodes.new('ShaderNodeMixRGB'); mul.blend_type = 'MULTIPLY'; mul.inputs[0].default_value = 1; mul.inputs[1].default_value = base
    nt.links.new(img('albedo').outputs['Color'], mul.inputs[2]); nt.links.new(mul.outputs[0], bsdf.inputs['Base Color'])
    rm = nt.nodes.new('ShaderNodeMath'); rm.operation = 'MULTIPLY'; rm.inputs[1].default_value = bsdf.inputs['Roughness'].default_value / 0.8
    nt.links.new(img('rough').outputs['Color'], rm.inputs[0]); nt.links.new(rm.outputs[0], bsdf.inputs['Roughness'])
    bp = nt.nodes.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = bump; bp.inputs['Distance'].default_value = 0.003
    nt.links.new(img('height').outputs['Color'], bp.inputs['Height']); nt.links.new(bp.outputs['Normal'], bsdf.inputs['Normal'])
    return mul

def edge_wear(m, colour_node=None, amount=0.6):
    """Preview only: paint rubbed off the sharp edges (Cycles' pointiness), showing brighter bare metal."""
    nt = m.node_tree; bsdf = nt.nodes['Principled BSDF']
    geo = nt.nodes.new('ShaderNodeNewGeometry'); ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.53; ramp.color_ramp.elements[1].position = 0.6
    nt.links.new(geo.outputs['Pointiness'], ramp.inputs['Fac'])
    k = nt.nodes.new('ShaderNodeMath'); k.operation = 'MULTIPLY'; k.inputs[1].default_value = amount
    nt.links.new(ramp.outputs['Color'], k.inputs[0])
    mix = nt.nodes.new('ShaderNodeMixRGB'); mix.inputs[2].default_value = (0.75, 0.77, 0.8, 1)
    src = colour_node.outputs[0] if colour_node else None
    if src: nt.links.new(src, mix.inputs[1])
    else: mix.inputs[1].default_value = bsdf.inputs['Base Color'].default_value[:]
    nt.links.new(k.outputs[0], mix.inputs[0]); nt.links.new(mix.outputs[0], bsdf.inputs['Base Color'])

# ---- Nova's heroic build: broader and heavier than the first, slender model ----
NOVA_H = 1.18                                         # everything below the neck, across and front to back
def nova_bulk(p):
    """Where a point of Nova's first model lands on his heavier build: wider and deeper all over, with extra breadth
    through the chest and upper back (a V to the waist) that fades out toward the arms and the neck. The skeleton
    (rig_export_nova.py) goes through the same function, so bones and mesh still meet."""
    x, y, z = p
    w = max(0.0, min(1.0, (0.24 - abs(x)) / 0.08))     # (the torso, not the arms)
    k = NOVA_H * (1 + 0.12 * max(0.0, 1 - abs(z - 1.36) / 0.22) * w)
    if z > 1.56: k = 1 + (k - 1) * max(0.0, 1 - (z - 1.56) / 0.08)   # (back to the head's own size above the collar)
    return Vector((x * k, y * k, z))

