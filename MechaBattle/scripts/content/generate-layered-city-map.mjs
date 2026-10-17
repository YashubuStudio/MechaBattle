import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(fileURLToPath(new URL('../../../',import.meta.url)));
const map={id:'layered-city',prototypeOnly:true,halfWidth:420,halfDepth:280,surfaceY:16,mainBases:[],capturePoints:[],obstacles:[],navigationNodes:[]};
function box(id,x,z,width,depth,bottom,height,platform=false){map.obstacles.push({id,x,y:bottom+height/2,z,width,height,depth,platform});}
function base(id,team,x,y,z,sx,sy,sz){map.mainBases.push({id,team,x,y,z,spawnRadius:6,attackRadius:4,buildingRadius:23,approachRoute:id==='B'||id==='Y'?'shielded-dogleg':id==='C'||id==='Z'?'underground-folds':'north-close-blocks',spawnOverride:true,spawnX:sx,spawnY:sy,spawnZ:sz});}
base('A',0,-280,16,185,-280,16,160);base('B',0,-380,16,0,-205,16,0);base('C',0,-280,0,-220,-280,0,-200);
base('X',1,280,16,185,280,16,160);base('Y',1,380,16,0,205,16,0);base('Z',1,280,0,-220,280,0,-200);
for(const [id,x,y,z] of [['1',0,16,155],['2',-230,16,120],['3',230,16,120],['4',0,0,-135],['5',-180,16,-65],['6',180,16,-65]])map.capturePoints.push({id,x,y,z,radius:13});
const nodes=map.navigationNodes;
function node(id,x,y,z){const index=nodes.length;nodes.push({id:'nav-'+id,x,y,z,links:[]});return index;}
function link(a,b){if(!nodes[a].links.includes(b))nodes[a].links.push(b);if(!nodes[b].links.includes(a))nodes[b].links.push(a);}
function chain(points){const ids=points.map(p=>node(...p));for(let i=1;i<ids.length;i++)link(ids[i-1],ids[i]);return ids;}
const center=node('center',0,16,0),west=node('west-front',-100,16,0),east=node('east-front',100,16,0);link(center,west);link(center,east);
const north=chain([['north-street',0,16,85],['relay-1',0,16,155]]);link(center,north[0]);
const fronts=[];const entries=[];
for(const side of [-1,1]){
  const tag=side<0?'west':'east';
  const front=node(tag+'-spawn',side*205,16,0);fronts.push(front);link(side<0?west:east,front);
  const upper=chain([[tag+'-north-turn',side*160,16,70],[tag+'-relay',side*230,16,120],[tag+'-north-alley',side*260,16,135],[tag+'-base-A-X',side*280,16,172]]);link(front,upper[0]);link(north[0],upper[0]);
  const back=chain([[tag+'-rear-approach',side*300,16,0],[tag+'-rear-turn-1',side*320,16,60],[tag+'-rear-turn-2',side*380,16,60],[tag+'-base-B-Y',side*380,16,18]]);link(front,back[0]);
  const lower=chain([[tag+'-south-turn',side*205,16,-35],[tag+'-relay-5-6',side*180,16,-65],[tag+'-stair-lip',side*180,16,-80]]);link(front,lower[0]);entries.push(lower[1]);
  for(const [i,x,z,w,d,h] of [[1,300,125,34,40,38],[2,193,158,30,34,28],[3,305,188,20,45,40],[4,245,213,22,40,32],[5,150,200,34,56,48],[6,340,230,42,40,35]])box('NorthBuilding-'+tag+'-'+i,side*x,z,w,d,16,h);
  box('B-Y-front-shield-'+tag,side*350,0,10,88,16,28);box('B-Y-south-shield-'+tag,side*380,-35,64,8,16,24);
  box('Relay5-6-enemy-sniper-block-'+tag,side*130,-65,28,42,16,32);
  box('Downtown-'+tag+'-1',side*90,-105,30,42,16,44);box('Downtown-'+tag+'-2',side*75,115,30,38,16,35);
  for(const [i,x,z,w,d,h] of [[1,275,-195,38,48,34],[2,340,-210,44,44,26],[3,80,-205,30,46,40],[4,135,-240,36,40,32],[5,220,-245,36,32,42],[6,85,235,38,42,36],[7,80,65,20,28,27]])box('DowntownBlock-'+tag+'-'+i,side*x,z,w,d,16,h);
  box('StreetCover-'+tag+'-1',side*55,15,12,8,16,1.8);box('StreetCover-'+tag+'-2',side*62,-30,10,12,16,2.6);
}
// The lower corridor is original geometry: repeated right-angle folds conceal each core.
const tunnel=[[-280,-220],[-230,-220],[-230,-170],[-180,-170],[-130,-170],[-130,-225],[-60,-225],[-60,-135],[0,-135],[60,-135],[60,-225],[130,-225],[130,-170],[180,-170],[230,-170],[230,-220],[280,-220]];
const tunnelNodes=chain(tunnel.map(([x,z],i)=>['under-'+i,x,0,z]));
const open=[];function corridor(a,b,width=24){open.push({minX:Math.min(a[0],b[0])-width/2,maxX:Math.max(a[0],b[0])+width/2,minZ:Math.min(a[1],b[1])-width/2,maxZ:Math.max(a[1],b[1])+width/2});}
for(let i=1;i<tunnel.length;i++)corridor(tunnel[i-1],tunnel[i]);
for(const side of [-1,1]){
  open.push({minX:side*280-23,maxX:side*280+23,minZ:-244,maxZ:-184});
  corridor([side*180,-170],[side*180,-75],22);
  const stairs=[];for(let k=0;k<=16;k++)stairs.push(node((side<0?'west':'east')+'-stair-'+k,side*180,16-k,-80-k*4));
  link(entries[side<0?0:1]+1,stairs[0]);for(let k=1;k<stairs.length;k++)link(stairs[k-1],stairs[k]);
  const exit=node((side<0?'west':'east')+'-stair-exit',side*180,0,-153);link(stairs.at(-1),exit);link(exit,tunnelNodes[side<0?3:13]);
  for(let k=0;k<64;k++)box('Access-stair-'+side+'-'+k,side*180,-80.5-k,18,1.1,0,16-k*.25,true);
}
// A finite solid basement outside the corridors. Merge occupied grid cells to keep volumes bounded.
const step=10,nx=84,nz=56;
function mergedCells(predicate,emit){const runs=new Map();for(let iz=0;iz<nz;iz++){const next=new Map();for(let ix=0;ix<nx;){if(!predicate(ix,iz)){ix++;continue;}const start=ix;while(ix<nx&&predicate(ix,iz))ix++;const key=start+':'+ix;const prev=runs.get(key);next.set(key,prev?{...prev,endZ:iz+1}:{startX:start,endX:ix,startZ:iz,endZ:iz+1});}for(const [key,rect] of runs)if(!next.has(key))emit(rect);runs.clear();for(const [key,v] of next)runs.set(key,v);}for(const rect of runs.values())emit(rect);}
let serial=0;
mergedCells((ix,iz)=>{const x=-420+(ix+.5)*step,z=-280+(iz+.5)*step;return !open.some(r=>x>=r.minX-5&&x<=r.maxX+5&&z>=r.minZ-5&&z<=r.maxZ+5);},r=>box('Basement-wall-'+serial++,-420+(r.startX+r.endX)*step/2,-280+(r.startZ+r.endZ)*step/2,(r.endX-r.startX)*step,(r.endZ-r.startZ)*step,0,15.2));
// Street deck with just the two real stair openings. Corridors have a continuous ceiling.
serial=0;mergedCells((ix,iz)=>{const x=-420+(ix+.5)*step,z=-280+(iz+.5)*step;return !(Math.abs(Math.abs(x)-180)<15&&z>-150&&z<-80);},r=>box('Street-deck-'+serial++,-420+(r.startX+r.endX)*step/2,-280+(r.startZ+r.endZ)*step/2,(r.endX-r.startX)*step,(r.endZ-r.startZ)*step,15.2,.8,true));
// Small landings close the half-cell gaps at the entrance without sealing the descent.
for(const side of [-1,1])box('Access-top-landing-'+side,side*180,-79,22,3,15.2,.8,true);
map.designNotes={original:true,source:'User-described two-layer urban topology; no imported IP map geometry or imagery.',north:'1 is central apex; 2 west near A; 3 east near X. Direct center-to-1 street is unobstructed.',south:'4 below the street in the C-Z folded corridor; 5/6 above ground at the staircase mouths. Enemy-facing buildings screen both mouths.',rear:'B/Y cores sit far back behind dogleg shields; their valid spawns are forward near the central street. All cores remain destructible.'};
await fs.writeFile(path.join(root,'MechaBattle/config/layered-city-map.json'),JSON.stringify(map,null,2)+String.fromCharCode(10));
await fs.writeFile(path.join(root,'MechaBattle/Assets/Game/Resources/MechaBattleMaps/LayeredCity.json'),JSON.stringify(map,null,2)+String.fromCharCode(10));
const file=path.join(root,'MechaBattle/config/map-catalog.json');const catalog=JSON.parse(await fs.readFile(file,'utf8'));catalog.entries=catalog.entries.filter(x=>!['p0-three-lanes','terraced-works','layered-city'].includes(x.id));catalog.entries.push({id:map.id,title:'二層都心・地下回廊',description:'地上のビル街と折れた地下通路。北1/2/3、地下4、地上入口5/6。後方本拠点は前方出撃。',resourcePath:'MechaBattleMaps/LayeredCity',sourceFile:'layered-city-map.json',theme:'urban'});await fs.writeFile(file,JSON.stringify(catalog,null,2)+String.fromCharCode(10));await fs.writeFile(path.join(root,'MechaBattle/Assets/Game/Resources/MechaBattleMaps/MapCatalog.json'),JSON.stringify(catalog,null,2)+String.fromCharCode(10));
console.log(JSON.stringify({map:map.id,obstacles:map.obstacles.length,nodes:nodes.length,bases:map.mainBases.length,relays:map.capturePoints.length,catalog:catalog.entries.map(x=>x.id)}));
