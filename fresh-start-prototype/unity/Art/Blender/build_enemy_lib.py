# Shared helpers for the enemy and boss models (build_enemy_<type>.py; export_enemy.py writes them for the game).
# Each enemy is modelled as rigid parts, one set per node of its procedural rig (Game/View/EnemyRigs.cs), so the
# game's own animation still poses them. Everything here is written in the rig's three.js units and axes (x forward
# along the enemy's facing, y up, z toward the camera) and converted to Blender's (x, -z, y) as it is built.
#
# A part is a mesh object named `<node>__<what>`, parented to an empty named after its node (`body`, `torso`,
# `legsW[0].knee`, `plates[1]`, ...) that holds the rig's rest transform, so the .blend shows the assembled enemy
# while each part's own vertices stay in its node's local space (what the game needs). For a node that is a mesh
# in the rig (an eye, a core, an armour plate, a horn, a missile tube) that space is the mesh's own: its origin and
# rotation. Materials: Enemy_Plate (pale ceramic), Enemy_Joint (graphite) and Enemy_Energy (hostile magenta).
# Style: chunky readable shapes with crisp single chamfers (export_enemy.py keeps one bevel segment).
import math, os, sys, bpy, bmesh
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_lib import reset, mat, srgb, mod

C = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))        # three.js -> Blender: (x, y, z) -> (x, -z, y)
def P(x, y, z): return Vector((x, -z, y))
def rot3(r):
    rx, ry, rz = r                                     # (three.js Euler, XYZ order)
    return Matrix.Rotation(rx, 3, 'X') @ Matrix.Rotation(ry, 3, 'Y') @ Matrix.Rotation(rz, 3, 'Z')

MATS = {}
NODES = {}
CUR = ['body']
TAU = math.pi * 2

def setup():
    reset(); NODES.clear()
    MATS['p'] = mat('Enemy_Plate', srgb('#e6e9f0'), metal=0.15, rough=0.36, coat=0.5)
    MATS['j'] = mat('Enemy_Joint', srgb('#2b2f3a'), metal=0.2, rough=0.6)
    MATS['e'] = mat('Enemy_Energy', srgb('#ff2e7e'), emit=srgb('#ff2e7e'), strength=3.5)
    node('body')

def node(name, parent=None, pos=(0, 0, 0), rot=(0, 0, 0)):
    """An empty for a rig node at its rest transform (in its parent node's three.js space); new parts go under it."""
    e = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(e)
    e.empty_display_size = 0.08
    if parent: e.parent = NODES[parent]
    M = (C @ rot3(rot) @ C.transposed()).to_4x4(); M.translation = P(*pos)
    e.matrix_basis = M
    NODES[name] = e; CUR[0] = name
    return e

def at(name): CUR[0] = name

def obj(what, m, verts, faces, bevel=0.0, sharp=35, smooth=True, bev_angle=30):
    """A part from vertices in the current node's three.js space."""
    name = f'{CUR[0]}__{what}'
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(C @ Vector(v)) for v in verts], [], faces); me.update()
    bm = bmesh.new(); bm.from_mesh(me)                 # (outward normals whatever the winding it was built with)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free(); me.update()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(MATS[m])
    o.parent = NODES[CUR[0]]
    for p in me.polygons: p.use_smooth = smooth
    if smooth and sharp: me.set_sharp_from_angle(angle=math.radians(sharp))
    if bevel:
        mod(o, 'BEVEL', width=bevel, segments=1, limit_method='ANGLE', angle_limit=math.radians(bev_angle), harden_normals=False)
        mod(o, 'WEIGHTED_NORMAL', keep_sharp=True, weight=50)
    return o

def _place(vs, c, r):
    R = rot3(r); c = Vector(c)
    return [R @ Vector(v) + c for v in vs]

def box(what, m, c, size, bev=0.03, rot=(0, 0, 0), taper=(1, 1), shift=(0, 0), btaper=(1, 1)):
    """A chamfered box centred on c (w along x, h along y, d along z). taper scales the top (+y) face in x and z,
    btaper the bottom face, shift slides the top face in x and z: wedges and tapered plates from one call."""
    w, h, d = (s / 2 for s in size)
    tx, tz = taper; bx, bz = btaper; sx, sz = shift
    vs = [(-w * bx, -h, -d * bz), (w * bx, -h, -d * bz), (w * bx, -h, d * bz), (-w * bx, -h, d * bz),
          (-w * tx + sx, h, -d * tz + sz), (w * tx + sx, h, -d * tz + sz), (w * tx + sx, h, d * tz + sz), (-w * tx + sx, h, d * tz + sz)]
    fs = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    return obj(what, m, _place(vs, c, rot), fs, bevel=bev, sharp=0, smooth=True)

def prism(what, m, pts, z0, z1, bev=0.02, c=(0, 0, 0), rot=(0, 0, 0), inset=0.0):
    """A plate cut to a 2D outline (x, y, counter-clockwise) and extruded from z0 to z1 (toward the camera).
    inset > 0 shrinks the outline on the z1 face (a chamfered, tapering edge)."""
    n = len(pts)
    cx = sum(p[0] for p in pts) / n; cy = sum(p[1] for p in pts) / n
    def shrink(p, k):
        if not k: return p
        dx, dy = p[0] - cx, p[1] - cy; L = math.hypot(dx, dy) or 1
        return (p[0] - dx / L * k, p[1] - dy / L * k)
    vs = [(x, y, z0) for x, y in pts] + [(*shrink(p, inset), z1) for p in pts]
    fs = [tuple(reversed(range(n))), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n; fs.append((i, j, n + j, n + i))
    return obj(what, m, _place(vs, c, rot), fs, bevel=bev, sharp=0, smooth=True)

def lathe(what, m, prof, n=16, c=(0, 0, 0), rot=(0, 0, 0), sharp=40, bev=0.0, a0=0.0, a1=TAU):
    """Revolves a profile [(radius, y), ...] about the y axis (radius 0 closes a pole)."""
    full = abs(a1 - a0 - TAU) < 1e-6
    cols = n if full else n + 1
    vs, fs, idx = [], [], []
    for r, y in prof:
        if r <= 1e-6: idx.append([len(vs)] * cols); vs.append((0, y, 0)); continue
        row = []
        for i in range(cols):
            a = a0 + (a1 - a0) * i / n; row.append(len(vs)); vs.append((r * math.cos(a), y, -r * math.sin(a)))
        idx.append(row)
    for k in range(len(prof) - 1):
        A, B = idx[k], idx[k + 1]
        for i in range(n):
            j = (i + 1) % cols
            f = [A[i], A[j], B[j], B[i]]
            f2 = []
            for v in f:
                if v not in f2: f2.append(v)
            if len(f2) >= 3: fs.append(tuple(f2))
    if not full: pass
    if prof[0][0] > 1e-6 and full: fs.append(tuple(idx[0]))                     # (open ends are capped)
    if prof[-1][0] > 1e-6 and full: fs.append(tuple(reversed(idx[-1])))
    return obj(what, m, _place(vs, c, rot), fs, bevel=bev, sharp=sharp)

def cyl(what, m, a, b, r, r2=None, n=12, bev=0.0, sharp=40):
    """A cylinder (or a cone with r2) from point a to point b, capped."""
    a, b = Vector(a), Vector(b); d = b - a; L = d.length; z = d.normalized()
    side = Vector((0, 0, 1)) if abs(z.z) < 0.9 else Vector((1, 0, 0))
    x = (side - z * side.dot(z)).normalized(); y = z.cross(x)
    r2 = r if r2 is None else r2
    vs = []
    for k in range(n):
        t = TAU * k / n; c, s = math.cos(t), math.sin(t)
        vs += [a + (x * c + y * s) * r, a + d + (x * c + y * s) * r2]
    fs = [(2 * k, 2 * ((k + 1) % n), 2 * ((k + 1) % n) + 1, 2 * k + 1) for k in range(n)]
    fs.append(tuple(2 * k for k in reversed(range(n)))); fs.append(tuple(2 * k + 1 for k in range(n)))
    o = obj(what, m, vs, fs, bevel=bev, sharp=sharp)
    return o

def sphere(what, m, c, radii, nu=16, nv=10, v0=-90, v1=90, rot=(0, 0, 0), cap=True, sharp=50):
    """An ellipsoid (radii along x, y, z), or a band of one between latitudes v0 and v1 (capped flat)."""
    rx, ry, rz = radii if isinstance(radii, (tuple, list)) else (radii,) * 3
    prof = []
    for j in range(nv + 1):
        ph = math.radians(v0 + (v1 - v0) * j / nv)
        prof.append((math.cos(ph), math.sin(ph)))
    if v0 <= -89.99: prof[0] = (0, -1)
    if v1 >= 89.99: prof[-1] = (0, 1)
    o = lathe(what, m, [(r, y) for r, y in prof], n=nu, sharp=sharp)
    me = o.data
    R = rot3(rot); cc = Vector(c)
    for p in me.polygons: p.use_smooth = True
    for v in me.vertices:                               # (scale in three space, then place)
        t = C.transposed() @ v.co; t = Vector((t.x * rx, t.y * ry, t.z * rz))
        v.co = C @ (R @ t + cc)
    me.update(); me.set_sharp_from_angle(angle=math.radians(sharp))
    return o

def torus(what, m, c, R, r, nu=24, nv=6, rot=(0, 0, 0)):
    """A ring about the y axis (lying flat), radius R, tube radius r."""
    vs, fs = [], []
    for i in range(nu):
        a = TAU * i / nu
        for j in range(nv):
            b = TAU * j / nv; rr = R + r * math.cos(b)
            vs.append((rr * math.cos(a), r * math.sin(b), -rr * math.sin(a)))
    for i in range(nu):
        for j in range(nv):
            i2, j2 = (i + 1) % nu, (j + 1) % nv
            fs.append((i * nv + j, i2 * nv + j, i2 * nv + j2, i * nv + j2))
    return obj(what, m, _place(vs, c, rot), fs, sharp=70)

def save(out, name):
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, f'enemy_{name}.blend'))
    print('built', name, len([o for o in bpy.data.objects if o.type == 'MESH']), 'parts')

def out_dir():
    return sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/tmp'
