# Compares a JavaScript trace with a C# trace, tick by tick. Prints the first difference (or that they match).
import json, sys, math
def flat(v, path, out):
    if isinstance(v, list):
        out.append((path + '#len', len(v)))
        for i, x in enumerate(v): flat(x, f'{path}[{i}]', out)
    elif isinstance(v, dict):
        for k in v: flat(v[k], f'{path}.{k}', out)
    else: out.append((path, v))
js = open(sys.argv[1]).read().strip().split('\n'); cs = open(sys.argv[2]).read().strip().split('\n')
tol = float(sys.argv[3]) if len(sys.argv) > 3 else 0.0
worst = 0.0
for n, (a, b) in enumerate(zip(js, cs)):
    A, B = [], []
    flat(json.loads(a), '', A); flat(json.loads(b), '', B)
    if len(A) != len(B) or any(x[0] != y[0] for x, y in zip(A, B)):
        da = dict(A); db = dict(B)
        diff = [k for k in set(da) | set(db) if da.get(k) != db.get(k)][:6]
        print(f'DIVERGE tick {n+1}: structure: ' + '; '.join(f'{k}: js={da.get(k)} cs={db.get(k)}' for k in sorted(diff)))
        sys.exit(1)
    for (k, x), (_, y) in zip(A, B):
        if isinstance(x, float) or isinstance(y, float):
            d = abs(x - y)
            worst = max(worst, d)
            if d > tol:
                print(f'DIVERGE tick {n+1}: {k}: js={x!r} cs={y!r}'); sys.exit(1)
        elif x != y:
            print(f'DIVERGE tick {n+1}: {k}: js={x!r} cs={y!r}'); sys.exit(1)
if len(js) != len(cs): print(f'LENGTH js={len(js)} cs={len(cs)}'); sys.exit(1)
print(f'MATCH {len(js)} ticks (largest difference {worst:.3g})')
