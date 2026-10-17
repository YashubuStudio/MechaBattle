import json,sys,math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont
workspace=Path(__file__).resolve().parents[3]
m=json.loads((workspace/'MechaBattle/config/urban-overpass-map.json').read_text(encoding='utf-8'))
output=Path(sys.argv[1]) if len(sys.argv)>1 else workspace/'MechaBattle/Build/Reports/UrbanOverpass_preview.png'
img=Image.new('RGB',(1900,1120),'#111b29');draw=ImageDraw.Draw(img)
def font(size,bold=False):return ImageFont.truetype('C:/Windows/Fonts/'+('segoeuib.ttf' if bold else 'segoeui.ttf'),size)
def text(xy,s,size=24,color='#ecf2f9'):draw.text(xy,s,font=font(size,True),fill=color)
text((38,28),'MECHABATTLE / URBAN OVERPASS',38)
text((40,82),'640 x 420 m / 6 hollow multi-storey buildings / 2 covered hangars / 2 skybridges',23,'#87d8cf')
right=np.array([.7071,0,-.7071]);up=np.array([.3536,.866,.3536]);eye=-np.cross(right,up)
def render(boxes,center,scale,shift=(0,0,0),cut=False):
    shift=np.array(shift)
    def pt(v):
        v=np.array(v)-shift
        return (center[0]+np.dot(v,right)*scale,center[1]-np.dot(v,up)*scale)
    ground=[(-320,0,-210),(-320,0,210),(320,0,210),(320,0,-210)] if not cut else [(-95,0,40),(-95,0,90),(-45,0,90),(-45,0,40)]
    draw.polygon([pt(v) for v in ground],fill='#263d44',outline='#59727a')
    if not cut:
        draw.polygon([pt(v) for v in [(-320,.01,-19),(-320,.01,19),(320,.01,19),(320,.01,-19)]],fill='#151d26')
    faces=[]
    for o in boxes:
        lo=np.array([o['x']-o['width']/2,o['y']-o['height']/2,o['z']-o['depth']/2]);hi=np.array([o['x']+o['width']/2,o['y']+o['height']/2,o['z']+o['depth']/2]);x,y,z=lo;X,Y,Z=hi
        name=o['id'];base=np.array([131,137,130])
        if o.get('platform'):base=np.array([65,111,126])
        if any(t in name for t in ['stair','landing','balcony','ledge']):base=np.array([192,141,66])
        if 'Skybridge' in name:base=np.array([72,154,175])
        for normal,verts,shade in [((0,1,0),[(x,Y,z),(X,Y,z),(X,Y,Z),(x,Y,Z)],1.12),((-1,0,0),[(x,y,z),(x,y,Z),(x,Y,Z),(x,Y,z)],.75),((0,0,-1),[(x,y,z),(X,y,z),(X,Y,z),(x,Y,z)],.91)]:
            if np.dot(normal,eye)<=0:continue
            dep=np.dot(np.mean(verts,axis=0),eye)
            faces.append((dep,verts,tuple(np.minimum(255,base*shade).astype(int))))
    for _,verts,color in sorted(faces,key=lambda f:f[0]):draw.polygon([pt(v) for v in verts],fill=color,outline=tuple(max(0,c-20) for c in color))
    if not cut:
        for b in m['mainBases']:
            q=pt((b['x'],1,b['z']));col='#5d9fff' if b['team']==0 else '#ff9f59'
            draw.ellipse((q[0]-8,q[1]-6,q[0]+8,q[1]+6),fill=col);draw.text((q[0]+8,q[1]),b['id'],font=font(14,True),fill=col)
        for c in m['capturePoints']:
            q=pt((c['x'],1,c['z']));draw.polygon([(q[0],q[1]-8),(q[0]+8,q[1]),(q[0],q[1]+8),(q[0]-8,q[1])],fill='#ffdb71')
render(m['obstacles'],(700,590),1.65)
draw.rounded_rectangle((1280,145,1860,860),radius=16,fill='#1c2c3c',outline='#486477',width=2)
text((1300,170),'ATRIUM / CUTAWAY',26,'#87d8cf')
text((1300,215),'Actual collision volumes, exterior opened',17)
cut=[o for o in m['obstacles'] if o['id'].startswith('Atrium-West-North') and 'side-' not in o['id'] and '-door--1-' not in o['id'] and '-floor-2' not in o['id'] and '-lintel--1-' not in o['id']]
render(cut,(1560,535),7.8,(-70,0,65),True)
text((1300,740),'24 x 0.25 m stairs -> 6 m storey',20,'#f4c66e')
text((1300,775),'Open stair/boost well -> upper levels',19)
text((1300,810),'Front / rear 8 m doorways',19)
text((40,963),'GROUND: central boulevard, courtyards and concealed side alleys',24)
text((40,1003),'VERTICAL: 6 / 12 / 18 / 24 m floors, balconies and roof access',24,'#87d8cf')
text((40,1060),'SOURCE GEOMETRY PREVIEW / no Unity rendering or performance result is implied',19,'#b0c3d3')
output.parent.mkdir(parents=True,exist_ok=True);img.save(output);print(output)
