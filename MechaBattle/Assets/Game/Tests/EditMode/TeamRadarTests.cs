using System;
using System.Linq;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class TeamRadarTests
    {
        [Test] public void OnlyValidExplicitEnemyLocksShareHpWithAllAllies()
        {
            var w=SupportFixture.World(SupportFixture.Gear());Assert.That(w.GetTeamContact(0,50).Valid,Is.False);
            FieldFixture.Cmd(w,locked:50);w.Step();var c=w.GetTeamContact(0,50);
            Assert.That(c.Valid&&c.HasHealth,Is.True);Assert.That(c.UnitId,Is.EqualTo(w.GetUnit(50).UnitId));Assert.That(c.Health,Is.EqualTo(500));
            Assert.That(w.GetTeamContact(1,50).Valid,Is.False);Assert.That(w.GetTeamContact(1,0).Valid,Is.False);
        }
        [Test] public void LockHpExpiresExactlyOnePointSevenSecondsAfterLastObservation()
        {
            var w=SupportFixture.World(SupportFixture.Gear());FieldFixture.Cmd(w,locked:50);w.Step();FieldFixture.Wait(w,84);
            Assert.That(w.GetTeamContact(0,50).HasHealth,Is.True);w.Step();Assert.That(w.GetTeamContact(0,50).Valid,Is.False);
        }
        [Test] public void ReleasedContactKeepsLastKnownPositionAndHpWithoutTrackingHiddenMovement()
        {
            var w=SupportFixture.World(SupportFixture.Gear());FieldFixture.Cmd(w,locked:50);w.Step();var first=w.GetTeamContact(0,50);
            for(int i=0;i<20;i++){FieldFixture.Cmd(w,50,z:1);w.Step();}
            var last=w.GetTeamContact(0,50);Assert.That(last.Valid,Is.True);Assert.That(last.Z,Is.EqualTo(first.Z));Assert.That(w.GetUnit(50).Z,Is.GreaterThan(first.Z+2));
            Assert.That(last.HealthAgeSeconds,Is.GreaterThan(0.3f));
        }
        [Test] public void AnotherAllyLockRefreshesTheSameSharedTarget()
        {
            var w=SupportFixture.World(SupportFixture.Gear());FieldFixture.MoveTo(w,1,-6,2);FieldFixture.Cmd(w,locked:50);w.Step();FieldFixture.Wait(w,60);FieldFixture.Cmd(w,1,locked:50);w.Step();FieldFixture.Wait(w,60);
            Assert.That(w.GetTeamContact(0,50).HasHealth,Is.True);FieldFixture.Wait(w,25);Assert.That(w.GetTeamContact(0,50).Valid,Is.False);
        }
        [Test] public void OccludedOutOfRangeFriendlyAndStaleLocksNeverCreateContacts()
        {
            var blocked=FieldFixture.World(SupportFixture.Gear(),new[]{FieldFixture.Box()});FieldFixture.Cmd(blocked,locked:50);blocked.Step();Assert.That(blocked.GetTeamContact(0,50).Valid,Is.False);
            var shortRange=SupportFixture.World(SupportFixture.Gear(range:25));FieldFixture.MoveTo(shortRange,50,22,0);FieldFixture.Cmd(shortRange,locked:50);shortRange.Step();Assert.That(shortRange.GetTeamContact(0,50).Valid,Is.False);
            var w=SupportFixture.World(SupportFixture.Gear());FieldFixture.Cmd(w,locked:1);w.Step();Assert.That(w.GetTeamContact(0,1).Valid,Is.False);
            var self=w.GetUnit(0);w.Submit(0,new UnitCommand(self.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,90,0,false,false,false,false,false,lockedTargetSlot:50,lockedTargetUnitId:w.GetUnit(50).UnitId+1));w.Step();Assert.That(w.GetTeamContact(0,50).Valid,Is.False);
        }
        [Test] public void TargetDeathAndRespawnCannotInheritItsOldHpBar()
        {
            var w=SupportFixture.World(SupportFixture.Gear(damage:1000));FieldFixture.Cmd(w,locked:50);w.Step();int old=w.GetUnit(50).UnitId;
            FieldFixture.Cmd(w,fire:true);w.Step();Assert.That(w.GetTeamContact(0,50).Valid,Is.False);FieldFixture.Wait(w,10);
            Assert.That(w.GetUnit(50).UnitId,Is.Not.EqualTo(old));Assert.That(w.GetTeamContact(0,50).Valid,Is.False);
        }
        [Test] public void RadarSharesOnlyBoundedEnemyPositionsAndNeverHpOrIncomingLock()
        {
            var radar=SupportFixture.Gear(WeaponKind.Radar,noSupply:true,duration:2);var w=SupportFixture.World(radar);FieldFixture.MoveTo(w,51,45,0);
            FieldFixture.Cmd(w,fire:true);w.Step();FieldFixture.Wait(w,30);var c=w.GetTeamContact(0,50);
            Assert.That(c.Valid&&c.RadarDetected,Is.True);Assert.That(c.HasHealth||c.LockDetected,Is.False);Assert.That(w.GetUnit(50).IncomingLockCount,Is.Zero);
            Assert.That(w.GetTeamContact(0,51).Valid,Is.False);Assert.That(w.GetTeamContact(1,0).Valid,Is.False);
        }
        [Test] public void RadarDetectsThroughCoverButExpiresAfterLifetimeAndContactAge()
        {
            var w=FieldFixture.World(SupportFixture.Gear(WeaponKind.Radar,noSupply:true,duration:1),new[]{FieldFixture.Box()});Assert.That(w.HasLineOfSight(0,50),Is.False);
            FieldFixture.Cmd(w,fire:true);w.Step();FieldFixture.Wait(w,30);Assert.That(w.GetTeamContact(0,50).RadarDetected,Is.True);
            FieldFixture.Wait(w,100);Assert.That(w.GetTeamContact(0,50).Valid,Is.False);
        }
        [Test] public void RadarDoesNotExtendTheLockHpRetentionTime()
        {
            var gun=SupportFixture.Gear();var radar=SupportFixture.Gear(WeaponKind.Radar,noSupply:true,duration:5);var w=SupportFixture.World(gun.WithWeapons(gun,radar));
            FieldFixture.Cmd(w,locked:50);w.Step();FieldFixture.Cmd(w,fire:true,change:1);w.Step();FieldFixture.Wait(w,85);var c=w.GetTeamContact(0,50);
            Assert.That(c.Valid&&c.RadarDetected,Is.True);Assert.That(c.HasHealth,Is.False);
        }
        [Test] public void EachOwnerKeepsThreeRadarsAndTheFourthReplacementSpendsAnotherCharge()
        {
            var w=SupportFixture.World(SupportFixture.Gear(WeaponKind.Radar,noSupply:true,duration:5));FieldFixture.Cmd(w,fire:true);w.Step();int first=w.GetDeployable(0).Id;
            for(int n=0;n<2;n++){FieldFixture.Wait(w,40);FieldFixture.Cmd(w,fire:true);w.Step();}Assert.That(w.GetDeployable(0).Id,Is.EqualTo(first));
            Assert.That(Enumerable.Range(0,w.DeployableCapacity).Count(i=>w.GetDeployable(i).Active&&w.GetDeployable(i).OwnerSlot==0),Is.EqualTo(3));
            FieldFixture.Wait(w,40);FieldFixture.Cmd(w,fire:true);w.Step();Assert.That(w.GetDeployable(0).Id,Is.Not.EqualTo(first));
            int count=Enumerable.Range(0,w.DeployableCapacity).Count(i=>w.GetDeployable(i).Active&&w.GetDeployable(i).OwnerSlot==0);
            Assert.That(count,Is.EqualTo(3));Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(8));
        }
        [Test] public void InvalidSupportAndDetectionSettingsAreRejected()
        {
            Assert.Throws<ArgumentException>(()=>new UnitLoadout("bad",500,0,10,10,20,50,0.1f,120,10,kind:WeaponKind.RepairGun,fireDownPoints:1));
            Assert.Throws<ArgumentException>(()=>new UnitLoadout("bad",500,0,10,10,20,50,0.1f,120,10,kind:WeaponKind.Radar));
            var c=FieldFixture.Config();c.sharedLockHealthSeconds=float.NaN;Assert.Throws<ArgumentException>(()=>c.Validate());c=FieldFixture.Config();c.radarScanSeconds=2;Assert.Throws<ArgumentException>(()=>c.Validate());
        }
    }
}
