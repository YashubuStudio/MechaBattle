using System;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
using MechaBattle.Observation;
using MechaBattle.AI.Bots;
using NUnit.Framework;

namespace MechaBattle.Tests
{
    // Regression for the user-reported bug: two opposing formations marched straight
    // through each other at midfield with no shots fired, because the old BOT only
    // aimed/fired once close to its distant objective (the enemy main base). Proves the
    // fix (INearbyThreatSensor-driven opportunistic engagement) actually causes combat
    // long before anyone could possibly be within fireRange of a base, and that the
    // old no-sensor overload still reproduces the original (base-only) behaviour.
    public sealed class BotEngagementTests
    {
        private sealed class SimulationView : ISelfObservationSource, INearbyThreatSensor
        {
            private readonly BattleSimulation world;
            internal SimulationView(BattleSimulation world) { this.world = world; }

            public bool TryGetSelf(int slotId, out SelfObservation self)
            {
                var u = world.GetUnit(slotId);
                self = new SelfObservation(slotId, u.UnitId, u.Team, u.Controller, u.IsAlive, u.X, u.Z, u.Ammo);
                return true;
            }

            public NearbyEnemy FindNearestEnemy(int slotId, float rangeMeters)
            {
                var self = world.GetUnit(slotId);
                if (!self.IsAlive) return NearbyEnemy.None;
                float bestSquared = rangeMeters * rangeMeters;
                bool found = false; float bestX = 0, bestZ = 0;
                for (int slot = 0; slot < MatchLimits.TotalControllerSlots; slot++)
                {
                    var u = world.GetUnit(slot);
                    if (!u.IsAlive || u.Team == self.Team) continue;
                    float dx = u.X - self.X, dz = u.Z - self.Z;
                    float squared = dx * dx + dz * dz;
                    if (squared <= bestSquared) { bestSquared = squared; found = true; bestX = u.X; bestZ = u.Z; }
                }
                return found ? new NearbyEnemy(true, bestX, bestZ) : NearbyEnemy.None;
            }
        }

        // Production-scale separation (bases 400m apart, lane-matched, like
        // config/standard-map.json) so "close enough to the enemy base" and "close
        // enough to a passing enemy unit" are clearly distinguishable in time.
        private static MapDefinition BigMap()
        {
            return new MapDefinition
            {
                id = "engagement-test", prototypeOnly = true, halfWidth = 240, halfDepth = 140,
                mainBases = new[]
                {
                    new MainBaseDefinition { id = "Blue-A", team = 0, x = -200, z = -80, spawnRadius = 8, attackRadius = 5, approachRoute = "north" },
                    new MainBaseDefinition { id = "Blue-B", team = 0, x = -200, z = 0, spawnRadius = 8, attackRadius = 5, approachRoute = "center" },
                    new MainBaseDefinition { id = "Blue-C", team = 0, x = -200, z = 80, spawnRadius = 8, attackRadius = 5, approachRoute = "south" },
                    new MainBaseDefinition { id = "Orange-A", team = 1, x = 200, z = -80, spawnRadius = 8, attackRadius = 5, approachRoute = "north" },
                    new MainBaseDefinition { id = "Orange-B", team = 1, x = 200, z = 0, spawnRadius = 8, attackRadius = 5, approachRoute = "center" },
                    new MainBaseDefinition { id = "Orange-C", team = 1, x = 200, z = 80, spawnRadius = 8, attackRadius = 5, approachRoute = "south" },
                },
                capturePoints = new[] { new CapturePointDefinition { id = "Relay", x = 0, z = 0, radius = 18 } }
            };
        }

        // moveSpeed=10 (SimulationTests.Config) closes the 400m gap at 20m/s combined,
        // crossing the x=0 midline around tick ~1000 (20s). At tick 1100 each side has
        // only covered ~220m - still ~180m short of the enemy base, far beyond
        // fireRange(65) - so any shot fired by then can only be a midfield engagement.
        private const int MidfieldOnlyTicks = 1100;

        [Test]
        public void WithoutSensor_NoShotsFiredWhileStillFarFromAnyBase()
        {
            var map = BigMap();
            var config = SimulationTests.Config();
            var world = new BattleSimulation(map, config, -1);
            var view = new SimulationView(world);
            var bot = new ForwardMarchBotCommandSource(map, view, config.fireRange, config.maxAmmo, config.homeSupplyRadius);
            RunTicks(world, bot, MidfieldOnlyTicks);
            Assert.That(world.Counters.ShotsFired, Is.Zero,
                "Pre-fix behaviour: the no-sensor overload should still walk through enemies without firing.");
        }

        [Test]
        public void WithSensor_EngagesEnemiesEncounteredBeforeReachingAnyBase()
        {
            var map = BigMap();
            var config = SimulationTests.Config();
            var world = new BattleSimulation(map, config, -1);
            var view = new SimulationView(world);
            var bot = new ForwardMarchBotCommandSource(map, view, view, config.fireRange, config.maxAmmo, config.homeSupplyRadius);
            RunTicks(world, bot, MidfieldOnlyTicks);
            Assert.That(world.Counters.ShotsFired, Is.GreaterThan(0),
                "Fix: opposing units converging on each other must fire while still far from either base.");

            for (int slot = 0; slot < MatchLimits.TotalControllerSlots; slot++)
            {
                var u = world.GetUnit(slot);
                if (!u.IsAlive) continue;
                float nearestBaseDistance = float.PositiveInfinity;
                for (int b = 0; b < world.MainBaseCount; b++)
                {
                    var baseSnapshot = world.GetBase(b);
                    if (baseSnapshot.Team == u.Team) continue;
                    float dx = baseSnapshot.X - u.X, dz = baseSnapshot.Z - u.Z;
                    nearestBaseDistance = Math.Min(nearestBaseDistance, (float)Math.Sqrt(dx * dx + dz * dz));
                }
                Assert.That(nearestBaseDistance, Is.GreaterThan(config.fireRange),
                    "Sanity check: no surviving unit should have reached enemy-base firing range yet at tick " + MidfieldOnlyTicks + ".");
            }
        }

        private sealed class FixedSelfView : ISelfObservationSource
        {
            internal SelfObservation Value;
            public bool TryGetSelf(int slotId, out SelfObservation self) { self = Value; return true; }
        }

        private sealed class NoNearbyEnemies : INearbyThreatSensor
        {
            public NearbyEnemy FindNearestEnemy(int slotId, float rangeMeters) => NearbyEnemy.None;
        }

        // Regression for GPT's review finding on CORE-01: once the BOT started walking
        // inside a declared building radius, its fire decision still used fireRange
        // (65m) instead of that same buildingRadius (20m) - so it would set
        // FirePrimary=true, and burn ammo, for ~45m of approach during which
        // Game.Simulation's own building gate would reject the base as a target and the
        // shot would hit nothing. The BOT's "am I close enough to fire" threshold must
        // match Simulation's "is this shot even valid" threshold exactly.
        [Test]
        public void DoesNotFireAtBaseFromOutsideItsOwnBuildingRadius()
        {
            var map = BigMap();
            map.mainBases[3].buildingRadius = 20f; // Orange-A: lane-matched target for team0 slot 0
            var view = new FixedSelfView
            {
                // 45m from Orange-A (200,-80): inside fireRange(65) but outside buildingRadius(20).
                Value = new SelfObservation(0, 1, 0, ControllerType.Bot, true, 155f, -80f, 30)
            };
            var bot = new ForwardMarchBotCommandSource(map, view, new NoNearbyEnemies(), fireRange: 65f, maxAmmo: 30, homeSupplyRadius: 20f);

            Assert.That(bot.TryGetCommand(0, 0, out var command), Is.True);
            Assert.That(command.FirePrimary, Is.False,
                "35m outside the building radius: Simulation would reject this base as a target, so the BOT must not fire either.");
        }

        [Test]
        public void FiresAtBaseOnceInsideItsOwnBuildingRadius()
        {
            var map = BigMap();
            map.mainBases[3].buildingRadius = 20f;
            var view = new FixedSelfView
            {
                // 10m from Orange-A (200,-80): inside buildingRadius(20).
                Value = new SelfObservation(0, 1, 0, ControllerType.Bot, true, 190f, -80f, 30)
            };
            var bot = new ForwardMarchBotCommandSource(map, view, new NoNearbyEnemies(), fireRange: 65f, maxAmmo: 30, homeSupplyRadius: 20f);

            Assert.That(bot.TryGetCommand(0, 0, out var command), Is.True);
            Assert.That(command.FirePrimary, Is.True,
                "Inside the building radius, the shot is valid in Simulation too, so the BOT should fire.");
        }

        private static void RunTicks(BattleSimulation world, ForwardMarchBotCommandSource bot, int ticks)
        {
            for (int tick = 0; tick < ticks && world.Phase == MatchPhase.Running; tick++)
            {
                for (int slot = 0; slot < MatchLimits.TotalControllerSlots; slot++)
                    if (bot.TryGetCommand(slot, world.CurrentTick, out var command))
                        Assert.That(world.Submit(slot, command), Is.True);
                world.Step();
            }
        }
    }
}
