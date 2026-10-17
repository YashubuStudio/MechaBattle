using System;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
using NUnit.Framework;

namespace MechaBattle.Tests
{
    public sealed class DeploymentTests
    {
        [TestCase(0, 0)] [TestCase(0, 1)] [TestCase(0, 2)]
        [TestCase(50, 3)] [TestCase(50, 4)] [TestCase(50, 5)]
        public void InitialDeploymentUsesTheSelectedBaseAndKeepsAllOtherSlotsUnchanged(int human, int selected)
        {
            var map = SimulationTests.Map(); var c = SimulationTests.Config();
            var baseline = new BattleSimulation(map, c, human);
            var world = new BattleSimulation(map, c, human, initialHumanDeckIndex: 1, humanMainBaseIndex: selected);
            var u = world.GetUnit(human); var b = map.mainBases[selected];
            Assert.That(Math.Abs(u.X - b.x), Is.LessThan(b.spawnRadius)); Assert.That(Math.Abs(u.Z - b.z), Is.LessThan(b.spawnRadius));
            Assert.That(u.DeckIndex, Is.EqualTo(1)); Assert.That(world.GetDeckReadyTick(human, 1), Is.Zero);
            Assert.That(world.Counters.UnitsSpawned, Is.EqualTo(100)); Assert.That(world.Counters.UnitsDestroyed, Is.Zero);
            Assert.That(world.GetGauge(0), Is.EqualTo(c.initialGauge)); Assert.That(world.GetGauge(1), Is.EqualTo(c.initialGauge));
            for (int i = 0; i < 100; i++) if (i != human)
            { Assert.That(world.GetUnit(i).X, Is.EqualTo(baseline.GetUnit(i).X)); Assert.That(world.GetUnit(i).Z, Is.EqualTo(baseline.GetUnit(i).Z)); }
        }
        [TestCase(-2)] [TestCase(6)] [TestCase(3)]
        public void InitialDeploymentRejectsInvalidOrEnemyBase(int selected)
        { Assert.Throws<ArgumentOutOfRangeException>(() => new BattleSimulation(SimulationTests.Map(), SimulationTests.Config(), humanMainBaseIndex: selected)); }
        [Test]
        public void AGlobalBaseIndexWorksWithInterleavedTeamDefinitionsAndBotOnlyOverrideIsRejected()
        {
            var map = SimulationTests.Map(); var first = map.mainBases[0]; map.mainBases[0] = map.mainBases[4]; map.mainBases[4] = first;
            var world = new BattleSimulation(map, SimulationTests.Config(), humanMainBaseIndex: 4);
            Assert.That(world.GetUnit(0).Z, Is.EqualTo(first.z).Within(first.spawnRadius));
            Assert.That(world.CanSpawnAtMainBase(0, 0), Is.False); Assert.That(world.CanSpawnAtMainBase(0, 4), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleSimulation(map, SimulationTests.Config(), -1, humanMainBaseIndex: 4));
        }
        [TestCase(0, 2, 5)] [TestCase(50, 5, 2)]
        public void ManualBaseSelectionPreservesDeckCooldownAndRespawnDelay(int human, int own, int enemy)
        {
            var map = SimulationTests.Map(); var c = SimulationTests.Config();
            c.baseCounterFireDamage = 1000; c.baseCounterFireIntervalSeconds = 100; c.baseCounterFireRadius = 1000; c.respawnDelaySeconds = 0.2f;
            var world = new BattleSimulation(map, c, human); world.SetAutomaticRespawn(human, false);
            Assert.That(world.RequestSpawn(human, 1, mainBaseIndex: own), Is.False, "A living unit cannot redeploy.");
            world.Step(); Assert.That(world.GetUnit(human).IsAlive, Is.False);
            Assert.That(world.RequestSpawn(human, 0, mainBaseIndex: own), Is.False, "The destroyed deck retains its CT.");
            Assert.That(world.RequestSpawn(human, 1, mainBaseIndex: enemy), Is.False);
            Assert.That(world.RequestSpawn(human, 1, mainBaseIndex: 6), Is.False);
            Assert.That(world.RequestSpawn(human, 1, mainBaseIndex: -2), Is.False);
            Assert.That(world.RequestSpawn(human, 1, 0, own), Is.False, "A relay and a base cannot both be selected.");
            Assert.That(world.RequestSpawn(human, 1, mainBaseIndex: own), Is.True);
            while (world.CurrentTick < world.GetRespawnReadyTick(human)) { Assert.That(world.GetUnit(human).IsAlive, Is.False); world.Step(); }
            world.Step(); var u = world.GetUnit(human);
            Assert.That(u.IsAlive, Is.True); Assert.That(u.DeckIndex, Is.EqualTo(1));
            Assert.That(u.Z, Is.EqualTo(map.mainBases[own].z).Within(map.mainBases[own].spawnRadius));
            Assert.That(world.GetDeckReadyTick(human, 0), Is.GreaterThan(world.CurrentTick));
        }
        private static BattleSimulation CapturedWorld(float respawnDelay = 0.04f)
        {
            var c = FieldFixture.Config(); c.captureSecondsPerAdvantage = 0.1f; c.respawnDelaySeconds = respawnDelay;
            var world = FieldFixture.World(FieldFixture.Gun(speed: 0, damage: 1100), config: c);
            FieldFixture.MoveTo(world, 0, 0, 0); FieldFixture.Wait(world, 10);
            Assert.That(world.CanSpawnAtCapture(0, 0), Is.True); world.SetAutomaticRespawn(0, false);
            var attacker = world.GetUnit(50); var target = world.GetUnit(0);
            Assert.That(world.Submit(50, new UnitCommand(attacker.UnitId, world.CurrentTick, (uint)world.CurrentTick + 1, 0, 0, 270, 0, false, true, false, false, false,
                hasAimPoint: true, aimTargetX: target.X, aimTargetY: target.Y + world.ShotHeight, aimTargetZ: target.Z)), Is.True);
            world.Step(); Assert.That(world.GetUnit(0).IsAlive, Is.False); return world;
        }
        [TestCase(true)] [TestCase(false)]
        public void ExplicitBaseAndOwnedRelayChoicesDoNotOverrideEachOther(bool relay)
        {
            var world = CapturedWorld();
            Assert.That(world.RequestSpawn(0, 1, relay ? 0 : -1, relay ? -1 : 2), Is.True);
            FieldFixture.Wait(world, 4); var u = world.GetUnit(0); Assert.That(u.IsAlive, Is.True);
            float dx = u.X - (relay ? 0 : -30), dz = u.Z - (relay ? 0 : 30);
            Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.LessThan(relay ? 3 : 0.02f), "Spawn scatter stays inside the selected location.");
        }
        [Test]
        public void QueuedRelayLostDuringTheDelayDoesNotSilentlySpawnAtAnotherLocation()
        {
            var world = CapturedWorld(2);
            Assert.That(world.RequestSpawn(0, 1, 0), Is.True);
            FieldFixture.MoveTo(world, 50, 0, 0); FieldFixture.Wait(world, 12);
            Assert.That(world.CanSpawnAtCapture(0, 0), Is.False);
            while (world.CurrentTick <= world.GetRespawnReadyTick(0) + 2) world.Step();
            Assert.That(world.GetUnit(0).IsAlive, Is.False); Assert.That(world.RequestSpawn(0, 1, 0), Is.False);
            Assert.That(world.RequestSpawn(0, 1, mainBaseIndex: 1), Is.True); world.Step();
            Assert.That(world.GetUnit(0).IsAlive, Is.True); Assert.That(world.GetUnit(0).X, Is.EqualTo(-30).Within(0.02));
        }
    }
}
