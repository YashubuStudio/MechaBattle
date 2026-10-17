import json,math,sys
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
root=Path(__file__).resolve().parents[3]
m=json.loads((root/'MechaBattle/config/layered-city-map.json').read_text(encoding='utf-8'))
W,H=2200,1400
im=Image.new('RGB',(W,H),'#101b28');d=ImageDraw.Draw(im)
def font(n,bold=False):return ImageFont.truetype('C:/Windows/Fonts/'+('segoeuib.ttf' if bold else 'segoeui.ttf'),n)
def text(x,y,s,n=22,c='#dbe8f1',bold=False):d.text((x,y),s,font=font(n,bold),fill=c)
text(48,25,'MECHABATTLE / LAYERED CITY + UNDERGROUND PASSAGES',35,bold=True)
text(50,77,'Original map geometry / north up / 840 x 560 m / source preview, not a Unity screenshot',21,'#9db4c6')
blue='#2584ff';orange='#ff8525'
scale=1.18
panels=[(50,175,True),(1160,175,False)]
for ox,oy,surface in panels:
    pw,ph=m['halfWidth']*2*scale,m['halfDepth']*2*scale
    text(ox,oy-53,'STREET +16 m' if surface else 'UNDERGROUND 0 m',29,bold=True)
    d.rectangle((ox,oy,ox+pw,oy+ph),fill='#243749' if surface else '#153a40',outline='#476376',width=2)
    def p(x,z):return (ox+(x+m['halfWidth'])*scale,oy+(m['halfDepth']-z)*scale)
    for o in m['obstacles']:
        lo=o['y']-o['height']/2;hi=o['y']+o['height']/2
        if surface:
            if lo<15.99:continue
            color='#677d8a' if hi>20 else '#b39a54'
        else:
            if not o['id'].startswith('Basement-wall-'):continue
            color='#17212c'
        x,z=p(o['x']-o['width']/2,o['z']+o['depth']/2)
        d.rectangle((x,z,x+o['width']*scale,z+o['depth']*scale),fill=color,outline='#83949a' if surface else '#263c48')
    ns=m['navigationNodes']
    for i,a in enumerate(ns):
        for j in a['links']:
            b=ns[j]
            if j<=i:continue
            if surface and (a['y']!=16 or b['y']!=16):continue
            if not surface and (a['y']>0 or b['y']>0):continue
            d.line((p(a['x'],a['z']),p(b['x'],b['z'])),fill='#516f84' if surface else '#32bfc8',width=2 if surface else 6)
    for b in m['mainBases']:
        visible=(b.get('y',0)==16)==surface
        if not visible:continue
        x,z=p(b['x'],b['z']);color=blue if b['team']==0 else orange
        d.rectangle((x-13,z-13,x+13,z+13),fill=color,outline='white',width=2)
        text(x-10,z-48,b['id'],27,color,True)
        if b['spawnOverride']:
            sx,sz=p(b['spawnX'],b['spawnZ']);d.ellipse((sx-5,sz-5,sx+5,sz+5),fill='#b4f2b8')
            if abs(b['spawnX']-b['x'])>100:
                d.line(((x,z),(sx,sz)),fill='#71917c',width=2);text(sx-25,sz+10,b['id']+' SPAWN',17,'#b4f2b8',True)
    for c in m['capturePoints']:
        if (c.get('y',0)==16)!=surface:continue
        x,z=p(c['x'],c['z']);d.ellipse((x-18,z-18,x+18,z+18),fill='#ced5da',outline='#f3f7fa',width=2);text(x-8,z-18,c['id'],27,'#142433',True)
    for side,n in [(-1,'5'),(1,'6')]:
        x,z=p(side*180,-112);d.rounded_rectangle((x-13,z-35,x+13,z+35),radius=5,outline='#ffd578',width=3)
        text(x-60,z+38,'ACCESS '+n,17,'#ffd578',True)
    if surface:
        a=p(0,0);b=p(0,130);d.line((a,b),fill='#c9e485',width=4);d.polygon([(b[0],b[1]-10),(b[0]-8,b[1]+9),(b[0]+8,b[1]+9)],fill='#c9e485')
text(50,870,'1: central win opens the north approach. 2/3: close defense of A/X.',23)
text(1160,870,'4: fight inside the folded C-Z corridor. Stairs connect 5/6 to 4.',23)
text(50,912,'B/Y: deep, shielded cores; forward deployment is closest to the central street.',23)
text(1160,912,'5/6: staging at each access; enemy-facing towers block long sniper lines.',23)
# Actual X/Y slice through relay 4. Open lower corridors remain below a continuous street deck.
text(50,983,'SECTION AT z = -135 m / PHYSICAL FLOOR AND CEILING',26,bold=True)
ox,oy=70,1255;sx=2.45;sy=6
for o in m['obstacles']:
    if abs(o['z']+135)>o['depth']/2:continue
    left=ox+(o['x']-o['width']/2+420)*sx;right=left+o['width']*sx;bottom=o['y']-o['height']/2;top=bottom+o['height']
    d.rectangle((left,oy-top*sy,right,oy-bottom*sy),fill='#607588' if bottom>=16 else '#293d4c',outline='#839ca9')
d.line((ox,oy,ox+840*sx,oy),fill='#3acbd1',width=3)
x=ox+420*sx;d.ellipse((x-13,oy-30,x+13,oy-4),fill='#dedfdf');text(x+22,oy-35,'4 / UNDERGROUND',20,'#57dbe0',True)
text(75,oy-16*sy-35,'STREET +16 m',19,'#d8e6b6');text(75,oy+18,'LOWER FLOOR 0 m',19,'#57dbe0')
text(50,1355,'ABC / XYZ: finite 20,000 HP cores. Direct gauge factor 0.08; destruction adds 8,000 once. Prototype tuning.',21,'#9db4c6')
out=Path(sys.argv[1]) if len(sys.argv)>1 else root/'MechaBattle/Build/Reports/LayeredCity.png'
out.parent.mkdir(parents=True,exist_ok=True);im.save(out);print(out)
