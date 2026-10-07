# Where the Movement Gym's kit pieces go, worked out from the level's boxes (Sim/Level.cs, the gym's part). Both
# the Blender preview (render_gym.py) and the game (export_kit.py writes this list; GymDressing.cs places it) use
# it, so what the preview shows is what the game builds.
# Each placement: (piece, x along the route, y up, dz toward the camera, yaw in degrees, scale x, y, z).
import math

GROUND = [(-10, 14, -6, 0), (18, 32, -6, 0), (32, 40, -6, 6), (40, 60, -6, 0)]   # (x0, x1, y0, y1), front at dz 2.2
CAP = 0.22
TERRACE_Y = -1.1
MIDS = ('DeckFaceVent', 'DeckFaceRib', 'DeckFaceHatch', 'DeckFaceRib')

def placements():
    P = []
    def put(name, x, y, dz, yaw=0, sx=1, sy=1, sz=1): P.append((name, x, y, dz, yaw, sx, sy, sz))
    k = 0
    for x0, x1, y0, y1 in GROUND:
        top = y1 - CAP
        for i in range(int((x1 - x0) / 2)):
            x = x0 + 1 + 2 * i
            put('DeckFaceTop', x, top, 2.2)
            put('DeckFloor', x, y1, 0.0)
            h = (top - 1.2) - y0; n = max(1, math.ceil(h / 2 - 0.15)); s = h / (2 * n)
            for j in range(n):
                put(MIDS[(k + j) % len(MIDS)], x, top - 1.2 - j * 2 * s, 2.2, 0, 1, s, 1)
            k += 1
    # the slide tunnel's block gets the same facade, above its warning band
    for i in range(3):
        x = 45 + 2 * i; top = 6.5 - CAP
        put('DeckFaceTop', x, top, 2.2)
        h = (top - 1.2) - 1.3; put(MIDS[i % len(MIDS)], x, top - 1.2, 2.2, 0, 1, h / 2, 1)
    # floor markings: the start line, chevrons into the slide tunnel, hazard stripes at the drops
    put('StartLine', 0.8, 0.0, 0.0)
    for x in (40.9, 42.6): put('Chevrons', x, 0.0, 0.0)
    for x, y in ((13.85, 0), (18.15, 0), (32.15, 6), (39.85, 6)):
        for dz in (-1.1, 1.1): put('HazardEdge', x, y, dz, 90)
    # the wall-jump panel and the slide tunnel's lower edge
    put('WallPanelDress', 28.9, 5.35, 0.9)
    put('TunnelEdge', 47.0, 1.0, 2.2)
    # the back terrace: training gear, a railing along its back edge, floodlight masts, and the sign beyond
    for name, x, dz, yaw in (('Hurdle', 2.0, -4.0, 0), ('Hurdle', 3.7, -4.0, 0), ('CrashMat', 9.4, -6.6, 0), ('Bench', 12.8, -4.3, 0),
                             ('Lockers', 17.4, -8.6, 0), ('AgilityRing', 20.8, -6.4, 0), ('AgilityRing', 22.9, -6.9, 0),
                             ('TargetStand', 26.0, -4.4, 0), ('HoloDisplay', 30.9, -8.4, 0), ('CrashMat', 35.4, -6.2, 0),
                             ('Hurdle', 38.4, -4.1, 0), ('Lockers', 42.4, -8.6, 0), ('TargetStand', 46.1, -4.6, 0),
                             ('AgilityRing', 52.4, -6.5, 0), ('Bench', 55.2, -4.3, 0)):
        put(name, x, TERRACE_Y, dz, yaw)
    for i in range(36): put('Railing', -10 + 1 + 2 * i, TERRACE_Y, -10.2)
    for x in (-6, 16, 38, 58): put('FloodMast', x, TERRACE_Y, -9.5)
    put('GymSign', 28.0, 8.0, -15.0)
    return P
