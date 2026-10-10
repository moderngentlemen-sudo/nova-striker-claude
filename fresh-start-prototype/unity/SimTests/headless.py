#!/usr/bin/env python3
"""Run the simulation suites without MSBuild or NuGet (useful in restricted cloud processes)."""
import argparse, json, os, shutil, subprocess, sys
from pathlib import Path

root = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('--project', choices=['ShieldTests', 'LevelTests', 'LayoutExport', 'Trace'], default='LevelTests')
parser.add_argument('--out', default=str(Path(os.environ.get('TMPDIR', '/tmp')) / 'nova-striker-headless'))
parser.add_argument('--verify-failure', action='store_true', help='Prove a deliberately failing assertion fails the process; source is not modified.')
args, program_args = parser.parse_known_args()
dotnet = os.environ.get('DOTNET') or shutil.which('dotnet') or str(Path.home() / '.dotnet/dotnet')
sdk_root = Path(dotnet).resolve().parent / 'sdk'
versions = lambda p: tuple(int(x) for x in p.name.split('.') if x.isdigit())
sdk = max(sdk_root.iterdir(), key=versions)
pack = max((sdk_root.parent / 'packs/Microsoft.NETCore.App.Ref').iterdir(), key=versions)
refs = sorted((pack / 'ref/net8.0').glob('*.dll'))
if not refs: raise SystemExit('Missing .NET 8 reference assemblies')
out = Path(args.out); out.mkdir(parents=True, exist_ok=True)
name = args.project
sources = sorted((root.parent / 'Assets/NovaStriker/Sim').glob('*.cs'))
sources += [root / name / 'Program.cs'] if name != 'Trace' else [root / 'Program.cs', root / 'Trace.cs']
if name == 'LevelTests': sources.insert(0, root.parent / 'Assets/NovaStriker/Game/View/EmissionClock.cs')
if args.verify_failure:
    if name not in ('ShieldTests', 'LevelTests'): raise SystemExit('Failure probe requires a test suite')
    text = sources[-1].read_text()
    text = text.replace('Console.WriteLine(fails == 0 ? "ALL PASS"', 'Check(false, "intentional failure probe"); Console.WriteLine(fails == 0 ? "ALL PASS"')
    probe = out / (name + '-probe.cs'); probe.write_text(text); sources[-1] = probe
dll = out / (name + '.dll')
rsp = out / (name + '.rsp')
rsp.write_text('\n'.join(['/nologo', '/target:exe', '/langversion:9.0', '/out:' + str(dll)] + ['/r:' + str(p) for p in refs] + [str(p) for p in sources]))
subprocess.run([dotnet, str(sdk / 'Roslyn/bincore/csc.dll'), '@' + str(rsp)], check=True)
(out / (name + '.runtimeconfig.json')).write_text(json.dumps({'runtimeOptions': {'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': pack.name}}}))
result = subprocess.run([dotnet, str(dll)] + program_args)
if args.verify_failure:
    if result.returncode == 0: raise SystemExit('FAIL: failing assertion returned success')
    print('PASS: intentional failure returns a nonzero status')
    sys.exit(0)
sys.exit(result.returncode)
