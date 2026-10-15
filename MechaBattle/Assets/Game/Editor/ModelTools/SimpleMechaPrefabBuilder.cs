using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MechaBattle.EditorTools.Models
{
    // Optional visual asset tool. No P0 scene, simulation, collider, or build setting is changed.
    public static class SimpleMechaPrefabBuilder
    {
        private const string SourcePath = "Assets/Game/Content/Models/MB_Worker01/MB_Worker01.mesh.json";
        private const string OutputFolder = "Assets/Game/Content/Models/MB_Worker01/Generated";

        [Serializable]
        private sealed class MeshGroup
        {
            public string material = "";
            public int[] triangles = Array.Empty<int>();
        }
        [Serializable]
        private sealed class MeshSource
        {
            public string name = "";
            public Vector3[] vertices = Array.Empty<Vector3>();
            public Vector3[] normals = Array.Empty<Vector3>();
            public MeshGroup[] subMeshes = Array.Empty<MeshGroup>();
        }

        [MenuItem("MechaBattle/Models/Generate Simple Robot Prefabs")]
        public static void Generate()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            var source = JsonUtility.FromJson<MeshSource>(File.ReadAllText(Path.Combine(project, SourcePath)));
            Validate(source);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit is required for this visual model.");
            EnsureFolder(OutputFolder);
            Mesh mesh = SaveMesh(source);
            Material frame = SaveMaterial("Frame", new Color(0.085f, 0.115f, 0.15f), 0.45f, 0.36f, false, shader);
            Material signal = SaveMaterial("Signal", new Color(0.25f, 0.95f, 0.80f), 0f, 0.65f, true, shader);
            BuildVariant("Neutral", new Color(0.59f, 0.65f, 0.70f), source, mesh, frame, signal, shader);
            BuildVariant("Blue", new Color(0.16f, 0.37f, 0.85f), source, mesh, frame, signal, shader);
            BuildVariant("Orange", new Color(0.95f, 0.40f, 0.10f), source, mesh, frame, signal, shader);
            AssetDatabase.SaveAssets();
            Debug.Log("MB_Worker01: 3 visual prefabs generated, one shared mesh / 3 material slots each. P0 scene unchanged. " + OutputFolder);
        }

        private static void Validate(MeshSource source)
        {
            if (source == null || source.vertices == null || source.normals == null || source.subMeshes == null ||
                source.vertices.Length == 0 || source.vertices.Length != source.normals.Length || source.subMeshes.Length != 3)
                throw new InvalidDataException("Invalid robot mesh structure.");
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < source.vertices.Length; i++)
            {
                Vector3 v = source.vertices[i], n = source.normals[i];
                if (!Finite(v.x) || !Finite(v.y) || !Finite(v.z) || !Finite(n.x) || !Finite(n.y) || !Finite(n.z) || Mathf.Abs(n.sqrMagnitude - 1) > 0.001f)
                    throw new InvalidDataException("Non-finite vertex or invalid normal.");
            }
            foreach (var group in source.subMeshes)
            {
                if (group == null || !seen.Add(group.material) ||
                    (group.material != "TeamArmor" && group.material != "Frame" && group.material != "Signal") ||
                    group.triangles == null || group.triangles.Length == 0 || group.triangles.Length % 3 != 0)
                    throw new InvalidDataException("Invalid robot material group.");
                foreach (int index in group.triangles)
                    if (index < 0 || index >= source.vertices.Length) throw new InvalidDataException("Triangle index out of bounds.");
            }
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        private static Mesh SaveMesh(MeshSource source)
        {
            var mesh = new Mesh { name = "MB_Worker01", indexFormat = source.vertices.Length <= 65535 ? IndexFormat.UInt16 : IndexFormat.UInt32 };
            mesh.vertices = source.vertices;
            mesh.normals = source.normals;
            mesh.subMeshCount = source.subMeshes.Length;
            for (int i = 0; i < source.subMeshes.Length; i++) mesh.SetTriangles(source.subMeshes[i].triangles, i, false);
            mesh.RecalculateBounds();
            string assetPath = OutputFolder + "/MB_Worker01_Mesh.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, assetPath); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Material SaveMaterial(string name, Color color, float metallic, float smoothness, bool emission, Shader shader)
        {
            string assetPath = OutputFolder + "/MB_Worker01_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            bool created = material == null;
            if (created) material = new Material(shader) { name = "MB_Worker01_" + name };
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            if (emission) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 0.65f); }
            else { material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black); }
            if (created) AssetDatabase.CreateAsset(material, assetPath);
            else EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildVariant(string variant, Color color, MeshSource source, Mesh mesh, Material frame, Material signal, Shader shader)
        {
            var armor = SaveMaterial("Armor_" + variant, color, 0.25f, 0.38f, false, shader);
            var materials = new Material[source.subMeshes.Length];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = source.subMeshes[i].material == "TeamArmor" ? armor : source.subMeshes[i].material == "Frame" ? frame : signal;
            // Work in an isolated preview scene; do not add objects to the user's open battlefield.
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = EditorUtility.CreateGameObjectWithHideFlags("MB_Worker01_" + variant, HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
                SceneManager.MoveGameObjectToScene(root, preview);
                root.hideFlags = HideFlags.None;
                root.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                string prefab = OutputFolder + "/MB_Worker01_" + variant + ".prefab";
                if (PrefabUtility.SaveAsPrefabAsset(root, prefab) == null) throw new IOException("Cannot save robot prefab: " + prefab);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
