using System;
using System.Linq;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;

namespace MechaBattle.Tests
{
    public sealed class FriendlySupportTests
    {
        private static UnitLoadout Repair() => new UnitLoadout("support-test",500,100,10,12,45,65,0.15f,120,10,WeaponFireMode.FullAutomatic,12,0.6f,kind:WeaponKind.RepairGun,redLockRange:25,yellowLockRange:60);
        [TestCase(WeaponKind.RepairGun)] [TestCase(WeaponKind.RepairPod)]
        public void CarryingHolsteredRecoveryEquipmentAllowsFriendlyLock(WeaponKind kind)
        {
            var gun=SupportFixture.Gear();var heal=kind==WeaponKind.RepairGun?Repair():SupportFixture.Gear(kind,range:8);
            var w=SupportFixture.World(gun.WithWeapons(gun,heal));FieldFixture.MoveTo(w,1,-2,0);
            Assert.That(w.GetActiveWeapon(0).Kind,Is.EqualTo(WeaponKind.Ranged));Assert.That(w.HasRecoveryEquipment(0),Is.True);
            Assert.That(w.IsFriendlyYellowLocked(0,1),Is.True);Assert.That(w.IsManualUnitYellowLocked(0,1),Is.True);
            Assert.That(w.IsYellowLocked(0,1),Is.False);Assert.That(w.IsFriendlyRedLocked(0,1),Is.False);
        }
        [Test] public void SelfEnemyAndNonHealingEquipmentAreNotFriendlyTargets()
        {
            var gun=SupportFixture.Gear();var radar=SupportFixture.Gear(WeaponKind.Radar);var w=SupportFixture.World(gun.WithWeapons(gun,radar));
            Assert.That(w.HasRecoveryEquipment(0),Is.False);Assert.That(w.IsFriendlyYellowLocked(0,1),Is.False);
            w=SupportFixture.World(Repair());Assert.That(w.IsFriendlyYellowLocked(0,0),Is.False);Assert.That(w.IsFriendlyYellowLocked(0,50),Is.False);
            Assert.That(w.IsManualUnitYellowLocked(0,50),Is.False,"RepairFA only selects allies.");
        }
        [Test] public void RecoveryInAReserveDeckDoesNotGrantTheCurrentBuildFriendlyLocks()
        {
            var gun=SupportFixture.Gear();var decks=new UnitLoadout[100][];for(int i=0;i<100;i++)decks[i]=new[]{gun,gun.WithWeapons(gun,Repair())};
            var w=new BattleSimulation(SimulationTests.Map(),FieldFixture.Config(),0,decks);
            Assert.That(w.HasRecoveryEquipment(0),Is.False);Assert.That(w.IsFriendlyYellowLocked(0,1),Is.False);
            w=new BattleSimulation(SimulationTests.Map(),FieldFixture.Config(),0,decks,1);Assert.That(w.HasRecoveryEquipment(0),Is.True);Assert.That(w.IsFriendlyYellowLocked(0,1),Is.True);
        }
        [Test] public void FriendlyRangeComesFromRecoveryEquipmentAndHonoursCover()
        {
            var gun=SupportFixture.Gear();var w=SupportFixture.World(gun.WithWeapons(gun,Repair()));FieldFixture.MoveTo(w,1,55,0);
            Assert.That(w.GetFriendlyLockRange(0),Is.EqualTo(60));Assert.That(w.IsManualUnitYellowLocked(0,1),Is.False);
            var blocked=FieldFixture.World(Repair(),new[]{FieldFixture.Box()});FieldFixture.MoveTo(blocked,1,-6,14);FieldFixture.MoveTo(blocked,1,3,14);FieldFixture.MoveTo(blocked,1,3,0);
            Assert.That(blocked.IsFriendlyYellowLocked(0,1),Is.False);Assert.That(blocked.GetUnitHealthBar(0,1).Visible,Is.False);
        }
        [Test] public void AllyLockSurvivesWeaponChangesWithoutHostileWarningsOrContactLeak()
        {
            var gun=SupportFixture.Gear();var radar=SupportFixture.Gear(WeaponKind.Radar);var w=SupportFixture.World(gun.WithWeapons(gun,Repair(),radar));FieldFixture.MoveTo(w,1,-2,0);
            FieldFixture.Cmd(w,locked:1);w.Step();Assert.That(w.GetUnit(1).IncomingLockCount,Is.Zero);
            Assert.That(w.GetTeamContact(0,1).Valid,Is.False);Assert.That(w.GetTeamContact(1,0).Valid,Is.False);
            FieldFixture.Cmd(w,change:1,locked:1);w.Step();Assert.That(w.IsManualUnitYellowLocked(0,1),Is.True);Assert.That(w.IsFriendlyRedLocked(0,1),Is.True);
            FieldFixture.Cmd(w,change:1,locked:1);w.Step();Assert.That(w.IsManualUnitYellowLocked(0,1),Is.True);Assert.That(w.IsFriendlyRedLocked(0,1),Is.False);
        }
        [Test] public void LockedRepairStillRequiresManualAimAndOnlyHealsFriendlyHp()
        {
            var gun=SupportFixture.Gear();var w=SupportFixture.World(gun.WithWeapons(gun,Repair()));SupportFixture.WoundFriend(w);float hp=w.GetUnit(1).Health;
            FieldFixture.Cmd(w,change:1,locked:1,fire:true,yaw:0);w.Step();Assert.That(w.GetUnit(1).Health,Is.EqualTo(hp));FieldFixture.Wait(w,8);
            FieldFixture.Cmd(w,locked:1,fire:true);w.Step();Assert.That(w.GetUnit(1).Health,Is.EqualTo(hp+45));Assert.That(w.GetUnit(1).IncomingLockCount,Is.Zero);
        }
        [Test] public void FriendlyLockCannotRemoveTheSpreadOfAnOffensiveWeapon()
        {
            var gun=new UnitLoadout("support-test",500,100,10,12,50,80,0.15f,120,10,WeaponFireMode.FullAutomatic,12,0.6f,projectileSpeed:8,hipSpreadDegrees:2);
            var c=FieldFixture.Config();c.weaponSpreadEnabled=true;c.redLockAssistEnabled=false;
            var a=SupportFixture.World(gun.WithWeapons(gun,Repair()),c);var b=SupportFixture.World(gun.WithWeapons(gun,Repair()),c);
            FieldFixture.Cmd(a,fire:true,locked:1);FieldFixture.Cmd(b,fire:true);a.Step();b.Step();
            var pa=a.GetProjectile(0);var pb=b.GetProjectile(0);Assert.That(pa.Active&&pb.Active,Is.True);
            Assert.That(pa.X,Is.EqualTo(pb.X));Assert.That(pa.Y,Is.EqualTo(pb.Y));Assert.That(pa.Z,Is.EqualTo(pb.Z));
        }
        [Test] public void VisibleAllyHealthNeedsNoLockAndSelfAndUnobservedEnemyStayHidden()
        {
            var w=SupportFixture.World(SupportFixture.Gear());FieldFixture.MoveTo(w,1,-2,0);var friend=w.GetUnitHealthBar(0,1);
            Assert.That(friend.Visible&&friend.Friendly,Is.True);Assert.That(friend.Health,Is.EqualTo(500));Assert.That(friend.MaxHealth,Is.EqualTo(500));
            Assert.That(w.GetUnitHealthBar(0,0).Visible,Is.False);Assert.That(w.GetUnitHealthBar(0,50).Visible,Is.False);
            FieldFixture.Cmd(w,locked:50);w.Step();Assert.That(w.GetUnitHealthBar(0,50).Visible,Is.True);Assert.That(w.GetUnitHealthBar(0,50).Friendly,Is.False);
            FieldFixture.Wait(w,85);Assert.That(w.GetUnitHealthBar(0,50).Visible,Is.False);Assert.That(w.GetUnitHealthBar(0,1).Visible,Is.True);
        }
        [Test] public void AllyDeathHidesTheBarAndRespawnShowsFreshHealthAndIdentity()
        {
            var gun=SupportFixture.Gear(damage:1000);var w=SupportFixture.World(gun.WithWeapons(gun,Repair()));FieldFixture.MoveTo(w,1,0,0);int old=w.GetUnit(1).UnitId;
            w.SetAutomaticRespawn(1,false);FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();Assert.That(w.GetUnit(1).IsAlive,Is.False);
            Assert.That(w.GetUnitHealthBar(0,1).Visible,Is.False);Assert.That(w.IsFriendlyYellowLocked(0,1),Is.False);
            w.SetAutomaticRespawn(1,true);FieldFixture.Wait(w,10);var fresh=w.GetUnitHealthBar(0,1);Assert.That(fresh.Visible,Is.True);Assert.That(fresh.UnitId,Is.Not.EqualTo(old));Assert.That(fresh.Health,Is.EqualTo(500));
        }
    }
}
