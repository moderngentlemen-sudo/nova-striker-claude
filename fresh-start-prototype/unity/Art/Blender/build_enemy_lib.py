# Shared helpers for the enemy and boss models (build_enemy_<type>.py; export_enemy.py writes them for the game).
# Each enemy is modelled as rigid parts, one set per node of its procedural rig (Game/View/EnemyRigs.cs), so the
# game's own animation still poses them. Everything here is written in the rig's three.js units and axes (x forward
# along the enemy's facing, y up, z toward the camera) and converted to Blender's (x, -z, y) as it is built.
#
# A part is a mesh object named `<node>__<what>`, parented to an empty named after its node (`body`, `torso`,
# `legsW[0].knee`, `plates[1]`, ...) that holds the rig's rest transform, so the .blend shows the assembled enemy
# while each part's own vertices stay in its node's local space (what the game needs). For a node that is a mesh
# in the rig (an eye, a core, an armour plate, a horn, a missile tube) that space is the mesh's own: its origin and
# rotation. Materials (each has a match in EnemyModels.Mat and a per-rig material in EnemyRigs.cs, with the same
# colour, roughness and metalness): Enemy_Plate (pearl armour under a clear coat, 'p'), Enemy_Joint (matte graphite
# bodywork, 'j'), Enemy_Metal (dark satin gunmetal frames and limbs, 'm'), Enemy_Chrome (piston rods and bright
# fittings, 'c'), Enemy_Rubber (hoses, cables, boots and tyres, 'r'), Enemy_Lens (dark tinted glass, 'l') and
# Enemy_Energy (the hostile magenta glow, 'e').
# Style: bold readable shapes with rounded two-segment bevels (export_enemy.py keeps them), curved parts with enough
# sides that their silhouettes stay round (seg), layered armour standing off a gunmetal undercut (armour), and the
# detail kit below. Budgets: small enemies about 8k triangles, heavy 14k, bosses 30k.
import math, os, sys, bpy, bmesh
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_lib import reset, mat, srgb, mod

C = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))        # three.js -> Blender: (x, y, z) -> (x, -z, y)
def P(x, y, z): return Vector((x, -z, y))
def rot3(r):
    if isinstance(r, Matrix): return r
    rx, ry, rz = r                                     # (three.js Euler, XYZ order)
    return Matrix.Rotation(rx, 3, 'X') @ Matrix.Rotation(ry, 3, 'Y') @ Matrix.Rotation(rz, 3, 'Z')

MATS = {}
NODES = {}
CUR = ['body']
TAU = math.pi * 2
Q = 2.0                                                # (at most this many times the sides the builders ask for)
def seg(n, r, L=0.04):
    """Sides for a round part of radius r: enough that no facet is much over L long (about 4 pixels at the game's
    camera distance), between the builders' own count and Q times it, so small parts stay cheap."""
    return int(max(n, min(n * Q, round(TAU * r / L))))

def setup():
    reset(); NODES.clear()
    MATS['p'] = mat('Enemy_Plate', srgb('#e6e9f0'), metal=0.15, rough=0.36, coat=0.5)
    MATS['j'] = mat('Enemy_Joint', srgb('#2b2f3a'), metal=0.2, rough=0.6)
    MATS['m'] = mat('Enemy_Metal', srgb('#464d5a'), metal=0.8, rough=0.38)
    MATS['c'] = mat('Enemy_Chrome', srgb('#dfe3ea'), metal=1.0, rough=0.14)
    MATS['r'] = mat('Enemy_Rubber', srgb('#16181c'), metal=0.0, rough=0.82)
    MATS['l'] = mat('Enemy_Lens', srgb('#1c0c18'), metal=0.3, rough=0.06, emit=srgb('#ff2e7e'), strength=0.4)
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
        mod(o, 'BEVEL', width=bevel, segments=2 if bevel >= 0.03 else 1, limit_method='ANGLE', angle_limit=math.radians(bev_angle), harden_normals=False)
        mod(o, 'WEIGHTED_NORMAL', keep_sharp=True, weight=50)
    return o

def _place(vs, c, r):
    R = rot3(r); c = Vector(c)
    return [R @ Vector(v) + c for v in vs]

def box(what, m, c, size, bev=0.03, rot=(0, 0, 0), taper=(1, 1), shift=(0, 0), btaper=(1, 1)):
    """A chamfered box centred on c (w along x, h along y, d along z). taper scales the top (+y) face in x and z,
    btaper the bottom face, shift slides the top face in x and z: wedges and tapered plates from one call."""
    if not bev and m in 'pjmc' and min(size) > 0.025: bev = min(0.008, min(size) * 0.18)   # (every solid edge catches light)
    w, h, d = (s / 2 for s in size)
    tx, tz = taper; bx, bz = btaper; sx, sz = shift
    vs = [(-w * bx, -h, -d * bz), (w * bx, -h, -d * bz), (w * bx, -h, d * bz), (-w * bx, -h, d * bz),
          (-w * tx + sx, h, -d * tz + sz), (w * tx + sx, h, -d * tz + sz), (w * tx + sx, h, d * tz + sz), (-w * tx + sx, h, d * tz + sz)]
    fs = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    return obj(what, m, _place(vs, c, rot), fs, bevel=bev, sharp=0, smooth=True)

def prism(what, m, pts, z0, z1, bev=0.02, c=(0, 0, 0), rot=(0, 0, 0), inset=0.0):
    """A plate cut to a 2D outline (x, y, counter-clockwise) and extruded from z0 to z1 (toward the camera).
    inset > 0 shrinks the outline on the z1 face (a chamfered, tapering edge)."""
    if not bev and m in 'pjmc' and abs(z1 - z0) > 0.025: bev = min(0.008, abs(z1 - z0) * 0.18)
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

def lathe(what, m, prof, n=16, c=(0, 0, 0), rot=(0, 0, 0), sharp=40, bev=0.0, a0=0.0, a1=TAU, q=True):
    """Revolves a profile [(radius, y), ...] about the y axis (radius 0 closes a pole)."""
    if q: n = seg(n, max(r for r, _ in prof))
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

def cyl(what, m, a, b, r, r2=None, n=12, bev=0.0, sharp=40, q=True):
    """A cylinder (or a cone with r2) from point a to point b, capped. q: sides from seg (else exactly n)."""
    if q: n = seg(n, max(r, r2 or 0))
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

def sphere(what, m, c, radii, nu=16, nv=10, v0=-90, v1=90, rot=(0, 0, 0), cap=True, sharp=50, q=True):
    """An ellipsoid (radii along x, y, z), or a band of one between latitudes v0 and v1 (capped flat)."""
    rx, ry, rz = radii if isinstance(radii, (tuple, list)) else (radii,) * 3
    if q: nu, nv = seg(nu, max(rx, ry, rz)), seg(nv, max(rx, ry, rz) * (v1 - v0) / 360)
    prof = []
    for j in range(nv + 1):
        ph = math.radians(v0 + (v1 - v0) * j / nv)
        prof.append((math.cos(ph), math.sin(ph)))
    if v0 <= -89.99: prof[0] = (0, -1)
    if v1 >= 89.99: prof[-1] = (0, 1)
    o = lathe(what, m, prof, n=nu, sharp=sharp, q=False)
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
    nu = seg(nu, R + r); nv = seg(nv, r)
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

# ---- The detail kit: the pieces that make a part read as built rather than blocked out: exposed actuators with
# chrome rods, rubber cable bundles in clamps, louvred vents, sensor clusters, bolt rings and armour standing off a
# gunmetal undercut. Sides follow seg, so what is too small to see stays cheap. ----
def _frame(t):
    t = Vector(t).normalized()
    side = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
    x = (side - t * side.dot(t)).normalized()
    return x, t.cross(x)

def bez(a, b, c, k=5):
    """k + 1 points on the quadratic curve from a to c, pulled toward b (a cable's sag or a hose's bend)."""
    a, b, c = Vector(a), Vector(b), Vector(c)
    return [a * (1 - t) ** 2 + b * 2 * t * (1 - t) + c * t * t for t in (i / k for i in range(k + 1))]

def tube(what, m, pts, r, n=6):
    """A pipe along a polyline (cable runs, hydraulic lines), its rings carried along so it never twists; a short
    polyline is subdivided and its corners rounded."""
    pts = [Vector(p) for p in pts]; vs, fs = [], []; x = None
    n = seg(n, r)
    if len(pts) <= 4:
        dense = [pts[0]]
        for i in range(len(pts) - 1):
            for k in (1, 2): dense.append(pts[i].lerp(pts[i + 1], k / 2))
        for _ in range(2):                             # (corner cutting; the ends stay put)
            dense = [dense[0]] + [dense[i - 1] * 0.25 + dense[i] * 0.5 + dense[i + 1] * 0.25 for i in range(1, len(dense) - 1)] + [dense[-1]]
        pts = dense
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        if x is None: x, _ = _frame(t)
        x = (x - t * x.dot(t)).normalized(); y = t.cross(x)
        for k in range(n):
            a = TAU * k / n; vs.append(p + (x * math.cos(a) + y * math.sin(a)) * r)
    for i in range(len(pts) - 1):
        for k in range(n):
            a, b = i * n + k, i * n + (k + 1) % n; fs.append((a, b, b + n, a + n))
    fs.append(tuple(reversed(range(n)))); fs.append(tuple(range((len(pts) - 1) * n, len(pts) * n)))
    return obj(what, m, vs, fs, sharp=60)

def rivets(m, pts, nrm, r=0.014, h=0.012, n=6, what='rivet'):
    """Round bolt heads standing h proud of a surface whose outward normal is nrm (sunk a little into it). Dark
    bolts ('j') are gunmetal."""
    m = 'm' if m == 'j' else m
    d = Vector(nrm).normalized()
    for p in pts: cyl(what, m, Vector(p) - d * h * 0.6, Vector(p) + d * h, r, r * 0.72, n=n, sharp=50, q=False)

def bolt_ring(c, R, k, nrm, r=0.013, h=0.01, m='m', a0=0.0):
    """k bolts round a circle of radius R on a surface facing nrm (a flange, a hub)."""
    d = Vector(nrm).normalized(); x, y = _frame(d); c = Vector(c)
    rivets(m, [c + (x * math.cos(a0 + TAU * i / k) + y * math.sin(a0 + TAU * i / k)) * R for i in range(k)], d, r=r, h=h)

def piston(what, a, b, r, k=0.55, n=8, collar=True, pin=None, line=True):
    """An exposed hydraulic ram from a to b: a gunmetal barrel with an end cap over the first k of it, a gland, a
    chrome rod, clevis eyes at both ends with chrome pins (along pin, else across the ram toward the camera) and
    (line) a rubber pressure line along the barrel."""
    a, b = Vector(a), Vector(b); d = (b - a).normalized(); m = a.lerp(b, k)
    side = Vector(pin) if pin else Vector((0, 0, 1))
    if abs(side.normalized().dot(d)) > 0.9: side = Vector((1, 0, 0)) if abs(d.x) < 0.9 else Vector((0, 1, 0))
    px = (side - d * side.dot(d)).normalized(); py = d.cross(px)
    cyl(what, 'm', a + d * r * 0.9, m, r, n=n)
    cyl(what + '_cap', 'm', a + d * r * 0.4, a + d * r * 0.95, r * 0.86, r, n=n)
    cyl(what + '_gland', 'm', m - d * r * 0.35, m + d * r * 0.3, r * (1.2 if collar else 1.08), n=n)
    cyl(what + '_rod', 'c', m, b - d * r * 0.5, r * 0.5, n=n)
    for p, rr in ((a, r * 0.75), (b, r * 0.62)):
        cyl(what + '_eye', 'm', p - px * r * 0.8, p + px * r * 0.8, rr, n=n)
        if r >= 0.02: cyl(what + '_pin', 'c', p - px * r * 1.0, p + px * r * 1.0, rr * 0.4, n=6)
    if line and r >= 0.02:
        q0 = a + d * r * 1.2 + py * r * 1.05; q1 = m - d * r * 0.5 + py * r * 1.05
        tube(what + '_line', 'r', [q0 - py * r * 0.4, q0 + d * r * 0.3, q1 - d * r * 0.3, q1 - py * r * 0.2], r * 0.22, n=6)

def cables(what, a, b, c, r, k=3, clamps=(0.3, 0.7)):
    """A bundle of k rubber cables along the curve a -> b -> c, held by gunmetal clamps at the given fractions."""
    a, b, c = Vector(a), Vector(b), Vector(c); d = (c - a).normalized(); x, y = _frame(d)
    for i in range(k):
        t = TAU * i / k + 0.4; off = (x * math.cos(t) + y * math.sin(t)) * r * (1.15 if k > 1 else 0)
        tube(what, 'r', bez(a + off, b + off, c + off, 8), r * (1 if i else 1.15))
    pts = bez(a, b, c, 40)
    for f in clamps:
        i = int(f * 40); p = pts[i]; t = (pts[min(i + 1, 40)] - pts[max(i - 1, 0)]).normalized()
        cyl(what + '_clamp', 'm', p - t * r * 1.2, p + t * r * 1.2, r * (2.5 if k > 1 else 1.5), n=10)

def grille(what, c, size, n=3, rot=(0, 0, 0), m='m', glow='e', bev=0.0, frame=True):
    """A louvred vent on a surface facing +x (before rot): a gunmetal frame round n slanted slats of size (w, -, d)
    over a glowing recess (glow None: a dark one), spaced along y."""
    R = rot3(rot); c = Vector(c); w, h, d = size
    def at_(x, y, z): return R @ Vector((x, y, z)) + c
    box(what + '_back', glow or 'j', at_(-w * 0.45, 0, 0), (w * 0.3, h * 0.96, d * 0.94), 0.0, rot=rot)
    for k in range(n):
        y = -h / 2 + h * (k + 0.5) / n
        box(what, m, at_(0, y, 0), (w * 0.8, h / n * 0.62, d * 0.96), bev or 0.004, rot=rot, taper=(0.55, 1.0))
    if frame:
        t = max(0.014, min(h, d) * 0.08)
        for s in (1, -1):
            box(what + '_frame', m, at_(w * 0.05, s * (h / 2 + t / 2), 0), (w * 1.1, t, d + 2 * t), 0.0, rot=rot)
            box(what + '_frame', m, at_(w * 0.05, 0, s * (d / 2 + t / 2)), (w * 1.1, h, t), 0.0, rot=rot)

def fins(what, m, c, size, n, rot=(0, 0, 0), bev=0.0):
    """A heat sink: n thin fins of size (w, h, -) standing on +y, in a row along z across depth d."""
    R = rot3(rot); c = Vector(c); w, h, d = size
    for k in range(n):
        z = -d / 2 + d * (k + 0.5) / n
        box(what, m, R @ Vector((0, 0, z)) + c, (w, h, d / n * 0.38), bev, rot=rot, taper=(0.8, 1.0))

def thruster(what, c, r, L, rot=(0, 0, 0), n=12, rings=True):
    """A thruster bell opening toward -y (before rot): a gunmetal bell with a rolled lip and cooling rings round a
    glowing core behind crossed vanes."""
    lathe(what, 'm', [(r * 0.72, 0.0), (r * 0.9, -L * 0.15), (r, -L * 0.3), (r, -L * 0.92), (r * 1.06, -L * 0.96), (r * 1.04, -L),
                      (r * 0.86, -L), (r * 0.64, -L * 0.5), (r * 0.58, -L * 0.42), (0.0, -L * 0.42)], n=n, c=c, rot=rot, sharp=35)
    R = rot3(rot); c = Vector(c)
    for f in (0.48, 0.66) if rings else ():
        lathe(what + '_ring', 'm', [(r * 0.99, -L * f + L * 0.05), (r * 1.08, -L * f + L * 0.025), (r * 1.08, -L * f - L * 0.025), (r * 0.99, -L * f - L * 0.05)],
              n=n, c=c, rot=rot, sharp=30)
    lathe(what + '_core', 'e', [(0.0, -L * 0.72), (r * 0.36, -L * 0.62), (r * 0.56, -L * 0.44)], n=n, c=c, rot=rot, sharp=35)
    if r >= 0.05:
        for k in range(2):
            box(what + '_vane', 'm', R @ Vector((0, -L * 0.8, 0)) + c, (r * 1.7, L * 0.06, r * 0.1), 0.0, rot=R @ rot3((0, k * math.pi / 2, 0)))

def sensor(what, c, r, rot=(0, 0, 0), n=10, d=0.6):
    """A sensor facing +x (before rot): a gunmetal collar with a chrome bezel round a tinted glass dome with a
    glowing pupil."""
    R = rot3(rot); c = Vector(c)
    cyl(what, 'm', c - R @ Vector((r * d, 0, 0)), c + R @ Vector((r * 0.05, 0, 0)), r * 1.4, r * 1.3, n=n)
    cyl(what + '_bezel', 'c', c, c + R @ Vector((r * 0.2, 0, 0)), r * 1.18, r * 1.1, n=n)
    sphere(what + '_lens', 'l' if r >= 0.02 else 'e', c + R @ Vector((r * 0.18, 0, 0)), (r * 0.55, r, r), nu=n, nv=5, rot=rot)   # (a pip too small for glass glows)
    if r >= 0.02: sphere(what + '_pupil', 'e', c + R @ Vector((r * 0.62, 0, 0)), (r * 0.16, r * 0.4, r * 0.4), nu=n, nv=4, rot=rot)

def lenses(what, c, specs, rot=(0, 0, 0), plate=None):
    """A sensor cluster facing +x (before rot): sensors at (y, z, r) on a gunmetal housing of size plate."""
    R = rot3(rot); c = Vector(c)
    if plate: box(what + '_housing', 'm', R @ Vector((-plate[0] / 2, 0, 0)) + c, plate, 0.01, rot=rot, taper=(0.9, 0.9))
    for y, z, r in specs: sensor(what, R @ Vector((0, y, z)) + c, r, rot=rot, n=12, d=0.5)

def armour(what, c, size, out=(1, 0, 0), bev=0.03, lip=0.02, rot=(0, 0, 0), m='p', **kw):
    """An armour plate with real thickness standing off the body: the pale box itself and behind it (away from
    out, in the node's space) a gunmetal undercut a little smaller, so a dark step shows under its edges."""
    o = box(what, m, c, size, bev, rot=rot, **kw)
    R = rot3(rot); n = Vector(out).normalized(); ln = R.inverted() @ n
    ax = max(range(3), key=lambda i: abs(ln[i]))
    sz = [s * 0.9 for s in size]; sz[ax] = size[ax] * 0.5 + lip
    cc = Vector(c) - n * (size[ax] * 0.25 + lip * 0.5)
    kw2 = {k: v for k, v in kw.items() if k in ('taper', 'btaper')}
    box(what + '_under', 'm', cc, tuple(sz), min(bev, 0.012), rot=rot, **kw2)
    return o

def joint(what, c, r, w, axis=(0, 0, 1), m='m', bolts=6, glow=False, both=False):
    """A hinge drum (a mechanical joint seen side on from the game's camera): a gunmetal drum of radius r and width
    w across axis, a stepped graphite cap, a chrome hub and a bolt ring; glow puts a magenta ring in the cap. The
    cap goes on the end facing the camera (+z: the rig mirrors enemies in x, so +z always faces it) unless both."""
    c = Vector(c); d = Vector(axis).normalized()
    cyl(what, m, c - d * w / 2, c + d * w / 2, r, n=14, bev=min(0.016, r * 0.12))
    for s in (1, -1):
        if not both and d.z * s < -0.2: continue
        e = c + d * s * w / 2
        cyl(what + '_cap', 'j', e, e + d * s * r * 0.12, r * 0.78, r * 0.7, n=14)
        cyl(what + '_hub', 'c', e + d * s * r * 0.1, e + d * s * r * 0.22, r * 0.32, r * 0.26, n=10)
        if glow: torus(what + '_glow', 'e', e + d * s * r * 0.1, r * 0.5, r * 0.05, nu=14, nv=4,
                       rot=Matrix.Rotation(0, 3, 'X') if abs(d.y) > 0.9 else (math.pi / 2, 0, 0) if abs(d.z) > 0.9 else (0, 0, math.pi / 2))
        if bolts and r >= 0.05: bolt_ring(e + d * s * r * 0.1, r * 0.55, bolts, d * s, r=r * 0.08, h=r * 0.06)

def panel(what, c, size, rot=(0, 0, 0), m='p', groove='j', g=0.012):
    """A raised panel facing +x (before rot), size (proud, h, d), sitting in a dark groove a little larger: the
    panel lines that break a big flat surface up."""
    R = rot3(rot); c = Vector(c); w, h, d = size
    box(what + '_groove', groove, R @ Vector((-w * 0.3, 0, 0)) + c, (w * 0.6, h + 2 * g, d + 2 * g), 0.0, rot=rot)
    box(what, m, c, (w, h, d), min(0.012, w * 0.6), rot=rot, taper=(0.92, 0.95))

def patch(what, m, c, radii, s0, s1, w0, w1, ns=8, nw=1, k=1.012, t=0.02, ring=False):
    """A raised strip or ring lying on an ellipsoid (centre c, radii along x, y, z): seam rings, glowing stripes and
    armour skins that follow a curved hull. s runs along x (-1..1 from tail to nose) and w round it (0 at +z, pi/2 at
    +y); the strip stands k - 1 proud of the surface and reaches t below it. ring: w spans the whole way round."""
    rx, ry, rz = radii; c = Vector(c); vs = []
    ns = seg(ns, max(radii)) if ns > 2 else ns; nw = seg(nw, max(radii)) if nw > 2 else nw
    def pt(s, w, kk):
        q = math.sqrt(max(0.0, 1 - s * s))
        return c + Vector((rx * s * kk, ry * q * math.sin(w) * kk, rz * q * math.cos(w) * kk))
    cols = nw if ring else nw + 1
    for kk in (k, 1 - t / max(rx, ry, rz)):
        for i in range(ns + 1):
            for j in range(cols): vs.append(pt(s0 + (s1 - s0) * i / ns, w0 + (w1 - w0) * j / nw, kk))
    N = (ns + 1) * cols; fs = []
    def at_(L, i, j): return L * N + i * cols + j % cols
    for L in (0, 1):
        for i in range(ns):
            for j in range(nw):
                f = (at_(L, i, j), at_(L, i + 1, j), at_(L, i + 1, j + 1), at_(L, i, j + 1))
                fs.append(f if L == 0 else tuple(reversed(f)))
    for j in range(nw):                                 # (walls closing the ends along s ...)
        fs.append((at_(0, 0, j), at_(0, 0, j + 1), at_(1, 0, j + 1), at_(1, 0, j)))
        fs.append((at_(0, ns, j), at_(1, ns, j), at_(1, ns, j + 1), at_(0, ns, j + 1)))
    if not ring:                                        # (... and the sides along w)
        for i in range(ns):
            fs.append((at_(0, i, 0), at_(1, i, 0), at_(1, i + 1, 0), at_(0, i + 1, 0)))
            fs.append((at_(0, i, nw), at_(0, i + 1, nw), at_(1, i + 1, nw), at_(1, i, nw)))
    return obj(what, m, vs, fs, sharp=40)

def save(out, name):
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, f'enemy_{name}.blend'))
    print('built', name, len([o for o in bpy.data.objects if o.type == 'MESH']), 'parts')

def out_dir():
    return sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/tmp'
