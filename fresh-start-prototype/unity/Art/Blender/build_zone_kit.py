# The dressing kits of the zones after the Movement Gym (Concourse Lock, Storm Spire Climb, Skyline Relay, Helix
# Foundry, Undercity Descent), in the gym kit's style (build_gym_kit.py): bold simple shapes, crisp single chamfers,
# light wear from the texture sets. Each piece is built at the origin in its own collection (Kit_<Name>); X is along
# the route, Z up, and its front faces -Y (toward the camera). Units are metres. zone_layout.py places the pieces and
# export_kit.py writes them out for the game (Resources/NovaStriker/Env/<zone>_kit.json).
# Every zone has the same facade and floor modules (FaceTop, FaceA/B/C, Flat*), themed, plus props of its own.
# Pieces whose name starts with "Flat" lie on a surface and cast no shadow (ZoneDressing.cs).
# Run: blender -b -P build_zone_kit.py -- <zone> <out-dir>   (writes <zone>_kit.blend)
import math, os, sys, bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_lib import *

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ZONE = args[0] if args else 'arena'
OUT = args[1] if len(args) > 1 else '/tmp'
reset()
R = math.radians

# ---- Materials. Names are shared with the game (ZoneDressing.MatFor); the gym's Kit_* keep their meaning ----
HULL = mat('Kit_Hull', srgb('#e9eef3'), metal=0.1, rough=0.42, coat=0.3)
HULLB = mat('Kit_HullB', srgb('#dce3eb'), metal=0.1, rough=0.46, coat=0.25)
STENCIL = mat('Kit_Stencil', srgb('#5d6b7d'), rough=0.6)
SHADOW = mat('Kit_Shadow', (0, 0, 0), rough=1)
DARK = mat('Kit_HullDark', srgb('#3d4f66'), metal=0.2, rough=0.5)
NAVY = mat('Kit_Navy', srgb('#2b4f7e'), metal=0.15, rough=0.45, coat=0.2)
METAL = mat('Kit_Metal', srgb('#9aa6b2'), metal=0.85, rough=0.3)
BLACK = mat('Kit_Black', srgb('#14181f'), rough=0.6)
LIGHT = mat('Kit_Light', srgb('#8fecff'), emit=srgb('#4fd6ff'), strength=14)
HAZ = mat('Kit_Hazard', srgb('#f2c230'), rough=0.5)
GLASS = mat('Kit_Glass', srgb('#cfeeff'), rough=0.05)
GLASS.node_tree.nodes['Principled BSDF'].inputs['Transmission Weight'].default_value = 0.9
PADO = mat('Kit_PadOrange', srgb('#f08a3c'), rough=0.75, sheen=0.3)
HOLO = mat('Kit_Holo', srgb('#7fe3ff'), emit=srgb('#5fd8ff'), strength=4)
SIGN = mat('Kit_Sign', srgb('#ffffff'), emit=srgb('#eaf8ff'), strength=5)
SEAM = mat('Kit_Seam', srgb('#9aa6b4'), rough=0.6)
GRIP = mat('Kit_Grip', srgb('#7d8794'), rough=0.95)
# new for these zones
STEEL = mat('Kit_Steel', srgb('#4a5563'), metal=0.7, rough=0.22)          # wet dark steel (Storm Spire)
STEELL = mat('Kit_SteelLight', srgb('#7d8a99'), metal=0.7, rough=0.25)
STEELB = mat('Kit_SteelB', srgb('#55606e'), metal=0.7, rough=0.24)        # (a second batch, so panels vary)
GRATE = mat('Kit_Grate', srgb('#2c333d'), metal=0.5, rough=0.45)
COPPER = mat('Kit_Copper', srgb('#b86b3c'), metal=0.9, rough=0.35)
CABLE = mat('Kit_Cable', srgb('#1a1c20'), rough=0.7)
WARN = mat('Kit_Warn', srgb('#ff5a3a'), emit=srgb('#ff3a1a'), strength=10)     # warning lamps (they blink in the game)
AMBER = mat('Kit_Amber', srgb('#ffc060'), emit=srgb('#ffa030'), strength=8)
WINDOW = mat('Kit_Window', srgb('#1d2a3d'), metal=0.3, rough=0.08)
WINLIT = mat('Kit_WinLit', srgb('#ffd9a0'), emit=srgb('#ffc880'), strength=4)
GUN = mat('Kit_Gunmetal', srgb('#3a4049'), metal=0.5, rough=0.45)
GUNB = mat('Kit_GunmetalB', srgb('#474d57'), metal=0.5, rough=0.5)         # (a second batch, so panels vary)
RUST = mat('Kit_Rust', srgb('#7a4a32'), metal=0.3, rough=0.75)
FURN = mat('Kit_Furnace', srgb('#ff8a2a'), emit=srgb('#ff6a10'), strength=12)  # glowing vents (they breathe)
MOLTEN = mat('Kit_Molten', srgb('#ffc060'), emit=srgb('#ff9a30'), strength=16)
CONC = mat('Kit_Concrete', srgb('#8d8f99'), rough=0.85)
PLASTER = mat('Kit_Plaster', srgb('#7a7682'), rough=0.8)
PAVE = mat('Kit_Pave', srgb('#4d525b'), rough=0.6)
WET = mat('Kit_Wet', srgb('#5a6272'), metal=0.6, rough=0.04)                 # puddles (they mirror the neon)
NEON = mat('Kit_Neon', srgb('#ff7ad9'), emit=srgb('#ff4fc8'), strength=12)
NEONB = mat('Kit_NeonB', srgb('#7fe9ff'), emit=srgb('#3fd8ff'), strength=12)
BIN = mat('Kit_Dumpster', srgb('#2f6b4f'), metal=0.3, rough=0.5)
WOOD = mat('Kit_Wood', srgb('#a87444'), rough=0.8)

def kit(name):
    col = bpy.data.collections.new('Kit_' + name); bpy.context.scene.collection.children.link(col)
    return col, set(bpy.context.scene.collection.objects)
def done(col, before):
    for o in list(bpy.context.scene.collection.objects):
        if o not in before:
            bpy.context.scene.collection.objects.unlink(o); col.objects.link(o)

def B(name, m, c, s, bev=0.0, rot=(0, 0, 0)):
    """A box with one crisp chamfer (or none)."""
    return box(name, m, c, s, bev, 1, rot)

def flat_poly(name, m, pts, z=0.004):
    o = mesh_obj(name, [(x, y, z) for x, y in pts], [tuple(range(len(pts)))], m)
    for p in o.data.polygons: p.use_smooth = False
    return o

def stripes(name, x0, x1, z0, z1, y, w=0.18, plane='xz'):
    """Diagonal yellow and black bands filling a rectangle on a face (xz: a front face at depth y; xy: a floor at height y)."""
    vs, fy, fb = [], [], []
    h = z1 - z0; n = int((x1 - x0 + h) / w) + 2
    for k in range(n):
        a = x0 - h + k * w
        quad = [(max(x0, min(x1, a)), z0), (max(x0, min(x1, a + w)), z0), (max(x0, min(x1, a + w + h)), z1), (max(x0, min(x1, a + h)), z1)]
        if quad[0][0] == quad[1][0] and quad[3][0] == quad[2][0]: continue
        base = len(vs)
        for (u, v) in quad: vs.append((u, y, v) if plane == 'xz' else (u, v, y))
        (fy if k % 2 == 0 else fb).append((base, base + 1, base + 2, base + 3))
    def make(nm, faces, m):
        if not faces: return
        used = sorted({i for f in faces for i in f}); remap = {i: j for j, i in enumerate(used)}
        o = mesh_obj(nm, [vs[i] for i in used], [tuple(remap[i] for i in f) for f in faces], m)
        for p in o.data.polygons: p.use_smooth = False
    make(name + 'Y', fy, HAZ); make(name + 'B', fb, BLACK)

def ring(name, m, c, r, t, n=8, axis='y'):
    """A wheel or hoop of n short bars (a valve wheel, a hoop), cheaper than a swept tube."""
    for k in range(n):
        a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        def P(a):
            u, v = math.cos(a) * r, math.sin(a) * r
            return Vector(c) + (Vector((u, 0, v)) if axis == 'y' else Vector((u, v, 0)) if axis == 'z' else Vector((0, u, v)))
        cyl(name, m, P(a0), P(a1), t, 6)

def sag(name, m, a, b, drop, r, n=6):
    """A cable hanging between two points."""
    a, b = Vector(a), Vector(b); pts = []
    for k in range(n + 1):
        t = k / n; p = a.lerp(b, t); p.z -= drop * 4 * t * (1 - t); pts.append(p)
    for k in range(n): cyl(name, m, pts[k], pts[k + 1], r, 6)

def shadow():
    c, b = kit('Shadow')              # a soft contact shadow, 1 x 1 m, scaled to what it sits under
    mesh_obj('shadow', [(-0.5, -0.5, 0.004), (0.5, -0.5, 0.004), (0.5, 0.5, 0.004), (-0.5, 0.5, 0.004)], [(0, 1, 2, 3)], SHADOW)
    done(c, b)

# ---- The facade modules (every zone): FaceTop is the top band under the deck's cap, origin at its top front edge;
# FaceA/B/C are 2 x 2 m middle sections, origin at the top centre, stretched in height by the layout ----
def facade(P1, P2, DK, NOSE, STRIP, top_extra=None, mids=None):
    c, b = kit('FaceTop')
    B('nose', NOSE, (0, -0.03, -0.06), (2.0, 0.12, 0.12), 0.03)
    B('recess', BLACK, (0, 0.0, -0.2), (2.0, 0.04, 0.09))
    B('strip', STRIP, (0, -0.012, -0.2), (1.98, 0.02, 0.045))
    for s in (-1, 1): B('panel', P1 if s < 0 else P2, (s * 0.495, -0.02, -0.72), (0.97, 0.05, 0.86), 0.02)
    B('band', DK, (0, 0.0, -1.18), (2.0, 0.04, 0.05))
    if top_extra: top_extra()
    done(c, b)
    for variant, fn in zip('ABC', mids):
        c, b = kit('Face' + variant)
        for s in (-1, 1): B('panel', P2 if (s > 0) != (variant == 'C') else P1, (s * 0.495, -0.02, -1.0), (0.97, 0.05, 1.94), 0.02)
        B('seam', DK, (0, 0.005, -1.0), (2.0, 0.03, 2.0))
        fn()
        done(c, b)

def louvre(x, z, w=0.7, n=6, frame=DARK, slat=METAL):
    B('ventframe', frame, (x, -0.05, z), (w, 0.03, n * 0.08 + 0.02), 0.01)
    for k in range(n): B('slat', slat, (x, -0.07, z + (n - 1) * 0.04 - k * 0.08), (w - 0.06, 0.02, 0.025), 0.004, rot=(R(30), 0, 0))

def bolts(x, z, m=METAL, dx=0.38, dz=0.85):
    for sx in (-1, 1):
        for sz in (-1, 1): cyl('bolt', m, (x + sx * dx, -0.045, z + sz * dz), (x + sx * dx, -0.06, z + sz * dz), 0.02, 6)

def floor_base(seams=SEAM, edge=None, xs=True):
    """Deck plating, 2 m along the route by the deck's 4.4 m depth: seams (off the lane guides at +-0.7 m)."""
    for y in (-1.1, 1.1): B('seam', seams, (0, y, 0.002), (2.0, 0.025, 0.004))
    if xs: B('seamx', seams, (-0.995, 0, 0.002), (0.025, 4.4, 0.004))
    if edge: B('edge', edge, (0, -2.05, 0.003), (2.0, 0.05, 0.006))

# =====================================================================================================================
def build_arena():
    """Concourse Lock: a transit hall. Glass balustrades, holo advertising columns, lock-gate frames with warning
    lights, floor lane markings, mezzanine supports for the catwalks, and the hall's sign."""
    def ad():
        B('adframe', DARK, (0.495, -0.05, -0.95), (0.8, 0.03, 1.3), 0.015)
        B('adscreen', HOLO, (0.495, -0.067, -0.95), (0.7, 0.006, 1.18))
        text('adtext', SIGN, 'LOCK 7', (0.495, -0.072, -0.5), 0.11, depth=0.002)
        B('stripe', NAVY, (-0.495, -0.047, -1.75), (0.97, 0.004, 0.12))
    def vent():
        louvre(-0.495, -0.6); B('stripe', NAVY, (0.495, -0.047, -1.75), (0.97, 0.004, 0.12))
    def rib():
        B('rib', METAL, (0, -0.07, -1.0), (0.16, 0.08, 2.0), 0.02)
        B('ribcore', LIGHT, (0, -0.112, -1.0), (0.03, 0.01, 1.8))
        text('gate', STENCIL, 'C-4', (-0.495, -0.047, -0.3), 0.14, depth=0.002)
    def top():
        B('navy', NAVY, (0, -0.03, -1.12), (2.0, 0.02, 0.1))
    facade(HULL, HULLB, DARK, METAL, LIGHT, top, (ad, vent, rib))

    # floor: seams, a yellow platform edge toward the camera, a white line at the back
    c, b = kit('FlatTransit'); floor_base()
    B('edge', HAZ, (0, -1.92, 0.003), (2.0, 0.22, 0.006))
    for k in range(5): B('dot', GRIP, (-0.8 + k * 0.4, -1.92, 0.0065), (0.08, 0.08, 0.002))
    B('back', STENCIL, (0.3, 1.5, 0.003), (1.2, 0.07, 0.006))
    done(c, b)
    c, b = kit('FlatArrow')            # a big direction arrow on the deck
    for s in (-1, 1): B('chev', STENCIL, (0.18, s * 0.32, 0.004), (0.9, 0.18, 0.008), 0, rot=(0, 0, -s * R(42)))
    B('shaft', STENCIL, (-0.55, 0, 0.004), (0.9, 0.18, 0.008))
    done(c, b)
    c, b = kit('FlatThreshold'); stripes('th', -0.55, 0.55, -2.2, 2.2, 0.004, 0.25, plane='xy'); done(c, b)

    # glass balustrade, 2 m, hung on the deck's back face (origin at the back top edge; it stands behind the deck)
    c, b = kit('Balustrade')
    B('channel', DARK, (0, 0.12, -0.05), (2.0, 0.18, 0.3), 0.02)
    B('glass', GLASS, (0, 0.12, 0.6), (1.96, 0.02, 1.0))
    for s in (-1, 1): B('clamp', METAL, (s * 0.97, 0.12, 0.6), (0.04, 0.06, 1.1), 0.01)
    B('rail', HULL, (0, 0.12, 1.12), (2.0, 0.09, 0.06), 0.02)
    B('glow', LIGHT, (0, 0.07, 1.085), (1.96, 0.01, 0.015))
    done(c, b)

    # holo advertising column
    c, b = kit('HoloColumn')
    cyl('base', DARK, (0, 0, 0), (0, 0, 0.45), 0.6, 16)
    cyl('collar', METAL, (0, 0, 0.45), (0, 0, 0.55), 0.52, 16)
    cyl('core', METAL, (0, 0, 0.55), (0, 0, 4.6), 0.12, 8)
    cyl('holo', HOLO, (0, 0, 0.6), (0, 0, 4.5), 0.46, 16, cap=False)
    for z in (1.6, 2.8, 3.9): cyl('band', LIGHT, (0, 0, z), (0, 0, z + 0.05), 0.48, 16)
    for a in (0, 120, 240):
        B('ad', SIGN, (math.sin(R(a)) * 0.3, -math.cos(R(a)) * 0.3, 2.2), (0.36, 0.01, 0.9), rot=(0, 0, R(a)))
    cyl('cap', NAVY, (0, 0, 4.55), (0, 0, 4.85), 0.6, 16)
    cyl('lip', LIGHT, (0, 0, 4.85), (0, 0, 4.9), 0.5, 16)
    done(c, b)

    # mezzanine support: a base, a 1 m shaft the layout stretches, a head whose arm reaches under the catwalk
    c, b = kit('MezzBase')
    B('plinth', DARK, (0, 0, 0.15), (0.8, 0.8, 0.3), 0.03); B('collar', METAL, (0, 0, 0.36), (0.55, 0.55, 0.12), 0.02)
    done(c, b)
    c, b = kit('MezzShaft')
    B('shaft', HULL, (0, 0, 0.5), (0.42, 0.42, 1.0)); B('light', LIGHT, (0, -0.215, 0.5), (0.05, 0.01, 1.0))
    B('back', HULLB, (0, 0.215, 0.5), (0.3, 0.01, 1.0))
    done(c, b)
    c, b = kit('MezzHead')              # origin at the arm's top where it meets the column; the arm runs toward -Y
    B('cap', NAVY, (0, 0, -0.35), (0.56, 0.56, 0.5), 0.03)
    B('arm', HULL, (0, -0.65, -0.12), (0.3, 1.3, 0.24), 0.03)
    blade('brace', METAL, (0, -0.1, -0.9), (0, -1.1, -0.24), (1, 0, 0), 0.1, 0.12)
    B('lamp', LIGHT, (0, -1.0, -0.25), (0.2, 0.2, 0.02))
    done(c, b)

    # lock-gate frame round a gate (origin on the deck at the gate's centre): a portal behind the gate's 3.2 m field
    # (two pillars and a striped header with warning lamps) and a sill on the deck's front face, below the deck, so
    # nothing stands between the gate and the camera
    c, b = kit('LockFrame')
    for s in (-1, 1):
        B('pillar', HULL, (s * 1.05, 2.45, 3.0), (0.55, 0.45, 8.4), 0.04)
        B('band', NAVY, (s * 1.05, 2.215, 3.0), (0.57, 0.02, 0.5))
        B('glow', LIGHT, (s * 0.765, 2.3, 3.4), (0.02, 0.1, 6.8))
        for z in (1.2, 3.4, 5.6): B('lamp', WARN, (s * 1.05, 2.215, z), (0.24, 0.03, 0.2), 0.02)
    B('header', HULLB, (0, 2.45, 7.55), (2.7, 0.6, 0.7), 0.04)
    stripes('hs', -1.3, 1.3, 7.25, 7.85, 2.145, 0.16)
    B('beam', METAL, (0, 2.1, 7.1), (1.6, 0.1, 0.1))
    for x in (-0.4, 0.0, 0.4): B('lamp', WARN, (x, 2.13, 7.02), (0.16, 0.06, 0.06), 0.01)
    B('sill', DARK, (0, -2.33, -0.32), (2.4, 0.26, 0.6), 0.03)
    stripes('ss', -1.2, 1.2, -0.58, -0.08, -2.465, 0.14)
    for s in (-1, 1): B('lamp', WARN, (s * 1.02, -2.47, -0.33), (0.18, 0.03, 0.18), 0.02)
    done(c, b)

    c, b = kit('TransitBench')
    B('seat', NAVY, (0, 0, 0.45), (1.8, 0.45, 0.08), 0.02)
    B('back', NAVY, (0, 0.2, 0.75), (1.8, 0.05, 0.4), 0.02)
    for s in (-1, 1): B('leg', METAL, (s * 0.75, 0, 0.22), (0.06, 0.4, 0.44), 0.01)
    done(c, b)

    c, b = kit('HallSign')
    B('board', NAVY, (0, 0.05, 0), (13.6, 0.2, 1.8), 0.05)
    for z in (-0.88, 0.88): B('edge', LIGHT, (0, -0.06, z), (13.4, 0.02, 0.05))
    text('words', SIGN, 'CONCOURSE LOCK', (0, -0.06, 0.0), 1.05, depth=0.03)
    for s in (-1, 1): cyl('pylon', HULL, (s * 4.8, 0.15, -10.5), (s * 4.8, 0.15, -0.9), 0.18, 12)
    done(c, b)
    shadow()

# =====================================================================================================================
def build_tower():
    """Storm Spire Climb: a weather station on the spire. Wet steel grating, lightning rods, cable trays,
    anemometers, storm shutters, ledge brackets under the catwalks, an instrument screen."""
    def ribs():
        for z in (-0.45, -1.0, -1.55): B('rib', STEELL, (0, -0.06, z), (2.0, 0.05, 0.07), 0.015)
        bolts(-0.495, -1.0, STEELL, 0.4, 0.88); bolts(0.495, -1.0, STEELL, 0.4, 0.88)
    def drain():
        B('grille', BLACK, (0.495, -0.047, -1.2), (0.6, 0.004, 0.8))
        for k in range(6): B('bar', STEELL, (0.495 - 0.25 + k * 0.1, -0.055, -1.2), (0.03, 0.02, 0.8))
        B('drip', STEELL, (-0.495, -0.06, -0.25), (0.97, 0.06, 0.04), 0.01)
    def stencil():
        B('rib', STEELL, (0, -0.07, -1.0), (0.16, 0.09, 2.0), 0.02)
        text('mark', HULL, 'S-07', (0.495, -0.047, -0.35), 0.15, depth=0.002)
        B('lamp', AMBER, (-0.495, -0.055, -0.3), (0.1, 0.02, 0.06))
    def top():
        B('gutter', STEELL, (0, -0.1, -1.25), (2.0, 0.12, 0.06), 0.015)
    facade(STEEL, STEELB, GRATE, STEELL, LIGHT, top, (ribs, drain, stencil))

    c, b = kit('FlatGrate')            # wet steel grating set into the deck
    floor_base(STEELL)
    B('frame', GRATE, (0, 0, 0.003), (1.8, 1.9, 0.006))
    for k in range(10): B('bar', STEELL, (-0.81 + k * 0.18, 0, 0.008), (0.05, 1.8, 0.006))
    B('edge', LIGHT, (0, -2.05, 0.003), (2.0, 0.05, 0.006))
    done(c, b)

    c, b = kit('LightningRod')         # origin at its foot
    B('foot', GRATE, (0, 0, 0.1), (0.5, 0.5, 0.2), 0.03)
    cyl('mast', STEEL, (0, 0, 0.2), (0, 0, 7.0), 0.09, 8, r2=0.06)
    for z in (2.2, 4.4, 6.2): cyl('insulator', HULL, (0, 0, z), (0, 0, z + 0.16), 0.17, 10)
    cyl('tip', COPPER, (0, 0, 7.0), (0, 0, 7.9), 0.05, 8, r2=0.005)
    cyl('crown', COPPER, (0, 0, 6.95), (0, 0, 7.02), 0.12, 8)
    cyl('down', COPPER, (0.12, 0.05, 0.2), (0.12, 0.05, 6.9), 0.015, 6)
    B('lamp', WARN, (0, -0.08, 6.6), (0.1, 0.05, 0.1))
    done(c, b)

    c, b = kit('CableTray')            # 2 m of tray hung on a wall (origin at its centre)
    B('bottom', METAL, (0, 0, -0.1), (2.0, 0.34, 0.02))
    for s in (-1, 1): B('side', METAL, (0, s * 0.16, 0.0), (2.0, 0.02, 0.2))
    for k, (y, z, r) in enumerate(((-0.08, -0.04, 0.035), (0.0, -0.05, 0.03), (0.08, -0.04, 0.04), (-0.03, 0.02, 0.03))):
        cyl('cable', CABLE if k != 1 else COPPER, (-1.0, y, z), (1.0, y, z), r, 6)
    for x in (-0.6, 0.6): B('hanger', STEELL, (x, 0.12, 0.12), (0.06, 0.4, 0.04))
    done(c, b)

    c, b = kit('Anemometer')
    B('foot', GRATE, (0, 0, 0.06), (0.4, 0.4, 0.12), 0.02)
    cyl('mast', STEELL, (0, 0, 0.1), (0, 0, 2.4), 0.04, 8)
    cyl('hub', STEEL, (0, 0, 2.4), (0, 0, 2.55), 0.06, 8)
    for a in (0, 120, 240):
        ca, sa = math.cos(R(a)), math.sin(R(a))
        cyl('arm', STEELL, (0, 0, 2.5), (ca * 0.4, sa * 0.4, 2.5), 0.012, 6)
        cyl('cup', HULL, (ca * 0.4 - sa * 0.06, sa * 0.4 + ca * 0.06, 2.5), (ca * 0.4 + sa * 0.06, sa * 0.4 - ca * 0.06, 2.5), 0.07, 8, r2=0.01)
    cyl('vanearm', STEELL, (-0.5, 0, 2.1), (0.5, 0, 2.1), 0.012, 6)
    B('vane', HAZ, (0.45, 0, 2.15), (0.25, 0.01, 0.18))
    B('lamp', WARN, (0, 0, 2.6), (0.06, 0.06, 0.06))
    done(c, b)

    c, b = kit('StormShutter')         # 3 x 3.5 m, its back on the wall (origin at the bottom centre)
    for s in (-1, 1): B('jamb', STEEL, (s * 1.45, 0, 1.75), (0.14, 0.16, 3.5), 0.02)
    B('head', STEEL, (0, -0.02, 3.55), (3.1, 0.22, 0.24), 0.03)
    for k in range(10): B('slat', STEELL if k % 2 else STEEL, (0, 0.02, 0.18 + k * 0.33), (2.8, 0.06, 0.3), 0.012)
    stripes('ss', -1.4, 1.4, 0.0, 0.16, -0.012, 0.16)
    B('lamp', AMBER, (1.2, -0.13, 3.55), (0.18, 0.02, 0.1))
    done(c, b)

    c, b = kit('LedgeBracket')         # origin at the arm's top where it meets the wall; the arm runs toward -Y
    B('plate', STEEL, (0, 0.04, -0.6), (0.36, 0.06, 1.3), 0.02)
    B('arm', STEELL, (0, -0.62, -0.08), (0.14, 1.26, 0.16), 0.02)
    blade('strut', STEEL, (0, 0.0, -1.15), (0, -1.15, -0.16), (1, 0, 0), 0.1, 0.12)
    done(c, b)

    c, b = kit('WeatherBox')           # a louvred instrument screen on legs
    for sx in (-1, 1):
        for sy in (-1, 1): B('leg', STEELL, (sx * 0.3, sy * 0.25, 0.5), (0.05, 0.05, 1.0))
    B('box', HULL, (0, 0, 1.3), (0.8, 0.65, 0.6), 0.02)
    for k in range(5): B('louvre', HULLB, (0, -0.33, 1.08 + k * 0.11), (0.72, 0.03, 0.05), 0.005, rot=(R(-25), 0, 0))
    B('roof', HULL, (0, 0, 1.64), (0.95, 0.8, 0.06), 0.02)
    cyl('antenna', STEELL, (0.3, 0.2, 1.67), (0.3, 0.2, 2.4), 0.012, 6)
    done(c, b)
    shadow()

# =====================================================================================================================
def build_skyline():
    """Skyline Relay: rooftops of the sky city. Relay towers and dishes on cantilevered pads, antenna arrays,
    maintenance gantries under the decks, wind socks, rooftop vents."""
    def windows():
        for s in (-1, 1):
            B('win', WINDOW, (s * 0.495, -0.05, -0.9), (0.8, 0.02, 1.1))
            B('mull', METAL, (s * 0.495, -0.065, -0.9), (0.03, 0.02, 1.1))
            B('sill', METAL, (s * 0.495, -0.075, -1.48), (0.86, 0.06, 0.04))
    def louvres():
        louvre(0.495, -0.7, 0.8, 8); B('win', WINDOW, (-0.495, -0.05, -0.9), (0.8, 0.02, 1.1)); B('lit', WINLIT, (-0.495, -0.062, -1.2), (0.8, 0.004, 0.5))
    def hatch():
        B('hatch', NAVY, (0.495, -0.055, -1.05), (0.62, 0.03, 1.3), 0.03)
        B('status', LIGHT, (0.3, -0.075, -0.45), (0.08, 0.01, 0.03))
        for k in range(5): B('rung', METAL, (-0.495, -0.1, -0.4 - k * 0.3), (0.4, 0.03, 0.03))
        for s in (-1, 1): B('stile', METAL, (-0.495 + s * 0.21, -0.08, -1.0), (0.03, 0.05, 1.4))
    def top():
        B('parapet', HULLB, (0, -0.06, -0.02), (2.0, 0.1, 0.06))
    facade(HULL, HULLB, DARK, METAL, LIGHT, top, (windows, louvres, hatch))

    c, b = kit('FlatRoof')             # roof deck: seams, a grip strip, a lit edge
    floor_base(SEAM, LIGHT)
    B('grip', GRIP, (0, -1.72, 0.003), (1.9, 0.22, 0.006))
    done(c, b)
    c, b = kit('FlatRoofB')            # the same with an access hatch toward the back
    floor_base(SEAM, LIGHT)
    B('grip', GRIP, (0, -1.72, 0.003), (1.9, 0.22, 0.006))
    B('hatch', SEAM, (0.2, 1.55, 0.003), (0.9, 0.6, 0.006)); B('hatchin', STENCIL, (0.2, 1.55, 0.005), (0.8, 0.5, 0.006))
    done(c, b)

    c, b = kit('GantryUnder')          # a maintenance gantry hung under a deck (origin at the deck's bottom front edge)
    for z in (-0.12, -1.0):
        B('chord', METAL, (0, -0.05, z), (2.0, 0.08, 0.08))
    for x in (-0.95, 0.95): B('vert', METAL, (x, -0.05, -0.56), (0.07, 0.07, 0.9))
    blade('diag', METAL, (-0.95, -0.05, -0.95), (0.95, -0.05, -0.16), (0, 1, 0), 0.05, 0.05)
    B('walk', GRATE, (0, 0.35, -1.02), (2.0, 0.8, 0.05))
    for x in (-0.6, 0.6): B('hang', METAL, (x, 0.6, -0.5), (0.04, 0.04, 1.0))
    B('lamp', LIGHT, (0, -0.1, -1.06), (1.2, 0.04, 0.02))
    done(c, b)

    c, b = kit('RelayPad')             # a 3 x 3 m platform cantilevered off a deck's back face (origin at the back top edge)
    B('slab', GRATE, (0, 1.7, -0.12), (3.0, 3.0, 0.2), 0.02)
    B('edge', HAZ, (0, 0.21, -0.03), (3.0, 0.02, 0.02))
    for x in (-1.3, 1.3): blade('strut', METAL, (x, 0.25, -2.4), (x, 3.0, -0.25), (1, 0, 0), 0.12, 0.14)
    for x in (-1.45, 1.45):
        for y in (0.9, 3.15): B('post', METAL, (x, y, 0.5), (0.05, 0.05, 1.0))
    B('rail', HULL, (0, 3.15, 1.0), (3.0, 0.06, 0.06)); B('rail', HULL, (-1.45, 2.0, 1.0), (0.06, 2.3, 0.06)); B('rail', HULL, (1.45, 2.0, 1.0), (0.06, 2.3, 0.06))
    done(c, b)

    c, b = kit('RelayTower')           # a lattice mast with panels, a small dish and a beacon (origin at its foot)
    H, w0, w1 = 9.0, 0.7, 0.25
    def corner(sx, sy, z): w = w0 + (w1 - w0) * z / H; return Vector((sx * w, sy * w, z))
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)): cyl('leg', METAL, corner(sx, sy, 0), corner(sx, sy, H), 0.045, 6)
    for k in range(6):
        z0, z1 = k * 1.5, (k + 1) * 1.5
        for (ax, ay), (bx, by) in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
            cyl('ringbar', METAL, corner(ax, ay, z1), corner(bx, by, z1), 0.02, 4)
            cyl('brace', METAL, corner(ax, ay, z0), corner(bx, by, z1), 0.015, 4)
    for a in (0, 120, 240):
        B('panel', HULL, (math.sin(R(a)) * 0.42, -math.cos(R(a)) * 0.42, 7.4), (0.3, 0.08, 1.2), 0.02, rot=(0, 0, R(a)))
    cyl('dish', HULL, (0.3, -0.6, 5.2), (0.3, -0.85, 5.35), 0.05, 16, r2=0.55, cap=False)
    cyl('feed', METAL, (0.3, -0.75, 5.3), (0.3, -1.15, 5.5), 0.015, 6)
    cyl('mast', METAL, (0, 0, H), (0, 0, H + 1.4), 0.04, 6)
    B('beacon', WARN, (0, 0, H + 1.45), (0.14, 0.14, 0.14), 0.03)
    done(c, b)

    c, b = kit('Dish')                 # a big relay dish on a turret (origin at its foot)
    cyl('base', DARK, (0, 0, 0), (0, 0, 0.5), 0.55, 12)
    cyl('turret', HULL, (0, 0, 0.5), (0, 0, 1.4), 0.32, 12)
    B('yoke', METAL, (0, 0, 1.6), (0.9, 0.3, 0.3), 0.03)
    cyl('dish', HULL, (0, -0.2, 1.8), (0, -0.9, 2.5), 0.15, 20, r2=1.25, cap=False)
    cyl('dishback', DARK, (0, -0.15, 1.75), (0, -0.25, 1.85), 0.5, 12)
    cyl('rim', METAL, (0, -0.9, 2.5), (0, -0.93, 2.53), 1.26, 20, cap=False)
    for a in (0, 120, 240):
        ca, sa = math.cos(R(a)), math.sin(R(a))
        cyl('strut', METAL, (ca * 1.0, -0.85 - sa * 0.2, 2.5 + sa * 0.95), (0, -1.6, 3.1), 0.015, 4)
    cyl('feed', DARK, (0, -1.6, 3.1), (0, -1.75, 3.22), 0.08, 8)
    B('lamp', WARN, (0, 0.2, 1.5), (0.08, 0.04, 0.08))
    done(c, b)

    c, b = kit('AntennaArray')
    B('plate', DARK, (0, 0, 0.05), (1.2, 0.6, 0.1), 0.02)
    for k, (x, h) in enumerate(((-0.45, 2.6), (-0.15, 1.8), (0.15, 3.2), (0.45, 2.2))):
        cyl('whip', METAL, (x, 0.1, 0.1), (x, 0.1, 0.1 + h), 0.02, 6, r2=0.008)
        if k == 2: B('tip', WARN, (x, 0.1, 0.12 + h), (0.06, 0.06, 0.06))
    for x in (-0.3, 0.3): B('panel', HULL, (x, -0.15, 0.7), (0.22, 0.06, 0.9), 0.02)
    done(c, b)

    c, b = kit('WindSock')             # it streams along the route, as the breeze up here blows
    B('foot', DARK, (0, 0, 0.05), (0.35, 0.35, 0.1), 0.02)
    cyl('pole', HULL, (0, 0, 0.1), (0, 0, 3.0), 0.04, 8)
    ring('hoop', METAL, (0.02, 0, 2.95), 0.2, 0.012, 8, axis='x')
    for k in range(4):
        x0, x1 = 0.04 + k * 0.35, 0.04 + (k + 1) * 0.35
        cyl('sock', PADO if k % 2 == 0 else HULL, (x0, 0, 2.95 - k * 0.07), (x1, 0, 2.95 - (k + 1) * 0.07), 0.2 - k * 0.035, 10, r2=0.2 - (k + 1) * 0.035, cap=False)
    done(c, b)

    c, b = kit('RoofVent')             # a rooftop air handler with a fan and two mushroom vents
    B('unit', DARK, (0, 0, 0.45), (1.6, 0.9, 0.9), 0.04)
    B('panel', HULLB, (-0.35, -0.455, 0.45), (0.7, 0.02, 0.7), 0.01)
    louvre(0.42, 0.45, 0.6, 6)
    cyl('fan', BLACK, (0.3, 0, 0.9), (0.3, 0, 0.93), 0.36, 16)
    for k in range(4): B('blade', METAL, (0.3, 0, 0.94), (0.66, 0.06, 0.01), rot=(0, 0, R(45 * k)))
    for x in (-0.55, -0.15):
        cyl('stem', METAL, (x, 0.15, 0.9), (x, 0.15, 1.25), 0.07, 8)
        cyl('cap', HULL, (x, 0.15, 1.25), (x, 0.15, 1.35), 0.16, 10, r2=0.06)
    done(c, b)
    shadow()

# =====================================================================================================================
def build_foundry():
    """Helix Foundry: furnace housings with glowing vents, crucibles, pipework and valves, crane rails and hooks,
    catwalk railings, warning stripes, and lower terraces behind the decks to stand them on."""
    def rivets():
        for s in (-1, 1):
            B('plate', RUST, (s * 0.495, -0.05, -1.0), (0.8, 0.02, 1.6), 0.02)
            for z in (-0.3, -1.7):
                for x in (-0.3, 0.3): cyl('rivet', METAL, (s * 0.495 + x, -0.06, z), (s * 0.495 + x, -0.075, z), 0.025, 6)
    def port():
        B('portframe', GUN, (0.495, -0.05, -0.9), (0.7, 0.04, 0.5), 0.02)
        B('portglow', FURN, (0.495, -0.06, -0.9), (0.56, 0.01, 0.34))
        for k in range(4): B('bar', BLACK, (0.495 - 0.21 + k * 0.14, -0.075, -0.9), (0.04, 0.02, 0.36))
        stripes('ps', -0.9, -0.1, -1.9, -1.76, -0.05)
    def pipe():
        cyl('pipe', COPPER, (-0.3, -0.16, 0.0), (-0.3, -0.16, -2.0), 0.09, 10)
        for z in (-0.15, -1.85): cyl('flange', GUN, (-0.3, -0.16, z), (-0.3, -0.16, z - 0.06), 0.13, 10)
        B('clamp', GUN, (-0.3, -0.08, -1.0), (0.24, 0.12, 0.08), 0.01)
        text('mark', HAZ, 'F-3', (0.495, -0.047, -0.4), 0.15, depth=0.002)
    def top():
        stripes('ts', -1.0, 1.0, -1.16, -1.02, -0.042, 0.16)
    facade(GUN, GUNB, BLACK, METAL, AMBER, top, (rivets, port, pipe))

    c, b = kit('FlatFoundry')          # deck plates, grip, a stripe of hazard bands along the front edge
    floor_base(SEAM)
    for s in (-1, 1): B('grip', GRIP, (s * 0.5, 1.6, 0.003), (0.7, 0.32, 0.006))
    stripes('fe', -1.0, 1.0, -2.12, -1.86, 0.004, 0.22, plane='xy')
    done(c, b)
    c, b = kit('FlatHazEdge'); stripes('he', -1.0, 1.0, -0.12, 0.12, 0.004, 0.2, plane='xy'); done(c, b)

    # a lower terrace behind the deck, 2 m along by 6.3 m deep (origin at its top front edge), and its legs
    for name, depth in (('Terrace', 6.3), ('TerraceS', 4.0)):
        c, b = kit(name)
        B('slab', GUN, (0, depth / 2, -0.15), (2.0, depth, 0.3))
        B('grate', GRATE, (0, depth / 2, 0.003), (1.9, depth - 0.2, 0.006))
        B('lip', HAZ, (0, 0.05, 0.004), (2.0, 0.1, 0.008))
        B('beam', RUST, (0, depth / 2, -0.45), (2.0, 0.25, 0.3))
        done(c, b)
    c, b = kit('TerraceLeg')           # 1 m of column, stretched by the layout
    B('leg', GUN, (0, 0, -0.5), (0.35, 0.35, 1.0))
    B('flange', RUST, (0, 0, -0.5), (0.42, 0.08, 1.0))
    done(c, b)
    c, b = kit('RailBack')             # a railing hung on a deck's back face (origin at the back top edge)
    for s in (-1, 1): B('post', GUN, (s * 0.97, 0.06, 0.45), (0.05, 0.05, 1.3))
    B('rail', HAZ, (0, 0.06, 1.05), (2.0, 0.07, 0.06), 0.015)
    B('mid', GUN, (0, 0.06, 0.55), (2.0, 0.03, 0.03))
    B('kick', GUN, (0, 0.06, 0.08), (2.0, 0.02, 0.16))
    done(c, b)

    c, b = kit('Furnace')              # a furnace housing: glowing vents, a door with hazard bands, a stack
    B('body', GUN, (0, 0, 2.2), (3.6, 2.6, 4.4), 0.06)
    for s in (-1, 1): B('side', RUST, (s * 1.81, 0, 2.0), (0.04, 2.3, 3.6), 0.02)
    B('plinth', BLACK, (0, 0, 0.15), (3.8, 2.8, 0.3), 0.03)
    for k in range(4):
        B('vent', FURN, (-0.9, -1.31, 1.4 + k * 0.42), (1.2, 0.02, 0.16))
        B('ventlip', METAL, (-0.9, -1.36, 1.5 + k * 0.42), (1.3, 0.08, 0.04))
    B('door', RUST, (0.85, -1.32, 1.3), (1.2, 0.04, 2.2), 0.03)
    stripes('ds', 0.25, 1.45, 0.3, 0.6, -1.345, 0.14)
    B('window', FURN, (0.85, -1.35, 1.9), (0.5, 0.01, 0.3))
    cyl('stack', GUN, (0.9, 0.5, 4.4), (0.9, 0.5, 7.8), 0.42, 12, r2=0.34)
    for z in (5.4, 6.8): cyl('band', RUST, (0.9, 0.5, z), (0.9, 0.5, z + 0.12), 0.44, 12)
    cyl('glow', FURN, (0.9, 0.5, 7.8), (0.9, 0.5, 7.82), 0.3, 12)
    B('lamp', AMBER, (-1.6, -1.31, 4.0), (0.18, 0.04, 0.12))
    done(c, b)

    c, b = kit('Crucible')             # a ladle in its stand, full and glowing
    cyl('pot', RUST, (0, 0, 0.5), (0, 0, 2.0), 0.8, 16, r2=1.05)
    cyl('rim', GUN, (0, 0, 2.0), (0, 0, 2.14), 1.12, 16, cap=False)
    cyl('melt', MOLTEN, (0, 0, 2.0), (0, 0, 2.08), 1.04, 16)
    for s in (-1, 1):
        cyl('trunnion', METAL, (s * 1.0, 0, 1.5), (s * 1.3, 0, 1.5), 0.12, 8)
        B('stand', GUN, (s * 1.4, 0, 0.85), (0.22, 0.6, 1.7), 0.03)
    B('base', BLACK, (0, 0, 0.12), (3.2, 1.0, 0.24), 0.03)
    B('spout', GUN, (0, -1.12, 1.95), (0.3, 0.3, 0.12), 0.02)
    done(c, b)

    c, b = kit('PipeRun')              # 2 m of two pipes on a rack (origin at the rack's foot)
    for y, z, r, m in ((0.0, 1.0, 0.16, COPPER), (0.0, 0.55, 0.11, GUN)):
        cyl('pipe', m, (-1.0, y, z), (1.0, y, z), r, 10)
        cyl('flange', RUST, (0.9, y, z), (1.0, y, z), r + 0.05, 10)
    B('stand', GUN, (-0.6, 0, 0.6), (0.1, 0.3, 1.2), 0.01)
    B('foot', BLACK, (-0.6, 0, 0.03), (0.3, 0.4, 0.06))
    done(c, b)

    c, b = kit('ValveStation')         # risers, a manifold and a valve wheel
    for x in (-0.5, 0.5): cyl('riser', COPPER, (x, 0, 0), (x, 0, 2.2), 0.11, 10)
    cyl('manifold', GUN, (-0.7, 0, 1.4), (0.7, 0, 1.4), 0.14, 10)
    cyl('stem', METAL, (0, 0, 1.4), (0, -0.4, 1.4), 0.03, 6)
    ring('wheel', HAZ, (0, -0.42, 1.4), 0.25, 0.025, 8, axis='y')
    for k in range(2): cyl('spoke', HAZ, (-0.25 if k else 0, -0.42, 1.4 if k else 1.15), (0.25 if k else 0, -0.42, 1.4 if k else 1.65), 0.015, 4)
    cyl('gauge', HULL, (0.5, -0.12, 1.85), (0.5, -0.17, 1.85), 0.09, 10)
    B('base', BLACK, (0, 0, 0.05), (1.6, 0.5, 0.1), 0.02)
    done(c, b)

    c, b = kit('CraneRail')            # 6 m of crane girder (origin at its centre)
    B('web', HAZ, (0, 0, 0), (6.0, 0.08, 0.7))
    for z in (-0.37, 0.37): B('flange', HAZ, (0, 0, z), (6.0, 0.45, 0.06))
    B('rail', METAL, (0, 0, 0.43), (6.0, 0.08, 0.06))
    for x in (-2.0, 0.0, 2.0):
        for s in (-1, 1): B('stiff', HAZ, (x, s * 0.12, 0), (0.04, 0.16, 0.66))
    stripes('cs', -3.0, 3.0, -0.3, 0.3, -0.045, 0.3)
    done(c, b)
    c, b = kit('CraneColumn')          # 1 m of the girders' column, stretched by the layout
    B('col', GUN, (0, 0, 0.5), (0.5, 0.5, 1.0)); B('face', RUST, (0, -0.255, 0.5), (0.3, 0.01, 1.0))
    done(c, b)
    c, b = kit('CraneHook')            # a trolley on the girder with a hook block hanging 5 m below (origin at the girder's centre)
    B('trolley', GUN, (0, 0, -0.55), (1.2, 0.8, 0.4), 0.03)
    for x in (-0.12, 0.12): cyl('rope', BLACK, (x, 0, -0.75), (x, 0, -5.0), 0.02, 6)
    B('block', HAZ, (0, 0, -5.2), (0.45, 0.3, 0.55), 0.04)
    cyl('hook', GUN, (0, 0, -5.45), (0, 0, -5.95), 0.06, 8)
    ring('hookcurve', GUN, (0.12, 0, -6.05), 0.13, 0.05, 6, axis='y')
    done(c, b)
    shadow()

# =====================================================================================================================
def build_undercity():
    """Undercity Descent: the old city. Fire escapes, neon signs, wet pavement, dumpsters and crates, hanging
    cables on utility poles, a track bed under the transit line."""
    def windows():
        for s, m in ((-1, WINDOW), (1, WINLIT)):
            B('win', m, (s * 0.495, -0.05, -0.85), (0.6, 0.02, 0.9))
            B('frame', CONC, (s * 0.495, -0.06, -0.85), (0.04, 0.03, 0.9))
            B('sill', CONC, (s * 0.495, -0.08, -1.33), (0.75, 0.1, 0.06), 0.01)
    def shutter():
        for k in range(9): B('rib', STEELL, (0.495, -0.055, -0.4 - k * 0.14), (0.82, 0.03, 0.11), 0.008)
        B('box', GUN, (0.495, -0.08, -0.28), (0.9, 0.1, 0.16), 0.02)
        B('neon', NEON, (-0.495, -0.06, -0.6), (0.7, 0.02, 0.04)); B('neon', NEONB, (-0.495, -0.06, -0.72), (0.5, 0.02, 0.03))
    def drain():
        cyl('pipe', GUN, (0.85, -0.12, 0.0), (0.85, -0.12, -2.0), 0.06, 8)
        for z in (-0.4, -1.4): B('clip', GUN, (0.85, -0.07, z), (0.14, 0.1, 0.05))
        B('poster', NAVY, (-0.4, -0.05, -0.9), (0.6, 0.01, 0.85))
        B('posterband', NEON, (-0.4, -0.057, -0.6), (0.5, 0.004, 0.1))
    def top():
        B('cornice', CONC, (0, -0.1, -0.02), (2.0, 0.16, 0.1), 0.02)
        B('neon', NEON, (0, -0.07, -1.12), (2.0, 0.02, 0.03))
    facade(CONC, PLASTER, GUN, CONC, NEONB, top, (windows, shutter, drain))

    for name, pud in (('FlatPaveA', None), ('FlatPaveB', [(0.1, 0.5), (0.8, 0.3), (0.9, 0.9), (0.3, 1.2)])):
        c, b = kit(name)                # wet pavement: slab seams, a puddle, a kerb line toward the camera
        floor_base(BLACK)
        B('seamm', BLACK, (0, 0, 0.002), (0.02, 4.4, 0.004))
        if pud: flat_poly('puddle', WET, pud)
        B('kerb', CONC, (0, -2.05, 0.004), (2.0, 0.16, 0.008))
        done(c, b)
    for name, pud in (('FlatTar', False), ('FlatTarB', True)):
        c, b = kit(name)                # tar roof (with a puddle)
        for y in (-1.4, 1.6): B('seam', BLACK, (0, y, 0.002), (2.0, 0.03, 0.004))
        if pud: flat_poly('puddle', WET, [(-0.6, 0.6), (0.3, 0.4), (0.4, 1.1), (-0.4, 1.3)])
        B('edge', NEONB, (0, -2.05, 0.003), (2.0, 0.04, 0.006))
        done(c, b)
    c, b = kit('FlatPlatform')          # the transit platform: paving and a yellow edge at the back, over the track
    floor_base(BLACK)
    B('edge', HAZ, (0, 1.95, 0.003), (2.0, 0.25, 0.006))
    for k in range(5): B('dot', GRIP, (-0.8 + k * 0.4, 1.95, 0.0065), (0.08, 0.08, 0.002))
    done(c, b)

    c, b = kit('FireEscape')            # balcony and stair on a wall below the deck (origin at the deck's top front edge)
    for z, w in ((-1.3, 3.0), (-4.3, 3.0)):
        B('deck', GRATE, (0, -0.5, z), (w, 0.9, 0.05))
        for x in (-1.48, 1.48): B('post', GUN, (x, -0.92, z + 0.5), (0.04, 0.04, 1.0))
        B('rail', GUN, (0, -0.92, z + 1.0), (w, 0.04, 0.04)); B('mid', GUN, (0, -0.92, z + 0.5), (w, 0.02, 0.02))
        for x in (-1.48, 1.48): B('siderail', GUN, (x, -0.5, z + 1.0), (0.04, 0.8, 0.04))
        for x in (-1.2, 1.2): blade('bracket', GUN, (x, 0.0, z - 0.6), (x, -0.85, z - 0.03), (1, 0, 0), 0.05, 0.06)
    blade('stringer', GUN, (-1.1, -0.35, -4.27), (0.9, -0.35, -1.33), (0, 1, 0), 0.04, 0.2)
    blade('stringer', GUN, (-1.1, -0.75, -4.27), (0.9, -0.75, -1.33), (0, 1, 0), 0.04, 0.2)
    for k in range(7):
        t = (k + 0.5) / 7; B('tread', GRATE, (-1.1 + 2.0 * t, -0.55, -4.27 + 2.94 * t), (0.22, 0.4, 0.03))
    B('ladder', GUN, (1.25, -0.6, -5.4), (0.4, 0.03, 2.0))
    done(c, b)

    c, b = kit('CableDroop')            # cables slung along a wall, 4 m (origin at its left attachment)
    for k, (z, d) in enumerate(((0.0, 0.45), (-0.12, 0.7), (0.05, 0.3))):
        sag('cable', CABLE, (0, -0.05 - k * 0.03, z), (4.0, -0.05 - k * 0.03, z - 0.05 * k), d, 0.018, 6)
    for x in (0.0, 4.0): B('clip', GUN, (x, -0.04, -0.03), (0.08, 0.08, 0.12))
    done(c, b)

    c, b = kit('UtilityPole')           # a pole with a crossarm and insulators (origin at its foot)
    cyl('pole', WOOD, (0, 0, 0), (0, 0, 7.4), 0.14, 8, r2=0.1)
    B('arm', GUN, (0, 0, 6.9), (0.12, 1.6, 0.12), 0.02)
    for y in (-0.65, 0.0, 0.65): cyl('insulator', HULL, (0, y, 6.96), (0, y, 7.12), 0.05, 6)
    B('box', GUN, (0, -0.16, 4.2), (0.36, 0.2, 0.5), 0.02)
    B('lamp', WINLIT, (0, -0.28, 4.35), (0.2, 0.02, 0.1))
    done(c, b)
    c, b = kit('CableSpan')             # three cables hanging between two poles 6 m apart (origin at the left pole's middle insulator)
    for y in (-0.65, 0.0, 0.65): sag('cable', CABLE, (0, y, 0), (6.0, y, 0), 0.35 + abs(y) * 0.2, 0.015, 6)
    done(c, b)

    c, b = kit('NeonBlade')             # a vertical neon sign on a bracket (origin at its foot on the post)
    cyl('post', GUN, (0, 0.3, 0), (0, 0.3, 6.2), 0.08, 8)
    B('arm', GUN, (0, -0.1, 5.9), (0.08, 0.9, 0.08)); B('arm', GUN, (0, -0.1, 2.6), (0.08, 0.9, 0.08))
    B('board', BLACK, (0, -0.25, 4.25), (0.9, 0.18, 3.4), 0.03)
    for s in (-1, 1): B('edge', NEON, (s * 0.44, -0.345, 4.25), (0.03, 0.02, 3.3))
    for k, ch in enumerate('HOTEL'): text('letter', NEONB, ch, (0, -0.345, 5.6 - k * 0.62), 0.5, depth=0.02)
    done(c, b)
    c, b = kit('NeonBoard')             # a shop sign on two legs (origin at its foot)
    for s in (-1, 1): cyl('leg', GUN, (s * 1.3, 0.05, 0), (s * 1.3, 0.05, 3.2), 0.06, 8)
    B('board', BLACK, (0, 0, 3.6), (3.2, 0.16, 1.1), 0.03)
    for z in (3.08, 4.12): B('edge', NEONB, (0, -0.085, z), (3.1, 0.02, 0.03))
    text('words', NEON, 'NOODLES 24H', (0, -0.09, 3.6), 0.42, depth=0.02)
    done(c, b)

    c, b = kit('Dumpster')
    B('body', BIN, (0, 0, 0.62), (1.8, 1.0, 1.0), 0.04)
    B('lid', BLACK, (0, 0.04, 1.17), (1.84, 1.06, 0.08), 0.02, rot=(R(-6), 0, 0))
    B('bar', METAL, (0, -0.52, 0.75), (1.7, 0.05, 0.05))
    for sx in (-1, 1):
        for sy in (-1, 1): cyl('wheel', BLACK, (sx * 0.7, sy * 0.35 - 0.04, 0.06), (sx * 0.7, sy * 0.35 + 0.04, 0.06), 0.06, 8)
    text('mark', HULL, 'SANITATION', (0, -0.505, 0.5), 0.13, depth=0.002)
    done(c, b)
    c, b = kit('Crates')                # a stack of three crates
    for (x, y, z, s) in ((-0.55, 0, 0, 1.0), (0.5, 0.05, 0, 0.9), (-0.45, 0.02, 1.0, 0.8)):
        h = s; B('crate', WOOD, (x, y, z + h / 2), (s, s, h), 0.02)
        for zz in (z + 0.12, z + h - 0.12): B('slat', RUST, (x, y - s / 2 - 0.005, zz), (s, 0.01, 0.08))
        B('brace', RUST, (x, y - s / 2 - 0.006, z + h / 2), (0.08, 0.01, h - 0.2), rot=(0, R(40), 0))
    done(c, b)
    c, b = kit('AcUnit')                # a rooftop air conditioner and a water tank
    B('unit', CONC, (0, 0, 0.4), (1.3, 0.8, 0.8), 0.03)
    cyl('fan', BLACK, (0.2, -0.41, 0.42), (0.2, -0.43, 0.42), 0.28, 12)
    for k in range(4): B('blade', GUN, (0.2, -0.44, 0.42), (0.5, 0.01, 0.05), rot=(0, R(45 * k), 0))
    B('pipe', COPPER, (-0.5, 0.2, 0.85), (0.05, 0.05, 0.3))
    done(c, b)
    c, b = kit('WaterTank')
    for a in (45, 135, 225, 315): B('leg', WOOD, (math.cos(R(a)) * 0.45, math.sin(R(a)) * 0.45, 0.6), (0.12, 0.12, 1.2))
    cyl('tank', WOOD, (0, 0, 1.2), (0, 0, 2.8), 0.7, 12)
    for z in (1.5, 2.1, 2.6): cyl('hoop', GUN, (0, 0, z), (0, 0, z + 0.05), 0.72, 12)
    cyl('roof', GUN, (0, 0, 2.8), (0, 0, 3.25), 0.78, 12, r2=0.05)
    done(c, b)

    c, b = kit('TrackBed')              # the transit line's bed behind the platform, 2 m (origin at the platform's back top edge)
    B('bed', CONC, (0, 2.1, -1.15), (2.0, 3.9, 0.3))
    B('wall', CONC, (0, 0.12, -0.6), (2.0, 0.24, 1.2))
    for k in range(3): B('sleeper', GUN, (-0.66 + k * 0.66, 2.7, -0.95), (0.2, 2.0, 0.1))
    for y in (2.0, 3.4): B('rail', METAL, (0, y, -0.86), (2.0, 0.08, 0.1))
    B('live', HAZ, (0, 1.5, -0.88), (2.0, 0.06, 0.06))
    B('puddle', WET, (0.3, 3.0, -0.995), (1.0, 0.8, 0.01))
    done(c, b)
    c, b = kit('Sidewalk')              # a raised walk behind the street, 2 m along by 4 m deep (origin at its top front edge)
    B('slab', PAVE, (0, 2.0, -0.6), (2.0, 4.0, 1.2))
    B('kerb', CONC, (0, 0.1, -0.02), (2.0, 0.2, 0.06), 0.01)
    B('seam', BLACK, (-0.99, 2.0, 0.002), (0.02, 4.0, 0.004))
    done(c, b)
    shadow()

{'arena': build_arena, 'tower': build_tower, 'skyline': build_skyline, 'foundry': build_foundry, 'undercity': build_undercity}[ZONE]()
for m in list(bpy.data.materials):
    if m.users == 0: bpy.data.materials.remove(m)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, ZONE + '_kit.blend'))
print('built kit:', ZONE, [c.name for c in bpy.data.collections])
