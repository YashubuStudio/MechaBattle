using System;
using System.Collections.Generic;
using MechaBattle.Contracts;
using MechaBattle.Contracts.Construction;
using MechaBattle.Construction;
using UnityEngine;
using UnityEngine.Rendering;

namespace MechaBattle.Playable
{
    public sealed class MechaMeshLibrary : IDisposable
    {
        [Serializable] private sealed class Group { public string material = ""; public int[] triangles = Array.Empty<int>(); }
        [Serializable] private sealed class Solid { public string name = ""; public int firstVertex, vertexCount; }
        [Serializable] private sealed class Source
        {
            public Vector3[] vertices = Array.Empty<Vector3>(), normals = Array.Empty<Vector3>();
            public Group[] subMeshes = Array.Empty<Group>();
            public Solid[] solids = Array.Empty<Solid>();
        }
        [Serializable] private sealed class Socket { public string id = ""; public Vector3 position = Vector3.zero; }
        [Serializable] private sealed class Module
        {
            public string partId = "", parentSocket = "", connectionStandard = "";
            public Socket[] sockets = Array.Empty<Socket>(); public Source mesh = new Source();
        }
        [Serializable] private sealed class Kit
        {
            public int schemaVersion = 0; public string connectionStandard = ""; public Module[] modules = Array.Empty<Module>();
        }
        private readonly Kit kit;
        private readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);
        private readonly Dictionary<string, Module> modules = new Dictionary<string, Module>(StringComparer.Ordinal);
        private readonly Material[][] palettes;
        public MechaMeshLibrary(string json, Shader shader)
        {
            if (shader == null) throw new ArgumentException("Missing URP model shader.");
            kit = JsonUtility.FromJson<Kit>(json);
            if (kit == null || kit.schemaVersion != 1 || kit.modules == null) throw new ArgumentException("Invalid modular kit.");
            foreach (var module in kit.modules)
            {
                if (module == null || module.connectionStandard != kit.connectionStandard || string.IsNullOrEmpty(module.partId) || modules.ContainsKey(module.partId))
                    throw new ArgumentException("Invalid module identity/standard.");
                Validate(module.mesh); modules.Add(module.partId, module);
            }
            var frame = Material(shader, "SharedFrame", new Color(0.085f, 0.115f, 0.15f), 0.45f);
            var choices = new List<Material[]>();
            var teamSignals = new Material[3];
            for(int team=0;team<3;team++)
            {
                BuildAppearance.TeamTint(team,out float r,out float g,out float b);
                var color=new Color(r,g,b);
                // Broad identification panels share this existing material slot with sensors.
                // Low emission keeps the color legible without washing it towards white.
                var signal=Material(shader,"TeamMarks_"+team,color);
                signal.EnableKeyword("_EMISSION");signal.SetColor("_EmissionColor",color*.2f);
                teamSignals[team]=signal;
                choices.Add(new[]{Material(shader,"TeamArmor_"+team,color,.1f),frame,signal});
            }
            for(int paint=1;paint<BuildAppearance.PaintCount;paint++)
            {
                BuildAppearance.Tint(paint,out float r,out float g,out float b);
                var armor=Material(shader,BuildAppearance.PaintId(paint)+"Armor",new Color(r,g,b),.1f);
                for(int team=0;team<3;team++)choices.Add(new[]{armor,frame,teamSignals[team]});
            }
            palettes=choices.ToArray();
        }
        public GameObject Create(ResolvedBuild build, int palette, Transform parent, bool articulated = false, int weaponIndex = 0, bool allowSpecialPaint = true)
        {
            if (palette < 0 || palette > 2 || build.ConnectionStandard != kit.connectionStandard) throw new ArgumentException("Invalid palette or connection standard.");
            int paint=allowSpecialPaint?BuildAppearance.PaintIndex(build.PaintId):0;if(paint>0)palette=3+(paint-1)*3+palette;
            if (articulated) return CreateArticulated(build, palette, parent, weaponIndex);
            var key = new System.Text.StringBuilder().Append(build.FrameId.Length).Append(':').Append(build.FrameId);
            var ids = new List<string> { build.FrameId };
            foreach (var equipment in build.Equipment) { if (equipment.Slot >= PartSlot.PrimaryWeapon && equipment.PartId != build.WeaponPartId(weaponIndex)) continue;
                if (equipment.Slot > PartSlot.PrimaryWeapon && ids.Contains(equipment.PartId)) continue; key.Append('|').Append(equipment.PartId.Length).Append(':').Append(equipment.PartId); ids.Add(equipment.PartId); }
            if (!meshes.TryGetValue(key.ToString(), out var mesh))
            {
                var vertices = new List<Vector3>(); var normals = new List<Vector3>();
                var indices = new[] { new List<int>(), new List<int>(), new List<int>() };
                var sockets = new Dictionary<string, Vector3>(StringComparer.Ordinal);
                for (int order = 0; order < ids.Count; order++)
                {
                    if (!modules.TryGetValue(ids[order], out var module)) throw new ArgumentException("Visual part missing: " + ids[order]);
                    Vector3 origin = Vector3.zero;
                    if (order == 0 && !string.IsNullOrEmpty(module.parentSocket)) throw new ArgumentException("Frame must be the assembly root.");
                    if (order > 0 && (string.IsNullOrEmpty(module.parentSocket) || !sockets.TryGetValue(module.parentSocket, out origin)))
                        throw new ArgumentException("Missing visual socket: " + module.parentSocket);
                    int offset = vertices.Count;
                    for (int i = 0; i < module.mesh.vertices.Length; i++) { vertices.Add(module.mesh.vertices[i] + origin); normals.Add(module.mesh.normals[i]); }
                    for (int material = 0; material < 3; material++) foreach (int index in module.mesh.subMeshes[material].triangles) indices[material].Add(offset + index);
                    foreach (var socket in module.sockets)
                    {
                        if (!Finite(socket.position) || sockets.ContainsKey(socket.id)) throw new ArgumentException("Invalid/duplicate visual socket.");
                        sockets.Add(socket.id, origin + socket.position);
                    }
                }
                mesh = new Mesh { name = "Assembled_" + build.Id, indexFormat = vertices.Count < 65536 ? IndexFormat.UInt16 : IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.subMeshCount = 3;
                for (int i = 0; i < 3; i++) mesh.SetTriangles(indices[i], i, false);
                mesh.RecalculateBounds(); meshes.Add(key.ToString(), mesh);
            }
            var root = new GameObject("Mecha_" + build.Id, typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(parent, false); root.GetComponent<MeshFilter>().sharedMesh = mesh;
            root.GetComponent<MeshRenderer>().sharedMaterials = palettes[palette];
            return root;
        }
        public GameObject CreateDeployable(WeaponKind kind,int palette,Transform parent)
        {
            string id=kind==WeaponKind.Radar?"weapon.radar":kind==WeaponKind.Mine?"weapon.mine":"weapon.repairpod";
            if(!modules.TryGetValue(id,out var module))throw new ArgumentException("Visual part missing: "+id);
            string key="deployed:"+id;
            if(!meshes.TryGetValue(key,out var mesh))
            {
                mesh=new Mesh{name=key};mesh.vertices=module.mesh.vertices;mesh.normals=module.mesh.normals;mesh.subMeshCount=3;
                for(int m=0;m<3;m++)mesh.SetTriangles(module.mesh.subMeshes[m].triangles,m,false);mesh.RecalculateBounds();meshes.Add(key,mesh);
            }
            var go=new GameObject("DeployedEquipment",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=palettes[palette];
            var child=go.transform;child.localPosition=Vector3.zero;return go;
        }
        private GameObject CreateArticulated(ResolvedBuild build, int palette, Transform parent, int weaponIndex)
        {
            var root = new GameObject("Mecha_" + build.Id); root.transform.SetParent(parent, false);
            var socketPositions = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            var ids = new List<string> { build.FrameId }; foreach (var e in build.Equipment) if (e.Slot < PartSlot.PrimaryWeapon || e.PartId == build.WeaponPartId(weaponIndex) && !ids.Contains(e.PartId)) ids.Add(e.PartId);
            Transform rightArm = null;
            foreach (string id in ids)
            {
                if (!modules.TryGetValue(id, out var module)) throw new ArgumentException("Visual part missing: " + id);
                Vector3 origin = string.IsNullOrEmpty(module.parentSocket) ? Vector3.zero : socketPositions[module.parentSocket];
                if (!id.StartsWith("arm.", StringComparison.Ordinal) && !id.StartsWith("weapon.", StringComparison.Ordinal) && module.mesh.solids.Length > 0)
                {
                    CreateLeggedFrame(root.transform, module, palette);
                    foreach (var socket in module.sockets) socketPositions.Add(socket.id, origin + socket.position);
                    continue;
                }
                string meshKey = "module:" + id;
                if (!meshes.TryGetValue(meshKey, out var mesh))
                {
                    mesh = new Mesh { name = id }; mesh.vertices = module.mesh.vertices; mesh.normals = module.mesh.normals; mesh.subMeshCount = 3;
                    for (int m = 0; m < 3; m++) mesh.SetTriangles(module.mesh.subMeshes[m].triangles, m, false);
                    mesh.RecalculateBounds(); meshes.Add(meshKey, mesh);
                }
                bool arm = id.StartsWith("arm.", StringComparison.Ordinal), weapon = id.StartsWith("weapon.", StringComparison.Ordinal);
                var go = new GameObject(weapon ? "PrimaryWeapon" : arm ? id.EndsWith(".right", StringComparison.Ordinal) ? "RightArm" : "LeftArm" : "Frame", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(weapon && rightArm != null ? rightArm : root.transform, false);
                go.transform.localPosition = weapon && rightArm != null ? origin - socketPositions["arm.right"] : origin;
                go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterials = palettes[palette];
                if (go.name == "RightArm") rightArm = go.transform;
                foreach (var socket in module.sockets)
                {
                    socketPositions.Add(socket.id, origin + socket.position);
                    if (socket.id == "muzzle") { var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(go.transform, false); muzzle.localPosition = socket.position; }
                }
            }
            return root;
        }
        private void CreateLeggedFrame(Transform root, Module module, int palette)
        {
            var groupByVertex = new int[module.mesh.vertices.Length];
            foreach (var solid in module.mesh.solids)
            {
                int side = solid.name.EndsWith("_L", StringComparison.Ordinal) ? 1 : solid.name.EndsWith("_R", StringComparison.Ordinal) ? 3 : 0;
                int group = side == 0 ? 0 : solid.name.IndexOf("Thigh", StringComparison.Ordinal)>=0 ? side : side+1;
                for (int v=solid.firstVertex;v<solid.firstVertex+solid.vertexCount;v++) groupByVertex[v]=group;
            }
            AddPiece("Frame", root, Vector3.zero, Vector3.zero, 0);
            for (int side=0;side<2;side++)
            {
                float hipX=0.27f;
                foreach(var solid in module.mesh.solids)if(solid.name.EndsWith(side==0?"_L":"_R",StringComparison.Ordinal) && solid.name.IndexOf("Thigh",StringComparison.Ordinal)>=0)
                {float sum=0;for(int v=solid.firstVertex;v<solid.firstVertex+solid.vertexCount;v++)sum+=module.mesh.vertices[v].x;if(solid.vertexCount>0)hipX=Mathf.Abs(sum/solid.vertexCount);}
                Vector3 hip = new Vector3(side==0 ? -hipX : hipX,1.04f,-0.025f);
                var leg = AddPiece(side==0?"LeftLeg":"RightLeg",root,hip,hip,1+side*2);
                Vector3 knee = new Vector3(hip.x,0.70f,hip.z);
                AddPiece("Knee",leg,knee-hip,knee,2+side*2);
            }
            Transform AddPiece(string name, Transform parent, Vector3 local, Vector3 pivot, int group)
            {
                string key = "rig:"+module.partId+":"+group;
                if (!meshes.TryGetValue(key,out var mesh))
                {
                    var vertices = new List<Vector3>(); var normals = new List<Vector3>();
                    var remap = new Dictionary<int,int>(); var triangles = new[]{new List<int>(),new List<int>(),new List<int>()};
                    for(int material=0;material<3;material++)
                    {
                        var source = module.mesh.subMeshes[material].triangles;
                        for(int t=0;t<source.Length;t+=3)
                        {
                            if(groupByVertex[source[t]]!=group)continue;
                            for(int j=0;j<3;j++)
                            {
                                int old=source[t+j]; if(groupByVertex[old]!=group)throw new ArgumentException("Cross-rig triangle.");
                                if(!remap.TryGetValue(old,out int index)) { index=vertices.Count;remap.Add(old,index);vertices.Add(module.mesh.vertices[old]-pivot);normals.Add(module.mesh.normals[old]); }
                                triangles[material].Add(index);
                            }
                        }
                    }
                    mesh=new Mesh{name=module.partId+"_"+name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.subMeshCount=3;
                    for(int i=0;i<3;i++)mesh.SetTriangles(triangles[i],i,false);mesh.RecalculateBounds();meshes.Add(key,mesh);
                }
                var piece=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));piece.transform.SetParent(parent,false);piece.transform.localPosition=local;
                piece.GetComponent<MeshFilter>().sharedMesh=mesh;piece.GetComponent<MeshRenderer>().sharedMaterials=palettes[palette];return piece.transform;
            }
        }
        private static void Validate(Source source)
        {
            if (source == null || source.vertices == null || source.vertices.Length == 0 || source.normals == null
                || source.normals.Length != source.vertices.Length || source.subMeshes == null || source.subMeshes.Length != 3) throw new ArgumentException("Invalid source mesh.");
            string[] names = { "TeamArmor", "Frame", "Signal" };
            for (int i = 0; i < source.vertices.Length; i++)
                if (!Finite(source.vertices[i]) || !Finite(source.normals[i]) || Mathf.Abs(source.normals[i].magnitude - 1) > 0.001f) throw new ArgumentException("Invalid visual coordinates.");
            for (int i = 0; i < 3; i++)
            {
                var group = source.subMeshes[i];
                if (group == null || group.material != names[i] || group.triangles == null || group.triangles.Length % 3 != 0) throw new ArgumentException("Invalid visual material group.");
                foreach (int index in group.triangles) if (index < 0 || index >= source.vertices.Length) throw new ArgumentException("Invalid visual index.");
            }
        }
        private static bool Finite(Vector3 v) { return Finite(v.x) && Finite(v.y) && Finite(v.z); }
        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        internal static Material Material(Shader shader, string name, Color color, float metallic = 0)
        {
            var result = new Material(shader) { name = name, enableInstancing = true };
            result.SetColor("_BaseColor", color); result.SetFloat("_Metallic", metallic); result.SetFloat("_Smoothness", 0.3f); return result;
        }
        public void Dispose()
        {
            foreach (var mesh in meshes.Values) UnityEngine.Object.Destroy(mesh);
            var materials = new HashSet<Material>(); foreach (var palette in palettes) foreach (var material in palette) materials.Add(material);
            foreach (var material in materials) UnityEngine.Object.Destroy(material);
            meshes.Clear();
        }
    }
}
