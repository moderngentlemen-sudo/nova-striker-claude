# Renders a zone in Blender, without and with its dressing kit, from the game's camera, for review (render_gym.py
# does the same for the Movement Gym). The level is rebuilt from level_boxes.json roughly as the game draws it:
# the boxes (View.BuildLevel, bent round the curves), the level features' catwalks and hazards (LevelFx), the
# breakable pieces, and the big set pieces behind each zone (the Storm Spire's tower, the Foundry's furnace dome and
# reactor core, the Undercity's cooling towers, buildings and transit line: Landmarks.cs), so clearances can be judged.
# Run: blender -b -P render_zone.py -- <zone> <dir holding <zone>_kit.blend and tex/> <out-prefix> [samples] [x:y,x:y...] [after]
import math, os, sys, bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_lib import *
import zone_layout as Z

args = sys.argv[sys.argv.index('--') + 1:]
ZONE, KITDIR, OUT = args[0], args[1], args[2]
SAMPLES = int(args[3]) if len(args) > 3 else 16
SHOTS = {'arena': [(66, 0), (79, 0), (93, 0)], 'tower': [(108, 0), (128, 0), (152, 15.6)],
         'skyline': [(176, 15.6), (205, 15.6), (232, 12.6), (282, 18.6)],
         'foundry': [(418, 0), (490, 0), (572, 0), (612, 4), (730, 24.2)],
         'undercity': [(815, 20), (868, 20), (950, 9), (1040, 0), (1150, 0)]}[ZONE]
if len(args) > 4 and args[4]: SHOTS = [tuple(float(v) for v in s.split(':')) for s in args[4].split(',')]
ONLY_AFTER = len(args) > 5 and args[5] == 'after'
reset()
sc = bpy.context.scene
def V(p): return Vector((p[0], -p[2], p[1]))                    # three.js space to Blender's
def at(x, y, dz): return V(Z.W(x, y, dz))
def pbox(name, m, x, y, dz, size, bev=0.0, yaw=0.0):
    return box(name, m, at(x, y, dz), size, bev, 1, rot=(0, 0, Z.yaw_at(x) + yaw))

zone = Z.ZONES[ZONE]; ROUTE0 = 400 if ZONE == 'foundry' else 790 if ZONE == 'undercity' else -20
ZX0, ZX1 = max(ROUTE0, zone['x0'] - 8), zone['x1'] + 8
def near(b): return b['x1'] > ZX0 and b['x0'] < ZX1 and b['x0'] >= ROUTE0 - 2

# ---- The level as the game draws it ----
CAPM = mat('cap', srgb('#d9dfe7'), rough=0.82); BODY = mat('body', srgb('#5f7897'), rough=0.72); DARKM = mat('dark', srgb('#46596f'), rough=0.7)
TRIM = mat('trim', srgb('#7fe3ff'), emit=srgb('#4fd6ff'), strength=4)
GATE = mat('gate', srgb('#ff2e7e'), emit=srgb('#ff2e7e'), strength=2)
GRATEM = mat('lgrate', srgb('#2c333d'), metal=0.5, rough=0.6); PALE = mat('pale', srgb('#c9d1da'), rough=0.5)
AMB = mat('amber', srgb('#ffb02e'), emit=srgb('#ffb02e'), strength=2); STEELM = mat('lmetal', srgb('#55606e'), metal=0.6, rough=0.45)
CRATE = mat('crate', srgb('#c98b4a'), rough=0.85); BARR = mat('barr', srgb('#8a97a8'), rough=0.6); PIL = mat('pillar', srgb('#b9c2cc'), rough=0.8)
def depth_for(b): return 2.6 if b['type'] == 'o' else 1.8 if b['tag'] in ('panel', 'column', 'pillar', 'wall') else 3.2 if b['type'] == 'g' else 4.4
def level_box(b):
    d = depth_for(b); h = b['y1'] - b['y0']; curved = any(abs(Z.yaw_at(b['x0'] + k * 0.5) - Z.yaw_at(b['x0'])) > 1e-3 for k in range(int((b['x1'] - b['x0']) * 2) + 1))
    n = int(math.ceil((b['x1'] - b['x0']) / 0.9)) if curved else 1
    for i in range(n):
        x0 = b['x0'] + (b['x1'] - b['x0']) * i / n; x1 = b['x0'] + (b['x1'] - b['x0']) * (i + 1) / n; xm = (x0 + x1) / 2; w = (x1 - x0) * (1.04 if curved else 1)
        if b['type'] == 'g': pbox('gate', GATE, xm, b['y0'] + min(h, 12) / 2, 0, (w, d, min(h, 12))); continue
        capH = min(0.22, h * 0.4)
        pbox('lvl', DARKM if b['type'] == 'o' or b['tag'] == 'bound' else BODY, xm, b['y0'] + (h - capH) / 2, 0, (w, d, h - capH), 0.04)
        pbox('cap', CAPM, xm, b['y1'] - capH / 2, 0, (w + 0.08, d + 0.1, capH), 0.02)
        if b['tag'] not in ('tunnel', 'panel'): pbox('trim', TRIM, xm, b['y1'] - capH - 0.05, d / 2 + 0.02, (w, 0.06, 0.06))
for b in Z.BOXES:
    if not near(b): continue
    if b['type'] == 'd':
        w, h = b['x1'] - b['x0'], b['y1'] - b['y0']; xm = (b['x0'] + b['x1']) / 2
        m, d = {'crate': (CRATE, max(w, 1)), 'barricade': (BARR, 2.6), 'glass': (mat('glassb', srgb('#bfe8ff'), rough=0.05), 3.4), 'pillar': (PIL, w)}[b['tag']]
        pbox('break', m, xm, b['y0'] + h / 2, 0, (w if b['tag'] != 'glass' else w * 0.6, d, h), 0.03)
    else: level_box(b)
# the level features (LevelFx): catwalks, crumbling platforms, hazards
for e in Z.EXTRA:
    if not near(e): continue
    xm, w = (e['x0'] + e['x1']) / 2, e['x1'] - e['x0']; crumble = e['tag'] == 'collapse'
    pbox('catwalk', PALE if crumble else GRATEM, xm, e['y1'] - 0.11, 0, (w, 2.6, 0.22))
    pbox('cwtrim', AMB if crumble else TRIM, xm, e['y1'] - 0.25, 1.3, (w, 0.06, 0.06))
    pbox('cwbar', STEELM, xm, e['y1'] - 0.3, -1.1, (w * 0.96, 0.12, 0.12))
for h in Z.HAZ:
    if h['zone'] != ZONE or h['kind'] == 'collapse': continue
    xm, w, k = (h['x0'] + h['x1']) / 2, h['x1'] - h['x0'], h['kind']
    if k == 'vent': pbox('vent', STEELM, xm, h['y0'] + 0.02, 0, (w + 0.3, 2.4, 0.12))
    elif k == 'shock': pbox('shock', mat('shockm', srgb('#7fd7ff'), emit=srgb('#7fd7ff'), strength=1.5), xm, h['y0'] + 0.03, 0, (w, 3.2, 0.05))
    elif k == 'laser':
        for dz in (-2.5, 2.5): pbox('lpost', STEELM, xm, (h['y0'] + h['y1']) / 2 - 0.1, dz, (0.3, h['y1'] - h['y0'] + 0.7, 0.3))
        pbox('beam', mat('beamm', srgb('#ff4a6a'), emit=srgb('#ff2e5a'), strength=4), xm, (h['y0'] + h['y1']) / 2, 0, (0.08, 4.8, h['y1'] - h['y0']))
    elif k == 'crusher':
        pbox('crusher', STEELM, xm, h['y1'], 0, (w, 2.6, 1.2)); pbox('ram', GRATEM, xm, h['y1'] + 3.6, 0, (0.5, 0.5, 6))
    elif k == 'slag': pbox('slag', mat('slagm', srgb('#ff6a1a'), emit=srgb('#ff5a10'), strength=3), xm, h['y0'] + 0.04, 0, (w, 3.0, 0.08))
    elif k == 'lightning':
        pbox('lplate', AMB, xm, h['y0'] + 0.02, -0.6, (w, w, 0.04)); pbox('lrod', STEELM, xm, h['y0'] + 1.6, -2.2, (0.12, 0.12, 3.2))
    elif k == 'debris':
        pbox('dplate', AMB, xm, h['y0'] + 0.02, 0, (w, 2.2, 0.04)); pbox('grid', GRATEM, xm, h['y1'] + 0.2, 0, (w + 0.6, 2.6, 0.4))
    elif k == 'wind':
        for x in (h['x0'], h['x1']): pbox('wpost', STEELM, x, h['y0'] + 1.2, -2.2, (0.2, 0.2, 2.4))
# lane guides
for l0, l1 in Z.LANES:
    if not (l1 > ZX0 and l0 < ZX1): continue
    for x in [l0 + k * 1.5 for k in range(int((l1 - l0) / 1.5))]:
        b = Z.deck_at(ZONE, x, x + 0.9) or Z.deck_at(ZONE, x - 0.15, x + 0.25)
        if b:
            for dz in (-0.7, 0.7): pbox('guide', TRIM, x + 0.45, b['y1'] + 0.012, dz, (0.9, 0.06, 0.012))

# ---- What stands behind each zone (View.BuildBackdrop/BuildProps, Landmarks.cs), roughly ----
WHITE = mat('white', srgb('#f1f4f7'), rough=0.5); NAVYP = mat('navyp', srgb('#2b4f7e'), rough=0.6)
LAMP = mat('lamp', srgb('#9ff0ff'), emit=srgb('#5fe0ff'), strength=6); GLOWM = mat('glowm', srgb('#7fe3ff'), emit=srgb('#5fd8ff'), strength=3)
TOWERM = mat('towerm', srgb('#eef3f8'), rough=0.4)
if ZONE in ('arena', 'tower', 'skyline'):
    box('terrace', mat('terr', srgb('#55708f'), rough=0.75), V((43.5, -3.6, -6.4)), (108, 8, 5), 0.05)
    box('terrtop', mat('terrtop', srgb('#c9d2dd'), rough=0.85), V((43.5, -1.2, -6.4)), (108, 8.1, 0.2), 0.02)
    for x in range(-4, 96, 11):
        box('pot', WHITE, V((x, -1.1 + 0.45, -5.2)), (1.4, 1.4, 0.9), 0.2, 3)
        cyl('pole', NAVYP, V((x + 5, -1.1, -7.2)), V((x + 5, 4.4, -7.2)), 0.08, 10)
        box('banner', mat('cloth', srgb('#2fb5c9'), rough=0.8), V((x + 5.62, 2.8, -7.2)), (1.1, 0.02, 2.6), 0)
    for x in (8, 48, 88):
        tube('arch', WHITE, [V((x + 9 * math.cos(a), -1 + 9 * math.sin(a), -16)) for a in [k * math.pi / 12 for k in range(13)]], 0.55)
    cyl('tower', TOWERM, V((104, -35, -14)), V((104, 75, -14)), 11.6, 48, r2=10.6)
    for i in range(12):
        a = i / 12 * math.pi * 2; box('tglow', GLOWM, V((104 + math.sin(a) * 10.7, 25, -14 + math.cos(a) * 10.7)), (0.3, 0.3, 90), 0)
    for (x, y, hh) in ((195, 15.6, 6), (212, 15.6, 8), (226, 12.6, 5), (252, 18.6, 6), (262, 18.6, 7), (294, 18.6, 7)):
        p = Z.W(x, y, -2.9); cyl('mast', NAVYP, V(p), V((p[0], y + hh, p[2])), 0.13, 10)
    p = Z.W(310, 18.6, -1.2); cyl('beacon', WHITE, V(p), V((p[0], 32.6, p[2])), 0.85, 16, r2=0.45)
ARCS = {'foundry': [(464, 516, 26, -1), (516, 562, 22, 1), (584, 662, 15, 1)],
        'undercity': [(850, 894, 28, -1), (924, 980, 18, 1), (980, 1010, 30, -1), (1085, 1119, 22, 1)]}.get(ZONE, [])
def centre(x0, x1, r, s):
    xm = (x0 + x1) / 2; p = Z.W(xm, 0, 0); q = Z.W(xm, 0, 1); n = (q[0] - p[0], q[2] - p[2])
    return (p[0] - s * n[0] * r, p[2] - s * n[1] * r)
if ZONE == 'foundry':
    STEEL_L = mat('lsteel', srgb('#6b6f7a'), metal=0.4, rough=0.55); RUSTL = mat('lrust', srgb('#8a5a3c'), rough=0.8)
    MOLT = mat('lmolten', srgb('#ff8a2a'), emit=srgb('#ff6a10'), strength=4)
    for x0, x1, r, s in ARCS:
        cx, cz = centre(x0, x1, r, s); R = r - 4.2
        if s > 0 and r < 18:
            cyl('core', mat('lcore', srgb('#d8dde4'), rough=0.35), V((cx, -17, cz)), V((cx, 53, cz)), R + 0.8, 48, r2=R)
            for y in range(-2, 50, 6): cyl('coreglow', MOLT, V((cx, y, cz)), V((cx, y + 0.5, cz)), R + 0.45, 48)
        elif s > 0:
            patch('dome', RUSTL, V((cx, -2, cz)), (R, R, R), v=(0, 90), res=(48, 12), thick=0, subsurf=0)
            cyl('domebase', STEEL_L, V((cx, -6, cz)), V((cx, -2, cz)), R, 48)
        else:
            for x in [x0 + k * 3.2 for k in range(int((x1 - x0) / 3.2) + 1)]: pbox('wall', STEEL_L, x, 2, -7.5, (3.4, 1.4, 22))
    for x in range(400, 764, 3): pbox('channel', MOLT, x + 1.5, -9, -1, (3.4, 5, 0.4))
if ZONE == 'undercity':
    WIN = mat('lwin', srgb('#1b1f33'), emit=srgb('#ffd9a0'), strength=0.3); SHELL = mat('lshell', srgb('#a7a9bd'), rough=0.85)
    CONCL = mat('lconc', srgb('#8d8fa3'), rough=0.9)
    import random; random.seed(5)
    for x0, x1, r, s in ARCS:
        if s > 0:
            cx, cz = centre(x0, x1, r, s); R = r - 4.2
            for k in range(12):
                t0, t1 = k / 12, (k + 1) / 12
                cyl('cooling', SHELL, V((cx, -20 + 56 * t0, cz)), V((cx, -20 + 56 * t1, cz)), R * (1 - 0.32 * math.sin(t0 * math.pi * 0.95)), 48, r2=R * (1 - 0.32 * math.sin(t1 * math.pi * 0.95)))
        else:
            for x in [x0 + k * 5 for k in range(int((x1 - x0) / 5) + 1)]:
                hh = 26 + random.random() * 18; pbox('block', WIN, x, -6 + hh / 2, -9, (4.6, 4, hh))
    for x in range(800, 1181, 9):
        if any(a <= x < b for a, b, r, s in ARCS if s < 0): continue
        for d in (-(12 + random.random() * 10), -(30 + random.random() * 20)):
            hh = 20 + random.random() * 50; ww = 5 + random.random() * 5; pbox('city', WIN, x, -20 + hh / 2, d, (ww, ww, hh))
    for x in [1010 + k * 2.5 for k in range(30)]:
        pbox('rail', CONCL, x, 9, -1.5, (2.6, 0.5, 0.3))
        if round(x - 1010) % 10 == 0: p = Z.W(x, 1, -6.5); box('col', CONCL, V((p[0], 2, p[2])), (0.4, 0.4, 14), 0)
    for i in range(3):
        x = 1030 + i * 9; p = Z.W(x, 1.6, -5); o = cyl('train', mat('ltrain', srgb('#e9edf3'), rough=0.35), V(Z.W(x - 4.6, 1.6, -5)), V(Z.W(x + 4.6, 1.6, -5)), 1.4, 16)

# ---- World, sun, mist (each route's light: Look.ATMOS) ----
ATM = {'foundry': ('#d99a6a', '#ffc690', '#e6c3a0'), 'undercity': ('#6d5f9e', '#ffb7c9', '#8e8fb8')}.get(ZONE, ('#a9d4f2', '#ffeed6', '#e4f1fa'))
w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (*srgb(ATM[0]), 1); w.node_tree.nodes['Background'].inputs[1].default_value = 1.0
sun = bpy.data.lights.new('sun', 'SUN'); sun.energy = 4.0 if ZONE != 'undercity' else 2.2; sun.angle = math.radians(3); sun.color = srgb(ATM[1])
so = bpy.data.objects.new('sun', sun); sc.collection.objects.link(so)
so.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
sc.cycles.max_bounces = 4; sc.cycles.transmission_bounces = 4
sc.render.resolution_x, sc.render.resolution_y = 1280, 720
sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Medium High Contrast'
sc.view_layers[0].use_pass_mist = True; sc.world.mist_settings.start = 14; sc.world.mist_settings.depth = 110; sc.world.mist_settings.falloff = 'QUADRATIC'
sc.use_nodes = True; nt = sc.node_tree; nt.nodes.clear()
rl = nt.nodes.new('CompositorNodeRLayers'); mix = nt.nodes.new('CompositorNodeMixRGB'); comp = nt.nodes.new('CompositorNodeComposite')
mix.blend_type = 'MIX'; mix.inputs[2].default_value = (*srgb(ATM[2]), 1)
mp = nt.nodes.new('CompositorNodeMath'); mp.operation = 'MULTIPLY'; mp.inputs[1].default_value = 0.5
nt.links.new(rl.outputs['Mist'], mp.inputs[0]); nt.links.new(mp.outputs[0], mix.inputs[0])
nt.links.new(rl.outputs['Image'], mix.inputs[1]); nt.links.new(mix.outputs[0], comp.inputs[0])

# the game's camera: 16 m out along the normal, 1.6 m above the framing height, 34 degrees of vertical field
cam = bpy.data.cameras.new('cam'); cam.sensor_fit = 'VERTICAL'; cam.angle_y = math.radians(34)
co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
def shoot(tag):
    for x, y in SHOTS:
        cy = y + 3; co.location = at(x, cy + 1.6, 16)
        co.rotation_euler = (at(x, cy, 0) - co.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = f'{OUT}_{tag}_{int(x)}.png'; bpy.ops.render.render(write_still=True)
if not ONLY_AFTER: shoot('before')

# ---- The kit, placed by the layout ----
with bpy.data.libraries.load(os.path.join(KITDIR, ZONE + '_kit.blend'), link=False) as (src, dst):
    dst.collections = [c for c in src.collections if c.startswith('Kit_')]
kits = {c.name[4:]: c for c in dst.collections}
TEX = os.path.join(KITDIR, 'tex')
SURF = {'Kit_Hull': ('paint', 2.5), 'Kit_HullB': ('paint', 2.5), 'Kit_HullDark': ('paint', 2.5), 'Kit_Navy': ('paint', 2.5),
        'Kit_Metal': ('paint', 1.5), 'Kit_PadOrange': ('rubber', 0.8), 'Kit_Grip': ('tread', 0.5),
        'Kit_Steel': ('paint', 2.0), 'Kit_SteelLight': ('paint', 2.0), 'Kit_Gunmetal': ('worn', 2.0), 'Kit_Rust': ('worn', 1.5),
        'Kit_Concrete': ('worn', 3.0), 'Kit_Plaster': ('worn', 3.0), 'Kit_Pave': ('worn', 3.0), 'Kit_Dumpster': ('worn', 1.5),
        'Kit_Wood': ('worn', 1.0), 'Kit_Grate': ('tread', 0.6), 'Kit_Copper': ('paint', 1.5)}
for m in bpy.data.materials:
    if m.name in SURF and os.path.exists(os.path.join(TEX, SURF[m.name][0] + '_albedo.png')): surface(m, TEX, *SURF[m.name])
sm = bpy.data.materials.get('Kit_Shadow')
if sm:          # the contact shadow: black, fading out from the middle
    nt = sm.node_tree; bsdf = nt.nodes['Principled BSDF']; g = nt.nodes.new('ShaderNodeTexGradient'); g.gradient_type = 'SPHERICAL'
    tc = nt.nodes.new('ShaderNodeTexCoord'); mp2 = nt.nodes.new('ShaderNodeMapping'); mp2.inputs['Scale'].default_value = (2, 2, 2)
    nt.links.new(tc.outputs['Object'], mp2.inputs['Vector']); nt.links.new(mp2.outputs['Vector'], g.inputs['Vector'])
    k = nt.nodes.new('ShaderNodeMath'); k.operation = 'MULTIPLY'; k.inputs[1].default_value = 0.6
    nt.links.new(g.outputs['Fac'], k.inputs[0]); nt.links.new(k.outputs[0], bsdf.inputs['Alpha'])
for name, x, y, dz, yaw, sx, sy, sz in Z.placements(ZONE):
    e = bpy.data.objects.new(name, None); e.instance_type = 'COLLECTION'; e.instance_collection = kits[name]
    e.location = at(x, y, dz); e.rotation_euler = (0, 0, Z.yaw_at(x) + math.radians(yaw)); e.scale = (sx, sz, sy)
    sc.collection.objects.link(e)
shoot('after')
print('rendered')
