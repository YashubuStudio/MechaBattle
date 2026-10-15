import json, math
import numpy as np
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter

workspace = Path(__file__).resolve().parents[3]
assets = workspace / 'MechaBattle/Assets/Game/Content/Models/MB_Worker01'
output = workspace / 'ArtSource/MB_Worker01'
mesh = json.loads((assets / 'MB_Worker01.mesh.json').read_text(encoding='utf-8'))
spec = json.loads((assets / 'model-spec.json').read_text(encoding='utf-8'))
vertices = [(v['x'], v['y'], v['z']) for v in mesh['vertices']]
normals = [(v['x'], v['y'], v['z']) for v in mesh['normals']]
S = 2
W, H = 1600, 900
image = Image.new('RGB', (W*S,H*S), '#111a27')
draw = ImageDraw.Draw(image)
for y in range(H*S):
    t = y/(H*S)
    draw.line((0,y,W*S,y), fill=(int(17+5*t),int(26+7*t),int(39+10*t)))

def font(size, bold=False):
    name = 'segoeuib.ttf' if bold else 'segoeui.ttf'
    return ImageFont.truetype('C:/Windows/Fonts/'+name, int(size*S))
def text(x,y,value,size=18,color='#a3b1c5',bold=False):
    draw.text((x*S,y*S),value,font=font(size,bold),fill=color)
def norm(v):
    length=math.sqrt(sum(x*x for x in v));return tuple(x/length for x in v)
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])

def render(cx,cy,scale,eye,armor,shadow=True,reinforced=False):
    source = json.loads((assets / 'MB_Worker01_reinforced.mesh.json').read_text(encoding='utf-8')) if reinforced else mesh
    vertices = [(v['x'], v['y'], v['z']) for v in source['vertices']]
    normals = [(v['x'], v['y'], v['z']) for v in source['normals']]
    view=norm(eye);right=norm(cross((0,1,0),view));up=cross(view,right)
    target=(0,1.22,0)
    light=norm((-3,7,6))
    if shadow:
        layer=Image.new('RGBA',image.size,(0,0,0,0));sd=ImageDraw.Draw(layer)
        floor_y=cy+dot((0,1.22,0),up)*scale
        sd.ellipse(((cx-scale*0.69)*S,(floor_y-scale*0.08)*S,(cx+scale*0.69)*S,(floor_y+scale*0.13)*S),fill=(0,0,0,125))
        layer=layer.filter(ImageFilter.GaussianBlur(13*S))
        image.paste(Image.alpha_composite(image.convert('RGBA'),layer).convert('RGB'))
    polygons=[]
    colors=[armor,(0.10,0.14,0.19),(0.40,1.0,0.85)]
    for material,group in enumerate(source['subMeshes']):
        idx=group['triangles']
        for i in range(0,len(idx),3):
            ids=idx[i:i+3];n=normals[ids[0]]
            if dot(n,view)<=0:continue
            ps=[tuple(vertices[k][a]-target[a] for a in range(3)) for k in ids]
            screen=[((cx+dot(p,right)*scale)*S,(cy-dot(p,up)*scale)*S) for p in ps]
            depth=sum(dot(p,view) for p in ps)/3
            shade=1.0 if material==2 else 0.48+0.52*max(0,dot(n,light))
            base=colors[material]
            color=tuple(min(255,max(0,int((c*shade)**0.80*255))) for c in base)
            polygons.append(([dot(p,view) for p in ps],screen,color))
    # Resolve visibility per pixel; overlapping armor parts cannot be rendered
    # accurately by sorting whole triangles by their average depth.
    pixels=np.array(image)
    zbuffer=np.full((H*S,W*S),-np.inf,dtype=np.float32)
    for depths,points,color in polygons:
        (ax,ay),(bx,by),(cxp,cyp)=points
        xmin=max(0,int(math.floor(min(ax,bx,cxp))))
        xmax=min(W*S-1,int(math.ceil(max(ax,bx,cxp))))
        ymin=max(0,int(math.floor(min(ay,by,cyp))))
        ymax=min(H*S-1,int(math.ceil(max(ay,by,cyp))))
        denom=(by-cyp)*(ax-cxp)+(cxp-bx)*(ay-cyp)
        if abs(denom)<1e-9 or xmin>xmax or ymin>ymax:continue
        yy,xx=np.mgrid[ymin:ymax+1,xmin:xmax+1]
        xx=xx+0.5;yy=yy+0.5
        a=((by-cyp)*(xx-cxp)+(cxp-bx)*(yy-cyp))/denom
        b=((cyp-ay)*(xx-cxp)+(ax-cxp)*(yy-cyp))/denom
        c=1-a-b
        depth=a*depths[0]+b*depths[1]+c*depths[2]
        old=zbuffer[ymin:ymax+1,xmin:xmax+1]
        mask=(a>=-1e-7)&(b>=-1e-7)&(c>=-1e-7)&(depth>old)
        old[mask]=depth[mask]
        pixels[ymin:ymax+1,xmin:xmax+1][mask]=color
    image.paste(Image.fromarray(pixels))

text(70,50,'MECHABATTLE  /  ORIGINAL PROTOTYPE',16,'#7dd9d1',True)
text(67,83,'MB-01  MODULAR FRAME',43,'#ecf2f9',True)
text(70,143,'One frame. Swappable arms and weapons. Two saved builds.',19)
draw.line((1000*S,195*S,1000*S,815*S),fill='#334255',width=S)
text(70,210,'STANDARD ARMS + CARBINE',15,'#6ba6ff',True)
render(490,520,220,(4,2.4,6),(0.16,0.37,0.85))
text(1070,210,'REINFORCED ARMS + LONG RIFLE',15,'#ffb572',True)
render(1275,380,102,(3,1.7,7),(0.95,0.40,0.10),reinforced=True)
text(1070,525,'SHARED FRAME / REAR',15,'#c3cdd9',True)
render(1275,690,102,(-4,2,-6),(0.59,0.65,0.70))
draw=ImageDraw.Draw(image)
draw.line((70*S,835*S,1530*S,835*S),fill='#334255',width=S)
text(70,852,'1,676 / 2,016 TRIANGLES   /   3 MATERIAL SLOTS   /   STATIC PROTOTYPE',15,'#a3b1c5')
text(1090,852,'Y UP  /  +Z FRONT  /  FOOT PIVOT',14,'#a3b1c5')
image.resize((W,H),Image.Resampling.LANCZOS).save(output/'MB_Worker01_preview.png')
print(output/'MB_Worker01_preview.png')
