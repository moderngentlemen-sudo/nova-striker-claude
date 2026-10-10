# Effect textures, made with numpy: billowing smoke and a fireball (8 x 8 flipbooks), sparks, embers, a soft glow,
# a shock ring, a tracer streak, a distortion ripple, scorch and scuff marks, lens dirt, and lit cloud puffs (four
# variants: density in alpha, the puff's surface normal in RGB). Each is written as a PNG named <name>.png.bytes
# under Resources/NovaStriker/Fx (FxTex.cs loads them; the .bytes ending keeps Unity's importer out of it) and as a
# plain .png preview.
# Run: python3 make_fx_textures.py <Resources/NovaStriker/Fx folder> <preview folder>
import math, os, sys
import numpy as np
from PIL import Image

OUT, PREV = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True); os.makedirs(PREV, exist_ok=True)
rng = np.random.default_rng(20261010)

def save(name, rgba):
    img = Image.fromarray(np.clip(rgba * 255 + 0.5, 0, 255).astype(np.uint8), 'RGBA')
    img.save(os.path.join(OUT, name + '.png.bytes'), 'PNG', optimize=True)
    img.save(os.path.join(PREV, name + '.png'))

def grid(n):
    y, x = np.mgrid[0:n, 0:n].astype(np.float64)
    return (x + 0.5) / n * 2 - 1, (y + 0.5) / n * 2 - 1

def value_noise(n, cells, seed):
    """Smooth tileable value noise, n x n, `cells` lattice cells across."""
    r = np.random.default_rng(seed).random((cells, cells))
    u = np.arange(n) / n * cells; i0 = np.floor(u).astype(int); f = u - i0; f = f * f * (3 - 2 * f)
    i1 = (i0 + 1) % cells
    a = r[np.ix_(i0, i0)]; b = r[np.ix_(i0, i1)]; c = r[np.ix_(i1, i0)]; d = r[np.ix_(i1, i1)]
    fx, fy = f[None, :], f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy

def fbm(n, cells, octaves, seed, gain=0.5):
    s, amp, tot = np.zeros((n, n)), 1.0, 0.0
    for o in range(octaves):
        s += amp * value_noise(n, cells * 2 ** o, seed + o * 101); tot += amp; amp *= gain
    return s / tot

def shade(dens, light=(-0.6, -0.7)):
    """Lit-looking grey from a density field: brighter where it faces the light (top left), darker in the folds."""
    gy, gx = np.gradient(dens)
    nx, ny = -gx * 6, -gy * 6; nz = np.ones_like(dens)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    d = (nx * light[0] + ny * light[1] + nz * 0.5) / l
    return np.clip(0.55 + 0.45 * d, 0, 1)

def flipbook(name, frames, size, make):
    cols = int(math.sqrt(frames)); sheet = np.zeros((size * cols, size * cols, 4))
    for k in range(frames):
        r, c = divmod(k, cols)
        sheet[r * size:(r + 1) * size, c * size:(c + 1) * size] = make(k / (frames - 1), k)
    save(name, sheet)

N = 128
X, Y = grid(N); R = np.sqrt(X * X + Y * Y)
SMOKE_N = [fbm(N, 4, 4, 1000 + k) for k in range(4)]

def smoke_frame(t, k):
    # a puff that billows out and thins: noise-broken sphere, growing, its edge eaten away as it fades
    rad = 0.62 + 0.32 * t
    n = SMOKE_N[k % 4] * 0.6 + SMOKE_N[(k + 1) % 4] * 0.4
    dens = np.clip((1 - (R / rad) ** 2) * 1.5 + (n - 0.5) * (1.0 + 0.8 * t), 0, 1) ** 1.1
    dens *= np.clip((1 - t) ** 0.9 * 1.05, 0, 1)
    g = shade(dens)
    return np.dstack([g, g, g, dens])

def fire_frame(t, k):
    rad = 0.55 + 0.38 * t
    n = SMOKE_N[k % 4]
    dens = np.clip((1 - (R / rad) ** 2) * 1.5 + (n - 0.5) * 1.1, 0, 1)
    heat = np.clip(dens * (1.6 - 1.5 * t) - (n - 0.5) * 0.4, 0, 1)    # the hot core cools from the outside in
    r = np.clip(0.25 + heat * 1.6, 0, 1); gch = np.clip(heat * 1.25 - 0.15, 0, 1); b = np.clip(heat * 1.4 - 0.75, 0, 1)
    smoke = (1 - heat) * 0.18                                           # cooled parts turn to dark smoke
    rgb = np.dstack([np.maximum(r * heat, smoke), np.maximum(gch * heat, smoke), np.maximum(b * heat, smoke)])
    a = np.clip(dens * (1.15 - 0.6 * t), 0, 1)
    return np.dstack([rgb, a])

flipbook('smoke', 64, N, smoke_frame)
flipbook('fireball', 64, N, fire_frame)

def radial(n, falloff):
    x, y = grid(n); r = np.sqrt(x * x + y * y); a = falloff(r)
    return np.dstack([np.ones((n, n))] * 3 + [np.clip(a, 0, 1)])
save('glow', radial(128, lambda r: np.clip(1 - r, 0, 1) ** 2.2))
save('spark', radial(64, lambda r: np.exp(-r * r * 18) + 0.35 * np.clip(1 - r, 0, 1) ** 3))
save('ember', radial(64, lambda r: np.exp(-r * r * 30)))
save('ring', radial(256, lambda r: np.exp(-((r - 0.78) / 0.07) ** 2) + 0.25 * np.exp(-((r - 0.7) / 0.18) ** 2)))

# a tracer streak: bright core, soft sides, fading along its length (64 x 256)
sx = np.linspace(-1, 1, 64)[None, :]; sy = np.linspace(0, 1, 256)[:, None]
st = np.exp(-sx * sx * 30) * (sy ** 0.6) * (1 - sy) ** 0.15 * 1.6
save('streak', np.dstack([np.ones((256, 64))] * 3 + [np.clip(st, 0, 1)]))

# distortion ripple: the outward offset of a ring in RG (0.5 = none), its strength in alpha
x, y = grid(256); r = np.sqrt(x * x + y * y) + 1e-6
k = np.exp(-((r - 0.7) / 0.16) ** 2)
save('ripple', np.dstack([0.5 + 0.5 * x / r * k, 0.5 + 0.5 * y / r * k, np.full_like(r, 0.5), k]))

# scorch: a charred centre with a ragged, sooty edge (dark colour, alpha = coverage)
n = fbm(256, 6, 5, 3000); x, y = grid(256); r = np.sqrt(x * x + y * y)
cov = np.clip((0.85 - r + (n - 0.5) * 0.7) * 2.2, 0, 1) ** 1.3
char = 0.06 + 0.1 * n
save('scorch', np.dstack([char * 1.1, char * 0.9, char * 0.8, cov * 0.92]))
# scuff: pale streaks along one direction (a slide or a hard landing)
n2 = fbm(256, 4, 4, 3100); lines = (np.sin(y * 40 + n2 * 6) * 0.5 + 0.5) ** 6
cov2 = np.clip(1 - np.abs(x) * 1.2, 0, 1) * np.clip(1 - np.abs(y) * 1.05, 0, 1) * (0.35 + 0.65 * lines) * (n2 > 0.35)
save('scuff', np.dstack([np.full_like(x, 0.85), np.full_like(x, 0.82), np.full_like(x, 0.78), np.clip(cov2, 0, 1) * 0.55]))

# lens dirt: soft smudges and specks, mostly clear (bloom shows it where bright light passes)
d = np.zeros((512, 512))
yy, xx = np.mgrid[0:512, 0:512]
for _ in range(70):
    cx, cy, rr = rng.random() * 512, rng.random() * 512, 3 + rng.random() * 28
    d += (0.2 + rng.random() * 0.6) * np.exp(-((xx - cx) ** 2 + (yy - cy) ** 2) / (2 * rr * rr))
d = np.clip(d * 0.45 * (0.3 + 0.7 * fbm(512, 5, 4, 3200)), 0, 1)
save('lensdirt', np.dstack([d, d * 0.97, d * 0.92, np.ones_like(d)]))

# cloud puffs: four variants in a 2 x 2 sheet, 512 px each. Density in alpha; RGB = a surface normal (0.5 = flat)
def puff(seed):
    n = 512; x, y = grid(n); dens = np.zeros((n, n))
    r = np.random.default_rng(seed)
    for _ in range(7):                                  # a few overlapping round lobes, the biggest low and central
        cx, cy = (r.random() - 0.5) * 0.9, (r.random() - 0.3) * 0.5; rr = 0.3 + r.random() * 0.3
        dens = np.maximum(dens, 1 - ((x - cx) ** 2 + ((y - cy) * 1.25) ** 2) / (rr * rr))
    dens = np.clip(dens + (fbm(n, 5, 5, seed + 7) - 0.5) * 0.7, 0, 1)
    dens *= np.clip((0.95 - np.sqrt(x * x + y * y)) * 4, 0, 1)   # nothing touches the edge
    dens = np.clip(dens * 1.6, 0, 1) ** 1.3
    gy, gx = np.gradient(dens * 40 / n * 8)
    nz = np.ones_like(dens); l = np.sqrt(gx * gx + gy * gy + nz * nz)
    return np.dstack([0.5 - 0.5 * gx / l, 0.5 + 0.5 * gy / l, 0.5 + 0.5 * nz / l, dens])
sheet = np.zeros((1024, 1024, 4))
for k in range(4):
    r_, c_ = divmod(k, 2); sheet[r_ * 512:(r_ + 1) * 512, c_ * 512:(c_ + 1) * 512] = puff(4000 + k * 13)
save('cloudpuff', sheet)
print('fx textures written')
