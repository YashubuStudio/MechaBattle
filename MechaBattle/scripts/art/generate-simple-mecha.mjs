import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// Original geometric study. No downloaded meshes, textures, or third-party IP.
const workspace = path.resolve(fileURLToPath(new URL('../../../', import.meta.url)));
const assetDir = path.join(workspace, 'MechaBattle/Assets/Game/Content/Models/MB_Worker01');
const sourceDir = path.join(workspace, 'ArtSource/MB_Worker01');
await fs.mkdir(assetDir, { recursive: true });
await fs.mkdir(sourceDir, { recursive: true });
const materials = [
  { name: 'TeamArmor', color: [0.59, 0.65, 0.70], metallic: 0.25, roughness: 0.62 },
  { name: 'Frame', color: [0.085, 0.115, 0.15], metallic: 0.45, roughness: 0.64 },
  { name: 'Signal', color: [0.25, 0.95, 0.80], metallic: 0, roughness: 0.35 }
];
const vertices = [], normals = [], groups = materials.map(m => ({ material: m.name, triangles: [] })), parts = [];
const sub = (a,b) => a.map((v,i)=>v-b[i]);
const cross = (a,b) => [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]];
const dot = (a,b) => a.reduce((s,v,i)=>s+v*b[i],0);
const unit = v => {const len=Math.hypot(...v);return v.map(x=>x/len);};
const round = v => Math.abs(v)<1e-8 ? 0 : +v.toFixed(7);
function rotate(v, euler) {
  let [x,y,z]=v;
  for (let axis=0;axis<3;axis++) {
    const a=(euler[axis]||0)*Math.PI/180,c=Math.cos(a),s=Math.sin(a);
    if(axis===0)[y,z]=[y*c-z*s,y*s+z*c];
    else if(axis===1)[x,z]=[x*c+z*s,-x*s+z*c];
    else [x,y]=[x*c-y*s,x*s+y*c];
  }
  return [x,y,z];
}
function face(points, center, rotation, material) {
  let normal=unit(cross(sub(points[1],points[0]),sub(points[2],points[0])));
  const middle=points[0].map((_,i)=>points.reduce((s,p)=>s+p[i],0)/points.length);
  if(dot(normal,middle)<0){points=[...points].reverse();normal=normal.map(v=>-v);}
  normal=rotate(normal,rotation);
  const start=vertices.length;
  for(const p of points){const v=rotate(p,rotation).map((x,i)=>round(x+center[i]));vertices.push(v);normals.push(normal.map(round));}
  for(let i=1;i<points.length-1;i++)groups[material].triangles.push(start,start+i,start+i+1);
}
function box(name, center, size, material, bevel=0, rotation=[0,0,0]) {
  const first=vertices.length, h=size.map(v=>v/2), b=Math.min(bevel,...h.map(v=>v*0.5));
  if(b===0) {
    for(let axis=0;axis<3;axis++)for(const sign of [-1,1]) {
      const others=[0,1,2].filter(i=>i!==axis);
      const points=[[-1,-1],[1,-1],[1,1],[-1,1]].map(ss=>{const p=[0,0,0];p[axis]=sign*h[axis];for(let i=0;i<2;i++)p[others[i]]=ss[i]*h[others[i]];return p;});
      face(points,center,rotation,material);
    }
  } else {
    const corner=(signs,longAxis)=>h.map((v,i)=>signs[i]*(v-(i===longAxis?0:b)));
    for(let axis=0;axis<3;axis++)for(const sign of [-1,1]) {
      const others=[0,1,2].filter(i=>i!==axis);
      const points=[[-1,-1],[1,-1],[1,1],[-1,1]].map(ss=>{const signs=[1,1,1];signs[axis]=sign;ss.forEach((s,i)=>signs[others[i]]=s);return corner(signs,axis);});
      face(points,center,rotation,material);
    }
    for(let a=0;a<3;a++)for(let bAxis=a+1;bAxis<3;bAxis++)for(const sa of [-1,1])for(const sb of [-1,1]) {
      const rest=3-a-bAxis;
      const signs=[1,1,1];signs[a]=sa;signs[bAxis]=sb;
      const points=[[-1,a],[-1,bAxis],[1,bAxis],[1,a]].map(([sr,longAxis])=>{const ss=[...signs];ss[rest]=sr;return corner(ss,longAxis);});
      face(points,center,rotation,material);
    }
    for(const sx of [-1,1])for(const sy of [-1,1])for(const sz of [-1,1])face([0,1,2].map(a=>corner([sx,sy,sz],a)),center,rotation,material);
  }
  parts.push({name,type:'box',center,size,bevel:b,rotation,material:materials[material].name,firstVertex:first,vertexCount:vertices.length-first});
}
function cylinderX(name, center, radius, length, material, segments=8) {
  const first=vertices.length;
  const circle=sign=>Array.from({length:segments},(_,i)=>{const a=i*2*Math.PI/segments;return [sign*length/2,Math.cos(a)*radius,Math.sin(a)*radius];});
  const left=circle(-1),right=circle(1);
  face(left,center,[0,0,0],material);face(right,center,[0,0,0],material);
  for(let i=0;i<segments;i++)face([left[i],right[i],right[(i+1)%segments],left[(i+1)%segments]],center,[0,0,0],material);
  parts.push({name,type:'cylinderX',center,radius,length,material:materials[material].name,firstVertex:first,vertexCount:vertices.length-first});
}

for(const side of [-1,1]) {
  const tag=side<0?'L':'R', x=side*0.27;
  box('Foot_'+tag,[x,0.10,0.105],[0.40,0.20,0.62],1,0.04,[0,side*5,0]);
  box('FootArmor_'+tag,[x,0.205,0.105],[0.34,0.13,0.46],0,0.035,[0,side*5,0]);
  box('Ankle_'+tag,[x,0.255,-0.025],[0.21,0.16,0.22],1,0.025);
  box('Shin_'+tag,[x,0.45,-0.015],[0.34,0.40,0.34],0,0.055);
  box('ShinVent_'+tag,[x,0.43,0.168],[0.18,0.065,0.022],1);
  cylinderX('Knee_'+tag,[x,0.70,0.005],0.125,0.35,1);
  box('Thigh_'+tag,[side*0.24,0.91,-0.025],[0.30,0.34,0.33],0,0.045);
}
cylinderX('HipAxle',[0,1.105,-0.02],0.15,0.69,1);
box('Pelvis',[0,1.19,-0.015],[0.71,0.27,0.43],0,0.065);
box('Abdomen',[0,1.40,-0.035],[0.43,0.22,0.31],1,0.035);
box('Torso',[0,1.73,-0.02],[0.88,0.59,0.51],0,0.095);
box('ChestPlate',[0,1.78,0.26],[0.66,0.29,0.09],0,0.038,[8,0,0]);
box('ChestCore',[0,1.56,0.25],[0.26,0.12,0.07],1,0.015);
box('ChestSignal',[0,1.57,0.29],[0.16,0.035,0.016],2);
for(const x of [-0.25,0.25])box('ChestVent_'+x,[x,1.62,0.257],[0.13,0.045,0.03],1);
for(const side of [-1,1]) {
  const tag=side<0?'L':'R', x=side*0.65;
  cylinderX('ShoulderJoint_'+tag,[side*0.49,1.91,-0.02],0.145,0.21,1);
  box('ShoulderArmor_'+tag,[x,1.99,-0.025],[0.43,0.29,0.49],0,0.06);
  box('UpperArm_'+tag,[x,1.71,-0.025],[0.23,0.32,0.27],1,0.035);
  cylinderX('Elbow_'+tag,[x,1.49,0.005],0.12,0.28,1);
  box('Forearm_'+tag,[x,1.28,0.025],[0.33,0.36,0.36],0,0.05);
  box('Hand_'+tag,[x,1.025,0.04],[0.245,0.19,0.25],1,0.028);
}
box('Neck',[0,2.105,-0.025],[0.22,0.18,0.22],1,0.025);
box('SensorHead',[0,2.29,-0.015],[0.46,0.27,0.37],1,0.055);
box('HeadArmor',[0,2.405,-0.02],[0.51,0.13,0.42],0,0.04);
box('SensorVisor',[0,2.30,0.187],[0.33,0.065,0.033],2,0.008);
box('Backpack',[0,1.76,-0.40],[0.60,0.48,0.24],1,0.045);
for(const side of [-1,1])box('CoolingStack_'+side,[side*0.18,1.75,-0.565],[0.16,0.30,0.10],0,0.018);
box('ToolHousing',[0.65,1.27,0.32],[0.22,0.19,0.39],1,0.025);
box('ToolMuzzle',[0.65,1.27,0.53],[0.15,0.12,0.08],1,0.018);
box('ToolSignal',[0.65,1.38,0.37],[0.10,0.022,0.14],2);

// Preserve the original build, then add compatible replacement modules.
const baseParts=[...parts];
for(const side of [-1,1]) {
  const tag=side<0?'L':'R',x=side*0.65;
  box('ReinforcedShoulder_'+tag,[x,2.15,-0.025],[0.43,0.10,0.46],0,0.025);
  box('ReinforcedForearm_'+tag,[x,1.28,0.255],[0.42,0.40,0.12],0,0.035);
  box('ReinforcedSignal_'+tag,[x,2.21,0.08],[0.22,0.025,0.10],2,0.005);
}
box('RifleBody',[0.65,1.27,0.39],[0.24,0.22,0.54],1,0.03);
box('RifleBarrel',[0.65,1.27,0.79],[0.12,0.12,0.32],1,0.015);
box('RifleMuzzle',[0.65,1.27,1.005],[0.18,0.16,0.11],0,0.02);
box('RifleSight',[0.65,1.43,0.45],[0.10,0.08,0.21],2,0.008);
const config=JSON.parse(await fs.readFile(path.join(workspace,'MechaBattle/config/prototype-defaults.json'),'utf8')).constructionPrototype;
const standard=config.visual.connectionStandard;
const xyz=v=>[v.x,v.y,v.z];const vector=v=>({x:round(v[0]),y:round(v[1]),z:round(v[2])});
const left=xyz(config.visual.leftShoulder),right=xyz(config.visual.rightShoulder),hold=xyz(config.visual.primaryWeaponOnRightArm);
const weaponOrigin=right.map((v,i)=>round(v+hold[i]));
const armNames=tag=>['ShoulderJoint_','ShoulderArmor_','UpperArm_','Elbow_','Forearm_','Hand_'].map(n=>n+tag);
const armPartNames=new Set([...armNames('L'),...armNames('R')]);
const toolNames=['ToolHousing','ToolMuzzle','ToolSignal'];
function extract(name,selection,origin=[0,0,0]) {
  const index=new Map(),vv=[],nn=[],solids=[];
  for(const part of selection) { solids.push({name:part.name,firstVertex:vv.length,vertexCount:part.vertexCount}); for(let i=part.firstVertex;i<part.firstVertex+part.vertexCount;i++) {
    index.set(i,vv.length);vv.push(vector(sub(vertices[i],origin)));nn.push(vector(normals[i]));
  }}
  return {name,vertices:vv,normals:nn,solids,subMeshes:groups.map(g=>({material:g.material,triangles:g.triangles.filter(i=>index.has(i)).map(i=>index.get(i))}))};
}
function module(id,selection,origin,parentSocket,sockets=[]) {
  return {partId:id,connectionStandard:standard,parentSocket,worldOrigin:vector(origin),sockets,mesh:extract(id,selection,origin)};
}
const find=names=>parts.filter(p=>names.includes(p.name));
const modules=[module('frame.standard.v1',baseParts.filter(p=>!armPartNames.has(p.name)&&!toolNames.includes(p.name)),[0,0,0],'',[
  {id:'arm.left',position:vector(left)},{id:'arm.right',position:vector(right)}])];
for(const side of ['left','right'])for(const type of ['standard','reinforced']) {
  const tag=side==='left'?'L':'R';const names=armNames(tag);
  if(type==='reinforced')names.push('ReinforcedShoulder_'+tag,'ReinforcedForearm_'+tag,'ReinforcedSignal_'+tag);
  modules.push(module('arm.'+type+'.'+side,find(names),side==='left'?left:right,'arm.'+side,
    side==='right'?[{id:'weapon.primary',position:vector(hold)}]:[]));
}
modules.push(module('weapon.carbine',find(toolNames),weaponOrigin,'weapon.primary',[{id:'muzzle',position:{x:0,y:0,z:0.25}}]));
modules.push(module('weapon.longrifle',find(['RifleBody','RifleBarrel','RifleMuzzle','RifleSight']),weaponOrigin,'weapon.primary',[{id:'muzzle',position:{x:0,y:0,z:0.74}}]));
const presets=config.catalog.presets;
function assembled(preset) {
  const chosen=[modules.find(m=>m.partId===preset.frameId),...preset.equipment.map(e=>modules.find(m=>m.partId===e.partId))];
  const mesh={name:preset.id,vertices:[],normals:[],subMeshes:materials.map(m=>({material:m.name,triangles:[]}))};
  for(const m of chosen) {
    const offset=mesh.vertices.length,origin=xyz(m.worldOrigin);
    mesh.vertices.push(...m.mesh.vertices.map(v=>vector(xyz(v).map((x,i)=>x+origin[i]))));
    mesh.normals.push(...m.mesh.normals);
    m.mesh.subMeshes.forEach((g,i)=>mesh.subMeshes[i].triangles.push(...g.triangles.map(x=>x+offset)));
  }
  return mesh;
}
const assemblyMeshes=presets.map(assembled);
const mesh=assemblyMeshes[0];
await fs.writeFile(path.join(assetDir,'MB_Worker01.mesh.json'),JSON.stringify(mesh));
await fs.writeFile(path.join(assetDir,'MB_Worker01_reinforced.mesh.json'),JSON.stringify(assemblyMeshes[1]));
const kit={schemaVersion:1,connectionStandard:standard,coordinateSystem:'Y-up, +Z forward, meters; foot origin',modules,presets};
await fs.writeFile(path.join(assetDir,'module-kit.json'),JSON.stringify(kit));
// OBJ keeps named module groups, so the source is editable rather than a fixed character.
let obj='# Original MechaBattle modular frame. Y-up, +Z forward, meters.\nmtllib MB_Worker01.mtl\n';
for(const v of mesh.vertices)obj+='v '+xyz(v).join(' ')+'\n';
for(const n of mesh.normals)obj+='vn '+xyz(n).join(' ')+'\n';
let start=0;
for(const id of [presets[0].frameId,...presets[0].equipment.map(e=>e.partId)]) {
  const m=modules.find(m=>m.partId===id);obj+='o '+id+'\n';
  for(const g of m.mesh.subMeshes){obj+='g '+id+'.'+g.material+'\nusemtl '+g.material+'\n';for(let i=0;i<g.triangles.length;i+=3)obj+='f '+g.triangles.slice(i,i+3).map(v=>(v+start+1)+'//'+(v+start+1)).join(' ')+'\n';}
  start+=m.mesh.vertices.length;
}
await fs.writeFile(path.join(assetDir,'MB_Worker01.obj'),obj.replaceAll('\\n','\n'));
await fs.writeFile(path.join(assetDir,'MB_Worker01.mtl'),materials.map(m=>'newmtl '+m.name+'\nKd '+m.color.join(' ')+'\nKa 0.1 0.1 0.1\nKs 0.15 0.15 0.15\nNs 24\nd 1\nillum 2\n').join('\n').replaceAll('\\n','\n'));
const vv=mesh.vertices.map(xyz),min=[0,1,2].map(a=>Math.min(...vv.map(v=>v[a]))),max=[0,1,2].map(a=>Math.max(...vv.map(v=>v[a])));
const spec={name:'MB-01 MODULAR FRAME',originalAsset:true,prototypeOnly:true,rigged:false,animated:false,textures:false,parts:baseParts,materials,bounds:{min,max},heightMeters:round(max[1]-min[1]),vertexCount:mesh.vertices.length,triangleCount:mesh.subMeshes.reduce((s,g)=>s+g.triangles.length/3,0),materialCount:3,modules:modules.map(m=>({partId:m.partId,vertices:m.mesh.vertices.length,triangles:m.mesh.subMeshes.reduce((s,g)=>s+g.triangles.length/3,0)})),presets:assemblyMeshes.map(m=>({id:m.name,triangles:m.subMeshes.reduce((s,g)=>s+g.triangles.length/3,0)})),notes:'One frame, two arm types, two weapons. Presets are ordinary builds. Static geometry; no rig/LOD/collision. Editor assembly combines material groups. Gameplay scale remains unverified.'};
await fs.writeFile(path.join(assetDir,'model-spec.json'),JSON.stringify(spec,null,2)+'\n');
// GLB stores each module with its own local origin and two selectable assembly scenes.
const binaryParts=[],bufferViews=[],accessors=[],meshes=[];let offset=0;
function accessor(buffer,componentType,type,count,target,min,max) {
  const padded=Buffer.alloc(Math.ceil(buffer.length/4)*4);buffer.copy(padded);binaryParts.push(padded);
  bufferViews.push({buffer:0,byteOffset:offset,byteLength:buffer.length,target});offset+=padded.length;
  accessors.push({bufferView:bufferViews.length-1,componentType,count,type,...(min?{min,max}:{})});return accessors.length-1;
}
for(const m of modules) {
  const floats=new Float32Array(m.mesh.vertices.flatMap(xyz));
  const min=[0,1,2].map(a=>Math.min(...Array.from({length:m.mesh.vertices.length},(_,i)=>floats[i*3+a])));
  const max=[0,1,2].map(a=>Math.max(...Array.from({length:m.mesh.vertices.length},(_,i)=>floats[i*3+a])));
  const position=accessor(Buffer.from(floats.buffer),5126,'VEC3',m.mesh.vertices.length,34962,min,max);
  const normal=accessor(Buffer.from(new Float32Array(m.mesh.normals.flatMap(xyz)).buffer),5126,'VEC3',m.mesh.normals.length,34962);
  const primitives=[];
  m.mesh.subMeshes.forEach((g,i)=>{if(g.triangles.length)primitives.push({attributes:{POSITION:position,NORMAL:normal},indices:accessor(Buffer.from(new Uint16Array(g.triangles).buffer),5123,'SCALAR',g.triangles.length,34963),material:i});});
  meshes.push({name:m.partId,primitives});
}
const nodes=[],scenes=[];
for(const preset of presets) {
  const frame=nodes.length;nodes.push({name:preset.id,mesh:0,children:[]});
  for(const e of preset.equipment.filter(e=>e.slot!==2)) {
    const m=modules.find(m=>m.partId===e.partId),index=nodes.length;
    nodes.push({name:m.partId,mesh:modules.indexOf(m),translation:xyz(m.worldOrigin)});nodes[frame].children.push(index);
    if(e.slot===1) {
      const wm=modules.find(m=>m.partId===preset.equipment.find(e=>e.slot===2).partId);
      nodes[index].children=[nodes.length];nodes.push({name:wm.partId,mesh:modules.indexOf(wm),translation:hold});
    }
  }
  scenes.push({name:preset.id,nodes:[frame]});
}
const bin=Buffer.concat(binaryParts);
const gltf={asset:{version:'2.0',generator:'MechaBattle original modular prototype'},scene:0,scenes,nodes,meshes,materials:materials.map((m,i)=>({name:m.name,pbrMetallicRoughness:{baseColorFactor:[...m.color,1],metallicFactor:m.metallic,roughnessFactor:m.roughness},...(i===2?{emissiveFactor:m.color.map(v=>v*0.35)}:{})})),buffers:[{byteLength:bin.length}],bufferViews,accessors};
const rawJson=Buffer.from(JSON.stringify(gltf)),json=Buffer.alloc(Math.ceil(rawJson.length/4)*4,32);rawJson.copy(json);
const header=Buffer.alloc(12);header.writeUInt32LE(0x46546c67,0);header.writeUInt32LE(2,4);header.writeUInt32LE(12+8+json.length+8+bin.length,8);
const jHeader=Buffer.alloc(8);jHeader.writeUInt32LE(json.length,0);jHeader.writeUInt32LE(0x4e4f534a,4);const bHeader=Buffer.alloc(8);bHeader.writeUInt32LE(bin.length,0);bHeader.writeUInt32LE(0x004e4942,4);
await fs.writeFile(path.join(sourceDir,'MB_Worker01.glb'),Buffer.concat([header,jHeader,json,bHeader,bin]));
console.log(JSON.stringify({modules:modules.length,presets:spec.presets,heightMeters:spec.heightMeters,materialSlots:3}));
