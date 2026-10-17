using System;
using System.Linq;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    internal static class ObjectFixture
    {
        internal static UnitLoadout Utility(WeaponKind kind=WeaponKind.RepairPod,float health=200,float duration=100)
        {return new UnitLoadout("field-test",500,0,10,5,20,3,0.2f,120,10,WeaponFireMode.SingleShot,1,0.6f,kind:kind,utilityRadius:kind==WeaponKind.Mine?2.5f:20,utilityDuration:duration,
            blastRadius:kind==WeaponKind.Mine?4:0,splashDamage:kind==WeaponKind.Mine?40:0,utilityHealth:health,ammoResupplyDisabled:true);}
        internal static BattleSimulation World(WeaponKind kind=WeaponKind.RepairPod,float health=200,float duration=100,float speed=0,float damage=80,SimulationConfig config=null)
        {var gun=FieldFixture.Gun(speed:speed,damage:damage);return FieldFixture.World(gun.WithWeapons(gun,Utility(kind,health,duration)),config:config);}
        internal static int Place(BattleSimulation w,int slot=50,float yaw=-90)
        {FieldFixture.Cmd(w,slot,fire:true,change:1,yaw:yaw);w.Step();for(int i=0;i<w.DeployableCapacity;i++)if(w.GetDeployable(i).Active&&w.GetDeployable(i).OwnerSlot==slot)return i;throw new Exception("Expected placed object");}
        internal static void Aim(BattleSimulation w,int index,int slot=0,bool fire=true,bool locked=true,int id=-1)
        {
            var u=w.GetUnit(slot);var d=w.GetDeployable(index);
            Assert.That(w.Submit(slot,new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,90,0,false,fire,false,false,false,
                hasAimPoint:true,aimTargetX:d.X,aimTargetY:d.Y+d.HitHeight*0.5f,aimTargetZ:d.Z,lockedDeployableIndex:locked?index:-1,lockedDeployableId:locked?(id<0?d.Id:id):0)),Is.True);
        }
    }
    public sealed class ObjectTargetTests
    {
        [Test] public void EnemyPodAndRadarHaveDurabilityAndOwnPlacementIdentity()
        {
            foreach(var kind in new[]{WeaponKind.RepairPod,WeaponKind.Radar})
            {var w=ObjectFixture.World(kind);int index=ObjectFixture.Place(w);var d=w.GetDeployable(index);Assert.That(d.Id,Is.GreaterThan(0));Assert.That(d.Health,Is.EqualTo(200));Assert.That(w.IsDeployableYellowLocked(0,index,d.Id),Is.True);Assert.That(w.IsDeployableYellowLocked(50,index,d.Id),Is.False);}
        }
        [Test] public void ObjectLocksRespectPhysicalRangeLosAndReplacementIdentity()
        {
            var w=ObjectFixture.World();int i=ObjectFixture.Place(w);var d=w.GetDeployable(i);Assert.That(w.IsDeployableYellowLocked(0,i,d.Id+1),Is.False);
            FieldFixture.MoveTo(w,0,-90,0);Assert.That(w.IsDeployableYellowLocked(0,i,d.Id),Is.False);
            var gun=FieldFixture.Gun(speed:0);var blocked=FieldFixture.World(gun.WithWeapons(gun,ObjectFixture.Utility()),new[]{FieldFixture.Box()});int j=ObjectFixture.Place(blocked);Assert.That(blocked.IsDeployableYellowLocked(0,j,blocked.GetDeployable(j).Id),Is.False);
        }
        [Test] public void ObjectPrecisionDoesNotSteerWrongAimOrCountAsAUnitWarning()
        {
            var w=ObjectFixture.World();int i=ObjectFixture.Place(w);var d=w.GetDeployable(i);var u=w.GetUnit(0);
            w.Submit(0,new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,0,0,false,true,false,false,false,lockedDeployableIndex:i,lockedDeployableId:d.Id));w.Step();
            Assert.That(w.GetDeployable(i).Health,Is.EqualTo(200));Assert.That(w.GetUnit(50).IncomingLockCount,Is.Zero);
        }
        [Test] public void MuzzleTraceFindsObjectAndInstantShotsDamageItsOwnHpOnly()
        {
            var w=ObjectFixture.World();int i=ObjectFixture.Place(w);var d=w.GetDeployable(i);var u=w.GetUnit(0);float dx=d.X-u.X,dy=d.Y+d.HitHeight*0.5f-u.Y-w.ShotHeight,dz=d.Z-u.Z;
            var p=w.TraceAim(0,u.X,u.Y+w.ShotHeight,u.Z,dx,dy,dz,80);Assert.That(p.DeployableIndex,Is.EqualTo(i));Assert.That(p.DeployableId,Is.EqualTo(d.Id));
            ObjectFixture.Aim(w,i);w.Step();Assert.That(w.GetDeployable(i).Health,Is.EqualTo(120));Assert.That(w.GetUnit(50).Health,Is.EqualTo(500));
            Assert.That(SupportFixture.Events(w).Single(e=>e.Kind==CombatEventKind.ObjectHit).HealthAmount,Is.EqualTo(80));
        }
        [Test] public void ObjectDestructionHasOneSeparateNotificationAndClearsTheLock()
        {
            var w=ObjectFixture.World(health:40);int i=ObjectFixture.Place(w);int id=w.GetDeployable(i).Id;ObjectFixture.Aim(w,i);w.Step();
            Assert.That(w.GetDeployable(i).Active,Is.False);Assert.That(w.IsDeployableYellowLocked(0,i,id),Is.False);
            var e=SupportFixture.Events(w);Assert.That(e.Count(x=>x.Kind==CombatEventKind.ObjectDestroyed),Is.EqualTo(1));Assert.That(e.Count(x=>x.Kind==CombatEventKind.Destroyed),Is.Zero);
            Assert.That(e.Single(x=>x.Kind==CombatEventKind.ObjectHit).HealthAmount,Is.EqualTo(40));
        }
        [Test] public void FlyingProjectilesTakeTimeToReachObjects()
        {
            var w=ObjectFixture.World(speed:20);int i=ObjectFixture.Place(w);ObjectFixture.Aim(w,i);w.Step();Assert.That(w.GetDeployable(i).Health,Is.EqualTo(200));FieldFixture.Wait(w,45);Assert.That(w.GetDeployable(i).Health,Is.EqualTo(120));
        }
        [Test] public void FriendlyObjectsCannotBeShotOrLockedByTheirOwner()
        {
            var w=ObjectFixture.World();int i=ObjectFixture.Place(w,0,90);FieldFixture.Cmd(w,change:1);w.Step();ObjectFixture.Aim(w,i);w.Step();
            Assert.That(w.GetDeployable(i).Health,Is.EqualTo(200));Assert.That(w.IsDeployableYellowLocked(0,i,w.GetDeployable(i).Id),Is.False);
        }
        [Test] public void ExpirationInvalidatesLockExactlyAtItsDeadline()
        {
            var w=ObjectFixture.World(duration:1);int i=ObjectFixture.Place(w);var d=w.GetDeployable(i);FieldFixture.Wait(w,49);
            Assert.That(w.GetDeployable(i).Active,Is.False);Assert.That(w.IsDeployableYellowLocked(0,i,d.Id),Is.False);
        }
        [Test] public void ReplacingAnObjectNeverTransfersItsOldLockOrPendingDamage()
        {
            var w=ObjectFixture.World();int i=ObjectFixture.Place(w);var old=w.GetDeployable(i);FieldFixture.Wait(w,40);
            for(int n=0;n<2;n++){FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();FieldFixture.Wait(w,40);}
            Assert.That(w.GetDeployable(i).Id,Is.EqualTo(old.Id),"Three objects must coexist before replacement.");
            ObjectFixture.Aim(w,i);FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();var newer=w.GetDeployable(i);
            Assert.That(newer.Id,Is.Not.EqualTo(old.Id));Assert.That(newer.Health,Is.EqualTo(200));Assert.That(w.IsDeployableYellowLocked(0,i,old.Id),Is.False);
        }
        [Test] public void DestroyedObjectSlotCanBeReusedWithANewIdentity()
        {
            var w=ObjectFixture.World(health:40);int i=ObjectFixture.Place(w);int old=w.GetDeployable(i).Id;ObjectFixture.Aim(w,i);w.Step();FieldFixture.Wait(w,40);FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();
            Assert.That(w.GetDeployable(i).Active,Is.True);Assert.That(w.GetDeployable(i).Id,Is.Not.EqualTo(old));Assert.That(w.IsDeployableYellowLocked(0,i,old),Is.False);
        }
        [Test] public void SimultaneousObjectHitsCreditOneDestroyerAndCapActualDamage()
        {
            var w=ObjectFixture.World(health:100);FieldFixture.MoveTo(w,1,-6,2);int i=ObjectFixture.Place(w);ObjectFixture.Aim(w,i);ObjectFixture.Aim(w,i,1);w.Step();var events=SupportFixture.Events(w);
            Assert.That(events.Count(e=>e.Kind==CombatEventKind.ObjectDestroyed),Is.EqualTo(1));Assert.That(events.Where(e=>e.Kind==CombatEventKind.ObjectHit).Sum(e=>e.HealthAmount),Is.EqualTo(100).Within(0.001));Assert.That(events.Single(e=>e.Kind==CombatEventKind.ObjectDestroyed).SourceSlot,Is.EqualTo(0));
        }
        [Test] public void BazookaSplashDamagesNearbyEquipmentWithoutDoubleDamageOnTheDirectObject()
        {
            var gun=new UnitLoadout("field-test",500,0,10,10,80,80,0.1f,120,10,WeaponFireMode.SingleShot,1,0.6f,blastRadius:4,splashDamage:40,projectileSpeed:40,redLockRange:24);
            var w=FieldFixture.World(gun.WithWeapons(gun,ObjectFixture.Utility()));int first=ObjectFixture.Place(w);FieldFixture.MoveTo(w,51,8,2);int second=ObjectFixture.Place(w,51);ObjectFixture.Aim(w,first);w.Step();FieldFixture.Wait(w,30);
            Assert.That(w.GetDeployable(first).Health,Is.EqualTo(120));Assert.That(w.GetDeployable(second).Health,Is.LessThan(200).And.GreaterThan(160));
        }
        [Test] public void ManualMeleeDamagesEnemyEquipmentThroughTheCommonAttackTiming()
        {
            var c=FieldFixture.Config();c.meleeAttacks=new MeleeAttackDefinition[5];for(int k=0;k<5;k++)c.meleeAttacks[k]=new MeleeAttackDefinition{style=(MeleeStyle)k,damage=80,downPoints=1,range=3.5f,halfAngleDegrees=65,windupSeconds=0.1f,recoverySeconds=0.2f,cooldownSeconds=0.3f,moveMultiplier=1,comboSteps=1,maximumTargets=3,minimumChargeSeconds=k==4?0.4f:0,fullChargeSeconds=k==4?1:0,minimumChargeMultiplier=k==4?0.3f:0};
            var blade=FieldFixture.Gun(kind:WeaponKind.Melee);var w=FieldFixture.World(blade.WithWeapons(blade,ObjectFixture.Utility()),config:c);int i=ObjectFixture.Place(w);FieldFixture.MoveTo(w,0,4.5f,0);
            FieldFixture.Cmd(w,melee:true);w.Step();Assert.That(w.GetDeployable(i).Health,Is.EqualTo(200));FieldFixture.Wait(w,6);Assert.That(w.GetDeployable(i).Health,Is.EqualTo(120));
        }
        [Test] public void ShootingAnArmedMineBeforeItsTriggerDestroysItWithoutDetonation()
        {
            var w=ObjectFixture.World(WeaponKind.Mine,health:40);int i=ObjectFixture.Place(w);FieldFixture.Wait(w,45);ObjectFixture.Aim(w,i);w.Step();
            Assert.That(w.GetDeployable(i).Active,Is.False);Assert.That(SupportFixture.Events(w).Count(e=>e.Kind==CombatEventKind.ObjectDestroyed),Is.EqualTo(1));Assert.That(Enumerable.Range(0,w.ExplosionCapacity).Any(x=>w.GetExplosion(x).Valid),Is.False);
        }
        [Test] public void InvalidOrConflictingObjectCommandsAreRejected()
        {
            var w=ObjectFixture.World();int i=ObjectFixture.Place(w);var u=w.GetUnit(0);int id=w.GetDeployable(i).Id;
            Assert.That(w.Submit(0,new UnitCommand(u.UnitId,w.CurrentTick,1,0,0,90,0,false,false,false,false,false,lockedDeployableIndex:w.DeployableCapacity,lockedDeployableId:id)),Is.False);
            Assert.That(w.Submit(0,new UnitCommand(u.UnitId,w.CurrentTick,1,0,0,90,0,false,false,false,false,false,lockedDeployableIndex:i,lockedDeployableId:0)),Is.False);
            Assert.That(w.Submit(0,new UnitCommand(u.UnitId,w.CurrentTick,1,0,0,90,0,false,false,false,false,false,lockedTargetSlot:50,lockedTargetUnitId:w.GetUnit(50).UnitId,lockedDeployableIndex:i,lockedDeployableId:id)),Is.False);
        }
        [Test] public void TriggerAdapterPreservesObjectLockIdentity()
        {
            var w=ObjectFixture.World();int i=ObjectFixture.Place(w);var u=w.GetUnit(0);var c=new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,90,0,false,false,false,false,false,lockedDeployableIndex:i,lockedDeployableId:w.GetDeployable(i).Id);
            var a=new CombatInputAdapter().Adapt(u,w.GetActiveWeapon(0),c);Assert.That(a.LockedDeployableIndex,Is.EqualTo(i));Assert.That(a.LockedDeployableId,Is.EqualTo(c.LockedDeployableId));
        }
    }
}
