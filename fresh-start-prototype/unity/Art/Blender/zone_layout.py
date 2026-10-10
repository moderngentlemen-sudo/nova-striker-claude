# Where each zone's kit pieces go (build_zone_kit.py models them), worked out from the level's real boxes, hazards,
# catwalks and lane stretches (level_boxes.json, written by SimTests/LevelDump). Both the Blender preview
# (render_zone.py) and the game (export_kit.py writes this list; ZoneDressing.cs places it) use it, so what the
# preview shows is what the game builds.
# Each placement: (piece, x along the route, y up, dz toward the camera, yaw in degrees, scale x, y, z), as in
# gym_layout.py. The game puts a piece at S.W(x, y, dz) turned by S.YawAt(x) + yaw.
# The rules: facades on the solid boxes' front faces (dz 2.2) and plating on their tops, as the gym has; props stand
# behind the play (on lower terraces, cantilevered pads, the Storm Spire's wall) or at the back of a deck, and a deck
# only takes props outside the lane stretches and clear of hazards, gates, breakables and low catwalks. Nothing
# stands between the play and the camera above deck height. The level features' boxes and hazards are not dressed
# (LevelFx draws them).
import json, math, os

D = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'level_boxes.json')))
BOXES, EXTRA, HAZ, LANES, FR = D['boxes'], D['extra'], D['hazards'], D['lanes'], D['frames']
ZONES = {z['id']: z for z in D['zones']}
CAP = 0.22                    # the deck's cap (the game draws it); facades start under it
FRONT = 2.2                   # the solid boxes' front face (4.4 m deep)
BACK = -1.75                  # where a prop stands at the back of a deck
THIN = ('column', 'wall', 'panel', 'pillar', 'bound')   # in-play pillars and walls: not dressed

# ---- The path (the game's Level.Frame, sampled every 0.5 m) ----
def frame(x):
    f = (x + 20) / 0.5; i = max(0, min(len(FR) - 2, int(math.floor(f)))); t = f - i
    a, b = FR[i], FR[i + 1]
    px, pz = a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t
    tx, tz = a[3] + (b[3] - a[3]) * t, a[4] + (b[4] - a[4]) * t; l = math.hypot(tx, tz)
    return px, pz, tx / l, tz / l
def W(x, y, dz):
    """Route coordinates to the game's three.js space (S.W)."""
    px, pz, tx, tz = frame(x)
    return (px - tz * dz, y, pz + tx * dz)
def yaw_at(x):
    _, _, tx, tz = frame(x); return math.atan2(-tz, tx)
def stretch(x, dz):
    """How much longer than 1 m of route a metre is at depth dz (round a bend, the outer side is longer)."""
    a, b = W(x - 0.5, 0, dz), W(x + 0.5, 0, dz)
    return math.hypot(b[0] - a[0], b[2] - a[2])

def in_zone(zone, b):
    z = ZONES[zone]; return b['x0'] >= z['x0'] - 0.01 and b['x1'] <= z['x1'] + 0.01
def solids(zone):
    return [b for b in BOXES if b['type'] == 's' and b['tag'] not in THIN and in_zone(zone, b) and b['x0'] >= (400 if zone == 'foundry' else -1e9)]
def sitting_on(b):
    """Solid boxes standing on b's top."""
    return [o for o in BOXES if o is not b and o['type'] == 's' and abs(o['y0'] - b['y1']) < 0.01 and o['x1'] > b['x0'] and o['x0'] < b['x1']]
def deck_at(zone, x0, x1):
    """The solid deck under a span, if one holds all of it."""
    for b in solids(zone):
        if b['x0'] + 0.2 <= x0 and x1 <= b['x1'] - 0.2: return b
    return None
def overlaps(a0, a1, b0, b1): return a1 > b0 and a0 < b1
def in_lanes(x0, x1, pad=0.5): return any(overlaps(x0, x1, l0 - pad, l1 + pad) for l0, l1 in LANES)

def blocked(zone, x0, x1, y, on_deck=True):
    """Whether a prop over x0..x1 at the back of a deck (or just behind it) would get in the way."""
    if on_deck and in_lanes(x0, x1): return True
    for h in HAZ:
        if h['kind'] == 'wind': spans = [(h['x0'] - 0.7, h['x0'] + 0.7), (h['x1'] - 0.7, h['x1'] + 0.7)]   # (its posts)
        elif h['kind'] == 'collapse': spans = [(h['x0'] - 0.3, h['x1'] + 0.3)] if h['y0'] - y < 2.2 else []
        else: spans = [(h['x0'] - 1.0, h['x1'] + 1.0)]
        if any(overlaps(x0, x1, a, b) for a, b in spans): return True
    for b in BOXES:
        if not overlaps(x0, x1, b['x0'] - 0.6, b['x1'] + 0.6): continue
        if b['type'] in 'gd' and b['y1'] > y - 0.5 and b['y0'] < y + 2.5: return True      # gates, breakables
        if b['type'] in 'so' and b['y0'] > y - 0.01 and b['y0'] - y < 2.2: return True      # boxes standing on it, low ledges
    for e in EXTRA:
        if overlaps(x0, x1, e['x0'] - 0.3, e['x1'] + 0.3) and -0.5 < e['y0'] - y < 2.2: return True
    return False

class Layout:
    def __init__(self, zone): self.zone, self.P = zone, []
    def put(self, name, x, y, dz, yaw=0, sx=1, sy=1, sz=1):
        self.P.append((name, round(x, 3), round(y, 3), round(dz, 3), round(yaw, 2), round(sx, 4), round(sy, 4), round(sz, 4)))
    def shadow(self, x, y, dz, fx, fz): self.put('Shadow', x, y, dz, 0, fx, 1, fz)
    def modules(self, x0, x1, size=2.0):
        """Centres and widths of equal modules filling x0..x1."""
        n = max(1, round((x1 - x0) / size)); w = (x1 - x0) / n
        return [(x0 + w * (i + 0.5), w) for i in range(n)]

    def facades(self, mids):
        """Facade panels over every solid box's front face, plating on its top (as gym_layout does from GROUND)."""
        k = 0
        for b in solids(self.zone):
            y0, y1 = b['y0'], b['y1']; top = y1 - CAP; h_all = y1 - y0
            for x, w in self.modules(b['x0'], b['x1']):
                sx = w / 2 * stretch(x, FRONT)
                if h_all >= 1.6:
                    self.put('FaceTop', x, top, FRONT, 0, sx)
                    h = (top - 1.2) - y0
                    if h > 0.25:
                        n = max(1, math.ceil(h / 2 - 0.15)); s = h / (2 * n)
                        for j in range(n): self.put('Face' + mids[(k + j) % len(mids)], x, top - 1.2 - j * 2 * s, FRONT, 0, sx, s)
                else: self.put('FaceTop', x, top, FRONT, 0, sx, max(0.2, (h_all - CAP) / 1.2))
                k += 1
    def floors(self, pick):
        for b in solids(self.zone):
            over = sitting_on(b)
            for i, (x, w) in enumerate(self.modules(b['x0'], b['x1'])):
                if any(o['x0'] - 0.5 < x < o['x1'] + 0.5 for o in over): continue
                name = pick(b, x, i)
                if name: self.put(name, x, b['y1'], 0, 0, w / 2 * stretch(x, 0))
    def deck_prop(self, name, x, half, fz=None, dz=BACK, yaw=0, shadow=True):
        """A prop at the back of a deck, if a deck holds it and nothing forbids it there. Returns whether it went in."""
        b = deck_at(self.zone, x - half, x + half)
        if b is None or b in [] or blocked(self.zone, x - half, x + half, b['y1']): return False
        self.put(name, x, b['y1'], dz, yaw)
        if shadow and fz: self.shadow(x, b['y1'], dz, 2 * half + 0.6, fz)
        return True
    def run(self, name, x0, x1, y, dz, size=2.0, skip=None, alt=0.0):
        """A row of 2 m modules along x0..x1 at a depth, stretched to meet round bends."""
        for i, (x, w) in enumerate(self.modules(x0, x1, size)):
            if skip and skip(x, w): continue
            self.put(name, x, y + (alt if i % 2 else 0), dz, 0, w / size * stretch(x, dz))
    def chord(self, xa, xb, y, dz):
        """The yaw (relative to the path at xa) and length of a straight piece from xa to xb at a depth."""
        a, b = W(xa, y, dz), W(xb, y, dz); th = math.atan2(-(b[2] - a[2]), b[0] - a[0])
        d = math.degrees(th - yaw_at(xa)); d = (d + 180) % 360 - 180
        return d, math.hypot(b[0] - a[0], b[2] - a[2])

# =====================================================================================================================
def arena(L):
    L.facades('ABCB')
    L.floors(lambda b, x, i: 'FlatTransit')
    for x in (66.5, 81.5, 92.0): L.put('FlatArrow', x, 0.0, 0.0)
    gates = [b for b in BOXES if b['type'] == 'g' and in_zone('arena', b)]
    for g in gates:
        xm = (g['x0'] + g['x1']) / 2
        L.put('FlatThreshold', xm, g['y0'], 0.0); L.put('LockFrame', xm, g['y0'], 0.0)
    laser = [(h['x0'] - 1.2, h['x1'] + 1.2) for h in HAZ if h['zone'] == 'arena' and h['kind'] == 'laser']
    gx = [((g['x0'] + g['x1']) / 2) for g in gates]
    # glass balustrades on the deck's back edge (behind the lanes) and along the back of the gym's terrace
    L.run('Balustrade', gx[0] + 1.45, gx[1] - 1.45, 0.0, -2.2, skip=lambda x, w: any(overlaps(x - w / 2, x + w / 2, a, b) for a, b in laser))
    L.run('Balustrade', 61.0, 97.0, -1.1, -10.25)
    # holo advertising columns and benches on the terrace (clear of its planters at x = 62, 73, 84, 95 and lamp posts)
    for x in (65.5, 75.5, 92.5): L.put('HoloColumn', x, -1.1, -3.7); L.shadow(x, -1.1, -3.7, 1.8, 1.8)
    for x in (69.0, 87.0): L.put('TransitBench', x, -1.1, -6.3); L.shadow(x, -1.1, -6.3, 2.3, 0.9)
    # mezzanine supports under the catwalks: a column on the terrace, its arm under the catwalk
    for e in EXTRA:
        if not in_zone('arena', e) or e['tag'] != 'tier': continue
        for x in (e['x0'] + 0.7, e['x1'] - 0.7):
            yb, yt = -1.1 + 0.42, e['y0'] - 0.6
            L.put('MezzBase', x, -1.1, -2.75); L.put('MezzShaft', x, yb, -2.75, 0, 1, yt - yb); L.put('MezzHead', x, e['y0'] - 0.05, -2.75)
    L.put('HallSign', 72.0, 9.6, -14.0)

# ---- Storm Spire: the tower the path wraps round (View.BuildBackdrop): a cylinder at the arc's centre ----
TOWER_C = (104.0, -14.0)
def tower_r(y): return 11.6 - (y + 35) / 110            # (radius 11.6 at its foot, y = -35, to 10.6 at the top, y = 75)
def tower_dz(x, y):
    """The depth of the tower's wall behind the path at x (None where it is not right behind)."""
    for k in range(0, 200):
        dz = -2.2 - k * 0.02; p = W(x, y, dz)
        if math.hypot(p[0] - TOWER_C[0], p[2] - TOWER_C[1]) <= tower_r(y): return dz
    return None

def tower(L):
    L.facades('ABCB')
    L.floors(lambda b, x, i: 'FlatGrate')
    # a cable tray along the wall, above the decks and below the catwalks' brackets
    for x, w in L.modules(100.0, 143.0):
        d = tower_dz(x, 4.4)
        if d is not None and d > -3.3: L.put('CableTray', x, 4.4, d + 0.19, 0, w / 2 * stretch(x, d + 0.19))
    # ledge brackets under the catwalks, from the wall
    for e in EXTRA:
        if not in_zone('tower', e) or e['tag'] != 'tier': continue
        for x in (e['x0'] + 0.5, e['x1'] - 0.5):
            d = tower_dz(x, e['y0'])
            if d is not None: L.put('LedgeBracket', x, e['y0'] - 0.02, d)
    # storm shutters on the wall where nothing hangs above (clear of the lightning hazards' rods)
    for x in (120.5, 128.0, 135.5):
        d = tower_dz(x, 1.75)
        if d is not None: L.put('StormShutter', x, 0.0, d + 0.1)
    # lightning rods: on the wall above the top deck, and at the back of the deck where the wall turns away
    d = tower_dz(145.5, 15.6)
    if d is not None: L.put('LightningRod', 145.5, 15.6, d + 0.3)
    L.deck_prop('LightningRod', 160.0, 0.3, 0.8)
    L.deck_prop('Anemometer', 99.0, 0.6, 1.0); L.deck_prop('WeatherBox', 101.0, 0.5, 0.9)
    L.deck_prop('Anemometer', 150.5, 0.6, 1.0); L.deck_prop('WeatherBox', 155.5, 0.5, 0.9); L.deck_prop('Anemometer', 158.0, 0.6, 1.0)

def skyline(L):
    L.facades('ABCB')
    L.floors(lambda b, x, i: 'FlatRoofB' if i % 4 == 2 else 'FlatRoof')
    # maintenance gantries under the decks that hang in the air
    for b in solids('skyline'):
        if b['y0'] < 0 or any(abs(o['y1'] - b['y0']) < 0.01 and overlaps(o['x0'], o['x1'], b['x0'], b['x1']) for o in BOXES if o['type'] == 's'): continue
        L.run('GantryUnder', b['x0'], b['x1'], b['y0'], FRONT)
    # cantilevered pads behind the decks with relay towers and dishes (clear of the masts and the beacon)
    for x, what in ((165.5, 'Dish'), (177.0, 'RelayTower'), (205.5, 'Dish'), (233.0, 'Dish'), (271.0, 'RelayTower'), (285.0, 'Dish'), (304.0, 'RelayTower')):
        b = deck_at('skyline', x - 1.5, x + 1.5)
        if b is None: continue
        L.put('RelayPad', x, b['y1'], -2.2); L.put(what, x, b['y1'] - 0.02, -4.3 if what == 'Dish' else -3.9)
    # rooftop props at the backs of the decks, outside the lanes
    for name, x, half, fz in (('WindSock', 163.5, 0.3, 0), ('AntennaArray', 172.0, 0.6, 0.9), ('RoofVent', 181.0, 0.8, 1.2),
                              ('RoofVent', 221.0, 0.8, 1.2), ('AntennaArray', 226.0, 0.6, 0.9), ('WindSock', 242.5, 0.3, 0),
                              ('AntennaArray', 246.0, 0.6, 0.9), ('WindSock', 249.5, 0.3, 0), ('RoofVent', 255.5, 0.8, 1.2),
                              ('RoofVent', 300.6, 0.8, 1.2), ('AntennaArray', 306.0, 0.6, 0.9), ('WindSock', 314.0, 0.3, 0)):
        L.deck_prop(name, x, half, fz, dz=-1.8 if name != 'WindSock' else -1.9)

# ---- What stands behind the winding routes (Landmarks.cs): at the centre of each bend away from the camera a structure
# (the Foundry's furnace dome and reactor core, the Undercity's cooling towers), behind each bend toward it a wall of
# machinery or buildings 6.8 m back, and along the straights blocks 8.7 m back or more ----
ARCS = {'foundry': [(464, 516, 26, -1), (516, 562, 22, 1), (584, 662, 15, 1)],
        'undercity': [(850, 894, 28, -1), (924, 980, 18, 1), (980, 1010, 30, -1), (1085, 1119, 22, 1)]}
def arc_centre(x0, x1, r, s):
    xm = (x0 + x1) / 2; p = W(xm, 0, 0); q = W(xm, 0, 1)
    return (p[0] - s * (q[0] - p[0]) * r, p[2] - s * (q[2] - p[2]) * r)
def landmark_free(zone, x, y, dz, pad=0.3):
    """Whether a point is clear of the set pieces behind the path."""
    p = W(x, y, dz)
    for x0, x1, r, s in ARCS.get(zone, []):
        if s < 0:
            if x0 - 1 <= x < x1 + 1 and dz < -6.6 + pad: return False
            continue
        cx, cz = arc_centre(x0, x1, r, s); d = math.hypot(p[0] - cx, p[2] - cz); R = r - 4.2
        if zone == 'foundry' and r < 18: rad = R + 0.8                                   # the reactor core
        elif zone == 'foundry': rad = math.sqrt(max(0.0, R * R - (y + 2) ** 2)) if y > -2 else R   # the dome
        else: t = min(1, max(0, (y + 20) / 56)); rad = R * (1 - 0.32 * math.sin(t * math.pi * 0.95))   # a cooling tower
        if d < rad + pad: return False
    return dz > {'foundry': -8.75, 'undercity': -6.5}.get(zone, -1e9)
def back_room(zone, x, y, deepest=-8.6):
    """How far back (a negative depth) the space behind the deck at x is free, from the deck's back edge."""
    dz = -2.3
    while dz > deepest and landmark_free(zone, x, y, dz - 0.1) and landmark_free(zone, x, y + 6, dz - 0.1): dz -= 0.1
    return dz

def foundry(L):
    L.facades('ABCB')
    L.floors(lambda b, x, i: 'FlatFoundry')
    for x, y in ((429.85, 0.0), (433.15, 0.0)):
        for dz in (-1.1, 1.1): L.put('FlatHazEdge', x, y, dz, 90)
    for b in solids('foundry'):
        L.run('RailBack', b['x0'] + 0.3, b['x1'] - 0.3, b['y1'], -2.2)
        if b['tag'] in ('landing', 'bridge') or b['x1'] - b['x0'] < 6: continue
        # the lower terrace, in stretches as deep as the space behind allows
        x = b['x0'] + 0.3; spans = []
        def kind(x):
            room = min(back_room('foundry', x + d, b['y1']) for d in (-1.2, 0, 1.2))
            return 'full' if room <= -8.55 else 'short' if room <= -6.25 else None
        while x < b['x1'] - 0.3:
            k = kind(x); x1 = x
            while x1 < b['x1'] - 0.3 and kind(x1) == k: x1 += 0.5
            x1 = min(x1, b['x1'] - 0.3)
            if k and x1 - x >= 4: spans.append((x, x1, -8.6 if k == 'full' else -6.5))
            x = x1 + (0.5 if x1 == x else 0)
        ty = b['y1'] - 0.6
        for x0, x1, lim in spans:
            full = lim <= -8.6; name, depth = ('Terrace', 6.3) if full else ('TerraceS', 4.0)
            L.run(name, x0, x1, ty, -2.3, alt=0.004)
            for i, (x, w) in enumerate(L.modules(x0, x1)):
                if i % 3 == 1:
                    for dz in (-3.0, -2.3 - depth + 0.6): L.put('TerraceLeg', x, ty - 0.3, dz, 0, 1, ty - 0.3 + 9.0)
            back = -2.3 - depth
            L.run('PipeRun', x0 + 0.5, x1 - 0.5, ty, back + 0.4)
            cols = []
            if full and (x1 - x0) >= 14:       # a crane girder on columns above the terrace, with a hook hanging from it
                gy = ty + 10.5; gx = L.modules(x0 + 1, x1 - 1, 6.0)
                for x, w in gx: L.put('CraneRail', x, gy, -7.4, 0, w / 6)
                for x in [x0 + 1.5 + 12 * k for k in range(int((x1 - x0 - 3) / 12) + 1)] + [x1 - 1.5]:
                    if all(abs(x - c) > 4 for c in cols): cols.append(x); L.put('CraneColumn', x, ty, -7.4, 0, 1, gy - 0.4 - ty)
                L.put('CraneHook', gx[len(gx) // 2][0] + 1.2, gy, -7.4)
            # furnaces, crucibles and valve stations in turn along the terrace
            seq = ('Furnace', 'Crucible', 'ValveStation') if full else ('Crucible', 'ValveStation', 'Furnace')
            x, k = x0 + 2.6, 0
            while x < x1 - 2.2:
                what = seq[k % 3]; half = {'Furnace': 1.9, 'Crucible': 1.6, 'ValveStation': 0.8}[what]
                if x + half > x1 - 0.4: break
                dz = {'Furnace': -5.6 if full else -4.8, 'Crucible': -4.0, 'ValveStation': -6.6 if full else -5.5}[what]
                fits = all(landmark_free('foundry', x + ex, ty + ey, dz + ed) for ex in (-half, half) for ed in (-1.3, 1.3) for ey in (0.5, 4.0))
                if fits and not (what == 'ValveStation' and any(abs(x - c) < 1.4 for c in cols)):
                    L.put(what, x + 0.0, ty, dz)
                    L.shadow(x, ty, dz, 2 * half + 0.8, {'Furnace': 3.2, 'Crucible': 1.6, 'ValveStation': 1.0}[what])
                x += 2 * half + 3.4; k += 1

# ---- Undercity Descent: the buildings behind (Landmarks.cs) stand 7 m back or more; the transit line runs 1010-1085 with
# its train standing 3.6-6.4 m back and columns 6.5 m back ----
TRANSIT = (1010.0, 1085.0)
def undercity(L):
    L.facades('ABCB')
    def pave(b, x, i):
        if b['y1'] >= 14 and b['tag'] == 'roof': return 'FlatTarB' if i % 3 == 1 else 'FlatTar'
        if TRANSIT[0] <= x < TRANSIT[1]: return 'FlatPlatform'
        return 'FlatPaveB' if i % 3 == 1 else 'FlatPaveA'
    L.floors(pave)
    roofs = [b for b in solids('undercity') if b['tag'] == 'roof']
    for b in roofs:
        # fire escapes and slung cables on the buildings' fronts, below the roofline
        w = b['x1'] - b['x0']; xs = [b['x0'] + 3 + 13 * k for k in range(int((w - 6) / 13) + 1)] if w >= 6 else []
        for x in xs: L.put('FireEscape', x, b['y1'], FRONT)
        for x in xs:
            if x + 8.5 < b['x1'] - 0.5: L.put('CableDroop', x + 4.5, b['y1'] - CAP - 0.25, FRONT + 0.07, 0, stretch(x + 6.5, FRONT))
    # rooftop props and neon signs at the backs of the roofs and steps
    for name, x, half, fz in (('AcUnit', 806.0, 0.7, 1.0), ('WaterTank', 822.0, 0.8, 1.6), ('AcUnit', 832.0, 0.7, 1.0),
                              ('AcUnit', 856.0, 0.7, 1.0), ('WaterTank', 879.5, 0.8, 1.6), ('AcUnit', 891.5, 0.7, 1.0),
                              ('AcUnit', 908.0, 0.7, 1.0), ('WaterTank', 921.0, 0.8, 1.6), ('Crates', 928.0, 1.1, 1.2),
                              ('Dumpster', 942.0, 1.0, 1.3), ('Crates', 956.5, 1.1, 1.2), ('AcUnit', 970.0, 0.7, 1.0)):
        L.deck_prop(name, x, half, fz)
    for x in (814.0, 844.0, 884.0, 912.5, 949.0):
        b = deck_at('undercity', x - 0.5, x + 0.5)
        if b is not None and not blocked('undercity', x - 0.5, x + 0.5, b['y1'], on_deck=False): L.put('NeonBlade', x, b['y1'] - 3.0, -2.6)
    # the street: a lower walk behind it (the track bed along the transit line), with its props, poles and cables
    for b in solids('undercity'):
        if b['tag'] not in ('ground', 'plaza'): continue
        ty = b['y1'] - 0.6
        def walk(x, w): return TRANSIT[0] <= x < TRANSIT[1]
        L.run('Sidewalk', b['x0'], b['x1'], ty, -2.3, skip=walk, alt=0.004)
        L.run('TrackBed', max(b['x0'], TRANSIT[0]), min(b['x1'], TRANSIT[1]), b['y1'], -2.2, skip=lambda x, w: not walk(x, w)) if b['x0'] < TRANSIT[1] and b['x1'] > TRANSIT[0] else None
        poles = []
        for k, x in enumerate([b['x0'] + 3 + 12 * k for k in range(int((b['x1'] - b['x0'] - 4) / 12) + 1)]):
            if TRANSIT[0] - 1 <= x < TRANSIT[1] + 1: continue
            L.put('UtilityPole', x, ty, -5.4); poles.append(x)
            prop = ('Dumpster', 'NeonBoard', 'Crates', 'NeonBlade')[k % 4]
            px = x + 5.0
            if px < b['x1'] - 1.5 and not (TRANSIT[0] - 2 <= px < TRANSIT[1] + 2):
                dz = {'Dumpster': -3.3, 'Crates': -3.1, 'NeonBoard': -5.7, 'NeonBlade': -3.4}[prop]
                L.put(prop, px, ty, dz)
                if prop in ('Dumpster', 'Crates'): L.shadow(px, ty, dz, 2.6, 1.4)
        for xa, xb in zip(poles, poles[1:]):
            if xb - xa < 13:
                yaw, length = L.chord(xa, xb, ty + 7.1, -5.4)
                L.put('CableSpan', xa, ty + 7.1, -5.4, yaw, length / 6)

ZONE_FN = {'arena': arena, 'tower': tower, 'skyline': skyline, 'foundry': foundry, 'undercity': undercity}

def placements(zone):
    L = Layout(zone); ZONE_FN[zone](L); return L.P

if __name__ == '__main__':
    import collections, sys
    for z in (sys.argv[1:] or ZONE_FN):
        P = placements(z); print(z, len(P), dict(collections.Counter(p[0] for p in P)))
