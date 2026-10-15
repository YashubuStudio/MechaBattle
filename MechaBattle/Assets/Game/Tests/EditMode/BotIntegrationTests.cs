using System;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
using MechaBattle.Observation;
using MechaBattle.AI.Bots;
using NUnit.Framework;

namespace MechaBattle.Tests
{
    public sealed class BotIntegrationTests
    {
        private sealed class SelfView : ISelfObservationSource
        {
            private readonly BattleSimulation world;
            public int ExpectedSlot, Queries;
            public SelfView(BattleSimulation world) { this.world = world; }
            public bool TryGetSelf(int slot, out SelfObservation self)
            {
                if (slot != ExpectedSlot) throw new InvalidOperationException("BOT requested another slot's state.");
                Queries++;
                var unit = world.GetUnit(slot);
                self = new SelfObservation(slot, unit.UnitId, unit.Team, unit.Controller, unit.IsAlive, unit.X, unit.Z, unit.Ammo);
                return true;
            }
        }
        private sealed class ManualView : ISelfObservationSource
        {
            public SelfObservation Value;
            public bool TryGetSelf(int slot, out SelfObservation self) { self = Value; return true; }
        }

        [TestCase(ControllerType.Human)] [TestCase(ControllerType.RemoteHuman)]
        public void T011_BotNeverIssuesCommandsForHumanControllers(ControllerType controller)
        {
            var view = new ManualView { Value = new SelfObservation(0, 1, 0, controller, true, -30, 0, 4) };
            var bot = new ForwardMarchBotCommandSource(SimulationTests.Map(), view, 65, 4, 1);
            Assert.That(bot.TryGetCommand(0, 0, out _), Is.False);
        }

        [Test]
        public void T010_BotUsesOnlyRequestedSelfViewAndPublicBaseGeometry()
        {
            var config = SimulationTests.Config(); var map = SimulationTests.Map();
            var world = new BattleSimulation(map, config, -1); var view = new SelfView(world);
            var bot = new ForwardMarchBotCommandSource(map, view, config.fireRange, config.maxAmmo, config.homeSupplyRadius);
            for (int slot = 0; slot < 100; slot++)
            {
                view.ExpectedSlot = slot; int previous = view.Queries;
                Assert.That(bot.TryGetCommand(slot, 0, out var command), Is.True);
                Assert.That(view.Queries - previous, Is.EqualTo(1));
                Assert.That(world.Submit(slot, command), Is.True);
            }
            world.Step(); Assert.That(world.AliveCount, Is.LessThanOrEqualTo(100));
        }

        [Test]
        public void T012_ActualBotCommandsFinishAnUnattended100SlotObjectiveMatch()
        {
            var config = SimulationTests.Config(); config.initialGauge = 1000; config.maxAmmo = 30;
            config.maxDurationSeconds = 60; config.fireRange = 20;
            var map = SimulationTests.Map(); var world = new BattleSimulation(map, config, -1);
            var view = new SelfView(world);
            var bot = new ForwardMarchBotCommandSource(map, view, config.fireRange, config.maxAmmo, config.homeSupplyRadius);
            int peakAlive = world.AliveCount;
            while (world.Phase == MatchPhase.Running)
            {
                for (int slot = 0; slot < 100; slot++)
                {
                    view.ExpectedSlot = slot;
                    if (bot.TryGetCommand(slot, world.CurrentTick, out var command))
                        Assert.That(world.Submit(slot, command), Is.True);
                }
                world.Step(); peakAlive = Math.Max(peakAlive, world.AliveCount);
                Assert.That(world.AliveCount, Is.LessThanOrEqualTo(100));
            }
            Assert.That(peakAlive, Is.EqualTo(100));
            Assert.That(world.Counters.ShotsFired, Is.GreaterThan(0));
            Assert.That(world.Counters.BaseHits, Is.GreaterThan(0));
            Assert.That(Math.Min(world.GetGauge(0), world.GetGauge(1)), Is.Zero);
            Assert.That(world.CurrentTick, Is.LessThan(config.tickHz * config.maxDurationSeconds));
        }

        [Test]
        public void FR306_AmmoDepletionTriggersRetreatAndFullSupplyResumesAttack()
        {
            var view = new ManualView { Value = new SelfObservation(0, 1, 0, ControllerType.Bot, true, 0, -30, 0) };
            var bot = new ForwardMarchBotCommandSource(SimulationTests.Map(), view, 20, 30, 1);
            Assert.That(bot.TryGetCommand(0, 0, out var retreat), Is.True);
            Assert.That(retreat.MoveX, Is.LessThan(0)); Assert.That(retreat.FirePrimary, Is.False);
            view.Value = new SelfObservation(0, 1, 0, ControllerType.Bot, true, -30, -30, 30);
            Assert.That(bot.TryGetCommand(0, 1, out var advance), Is.True);
            Assert.That(advance.MoveX, Is.GreaterThan(0));
        }
    }
}
