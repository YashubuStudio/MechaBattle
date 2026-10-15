using System;
using System.Linq;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
using NUnit.Framework;

namespace MechaBattle.Tests
{
    public sealed class SimulationTests
    {
        public static SimulationConfig Config()
        {
            return new SimulationConfig
            {
                tickHz = 50, maxDurationSeconds = 1000, initialGauge = 100000,
                maxHealth = 100, maxShield = 0, maxAmmo = 4, moveSpeed = 10,
                unitRadius = 0.2f, fireDamage = 60, fireRange = 65, fireIntervalSeconds = 0.1f,
                redLockRange = 50, botToHumanRedLockMultiplier = 0.8f,
                deckCosts = new[] { 120, 240 }, deckCooldownSeconds = new[] { 10f, 12f },
                respawnDelaySeconds = 0.04f, unitGaugeLossPerCost = 0.1f, baseGaugeDamageFactor = 2,
                captureSecondsPerAdvantage = 1, capturePresenceCap = 8, homeSupplyRadius = 0.5f,
                homeHealthPerSecond = 30, homeShieldPerSecond = 40, homeAmmoPerSecond = 15,
                spawnScatterFraction = 0.6f
            };
        }
        public static MapDefinition Map()
        {
            var map = FoundationTests.Map();
            for (int i = 0; i < map.mainBases.Length; i++)
            {
                var b = map.mainBases[i]; b.x = b.team == 0 ? -30 : 30;
                b.z = i % 3 * 30 - 30; b.spawnRadius = 0.01f; b.attackRadius = 0.001f;
            }
            map.capturePoints = new[] { new CapturePointDefinition { id = "Relay", x = 0, z = 0, radius = 3 } };
            return map;
        }
        public static bool Command(BattleSimulation world, int slot, float x = 0, float z = 0, float yaw = 0, bool fire = false)
        {
            var u = world.GetUnit(slot);
            return world.Submit(slot, new UnitCommand(u.UnitId, world.CurrentTick, unchecked((uint)world.CurrentTick + 1), x, z, yaw, 0, false, fire, false, false, false));
        }
        public static void MoveTeam(BattleSimulation world, int team, float x, float z, int ticks)
        {
            for (int tick = 0; tick < ticks; tick++)
            {
                for (int slot = team * 50; slot < (team + 1) * 50; slot++) Command(world, slot, x, z);
                world.Step();
            }
        }
        public static void Approach(BattleSimulation world)
        {
            for (int tick = 0; tick < 100; tick++)
            { for (int slot = 0; slot < 100; slot++) Command(world, slot, slot < 50 ? 1 : -1); world.Step(); }
        }
        private static float Yaw(UnitSnapshot from, float x, float z)
        { return (float)(Math.Atan2(x - from.X, z - from.Z) * 180 / Math.PI); }
        private static void MoveTo(BattleSimulation world, int slot, float x, float z)
        {
            for (int tick = 0; tick < 500; tick++)
            {
                var u = world.GetUnit(slot); double dx = x - u.X, dz = z - u.Z;
                double length = Math.Sqrt(dx * dx + dz * dz);
                if (length < 0.05) return;
                double step = 10 * world.FixedDeltaTime;
                Command(world, slot, (float)(dx / Math.Max(length, step)), (float)(dz / Math.Max(length, step))); world.Step();
            }
            Assert.Fail("MoveTo did not reach target.");
        }
        private static int KillOne(BattleSimulation world, int attackerSlot)
        {
            for (int tick = 0; tick < 50; tick++)
            {
                var attacker = world.GetUnit(attackerSlot);
                var target = Enumerable.Range(0, 100).Select(world.GetUnit).Where(u => u.IsAlive && u.Team != attacker.Team)
                    .OrderBy(u => (u.X - attacker.X) * (u.X - attacker.X) + (u.Z - attacker.Z) * (u.Z - attacker.Z)).First();
                Command(world, attackerSlot, yaw: Yaw(attacker, target.X, target.Z), fire: true); world.Step();
                for (int i = 0; i < 100; i++) if (!world.GetUnit(i).IsAlive) return i;
            }
            Assert.Fail("No unit destroyed."); return -1;
        }

        [Test]
        public void T001_InitialWorldHas100UniqueAliveUnitsAndSixIndependentBases()
        {
            var world = new BattleSimulation(Map(), Config());
            Assert.That(world.AliveCount, Is.EqualTo(100));
            Assert.That(Enumerable.Range(0, 100).Select(i => world.GetUnit(i).UnitId).Distinct().Count(), Is.EqualTo(100));
            Assert.That(Enumerable.Range(0, 100).Count(i => world.GetUnit(i).Controller == ControllerType.Human), Is.EqualTo(1));
            Assert.That(world.MainBaseCount, Is.EqualTo(6));
        }

        [Test]
        public void T014_CommandValidationRejectsForeignUnitStaleTickReplayAndNonFiniteInput()
        {
            var w = new BattleSimulation(Map(), Config()); var u = w.GetUnit(0);
            Assert.That(w.Submit(1, new UnitCommand(u.UnitId, 0, 1, 0, 0, 0, 0, false, false, false, false, false)), Is.False);
            Assert.That(w.Submit(0, new UnitCommand(u.UnitId, 1, 1, 0, 0, 0, 0, false, false, false, false, false)), Is.False);
            Assert.That(Command(w, 0, float.NaN), Is.False);
            Assert.That(Command(w, 0, 2), Is.False);
            Assert.That(Command(w, 0, 1), Is.True);
            Assert.That(Command(w, 0, 1), Is.False);
            w.Step(); var moved = w.GetUnit(0);
            Assert.That(moved.X - u.X, Is.EqualTo(0.2f).Within(0.0001));
            w.Step(); Assert.That(w.GetUnit(0).X, Is.EqualTo(moved.X));
            Assert.That(w.Submit(0, new UnitCommand(u.UnitId, 0, 50, 0, 0, 0, 0, false, false, false, false, false)), Is.False);
        }

        [Test]
        public void T014_DiagonalMovementDoesNotExceedSpeedAndMapBounds()
        {
            var w = new BattleSimulation(Map(), Config()); var before = w.GetUnit(0);
            Command(w, 0, 1, 1); w.Step(); var after = w.GetUnit(0);
            double distance = Math.Sqrt(Math.Pow(after.X - before.X, 2) + Math.Pow(after.Z - before.Z, 2));
            Assert.That(distance, Is.EqualTo(0.2).Within(0.0001));
            for (int i = 0; i < 1000; i++) { Command(w, 0, 1, 1); w.Step(); }
            Assert.That(w.GetUnit(0).X, Is.LessThanOrEqualTo(99.8f));
            Assert.That(w.GetUnit(0).Z, Is.LessThanOrEqualTo(99.8f));
        }

        [Test]
        public void T014_AmmoIsFiniteAndRestoredOnlyAtHomeSupply()
        {
            var w = new BattleSimulation(Map(), Config()); Approach(w);
            for (int i = 0; i < 100; i++) { Command(w, 0, yaw: 180, fire: true); w.Step(); }
            Assert.That(w.GetUnit(0).Ammo, Is.Zero); Assert.That(w.Counters.ShotsFired, Is.EqualTo(4));
            for (int i = 0; i < 100; i++) w.Step();
            Assert.That(w.GetUnit(0).Ammo, Is.Zero);
            MoveTeam(w, 0, -1, 0, 100);
            for (int i = 0; i < 20; i++) w.Step();
            Assert.That(w.GetUnit(0).Ammo, Is.GreaterThan(0).And.LessThanOrEqualTo(4));
        }

        [Test]
        public void T008_OnlyBotToHumanHasReducedRedLockRangeAndNormalFireStillWorksOutsideIt()
        {
            var c = Config(); c.redLockRange = 5;
            var w = new BattleSimulation(Map(), c, 50); Approach(w);
            Assert.That(w.GetRedLockRange(0, 50), Is.EqualTo(4));
            Assert.That(w.GetRedLockRange(50, 0), Is.EqualTo(5));
            Assert.That(w.GetRedLockRange(1, 51), Is.EqualTo(5));
            Assert.That(w.IsRedLocked(0, 50), Is.False);
            var a = w.GetUnit(0); var target = w.GetUnit(50);
            Command(w, 0, yaw: Yaw(a, target.X, target.Z), fire: true); w.Step();
            Assert.That(Enumerable.Range(50, 50).Any(i => w.GetUnit(i).Health < 100), Is.True);
        }

        [Test]
        public void T009_ShieldAbsorbsDamageAndTargetDoesNotAutomaticallyCounter()
        {
            var c = Config(); c.maxShield = 100;
            var w = new BattleSimulation(Map(), c); Approach(w);
            var a = w.GetUnit(0); var target = w.GetUnit(50);
            Command(w, 0, yaw: Yaw(a, target.X, target.Z), fire: true); w.Step();
            var damaged = Enumerable.Range(50, 50).Select(w.GetUnit).Single(u => u.Shield < 100);
            Assert.That(damaged.Shield, Is.EqualTo(40)); Assert.That(damaged.Health, Is.EqualTo(100));
            for (int i = 0; i < 20; i++) w.Step();
            Assert.That(w.Counters.ShotsFired, Is.EqualTo(1));
        }

        [Test]
        public void T014_SimultaneousLethalShotsResolveForBothTeams()
        {
            var c = Config(); c.fireDamage = 1000;
            var w = new BattleSimulation(Map(), c); Approach(w);
            for (int slot = 0; slot < 100; slot++)
            {
                var a = w.GetUnit(slot); var target = w.GetUnit(slot < 50 ? slot + 50 : slot - 50);
                Command(w, slot, yaw: Yaw(a, target.X, target.Z), fire: true);
            }
            w.Step();
            Assert.That(w.Counters.ShotsFired, Is.EqualTo(100));
            Assert.That(Enumerable.Range(0, 50).Any(i => !w.GetUnit(i).IsAlive), Is.True);
            Assert.That(Enumerable.Range(50, 50).Any(i => !w.GetUnit(i).IsAlive), Is.True);
        }

        [Test]
        public void T002_DeathRotatesDeckAndWaitsWhenAllEntriesAreCoolingDown()
        {
            var c = Config(); c.fireDamage = 1000;
            var w = new BattleSimulation(Map(), c); Approach(w);
            int victim = KillOne(w, 0); var dead = w.GetUnit(victim);
            Assert.That(dead.Health, Is.Zero); Assert.That(dead.IsAlive, Is.False);
            long ready = w.GetDeckReadyTick(victim, 0);
            Assert.That(ready, Is.EqualTo(w.CurrentTick - 1 + 500));
            Assert.That(Command(w, victim, 1), Is.False);
            w.Step(); w.Step();
            var second = w.GetUnit(victim);
            Assert.That(second.IsAlive, Is.True); Assert.That(second.DeckIndex, Is.EqualTo(1));
            Assert.That(second.UnitId, Is.Not.EqualTo(dead.UnitId));
            Assert.That(w.Submit(victim, new UnitCommand(dead.UnitId, w.CurrentTick, 999, 1, 0, 0, 0, false, false, false, false, false)), Is.False);
            MoveTo(w, victim, 0, w.GetUnit(0).Z + 10);
            var a = w.GetUnit(0); var v = w.GetUnit(victim);
            Command(w, 0, yaw: Yaw(a, v.X, v.Z), fire: true); w.Step();
            Assert.That(w.GetUnit(victim).IsAlive, Is.False);
            while (w.CurrentTick < ready) { w.Step(); Assert.That(w.GetUnit(victim).IsAlive, Is.False); }
            w.Step(); Assert.That(w.GetUnit(victim).IsAlive, Is.True);
            Assert.That(w.GetUnit(victim).DeckIndex, Is.EqualTo(0));
        }

        [Test]
        public void T002_HumanAndBotUseIdenticalDeathCooldownAndRespawnRules()
        {
            var c = Config(); c.fireDamage = 1000;
            var botWorld = new BattleSimulation(Map(), c, -1); Approach(botWorld);
            int victim = KillOne(botWorld, 0);
            var humanWorld = new BattleSimulation(Map(), c, victim); Approach(humanWorld);
            int humanVictim = KillOne(humanWorld, 0);
            Assert.That(humanVictim, Is.EqualTo(victim));
            Assert.That(humanWorld.GetDeckReadyTick(victim, 0), Is.EqualTo(botWorld.GetDeckReadyTick(victim, 0)));
            Assert.That(humanWorld.GetGauge(1), Is.EqualTo(botWorld.GetGauge(1)));
            botWorld.Step(); botWorld.Step(); humanWorld.Step(); humanWorld.Step();
            Assert.That(humanWorld.GetUnit(victim).DeckIndex, Is.EqualTo(botWorld.GetUnit(victim).DeckIndex));
            Assert.That(humanWorld.GetUnit(victim).Health, Is.EqualTo(botWorld.GetUnit(victim).Health));
        }

        [Test]
        public void T003_CaptureReversesThroughNeutralAndEnemyPresenceDisablesForwardSpawn()
        {
            var m = Map(); m.capturePoints[0].x = -30; m.capturePoints[0].radius = 40;
            var w = new BattleSimulation(m, Config());
            for (int i = 0; i < 10; i++) w.Step();
            Assert.That(w.GetCapturePoint(0).OwnerTeam, Is.EqualTo(0));
            Assert.That(w.CanSpawnAtCapture(0, 0), Is.True);
            MoveTeam(w, 1, -1, 0, 300);
            var tie = w.GetCapturePoint(0);
            Assert.That(tie.BluePresence, Is.EqualTo(50)); Assert.That(tie.OrangePresence, Is.EqualTo(50));
            Assert.That(w.CanSpawnAtCapture(0, 0), Is.False);
            for (int i = 0; i < 30; i++) w.Step();
            Assert.That(w.GetCapturePoint(0).Progress, Is.EqualTo(tie.Progress));
            bool neutral = false;
            for (int i = 0; i < 400; i++)
            {
                for (int slot = 0; slot < 50; slot++) Command(w, slot, 1);
                w.Step(); if (w.GetCapturePoint(0).OwnerTeam == -1) neutral = true;
            }
            Assert.That(neutral, Is.True); Assert.That(w.GetCapturePoint(0).OwnerTeam, Is.EqualTo(1));
            Assert.That(w.CanSpawnAtCapture(1, 0), Is.True);
        }

        [TestCase(0)] [TestCase(1)]
        public void T016_EachMainBaseReceivesIndependentDamageAndReducesItsTeamGauge(int attackingTeam)
        {
            var c = Config(); c.fireIntervalSeconds = 0.02f;
            var w = new BattleSimulation(Map(), c); Approach(w);
            MoveTeam(w, 1 - attackingTeam, 0, 1, 250);
            int slot = attackingTeam * 50;
            MoveTo(w, slot, 0, 0);
            for (int i = 0; i < w.MainBaseCount; i++)
            {
                var b = w.GetBase(i); if (b.Team == attackingTeam) continue;
                double gauge = w.GetGauge(b.Team);
                Command(w, slot, yaw: Yaw(w.GetUnit(slot), b.X, b.Z), fire: true); w.Step();
                Assert.That(w.GetBase(i).DamageReceived, Is.EqualTo(c.fireDamage), b.Id);
                Assert.That(w.GetGauge(b.Team), Is.EqualTo(gauge - c.fireDamage * c.baseGaugeDamageFactor).Within(0.001));
            }
            Assert.That(w.Counters.BaseHits, Is.EqualTo(3));
        }

        [Test]
        public void T004_HigherDeckCostProducesHigherGaugeLoss()
        {
            var c = Config(); c.fireDamage = 1000;
            var w = new BattleSimulation(Map(), c); Approach(w);
            int victim = KillOne(w, 0);
            Assert.That(w.GetGauge(1), Is.EqualTo(c.initialGauge - 12).Within(0.001));
            w.Step(); w.Step(); MoveTo(w, victim, 0, w.GetUnit(0).Z + 10);
            var a = w.GetUnit(0); var v = w.GetUnit(victim);
            Command(w, 0, yaw: Yaw(a, v.X, v.Z), fire: true); w.Step();
            Assert.That(w.GetGauge(1), Is.EqualTo(c.initialGauge - 12 - 24).Within(0.001));
        }

        [Test]
        public void FR104_TimeoutDrawStopsTickAndRejectsFurtherCommands()
        {
            var c = Config(); c.maxDurationSeconds = 0.1f;
            var w = new BattleSimulation(Map(), c);
            for (int i = 0; i < 5; i++) w.Step();
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Finished)); Assert.That(w.WinnerTeam, Is.EqualTo(-1));
            Assert.That(w.CurrentTick, Is.EqualTo(5)); w.Step(); Assert.That(w.CurrentTick, Is.EqualTo(5));
            Assert.That(Command(w, 0, 1), Is.False);
        }

        [Test]
        public void FR101_ZeroGaugeEndsMatchAndClampsGauge()
        {
            var c = Config(); c.initialGauge = 100;
            var w = new BattleSimulation(Map(), c); Approach(w); MoveTeam(w, 1, 0, 1, 250);
            var a = w.GetUnit(0); var b = w.GetBase(3);
            Command(w, 0, yaw: Yaw(a, b.X, b.Z), fire: true); w.Step();
            Assert.That(w.GetGauge(1), Is.Zero); Assert.That(w.WinnerTeam, Is.EqualTo(0));
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Finished));
        }

        [Test]
        public void ConfigAndMapAreCopiedToProtectAuthoritativeState()
        {
            var c = Config(); var m = Map(); var w = new BattleSimulation(m, c);
            c.moveSpeed = 10000; c.deckCosts[0] = 99999; m.mainBases[0].x = 99;
            var before = w.GetUnit(0); Command(w, 0, 1); w.Step();
            Assert.That(w.GetUnit(0).X - before.X, Is.EqualTo(0.2).Within(0.001));
            Assert.That(w.GetBase(0).X, Is.EqualTo(-30));
        }

        [Test]
        public void InvalidConfigIsRejectedBeforeWorldCreation()
        {
            var c = Config(); c.maxHealth = float.NaN;
            Assert.Throws<ArgumentException>(() => new BattleSimulation(Map(), c));
            c = Config(); c.deckCosts = new[] { 1 };
            Assert.Throws<ArgumentException>(() => new BattleSimulation(Map(), c));
        }

        [Test]
        public void FireIntervalDoesNotGainAnExtraTickFromFloatRounding()
        {
            var c = Config(); c.fireIntervalSeconds = 0.4f; c.maxAmmo = 100;
            var w = new BattleSimulation(Map(), c); Approach(w);
            for (int i = 0; i < 100; i++) { Command(w, 0, yaw: 180, fire: true); w.Step(); }
            Assert.That(w.Counters.ShotsFired, Is.EqualTo(5));
        }

        [Test]
        public void T014_SequenceNumbersCanWrapAndRejectOldSerials()
        {
            var w = new BattleSimulation(Map(), Config());
            foreach (uint sequence in new[] { uint.MaxValue - 1, uint.MaxValue, 0u })
            {
                var u = w.GetUnit(0);
                Assert.That(w.Submit(0, new UnitCommand(u.UnitId, w.CurrentTick, sequence, 1, 0, 0, 0, false, false, false, false, false)), Is.True);
                w.Step();
            }
            var current = w.GetUnit(0);
            Assert.That(w.Submit(0, new UnitCommand(current.UnitId, w.CurrentTick, uint.MaxValue, 1, 0, 0, 0, false, false, false, false, false)), Is.False);
        }

        [Test]
        public void IdenticalInputStreamsProduceIdenticalWorldsAcrossDeathsAndRespawns()
        {
            var c = Config(); c.maxAmmo = 50;
            var first = new BattleSimulation(Map(), c, -1);
            var second = new BattleSimulation(Map(), c, -1);
            for (int tick = 0; tick < 600; tick++)
            {
                for (int slot = 0; slot < 100; slot++)
                {
                    var u = first.GetUnit(slot);
                    var cmd = new UnitCommand(u.UnitId, first.CurrentTick, (uint)tick + 1,
                        slot < 50 ? 1 : -1, 0, slot < 50 ? 90 : 270, 0, false, tick > 100, false, false, false);
                    Assert.That(first.Submit(slot, cmd), Is.EqualTo(second.Submit(slot, cmd)));
                }
                first.Step(); second.Step();
            }
            for (int slot = 0; slot < 100; slot++) Assert.That(first.GetUnit(slot), Is.EqualTo(second.GetUnit(slot)));
            Assert.That(first.GetGauge(0), Is.EqualTo(second.GetGauge(0)));
            Assert.That(first.GetGauge(1), Is.EqualTo(second.GetGauge(1)));
            Assert.That(first.Counters, Is.EqualTo(second.Counters));
            Assert.That(first.Counters.UnitsDestroyed, Is.GreaterThan(0));
            Assert.That(first.Counters.UnitsSpawned, Is.GreaterThan(100));
        }

        [Test]
        public void T003_DestroyedAndCooldownUnitsDoNotCountTowardCapturePresence()
        {
            var m = Map(); m.capturePoints[0].radius = 100;
            var c = Config(); c.fireDamage = 1000;
            var w = new BattleSimulation(m, c); Approach(w);
            int victim = KillOne(w, 0);
            Assert.That(w.GetUnit(victim).IsAlive, Is.False);
            Assert.That(w.GetCapturePoint(0).BluePresence, Is.EqualTo(50));
            Assert.That(w.GetCapturePoint(0).OrangePresence, Is.EqualTo(49));
            w.Step(); Assert.That(w.GetCapturePoint(0).OrangePresence, Is.EqualTo(49));
            w.Step(); Assert.That(w.GetCapturePoint(0).OrangePresence, Is.EqualTo(50));
        }
    }
}
