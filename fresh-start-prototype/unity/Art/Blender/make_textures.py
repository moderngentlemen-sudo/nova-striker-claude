# Tileable surface textures, made with numpy: painted metal (orange peel, light wear, fine scratches, faint grime),
# battle-worn metal (RAM: heavy scratches, chipped paint, grime), pebbled rubber, diamond tread plate, and a soft round contact shadow.
# Each set is height, albedo (grey detail the material colour multiplies) and roughness, 512 x 512, written as
# <kind>.bytes for the game (Look.Surface reads them: three planes of 8-bit values, height then albedo then
# roughness; the shadow is one plane of alpha) and as PNGs for the Blender preview.
# Run: blender -b -P make_textures.py -- <game dir> <preview dir>     (or plain python3 with numpy and Pillow)
import os, sys
import numpy as np
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
GAME, PREV = args[0], args[1]
N = 512
rng = np.random.default_rng(11)

def fbm(octaves=5, base=4, persist=0.5):
    """Tileable fractal noise in 0..1 (bilinear value noise on periodic grids)."""
    out = np.zeros((N, N)); amp = 1; tot = 0
    for o in range(octaves):
        g = base * 2 ** o; grid = rng.random((g, g))
        y = np.linspace(0, g, N, endpoint=False); x0 = np.floor(y).astype(int); f = y - x0; f = f * f * (3 - 2 * f)
        x1 = (x0 + 1) % g
        a = grid[x0][:, x0]; b = grid[x0][:, x1]; c = grid[x1][:, x0]; d = grid[x1][:, x1]
        fx = f[None, :]; fy = f[:, None]
        out += amp * ((a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy); tot += amp; amp *= persist
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-9)

def lines(count, length, width, img, value):
    """Thin random scratches drawn into img (wrapping)."""
    for _ in range(count):
        x, y = rng.random() * N, rng.random() * N; a = rng.random() * np.pi; L = length * (0.3 + rng.random())
        for t in np.linspace(0, L, int(L * 2)):
            px = int(x + np.cos(a) * t) % N; py = int(y + np.sin(a) * t) % N
            img[py, px] = value; 
            if width > 1: img[(py + 1) % N, px] = value

def save(kind, height, albedo, rough):
    os.makedirs(GAME, exist_ok=True); os.makedirs(PREV, exist_ok=True)
    planes = [np.clip(p, 0, 1) for p in (height, albedo, rough)]
    with open(os.path.join(GAME, kind + '.bytes'), 'wb') as f:
        for p in planes: f.write((p * 255).astype(np.uint8).tobytes())
    try:
        from PIL import Image
        for nm, p in zip(('height', 'albedo', 'rough'), planes): Image.fromarray((p * 255).astype(np.uint8)).save(os.path.join(PREV, f'{kind}_{nm}.png'))
    except ImportError:
        import bpy
        for nm, p in zip(('height', 'albedo', 'rough'), planes):
            im = bpy.data.images.new(f'{kind}_{nm}', N, N); px = np.repeat(p[::-1, :, None], 4, axis=2); px[..., 3] = 1
            im.pixels = px.ravel().tolist(); im.filepath_raw = os.path.join(PREV, f'{kind}_{nm}.png'); im.file_format = 'PNG'; im.save()

# Painted metal: orange peel, broad faint grime, sparse scratches that catch the light
peel = fbm(3, 64, 0.5); broad = fbm(5, 3, 0.55); dents = fbm(4, 12, 0.5)
h = 0.5 + 0.05 * (peel - 0.5) + 0.06 * (dents - 0.5)
alb = 1 - 0.07 * np.clip(broad - 0.45, 0, 1) * 2 - 0.02 * (peel - 0.5)
r = 0.85 + 0.12 * (broad - 0.5) + 0.05 * (peel - 0.5)
scr = np.zeros((N, N)); lines(90, 60, 1, scr, 1.0)
h -= 0.05 * scr; r -= 0.35 * scr; alb -= 0.03 * scr
save('paint', h, alb, r)

# Rubber: pebbled, matte
peb = fbm(2, 128, 0.5); h = 0.5 + 0.25 * (peb - 0.5); alb = 1 - 0.06 * (fbm(4, 4) - 0.5); r = 0.95 - 0.05 * peb
save('rubber', h, alb, r)

# Diamond tread plate: raised diamonds on a staggered grid, worn brighter on their tops
yy, xx = np.mgrid[0:N, 0:N] / N
cell = 1 / 16; h = np.full((N, N), 0.4)
for flip in (0, 1):                                   # diamonds lean one way, and the other on the offset grid
    ox = cell / 2 * flip; oy = cell / 2 * flip
    u = (xx + ox) % cell / cell - 0.5; v = (yy + oy) % cell / cell - 0.5
    ang = np.pi / 4 if flip == 0 else -np.pi / 4
    ru = u * np.cos(ang) - v * np.sin(ang); rv = u * np.sin(ang) + v * np.cos(ang)
    d = (np.abs(ru) / 0.3) ** 2 + (np.abs(rv) / 0.08) ** 2
    h = np.maximum(h, np.where(d < 1, 0.4 + 0.5 * np.clip(1 - d, 0, 1) ** 0.4, 0.4))
h += 0.04 * (fbm(4, 8) - 0.5)
alb = 0.9 + 0.1 * (h - 0.4) - 0.06 * fbm(5, 3); r = 0.75 - 0.4 * np.clip(h - 0.45, 0, 1)
save('tread', h, alb, r)

# Contact shadow: a soft round falloff (alpha)
d = np.hypot(xx - 0.5, yy - 0.5) * 2
sh = np.clip(1 - d, 0, 1) ** 1.8
os.makedirs(GAME, exist_ok=True)
with open(os.path.join(GAME, 'shadow.bytes'), 'wb') as f: f.write((sh * 255).astype(np.uint8).tobytes())
try:
    from PIL import Image; Image.fromarray((sh * 255).astype(np.uint8)).save(os.path.join(PREV, 'shadow.png'))
except ImportError: pass
# Battle-worn metal (RAM): heavy scratches, chipped paint showing bright bare metal, scuffs and grime
peel = fbm(3, 48, 0.5); grime = fbm(5, 3, 0.6); chipN = fbm(4, 10, 0.55)
h = 0.5 + 0.04 * (peel - 0.5)
alb = 1 - 0.16 * np.clip(grime - 0.4, 0, 1) * 2
r = 0.8 + 0.1 * (grime - 0.5)
chips = np.clip((chipN - 0.68) / 0.08, 0, 1)                      # bare metal where the paint has chipped
h -= 0.06 * chips; alb = alb * (1 - chips) + 1.35 * chips; r = r * (1 - chips) + 0.35 * chips
scr = np.zeros((N, N)); lines(260, 80, 1, scr, 1.0); lines(40, 140, 2, scr, 1.0)
h -= 0.07 * scr; r -= 0.4 * scr; alb += 0.25 * scr
save('worn', h, np.clip(alb, 0, 1.0), r)

print('textures written')
