# Converts Rigs.BuildNovaRig (C#) into JavaScript for preview.sh, mechanically, so the picture uses the same numbers
import re, sys
s = open(sys.argv[1]).read()
i = s.index('static Rig BuildNovaRig()'); body = s[s.index('{', i) + 1:s.index('S.extra = ex; S.mats = M;')]
body = re.sub(r'foreach \(var \(([\w, ]+)\) in new\[\] \{ (.*?) \}\)', lambda m: 'for (const [' + m.group(1) + '] of [' + m.group(2).replace('(', '[').replace(')', ']') + '])', body)
out = []
for line in body.split('\n'):
    if 'new Edge(' in line: continue
    l = line
    l = re.sub(r'foreach \((?:var|float) (\w+) in new\[\] \{ (.*?) \}\)', r'for (const \1 of [\2])', l)
    l = re.sub(r'new TMat(?:\(TMat\.Kind\.\w+\))? \{ (.*?) \}', lambda m: 'mkMat({' + re.sub(r'(\w+) = ', r'\1: ', m.group(1)) + '})', l)
    l = re.sub(r'^(\s*)(?:TObj|Limb|var|float|TMesh|TMat) ', r'\1let ', l)
    l = l.replace('new Dictionary<string, TObj>()', '{}')
    l = re.sub(r'; (?:TObj|Limb) ', '; let ', l)
    l = re.sub(r'(\d)f\b', r'\1', l)
    l = l.replace('Mathf.PI', 'Math.PI').replace('Mathf.Sign', 'Math.sign').replace('new List<TMesh>()', '[]').replace('.Add(', '.push(')
    l = l.replace('Side.Double', 'THREE.DoubleSide').replace('Blending.Additive', 'THREE.AdditiveBlending').replace('new TMesh(', 'new THREE.Mesh(')
    l = l.replace('new RigExtra()', '{ jets: [], blades: [], edges: {} }').replace('@base', 'base')
    out.append(l)
print('\n'.join(out))
