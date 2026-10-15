using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using MechaBattle.Construction;
using MechaBattle.Contracts;
using MechaBattle.Playable;
using MechaBattle.EditorTools;
using MechaBattle.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MechaBattle.Tests.Playable
{
    public sealed class PlayableFlowTests
    {
        private const string TestScene = "Assets/Game/Tests/Generated/PlayableFlow.unity";
        private const string TestConfig = "Assets/Game/Tests/Generated/PlayableFlowConfig.asset";
        private string originalScene;
        private bool generated;
        [UnityTest]
        public IEnumerator PreparationConstructionBattlePauseResultsAndReplaySurviveSceneReload()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) Assert.Ignore("Save open scenes to run scene-generating integration tests.");
            originalScene = SceneManager.GetActiveScene().path;
            PlayableSceneBuilder.Generate();
            if (!AssetDatabase.IsValidFolder("Assets/Game/Tests/Generated")) AssetDatabase.CreateFolder("Assets/Game/Tests", "Generated");
            var config = JsonUtility.FromJson<PlayableConfiguration>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Playable/PrototypeConfig.json").text);
            config.p0Simulation.maxDurationSeconds = 0.5f;
            AssetDatabase.CreateAsset(new TextAsset(JsonUtility.ToJson(config)), TestConfig); generated = true;
            var entry = UnityEngine.Object.FindFirstObjectByType<PlayableEntry>();
            entry.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Playable/StandardMap.json"),
                AssetDatabase.LoadAssetAtPath<TextAsset>(TestConfig),
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Content/Models/MB_Worker01/module-kit.json"),
                Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("Universal Render Pipeline/Unlit"), Shader.Find("TextMeshPro/Mobile/Distance Field"), false);
            Assert.That(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), TestScene), Is.True);
            EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            entry = UnityEngine.Object.FindFirstObjectByType<PlayableEntry>();
            Assert.That(entry, Is.Not.Null); Assert.That(entry.IsInPreparation, Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Canvas.ForceUpdateCanvases();
            var canvas = entry.GetComponentInChildren<Canvas>(); var bounds = new Vector3[4]; canvas.GetComponent<RectTransform>().GetWorldCorners(bounds);
            foreach (var button in entry.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                var corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(bounds[0].x - 1), button.name);
                Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(bounds[0].y - 1), button.name);
                Assert.That(corners[2].x, Is.LessThanOrEqualTo(bounds[2].x + 1), button.name);
                Assert.That(corners[2].y, Is.LessThanOrEqualTo(bounds[2].y + 1), button.name);
            }
            var before = entry.CopyProfile().deck[0].equipment[0].partId;
            entry.CyclePart(0); Assert.That(entry.CopyProfile().deck[0].equipment[0].partId, Is.Not.EqualTo(before));
            entry.SelectTeam(1); entry.SelectDeckSlot(1); entry.ApplyPreset(0);
            entry.BeginMatch(); Assert.That(entry.Simulation.AliveCount, Is.EqualTo(100)); Assert.That(entry.HumanSlotId, Is.EqualTo(50));
            Assert.That(entry.Simulation.GetUnit(50).Controller, Is.EqualTo(ControllerType.Human));
            var custom = entry.CopyProfile().deck[0];
            var expected = ConstructionEvaluator.Evaluate(config.constructionPrototype.catalog, custom);
            Assert.That(entry.Simulation.GetUnit(50).Health, Is.EqualTo(expected.MaxHealth));
            entry.PauseMatch(); long tick = entry.Simulation.CurrentTick; entry.AdvanceTick(); Assert.That(entry.Simulation.CurrentTick, Is.EqualTo(tick));
            entry.ResumeMatch();
            for (int i = 0; i < 30; i++) entry.AdvanceTick();
            Assert.That(entry.Simulation.Phase, Is.EqualTo(MatchPhase.Finished));
            Assert.That(entry.transform.Find("PlayableCanvas/Results").gameObject.activeSelf, Is.True);
            entry.BeginMatch(); Assert.That(entry.Simulation.CurrentTick, Is.Zero); Assert.That(entry.Simulation.AliveCount, Is.EqualTo(100));
            entry.ReturnToPreparation(); Assert.That(entry.IsInPreparation, Is.True);
            Assert.That(entry.transform.Find("PlayableCanvas/Preparation").gameObject.activeSelf, Is.True);
            yield return new ExitPlayMode();
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (!string.IsNullOrEmpty(originalScene) && File.Exists(originalScene)) EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
            else if (generated) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (generated) { AssetDatabase.DeleteAsset(TestScene); AssetDatabase.DeleteAsset(TestConfig); }
        }
    }
}
