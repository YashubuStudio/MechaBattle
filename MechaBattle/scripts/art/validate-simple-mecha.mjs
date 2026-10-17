import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const workspace=path.resolve(fileURLToPath(new URL('../../../',import.meta.url)));
const assets=path.join(workspace,'MechaBattle/Assets/Game/Content/Models/MB_Worker01');
const kit=JSON.parse(await fs.readFile(path.join(assets,'module-kit.json'),'utf8'));
const config=JSON.parse(await fs.readFile(path.join(workspace,'MechaBattle/config/prototype-defaults.json'),'utf8')).constructionPrototype;
const xyz=v=>[v.x,v.y,v.z],sub=(a,b)=>a.map((v,i)=>v-b[i]);
const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0);
function check(condition,message){if(!condition)throw Error(message);}
let triangleCount=0,solidCount=0;
const expectedIds=[...config.catalog.frames,...config.catalog.parts].map(p=>p.id).sort();
check(JSON.stringify(kit.modules.map(m=>m.partId).sort())===JSON.stringify(expectedIds),'Kit/catalog IDs differ');
for(const module of kit.modules) {
  check(module.connectionStandard===kit.connectionStandard,'Module standard mismatch');
  const m=module.mesh,v=m.vertices.map(xyz),n=m.normals.map(xyz);
  check(v.length===n.length && v.length>0,'Missing vertices/normals');
  check(v.flat().every(Number.isFinite) && n.flat().every(Number.isFinite),'Non-finite coordinates');
  check(n.every(p=>Math.abs(Math.hypot(...p)-1)<1e-5),'Non-unit normals');
  for(const group of m.subMeshes) {
    check(group.triangles.length%3===0,'Incomplete triangles');
    check(group.triangles.every(i=>Number.isInteger(i)&&i>=0&&i<v.length),'Index out of bounds');
    for(let i=0;i<group.triangles.length;i+=3) {
      const [a,b,c]=group.triangles.slice(i,i+3);
      const areaNormal=cross(sub(v[b],v[a]),sub(v[c],v[a]));
      check(Math.hypot(...areaNormal)>1e-9,'Degenerate face');
      check(dot(areaNormal,n[a])>0,'Face orientation/normal mismatch');triangleCount++;
    }
  }
  for(const solid of m.solids) {
    const edges=new Map();let volume=0,faces=0;
    const key=p=>p.map(x=>Math.round(x*1e6)).join(',');
    for(const group of m.subMeshes)for(let i=0;i<group.triangles.length;i+=3) {
      const ids=group.triangles.slice(i,i+3);
      if(ids[0]<solid.firstVertex||ids[0]>=solid.firstVertex+solid.vertexCount)continue;
      check(ids.every(j=>j>=solid.firstVertex&&j<solid.firstVertex+solid.vertexCount),'Cross-solid triangle');
      volume+=dot(v[ids[0]],cross(v[ids[1]],v[ids[2]]))/6;faces++;
      for(let j=0;j<3;j++){
        const a=key(v[ids[j]]),b=key(v[ids[(j+1)%3]]),k=[a,b].sort().join('|');
        const edge=edges.get(k)||{count:0,balance:0};edge.count++;edge.balance+=a<b?1:-1;edges.set(k,edge);
      }
    }
    check(faces>0&&volume>1e-9,'Invalid closed-solid volume: '+solid.name);
    check([...edges.values()].every(e=>e.count===2&&e.balance===0),'Open/non-manifold solid: '+solid.name);solidCount++;
  }
}
for(const preset of kit.presets) {
  const sockets=new Map();
  for(const id of [preset.frameId,...preset.equipment.filter(e=>e.slot<=2).sort((a,b)=>a.slot-b.slot).map(e=>e.partId)]){
    const module=kit.modules.find(m=>m.partId===id);
    const origin=module.parentSocket?sockets.get(module.parentSocket):[0,0,0];
    check(origin,'Missing assembly socket');
    check(origin.every((x,i)=>Math.abs(x-xyz(module.worldOrigin)[i])<1e-6),'Socket/world coordinate drift: '+id);
    for(const s of module.sockets){check(!sockets.has(s.id),'Socket duplicate');sockets.set(s.id,xyz(s.position).map((x,i)=>x+origin[i]));}
  }
}
const glb=await fs.readFile(path.join(workspace,'ArtSource/MB_Worker01/MB_Worker01.glb'));
check(glb.readUInt32LE(0)===0x46546c67&&glb.readUInt32LE(4)===2&&glb.readUInt32LE(8)===glb.length,'Invalid GLB header');
const jsonLength=glb.readUInt32LE(12);check(glb.readUInt32LE(16)===0x4e4f534a,'Missing JSON chunk');
const gltf=JSON.parse(glb.subarray(20,20+jsonLength).toString('utf8'));
const binHeader=20+jsonLength,binStart=binHeader+8;
check(glb.readUInt32LE(binHeader+4)===0x004e4942&&binStart+glb.readUInt32LE(binHeader)===glb.length,'Invalid BIN chunk');
check(gltf.meshes.length===kit.modules.length&&gltf.scenes.length===config.catalog.presets.length,'Missing modular GLB assemblies');
for(const preset of kit.presets){
  const scene=gltf.scenes.find(s=>s.name===preset.id),rootNode=gltf.nodes[scene.nodes[0]];
  check(gltf.meshes[rootNode.mesh].name===preset.frameId,'GLB frame mismatch: '+preset.id);
  const visible=[];function visit(index){const n=gltf.nodes[index];if(n.mesh!==undefined)visible.push(gltf.meshes[n.mesh].name);for(const child of n.children??[])visit(child);}visit(scene.nodes[0]);
  const expected=[preset.frameId,...preset.equipment.filter(e=>e.slot<=2).map(e=>e.partId)].sort();
  check(JSON.stringify(visible.sort())===JSON.stringify(expected),'GLB must show the selected weapon only: '+preset.id);
}
for(const view of gltf.bufferViews)check(view.byteOffset%4===0&&view.byteOffset+view.byteLength<=gltf.buffers[0].byteLength,'Invalid buffer view');
for(const a of gltf.accessors) {
  const view=gltf.bufferViews[a.bufferView],width=a.type==='VEC3'?3:1,bytes=a.componentType===5126?4:2;
  check(a.count*width*bytes<=view.byteLength,'Accessor out of bounds');
  if(a.min)for(let axis=0;axis<3;axis++){
    const values=Array.from({length:a.count},(_,i)=>glb.readFloatLE(binStart+view.byteOffset+(i*3+axis)*4));
    check(Math.min(...values)===a.min[axis]&&Math.max(...values)===a.max[axis],'Incorrect Float32 bounds');
  }
}
for(const mesh of gltf.meshes)for(const p of mesh.primitives){
  const a=gltf.accessors[p.indices],v=gltf.bufferViews[a.bufferView],count=gltf.accessors[p.attributes.POSITION].count;
  for(let i=0;i<a.count;i++)check(glb.readUInt16LE(binStart+v.byteOffset+i*2)<count,'GLB index out of bounds');
}
const obj=await fs.readFile(path.join(assets,'MB_Worker01.obj'),'utf8');
check(obj.split('\n').filter(l=>l.startsWith('o ')).length===4,'OBJ lost module grouping');
check(obj.split('\n').filter(l=>l.startsWith('f ')).length===1676,'OBJ face count mismatch');
const report={scope:'source-geometry-and-glb-not-unity-rendering',modules:kit.modules.length,presets:kit.presets.length,closedSolidInstances:solidCount,validatedTriangles:triangleCount,passed:true};
const output=path.join(workspace,'MechaBattle/Build/Reports/simple-mecha.json');
await fs.mkdir(path.dirname(output),{recursive:true});await fs.writeFile(output,JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
