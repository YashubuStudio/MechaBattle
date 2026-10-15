using System;
using System.Collections.Generic;
using MechaBattle.Contracts.Construction;
using MechaBattle.Construction;
using UnityEngine;
using UnityEngine.Rendering;

namespace MechaBattle.Playable
{
    public sealed class MechaMeshLibrary : IDisposable
    {
        [Serializable] private sealed class Group { public string material = ""; public int[] triangles = Array.Empty<int>(); }
        [Serializable] private sealed class Source
        {
            public Vector3[] vertices = Array.Empty<Vector3>(), normals = Array.Empty<Vector3>();
            public Group[] subMeshes = Array.Empty<Group>();
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
            var signal = Material(shader, "SharedSensor", new Color(0.25f, 0.95f, 0.80f), 0);
            signal.EnableKeyword("_EMISSION"); signal.SetColor("_EmissionColor", new Color(0.15f, 0.6f, 0.45f));
            palettes = new[] {
                new[] { Material(shader, "BlueArmor", new Color(0.16f, 0.37f, 0.85f), 0.25f), frame, signal },
                new[] { Material(shader, "OrangeArmor", new Color(0.95f, 0.40f, 0.10f), 0.25f), frame, signal },
                new[] { Material(shader, "NeutralArmor", new Color(0.59f, 0.65f, 0.70f), 0.25f), frame, signal } };
        }
        public GameObject Create(ResolvedBuild build, int palette, Transform parent)
        {
            if (palette < 0 || palette >= palettes.Length || build.ConnectionStandard != kit.connectionStandard) throw new ArgumentException("Invalid palette or connection standard.");
            var key = new System.Text.StringBuilder().Append(build.FrameId.Length).Append(':').Append(build.FrameId);
            var ids = new List<string> { build.FrameId };
            foreach (var equipment in build.Equipment) { key.Append('|').Append(equipment.PartId.Length).Append(':').Append(equipment.PartId); ids.Add(equipment.PartId); }
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
