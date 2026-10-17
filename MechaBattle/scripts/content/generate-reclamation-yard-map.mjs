import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(fileURLToPath(new URL('../../../',import.meta.url)));
const map={id:'reclamation-yard',prototypeOnly:true,halfWidth:450,halfDepth:260,surfaceY:18,lowerLayerName:'低地',mainBases:[],capturePoints:[],obstacles:[],navigationNodes:[]};
function box(id,x,z,width,depth,bottom,height,platform=false){map.obstacles.push({id,x,y:bottom+height/2,z,width,height,depth,platform});}
function base(id,team,x,y,z,sx,sz,approachRoute){map.mainBases.push({id,team,x,y,z,spawnRadius:7,attackRadius:4,buildingRadius:23,spawnOverride:true,spawnX:sx,spawnY:y,spawnZ:sz,approachRoute});}
base('A',0,-350,18,185,-325,165,'factory-alley');base('B',0,-265,18,0,-220,-10,'scrap-front');base('C',0,-405,0,-215,-375,-215,'lower-service');
base('X',1,350,18,185,325,165,'factory-alley');base('Y',1,265,18,0,220,-10,'scrap-front');base('Z',1,405,0,-215,375,-215,'lower-service');
for(const [id,x,y,z] of [['1',0,18,20],['2',-210,18,120],['3',210,18,120],['4',0,0,-180],['5',-160,18,-60],['6',160,18,-60]])map.capturePoints.push({id,x,y,z,radius:12});
// The whole north/center is an 18m raised terrace. The south is open-air low ground.
box('YardUpperDeck',0,90,900,340,0,18,true);
for(const x of [-235,-95,95,235]){
 const id='Factory-'+x;
 box(id+'-west-wall',x-32,170,2,60,18,8);box(id+'-east-wall',x+32,170,2,60,18,8);
 for(const z of [140,200])for(const s of [-1,1])box(id+'-door-jamb-'+z+'-'+s,x+s*20, z,24,2,18,8);
 box(id+'-roof',x,170,66,62,25.4,.6,true);
 box(id+'-machine',x-14,174,8,10,18,3);box(id+'-pallet',x+15,182,7,9,18,2,true);
}
for(const side of [-1,1]){
 for(const [id,x,z,w,d,h] of [[1,310,225,44,20,12],[2,155,226,22,20,10],[3,35,208,16,35,9],[4,320,112,10,28,5]])box('FactoryStore-'+side+'-'+id,side*x,z,w,d,18,h);
 box('FactoryGantry-pier-'+side,side*85,90,3,14,18,6);
 for(const [k,z,h] of [[0,62,2],[1,70,4],[2,78,6]])box('YardAccess-pallet-'+side+'-'+k,side*85,z,10,8,18,h,true);
 for(const [ix,x] of [[0,80],[1,170]])for(const [iz,z] of [[0,-36],[1,36]]){
  const id='Scrap-'+side+'-'+ix+'-'+iz;
  box(id+'-base',side*x,z,30,22,18,4,true);
  box(id+'-stack',side*(x+3),z+1,22,16,22,3,true);
  box(id+'-cap',side*(x+5),z-1,12,10,25,3,true);
 }
 box('Scrap-front-cover-'+side,side*105,12,7,7,18,1.6);
 box('Scrap-east-west-screen-'+side,side*52,60,16,14,18,6,true);
 for(const [id,x,z,w,d,h] of [[1,80,-130,25,14,4],[2,220,-225,28,16,4],[3,290,-120,18,22,4],[4,42,-237,24,14,3],[5,400,-145,26,20,6]])box('Service-cargo-'+side+'-'+id,side*x,z,w,d,0,h,true);
 box('Service-core-screen-'+side,side*420,-185,10,32,0,9);
 // Outer service ramps are the long route back up; the center cliff is a drop only.
 for(let k=0;k<72;k++)box('YardAccess-ramp-'+side+'-'+k,side*350,-200+(k+.5)*(120/72),24,1.7,0,(k+1)*.25,true);
 box('YardAccess-landing-'+side,side*350,-79,26,3,0,18,true);
}
// Smaller workshops, crane frames and additional scrap make distinct local combat spaces.
for(const x of [-165,0,165]){
 box('FactoryWorkshop-'+x+'-back',x,188,24,2,18,6);
 for(const side of [-1,1])box('FactoryWorkshop-'+x+'-side-'+side,x+side*12,174,2,28,18,6);
 box('FactoryWorkshop-'+x+'-roof',x,174,26,30,23.5,.5,true);
}
for(const side of [-1,1]){
 box('FactoryRearShield-'+side,side*350,213,70,4,18,9);
 box('FactoryOuterShield-'+side,side*390,190,6,50,18,8);
 for(const leg of [-1,1])box('FactoryCrane-'+side+'-leg-'+leg,side*(250+leg*20),65,3,8,18,16);
 box('FactoryCrane-'+side+'-beam',side*250,65,48,8,34,2,true);
 for(const [tag,x,z,w,d] of [['inner',40,-30,22,18],['outer-north',250,50,38,26],['outer-south',250,-50,38,26]]){
  box('ScrapDetail-'+side+'-'+tag+'-base',side*x,z,w,d,18,4,true);
  box('ScrapDetail-'+side+'-'+tag+'-tier',side*(x+2),z+1,w*.72,d*.72,22,4,true);
  box('ScrapDetail-'+side+'-'+tag+'-top',side*(x+3),z,w*.42,d*.42,26,4,true);
 }
 box('ScrapBarrier-'+side,side*60,-8,12,4,18,2.3);
 box('ServiceStoredCargo-'+side,side*130,-225,35,16,0,4,true);
 box('ServiceRelayCover-'+side,side*30,-200,16,10,0,2.2);
}
box('FactoryGantry-deck',0,90,174,14,23.4,.6,true);
box('Scrap-crosswalk',0,36,120,8,26.4,.6,true);
box('Service-overpass',0,-225,100,12,5.4,.6,true);
for(const x of [-47,47])box('Service-overpass-pier-'+x,x,-225,3,12,0,5.4);
const nodes=map.navigationNodes;
function node(id,x,y,z){nodes.push({id:'yard-nav-'+id,x,y,z,links:[]});return nodes.length-1;}
function link(a,b,both=true){if(!nodes[a].links.includes(b))nodes[a].links.push(b);if(both&&!nodes[b].links.includes(a))nodes[b].links.push(a);}
function chain(points){const ids=points.map(p=>node(...p));for(let i=1;i<ids.length;i++)link(ids[i-1],ids[i]);return ids;}
const center=chain([['main-W',-235,18,0],['main-W2',-180,18,0],['main-W3',-100,18,0],['main',0,18,0],['main-E3',100,18,0],['main-E2',180,18,0],['main-E',235,18,0]]);
const north=chain([['relay-1',0,18,20],['factory-front',0,18,110]]);link(center[3],north[0]);
const lower=chain([['lower-W3',-280,0,-180],['lower-W2',-190,0,-180],['lower-W',-90,0,-180],['relay-4',0,0,-180],['lower-E',90,0,-180],['lower-E2',190,0,-180],['lower-E3',280,0,-180]]);
const drop=chain([['drop-center-lip',0,18,-77]]);link(center[3],drop[0]);const landing=node('drop-center-floor',0,0,-105);link(drop[0],landing,false);link(landing,lower[3]);
for(const side of [-1,1]){
 const west=side<0,tag=west?'W':'E',front=center[west?0:6];
 const coreB=node('base-BY-'+tag,side*257,18,0);link(front,coreB);
 const factory=chain([[tag+'-factory-turn',side*210,18,90],[tag+'-relay-23',side*210,18,120],[tag+'-factory-alley',side*300,18,120],[tag+'-factory-back',side*350,18,150],[tag+'-base-AX',side*350,18,172]]);link(front,factory[0]);link(north[1],factory[1]);
 const mouth=chain([[tag+'-south-turn',side*210,18,-15],[tag+'-south-alley',side*210,18,-60],[tag+'-relay-56',side*160,18,-60],[tag+'-drop-lip',side*160,18,-77]]);link(front,mouth[0]);const floor=node(tag+'-drop-floor',side*160,0,-105);link(mouth.at(-1),floor,false);link(floor,lower[west?1:5]);
 const rear=chain([[tag+'-low-rear',side*320,0,-215],[tag+'-low-spawn',side*375,0,-215],[tag+'-base-CZ',side*392,0,-215]]);link(lower[west?0:6],rear[0]);
 const ramp=chain(Array.from({length:13},(_,k)=>[tag+'-ramp-'+k,side*350,k*1.5,-200+k*10]));link(rear[0],ramp[0]);link(rear[1],ramp[0]);
 const rampTop=chain([[tag+'-ramp-exit',side*350,18,-60],[tag+'-upper-rear',side*300,18,-45]]);link(ramp.at(-1),rampTop[0]);link(rampTop[0],rampTop[1]);link(rampTop[1],front);link(rampTop[0],factory[3]);
}
map.designNotes={original:true,reference:'https://ggame.jp/news/detail.php?id=2184',source:'Original greybox inspired by the published factory / junkyard / lower-area combat roles; no copied imagery, geometry or IP assets.',north:'Dense open-door workshops with roofs, narrow alleys and a raised gantry for close combat and infiltration.',center:'Staggered climbable scrap stacks, overhead walkway and wider sight gaps; relay 1 and the nearer B/Y core approaches hold the main line.',south:'Open-air lower service basin at Y0, 18m below the yard. Central ledges are one-way drops. Two outer 120m stepped ramps provide longer returns; directed public nav models the drops.',objectives:'Three finite cores per team ABC/XYZ, six neutral relays. All routes use shared movement, fuel, ammunition and perception rules.'};
for(const file of ['MechaBattle/config/reclamation-yard-map.json','MechaBattle/Assets/Game/Resources/MechaBattleMaps/ReclamationYard.json'])await fs.writeFile(path.join(root,file),JSON.stringify(map,null,2)+'\n');
const catalogPath=path.join(root,'MechaBattle/config/map-catalog.json');const catalog=JSON.parse(await fs.readFile(catalogPath,'utf8'));catalog.entries=catalog.entries.filter(e=>e.id!==map.id);catalog.entries.push({id:map.id,title:'再生工廠・三帯戦線',description:'北の工場路地、中央の積層ジャンク、18m下の南部低地。中央から飛び降り、外周ランプで帰還。',resourcePath:'MechaBattleMaps/ReclamationYard',sourceFile:'reclamation-yard-map.json',theme:'works'});
for(const file of ['MechaBattle/config/map-catalog.json','MechaBattle/Assets/Game/Resources/MechaBattleMaps/MapCatalog.json'])await fs.writeFile(path.join(root,file),JSON.stringify(catalog,null,2)+'\n');
console.log(JSON.stringify({map:map.id,obstacles:map.obstacles.length,nodes:nodes.length,mainBases:map.mainBases.length,relays:map.capturePoints.length,maps:catalog.entries.map(e=>e.id)}));
