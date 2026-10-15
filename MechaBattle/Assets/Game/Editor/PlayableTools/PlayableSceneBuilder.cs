using System;
using System.IO;
using MechaBattle.Battlefield;
using MechaBattle.Construction;
using MechaBattle.Playable;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MechaBattle.EditorTools
{
    public static class PlayableSceneBuilder
    {
        public const string ScenePath = "Assets/Game/Scenes/MechaBattle_Playable.unity";
        private const string DataFolder = "Assets/Game/Content/Playable";
        private static string Project => Directory.GetParent(Application.dataPath).FullName;
        [MenuItem("MechaBattle/Playable/Generate and Open Playable Scene")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before generating the playable scene.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your open scenes first. Playable generation never discards unsaved work.");
            var configText = File.ReadAllText(Path.Combine(Project, "config/prototype-defaults.json"));
            var config = JsonUtility.FromJson<PlayableConfiguration>(configText);
            config.playablePrototype.Apply(config.p0Simulation);
            DeckConstruction.CreateDefault(config.constructionPrototype.catalog, config.playablePrototype.starterDeckSlots);
            var mapText = File.ReadAllText(Path.Combine(Project, "config/standard-map.json"));
            MapValidator.ThrowIfInvalid(JsonUtility.FromJson<MapDefinition>(mapText));
            if (Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            Shader lit = Shader.Find("Universal Render Pipeline/Lit"), unlit = Shader.Find("Universal Render Pipeline/Unlit"), text = Shader.Find("TextMeshPro/Mobile/Distance Field");
            if (lit == null || unlit == null || text == null) throw new InvalidOperationException("Wait for URP/TMP resources to finish importing, then run Generate again.");
            Folder(DataFolder); Folder("Assets/Game/Scenes");
            File.WriteAllText(Path.Combine(Project, DataFolder + "/PrototypeConfig.json"), configText);
            File.WriteAllText(Path.Combine(Project, DataFolder + "/StandardMap.json"), mapText);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var map = AssetDatabase.LoadAssetAtPath<TextAsset>(DataFolder + "/StandardMap.json");
            var settings = AssetDatabase.LoadAssetAtPath<TextAsset>(DataFolder + "/PrototypeConfig.json");
            var kit = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Models/MB_Worker01/module-kit.json");
            if (map == null || settings == null || kit == null) throw new InvalidOperationException("Playable TextAssets were not imported.");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("MechaBattle_Playable", typeof(PlayableEntry));
            go.GetComponent<PlayableEntry>().Configure(map, settings, kit, lit, unlit, text);
            EditorUtility.SetDirty(go.GetComponent<PlayableEntry>());
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Cannot save " + ScenePath);
            Debug.Log("Playable scene ready. Press Play: prepare a deck, deploy, fight, redeploy, then replay. Windows build: MechaBattle > Playable > Build Windows Playable.");
        }
        [MenuItem("MechaBattle/Playable/Build Windows Playable")]
        public static void BuildWindows()
        {
            Generate();
            string output = Path.Combine(Project, "Build/PlayableWindows/MechaBattle.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development });
            string folder = Path.Combine(Project, "Build/Reports"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "playable-windows-build.json"), JsonUtility.ToJson(new Record {
                scope = "playable-offline-first-loop-not-performance-certification", result = report.summary.result.ToString(),
                output = report.summary.outputPath, errors = report.summary.totalErrors, warnings = report.summary.totalWarnings, unityVersion = Application.unityVersion }, true));
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Playable Windows build failed.");
        }
        private static void Folder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace(Path.DirectorySeparatorChar, '/');
            if (string.IsNullOrEmpty(parent)) throw new IOException("Invalid asset path.");
            Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
        [Serializable] private sealed class Record { public string scope, result, output, unityVersion; public int errors, warnings; }
    }
}
