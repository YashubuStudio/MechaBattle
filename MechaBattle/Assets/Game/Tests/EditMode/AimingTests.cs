using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class AimingTests
    {
        private static BattleSimulation World(bool bazooka = false)
        {
            var c = SimulationTests.Config(); c.aimIn3DEnabled = true; c.unitHeight = 2.47f; c.shotOriginHeight = 1.3f; c.coreHeight = 3.6f; c.homeSupplyRadius = 0.01f;
            var map = SimulationTests.Map(); foreach (var b in map.mainBases) b.buildingRadius = 10;
            var gun = new UnitLoadout("aim-test", 10000, 0, 10, 100, 10, 65, 0.1f, 120, 10,
                bazooka ? WeaponFireMode.SingleShot : WeaponFireMode.FullAutomatic, bazooka ? 1 : 0, bazooka ? 2 : 0,
                blastRadius: bazooka ? 5 : 0, splashDamage: bazooka ? 35 : 0, projectileSpeed: bazooka ? 45 : 0);
            var decks = new UnitLoadout[100][]; for (int i = 0; i < 100; i++) decks[i] = new[] { gun, gun };
            var w = new BattleSimulation(map, c, 0, decks); MoveTo(w, 0, -5, 0); MoveTo(w, 50, 20, 0); return w;
        }
        private static void MoveTo(BattleSimulation w, int slot, float x, float z)
        {
            for (int i = 0; i < 500; i++)
            { var u = w.GetUnit(slot); float dx = x - u.X, dz = z - u.Z, d = (float)Math.Sqrt(dx * dx + dz * dz); if (d < 0.01f) return;
                float scale = Math.Max(d, 0.2f); SimulationTests.Command(w, slot, dx / scale, dz / scale); w.Step(); }
            Assert.Fail("Aim fixture could not move.");
        }
        private static AimPoint CameraAim(BattleSimulation w)
        {
            var self = w.GetUnit(0); var target = w.GetUnit(50);
            // A real shoulder camera: behind the unit, offset right, above the muzzle.
            float x = self.X - 7, y = 2, z = self.Z - 1.05f;
            return w.TraceAim(0, x, y, z, target.X - x, w.ShotHeight - y, target.Z - z, 74);
        }
        private static void FireAt(BattleSimulation w, AimPoint point, float moveZ = 0)
        {
            var u = w.GetUnit(0);
            Assert.That(w.Submit(0, new UnitCommand(u.UnitId, w.CurrentTick, (uint)w.CurrentTick + 1, 0, moveZ, 90, 0, false, true, false, false, false,
                hasAimPoint: true, aimTargetX: point.X, aimTargetY: point.Y, aimTargetZ: point.Z)), Is.True); w.Step();
        }
        [Test] public void CenterCameraRayAndShotConvergeOnTheSameDistantTarget()
        {
            var w = World(); var point = CameraAim(w); Assert.That(point.TargetSlot, Is.EqualTo(50)); FireAt(w, point);
            Assert.That(w.GetUnit(50).Health, Is.EqualTo(9990)); var shot = w.GetUnit(0);
            Assert.That(shot.ShotEndX, Is.EqualTo(point.X).Within(0.002)); Assert.That(shot.ShotEndY, Is.EqualTo(point.Y).Within(0.002)); Assert.That(shot.ShotEndZ, Is.EqualTo(point.Z).Within(0.002));
            Assert.That(Math.Abs(shot.AimYaw - 90), Is.LessThan(1), "Shoulder offset must not rotate a distant shot by ~16 degrees.");
        }
        [Test] public void StrafingDuringTheTickStillHitsThePointUnderTheReticle()
        {
            var w = World(); var point = CameraAim(w); float z = w.GetUnit(0).Z; FireAt(w, point, 1);
            Assert.That(w.GetUnit(0).Z, Is.GreaterThan(z)); Assert.That(w.GetUnit(50).Health, Is.EqualTo(9990));
            Assert.That(w.GetUnit(0).ShotEndX, Is.EqualTo(point.X).Within(0.002));
        }
        [Test] public void LookingAboveAUnitDoesNotProduceAFalsePlanarHit()
        {
            var w = World(); var u = w.GetUnit(0); var point = w.TraceAim(0, u.X - 7, 4, u.Z, 1, 0, 0, 40);
            Assert.That(point.TargetSlot, Is.EqualTo(-1)); FireAt(w, point); Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000));
            Assert.That(w.GetUnit(0).AimPitch, Is.LessThan(0)); Assert.That(w.GetUnit(0).ShotEndY, Is.GreaterThan(2.47));
        }
        [Test] public void LookingAtGroundStopsTheShotBeforeTargetsBehindIt()
        {
            var w = World(); var u = w.GetUnit(0); var point = w.TraceAim(0, u.X, 2, u.Z, 1, -0.5f, 0, 65);
            Assert.That(point.Y, Is.EqualTo(0).Within(0.0001)); Assert.That(point.TargetSlot, Is.EqualTo(-1)); FireAt(w, point);
            Assert.That(w.GetUnit(0).ShotEndY, Is.EqualTo(0).Within(0.0001)); Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000));
        }
        [Test] public void BazookaFollowsReticlePitchAndExplodesAtGroundHeight()
        {
            var w = World(true); var u = w.GetUnit(0); var point = w.TraceAim(0, u.X, 2, u.Z, 1, -0.5f, 0, 65); FireAt(w, point);
            for (int i = 0; i < 20; i++) w.Step(); bool exploded = false;
            for (int i = 0; i < w.ExplosionCapacity; i++) { var e = w.GetExplosion(i); if (!e.Valid) continue; exploded = true; Assert.That(e.Y, Is.EqualTo(0).Within(0.001)); Assert.That(e.X, Is.EqualTo(point.X).Within(0.002)); }
            Assert.That(exploded, Is.True); Assert.That(w.GetUnit(50).Health, Is.EqualTo(10000));
        }
        [Test] public void VerticalCameraRayCanHitTheTopOfTheFiniteUnitCylinder()
        {
            var w = World(); var target = w.GetUnit(50); var point = w.TraceAim(0, target.X, 10, target.Z, 0, -1, 0, 20);
            Assert.That(point.TargetSlot, Is.EqualTo(50)); Assert.That(point.Y, Is.EqualTo(2.47).Within(0.0001));
        }
        [Test] public void HighAirburstDoesNotDamageTheCoreDirectlyBelowIt()
        {
            var w = World(true); MoveTo(w, 0, 21, 0); var u = w.GetUnit(0);
            var point = w.TraceAim(0, u.X, w.ShotHeight, u.Z, 9, 90, 0, 65); FireAt(w, point);
            for (int i = 0; i < 80; i++) w.Step(); Assert.That(w.GetBase(4).DamageReceived, Is.Zero);
        }
        [Test] public void OverlappingCameraTargetDoesNotThrowWhenTheMuzzleRayHasZeroLength()
        {
            var w = World(); var self = w.GetUnit(0); MoveTo(w, 50, self.X, self.Z);
            var point = w.TraceCameraAim(0, self.X - 7, self.Y + w.ShotHeight, self.Z, 1, 0, 0, 74);
            Assert.That(point.TargetSlot, Is.EqualTo(50));
            Assert.That(point.X, Is.EqualTo(self.X)); Assert.That(point.Z, Is.EqualTo(self.Z));
            var barrel = w.TraceAim(0, self.X, self.Y + w.ShotHeight, self.Z,
                point.X - self.X, point.Y - self.Y - w.ShotHeight, point.Z - self.Z, 65);
            Assert.That(barrel.TargetSlot, Is.EqualTo(-1)); Assert.That(barrel.CoreIndex, Is.EqualTo(-1));
            Assert.That(barrel.DeployableIndex, Is.EqualTo(-1));
        }
        [TestCase(0f)] [TestCase(0.00000001f)]
        public void DegenerateMuzzleSegmentReturnsTheOriginWithoutAHit(float direction)
        {
            var w = World(); var u = w.GetUnit(0);
            var point = w.TraceAim(0, u.X, u.Y + w.ShotHeight, u.Z, direction, 0, 0, 65);
            Assert.That(point.X, Is.EqualTo(u.X)); Assert.That(point.Y, Is.EqualTo(u.Y + w.ShotHeight)); Assert.That(point.Z, Is.EqualTo(u.Z));
            Assert.That(point.TargetSlot, Is.EqualTo(-1)); Assert.That(point.CoreIndex, Is.EqualTo(-1)); Assert.That(point.DeployableIndex, Is.EqualTo(-1));
        }
        [Test] public void MalformedAimRaysStillRejectNonFiniteInputAndInvalidRange()
        {
            var w = World();
            Assert.Throws<ArgumentException>(() => w.TraceAim(0, float.NaN, 1, 0, 1, 0, 0, 65));
            Assert.Throws<ArgumentException>(() => w.TraceAim(0, 0, 1, 0, float.PositiveInfinity, 0, 0, 65));
            Assert.Throws<ArgumentException>(() => w.TraceAim(0, 0, 1, 0, 0, 0, 0, 0));
        }
    }
}
