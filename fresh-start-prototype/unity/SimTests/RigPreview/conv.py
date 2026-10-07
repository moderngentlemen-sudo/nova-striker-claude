# Converts a rig builder in Rigs.cs (C#) into JavaScript for preview.sh, mechanically, so the picture uses the same numbers.
# Usage: conv.py Rigs.cs nova|echo
import re, sys
s = open(sys.argv[1]).read()
if sys.argv[2] == 'nova':
    i = s.index('static Rig BuildNovaRig()'); body = s[s.index('{', i) + 1:s.index('S.extra = ex; S.mats = M;', i)]
else:   # Echo: the shared builder, minus its first lines that hand the others off
    i = s.index('public static Rig BuildPlayerRig(string charId)'); body = s[s.index('{', i) + 1:s.index('var rig = FinishRig(new Rig', i)]
    body = '\n'.join(l for l in body.split('\n') if 'return Build' not in l) + '\nconst S = { root, armN, armF, legN, legF };'
body = body.replace('@base', 'base')
body = re.sub(r'new RigMats \{ (.*?) \}', lambda m: '({' + re.sub(r'(\w+) = ', r'\1: ', m.group(1)) + '})', body)
body = re.sub(r'new\[\] \{ ([\w, ]+) \};', r'[\1];', body)
body = re.sub(r'foreach \(var \(([\w, ]+)\) in new\[\] \{ (.*?) \}\)', lambda m: 'for (const [' + m.group(1) + '] of [' + m.group(2).replace('(', '[').replace(')', ']') + '])', body, flags=re.S)
out = []
for line in body.split('\n'):
    if 'new Edge(' in line: continue
    l = line
    l = re.sub(r'foreach \((?:var|float) (\w+) in new\[\] \{ (.*?) \}\)', r'for (const \1 of [\2])', l)
    l = re.sub(r'new TMat(?:\(TMat\.Kind\.\w+\))? \{ (.*?) \}', lambda m: 'mkMat({' + re.sub(r'(\w+) = ', r'\1: ', m.group(1)) + '})', l)
    l = re.sub(r'^(\s*)(?:TObj|Limb|var|float|TMesh|TMat) ', r'\1let ', l)
    l = l.replace('new Dictionary<string, TObj>()', '{}')
    l = re.sub(r'; (?:TObj|Limb|var) ', '; let ', l)
    l = re.sub(r'(\d)f\b', r'\1', l)
    l = l.replace('Mathf.PI', 'Math.PI').replace('Mathf.Sign', 'Math.sign').replace('new List<TMesh>()', '[]').replace('.Add(', '.push(')
    l = l.replace('Side.Double', 'THREE.DoubleSide').replace('Blending.Additive', 'THREE.AdditiveBlending').replace('new TMesh(', 'new THREE.Mesh(')
    l = l.replace('new RigExtra()', '{ jets: [], blades: [], edges: {} }').replace('new List<TObj>()', '[]').replace('const float ', 'const ')
    out.append(l)
print('\n'.join(out))
