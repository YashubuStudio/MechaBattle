import json, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
workspace=Path(__file__).resolve().parents[3]
project=workspace/'MechaBattle'
catalog=json.loads((project/'config/map-catalog.json').read_text(encoding='utf-8'))
output=Path(sys.argv[1]) if len(sys.argv)>1 else project/'Build/Reports/PlayableMaps_preview.png'
columns=min(3,len(catalog['entries']))
rows=(len(catalog['entries'])+columns-1)//columns
image=Image.new('RGB',(30+columns*590,130+rows*550),'#111a27')
draw=ImageDraw.Draw(image)
def font(size,bold=False):return ImageFont.truetype('C:/Windows/Fonts/'+('segoeuib.ttf' if bold else 'segoeui.ttf'),size)
draw.text((35,20),'MECHABATTLE / SELECTABLE MAPS',font=font(30,True),fill='#edf4fa')
titles={'p0-three-lanes':'CENTRAL INDUSTRIAL FRONT','terraced-works':'TERRACED WORKS','canyon-cross':'CANYON CROSS','urban-overpass':'URBAN OVERPASS'}
for index,entry in enumerate(catalog['entries']):
    m=json.loads((project/'config'/entry['sourceFile']).read_text(encoding='utf-8'))
    left=25+(index%columns)*590
    row=(index//columns)*550
    draw.rounded_rectangle((left,85+row,left+570,616+row),radius=14,fill='#192637',outline='#34485a',width=2)
    draw.text((left+18,102+row),titles.get(m['id'],m['id']),font=font(23,True),fill='#7dd9d1')
    draw.text((left+18,140+row),str(int(m['halfWidth']*2))+' x '+str(int(m['halfDepth']*2))+' m / 3 bases per team',font=font(18),fill='#bdcbd7')
    scale=min(510/(m['halfWidth']*2),360/(m['halfDepth']*2))
    cx,cy=left+285,370+row
    def point(x,z):return (cx+x*scale,cy-z*scale)
    bounds=(*point(-m['halfWidth'],m['halfDepth']),*point(m['halfWidth'],-m['halfDepth']))
    draw.rectangle(bounds,fill='#10212a' if entry['theme']!='canyon' else '#2b241e',outline='#5d6d79',width=2)
    for o in m['obstacles']:
        top=o['y']+o['height']/2
        tint=min(1,top/10)
        color=(int(40+40*tint),int(92+50*tint),int(100+50*tint)) if o.get('platform',False) else (105,111,120)
        if entry['theme']=='canyon':color=(int(104+60*tint),int(77+38*tint),int(47+25*tint)) if o.get('platform',False) else (108,98,88)
        draw.rectangle((*point(o['x']-o['width']/2,o['z']+o['depth']/2),*point(o['x']+o['width']/2,o['z']-o['depth']/2)),fill=color)
    for b in m['mainBases']:
        x,y=point(b['x'],b['z']);color='#4d9eff' if b['team']==0 else '#ff9850'
        draw.rectangle((x-9,y-9,x+9,y+9),fill=color)
        draw.text((x-6,y+11),b['id'][-1],font=font(15,True),fill='#ffffff')
    for c in m['capturePoints']:
        x,y=point(c['x'],c['z']);draw.polygon([(x,y-8),(x+8,y),(x,y+8),(x-8,y)],fill='#f1d46d',outline='#fff2c1')
    height=max(o['y']+o['height']/2 for o in m['obstacles'])
    draw.text((left+18,568+row),str(len(m['obstacles']))+' terrain blocks / highest top '+format(height,'.1f')+' m',font=font(18),fill='#bdcbd7')
draw.text((35,94+rows*550),'SOURCE DATA OVERVIEW / gold: relay / blue-orange: main bases / terrain colors indicate platforms and cover',font=font(20),fill='#bdcbd7')
output.parent.mkdir(parents=True,exist_ok=True)
image.save(output)
print(output)
