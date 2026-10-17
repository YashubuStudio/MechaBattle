using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Contracts.Construction;
using MechaBattle.Construction;
using MechaBattle.Simulation;

namespace MechaBattle.Tests
{
    public sealed class PlayableRulesTests
    {
        private static UnitLoadout Loadout(string id = "custom", float health = 100, float speed = 10, int ammo = 4, float range = 65, int cost = 120, float cooldown = 10)
        { return new UnitLoadout(id, health, 0, speed, ammo, 60, range, 0.1f, cost, cooldown); }
        private static UnitLoadout[][] Decks()
        {
            var result = new UnitLoadout[100][];
            for (int i = 0; i < 100; i++) result[i] = new[] { Loadout(), Loadout("reserve", cooldown: 12) };
            return result;
        }
        [TestCase(0, 0)]
        [TestCase(0, 1)]
        [TestCase(50, 0)]
        [TestCase(50, 1)]
        public void Playable_SelectedInitialDeckSpawnsWithoutChangingBotDecksOrSpendingResources(int humanSlot, int selected)
        {
            var config = SimulationTests.Config(); var decks = Decks();
            decks[humanSlot][1] = Loadout("selected-reserve", health: 275, ammo: 8, cost: 250, cooldown: 25);
            var world = new BattleSimulation(SimulationTests.Map(), config, humanSlot, decks, selected);
            var human = world.GetUnit(humanSlot);
            Assert.That(human.DeckIndex, Is.EqualTo(selected));
            Assert.That(human.Health, Is.EqualTo(decks[humanSlot][selected].MaxHealth));
            Assert.That(human.Ammo, Is.EqualTo(decks[humanSlot][selected].MaxAmmo));
            Assert.That(world.GetDeckReadyTick(humanSlot, selected), Is.Zero);
            Assert.That(world.GetGauge(0), Is.EqualTo(config.initialGauge));
            Assert.That(world.GetGauge(1), Is.EqualTo(config.initialGauge));
            Assert.That(world.Counters.UnitsDestroyed, Is.Zero); Assert.That(world.Counters.UnitsSpawned, Is.EqualTo(100));
            for (int i = 0; i < 100; i++) if (i != humanSlot) Assert.That(world.GetUnit(i).DeckIndex, Is.Zero);
        }
        [TestCase(-1)]
        [TestCase(2)]
        public void Playable_RejectsInvalidInitialDeck(int selected)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleSimulation(SimulationTests.Map(), SimulationTests.Config(), 0, Decks(), selected));
        }
        [Test]
        public void Playable_BotOnlyMatchKeepsDefaultStartAndRejectsAHumanDeckOverride()
        {
            var world = new BattleSimulation(SimulationTests.Map(), SimulationTests.Config(), -1, Decks());
            for (int i = 0; i < 100; i++) { Assert.That(world.GetUnit(i).Controller, Is.EqualTo(ControllerType.Bot)); Assert.That(world.GetUnit(i).DeckIndex, Is.Zero); }
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleSimulation(SimulationTests.Map(), SimulationTests.Config(), -1, Decks(), 1));
        }
        [Test]
        public void Playable_PerSlotPerformanceAndInputArraysCannotChangeAnActiveLoadout()
        {
            var decks = Decks(); decks[0][0] = Loadout("heavy", health: 200, speed: 5, ammo: 8, cost: 250, cooldown: 25);
            var world = new BattleSimulation(SimulationTests.Map(), SimulationTests.Config(), 0, decks);
            decks[0][0] = Loadout("replaced");
            Assert.That(world.GetUnit(0).Health, Is.EqualTo(200)); Assert.That(world.GetUnit(0).Ammo, Is.EqualTo(8));
            Assert.That(world.GetLoadout(0, 0).BuildId, Is.EqualTo("heavy"));
            float x0 = world.GetUnit(0).X, x1 = world.GetUnit(1).X;
            SimulationTests.Command(world, 0, 1); SimulationTests.Command(world, 1, 1); world.Step();
            Assert.That(world.GetUnit(1).X - x1, Is.EqualTo(2 * (world.GetUnit(0).X - x0)).Within(0.00001));
            Assert.That(world.AliveCount, Is.EqualTo(100));
        }
        [Test]
        public void Playable_ConstructedCostAndCooldownApplyToHumansAndBots()
        {
            var config = SimulationTests.Config(); config.baseCounterFireDamage = 1000; config.baseCounterFireIntervalSeconds = 100; config.baseCounterFireRadius = 1000;
            var decks = Decks(); decks[0][0] = Loadout("heavy", health: 200, cost: 250, cooldown: 25);
            decks[50][0] = decks[0][0];
            var world = new BattleSimulation(SimulationTests.Map(), config, 0, decks); world.Step();
            Assert.That(world.GetUnit(0).IsAlive, Is.False); Assert.That(world.GetUnit(50).IsAlive, Is.False);
            Assert.That(world.GetDeckReadyTick(0, 0), Is.EqualTo(25 * config.tickHz));
            Assert.That(world.GetDeckReadyTick(50, 0), Is.EqualTo(world.GetDeckReadyTick(0, 0)));
            Assert.That(world.GetGauge(0), Is.EqualTo(config.initialGauge - (250 + 49 * 120) * config.unitGaugeLossPerCost).Within(0.01));
            Assert.That(world.GetGauge(1), Is.EqualTo(world.GetGauge(0)));
        }
        [Test]
        public void Playable_ManualSpawnWaitsAndRejectsCoolingOrHostileChoices()
        {
            var config = SimulationTests.Config(); config.baseCounterFireDamage = 1000; config.baseCounterFireIntervalSeconds = 100; config.baseCounterFireRadius = 1000;
            var world = new BattleSimulation(SimulationTests.Map(), config); world.SetAutomaticRespawn(0, false); world.Step();
            for (int i = 0; i < 10; i++) world.Step();
            Assert.That(world.GetUnit(0).IsAlive, Is.False);
            Assert.That(world.RequestSpawn(0, 0), Is.False, "Destroyed deck is still cooling.");
            Assert.That(world.RequestSpawn(0, 1, 0), Is.False, "Neutral capture cannot be used.");
            Assert.That(world.RequestSpawn(0, 1), Is.True); world.Step();
            Assert.That(world.GetUnit(0).IsAlive, Is.True); Assert.That(world.GetUnit(0).DeckIndex, Is.EqualTo(1));
            Assert.That(world.AliveCount, Is.LessThanOrEqualTo(100));
        }
        [Test]
        public void Playable_BoostIsFiniteAndIdenticalForHumanAndBot()
        {
            var config = SimulationTests.Config(); config.boostCapacity = 10; config.boostDrainPerSecond = 10; config.boostRecoveryPerSecond = 10; config.boostSpeedMultiplier = 2;
            var world = new BattleSimulation(SimulationTests.Map(), config); float x0 = world.GetUnit(0).X, x1 = world.GetUnit(1).X;
            for (int t = 0; t < 50; t++)
            {
                for (int slot = 0; slot < 2; slot++) { var u = world.GetUnit(slot); world.Submit(slot, new UnitCommand(u.UnitId, world.CurrentTick, (uint)t + 1, 1, 0, 90, 0, true, false, false, false, false)); }
                world.Step();
            }
            Assert.That(world.GetUnit(0).X - x0, Is.EqualTo(20).Within(0.001));
            Assert.That(world.GetUnit(1).X - x1, Is.EqualTo(world.GetUnit(0).X - x0).Within(0.001));
            Assert.That(world.GetUnit(0).Boost, Is.EqualTo(0).Within(0.001));
            float before = world.GetUnit(0).X; SimulationTests.Command(world, 0, 1); world.Step();
            Assert.That(world.GetUnit(0).X - before, Is.EqualTo(0.2).Within(0.001));
            Assert.That(world.GetUnit(0).Boost, Is.GreaterThan(0));
        }
        [Test]
        public void Playable_MeleeRequiresAnExplicitCommandAndRespectsItsCooldown()
        {
            var config = SimulationTests.Config(); config.meleeDamage = 20; config.meleeRange = 25; config.meleeIntervalSeconds = 1; config.meleeHalfAngleDegrees = 45;
            var world = new BattleSimulation(SimulationTests.Map(), config); SimulationTests.Approach(world);
            float before = world.GetUnit(50).Health; world.Step(); Assert.That(world.GetUnit(50).Health, Is.EqualTo(before));
            for (int i = 0; i < 2; i++)
            {
                var u = world.GetUnit(0); world.Submit(0, new UnitCommand(u.UnitId, world.CurrentTick, (uint)world.CurrentTick + 1, 0, 0, 90, 0, false, false, false, true, false)); world.Step();
            }
            Assert.That(world.GetUnit(50).Health, Is.EqualTo(before - 20));
            Assert.That(world.GetUnit(0).Health, Is.EqualTo(100), "No automatic enemy counter.");
        }
        [Test]
        public void Playable_CoreRequiresEntryAndDefenseUsesTheSameBuildingRadius()
        {
            var map = SimulationTests.Map(); map.mainBases[3].buildingRadius = 10;
            map.mainBases[3].attackRadius = 2; // The core front face is ahead of overlapping defenders, isolating the entry gate.
            var config = SimulationTests.Config(); config.maxAmmo = 1000; // Keep this core-access test independent of ammo exhaustion during approach.
            config.baseCounterFireDamage = 10; config.baseCounterFireIntervalSeconds = 1; config.baseCounterFireRadius = 1000;
            // All bases use explicit buildings so the legacy huge radius never leaks into this scenario.
            foreach (var b in map.mainBases) b.buildingRadius = 10;
            var world = new BattleSimulation(map, config);
            SimulationTests.Command(world, 0, yaw: 90, fire: true); world.Step();
            Assert.That(world.GetBase(3).DamageReceived, Is.Zero, "Outside the building cannot damage the core.");
            Assert.That(world.GetUnit(0).Health, Is.EqualTo(100), "Defense must not use the legacy 1000m radius.");
            for (int tick = 0; tick < 260; tick++) { SimulationTests.Command(world, 0, 1, yaw: 90, fire: true); world.Step(); }
            Assert.That(world.GetBase(3).DamageReceived, Is.GreaterThan(0));
            Assert.That(world.Counters.BaseCounterShotsFired, Is.GreaterThan(0));
            Assert.That(world.GetBase(3).LastCounterShotTick, Is.GreaterThanOrEqualTo(0));
        }
        [Test]
        public void Playable_InvalidLoadoutOrPartialDeckIsRejectedBeforeBattle()
        {
            Assert.Throws<ArgumentException>(() => Loadout(speed: float.NaN));
            var decks = Decks(); decks[99] = new[] { Loadout() };
            Assert.Throws<ArgumentException>(() => new BattleSimulation(SimulationTests.Map(), SimulationTests.Config(), 0, decks));
        }
    }
}
