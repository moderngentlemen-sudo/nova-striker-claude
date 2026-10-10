# A gull for the sky (Birds.cs): white body and head, pale grey wings with dark grey tips, a yellow beak, about 300
# triangles. Facing +Y (export_bird.py turns that into the game's forward, Unity +z after its mirror),
# wings spread along X from shoulders at x = ±0.06, 1.3 m wingspan. The wings are separate thin plates so the
# shader can flap them (export_bird.py writes each vertex's flap weight).
# Run: blender -b -P build_bird.py -- <out-dir>   (writes bird.blend)
import math, os, sys, bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_lib import *

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else '/tmp'
reset()
WHITE = mat('Bird_White', srgb('#f4f6f8'), rough=0.6)
GREY = mat('Bird_Grey', srgb('#a9b2bc'), rough=0.6)
DARK = mat('Bird_Dark', srgb('#2e3238'), rough=0.6)
BEAK = mat('Bird_Beak', srgb('#f2c14e'), rough=0.5)

def nose_shape(p, s, t):                     # a teardrop body: full at the chest, tapering to the tail
    q = p.copy(); k = (q.y + 0.2) / 0.4
    q.x *= 0.55 + 0.45 * math.sin(max(0, min(1, k)) * math.pi * 0.85 + 0.25)
    q.z *= 0.6 + 0.4 * math.sin(max(0, min(1, k)) * math.pi * 0.85 + 0.25)
    return q
patch('Bird_Body', WHITE, (0, 0, 0), (0.07, 0.22, 0.065), res=(10, 6), thick=0, shape=nose_shape, subsurf=0)
patch('Bird_Head', WHITE, (0, 0.2, 0.035), (0.045, 0.055, 0.045), res=(8, 5), thick=0, subsurf=0)
cyl('Bird_Beak', BEAK, (0, 0.245, 0.03), (0, 0.3, 0.022), 0.012, 5, r2=0.003)
# tail: a short fan
mesh_obj('Bird_Tail', [(-0.05, -0.2, 0.01), (0.05, -0.2, 0.01), (0.07, -0.3, 0.0), (-0.07, -0.3, 0.0)], [(0, 1, 2, 3)], WHITE)
# wings: an inner and an outer plate each side, swept back, the outer one with a dark tip
for s in (1, -1):
    sh, el, tip = 0.06, 0.34, 0.65
    inner = [(s * sh, 0.06, 0.01), (s * el, 0.03, 0.02), (s * el, -0.1, 0.02), (s * sh, -0.1, 0.01)]
    outer = [(s * el, 0.03, 0.02), (s * 0.55, -0.04, 0.015), (s * 0.55, -0.12, 0.015), (s * el, -0.1, 0.02)]
    tipq = [(s * 0.55, -0.04, 0.015), (s * tip, -0.12, 0.01), (s * 0.55, -0.12, 0.015)]
    for nm, q, m in (('Inner', inner, GREY), ('Outer', outer, GREY)):
        o = mesh_obj(f'Bird_Wing{nm}{s}', q, [(0, 1, 2, 3) if s > 0 else (3, 2, 1, 0)], m)
        mod(o, 'SOLIDIFY', thickness=0.012, offset=0)
    o = mesh_obj(f'Bird_WingTip{s}', tipq, [(0, 1, 2) if s > 0 else (2, 1, 0)], DARK); mod(o, 'SOLIDIFY', thickness=0.01, offset=0)
# Small overlapping primaries give a feathered trailing edge without enlarging the 1.3m wingspan.
    for k in range(5):
        x=.35+k*.05; trailing=-.12-k*.008
        q=[(s*x,-.04,.018),(s*(x+.065),-.07,.012),(s*(x+.07),trailing-.05,.007),(s*x,trailing,.012)]
        o=mesh_obj(f'Bird_WingFeather{s}_{k}',q,[(0,1,2,3) if s>0 else (3,2,1,0)], DARK if k==4 else GREY)
        mod(o,'SOLIDIFY',thickness=.006,offset=0)
# Eyes and a split tail preserve a gull silhouette at close review scale.
for s in (-1,1):patch('Bird_Eye'+str(s),DARK,(s*.043,.208,.048),(.005,.005,.005),res=(6,4),subsurf=0)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'bird.blend'))
print('built', len(bpy.data.objects), 'objects')
