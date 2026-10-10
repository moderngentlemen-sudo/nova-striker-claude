#!/usr/bin/env python3
"""Validate runtime mesh payloads and authored triangle budgets, without Unity."""
import json, math
from pathlib import Path

res = Path(__file__).resolve().parent.parent / 'Assets/NovaStriker/Resources/NovaStriker'
errors = []
def mesh(part, path):
    p, n, uv, tri = (part.get(k, []) for k in ('p', 'n', 'uv', 'i'))
    nv = len(p) // 3
    if len(p) % 3 or not nv or len(n) != len(p): errors.append(f'{path}: invalid positions/normals')
    if 'uv' in part and len(uv) != nv * 2: errors.append(f'{path}: invalid UV length')
    if len(tri) % 3 or not tri or any(not isinstance(i,int) or i < 0 or i >= nv for i in tri): errors.append(f'{path}: invalid triangles')
    if not all(math.isfinite(v) for v in p+n+uv): errors.append(f'{path}: nonfinite mesh')
    if p and all(max(p[k::3])-min(p[k::3]) < 0.00001 for k in range(3)): errors.append(f'{path}: empty bounds')
    return len(tri)//3

for path in sorted((res / 'Models').glob('enemy_*.json')):
    data = json.loads(path.read_text()); parts = data.get('parts', [])
    tris = sum(mesh(p, f'{path.name}/{p.get("node")}') for p in parts)
    kind = path.stem.removeprefix('enemy_'); budget = 18000 if kind in ('warden','stormcaller') else 7000 if kind in ('brute','charger') else 3000
    if not parts or any(not p.get('node') or p.get('mat') not in ('Enemy_Plate','Enemy_Joint','Enemy_Energy','Enemy_Glass') for p in parts): errors.append(f'{path.name}: invalid rig/material mapping')
    if tris > budget: errors.append(f'{path.name}: {tris} triangles exceeds {budget}')
    print(f'{path.name}: {tris} triangles / {budget}')
for path in sorted((res / 'Env').glob('*_kit.json')):
    data = json.loads(path.read_text()); assets = {p.get('asset') for p in data.get('parts',[])}
    tris = sum(mesh(p,f'{path.name}/{p.get("asset")}') for p in data.get('parts',[]))
    if any(p.get('asset') not in assets for p in data.get('place',[])): errors.append(f'{path.name}: unknown placement asset')
    if tris > 80000: errors.append(f'{path.name}: triangle budget exceeded')
    print(f'{path.name}: {tris} source triangles, {len(data.get("place",[]))} placements')
bird = json.loads((res/'Models/bird.json').read_text()); mesh(bird,'bird')
if len(bird.get('c',[])) != len(bird['p']) or len(bird.get('uv2',[])) != len(bird['p'])//3*2: errors.append('bird: invalid colour/wing data')
for path in sorted((res/'Fx').glob('*.bytes')):
    if not path.read_bytes().startswith(b'\x89PNG\r\n\x1a\n'): errors.append(f'{path.name}: invalid PNG signature')
if errors: raise SystemExit('\n'.join(errors))
print('ALL PASS: runtime mesh payloads and effect textures')
