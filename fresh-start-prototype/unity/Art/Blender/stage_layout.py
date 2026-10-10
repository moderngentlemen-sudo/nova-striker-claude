"""Placement shared by Blender review and runtime export, from LayoutExport's authoritative data."""
import json, math
from pathlib import Path
DATA=json.loads((Path(__file__).with_name('stage_layout.json')).read_text())
def frame(x):
    return DATA['frames'][max(0,min(len(DATA['frames'])-1,round((x+12)*2)))]
def placements(zone):
    z=next(z for z in DATA['zones'] if z['id']==zone); out=[]
    def put(a,x,y,d=-3.05,yaw=0,sx=1,sy=1,sz=1):out.append(dict(asset=a,x=x,y=y,dz=d,yaw=yaw,sx=sx,sy=sy,sz=sz))
    boxes=[b for b in DATA['boxes'] if b['x0']>=z['x0'] and b['x1']<=z['x1'] and b['type'] in ('s','o') and b['tag']!='bound' and b['id']<10000]
    for bi,b in enumerate(boxes):
        top=b['y1'];w=b['x1']-b['x0']
        for x in range(math.ceil(b['x0']+1),math.floor(b['x1']),5):
            put('Rail',x,top);put('DeckPanel',x,top-0.35,2.82)
            if (x+bi)%3==0:put('Light',x,top+0.5)
        if w>4:put('Support',b['x0']+1,top-2,-3.25)
        if w>7:
            x=(b['x0']+b['x1'])/2
            specials={'arena':['Glass','Holo','GateHardware'],'tower':['Shutter','Rod','Cable'],'skyline':['Relay','Antenna','Windsock'],'foundry':['Furnace','Pipe','Crucible','Crane'],'undercity':['FireEscape','Neon','Cable','Crate']}
            put(specials[zone][bi%len(specials[zone])],x,top,-4.4)
    # Boss dressing stays behind the actual deck; both encounter arenas use their zone kit.
    if zone=='arena':
        for x in (66,73,84,91):put('Holo',x,0,-7);put('Glass',x,7,-3.7)
        put('GateHardware',79,0,-8,sx=2,sy=2,sz=2)
    if zone=='skyline':
        for x in (260,274,286):put('Relay',x,16,-8);put('Antenna',x+3,16,-10)
    if zone=='foundry':
        for x in (424,480,556,620,740):put('Furnace',x,0,-12,sx=2,sy=2,sz=2)
    if zone=='undercity':
        put('Cooling',1146,-2,-20,sx=2,sy=2,sz=2);put('Train',1060,-3,-12)
        for x in (840,868,902,1090,1135):put('CityBlock',x,0,-24,sx=1.5,sy=1.5,sz=1.5)
    return out
