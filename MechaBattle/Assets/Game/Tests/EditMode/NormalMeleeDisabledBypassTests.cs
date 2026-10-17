using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;

namespace MechaBattle.Tests
{
    // User request 2026-10-16: a staggered/knocked-down unit may still throw a plain Normal
    // melee swing (only that style). Verifies the bypass actually lands a hit, that other
    // melee styles stay locked out during the same disabled window, and that the default
    // (flag off) preserves the original all-actions-locked behaviour.
    public sealed class NormalMeleeDisabledBypassTests
    {
        private static SimulationConfig Config(bool bypassEnabled)
        {
            var c = SimulationTests.Config();
            c.downPointRecoverySeconds = 10f; c.staggerSeconds = 1.5f; c.knockdownSeconds = 3f; c.knockdownThresholdMultiplier = 1.8f;
            c.boostOverheatEnabled = false; c.seamlessMeleeEnabled = false;
            c.normalMeleeBypassesDisabledEnabled = bypassEnabled;
            c.meleeAttacks = new MeleeAttackDefinition[5];
            for (int i = 0; i < 5; i++) c.meleeAttacks[i] = new MeleeAttackDefinition
            {
                style = (MeleeStyle)i, damage = i == 0 ? 500 : i == 2 ? 2000 : 400, downPoints = 900, range = 3.8f, halfAngleDegrees = 65,
                windupSeconds = i == 0 ? 0.06f : 0.12f, recoverySeconds = i == 0 ? 0.14f : 0.22f, cooldownSeconds = i == 0 ? 0.15f : 0.2f,
                lungeMeters = i == 2 ? 1 : 0.2f, boostCost = 0, moveMultiplier = 0.35f, comboSteps = i == 1 || i == 3 ? 2 : 1, maximumTargets = 3,
                minimumChargeSeconds = i == 4 ? 0.4f : 0, fullChargeSeconds = i == 4 ? 1.4f : 0, minimumChargeMultiplier = i == 4 ? 0.3f : 0
            };
            return c;
        }
        private static UnitLoadout Blade() => new UnitLoadout("melee-bypass", 10000, 0, 10, 1, 500, 3.8f, 0.35f, 120, 10, kind: WeaponKind.Melee);
        private static UnitLoadout Stunner() => new UnitLoadout("stun-gun", 10000, 0, 10, 50, 10, 60, 0.1f, 120, 10, fireDownPoints: 600);

        private static BattleSimulation World(SimulationConfig c)
        {
            var decks = new UnitLoadout[100][];
            decks[0] = new[] { Blade(), Blade() };
            decks[50] = new[] { Stunner(), Stunner() };
            for (int i = 1; i < 100; i++) decks[i] = i < 50 ? new[] { Blade(), Blade() } : new[] { Stunner(), Stunner() };
            var w = new BattleSimulation(SimulationTests.Map(), c, 0, decks);
            MoveTo(w, 0, 0, -1.5f); MoveTo(w, 50, 0, 1.5f); return w;
        }
        private static void MoveTo(BattleSimulation w, int slot, float x, float z)
        {
            for (int t = 0; t < 500; t++)
            {
                var u = w.GetUnit(slot); float dx = x - u.X, dz = z - u.Z; float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                if (distance < 0.01f) return;
                float scale = Math.Max(distance, 10 * w.FixedDeltaTime);
                Submit(w, slot, dx / scale, dz / scale, 0, false, false); w.Step();
            }
            Assert.Fail("Fixture did not reach position.");
        }
        private static void Submit(BattleSimulation w, int slot, float x, float z, float yaw, bool fire, bool melee, MeleeStyle style = MeleeStyle.Normal)
        {
            var u = w.GetUnit(slot);
            Assert.That(w.Submit(slot, new UnitCommand(u.UnitId, w.CurrentTick, (uint)w.CurrentTick + 1, x, z, yaw, 0, false, fire, false, melee, false, meleeStyle: style)), Is.True);
        }
        // Stagger slot 0 by having slot 50 fire a single high-DP shot at it.
        private static void StaggerAttacker(BattleSimulation w)
        {
            Submit(w, 50, 0, 0, 180, true, false); w.Step();
            Assert.That(w.GetUnit(0).Disruption, Is.EqualTo(DisruptionState.Staggered), "Setup failed: attacker is not staggered.");
        }

        [Test] public void NormalMeleeLandsWhileStaggeredWhenBypassEnabled()
        {
            var w = World(Config(bypassEnabled: true)); StaggerAttacker(w);
            float before = w.GetUnit(50).Health;
            Submit(w, 0, 0, 0, 0, false, true, MeleeStyle.Normal); w.Step();
            for (int i = 0; i < 10 && w.GetUnit(50).Health == before; i++) { Submit(w, 0, 0, 0, 0, false, false); w.Step(); }
            Assert.That(w.GetUnit(50).Health, Is.LessThan(before), "Normal melee should land through the bypass while staggered.");
        }

        [Test] public void DashMeleeStaysLockedOutWhileStaggeredEvenWithBypassEnabled()
        {
            var w = World(Config(bypassEnabled: true)); StaggerAttacker(w);
            float before = w.GetUnit(50).Health;
            for (int i = 0; i < 20; i++) { Submit(w, 0, 0, 0, 0, false, true, MeleeStyle.Dash); w.Step(); }
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(before), "Only Normal is exempted; Dash must stay locked out while staggered.");
        }

        [Test] public void NormalMeleeStaysLockedOutWhileStaggeredWhenBypassDisabled()
        {
            var w = World(Config(bypassEnabled: false)); StaggerAttacker(w);
            float before = w.GetUnit(50).Health;
            for (int i = 0; i < 20; i++) { Submit(w, 0, 0, 0, 0, false, true, MeleeStyle.Normal); w.Step(); }
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(before), "Default (flag off) must preserve the original all-actions-locked behaviour.");
        }

        [Test] public void BypassFlagRequiresAllFiveMeleeStylesConfigured()
        {
            var c = Config(bypassEnabled: true); c.meleeAttacks = null;
            Assert.Throws<ArgumentException>(() => c.Validate());
        }
    }
}
