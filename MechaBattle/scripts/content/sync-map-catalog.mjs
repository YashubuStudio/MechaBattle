import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const project=path.resolve(fileURLToPath(new URL('../../',import.meta.url)));
const source=path.join(project,'config');
const output=path.join(project,'Assets/Game/Resources/MechaBattleMaps');
const catalog=JSON.parse(await fs.readFile(path.join(source,'map-catalog.json'),'utf8'));
await fs.mkdir(output,{recursive:true});
for(const entry of catalog.entries){
  if(!/^[a-z0-9-]+\.json$/.test(entry.sourceFile)||!/^MechaBattleMaps\/[A-Za-z0-9]+$/.test(entry.resourcePath))throw Error('Invalid source/resource path');
  const data=await fs.readFile(path.join(source,entry.sourceFile),'utf8');
  if(JSON.parse(data).id!==entry.id)throw Error('Map ID mismatch');
  await fs.writeFile(path.join(output,path.basename(entry.resourcePath)+'.json'),data);
}
await fs.writeFile(path.join(output,'MapCatalog.json'),JSON.stringify(catalog,null,2)+'\n');
console.log('Synchronized '+catalog.entries.length+' selectable maps.');
