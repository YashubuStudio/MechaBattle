using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;

namespace MechaBattle.Tests
{
    // User request 2026-10-16: arm-type-specific charge melee. A standard-arm charge attack
    // spends its charge on area (chargeScalesRange); a reinforced-arm one spends it on power
    // through a two-phase curve (thresholdChargeFraction). These are alternate archetypes
    // and mutually exclusive on a single MeleeAttackDefinition.
    public sealed class ChargeMeleeArchetypeTests
    {
        private static MeleeAttackDefinition Base() => new MeleeAttackDefinition
        {
            style = MeleeStyle.Charge, damage = 1000, downPoints = 1000, range = 5, halfAngleDegrees = 90,
            windupSeconds = 0.1f, recoverySeconds = 0.1f, cooldownSeconds = 0.1f, moveMultiplier = 0.5f,
            comboSteps = 1, maximumTargets = 1, minimumChargeSeconds = 0.1f, fullChargeSeconds = 0.5f, minimumChargeMultiplier = 0.5f
        };
        [Test] public void RangeScalingArchetypeAcceptsAValidConfiguration()
        {
            var a = Base(); a.chargeScalesRange = true; a.minimumRange = 2;
            Assert.DoesNotThrow(() => a.Validate());
        }
        [Test] public void TwoPhaseArchetypeAcceptsAValidConfiguration()
        {
            var a = Base(); a.thresholdChargeFraction = 0.5f; a.belowThresholdDamage = 100; a.belowThresholdDownPoints = 100;
            Assert.DoesNotThrow(() => a.Validate());
        }
        [Test] public void BothArchetypesTogetherAreRejected()
        {
            var a = Base(); a.chargeScalesRange = true; a.minimumRange = 2; a.thresholdChargeFraction = 0.5f; a.belowThresholdDamage = 100; a.belowThresholdDownPoints = 100;
            Assert.Throws<ArgumentException>(() => a.Validate());
        }
        [TestCase(MeleeStyle.Normal)] [TestCase(MeleeStyle.Dash)]
        public void RangeScalingRequiresChargeStyle(MeleeStyle style)
        {
            var a = Base(); a.style = style; a.chargeScalesRange = true; a.minimumRange = 2;
            Assert.Throws<ArgumentException>(() => a.Validate());
        }
        [Test] public void RangeScalingRequiresMinimumRangeBelowFullRange()
        {
            var a = Base(); a.chargeScalesRange = true; a.minimumRange = 5; // == range, not <
            Assert.Throws<ArgumentException>(() => a.Validate());
        }
        [Test] public void RangeScalingRejectsZeroMinimumRange()
        {
            var a = Base(); a.chargeScalesRange = true; a.minimumRange = 0;
            Assert.Throws<ArgumentException>(() => a.Validate());
        }
        [TestCase(MeleeStyle.Normal)] [TestCase(MeleeStyle.Dash)]
        public void TwoPhaseRequiresChargeStyle(MeleeStyle style)
        {
            var a = Base(); a.style = style; a.thresholdChargeFraction = 0.5f; a.belowThresholdDamage = 100; a.belowThresholdDownPoints = 100;
            Assert.Throws<ArgumentException>(() => a.Validate());
        }
        [Test] public void TwoPhaseRejectsThresholdAtOrAboveOne()
        {
            var a = Base(); a.thresholdChargeFraction = 1f; a.belowThresholdDamage = 100; a.belowThresholdDownPoints = 100;
            Assert.Throws<ArgumentException>(() => a.Validate());
        }
        [Test] public void TwoPhaseRejectsBelowThresholdDamageAtOrAboveFullDamage()
        {
            var a = Base(); a.thresholdChargeFraction = 0.5f; a.belowThresholdDamage = 1000; a.belowThresholdDownPoints = 100; // == damage, not <
            Assert.Throws<ArgumentException>(() => a.Validate());
        }
        [Test] public void TwoPhaseRejectsBelowThresholdDownPointsAtOrAboveFullDownPoints()
        {
            var a = Base(); a.thresholdChargeFraction = 0.5f; a.belowThresholdDamage = 100; a.belowThresholdDownPoints = 1000; // == downPoints, not <
            Assert.Throws<ArgumentException>(() => a.Validate());
        }
        [Test] public void TwoPhaseRejectsNonPositiveBelowThresholdValues()
        {
            var a = Base(); a.thresholdChargeFraction = 0.5f; a.belowThresholdDamage = 0; a.belowThresholdDownPoints = 100;
            Assert.Throws<ArgumentException>(() => a.Validate());
        }

        // --- BattleSimulation integration: confirms the per-unit arm-derived ChargeMelee
        // (not the shared global config.meleeAttacks Charge entry) actually drives Melee().
        private static SimulationConfig SimConfig()
        {
            var c = SimulationTests.Config(); c.boostOverheatEnabled = false; c.seamlessMeleeEnabled = false;
            c.meleeAttacks = new MeleeAttackDefinition[5];
            for (int i = 0; i < 5; i++) c.meleeAttacks[i] = new MeleeAttackDefinition
            {
                style = (MeleeStyle)i, damage = 999, downPoints = 999, range = 3, halfAngleDegrees = 90,
                windupSeconds = 0.06f, recoverySeconds = 0.1f, cooldownSeconds = 0.1f, moveMultiplier = 0.5f,
                comboSteps = i == 1 || i == 3 ? 2 : 1, maximumTargets = 3,
                minimumChargeSeconds = i == 4 ? 0.4f : 0, fullChargeSeconds = i == 4 ? 1.4f : 0, minimumChargeMultiplier = i == 4 ? 0.3f : 0
            };
            return c;
        }
        private static UnitLoadout Blade(MeleeAttackDefinition chargeMelee) =>
            new UnitLoadout("charge-test", 10000, 0, 10, 1, 500, 3.8f, 0.35f, 120, 10, kind: WeaponKind.Melee, chargeMelee: chargeMelee);
        private static BattleSimulation World(MeleeAttackDefinition chargeMelee, float targetZ)
        {
            var blade = Blade(chargeMelee);
            var decks = new UnitLoadout[100][]; for (int i = 0; i < 100; i++) decks[i] = new[] { blade, blade };
            var w = new BattleSimulation(SimulationTests.Map(), SimConfig(), 0, decks);
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
        private static void Submit(BattleSimulation w, int slot, float x, float z, float yaw, bool melee, MeleeStyle style = MeleeStyle.Charge)
        {
            var u = w.GetUnit(slot);
            Assert.That(w.Submit(slot, new UnitCommand(u.UnitId, w.CurrentTick, (uint)w.CurrentTick + 1, x, z, yaw, 0, false, false, false, melee, false, meleeStyle: style)), Is.True);
        }
        // Holds the Charge input for chargeSeconds, releases it (triggering StartMelee that
        // same tick), then waits for the windup so Melee() resolves the hit.
        private static void ChargeAndRelease(BattleSimulation w, float chargeSeconds)
        {
            int holdTicks = (int)(chargeSeconds / w.FixedDeltaTime);
            for (int i = 0; i < holdTicks; i++) { Submit(w, 0, 0, 0, 0, true); w.Step(); }
            Submit(w, 0, 0, 0, 0, false); w.Step(); // release
            for (int i = 0; i < 10; i++) { Submit(w, 0, 0, 0, 0, false); w.Step(); }
        }
        private static MeleeAttackDefinition StandardArmCharge() => new MeleeAttackDefinition
        {
            style = MeleeStyle.Charge, damage = 500, downPoints = 1600, range = 6.5f, minimumRange = 1.0f, chargeScalesRange = true,
            halfAngleDegrees = 180, windupSeconds = 0.06f, recoverySeconds = 0.1f, cooldownSeconds = 0.1f, moveMultiplier = 0.5f,
            comboSteps = 1, maximumTargets = 3, minimumChargeSeconds = 0.4f, fullChargeSeconds = 1.4f, minimumChargeMultiplier = 0.3f
        };
        private static MeleeAttackDefinition ReinforcedArmCharge() => new MeleeAttackDefinition
        {
            style = MeleeStyle.Charge, damage = 3000, downPoints = 2200, belowThresholdDamage = 550, belowThresholdDownPoints = 700, thresholdChargeFraction = 0.5f,
            range = 4.5f, halfAngleDegrees = 90, windupSeconds = 0.06f, recoverySeconds = 0.1f, cooldownSeconds = 0.1f, moveMultiplier = 0.5f,
            comboSteps = 1, maximumTargets = 3, minimumChargeSeconds = 0.4f, fullChargeSeconds = 1.4f, minimumChargeMultiplier = 0.3f
        };
        [Test] public void StandardArmChargeHitsAtMinimumRangeButMissesThereWhenBarelyCharged()
        {
            // Target sits beyond minimumRange but within the full range - only reachable once
            // enough charge has grown the effective range.
            var w = World(StandardArmCharge(), targetZ: 4.0f);
            ChargeAndRelease(w, 0.45f); // just above minimumChargeSeconds: low fraction, short range.
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000), "Barely-charged swing should not reach a target beyond the minimum range.");
        }
        [Test] public void StandardArmChargeReachesFartherAndDealsUnscaledDamageWhenFullyCharged()
        {
            var w = World(StandardArmCharge(), targetZ: 4.0f);
            ChargeAndRelease(w, 1.4f); // fully charged: range grows to 6.5, target at 4.0 is in range.
            // Damage is fixed (not scaled by charge) for this archetype - always exactly 500.
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000 - 500).Within(0.01));
        }
        [Test] public void ReinforcedArmChargeDealsTheFlatBelowThresholdDamageWhenChargedJustAboveTheMinimum()
        {
            var w = World(ReinforcedArmCharge(), targetZ: 3.0f); // well within the fixed 4.5 range regardless of charge.
            ChargeAndRelease(w, 0.45f); // well below the 0.5 threshold fraction (~0.32).
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000 - 550).Within(0.01));
        }
        [Test] public void ReinforcedArmChargeDealsFullDamageWhenFullyCharged()
        {
            var w = World(ReinforcedArmCharge(), targetZ: 3.0f);
            ChargeAndRelease(w, 1.4f); // fully charged.
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000 - 3000).Within(0.01));
        }
    }
}
