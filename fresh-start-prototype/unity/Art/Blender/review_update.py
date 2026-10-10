"""Reproducible Blender review renders of kits, enemies and gull, in an ordinary bpy 4.2 process."""
import os,sys,math,runpy,json
from pathlib import Path
import bpy
from mathutils import Vector,Matrix
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[1];ARGS=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sys.argv[1:];OUT=Path(ARGS[0]).resolve();OUT.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(HERE));import stage_layout
from nova_lib import mat,srgb,box

def run(name,*args):
 sys.argv=['blender','--',*map(str,args)];runpy.run_path(str(HERE/name),run_name='__main__')
def lighting():
 sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.device='CPU';sc.cycles.samples=12;sc.cycles.use_denoising=True;sc.render.threads_mode='FIXED';sc.render.threads=4
 sc.render.resolution_x=800;sc.render.resolution_y=450;sc.render.resolution_percentage=100
 sc.view_settings.view_transform='AgX';sc.view_settings.look='AgX - Medium High Contrast'
 w=bpy.data.worlds.new('ReviewWorld');w.use_nodes=True;w.node_tree.nodes['Background'].inputs[0].default_value=(.18,.23,.3,1);w.node_tree.nodes['Background'].inputs[1].default_value=.6;sc.world=w
 for name,loc,power,size in [('key',(5,-8,16),2200,8),('fill',(-8,-4,8),900,10),('rim',(3,8,12),1700,6)]:
  d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);sc.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
 d=bpy.data.cameras.new('ReviewCamera');o=bpy.data.objects.new('ReviewCamera',d);sc.collection.objects.link(o);sc.camera=o;return sc,o

def capture(sc,cam,pos,target,file,ortho=None):
 cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO' if ortho else 'PERSP';cam.data.lens=45
 if ortho:cam.data.ortho_scale=ortho
 sc.render.filepath=str(file);bpy.ops.render.render(write_still=True)

def stage(z,anchor):
 bpy.ops.wm.open_mainfile(filepath=str(OUT/(z+'_kit.blend')))
 # Meshes in collection space, cloned into an exact route-space review slice.
 kits={c.name[4:]:list(c.objects) for c in bpy.data.collections if c.name.startswith('Kit_')}
 f=stage_layout.frame(anchor);base=Vector((f['px'],-f['pz'],0));yaw=math.atan2(-f['tz'],f['tx'])
 inv=Matrix.Rotation(-yaw,4,'Z')
 def position(x,y,d):
  q=stage_layout.frame(x);return inv@(Vector((q['px']+q['nx']*d,-q['pz']-q['nz']*d,y))-base)
 for p in stage_layout.placements(z):
  if abs(p['x']-anchor)>22:continue
  q=stage_layout.frame(p['x']);a=math.atan2(-q['tz'],q['tx'])-yaw+math.radians(p['yaw'])
  transform=Matrix.Translation(position(p['x'],p['y'],p['dz']))@Matrix.Rotation(a,4,'Z')@Matrix.Diagonal((p['sx'],p['sz'],p['sy'],1))
  for src in kits.get(p['asset'],[]):o=src.copy();o.data=src.data;bpy.context.scene.collection.objects.link(o);o.matrix_world=transform@src.matrix_world
 for objs in kits.values():
  for o in objs:o.hide_render=True
 hull=mat('CollisionPreview',srgb('#657e9f'),rough=.6);deck=mat('DeckPreview',srgb('#cbd4df'),rough=.55)
 # Same simulation boxes and route frames, no reconstructed coordinates.
 for b in stage_layout.DATA['boxes']:
  if b['x1']<anchor-22 or b['x0']>anchor+22 or b['type'] not in ('s','o') or b['tag']=='bound':continue
  x0=max(b['x0'],anchor-22);x1=min(b['x1'],anchor+22)
  for i in range(max(1,math.ceil((x1-x0)/2))):
   n=max(1,math.ceil((x1-x0)/2));x=x0+(x1-x0)*(i+.5)/n;q=stage_layout.frame(x);a=math.atan2(-q['tz'],q['tx'])-yaw
   o=box('LayoutDeck',deck if b['type']=='o' else hull,position(x,(b['y0']+b['y1'])/2,0),((x1-x0)/n*1.02,5.6,b['y1']-b['y0']),.02,1,rot=(0,0,a))
 sc,cam=lighting();heights=[b['y1'] for b in stage_layout.DATA['boxes'] if abs((b['x0']+b['x1'])/2-anchor)<20 and b['type'] in ('s','o') and b['tag']!='bound'];top=min(heights);target=(0,0,(min(heights)+max(heights))/2+1)
 for label,pos,scale in [('wide',(22,-30,top+22),48),('gameplay',(2,-26,top+8),32),('detail',(4,-10,top+5),14)]:
  if label=='detail':
   candidates=[p for p in stage_layout.placements(z) if p['asset'] not in ('Rail','Support','Light','DeckPanel') and abs(p['x']-anchor)<22]
   prop=min(candidates,key=lambda p:abs(p['x']-anchor)) if candidates else None
   if prop:target=position(prop['x'],prop['y']+1.5,prop['dz']);pos=target+Vector((5,-9,3));scale=8
  capture(sc,cam,pos,target,OUT/f'{z}_{label}.png',scale)

for z,a in [('arena',79),('tower',110),('skyline',200),('foundry',540),('undercity',1050)]:stage(z,a)
# Enemy sources remain editable/reproducible; export uses the rig's node-local coordinates.
RES=ROOT/'Assets/NovaStriker/Resources/NovaStriker/Models'
for kind in ['swarmer','shield','sniper','brute','post','turret','drone','mortar','charger','warden','stormcaller']:
 run('build_enemy_'+kind+'.py',OUT)
 if kind=='mortar':run('export_enemy.py',RES/'enemy_mortar.json')
 sc,cam=lighting();sc.render.resolution_x=384;sc.render.resolution_y=448
 verts=[o.matrix_world@Vector(c) for o in sc.objects if o.type=='MESH' for c in o.bound_box];height=max(v.z for v in verts);center=(0,0,height*.5);span=max(height,max(v.x for v in verts)-min(v.x for v in verts))*1.35
 for label,a in [('front',90),('quarter',50),('side',0),('back',270)]:
  a=math.radians(a);capture(sc,cam,(math.sin(a)*8,-math.cos(a)*8,height*.6),center,OUT/f'enemy_{kind}_{label}.png',span)
run('build_bird.py',OUT);run('export_bird.py',RES/'bird.json');sc,cam=lighting();capture(sc,cam,(1,-1,1),(0,0,0),OUT/'bird_quarter.png',1.7)
print('Review renders complete')
