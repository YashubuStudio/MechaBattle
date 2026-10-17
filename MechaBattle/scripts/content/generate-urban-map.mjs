import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const project=path.resolve(fileURLToPath(new URL('../../',import.meta.url)));
const map={id:'urban-overpass',prototypeOnly:true,halfWidth:320,halfDepth:210,mainBases:[],capturePoints:[{id:'Central-Square',x:0,z:0,radius:22},{id:'North-Courtyard',x:-15,z:135,radius:18},{id:'South-Service',x:15,z:-135,radius:18}],obstacles:[]};
const round=n=>Math.round(n*1000)/1000;
function box(id,x,y,z,width,height,depth,platform=false){map.obstacles.push({id,x:round(x),y:round(y),z:round(z),width:round(width),height:round(height),depth:round(depth),platform});}
for(let team=0;team<2;team++)for(let lane=0;lane<3;lane++)map.mainBases.push({id:(team?'Orange':'Blue')+'-'+String.fromCharCode(65+lane),team,x:team?280:-280,z:(lane-1)*145,spawnRadius:12,attackRadius:8,buildingRadius:26,approachRoute:['service-alley','central-boulevard','rooftop-courtyard'][lane]});
// Hollow buildings: no enclosing solid box. Floor slabs leave a 9m-wide stair/boost
// well at the east edge. 8m doors, 5.5m clear storeys and open upper windows.
function building(id,x,z,w,d,floors,stairs){
  const floor=6,shaft=9,top=floors*floor;
  for(let level=1;level<=floors;level++){
    const y=level*floor;
    box(id+'-floor-'+level,x-shaft/2,y-.25,z,w-shaft,.5,d,true);
    // landings connect the shaft to the main floor, alternating at each end.
    const end=level%2?1:-1;
    box(id+'-landing-'+level,x+w/2-shaft/2,y-.25,z+end*(d/2-4.5),shaft,.5,9,true);
  }
  for(let side=-1;side<=1;side+=2){
    box(id+'-side-'+side,x+side*(w/2-.55),top/2,z,1.1,top,d);
    for(let front=-1;front<=1;front+=2){
      const zz=z+front*(d/2-.55);
      // Ground doors at the middle of both façades.
      for(let half=-1;half<=1;half+=2)if(side===-1)
        box(id+'-door-'+front+'-'+half,x+half*(w/4+2),3,zz,w/2-4,6,1.1);
      for(let level=1;level<floors;level++)if(side===-1){
        for(const half of [-1,1])box(id+'-sill-'+front+'-'+level+'-'+half,x+half*(w/4+2),level*6+.5,zz,w/2-4,1,1.1);
        box(id+'-lintel-'+front+'-'+level,x,(level+1)*6-.55,zz,w,1.1,1.1);
        for(let p=-1;p<=1;p++)if(level!==1||p!==0)box(id+'-windowpost-'+front+'-'+level+'-'+p,x+p*(w/2-1),level*6+3,zz,1.1,4,1.1);
      }
    }
  }
  if(stairs){
    // A walkable flight to the first floor (0.25m is the shared step limit).
    // Other floors are reached by powered hops in the same open shaft.
    for(let n=1;n<=24;n++)box(id+'-stair-'+n,x+w/2-4.5,n*.25-.125,z-12+(n-.5),6,.25,1.02,true);
  }
  // Exterior balconies provide distinct landings every 6m up to the roof.
  for(let level=1;level<=floors;level++){
    const front=level%2?1:-1;
    box(id+'-balcony-'+level,x,level*6-.25,z+front*(d/2+2.5),12,.5,5,true);
  }
}
building('Atrium-West-North',-70,65,44,40,2,true);
building('Atrium-East-North',70,65,44,40,3,true);
building('Atrium-West-South',-70,-65,44,40,3,true);
building('Atrium-East-South',70,-65,44,40,2,true);
building('Tower-West',-170,72,38,42,4,false);
building('Tower-East',170,-72,38,42,4,false);
// Covered hangars have 10m doors and an elevated roof, rather than solid cargo cubes.
for(const side of [-1,1]){
  const x=side*145,z=-side*137,id='Hangar-'+side;
  box(id+'-roof',x,5.75,z,56,.5,30,true);
  for(const end of [-1,1]){
    box(id+'-side-'+end,x+end*27.5,3,z,1,6,30);
    for(const h of [-1,1])box(id+'-front-'+end+'-'+h,x+h*16.5,3,z+end*14.5,23,6,1);
  }
  for(let level=1;level<=3;level++)box(id+'-loading-ledge-'+level,x-20+level*7,level*1.5-.2,z+20,7,.4,7,true);
}
// Elevated transverse routes cross OVER the boulevard; ground combat goes beneath.
for(const x of [-70,70]){
  box('Skybridge-'+x,x,5.75,0,7,.5,94,true);
  for(const z of [-20,20])for(const side of [-1,1])box('Bridge-pier-'+x+'-'+z+'-'+side,x+side*2.8,2.75,z,1.1,5.5,1.1);
}
for(const side of [-1,1]){
  box('Boulevard-cover-'+side,side*24,1.1,side*19,13,2.2,4);
  box('Courtyard-block-'+side,side*115,2,side*130,18,4,8);
  box('Alley-screen-'+side,side*225,3,-side*75,5,6,28);
}
await fs.writeFile(path.join(project,'config/urban-overpass-map.json'),JSON.stringify(map,null,2)+'\n');
console.log('Urban map: '+map.obstacles.length+' collision volumes; 6 hollow multi-storey buildings, 2 hangars, 2 skybridges.');
