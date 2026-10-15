using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
using MechaBattle.UnityAdapters;

namespace MechaBattle.EditorTools.Generators
{
    // P0-02: builds a battlefield LAYOUT (ground, main bases, capture points, 100
    // controller-slot placeholders) from config/standard-map.json + MatchRoster, and
    // wires a BattleSimulationRunner so the scene is driven by the real Game.Simulation
    // (P0-01, GPT) instead of a disconnected placeholder. The only P0 placeholder left
    // is the BOT brain itself (ForwardMarchBotCommandSource has no enemy perception);
    // real TeamKnowledge-based BOT/Squad/Commander AI is later P0-03+ work.
    public static class BattlefieldSceneGenerator
    {
        private const string GeneratedScenePath = "Assets/Game/Scenes/P0_Battlefield.unity";
        private const string MapConfigRelativePath = "config/standard-map.json";
        private const string PrototypeConfigRelativePath = "config/prototype-defaults.json";
        private const int DeterministicSeed = 1234;

        [Serializable]
        private sealed class PrototypeConfigDocument
        {
            public SimulationConfig p0Simulation;
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("MechaBattle/Generators/Generate P0 Battlefield Scene")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before generating the battlefield scene.");

            // NewSceneMode.Single below would silently discard ANY currently loaded scene,
            // not just the active one (additive scenes included) - check them all.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.isDirty)
                    throw new InvalidOperationException(
                        "Scene '" + loaded.name + "' has unsaved changes. Save or discard them before " +
                        "generating a new scene; this generator never auto-saves or overwrites unsaved work.");
            }

            string mapFile = Path.Combine(ProjectRoot, MapConfigRelativePath);
            if (!File.Exists(mapFile))
                throw new FileNotFoundException("Map config not found: " + mapFile);
            var map = JsonUtility.FromJson<MapDefinition>(File.ReadAllText(mapFile));
            MapValidator.ThrowIfInvalid(map);

            string prototypeFile = Path.Combine(ProjectRoot, PrototypeConfigRelativePath);
            if (!File.Exists(prototypeFile))
                throw new FileNotFoundException("Prototype config not found: " + prototypeFile);
            var simConfig = JsonUtility.FromJson<PrototypeConfigDocument>(File.ReadAllText(prototypeFile)).p0Simulation;
            if (simConfig == null)
                throw new InvalidOperationException("config/prototype-defaults.json has no p0Simulation section.");
            simConfig.Validate();

            EnsureFolder("Assets/Game/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camera = new GameObject("Main Camera", typeof(Camera));
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, Mathf.Max(map.halfWidth, map.halfDepth) * 0.8f, -map.halfDepth * 1.2f);
            camera.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

            var root = new GameObject("P0_Battlefield_" + map.id);

            // Full-BOT bench scene: no human input source exists yet, so reserving slot 0
            // for a human would just sit idle. Pass humanSlotId=0 instead for a manual
            // single-human-slot test once player input lands.
            const int humanSlotId = -1;

            BuildGround(map, root.transform);
            var baseMaterials = BuildTeamMaterials();
            BuildMainBases(map, root.transform, baseMaterials);
            BuildCapturePoints(map, root.transform);
            Transform[] unitTransforms = BuildUnits(map, root.transform, humanSlotId, baseMaterials);

            var runnerGo = new GameObject("BattleSimulationRunner");
            runnerGo.transform.SetParent(root.transform);
            var runner = runnerGo.AddComponent<BattleSimulationRunner>();
            runner.Initialize(map, simConfig, humanSlotId, unitTransforms);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, GeneratedScenePath);
            if (!saved)
                throw new IOException("Failed to save generated scene to " + GeneratedScenePath);

            Debug.Log(string.Format(
                "P0 battlefield generated: {0} main bases, {1} capture points, {2} controller slots " +
                "({3} per team) -> {4}. BattleSimulationRunner self-bootstraps from serialized map/" +
                "config JSON (Awake), so saving and reloading this scene keeps working. The only " +
                "placeholder left is ForwardMarchBotCommandSource (no enemy-unit perception, only " +
                "public base geometry). Enter Play Mode to step the simulation; this scene was NOT " +
                "added to Build Settings automatically.",
                map.mainBases.Length, map.capturePoints.Length, unitTransforms.Length,
                unitTransforms.Length / MatchLimits.TeamCount, GeneratedScenePath));
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;
            string parent = Path.GetDirectoryName(assetFolderPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolderPath);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void BuildGround(MapDefinition map, Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent);
            // Default Unity Plane is 10x10 world units at scale 1.
            ground.transform.localScale = new Vector3(map.halfWidth * 2f / 10f, 1f, map.halfDepth * 2f / 10f);
        }

        private const string MaterialFolder = "Assets/Game/Content/Materials/Generated";

        private static Shader RequireUrpLitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException(
                    "Universal Render Pipeline/Lit shader not found. Is URP installed and active " +
                    "for this project? Refusing to silently fall back to the Standard shader.");
            return shader;
        }

        // Reuses an existing generated material asset if present (so repeated generation
        // doesn't pile up throwaway Material instances embedded only in the scene) and
        // otherwise creates+saves one under MaterialFolder.
        private static Material CreateOrLoadMaterial(string assetPath, Color color)
        {
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null) { existing.color = color; return existing; }
            var material = new Material(RequireUrpLitShader()) { color = color };
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        private static Material[] BuildTeamMaterials()
        {
            var blue = CreateOrLoadMaterial(MaterialFolder + "/TeamBlue.mat", new Color(0.2f, 0.4f, 1f));
            var orange = CreateOrLoadMaterial(MaterialFolder + "/TeamOrange.mat", new Color(1f, 0.5f, 0.1f));
            var neutral = CreateOrLoadMaterial(MaterialFolder + "/Neutral.mat", new Color(0.7f, 0.7f, 0.7f));
            return new[] { blue, orange, neutral };
        }

        private static void BuildMainBases(MapDefinition map, Transform parent, Material[] teamMaterials)
        {
            foreach (var b in map.mainBases)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "MainBase_" + b.id;
                go.transform.SetParent(parent);
                go.transform.position = new Vector3(b.x, 1f, b.z);
                go.transform.localScale = new Vector3(b.attackRadius * 2f, 2f, b.attackRadius * 2f);
                go.GetComponent<Renderer>().sharedMaterial = teamMaterials[b.team];
                var marker = go.AddComponent<MapObjectiveMarker>();
                marker.Kind = ObjectiveKind.MainBase;
                marker.ObjectiveId = b.id;
                marker.Team = b.team;
                marker.Radius = b.attackRadius;
            }
        }

        private static void BuildCapturePoints(MapDefinition map, Transform parent)
        {
            var neutralMat = CreateOrLoadMaterial(MaterialFolder + "/CapturePoint.mat", new Color(0.8f, 0.8f, 0.3f));
            foreach (var c in map.capturePoints)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "CapturePoint_" + c.id;
                go.transform.SetParent(parent);
                go.transform.position = new Vector3(c.x, 0.1f, c.z);
                go.transform.localScale = new Vector3(c.radius * 2f, 0.1f, c.radius * 2f);
                go.GetComponent<Renderer>().sharedMaterial = neutralMat;
                var marker = go.AddComponent<MapObjectiveMarker>();
                marker.Kind = ObjectiveKind.CapturePoint;
                marker.ObjectiveId = c.id;
                marker.Team = -1;
                marker.Radius = c.radius;
            }
        }

        // Returns one Transform per controller slot, indexed by slot id, so the caller
        // can hand BattleSimulationRunner a slot-ordered array to drive from snapshots.
        private static Transform[] BuildUnits(MapDefinition map, Transform parent, int humanSlotId, Material[] teamMaterials)
        {
            var roster = new MatchRoster(humanSlotId);
            var basesByTeam = new MainBaseDefinition[MatchLimits.TeamCount][];
            for (int t = 0; t < MatchLimits.TeamCount; t++)
                basesByTeam[t] = map.mainBases.Where(b => b.team == t).ToArray();

            var rng = new System.Random(DeterministicSeed);
            var unitsRoot = new GameObject("Units");
            unitsRoot.transform.SetParent(parent);
            int[] indexWithinTeam = new int[MatchLimits.TeamCount];
            var transforms = new Transform[roster.Controllers.Count];

            foreach (var slot in roster.Controllers)
            {
                var teamBases = basesByTeam[slot.Team];
                var home = teamBases[indexWithinTeam[slot.Team] % teamBases.Length];
                indexWithinTeam[slot.Team]++;

                float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                float radiusFrac = (float)Math.Sqrt(rng.NextDouble()) * home.spawnRadius;
                var spawnPos = new Vector3(
                    home.x + Mathf.Cos(angle) * radiusFrac,
                    1f,
                    home.z + Mathf.Sin(angle) * radiusFrac);

                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = string.Format("Unit_{0:D3}_T{1}_{2}", slot.Id, slot.Team, slot.Controller);
                go.transform.SetParent(unitsRoot.transform);
                go.transform.position = spawnPos;
                go.GetComponent<Renderer>().sharedMaterial = teamMaterials[slot.Team];

                var presentation = go.AddComponent<UnitPresentation>();
                presentation.SlotId = slot.Id;
                presentation.Team = slot.Team;
                transforms[slot.Id] = go.transform;
            }

            return transforms;
        }
    }
}
