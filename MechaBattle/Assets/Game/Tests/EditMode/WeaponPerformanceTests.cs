using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Contracts.Construction;
using MechaBattle.Construction;
namespace MechaBattle.Tests
{
    public sealed class WeaponPerformanceTests
    {
        [Test] public void SustainedFirepowerIncludesReloadWithoutCountingOverlappingRecoveryTwice()
        {
            var w = new WeaponDefinition { damage = 6, range = 65, intervalSeconds = 0.2f, ammo = 120, magazineCapacity = 24, reloadSeconds = 2.2f };
            Assert.That(WeaponPerformance.SustainedDps(w), Is.EqualTo(144d / 6.8).Within(0.001));
            w.mode = WeaponFireMode.SingleShot; w.damage = 65; w.magazineCapacity = 1; w.reloadSeconds = 2.8f;
            Assert.That(WeaponPerformance.SustainedDps(w), Is.EqualTo(65d / 2.8).Within(0.001));
            w.mode = WeaponFireMode.Charge; w.damage = 95; w.magazineCapacity = 3; w.reloadSeconds = 3; w.intervalSeconds = 1.2f; w.minimumChargeSeconds = 0.45f; w.fullChargeSeconds = 1.6f;
            Assert.That(WeaponPerformance.SustainedDps(w), Is.EqualTo(285d / 10.2).Within(0.001));
        }
        [Test] public void SustainedBeamAccountsForBurstGapsAndMagazineDepletion()
        {
            var w = new WeaponDefinition { mode = WeaponFireMode.ContinuousBeam, damage = 6, range = 45, intervalSeconds = 0.1f, ammo = 72, magazineCapacity = 18,
                reloadSeconds = 2.6f, maximumBeamSeconds = 1.8f, beamRecoverySeconds = 0.9f, firingMoveMultiplier = 0.45f };
            Assert.That(WeaponPerformance.SustainedDps(w), Is.EqualTo(108d / 4.3).Within(0.001));
            w.magazineCapacity = 36; Assert.That(WeaponPerformance.SustainedDps(w), Is.EqualTo(216d / 7).Within(0.001));
        }
        [Test] public void ConstructionRejectsMalformedDPAndExplosionFields()
        {
            var w = new WeaponDefinition { damage = 1, range = 10, intervalSeconds = 1, ammo = 10, downPoints = float.NaN };
            Assert.That(WeaponPerformance.Valid(w), Is.False); w.downPoints = 1; w.blastRadius = 5; Assert.That(WeaponPerformance.Valid(w), Is.False);
            w.mode = WeaponFireMode.SingleShot; w.magazineCapacity = 1; w.reloadSeconds = 1; w.splashDamage = 2; w.projectileSpeed = 20; Assert.That(WeaponPerformance.Valid(w), Is.True);
        }
    }
}
