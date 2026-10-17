using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    internal static class SupportFixture
    {
        internal static UnitLoadout Gear(WeaponKind kind=WeaponKind.Ranged,float damage=220,float shield=100,bool noSupply=false,float shieldRate=0,float ammoRate=0,float range=80,float duration=3,float speed=0)
        {
            bool place=kind==WeaponKind.RepairPod||kind==WeaponKind.Radar;
            return new UnitLoadout("support-test",500,shield,10,12,damage,range,0.15f,120,10,place?WeaponFireMode.SingleShot:WeaponFireMode.FullAutomatic,
                place?1:12,0.6f,kind:kind,projectileSpeed:speed,redLockRange:place||kind==WeaponKind.RepairGun?0:24,
                utilityRadius:place?20:0,utilityDuration:place?duration:0,ammoResupplyDisabled:noSupply,podShieldPerSecond:shieldRate,podAmmoPerSecond:ammoRate);
        }
        internal static BattleSimulation World(UnitLoadout frame,SimulationConfig config=null)=>FieldFixture.World(frame,config:config);
        internal static List<CombatEventSnapshot> Events(BattleSimulation w)
        {var result=new List<CombatEventSnapshot>();for(long n=Math.Max(1,w.CombatEventSequence-w.CombatEventCapacity+1);n<=w.CombatEventSequence;n++)if(w.TryGetCombatEvent(n,out var e))result.Add(e);return result;}
        internal static void WoundFriend(BattleSimulation w){FieldFixture.MoveTo(w,1,0,0);FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();Assert.That(w.GetUnit(1).Health,Is.LessThan(500));}
    }
    public sealed class SupportFeedbackTests
    {
        [Test] public void ActualHitReportsCappedHealthAndShieldDamageOnlyToItsOwner()
        {
            var w=SupportFixture.World(SupportFixture.Gear(damage:1000));int id=w.GetUnit(0).UnitId;FieldFixture.Cmd(w,fire:true);w.Step();
            var events=SupportFixture.Events(w);var hit=events.Single(e=>e.Kind==CombatEventKind.Hit&&e.SourceSlot==0);
            Assert.That(hit.HealthAmount,Is.EqualTo(500));Assert.That(hit.ShieldAmount,Is.EqualTo(100));Assert.That(hit.SourceUnitId,Is.EqualTo(id));
            Assert.That(events.Count(e=>e.Kind==CombatEventKind.Destroyed&&e.TargetSlot==50),Is.EqualTo(1));
            Assert.That(events.Single(e=>e.Kind==CombatEventKind.Destroyed&&e.TargetSlot==50).SourceSlot,Is.EqualTo(0));
            FieldFixture.Wait(w,20);Assert.That(SupportFixture.Events(w).Count(e=>e.Kind==CombatEventKind.Destroyed&&e.TargetSlot==50),Is.EqualTo(1));
        }
        [Test] public void AShotThatMissesNeverCreatesAHitOrKill()
        {var w=SupportFixture.World(SupportFixture.Gear());FieldFixture.Cmd(w,fire:true,yaw:0);w.Step();Assert.That(w.CombatEventSequence,Is.Zero);}
        [Test] public void PureShieldHitCreatesASeparateShieldOnlyConfirmation()
        {
            var w=SupportFixture.World(SupportFixture.Gear(damage:40));FieldFixture.Cmd(w,fire:true);w.Step();
            var e=SupportFixture.Events(w).Single(x=>x.Kind==CombatEventKind.Hit);Assert.That(e.HealthAmount,Is.Zero);Assert.That(e.ShieldAmount,Is.EqualTo(40));
        }
        [Test] public void SimultaneousKillIsAttributedOnceToTheLargestHpContributor()
        {
            var a=SupportFixture.Gear(damage:180);var b=SupportFixture.Gear(damage:480);var w=SupportFixture.World(a.WithWeapons(a,b));FieldFixture.MoveTo(w,1,-6,0);
            FieldFixture.Cmd(w,fire:true);FieldFixture.Cmd(w,1,fire:true,change:1);w.Step();var all=SupportFixture.Events(w);
            var kill=all.Single(e=>e.Kind==CombatEventKind.Destroyed&&e.TargetSlot==50);Assert.That(kill.SourceSlot,Is.EqualTo(1));
            Assert.That(all.Where(e=>e.Kind==CombatEventKind.Hit&&e.TargetSlot==50).Sum(e=>e.HealthAmount),Is.EqualTo(500).Within(0.001));
        }
        [Test] public void InFlightHitKeepsOriginalSpawnIdentityAfterOwnerRespawns()
        {
            var slow=SupportFixture.Gear(damage:1000,speed:6);var instant=SupportFixture.Gear(damage:1000);var w=SupportFixture.World(slow.WithWeapons(slow,instant));int owner=w.GetUnit(0).UnitId;
            FieldFixture.Cmd(w,fire:true);w.Step();FieldFixture.Cmd(w,50,fire:true,change:1,yaw:-90);w.Step();FieldFixture.Wait(w,150);
            Assert.That(w.GetUnit(0).UnitId,Is.Not.EqualTo(owner));
            Assert.That(SupportFixture.Events(w).Any(e=>e.Kind==CombatEventKind.Hit&&e.SourceSlot==0&&e.SourceUnitId==owner),Is.True);
        }
        [Test] public void RepairFaHealsOnlyFriendlyHpAndUsesAmmoWithoutRestoringShieldOrDp()
        {
            var gun=SupportFixture.Gear();var repair=SupportFixture.Gear(WeaponKind.RepairGun,45);var w=SupportFixture.World(gun.WithWeapons(gun,repair));SupportFixture.WoundFriend(w);
            float hp=w.GetUnit(1).Health,shield=w.GetUnit(1).Shield,down=w.GetUnit(1).DownPoints;
            FieldFixture.Cmd(w,fire:true,change:1);w.Step();var u=w.GetUnit(1);
            Assert.That(u.Health,Is.EqualTo(hp+45));Assert.That(u.Shield,Is.EqualTo(shield));Assert.That(u.DownPoints,Is.EqualTo(down));Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(11));
            Assert.That(SupportFixture.Events(w).Single(e=>e.Kind==CombatEventKind.Healed).TargetSlot,Is.EqualTo(1));
        }
        [Test] public void HoldingRepairFaPulsesWithoutRepeatedClicksAndClampsToFullHp()
        {
            var gun=SupportFixture.Gear();var repair=SupportFixture.Gear(WeaponKind.RepairGun,45);var w=SupportFixture.World(gun.WithWeapons(gun,repair));SupportFixture.WoundFriend(w);
            for(int i=0;i<65;i++){FieldFixture.Cmd(w,fire:true,change:i==0?1:0);w.Step();}
            Assert.That(w.GetUnit(1).Health,Is.EqualTo(500));Assert.That(w.GetUnit(0).Ammo,Is.LessThan(8));
            Assert.That(SupportFixture.Events(w).Where(e=>e.Kind==CombatEventKind.Healed).Sum(e=>e.HealthAmount),Is.EqualTo(120).Within(0.001));
        }
        [Test] public void RepairRayCannotHealEnemiesOrTheShooter()
        {
            var repair=SupportFixture.Gear(WeaponKind.RepairGun,45);var w=SupportFixture.World(repair);FieldFixture.Cmd(w,fire:true);w.Step();
            Assert.That(w.GetUnit(50).Health,Is.EqualTo(500));Assert.That(w.GetUnit(0).Health,Is.EqualTo(500));Assert.That(w.CombatEventSequence,Is.Zero);
        }
        [Test] public void AFullHealthAllyDoesNotCreateAHealingConfirmation()
        {
            var w=SupportFixture.World(SupportFixture.Gear(WeaponKind.RepairGun,45));FieldFixture.MoveTo(w,1,0,0);FieldFixture.Cmd(w,fire:true);w.Step();Assert.That(w.CombatEventSequence,Is.Zero);
        }
        [Test] public void RepairAimTraceSeesFriendlyBodyAndObeysWallsAndPhysicalRange()
        {
            var repair=SupportFixture.Gear(WeaponKind.RepairGun,45);var w=SupportFixture.World(repair);FieldFixture.MoveTo(w,1,0,0);
            Assert.That(w.TraceAim(0,-8,1.3f,0,1,0,0,65).TargetSlot,Is.EqualTo(1));Assert.That(w.TraceAim(0,-8,1.3f,0,1,0,0,3).TargetSlot,Is.EqualTo(-1));
            var wall=FieldFixture.World(repair,new[]{FieldFixture.Box()});FieldFixture.Cmd(wall,fire:true);wall.Step();Assert.That(wall.CombatEventSequence,Is.Zero);Assert.That(wall.TraceAim(0,-8,1.3f,0,1,0,0,65).TargetSlot,Is.EqualTo(-1));
        }
        [Test] public void PodRestoresShieldAndEligibleReserveAmmoButCannotRefillItself()
        {
            var gun=SupportFixture.Gear();var pod=SupportFixture.Gear(WeaponKind.RepairPod,10,noSupply:true,shieldRate:100,ammoRate:10);var w=SupportFixture.World(gun.WithWeapons(gun,pod));
            FieldFixture.Cmd(w,fire:true);FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();float hp=w.GetUnit(0).Health,shield=w.GetUnit(0).Shield;
            FieldFixture.Cmd(w,fire:true,change:1);w.Step();FieldFixture.Wait(w,70);
            Assert.That(w.GetUnit(0).Health-hp,Is.LessThan(20));Assert.That(w.GetUnit(0).Shield-shield,Is.GreaterThan(90));
            Assert.That(w.GetWeaponAmmo(0,0),Is.EqualTo(12));Assert.That(w.GetWeaponAmmo(0,1),Is.EqualTo(11));
        }
        [Test] public void HomeSupplyHonoursNoResupplyForHolsteredAndActiveUtility()
        {
            var c=FieldFixture.Config();c.homeSupplyRadius=100;var gun=SupportFixture.Gear();var pod=SupportFixture.Gear(WeaponKind.RepairPod,10,noSupply:true);var radar=SupportFixture.Gear(WeaponKind.Radar,noSupply:true);
            var w=SupportFixture.World(gun.WithWeapons(gun,pod,radar),c);FieldFixture.Cmd(w,fire:true,change:1);w.Step();FieldFixture.Cmd(w,fire:true,change:1);w.Step();FieldFixture.Wait(w,100);
            Assert.That(w.GetWeaponAmmo(0,1),Is.EqualTo(11));Assert.That(w.GetWeaponAmmo(0,2),Is.EqualTo(11));
        }
        [Test] public void PodCannotHealAnEnemyOrPassThroughCover()
        {
            var pod=SupportFixture.Gear(WeaponKind.RepairPod,10,shieldRate:100,ammoRate:10);var gun=SupportFixture.Gear();var w=SupportFixture.World(gun.WithWeapons(gun,pod));
            FieldFixture.Cmd(w,fire:true);w.Step();float hp=w.GetUnit(50).Health;FieldFixture.Cmd(w,fire:true,change:1);w.Step();FieldFixture.Wait(w,90);Assert.That(w.GetUnit(50).Health,Is.EqualTo(hp));
            var blocked=FieldFixture.World(gun.WithWeapons(gun,pod),new[]{FieldFixture.Box()});FieldFixture.MoveTo(blocked,1,-6,14);FieldFixture.MoveTo(blocked,1,3,14);FieldFixture.MoveTo(blocked,1,3,0);FieldFixture.Cmd(blocked,50,fire:true,yaw:-90);blocked.Step();
            float friendHp=blocked.GetUnit(1).Health,friendShield=blocked.GetUnit(1).Shield;Assert.That(friendHp,Is.LessThan(500));
            FieldFixture.Cmd(blocked,fire:true,change:1);blocked.Step();FieldFixture.Wait(blocked,90);Assert.That(blocked.GetUnit(1).Health,Is.EqualTo(friendHp));Assert.That(blocked.GetUnit(1).Shield,Is.EqualTo(friendShield));
        }
        [Test] public void MixedFrontShieldAndRearHpHitsHaveCorrectDamageAndOneKill()
        {
            var c=FieldFixture.Config();c.directionalShieldEnabled=true;c.shieldHalfAngleDegrees=65;c.shieldRecoverySeconds=0.35f;
            var front=SupportFixture.Gear(damage:40);var rear=SupportFixture.Gear(damage:600);var w=SupportFixture.World(front.WithWeapons(front,rear),c);FieldFixture.MoveTo(w,1,12,0);
            FieldFixture.Cmd(w,50,yaw:-90);FieldFixture.Cmd(w,fire:true);FieldFixture.Cmd(w,1,fire:true,change:1,yaw:-90);w.Step();var events=SupportFixture.Events(w);
            var shield=events.Single(e=>e.Kind==CombatEventKind.Hit&&e.SourceSlot==0);var hp=events.Single(e=>e.Kind==CombatEventKind.Hit&&e.SourceSlot==1);
            Assert.That(shield.HealthAmount,Is.Zero);Assert.That(shield.ShieldAmount,Is.EqualTo(40));Assert.That(hp.HealthAmount,Is.EqualTo(500));Assert.That(hp.ShieldAmount,Is.Zero);
            Assert.That(events.Single(e=>e.Kind==CombatEventKind.Destroyed&&e.TargetSlot==50).SourceSlot,Is.EqualTo(1));
        }
        [Test] public void EventStreamRejectsUnknownSequences()
        {
            var gun=SupportFixture.Gear(damage:1);var w=SupportFixture.World(gun);Assert.That(w.TryGetCombatEvent(0,out _),Is.False);Assert.That(w.TryGetCombatEvent(1,out _),Is.False);
            Assert.That(w.TryGetCombatEvent(-1,out _),Is.False);
        }
    }
}
