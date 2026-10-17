using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class CombatRulesTests
    {
        private static UnitLoadout Gun(WeaponFireMode mode = WeaponFireMode.FullAutomatic, float damage = 10, float dp = 150, int ammo = 100, int magazine = 10,
            float interval = 0.65f, float reload = 2, float blast = 0)
        { return new UnitLoadout("combat-test", 10000, 50, 10, ammo, damage, 25, interval, 120, 10, mode, magazine, reload,
            mode == WeaponFireMode.Charge ? 0.4f : 0, mode == WeaponFireMode.Charge ? 1.2f : 0,
            mode == WeaponFireMode.ContinuousBeam ? 0.6f : 0, mode == WeaponFireMode.ContinuousBeam ? 0.4f : 0,
            mode == WeaponFireMode.ContinuousBeam ? 0.4f : mode == WeaponFireMode.Charge ? 0.6f : 1, 600, dp,
            blast, blast > 0 ? 35 : 0, blast > 0 ? 300 : 0, blast > 0 ? 20 : 0); }
        private static SimulationConfig Config(bool shield = true)
        {
            var c = SimulationTests.Config(); c.directionalShieldEnabled = shield; c.shieldHalfAngleDegrees = 60; c.shieldRecoverySeconds = 0.35f;
            c.downPointRecoverySeconds = 1.5f; c.staggerSeconds = 0.35f; c.knockdownSeconds = 1.25f; c.knockdownThresholdMultiplier = 1.8f;
            c.boostCapacity = 100; c.boostDrainPerSecond = 35; c.boostRecoveryPerSecond = 20; c.boostSpeedMultiplier = 1.8f; c.homeSupplyRadius = 0.01f;
            return c;
        }
        private static BattleSimulation World(UnitLoadout gun, SimulationConfig c = null)
        {
            var decks = new UnitLoadout[100][]; for (int i = 0; i < 100; i++) decks[i] = new[] { gun, gun };
            var w = new BattleSimulation(SimulationTests.Map(), c ?? Config(), 0, decks);
            MoveTo(w, 0, -5, 0); MoveTo(w, 50, 5, 0); return w;
        }
        private static void MoveTo(BattleSimulation w, int slot, float x, float z)
        {
            for (int t = 0; t < 500; t++)
            {
                var u = w.GetUnit(slot); float dx = x - u.X, dz = z - u.Z; float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                if (distance < 0.01f) return;
                float scale = Math.Max(distance, 10 * w.FixedDeltaTime);
                SimulationTests.Command(w, slot, dx / scale, dz / scale); w.Step();
            }
            Assert.Fail("Combat fixture did not reach position.");
        }
        private static void Input(BattleSimulation w, int slot = 0, bool fire = false, bool melee = false, float yaw = 90, float move = 0, bool reload = false, MeleeStyle style = MeleeStyle.Normal)
        {
            var u = w.GetUnit(slot); Assert.That(w.Submit(slot, new UnitCommand(u.UnitId, w.CurrentTick, (uint)w.CurrentTick + 1,
                move, 0, yaw, 0, false, fire, false, melee, false, reload, style)), Is.True);
        }
        private static void Ticks(BattleSimulation w, int count, bool fire = false)
        { for (int i = 0; i < count; i++) { Input(w, fire: fire); w.Step(); } }
        [Test] public void Fire_FARepeatsWhileHeld_SARequiresNewPress()
        {
            var fa = World(Gun(interval: 0.2f)); Ticks(fa, 31, true); Assert.That(fa.Counters.ShotsFired, Is.EqualTo(4));
            var sa = World(Gun(WeaponFireMode.SemiAutomatic, interval: 0.2f)); Ticks(sa, 31, true); Assert.That(sa.Counters.ShotsFired, Is.EqualTo(1));
            Ticks(sa, 1); Ticks(sa, 1, true); Assert.That(sa.Counters.ShotsFired, Is.EqualTo(2));
        }
        [Test] public void Fire_SingleShotReloadsButHeldTriggerDoesNotFireAgain_AmmoIsFinite()
        {
            var w = World(Gun(WeaponFireMode.SingleShot, ammo: 2, magazine: 1, interval: 0.1f, reload: 0.5f));
            Ticks(w, 40, true); Assert.That(w.Counters.ShotsFired, Is.EqualTo(1)); Assert.That(w.GetUnit(0).Magazine, Is.EqualTo(1));
            Ticks(w, 1); Ticks(w, 1, true); Assert.That(w.GetUnit(0).Ammo, Is.Zero); Ticks(w, 100, true);
            Assert.That(w.Counters.ShotsFired, Is.EqualTo(2)); Assert.That(w.GetUnit(0).Magazine, Is.Zero);
        }
        [Test] public void Fire_ManualReloadFillsOnlyFromRemainingTotal()
        {
            var w = World(Gun(ammo: 5, magazine: 3, interval: 0.1f, reload: 0.5f)); Ticks(w, 1, true); Ticks(w, 1);
            Input(w, reload: true); w.Step(); Assert.That(w.GetUnit(0).ReloadSeconds, Is.GreaterThan(0)); Ticks(w, 26);
            Assert.That(w.GetUnit(0).Magazine, Is.EqualTo(3)); Assert.That(w.GetUnit(0).Ammo, Is.EqualTo(4));
        }
        [Test] public void Fire_ChargeCancelsEarlyReleaseAndScalesDamageOnRelease()
        {
            var w = World(Gun(WeaponFireMode.Charge, damage: 100, dp: 0, interval: 0.2f));
            Ticks(w, 10, true); Ticks(w, 1); Assert.That(w.Counters.ShotsFired, Is.Zero); Assert.That(w.GetUnit(0).Ammo, Is.EqualTo(100));
            float before = w.GetUnit(50).Health; Ticks(w, 30, true); Ticks(w, 1);
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(before - 50).Within(0.01)); Assert.That(w.Counters.ShotsFired, Is.EqualTo(1));
            Ticks(w, 10); before = w.GetUnit(50).Health; Ticks(w, 65, true); Ticks(w, 1);
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(before - 100).Within(0.01));
        }
        [Test] public void Fire_BeamHasBoundedBurstRecoveryAndMovementPenalty()
        {
            var w = World(Gun(WeaponFireMode.ContinuousBeam, dp: 0, interval: 0.1f, magazine: 50));
            Input(w, fire: true); w.Step(); float x = w.GetUnit(0).X; Input(w, fire: true, move: 1); w.Step();
            Assert.That(w.GetUnit(0).X - x, Is.EqualTo(0.08).Within(0.001));
            Ticks(w, 28, true); Assert.That(w.Counters.ShotsFired, Is.EqualTo(6)); Ticks(w, 1, true); Assert.That(w.GetUnit(0).BeamActive, Is.False);
            Ticks(w, 18, true); Assert.That(w.Counters.ShotsFired, Is.EqualTo(6)); Ticks(w, 2, true); Assert.That(w.Counters.ShotsFired, Is.GreaterThan(6));
        }
        [Test] public void Shield_IdleFrontBlocksDamageAndDP_BreakOverflowReachesHealth()
        {
            var w = World(Gun(damage: 75, dp: 800, interval: 0.1f), Config(true));
            Input(w, 50, yaw: 270); Input(w, fire: true); w.Step(); var t = w.GetUnit(50);
            Assert.That(t.Shield, Is.Zero); Assert.That(t.Health, Is.EqualTo(9975)); Assert.That(t.DownPoints, Is.EqualTo(800f / 3).Within(0.01));
            Assert.That(t.LastShieldBlockTick, Is.GreaterThanOrEqualTo(0)); Assert.That(t.Disruption, Is.EqualTo(DisruptionState.Ready));
        }
        [Test] public void Shield_SideRearAndAttackingBypassGuard_RecoversAfterDelay()
        {
            foreach (float yaw in new[] { 0f, 90f })
            {
                var w = World(Gun(interval: 0.1f), Config(true)); Input(w, 50, yaw: yaw); Input(w, fire: true); w.Step();
                Assert.That(w.GetUnit(50).Health, Is.EqualTo(9990)); Assert.That(w.GetUnit(50).Shield, Is.EqualTo(50));
            }
            var a = World(Gun(interval: 0.1f), Config(true)); Input(a, 50, fire: true, yaw: 270); Input(a, fire: true); a.Step();
            Assert.That(a.GetUnit(50).Health, Is.EqualTo(9990)); Assert.That(a.GetUnit(50).Guarding, Is.False);
            Ticks(a, 19); Assert.That(a.GetUnit(50).Guarding, Is.True);
        }
        [Test] public void DP_SAHitsAndOneMissMaintainAccumulation_TwoMissesFullyReset()
        {
            var w = World(Gun(WeaponFireMode.SemiAutomatic, dp: 150));
            Ticks(w, 1, true); Ticks(w, 32); Ticks(w, 1, true); Assert.That(w.GetUnit(50).DownPoints, Is.EqualTo(300));
            Ticks(w, 64); Ticks(w, 1, true); Assert.That(w.GetUnit(50).DownPoints, Is.EqualTo(450), "One missed SA interval must preserve DP.");
            Ticks(w, 75); Assert.That(w.GetUnit(50).DownPoints, Is.Zero, "Two missed intervals exceed 1.5s.");
        }
        [Test] public void DP_StaggerThenKnockdownInterruptsMovementAndFire_ThresholdDoesNotRepeat()
        {
            var w = World(Gun(dp: 600, interval: 0.1f)); Ticks(w, 1, true); Assert.That(w.GetUnit(50).Disruption, Is.EqualTo(DisruptionState.Staggered));
            float x = w.GetUnit(50).X; Input(w, 50, fire: true, move: 1, yaw: 270); w.Step(); Assert.That(w.GetUnit(50).X, Is.EqualTo(x)); Assert.That(w.Counters.ShotsFired, Is.EqualTo(1));
            Ticks(w, 4); Ticks(w, 1, true); Assert.That(w.GetUnit(50).Disruption, Is.EqualTo(DisruptionState.KnockedDown));
            float duration = w.GetUnit(50).DisruptionSeconds; Ticks(w, 5); Ticks(w, 1, true);
            Assert.That(w.GetUnit(50).DisruptionSeconds, Is.LessThan(duration), "Capped DP cannot reapply knockdown on each tick.");
            Ticks(w, 70); Assert.That(w.GetUnit(50).Disruption, Is.EqualTo(DisruptionState.Ready));
        }
        [Test] public void Bazooka_TravelsBeforeDirectHit_StaggersWithoutSplashDoubleCounting()
        {
            var w = World(Gun(WeaponFireMode.SingleShot, damage: 75, dp: 800, magazine: 1, interval: 0.1f, blast: 5));
            Ticks(w, 1, true); Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000), "Bazooka must not be an instant hitscan.");
            Ticks(w, 26); var t = w.GetUnit(50); Assert.That(t.Health, Is.EqualTo(9925)); Assert.That(t.DownPoints, Is.EqualTo(800));
            Assert.That(t.Disruption, Is.EqualTo(DisruptionState.Staggered));
        }
        [Test] public void Bazooka_SplashFallsOffAndBypassesFrontalShield_DoesNotHitFriendlies()
        {
            var w = World(Gun(WeaponFireMode.SingleShot, damage: 75, dp: 800, magazine: 1, interval: 0.1f, blast: 5), Config(true));
            MoveTo(w, 51, 5, 2); MoveTo(w, 1, 5, 1); Input(w, 51, yaw: 270); Input(w, fire: true); w.Step(); Ticks(w, 26);
            Assert.That(w.GetUnit(51).Health, Is.LessThan(10000).And.GreaterThan(9965)); Assert.That(w.GetUnit(51).Shield, Is.EqualTo(50));
            Assert.That(w.GetUnit(51).DownPoints, Is.LessThan(300)); Assert.That(w.GetUnit(1).Health, Is.EqualTo(10000));
        }
        private static MeleeAttackDefinition[] Attacks()
        {
            var a = new MeleeAttackDefinition[5];
            for (int i = 0; i < 5; i++) a[i] = new MeleeAttackDefinition { style = (MeleeStyle)i, damage = 30, downPoints = 350, range = 3.5f, halfAngleDegrees = 60,
                windupSeconds = 0.1f, recoverySeconds = 0.2f, cooldownSeconds = 0.3f, lungeMeters = i == 2 || i == 3 ? 5 : 0,
                boostCost = i == 2 || i == 3 ? 30 : 0, moveMultiplier = 0.4f, comboSteps = i == 1 ? 3 : i == 3 ? 2 : 1, maximumTargets = 3,
                minimumChargeSeconds = i == 4 ? 0.4f : 0, fullChargeSeconds = i == 4 ? 1.2f : 0, minimumChargeMultiplier = i == 4 ? 0.3f : 0 };
            return a;
        }
        [Test] public void Melee_WindupAndRecoveryBlockShooting_ManualStrikeBypassesShield_NoCounter()
        {
            var c = Config(true); c.meleeAttacks = Attacks(); var w = World(Gun(dp: 0), c); MoveTo(w, 0, 2, 0);
            Input(w, 50, yaw: 270); Input(w, melee: true, fire: true); w.Step(); Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000)); Assert.That(w.Counters.ShotsFired, Is.Zero);
            Ticks(w, 5, true); Assert.That(w.GetUnit(50).Health, Is.EqualTo(9970)); Assert.That(w.GetUnit(50).Shield, Is.EqualTo(50)); Assert.That(w.GetUnit(0).Health, Is.EqualTo(10000));
            Ticks(w, 5, true); Assert.That(w.Counters.ShotsFired, Is.Zero);
        }
        [Test] public void Melee_ComboRequiresHeldInput_StopsAtThreeStages()
        {
            var c = Config(); c.meleeAttacks = Attacks(); var w = World(Gun(dp: 0), c); MoveTo(w, 0, 2, 0);
            for (int i = 0; i < 65; i++) { Input(w, melee: true, style: MeleeStyle.Combo); w.Step(); }
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(9910)); Assert.That(w.GetUnit(0).ComboStep, Is.EqualTo(3));
            var release = World(Gun(dp: 0), c); MoveTo(release, 0, 2, 0); Input(release, melee: true, style: MeleeStyle.Combo); release.Step(); Ticks(release, 60);
            Assert.That(release.GetUnit(50).Health, Is.EqualTo(9970));
        }
        [Test] public void Melee_DashLungesAndSpendsBoost_DashComboCanChainTwice()
        {
            foreach (var style in new[] { MeleeStyle.Dash, MeleeStyle.DashCombo })
            {
                var c = Config(); c.meleeAttacks = Attacks(); var w = World(Gun(dp: 0), c); MoveTo(w, 0, -2, 0);
                float x = w.GetUnit(0).X; Input(w, melee: true, style: style); w.Step(); Assert.That(w.GetUnit(0).X - x, Is.EqualTo(5).Within(0.001)); Assert.That(w.GetUnit(0).Boost, Is.LessThan(71));
                for (int i = 0; i < 18; i++) { Input(w, melee: true, style: style); w.Step(); }
                Assert.That(w.GetUnit(0).ComboStep, Is.EqualTo(style == MeleeStyle.Dash ? 1 : 2));
            }
        }
        [Test] public void Melee_ChargeRequiresMinimumHold_ReleasesOneScaledStrike()
        {
            var c = Config(); c.meleeAttacks = Attacks(); var w = World(Gun(dp: 0), c); MoveTo(w, 0, 2, 0);
            for (int i = 0; i < 10; i++) { Input(w, melee: true, style: MeleeStyle.Charge); w.Step(); } Ticks(w, 10); Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000));
            for (int i = 0; i < 60; i++) { Input(w, melee: true, style: MeleeStyle.Charge); w.Step(); } Ticks(w, 6);
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(9970).Within(0.01));
        }
        [Test] public void Melee_MaximumTargetsAndNoInputNeverAutoAttack()
        {
            var c = Config(); c.meleeAttacks = Attacks(); var w = World(Gun(dp: 0), c); MoveTo(w, 0, 2, 0);
            for (int i = 51; i < 55; i++) MoveTo(w, i, 5, (i - 52) * 0.2f);
            Ticks(w, 60); Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000)); Input(w, melee: true); w.Step(); Ticks(w, 6);
            int damaged = 0; for (int i = 50; i < 55; i++) if (w.GetUnit(i).Health < 10000) damaged++;
            Assert.That(damaged, Is.EqualTo(3));
        }
        [Test] public void Melee_HeldNormalDoesNotRepeatAndChargeIsCancelledByStagger()
        {
            var c = Config(); c.meleeAttacks = Attacks(); var w = World(Gun(dp: 600, interval: 0.1f), c); MoveTo(w, 0, 2, 0);
            for (int i = 0; i < 65; i++) { Input(w, melee: true); w.Step(); }
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(9970)); Ticks(w, 1);
            Input(w, melee: true, style: MeleeStyle.Charge); w.Step();
            Input(w, melee: true, style: MeleeStyle.Charge); Input(w, 50, fire: true, yaw: 270); w.Step();
            Assert.That(w.GetUnit(0).Disruption, Is.EqualTo(DisruptionState.Staggered)); Assert.That(w.GetUnit(0).MeleeChargeFraction, Is.Zero);
            Ticks(w, 70); Assert.That(w.GetUnit(50).Health, Is.EqualTo(9970), "Interrupted charge must not release later without a new gesture.");
        }
        [Test] public void Charge_MissingCommandCancelsInsteadOfFiringAndMeleeStopsBeam()
        {
            var w = World(Gun(WeaponFireMode.Charge, dp: 0)); Ticks(w, 60, true); w.Step(); Assert.That(w.Counters.ShotsFired, Is.Zero);
            var c = Config(); c.meleeAttacks = Attacks(); var beam = World(Gun(WeaponFireMode.ContinuousBeam, dp: 0, interval: 0.1f), c);
            Ticks(beam, 1, true); Assert.That(beam.GetUnit(0).BeamActive, Is.True); Input(beam, melee: true); beam.Step();
            Assert.That(beam.GetUnit(0).BeamActive, Is.False); Assert.That(beam.GetUnit(0).LastMeleeTick, Is.GreaterThanOrEqualTo(0));
        }
        [Test] public void Bazooka_SplashCannotBypassCoreBuildingEntryGate()
        {
            var gun = Gun(WeaponFireMode.SingleShot, damage: 75, dp: 800, magazine: 1, interval: 0.1f, reload: 0.5f, blast: 5);
            var map = SimulationTests.Map(); foreach (var b in map.mainBases) b.buildingRadius = 10;
            var decks = new UnitLoadout[100][]; for (int i = 0; i < 100; i++) decks[i] = new[] { gun, gun };
            var w = new BattleSimulation(map, Config(), 0, decks); MoveTo(w, 0, 8, 0); Ticks(w, 1, true); Ticks(w, 70);
            Assert.That(w.GetBase(4).DamageReceived, Is.Zero, "Explosion from outside cannot damage a protected core.");
            MoveTo(w, 0, 21, 0); Ticks(w, 1, true); Ticks(w, 30); Assert.That(w.GetBase(4).DamageReceived, Is.GreaterThan(0));
        }
        [Test] public void Combat_BotGesturesUseSAEdgesAndReleaseFullyChargedShots()
        {
            foreach (var mode in new[] { WeaponFireMode.SemiAutomatic, WeaponFireMode.SingleShot, WeaponFireMode.Charge, WeaponFireMode.ContinuousBeam })
            {
                var gun = Gun(mode, dp: 0, interval: 0.1f, magazine: mode == WeaponFireMode.SingleShot ? 1 : 10, reload: 0.5f);
                var w = World(gun); var adapter = new CombatInputAdapter();
                for (int i = 0; i < 160; i++)
                {
                    var self = w.GetUnit(0); var intent = new UnitCommand(self.UnitId, w.CurrentTick, (uint)w.CurrentTick + 1, 0, 0, 90, 0, false, true, false, false, false);
                    Assert.That(w.Submit(0, adapter.Adapt(self, gun, intent)), Is.True); w.Step();
                }
                Assert.That(w.Counters.ShotsFired, Is.GreaterThanOrEqualTo(2), mode.ToString()); Assert.That(w.GetUnit(0).Ammo, Is.EqualTo(100 - w.Counters.ShotsFired));
            }
        }
        [Test] public void Combat_RejectsInvalidModeTimingAndMeleeConfiguration()
        {
            Assert.Throws<ArgumentException>(() => Gun(WeaponFireMode.SingleShot, magazine: 2));
            Assert.Throws<ArgumentException>(() => Gun(WeaponFireMode.ContinuousBeam, interval: 1));
            var c = Config(); c.meleeAttacks = new[] { Attacks()[0] }; Assert.Throws<ArgumentException>(() => c.Validate());
            c = Config(); c.meleeAttacks = Attacks(); c.meleeAttacks[4].style = MeleeStyle.Normal; Assert.Throws<ArgumentException>(() => c.Validate());
            c = Config(); c.knockdownThresholdMultiplier = 1; Assert.Throws<ArgumentException>(() => c.Validate());
        }
        [Test] public void PauseCancellationAndStyleChangeDoNotReleaseUnrequestedChargedAttacks()
        {
            var w = World(Gun(WeaponFireMode.Charge)); Ticks(w, 65, true); w.CancelHeldActions(0); Ticks(w, 1); Assert.That(w.Counters.ShotsFired, Is.Zero);
            var c = Config(); c.meleeAttacks = Attacks(); var melee = World(Gun(dp: 0), c); MoveTo(melee, 0, 2, 0);
            for (int i = 0; i < 65; i++) { Input(melee, melee: true, style: MeleeStyle.Charge); melee.Step(); }
            Input(melee, melee: true, style: MeleeStyle.Normal); melee.Step(); Ticks(melee, 15);
            Assert.That(melee.GetUnit(50).Health, Is.EqualTo(10000));
        }
    }
}
