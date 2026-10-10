#!/usr/bin/env python3
# Compile Unity asmdef assemblies (packages + the project) with Roslyn against the Unity 6.3 managed DLLs,
# to type-check the port without the editor. Usage: ucompile.py <out-dir> [--player] <root>...
import json, os, re, subprocess, sys, glob

SCR = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(SCR, 'unity/Editor/Data')
CSC = os.path.expanduser('~/.dotnet/sdk/8.0.425/Roslyn/bincore/csc.dll')
DOTNET = os.path.expanduser('~/.dotnet/dotnet')

args = sys.argv[1:]
out = args.pop(0)
player = '--player' in args
args = [a for a in args if a != '--player']
only = None
if '--only' in args:
    i = args.index('--only'); only = set(args[i + 1].split(',')); del args[i:i + 2]
roots = args
os.makedirs(out, exist_ok=True)

ver = [(5, 3), (5, 4), (5, 5), (5, 6), (2017, 1), (2017, 2), (2017, 3), (2017, 4), (2018, 1), (2018, 2), (2018, 3), (2018, 4), (2019, 1), (2019, 2), (2019, 3), (2019, 4),
       (2020, 1), (2020, 2), (2020, 3), (2021, 1), (2021, 2), (2021, 3), (2022, 1), (2022, 2), (2022, 3), (2023, 1), (2023, 2), (2023, 3), (6000, 0), (6000, 1), (6000, 2), (6000, 3)]
DEFINES = ['UNITY_%d_%d_OR_NEWER' % v for v in ver] + ['UNITY_6000_3', 'UNITY_6000', 'UNITY_64', 'NET_STANDARD_2_1', 'NET_STANDARD', 'NETSTANDARD2_1', 'NETSTANDARD',
    'CSHARP_7_3_OR_NEWER', 'ENABLE_INPUT_SYSTEM', 'ENABLE_LEGACY_INPUT_MANAGER', 'ENABLE_MONO', 'ENABLE_UNITYWEBREQUEST', 'ENABLE_PROFILER', 'UNITY_ASSERTIONS',
    'UNITY_STANDALONE', 'UNITY_STANDALONE_WIN', 'PLATFORM_STANDALONE', 'PLATFORM_STANDALONE_WIN', 'ENABLE_PHYSICS', 'ENABLE_UNITY_COLLECTIONS_CHECKS', 'ENABLE_BURST_AOT',
    'ENABLE_VR', 'ENABLE_XR_MODULE', 'UNITY_UGP_API', 'UNITY_INPUT_SYSTEM_ENABLE_UI', 'UNITY_INPUT_SYSTEM_ENABLE_XR', 'UNITY_INPUT_SYSTEM_ENABLE_PHYSICS', 'UNITY_INPUT_SYSTEM_ENABLE_PHYSICS2D']
if not player:
    DEFINES += ['UNITY_EDITOR', 'UNITY_EDITOR_64', 'UNITY_EDITOR_WIN', 'DEVELOPMENT_BUILD']

engine = sorted(glob.glob(os.path.join(DATA, 'Managed/UnityEngine/UnityEngine*.dll')))
editor = sorted(glob.glob(os.path.join(DATA, 'Managed/UnityEngine/UnityEditor*.dll'))) + [os.path.join(DATA, 'Managed/UnityEditor.dll')] if os.path.exists(os.path.join(DATA, 'Managed/UnityEditor.dll')) else sorted(glob.glob(os.path.join(DATA, 'Managed/UnityEngine/UnityEditor*.dll')))
extra = [p for p in glob.glob(os.path.join(DATA, 'Managed/*.dll')) if os.path.basename(p).startswith(('Unity.Cecil', 'Newtonsoft'))]
base_refs = [os.path.join(DATA, 'NetStandard/ref/2.1.0/netstandard.dll')] + engine + glob.glob(os.path.join(DATA, 'NetStandard/compat/2.1.0/shims/netstandard/*.dll'))

# asmdefs
asm = {}
guid = {}
for root in roots:
    for path in glob.glob(os.path.join(root, '**/*.asmdef'), recursive=True):
        if re.search(r'(?i)/(tests?|samples?~?|documentation~|.*\.tests?)(/|$)', path.replace(root, '')) or 'source~' in path or '~/' in path.replace(root, ''):
            continue
        try:
            d = json.load(open(path, encoding='utf-8-sig'))
        except Exception as e:
            print('skip', path, e); continue
        d['dir'] = os.path.dirname(path)
        asm[d['name']] = d
        meta = path + '.meta'
        if os.path.exists(meta):
            m = re.search(r'guid: (\w+)', open(meta).read())
            if m: guid[m.group(1)] = d['name']
# precompiled dlls in the roots
precompiled = {}
for root in roots:
    for p in glob.glob(os.path.join(root, '**/*.dll'), recursive=True):
        if 'source~' in p or '/Tests' in p: continue
        precompiled[os.path.basename(p)] = p
dirs = {a['dir']: n for n, a in asm.items()}

def sources(a):
    out_ = []
    for dp, dn, fn in os.walk(a['dir']):
        if dp != a['dir'] and dp in dirs: dn[:] = []; continue
        dn[:] = [x for x in dn if not x.endswith('~') and not (os.path.join(dp, x) in dirs)]
        out_ += [os.path.join(dp, f) for f in fn if f.endswith('.cs')]
    return out_

def is_editor(a): return a.get('includePlatforms') == ['Editor']

present = set(asm) | {'com.unity.inputsystem', 'com.unity.render-pipelines.universal', 'com.unity.render-pipelines.core', 'com.unity.ugui', 'com.unity.mathematics',
                      'com.unity.burst', 'com.unity.collections', 'com.unity.modules.physics', 'com.unity.modules.physics2d', 'com.unity.modules.ui', 'com.unity.modules.uielements',
                      'com.unity.modules.imgui', 'com.unity.modules.audio', 'com.unity.modules.animation', 'com.unity.modules.xr', 'com.unity.modules.vr', 'Unity'}
pkgver = {'com.unity.inputsystem': '1.17.0', 'com.unity.render-pipelines.universal': '17.3.0', 'com.unity.render-pipelines.core': '17.3.0', 'com.unity.ugui': '2.0.0',
          'com.unity.mathematics': '1.3.2', 'com.unity.burst': '1.8.14', 'com.unity.collections': '2.4.3', 'Unity': '6000.3.0'}

def vcmp(a, b):
    def t(s): return [int(x) for x in re.findall(r'\d+', s)][:3] + [0] * (3 - len(re.findall(r'\d+', s)[:3]))
    return (t(a) > t(b)) - (t(a) < t(b))
def in_range(v, expr):
    expr = expr.strip()
    if not expr: return True
    if expr[0] in '[(':
        lo, hi = expr[1:-1].split(',') if ',' in expr else (expr[1:-1], expr[1:-1])
        ok = True
        if lo.strip(): ok &= vcmp(v, lo) >= 0 if expr[0] == '[' else vcmp(v, lo) > 0
        if hi.strip(): ok &= vcmp(v, hi) <= 0 if expr[-1] == ']' else vcmp(v, hi) < 0
        return ok
    return vcmp(v, expr) >= 0

built = {}
trans = {}
failed = set()
def build(name, stack=()):
    if name in built: return built[name]
    if name in failed or name not in asm: return None
    a = asm[name]
    if is_editor(a) and player: return None
    defs = list(DEFINES)
    for vd in a.get('versionDefines', []):
        n = vd['name']
        if n in pkgver and in_range(pkgver[n], vd.get('expression', '')): defs.append(vd['define'])
        elif n.startswith('com.unity.modules.') and n in present: defs.append(vd['define'])
    for c in a.get('defineConstraints', []):
        neg = c.startswith('!'); c2 = c.lstrip('!')
        have = any(x == c2 for x in defs) or (c2 == 'UNITY_INCLUDE_TESTS' and False)
        if ' || ' in c2: have = any(x.strip() in defs for x in c2.split('||'))
        if have == neg:
            failed.add(name); return None
    refs = []
    ALIAS = {'Unity.ugui': 'UnityEngine.UI'}
    EXTRA = {'Unity.TextMeshPro': ['UnityEngine.UI'], 'Unity.TextMeshPro.Editor': ['UnityEngine.UI', 'UnityEditor.UI'], 'UnityEditor.UI.Analytics': ['Unity.TextMeshPro']}
    for r in a.get('references', []) + EXTRA.get(name, []):
        rn = guid.get(r[5:]) if r.startswith('GUID:') else ALIAS.get(r, r)
        if rn is None: print('  (unresolved %s in %s)' % (r, name))
        if rn and rn not in stack:
            p = build(rn, stack + (name,))
            if p: refs.append(p); refs += trans.get(rn, [])
    if a.get('overrideReferences'):
        for pr in a.get('precompiledReferences', []):
            if pr in precompiled: refs.append(precompiled[pr])
    else:
        refs += [p for k, p in precompiled.items() if k in ('Unity.Burst.Unsafe.dll', 'Unity.Collections.LowLevel.ILSupport.dll')]
    # (Unity 6's uGUI is a core package every assembly sees)
    if name not in ('UnityEngine.UI', 'UnityEditor.UI', 'Unity.InternalAPIEngineBridge.004') and not name.startswith(('Unity.Mathematics', 'Unity.Burst', 'Unity.Collections')):
        for imp in ['UnityEngine.UI'] + ([] if player else ['UnityEditor.UI']):
            p = build(imp, stack + (name,))
            if p: refs.append(p)
    allrefs = base_refs + refs + (editor if not player else []) + extra
    # (every other built assembly an asmdef doesn't list is still visible in Unity when autoReferenced; keep it explicit)
    srcs = sources(a)
    if not srcs: failed.add(name); return None
    dll = os.path.join(out, name + '.dll')
    rsp = os.path.join(out, name + '.rsp')
    with open(rsp, 'w') as f:
        f.write('-nologo\n-target:library\n-nostdlib\n-noconfig\n-langversion:9.0\n-nowarn:0169,0649,0414,0618,0067,0219,0168,0162,0108,0114,0436,1701,1702,8632\n-debug-\n')
        if a.get('allowUnsafeCode'): f.write('-unsafe\n')
        f.write('-out:%s\n' % dll)
        for d in sorted(set(defs)): f.write('-define:%s\n' % d)
        seen = set()
        for r in allrefs:
            b = os.path.basename(r)
            if b in seen or os.path.abspath(r) == os.path.abspath(dll): continue
            seen.add(b); f.write('-r:"%s"\n' % r)
        for s in srcs: f.write('"%s"\n' % s)
    if only is None or name in only:
        res = subprocess.run([DOTNET, CSC, '@' + rsp], capture_output=True, text=True)
        errs = [l for l in res.stdout.splitlines() if ': error ' in l]
        if res.returncode != 0:
            print('FAIL %s (%d errors)' % (name, len(errs)))
            for l in errs[:int(os.environ.get('NERR', '25'))]: print('  ' + l.replace(a['dir'] + '/', ''))
            failed.add(name); return None
        print('ok   %s' % name)
    built[name] = dll
    trans[name] = list(dict.fromkeys(refs))
    return dll

for n in ['Unity.Mathematics', 'Unity.Burst', 'Unity.Collections', 'UnityEngine.UI', 'UnityEditor.UI'] + sorted(asm):
    build(n)

# Package editor failures are expected in this lightweight checker; project failures must fail the process.
project_failures = failed.intersection(only or {n for n in asm if n.startswith("NovaStriker.")})
if project_failures: sys.exit(1)
