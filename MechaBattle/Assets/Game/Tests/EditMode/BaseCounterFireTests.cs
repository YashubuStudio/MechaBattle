using System;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
using NUnit.Framework;

namespace MechaBattle.Tests
{
    // User request 2026-10-15: main bases should fire back - slow rate, damage around
    // 70% of a unit's max effective HP, as an area attack. NOT the per-unit melee
    // auto-counter AGENTS.md prohibits (that is about mobile units reflexively
    // retaliating/guarding when hit); this is a static structure's own weapon on a
    // cooldown, independent of whether the base itself has been attacked.
    public sealed class BaseCounterFireTests
    {
        private static void MoveNear(BattleSimulation world, int slot, float x, float z, int maxTicks)
        {
            bool done = false;
            for (int tick = 0; tick < maxTicks && !done; tick++)
            {
                done = SubmitStepTowards(world, slot, x, z, tick);
                world.Step();
            }
            Assert.That(done, Is.True, "Unit did not reach the target position.");
        }

        // Advances both slots toward (x, z) together, tick by tick, so neither sits
        // exposed in the base's radius alone while the other is still travelling.
        private static void MoveBothNear(BattleSimulation world, int slotA, int slotB, float x, float z, int maxTicks)
        {
            bool aDone = false, bDone = false;
            for (int tick = 0; tick < maxTicks && !(aDone && bDone); tick++)
            {
                if (!aDone) aDone = SubmitStepTowards(world, slotA, x, z, tick);
                if (!bDone) bDone = SubmitStepTowards(world, slotB, x, z, tick);
                world.Step();
            }
            Assert.That(aDone && bDone, Is.True, "Both units must reach the target position.");
        }

        // Returns true once within 1m of the target (and submits a hold-still command
        // from then on so it doesn't drift away while waiting for the other unit).
        private static bool SubmitStepTowards(BattleSimulation world, int slot, float x, float z, int tick)
        {
            var u = world.GetUnit(slot);
            double dx = x - u.X, dz = z - u.Z;
            double length = Math.Sqrt(dx * dx + dz * dz);
            bool arrived = length < 1;
            double step = 10 * world.FixedDeltaTime;
            float moveX = arrived ? 0f : (float)(dx / Math.Max(length, step));
            float moveY = arrived ? 0f : (float)(dz / Math.Max(length, step));
            var command = new UnitCommand(u.UnitId, world.CurrentTick, unchecked((uint)tick + 1),
                moveX, moveY, 0, 0, false, false, false, false, false);
            Assert.That(world.Submit(slot, command), Is.True);
            return arrived;
        }

        private static void HoldStill(BattleSimulation world, int slot, int ticks)
        {
            for (int tick = 0; tick < ticks; tick++)
            {
                var u = world.GetUnit(slot);
                var holdCommand = new UnitCommand(u.UnitId, world.CurrentTick, unchecked((uint)(tick + 100000)),
                    0, 0, 0, 0, false, false, false, false, false);
                Assert.That(world.Submit(slot, holdCommand), Is.True);
                world.Step();
            }
        }

        [Test]
        public void DisabledByDefault_StationaryEnemyNearBaseTakesNoDamage()
        {
            var config = SimulationTests.Config(); // baseCounterFire* left at 0 = disabled
            var world = new BattleSimulation(SimulationTests.Map(), config);
            var blueBase = world.GetBase(0);
            int enemySlot = 50; // first Orange slot
            MoveNear(world, enemySlot, blueBase.X, blueBase.Z, 500);
            float healthBefore = world.GetUnit(enemySlot).Health;
            HoldStill(world, enemySlot, 300);
            Assert.That(world.GetUnit(enemySlot).Health, Is.EqualTo(healthBefore));
            Assert.That(world.Counters.BaseCounterShotsFired, Is.Zero);
        }

        [Test]
        public void Enabled_DamagesEveryEnemyInRadiusOnASlowCooldown()
        {
            var config = SimulationTests.Config();
            config.baseCounterFireDamage = 50;
            config.baseCounterFireIntervalSeconds = 1f;
            config.baseCounterFireRadius = 10f;
            // High enough that the handful of volleys this test can receive never kill
            // and respawn the target - a respawn would heal it back to full and erase
            // the very damage this test is trying to observe.
            config.maxHealth = 10000f;
            var world = new BattleSimulation(SimulationTests.Map(), config);
            var blueBase = world.GetBase(0);

            int firstEnemy = 50, secondEnemy = 51;
            float startingTotal = world.GetUnit(firstEnemy).Health + world.GetUnit(firstEnemy).Shield;

            MoveBothNear(world, firstEnemy, secondEnemy, blueBase.X, blueBase.Z, 500);
            // Comfortably longer than one full interval so at least one volley lands
            // regardless of exactly when the cooldown last reset during the approach.
            HoldStill(world, firstEnemy, 150);

            float firstAfter = world.GetUnit(firstEnemy).Health + world.GetUnit(firstEnemy).Shield;
            float secondAfter = world.GetUnit(secondEnemy).Health + world.GetUnit(secondEnemy).Shield;

            Assert.That(startingTotal - firstAfter, Is.GreaterThan(0), "Area attack must damage the first nearby enemy.");
            Assert.That(startingTotal - secondAfter, Is.GreaterThan(0), "Area attack must damage a second nearby enemy simultaneously.");
            Assert.That(world.Counters.BaseCounterShotsFired, Is.GreaterThan(0));
        }

        [Test]
        public void Enabled_WaitsForATargetInsteadOfFiringOnAFixedClock()
        {
            var config = SimulationTests.Config();
            config.baseCounterFireDamage = 50;
            config.baseCounterFireIntervalSeconds = 1f;
            config.baseCounterFireRadius = 10f;
            var world = new BattleSimulation(SimulationTests.Map(), config);
            for (int i = 0; i < 200; i++) world.Step(); // nobody near any base yet
            Assert.That(world.Counters.BaseCounterShotsFired, Is.Zero,
                "An idle base with no target in range must not consume its cooldown.");
        }
    }
}
