# Reuse the proven gym mesh exporter; only the authoritative placement provider changes.
import sys,os,runpy,json
from pathlib import Path
sys.path.insert(0,os.path.dirname(__file__))
import gym_layout,stage_layout
args=sys.argv[sys.argv.index('--')+1:];ZONE=args[1]
gym_layout.placements=lambda:[(p['asset'],p['x'],p['y'],p['dz'],p['yaw'],p['sx'],p['sy'],p['sz']) for p in stage_layout.placements(ZONE)]
runpy.run_path(str(Path(__file__).with_name('export_kit.py')),run_name='__main__')
p=Path(args[0]);data=json.loads(p.read_text());data['schema']=1;data['zone']=ZONE
# Metre-scaled box projection preserves material detail on vertical walls and curved pipes.
for part in data['parts']:
 part['lod']=0
 for k in range(len(part['p'])//3):
  pos=part['p'][3*k:3*k+3];normal=part['n'][3*k:3*k+3]
  axis=max(range(3),key=lambda a:abs(normal[a]));axes=((2,1),(0,2),(0,1))[axis]
  part['uv'][2*k:2*k+2]=[round(pos[a]*1.5,4) for a in axes]
p.write_text(json.dumps(data,separators=(',',':')))
