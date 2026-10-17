using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class EquipmentRulesTests
    {
        [Test] public void WheelSelectionCyclesThreeWeaponsAndPreservesAmmoAndReload()
        {
            var a=FieldFixture.Gun(speed:0);var b=FieldFixture.Gun(speed:0,damage:30);var c=FieldFixture.Gun(speed:0,damage:40);var w=FieldFixture.World(a.WithWeapons(a,b,c));
            FieldFixture.Cmd(w,fire:true);w.Step();FieldFixture.Wait(w,5);FieldFixture.Cmd(w,fire:true);w.Step();Assert.That(w.GetUnit(0).Magazine,Is.Zero);
            FieldFixture.Cmd(w,change:1);w.Step();Assert.That(w.GetUnit(0).ActiveWeaponIndex,Is.EqualTo(1));Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(20));
            FieldFixture.Cmd(w,fire:true,change:1);w.Step();Assert.That(w.GetUnit(0).ActiveWeaponIndex,Is.EqualTo(2));Assert.That(w.GetWeaponAmmo(0,0),Is.EqualTo(18));Assert.That(w.GetUnit(50).Health,Is.EqualTo(420),"The newly equipped weapon must use its own damage.");
            FieldFixture.Cmd(w,change:1);w.Step();Assert.That(w.GetUnit(0).ActiveWeaponIndex,Is.Zero);Assert.That(w.GetUnit(0).Magazine,Is.Zero);Assert.That(w.GetUnit(0).ReloadSeconds,Is.GreaterThan(0));
            FieldFixture.Wait(w,35);Assert.That(w.GetUnit(0).Magazine,Is.EqualTo(2));Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(18));
        }
        [Test] public void ThreeWeaponSnapshotCannotBeChangedByMutatingTheInputArray()
        {
            var a=FieldFixture.Gun();var b=FieldFixture.Gun(damage:30);var options=new[]{a,b,a};var frame=a.WithWeapons(options);options[1]=a;
            Assert.That(frame.GetWeapon(1).FireDamage,Is.EqualTo(30));Assert.Throws<ArgumentException>(()=>a.WithWeapons(a,a,a,a));
        }
        [Test] public void RepairPodConsumesItsOwnChargeAndHealsFriendliesWithoutAmmoRefill()
        {
            var a=FieldFixture.Gun(speed:0,damage:80);var pod=FieldFixture.Gun(kind:WeaponKind.RepairPod,damage:20);var w=FieldFixture.World(a.WithWeapons(a,pod));
            FieldFixture.Cmd(w,50,fire:true,yaw:270);w.Step();Assert.That(w.GetUnit(0).Health,Is.EqualTo(420));
            FieldFixture.Cmd(w,fire:true,change:1);w.Step();Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(4));FieldFixture.Wait(w,55);
            var podState=w.GetDeployable(0);Assert.That(w.GetUnit(0).Health,Is.GreaterThan(430),"pod active="+podState.Active+" kind="+podState.Kind+" team="+podState.Team+" owner="+podState.OwnerSlot+" radius="+podState.Radius+" left="+podState.SecondsLeft+" x="+podState.X+" selfX="+w.GetUnit(0).X+" y="+podState.Y+" selfY="+w.GetUnit(0).Y+" armed="+podState.Armed);Assert.That(w.GetWeaponAmmo(0,1),Is.EqualTo(4));Assert.That(w.GetUnit(50).Health,Is.EqualTo(500));
            int pods=0;for(int i=0;i<w.DeployableCapacity;i++)if(w.GetDeployable(i).Active)pods++;Assert.That(pods,Is.EqualTo(1));
        }
        [Test] public void DeployedMineWaitsForArmingThenHitsOnlyEnemiesAndExpires()
        {
            var mine=FieldFixture.Gun(kind:WeaponKind.Mine);var w=FieldFixture.World(mine);FieldFixture.MoveTo(w,50,-5,0);
            FieldFixture.Cmd(w,fire:true);w.Step();FieldFixture.Wait(w,15);Assert.That(w.GetUnit(50).Health,Is.EqualTo(500));FieldFixture.Wait(w,30);
            Assert.That(w.GetUnit(50).Health,Is.LessThan(500));Assert.That(w.GetUnit(0).Health,Is.EqualTo(500));
            FieldFixture.Wait(w,50);FieldFixture.Cmd(w,fire:true,yaw:270);w.Step();FieldFixture.Wait(w,260);int live=0;for(int i=0;i<w.DeployableCapacity;i++)if(w.GetDeployable(i).Active)live++;Assert.That(live,Is.Zero);
        }
        [Test] public void GrenadeUsesGravityAndFuseInsteadOfExplodingOnFirstFloorBounce()
        {
            var grenade=FieldFixture.Gun(kind:WeaponKind.Grenade,speed:20,range:60);var w=FieldFixture.World(grenade);long start=w.CurrentTick;
            FieldFixture.Cmd(w,fire:true,yaw:0);w.Step();FieldFixture.Wait(w,75);int booms=0;for(int i=0;i<w.ExplosionCapacity;i++)if(w.GetExplosion(i).Valid)booms++;Assert.That(booms,Is.Zero);
            FieldFixture.Wait(w,35);for(int i=0;i<w.ExplosionCapacity;i++){var e=w.GetExplosion(i);if(e.Valid){booms++;Assert.That(e.Tick-start,Is.GreaterThanOrEqualTo(100));}}
            Assert.That(booms,Is.EqualTo(1));Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(4));
        }
        [Test] public void ScrollCancelsChargeAndDoesNotTransferItsPowerOrAmmo()
        {
            var a=new UnitLoadout("field-test",500,0,10,20,100,80,0.1f,120,10,WeaponFireMode.Charge,2,0.6f,0.2f,1);
            var b=FieldFixture.Gun(speed:0);var w=FieldFixture.World(a.WithWeapons(a,b));FieldFixture.Cmd(w,fire:true);w.Step();FieldFixture.Wait(w,1);
            FieldFixture.Cmd(w,change:1);w.Step();Assert.That(w.GetUnit(0).ChargeFraction,Is.Zero);Assert.That(w.GetWeaponAmmo(0,0),Is.EqualTo(20));Assert.That(w.Counters.ShotsFired,Is.Zero);
        }
        [Test] public void UtilityCannotBePlacedInMidAirOrThroughSolidCover()
        {
            var pod=FieldFixture.Gun(kind:WeaponKind.RepairPod);var w=FieldFixture.World(pod);FieldFixture.Cmd(w,jump:true);w.Step();FieldFixture.Cmd(w,fire:true);w.Step();Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(5));
            for(int i=0;i<w.DeployableCapacity;i++)Assert.That(w.GetDeployable(i).Active,Is.False);
        }
        [Test] public void PlatformPlacementRequiresSupportAtTheItemAndDoesNotSpendChargesOverALedge()
        {
            var w = FieldFixture.World(FieldFixture.Gun(kind:WeaponKind.RepairPod), new[]{FieldFixture.Box(top:1.2f,depth:6)});
            FieldFixture.Cmd(w,x:1,jump:true); w.Step();
            for(int i=0;i<80 && w.GetUnit(0).X<0;i++){FieldFixture.Cmd(w,x:1);w.Step();}
            FieldFixture.Wait(w,60);
            Assert.That(w.GetUnit(0).Y,Is.EqualTo(1.2f).Within(0.001));
            FieldFixture.Cmd(w,fire:true); w.Step();
            Assert.That(w.GetDeployable(0).Active,Is.True);
            Assert.That(w.GetDeployable(0).Y,Is.EqualTo(1.24f).Within(0.001));
            FieldFixture.MoveTo(w,0,2.1f,0); FieldFixture.Wait(w,35);
            Assert.That(w.GetUnit(0).Grounded,Is.True);
            FieldFixture.Cmd(w,fire:true); w.Step();
            Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(4),"A placement over the ledge must fail without spending a charge.");
            Assert.That(w.GetDeployable(0).X,Is.LessThan(2),"The supported pod must not be replaced with a floating one.");
        }
        private static UnitLoadout LimitUtility(WeaponKind kind,float duration=120)
        {return new UnitLoadout("limit-test",500,0,10,12,20,3,.1f,120,10,WeaponFireMode.SingleShot,1,.1f,kind:kind,utilityRadius:kind==WeaponKind.Mine?.5f:20,utilityDuration:duration,blastRadius:kind==WeaponKind.Mine?4:0,splashDamage:kind==WeaponKind.Mine?40:0,utilityHealth:200,ammoResupplyDisabled:true);}
        private static int LiveCount(BattleSimulation w,int owner,WeaponKind kind)
        {int count=0;for(int i=0;i<w.DeployableCapacity;i++){var d=w.GetDeployable(i);if(d.Active&&d.OwnerSlot==owner&&d.Kind==kind)count++;}return count;}
        private static bool HasLiveId(BattleSimulation w,int id)
        {for(int i=0;i<w.DeployableCapacity;i++){var d=w.GetDeployable(i);if(d.Active&&d.Id==id)return true;}return false;}
        private static void PlaceLimitItem(BattleSimulation w,int slot=0,int change=0)
        {FieldFixture.Cmd(w,slot,fire:true,change:change);w.Step();FieldFixture.Wait(w,7);}
        [TestCase(WeaponKind.Mine,5)] [TestCase(WeaponKind.RepairPod,3)] [TestCase(WeaponKind.Radar,3)]
        public void OwnerDeployableLimitReplacesOldestInsteadOfAccumulatingForever(WeaponKind kind,int limit)
        {
            var w=FieldFixture.World(LimitUtility(kind));PlaceLimitItem(w);int first=w.GetDeployable(0).Id;
            for(int i=1;i<limit;i++)PlaceLimitItem(w);Assert.That(LiveCount(w,0,kind),Is.EqualTo(limit));Assert.That(HasLiveId(w,first),Is.True);
            PlaceLimitItem(w);Assert.That(LiveCount(w,0,kind),Is.EqualTo(limit));Assert.That(HasLiveId(w,first),Is.False);Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(12-limit-1));
        }
        [Test] public void EquippedCopiesShareTheirOwnersLimitButOtherPlayersHaveTheirOwn()
        {
            var radar=LimitUtility(WeaponKind.Radar);var w=FieldFixture.World(radar.WithWeapons(radar,radar,radar));
            for(int i=0;i<6;i++)PlaceLimitItem(w,change:i==0?0:1);
            Assert.That(LiveCount(w,0,WeaponKind.Radar),Is.EqualTo(3));for(int i=0;i<3;i++)Assert.That(w.GetWeaponAmmo(0,i),Is.EqualTo(10));
            for(int i=0;i<3;i++)PlaceLimitItem(w,1);Assert.That(LiveCount(w,1,WeaponKind.Radar),Is.EqualTo(3));Assert.That(LiveCount(w,0,WeaponKind.Radar),Is.EqualTo(3));
        }
        [Test] public void RespawningDoesNotGiveTheSamePlayerASeparatePlacementLimit()
        {
            var gun=FieldFixture.Gun(speed:0,damage:1000);var radar=LimitUtility(WeaponKind.Radar);var w=FieldFixture.World(gun.WithWeapons(gun,radar));
            for(int i=0;i<3;i++)PlaceLimitItem(w,change:i==0?1:0);int first=w.GetDeployable(0).Id;int oldLife=w.GetUnit(0).UnitId;
            FieldFixture.MoveTo(w,50,-14,0);FieldFixture.Cmd(w,50,fire:true,yaw:90);w.Step();Assert.That(w.GetUnit(0).IsAlive,Is.False);
            FieldFixture.Wait(w,20);Assert.That(w.GetUnit(0).IsAlive,Is.True);Assert.That(w.GetUnit(0).UnitId,Is.Not.EqualTo(oldLife));Assert.That(LiveCount(w,0,WeaponKind.Radar),Is.EqualTo(3));
            PlaceLimitItem(w,change:1);Assert.That(LiveCount(w,0,WeaponKind.Radar),Is.EqualTo(3));Assert.That(HasLiveId(w,first),Is.False);
        }
        [Test] public void ReplacementUsesPlacementAgeInsteadOfTheShortestRemainingLifetime()
        {
            var longRadar=LimitUtility(WeaponKind.Radar);var shortRadar=LimitUtility(WeaponKind.Radar,20);var w=FieldFixture.World(longRadar.WithWeapons(longRadar,shortRadar));
            PlaceLimitItem(w);int first=w.GetDeployable(0).Id;PlaceLimitItem(w,change:1);int second=w.GetDeployable(1).Id;PlaceLimitItem(w,change:1);PlaceLimitItem(w);
            Assert.That(HasLiveId(w,first),Is.False);Assert.That(HasLiveId(w,second),Is.True);Assert.That(LiveCount(w,0,WeaponKind.Radar),Is.EqualTo(3));
        }
        [Test] public void All100ControllersCanMaintainEveryKindAtItsLimitBeyondTheOldPoolSize()
        {
            var radar=LimitUtility(WeaponKind.Radar);var pod=LimitUtility(WeaponKind.RepairPod);var mine=LimitUtility(WeaponKind.Mine);var frame=radar.WithWeapons(radar,pod,mine);
            var decks=new UnitLoadout[100][];for(int i=0;i<100;i++)decks[i]=new[]{frame,frame};var w=new BattleSimulation(SimulationTests.Map(),FieldFixture.Config(),-1,decks);
            for(int kind=0;kind<3;kind++)for(int wave=0;wave<(kind==2?5:3);wave++){for(int slot=0;slot<100;slot++)FieldFixture.Cmd(w,slot,fire:true,change:kind>0&&wave==0?1:0);w.Step();FieldFixture.Wait(w,7);}
            int total=0;for(int i=0;i<w.DeployableCapacity;i++)if(w.GetDeployable(i).Active)total++;
            Assert.That(total,Is.EqualTo(1100));Assert.That(w.DeployableCapacity,Is.GreaterThanOrEqualTo(1102));for(int slot=0;slot<100;slot++){Assert.That(LiveCount(w,slot,WeaponKind.Radar),Is.EqualTo(3));Assert.That(LiveCount(w,slot,WeaponKind.RepairPod),Is.EqualTo(3));Assert.That(LiveCount(w,slot,WeaponKind.Mine),Is.EqualTo(5));}
        }
        [Test] public void ObjectsAboveTheOldPoolBoundaryCanBeLockedAndDamaged()
        {
            var gun=FieldFixture.Gun(speed:0,damage:80);var radar=LimitUtility(WeaponKind.Radar);var pod=LimitUtility(WeaponKind.RepairPod);var frame=gun.WithWeapons(gun,radar,pod);
            var decks=new UnitLoadout[100][];for(int i=0;i<100;i++)decks[i]=new[]{frame,frame};var w=new BattleSimulation(SimulationTests.Map(),FieldFixture.Config(),0,decks);FieldFixture.MoveTo(w,99,8,-10);
            for(int kind=0;kind<2;kind++)for(int wave=0;wave<3;wave++){for(int slot=0;slot<99;slot++)FieldFixture.Cmd(w,slot,fire:true,change:wave==0?1:0);w.Step();FieldFixture.Wait(w,7);}
            PlaceLimitItem(w,99,1);int target=-1;for(int i=0;i<w.DeployableCapacity;i++)if(w.GetDeployable(i).Active&&w.GetDeployable(i).OwnerSlot==99){target=i;break;}
            Assert.That(target,Is.GreaterThanOrEqualTo(512));FieldFixture.Cmd(w,change:1);w.Step();ObjectFixture.Aim(w,target);w.Step();Assert.That(w.GetDeployable(target).Health,Is.EqualTo(120));
        }
    }
}
