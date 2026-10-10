"""Run with Blender's Python (or the official bpy 4.2 wheel). All inputs are tracked builders/layouts.
python author_update.py <output-review-dir> [--enemies]
"""
import sys,os,runpy
from pathlib import Path
import bpy
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[1];ARGS=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sys.argv[1:];OUT=Path(ARGS[0]).resolve();OUT.mkdir(parents=True,exist_ok=True)
RES=ROOT/'Assets/NovaStriker/Resources/NovaStriker'
def run(name,*args):
 sys.argv=['blender','--',*map(str,args)];runpy.run_path(str(HERE/name),run_name='__main__')
for z in ('arena','tower','skyline','foundry','undercity'):
 run('build_stage_kit.py',z,OUT);run('export_stage_kit.py',RES/'Env'/f'{z}_kit.json',z)
print('All stage kits exported')

for kind in ("swarmer","shield","sniper","brute","post","turret","drone","mortar","charger","warden","stormcaller"):
 run("build_enemy_"+kind+".py",OUT)
 run("export_enemy.py",RES/"Models"/f"enemy_{kind}_lod1.json","--lod")
print("Enemy distance LODs exported")
