using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
using MechaBattle.UnityAdapters;
using MechaBattle.EditorTools.Generators;

namespace MechaBattle.Tests.Generators
{
    // Exercises the P0-02 Editor generator against the real config/standard-map.json.
    // Not a PlayMode/perf test: only checks that the generator produces the declared
    // topology (base/capture counts, 50/50 unit split) without touching Build Settings.
    public sealed class BattlefieldSceneGeneratorTests
    {
        [SetUp]
        public void SkipIfActiveSceneHasUnsavedChanges()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                Assert.Ignore("Active scene has unsaved changes; the generator refuses to discard them.");
        }

        [Test]
        public void Generate_ProducesDeclaredTopologyFromStandardMap()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var map = JsonUtility.FromJson<MapDefinition>(
                File.ReadAllText(Path.Combine(projectRoot, "config/standard-map.json")));
            MapValidator.ThrowIfInvalid(map);

            BattlefieldSceneGenerator.Generate();
            Scene scene = EditorSceneManager.GetActiveScene();

            var presentations = AllComponents<UnitPresentation>(scene);
            Assert.That(presentations.Count, Is.EqualTo(MatchLimits.TotalControllerSlots));
            foreach (int team in new[] { 0, 1 })
                Assert.That(presentations.Count(p => p.Team == team), Is.EqualTo(MatchLimits.ControllerSlotsPerTeam));
            Assert.That(presentations.Select(p => p.SlotId).Distinct().Count(), Is.EqualTo(MatchLimits.TotalControllerSlots));

            var markers = AllComponents<MapObjectiveMarker>(scene);
            Assert.That(markers.Count(m => m.Kind == ObjectiveKind.MainBase), Is.EqualTo(map.mainBases.Length));
            Assert.That(markers.Count(m => m.Kind == ObjectiveKind.CapturePoint), Is.EqualTo(map.capturePoints.Length));
            foreach (int team in new[] { 0, 1 })
                Assert.That(
                    markers.Count(m => m.Kind == ObjectiveKind.MainBase && m.Team == team),
                    Is.GreaterThanOrEqualTo(MatchLimits.MinimumMainBasesPerTeam));

            var runners = AllComponents<BattleSimulationRunner>(scene);
            Assert.That(runners.Count, Is.EqualTo(1));
            BattleSimulation sim = runners[0].Simulation;
            Assert.That(sim, Is.Not.Null);
            Assert.That(sim.AliveCount, Is.EqualTo(MatchLimits.TotalControllerSlots));

            long startTick = sim.CurrentTick;
            for (int i = 0; i < 25; i++)
                runners[0].Tick();
            Assert.That(sim.CurrentTick, Is.EqualTo(startTick + 25));
            for (int slot = 0; slot < MatchLimits.TotalControllerSlots; slot++)
            {
                var u = sim.GetUnit(slot);
                Assert.That(float.IsNaN(u.X) || float.IsInfinity(u.X), Is.False);
                Assert.That(float.IsNaN(u.Z) || float.IsInfinity(u.Z), Is.False);
            }
        }

        // The main P0-02 acceptance bar per docs/reviews/GPT_REVIEW_CLAUDE_P0.md: a saved
        // scene must still work after a real reload, not just while the generator's
        // in-memory objects are still alive. OpenScene forces an actual deserialize, so
        // BattleSimulationRunner.Awake() has to rebuild Simulation/CommandSource from the
        // serialized JSON fields on its own - exactly what Bootstrap() exists for.
        [Test]
        public void Generate_SavedSceneReloadsAndRebuildsSimulationOnItsOwn()
        {
            BattlefieldSceneGenerator.Generate();
            const string path = "Assets/Game/Scenes/P0_Battlefield.unity";

            Scene reloaded = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var runners = AllComponents<BattleSimulationRunner>(reloaded);
            Assert.That(runners.Count, Is.EqualTo(1));

            BattleSimulation sim = runners[0].Simulation;
            Assert.That(sim, Is.Not.Null, "BattleSimulationRunner.Awake() did not rebuild Simulation after a real scene reload.");
            Assert.That(sim.AliveCount, Is.EqualTo(MatchLimits.TotalControllerSlots));
            Assert.That(sim.CurrentTick, Is.EqualTo(0));

            for (int i = 0; i < 10; i++)
                runners[0].Tick();
            Assert.That(sim.CurrentTick, Is.EqualTo(10));
        }

        private static System.Collections.Generic.List<T> AllComponents<T>(Scene scene) where T : Component
        {
            var result = new System.Collections.Generic.List<T>();
            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>(includeInactive: true));
            return result;
        }
    }
}
