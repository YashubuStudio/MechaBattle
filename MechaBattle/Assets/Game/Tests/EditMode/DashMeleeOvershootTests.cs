using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;

namespace MechaBattle.Tests
{
    // User report 2026-10-16 (task #38): "D格が当たらない". Root cause found by code trace,
    // not by replaying real logs: StartMelee freezes the swing direction (u.MeleeYaw) at
    // windup start, then AdvanceMeleeLunge spends the full lungeMeters distance within the
    // windup window. For MeleeStyle.Dash (lungeMeters=5m in the real catalog), a target that
    // was already within the attack's own range (e.g. 3m away) gets overshot - by the time
    // Melee() resolves, the attacker has dashed past the target, who now sits behind the
    // attacker's final position and fails the forward cone check. This reproduces with a
    // completely stationary target, so it is a deterministic logic bug, not timing/movement
    // sensitivity.
    public sealed class DashMeleeOvershootTests
    {
        private static SimulationConfig Config()
        {
            var c = SimulationTests.Config(); c.boostOverheatEnabled = false; c.seamlessMeleeEnabled = true;
            c.meleeAttacks = new MeleeAttackDefinition[5];
            for (int i = 0; i < 5; i++) c.meleeAttacks[i] = new MeleeAttackDefinition
            {
                style = (MeleeStyle)i, damage = 999, downPoints = 999, range = 3, halfAngleDegrees = 90,
                windupSeconds = 0.06f, recoverySeconds = 0.1f, cooldownSeconds = 0.1f, moveMultiplier = 0.5f,
                comboSteps = 1, maximumTargets = 3,
                minimumChargeSeconds = i == 4 ? 0.4f : 0, fullChargeSeconds = i == 4 ? 1.4f : 0, minimumChargeMultiplier = i == 4 ? 0.3f : 0
            };
            // Real catalog Dash values (task #39 widened halfAngle 50->60): a 5m lunge covering
            // a target that starts only 3m away is exactly the overshoot scenario.
            c.meleeAttacks[2] = new MeleeAttackDefinition
            {
                style = MeleeStyle.Dash, damage = 2280, downPoints = 1400, range = 3.8f, halfAngleDegrees = 60,
                windupSeconds = 0.12f, recoverySeconds = 0.16f, cooldownSeconds = 0.2f, moveMultiplier = 0.35f,
                comboSteps = 1, maximumTargets = 3, lungeMeters = 5
            };
            return c;
        }
        private static UnitLoadout Blade() =>
            new UnitLoadout("dash-test", 10000, 0, 10, 1, 500, 3.8f, 0.35f, 120, 10, kind: WeaponKind.Melee);
        private static BattleSimulation World(float targetZ)
        {
            var blade = Blade();
            var decks = new UnitLoadout[100][]; for (int i = 0; i < 100; i++) decks[i] = new[] { blade, blade };
            var w = new BattleSimulation(SimulationTests.Map(), Config(), 0, decks);
            MoveTo(w, 0, 0, 0); MoveTo(w, 50, 0, targetZ); return w;
        }
        private static void MoveTo(BattleSimulation w, int slot, float x, float z)
        {
            for (int i = 0; i < 300; i++)
            {
                var u = w.GetUnit(slot); float dx = x - u.X, dz = z - u.Z; float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d < 0.02f) return; float scale = Math.Max(d, 10 * w.FixedDeltaTime);
                Submit(w, slot, dx / scale, dz / scale, 0, false); w.Step();
            }
            Assert.Fail("Fixture did not reach position.");
        }
        private static void Submit(BattleSimulation w, int slot, float x, float z, float yaw, bool melee, MeleeStyle style = MeleeStyle.Dash)
        {
            var u = w.GetUnit(slot);
            Assert.That(w.Submit(slot, new UnitCommand(u.UnitId, w.CurrentTick, (uint)w.CurrentTick + 1, x, z, yaw, 0, false, false, false, melee, false, meleeStyle: style)), Is.True);
        }
        private static void DashOnce(BattleSimulation w)
        {
            Submit(w, 0, 0, 0, 0, true); w.Step();
            for (int i = 0; i < 20; i++) { Submit(w, 0, 0, 0, 0, false); w.Step(); }
        }
        [Test] public void DashConnectsWithAStationaryTargetWellInsideTheOriginalSwingRange()
        {
            // Target sits 3m ahead - inside both the 3.8m swing range and the 60 degree cone,
            // measured from the attacker's starting position, before any lunge happens.
            var w = World(targetZ: 3.0f);
            DashOnce(w);
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000 - 2280).Within(0.01),
                "A target well inside the original swing range must not be overshot by the dash's own lunge.");
        }
        [Test] public void DashStillMissesATargetTrulyOutOfRangeAndOutOfTheLungeCorridor()
        {
            // Target sits far to the side, never on the dash's forward axis and well beyond
            // range from both the origin and the final position - this must stay a miss.
            var w = World(targetZ: 0.1f);
            MoveTo(w, 50, 20f, 0.1f);
            DashOnce(w);
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000), "A target far outside the swing corridor must still be missed.");
        }
    }
}
