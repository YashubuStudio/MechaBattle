using System;
using System.Collections.Generic;
using System.IO;
using MechaBattle.Contracts.Construction;
using MechaBattle.Construction;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MechaBattle.EditorTools.Models
{
    public static class SimpleMechaPrefabBuilder
    {
        public const string OutputFolder = "Assets/Game/Content/Models/MB_Worker01/Generated";
        private const string KitPath = "Assets/Game/Content/Models/MB_Worker01/module-kit.json";
        private static readonly string[] MaterialNames = { "TeamArmor", "Frame", "Signal" };
        [Serializable] private sealed class MeshGroup { public string material = ""; public int[] triangles = Array.Empty<int>(); }
        [Serializable] private sealed class MeshSource
        {
            public string name = "";
            public Vector3[] vertices = Array.Empty<Vector3>(), normals = Array.Empty<Vector3>();
            public MeshGroup[] subMeshes = Array.Empty<MeshGroup>();
        }
        [Serializable] private sealed class Socket { public string id = ""; public Vector3 position = Vector3.zero; }
        [Serializable] private sealed class Module
        {
            public string partId = "", connectionStandard = "", parentSocket = "";
            public Socket[] sockets = Array.Empty<Socket>();
            public MeshSource mesh = new MeshSource();
        }
        [Serializable] private sealed class Kit
        {
            public int schemaVersion = 0;
            public string connectionStandard = "";
            public Module[] modules = Array.Empty<Module>();
        }
        [Serializable] private sealed class ConfigRoot { public Prototype constructionPrototype = new Prototype(); }
        [Serializable] private sealed class Prototype { public ConstructionCatalog catalog = new ConstructionCatalog(); }
        private static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }

        [MenuItem("MechaBattle/Models/Generate Simple Robot Prefabs")]
        public static void Generate()
        {
            CheckEditMode();
            var catalog = JsonUtility.FromJson<ConfigRoot>(File.ReadAllText(Path.Combine(ProjectRoot, "config/prototype-defaults.json"))).constructionPrototype.catalog;
            var kit = ReadKit();
            // Complete validation before writing any generated asset.
            foreach (var build in catalog.presets) ValidateBuildModules(kit, ConstructionEvaluator.Evaluate(catalog, build));
            EnsureFolder(OutputFolder);
            var materials = Materials("Neutral");
            foreach (var module in kit.modules)
            {
                Mesh mesh = SaveMesh(module.mesh, "Part_" + Safe(module.partId));
                WithRoot(module.partId, root => {
                    AddRenderer(root, mesh, materials);
                    foreach (var socket in module.sockets) NewChild(socket.id, root.transform).localPosition = socket.position;
                    SavePrefab(root, OutputFolder + "/Part_" + Safe(module.partId) + ".prefab");
                });
            }
            foreach (var build in catalog.presets)
                foreach (var paint in new[] { "Neutral", "Blue", "Orange" }) GenerateBuildInternal(kit, catalog, build, paint);
            AssetDatabase.SaveAssets();
            Debug.Log("Modular mecha: " + kit.modules.Length + " compatible modules and " + catalog.presets.Length
                + " builds x 3 palettes generated. P0 scene unchanged.");
        }

        // The same saved build accepted by the domain evaluator drives the visual assembly.
        // Team palette is supplied by the match; cosmetic paint does not override team identity.
        public static string GenerateBuild(ConstructionCatalog catalog, BuildDefinition build, string teamPalette = null)
        {
            CheckEditMode();
            var kit = ReadKit();
            var resolved = ConstructionEvaluator.Evaluate(catalog, build);
            ValidateBuildModules(kit, resolved);
            string paint = teamPalette ?? resolved.PaintId;
            ValidatePalette(paint);
            EnsureFolder(OutputFolder);
            string output = GenerateBuildInternal(kit, catalog, build, paint);
            AssetDatabase.SaveAssets();
            return output;
        }

        private static string GenerateBuildInternal(Kit kit, ConstructionCatalog catalog, BuildDefinition build, string paint)
        {
            var resolved = ConstructionEvaluator.Evaluate(catalog, build);
            ValidateBuildModules(kit, resolved);
            var ids = new List<string> { resolved.FrameId };
            foreach (var part in resolved.Equipment) ids.Add(part.PartId);
            var vv = new List<Vector3>(); var nn = new List<Vector3>();
            var indices = new[] { new List<int>(), new List<int>(), new List<int>() };
            string output = OutputFolder + "/MB_Worker01_" + Safe(build.id) + "_" + paint + ".prefab";
            WithRoot("MB_Worker01_" + build.id, root => {
                var sockets = new Dictionary<string, Transform>(StringComparer.Ordinal);
                foreach (var id in ids)
                {
                    var module = Find(kit, id);
                    Transform parent = root.transform;
                    if (!string.IsNullOrEmpty(module.parentSocket) && !sockets.TryGetValue(module.parentSocket, out parent))
                        throw new ArgumentException("Missing parent socket: " + module.parentSocket);
                    var node = NewChild(module.partId, parent);
                    foreach (var socket in module.sockets)
                    {
                        var t = NewChild(socket.id, node); t.localPosition = socket.position;
                        if (sockets.ContainsKey(socket.id)) throw new ArgumentException("Duplicate assembled socket: " + socket.id);
                        sockets.Add(socket.id, t);
                    }
                    int offset = vv.Count;
                    for (int i = 0; i < module.mesh.vertices.Length; i++)
                    {
                        vv.Add(root.transform.InverseTransformPoint(node.TransformPoint(module.mesh.vertices[i])));
                        nn.Add(root.transform.InverseTransformDirection(node.TransformDirection(module.mesh.normals[i])).normalized);
                    }
                    for (int material = 0; material < 3; material++)
                        foreach (int index in module.mesh.subMeshes[material].triangles) indices[material].Add(offset + index);
                }
                var source = new MeshSource { vertices = vv.ToArray(), normals = nn.ToArray(), subMeshes = new MeshGroup[3] };
                for (int i = 0; i < 3; i++) source.subMeshes[i] = new MeshGroup { material = MaterialNames[i], triangles = indices[i].ToArray() };
                // Static prototype: keep assembly/socket hierarchy but bake one renderer and 3 material groups.
                Mesh mesh = SaveMesh(source, "Build_" + GeometryKey(resolved));
                AddRenderer(root, mesh, Materials(paint));
                SavePrefab(root, output);
            });
            return output;
        }

        private static Kit ReadKit()
        {
            var kit = JsonUtility.FromJson<Kit>(File.ReadAllText(Path.Combine(ProjectRoot, KitPath)));
            if (kit == null || kit.schemaVersion != 1 || string.IsNullOrWhiteSpace(kit.connectionStandard) || kit.modules == null || kit.modules.Length == 0)
                throw new ArgumentException("Invalid modular kit schema.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var module in kit.modules)
            {
                if (module == null || string.IsNullOrWhiteSpace(module.partId) || !ids.Add(module.partId)
                    || module.connectionStandard != kit.connectionStandard || module.sockets == null)
                    throw new ArgumentException("Invalid module definition.");
                ValidateMesh(module.mesh);
                var sockets = new HashSet<string>(StringComparer.Ordinal);
                foreach (var socket in module.sockets)
                    if (socket == null || string.IsNullOrWhiteSpace(socket.id) || !sockets.Add(socket.id) || !Finite(socket.position))
                        throw new ArgumentException("Invalid socket on " + module.partId);
            }
            return kit;
        }
        private static void ValidateBuildModules(Kit kit, ResolvedBuild build)
        {
            if (build.ConnectionStandard != kit.connectionStandard) throw new ArgumentException("Visual connection standard mismatch.");
            var frame = Find(kit, build.FrameId);
            if (frame.parentSocket != "") throw new ArgumentException("Frame must be a root module.");
            var sockets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var socket in frame.sockets) sockets.Add(socket.id);
            foreach (var choice in build.Equipment)
            {
                var module = Find(kit, choice.PartId);
                string expected = choice.Slot == PartSlot.LeftArm ? "arm.left" : choice.Slot == PartSlot.RightArm ? "arm.right" : "weapon.primary";
                if (module.parentSocket != expected || !sockets.Contains(expected))
                    throw new ArgumentException("Visual module socket mismatch: " + choice.PartId);
                foreach (var socket in module.sockets)
                    if (!sockets.Add(socket.id)) throw new ArgumentException("Duplicate assembled socket: " + socket.id);
            }
        }
        private static Module Find(Kit kit, string id)
        {
            foreach (var module in kit.modules) if (module.partId == id) return module;
            throw new ArgumentException("Visual module is unavailable: " + id);
        }
        private static void ValidateMesh(MeshSource source)
        {
            if (source == null || source.vertices == null || source.vertices.Length == 0 || source.normals == null
                || source.normals.Length != source.vertices.Length || source.subMeshes == null || source.subMeshes.Length != 3)
                throw new ArgumentException("Invalid mesh arrays.");
            for (int i = 0; i < source.vertices.Length; i++)
                if (!Finite(source.vertices[i]) || !Finite(source.normals[i]) || Mathf.Abs(source.normals[i].magnitude - 1) > 0.001f)
                    throw new ArgumentException("Mesh contains a non-finite vertex or invalid normal.");
            int count = 0;
            for (int i = 0; i < 3; i++)
            {
                var group = source.subMeshes[i];
                if (group == null || group.material != MaterialNames[i] || group.triangles == null || group.triangles.Length % 3 != 0)
                    throw new ArgumentException("Invalid material group.");
                count += group.triangles.Length;
                foreach (int index in group.triangles) if (index < 0 || index >= source.vertices.Length) throw new ArgumentException("Invalid mesh index.");
            }
            if (count == 0) throw new ArgumentException("Mesh has no faces.");
        }
        private static bool Finite(Vector3 v) { return Finite(v.x) && Finite(v.y) && Finite(v.z); }
        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        private static void CheckEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Generate mecha assets in Edit Mode.");
        }
        private static string GeometryKey(ResolvedBuild build)
        {
            var value = new System.Text.StringBuilder();
            value.Append(build.FrameId.Length).Append(':').Append(build.FrameId);
            foreach (var part in build.Equipment) value.Append('|').Append((int)part.Slot).Append(':').Append(part.PartId.Length).Append(':').Append(part.PartId);
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value.ToString()))).Replace("-", "");
        }
        private static string Safe(string id)
        {
            // Encode every unsafe UTF-16 code unit; two different IDs cannot silently overwrite one asset.
            var text = new System.Text.StringBuilder();
            foreach (char c in id)
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-') text.Append(c);
                else text.Append('_').Append(((int)c).ToString("X4"));
            return text.ToString();
        }
        private static void ValidatePalette(string paint)
        {
            if (paint != "Neutral" && paint != "Blue" && paint != "Orange") throw new ArgumentException("Unknown palette: " + paint);
        }
        private static Material[] Materials(string paint)
        {
            ValidatePalette(paint);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit is required.");
            Color armor = paint == "Blue" ? new Color(0.16f, 0.37f, 0.85f) : paint == "Orange" ? new Color(0.95f, 0.40f, 0.10f) : new Color(0.59f, 0.65f, 0.70f);
            return new[] { SaveMaterial("Armor_" + paint, armor, 0.25f, false, shader),
                SaveMaterial("Frame", new Color(0.085f, 0.115f, 0.15f), 0.45f, false, shader),
                SaveMaterial("Signal", new Color(0.25f, 0.95f, 0.80f), 0, true, shader) };
        }
        private static Mesh SaveMesh(MeshSource source, string name)
        {
            ValidateMesh(source);
            var mesh = new Mesh { name = name, indexFormat = source.vertices.Length <= 65535 ? IndexFormat.UInt16 : IndexFormat.UInt32 };
            mesh.vertices = source.vertices; mesh.normals = source.normals; mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(source.subMeshes[i].triangles, i, false);
            mesh.RecalculateBounds();
            string assetPath = OutputFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, assetPath); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing); return existing;
        }
        private static Material SaveMaterial(string name, Color color, float metallic, bool emission, Shader shader)
        {
            string assetPath = OutputFolder + "/MB_Worker01_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            bool created = material == null;
            if (created) material = new Material(shader) { name = "MB_Worker01_" + name };
            material.shader = shader; material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", 0.38f); material.enableInstancing = true;
            if (emission) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 0.65f); }
            else { material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black); }
            if (created) AssetDatabase.CreateAsset(material, assetPath); else EditorUtility.SetDirty(material);
            return material;
        }
        private static void AddRenderer(GameObject root, Mesh mesh, Material[] materials)
        {
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        }
        private static Transform NewChild(string name, Transform parent)
        {
            var child = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
            SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
            child.transform.SetParent(parent, false); child.hideFlags = HideFlags.None;
            return child.transform;
        }
        private static void WithRoot(string name, Action<GameObject> action)
        {
            Scene preview = EditorSceneManager.NewPreviewScene(); GameObject root = null;
            try
            {
                root = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(root, preview); root.hideFlags = HideFlags.None; action(root);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
        private static void SavePrefab(GameObject root, string output)
        {
            if (PrefabUtility.SaveAsPrefabAsset(root, output) == null) throw new IOException("Cannot save prefab: " + output);
        }
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace(Path.DirectorySeparatorChar, '/');
            if (string.IsNullOrEmpty(parent)) throw new IOException("Invalid asset folder: " + folder);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
