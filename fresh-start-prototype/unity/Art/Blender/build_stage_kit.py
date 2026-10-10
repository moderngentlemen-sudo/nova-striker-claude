"""Blender 4.2 shared environment kit. Meters, editable pieces; no gameplay colliders in the kit."""
import os,sys,math,bpy
sys.path.insert(0,os.path.dirname(__file__))
from nova_lib import *
args=sys.argv[sys.argv.index('--')+1:];ZONE,OUT=args[:2];reset()
M={name:mat('Kit_'+name,srgb(col),rough=r,metal=m,emit=srgb(col) if glow else None,strength=glow) for name,col,r,m,glow in [
 ('HullDark','#3d4f66',.46,.2,0),('Metal','#9aa6b2',.32,.85,0),('Grip','#7d8794',.8,.4,0),('Hazard','#f2c230',.55,.1,0),('Black','#14181f',.65,.1,0),('Light','#8fecff',.25,.1,2.2),('Holo','#7fe3ff',.2,.1,1.8),('Glass','#cfeeff',.05,.1,0),('Navy','#2b4f7e',.32,.15,0),('Heat','#ff813b',.4,.1,2),('Neon','#ce5cff',.3,.1,2)]}
def B(n,c,s,m='HullDark',bev=.025):return box(n,M[m],c,s,bev,1)
def C(n,a,b,r,m='Metal',v=12):return cyl(n,M[m],a,b,r,v)
def piece(name,fn):
 before=set(bpy.data.objects);fn();col=bpy.data.collections.new('Kit_'+name);bpy.context.scene.collection.children.link(col)
 for o in set(bpy.data.objects)-before:
  for c in list(o.users_collection):c.objects.unlink(o)
  col.objects.link(o)
def rail():
 for x in (-1.35,1.35):B('post',(x,0,.5),(.07,.1,1),'Metal')
 B('toprail',(0,0,1.02),(3,.11,.1),'Metal');B('bottom',(0,0,.18),(3,.08,.06),'Metal')
 for i in range(5):B('infill',(-1.2+i*.6,0,.5),(.03,.08,.6),'Metal')
def deck():
 B('panel',(0,0,0),(3.7,.12,.48),'Navy');B('rim',(0,-.09,.16),(3.5,.05,.025),'Metal')
 for x in (-1.7,1.7):
  for z in (-.14,.14):C('rivet',(x,-.075,z),(x,-.095,z),.035,'Metal',8)
 for i in range(7):B('chip',(-1.3+i*.4,-.09,-.08),(.05,.01,.045),'Black',0)
def support():
 B('column',(0,0,0),(.22,.3,4),'Metal');B('bracket',(0,-.4,1.65),(.4,1.1,.2),'HullDark')
 C('diagonal',(0,-1,1.7),(0,0,.6),.06)
def light():
 B('casing',(0,0,0),(.75,.14,.18));B('lens',(0,-.08,0),(.62,.035,.09),'Light',.012)
def glass():
 rail();B('glass',(0,0,.57),(2.7,.025,.75),'Glass',.012)
def holo():
 B('base',(0,0,.2),(.65,.65,.4));B('column',(0,0,1.3),(.12,.12,2.5),'Light')
 for z in (.65,1.2,1.8):B('display',(0,-.16,z),(1.1,.035,.34),'Holo')
def gate():
 for x in (-1.25,1.25):B('gatepost',(x,0,2),(.35,.7,4),'Metal');B('lens',(x,-.4,1.4),(.12,.06,.75),'Light')
 B('lintel',(0,0,4.1),(3,.7,.45));B('gatewarning',(0,-.4,4.15),(1.8,.025,.12),'Hazard')
def cable():
 B('tray',(0,0,.25),(2.8,.35,.12),'Metal')
 for i in range(4):C('cable',(-1.4,-.1+i*.065,.36),(1.4,-.1+i*.065,.36),.02,'Black',8)
 for x in (-1,0,1):B('strap',(x,0,.39),(.04,.34,.08),'Metal')
def shutter():
 B('frame',(0,0,1.8),(2,.25,3.6),'Metal')
 for k in range(11):B('slat',(0,-.16,.25+k*.28),(1.8,.16,.16),'Navy')
def rod():
 C('rod',(0,0,0),(0,0,4),.065);C('tip',(0,0,4),(0,0,4.6),.025)
 for i in range(3):C('ring',(0,0,.2+i*.15),(0,0,.27+i*.15),.17,'Black')
def relay():
 C('mast',(0,0,0),(0,0,2),.13);B('yoke',(0,0,1.8),(.9,.3,.4),'Metal')
 # Parabolic dish with an open silhouette and visible receiver arm.
 vs=[(0,-.2,2.7)];rings=4;seg=20
 for j in range(1,rings+1):
  r=j/rings*1.2
  for i in range(seg):a=i/seg*math.tau;vs.append((r*math.cos(a),-.2+.3*r*r,2.7+r*math.sin(a)))
 fs=[]
 for i in range(seg):fs.append((0,1+i,1+(i+1)%seg))
 for j in range(rings-1):
  for i in range(seg):a=1+j*seg+i;b=1+j*seg+(i+1)%seg;fs.append((a,b,b+seg,a+seg))
 mesh_obj('dish',vs,fs,M['Metal']);C('receiver',(0,-.1,2.7),(0,-1.1,2.7),.045);B('receiverhead',(0,-1.1,2.7),(.16,.2,.16),'Light')
def antenna():
 rod()
 for z in (1.6,2.3,3):C('bar',(-.7,0,z),(.7,0,z),.035)
def windsock():
 C('pole',(0,0,0),(0,0,3),.045);C('sock',(0,0,3),(1.2,0,2.8),.23,'Hazard',12)
 for x in (.25,.65,1):C('stripe',(x,0,3-x*.16),(x+.08,0,3-(x+.08)*.16),.22-x*.1,'Black')
def pipe():
 for x in (-.45,.45):
  C('pipe',(x,0,0),(x,0,2.8),.14);C('flange',(x,0,.6),(x,0,.72),.22)
 C('cross',(-.6,0,2.2),(.6,0,2.2),.1);C('valve',(0,0,2.2),(0,-.3,2.2),.05)
 C('wheel',(0,-.31,2.2),(0,-.37,2.2),.25,'Hazard',16)
def furnace():
 B('shell',(0,0,1.5),(2.2,1.8,3),'Metal');B('mouth',(0,-.95,1.2),(1.5,.05,1.4),'Black')
 for x in (-.55,0,.55):B('heat',(x,-.99,1.1),(.15,.025,1),'Heat')
 B('hood',(0,0,3.15),(2.5,2,.3));C('exhaust',(0,0,3),(0,0,4),.32)
 for z in (.3,2.5):B('stripe',(0,-1.02,z),(2,.04,.12),'Hazard')
def crucible():
 C('pot',(0,0,.25),(0,0,1.5),.65,'Metal',16);C('slag',(0,0,1.5),(0,0,1.52),.6,'Heat',20)
 for x in (-.8,.8):B('handle',(x,0,1),(.2,.6,.25),'Metal')
def crane():
 for x in (-1.8,1.8):B('pillar',(x,0,2),(.25,.5,4),'Metal')
 B('rail',(0,0,4),(4.3,.6,.25),'Hazard');B('trolley',(0,0,3.75),(.65,.7,.5),'Metal')
 C('cable',(0,0,3.7),(0,0,1.7),.025,'Black');C('hook',(0,0,1.7),(.2,0,1.35),.065)
def escape():
 B('balcony',(0,0,1.2),(2.8,1.3,.15),'Metal');rail()
 for x in (-1.1,1.1):C('ladder',(x,0,0),(x,0,3),.04)
 for z in (.2,.5,.8,1.1,1.4,1.7,2,2.3,2.6):C('rung',(-1.1,0,z),(1.1,0,z),.028)
def neon():
 B('sign',(0,0,1.5),(1.8,.2,2.6),'Black');text('transit',M['Neon'],'NOVA\nTRANSIT',(0,-.14,1.5),.25,.01)
 for x in (-.84,.84):B('edge',(x,-.14,1.5),(.025,.025,2.5),'Neon')
def crate():
 B('crate',(0,0,.5),(1,1,1),'Navy')
 for x in (-.45,.45):B('band',(x,-.51,.5),(.07,.025,.85),'Metal')
 B('label',(0,-.53,.6),(.35,.015,.2),'Hazard')
def train():
 B('hull',(0,0,1.2),(14,2.4,2.2),'Navy',.2)
 for x in range(-6,7,2):B('window',(x,-1.23,1.6),(1.3,.025,.65),'Glass');B('light',(x,-1.25,.5),(1.2,.02,.04),'Light')
 for x in (-5,5):C('wheel',(x,-1,.1),(x,1,.1),.3,'Black')
def cooling():
 C('tower',(0,0,0),(0,0,8),2,'Metal',20);C('rim',(0,0,8),(0,0,8.3),2.3,'Navy',20)
 for a in range(0,360,30):x,y=2.1*math.cos(math.radians(a)),2.1*math.sin(math.radians(a));C('brace',(x,y,0),(x,y,7.8),.06)
def city():
 B('block',(0,0,7),(5,4,14),'Navy',.12)
 for x in (-1.7,0,1.7):
  for z in range(2,14,2):B('window',(x,-2.02,z),(.6,.02,.7),'Light')
common={'Rail':rail,'DeckPanel':deck,'Support':support,'Light':light}
special={'arena':{'Glass':glass,'Holo':holo,'GateHardware':gate},'tower':{'Shutter':shutter,'Rod':rod,'Cable':cable},'skyline':{'Relay':relay,'Antenna':antenna,'Windsock':windsock},'foundry':{'Furnace':furnace,'Pipe':pipe,'Crucible':crucible,'Crane':crane},'undercity':{'FireEscape':escape,'Neon':neon,'Cable':cable,'Crate':crate,'Train':train,'Cooling':cooling,'CityBlock':city}}
for name,fn in {**common,**special[ZONE]}.items():piece(name,fn)
os.makedirs(OUT,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,ZONE+'_kit.blend'))
