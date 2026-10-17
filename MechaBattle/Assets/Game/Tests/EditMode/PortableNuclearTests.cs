using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    internal static class CarryFixture
    {
        internal static void Cmd(BattleSimulation w,int slot=0,bool fire=false,bool interact=false,bool back=false,float x=0,float z=0,bool jump=false,bool boost=false,bool melee=false,int change=0,float yaw=90,bool aim=false,float ax=0,float ay=0,float az=0)
        {var u=w.GetUnit(slot);Assert.That(w.Submit(slot,new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,x,z,yaw,0,boost,fire,false,melee,interact,hasAimPoint:aim,aimTargetX:ax,aimTargetY:ay,aimTargetZ:az,jump:jump,weaponSwitchDelta:change,emergencyReturn:back)),Is.True);}
        internal static BattleSimulation World(UnitLoadout gear=null,SimulationConfig config=null)
        {config=config??FieldFixture.Config();if(config.strategy==null||!config.strategy.enabled)config.strategy=StrategyTests.Rules();return FieldFixture.World(gear??SupportFixture.Gear(damage:1000),config:config);}
        internal static StrategySnapshot Request(BattleSimulation w,int slot=0)
        {var u=w.GetUnit(slot);var home=w.GetBase(u.Team*3+1);Assert.That(w.RequestStrategy(slot,StrategyKind.Nuclear,home.X,home.Z,out var reason),Is.True,reason);return w.GetStrategy(u.Team*3,u.Team);}
        internal static void Pickup(BattleSimulation w,int slot=0)
        {var u=w.GetUnit(slot);var s=w.GetStrategy(u.Team*3,u.Team);FieldFixture.MoveTo(w,slot,s.X,s.Z);Cmd(w,slot,interact:true);w.Step();Assert.That(w.IsCarryingNuclear(slot),Is.True);}
        internal static void ArmAt(BattleSimulation w,float x,float z,int slot=0)
        {Request(w,slot);Pickup(w,slot);FieldFixture.MoveTo(w,slot,x,z);Cmd(w,slot);w.Step();Cmd(w,slot,fire:true);w.Step();Assert.That(w.GetStrategy(w.GetUnit(slot).Team*3,w.GetUnit(slot).Team).NuclearPhase,Is.EqualTo(NuclearPhase.Arming));}
        internal static void Blast(BattleSimulation w,int team=0)
        {int safety=1000;while(w.GetStrategy(team*3,team).Active&&!w.GetStrategy(team*3,team).Detonated&&safety-->0)w.Step();Assert.That(w.GetStrategy(team*3,team).Detonated,Is.True);}
    }
    public sealed class PortableNuclearTests
    {
        [Test] public void EnemyBaseAndArbitraryFrontlineRequestsAreRejectedWithoutSpending()
        {var w=CarryFixture.World();Assert.That(w.RequestStrategy(0,StrategyKind.Nuclear,30,0,out _),Is.False);Assert.That(w.RequestStrategy(0,StrategyKind.Nuclear,0,0,out _),Is.False);Assert.That(w.GetStrategyTeam(0).Points,Is.EqualTo(600));}
        [Test] public void RequestCreatesAFiniteDestructibleObjectWithoutCountdownOrRemoteDamage()
        {var w=CarryFixture.World();var s=CarryFixture.Request(w);Assert.That(s.NuclearPhase,Is.EqualTo(NuclearPhase.AwaitingPickup));Assert.That(s.Health,Is.EqualTo(2400));Assert.That(w.GetDeployable(s.DeviceIndex).Kind,Is.EqualTo(WeaponKind.StrategicNuclear));FieldFixture.Wait(w,100);Assert.That(w.GetStrategy(0,0).Detonated,Is.False);Assert.That(w.GetUnit(50).Health,Is.EqualTo(500));}
        [Test] public void DeviceMustBePickedUpNearbyAndOnlyByItsOwnTeam()
        {var w=CarryFixture.World();var s=CarryFixture.Request(w);CarryFixture.Cmd(w,interact:true);w.Step();Assert.That(w.IsCarryingNuclear(0),Is.False);FieldFixture.MoveTo(w,50,s.X,s.Z);CarryFixture.Cmd(w,50,interact:true);w.Step();Assert.That(w.IsCarryingNuclear(50),Is.False);CarryFixture.Pickup(w);Assert.That(w.IsCarryingNuclear(0),Is.True);}
        [Test] public void CarryFollowsMovementAndSuppressesNormalWeaponsMeleeAndShield()
        {var w=CarryFixture.World();CarryFixture.Request(w);CarryFixture.Pickup(w);int ammo=w.GetUnit(0).Ammo;CarryFixture.Cmd(w,fire:false,melee:true,x:1,boost:true);w.Step();var u=w.GetUnit(0);var s=w.GetStrategy(0,0);Assert.That(s.X,Is.EqualTo(u.X+.8f).Within(.001));Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(ammo));Assert.That(u.Guarding,Is.False);Assert.That(u.LastMeleeTick,Is.LessThan(0));}
        [Test] public void AirborneUseCannotArmAndHoldingInAirDoesNotAutoArmOnLanding()
        {var w=CarryFixture.World();CarryFixture.Request(w);CarryFixture.Pickup(w);CarryFixture.Cmd(w,jump:true);w.Step();Assert.That(w.GetUnit(0).Grounded,Is.False);for(int i=0;i<90;i++){CarryFixture.Cmd(w,fire:true);w.Step();}Assert.That(w.IsCarryingNuclear(0),Is.True);Assert.That(w.GetUnit(0).Grounded,Is.True);CarryFixture.Cmd(w);w.Step();CarryFixture.Cmd(w,fire:true);w.Step();Assert.That(w.GetStrategy(0,0).NuclearPhase,Is.EqualTo(NuclearPhase.Arming));}
        [Test] public void GroundActivationUsesTheCarriersLocationAndPreservesWeaponAmmo()
        {var w=CarryFixture.World();CarryFixture.ArmAt(w,0,0);var s=w.GetStrategy(0,0);Assert.That(s.X,Is.EqualTo(0).Within(.03));Assert.That(s.Z,Is.EqualTo(0).Within(.03));Assert.That(w.IsCarryingNuclear(0),Is.False);Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(12));CarryFixture.Blast(w);Assert.That(w.GetUnit(50).IsAlive,Is.False);}
        [Test] public void UnarmedEnemyNuclearPositionIsNotGloballyPublishedButArmingIsWarned()
        {var w=CarryFixture.World();CarryFixture.Request(w);Assert.That(w.GetStrategy(0,1).Active,Is.False);CarryFixture.Pickup(w);Assert.That(w.GetStrategy(0,1).Active,Is.False);FieldFixture.MoveTo(w,0,0,0);CarryFixture.Cmd(w,fire:true);w.Step();Assert.That(w.GetStrategy(0,1).NuclearPhase,Is.EqualTo(NuclearPhase.Arming));}
        [Test] public void ManualDropCanBePickedUpAgainWithoutAnotherPointPayment()
        {var w=CarryFixture.World();CarryFixture.Request(w);CarryFixture.Pickup(w);CarryFixture.Cmd(w,interact:true);w.Step();Assert.That(w.IsCarryingNuclear(0),Is.False);FieldFixture.Wait(w,30);CarryFixture.Pickup(w);Assert.That(w.GetStrategyTeam(0).Points,Is.Zero);}
        [Test] public void ACarrierDeathDropsTheSameDeviceForAnAllyInsteadOfExploding()
        {var w=CarryFixture.World();CarryFixture.Request(w);CarryFixture.Pickup(w);FieldFixture.MoveTo(w,0,0,0);FieldFixture.MoveTo(w,50,-8,0);CarryFixture.Cmd(w,50,fire:true,yaw:90);w.Step();Assert.That(w.GetUnit(0).IsAlive,Is.False);var s=w.GetStrategy(0,0);Assert.That(s.NuclearPhase,Is.EqualTo(NuclearPhase.AwaitingPickup));Assert.That(s.Detonated,Is.False);FieldFixture.Wait(w,30);CarryFixture.Pickup(w,1);Assert.That(w.IsCarryingNuclear(1),Is.True);}
        [Test] public void TwoAlliesCannotCarryTheSameObjectAtTheSameTime()
        {var w=CarryFixture.World();var s=CarryFixture.Request(w);FieldFixture.MoveTo(w,0,s.X,s.Z);FieldFixture.MoveTo(w,1,s.X,s.Z);CarryFixture.Cmd(w,interact:true);CarryFixture.Cmd(w,1,interact:true);w.Step();Assert.That(w.IsCarryingNuclear(0),Is.True);Assert.That(w.IsCarryingNuclear(1),Is.False);}
        [Test] public void EnemyFireCanDestroyTheGroundDeviceAndCancelItsArmedBlast()
        {var c=FieldFixture.Config();c.strategy=StrategyTests.Rules();c.strategy.nuclear.delaySeconds=10;var w=CarryFixture.World(SupportFixture.Gear(damage:3000),c);CarryFixture.ArmAt(w,-4,4);var s=w.GetStrategy(0,0);FieldFixture.MoveTo(w,0,-12,4);FieldFixture.MoveTo(w,50,8,4);var enemy=w.GetUnit(50);CarryFixture.Cmd(w,50,fire:true,aim:true,ax:s.X,ay:s.Y+.6f,az:s.Z,yaw:-90);w.Step();Assert.That(w.GetDeployable(s.DeviceIndex).Active,Is.False);FieldFixture.Wait(w,30);Assert.That(w.GetStrategy(0,0).Active,Is.False);Assert.That(w.GetSlotStats(0).Deaths,Is.Zero);}
        [Test] public void AnAllyCanFinishArmingAfterTheOriginalRequesterDiesOrRespawns()
        {var w=CarryFixture.World();CarryFixture.Request(w);CarryFixture.Pickup(w);FieldFixture.MoveTo(w,0,0,0);FieldFixture.MoveTo(w,50,-8,0);CarryFixture.Cmd(w,50,fire:true,yaw:90);w.Step();FieldFixture.Wait(w,30);CarryFixture.Pickup(w,1);CarryFixture.Cmd(w,1,fire:true);w.Step();Assert.That(w.GetStrategy(0,0).NuclearPhase,Is.EqualTo(NuclearPhase.Arming));Assert.That(w.GetStrategy(0,1).Active,Is.True);}
        [Test] public void KnockdownDropsACarriedDeviceEvenWhenItsCarrierSurvives()
        {
            var gun=new UnitLoadout("dp-test",500,100,10,20,1,80,.1f,120,10,downPointCapacity:600,fireDownPoints:700);
            var w=CarryFixture.World(gun);CarryFixture.Request(w);CarryFixture.Pickup(w);FieldFixture.MoveTo(w,0,0,0);FieldFixture.MoveTo(w,50,-8,0);
            for(int i=0;i<6;i++){CarryFixture.Cmd(w,50,fire:true,yaw:90);w.Step();}
            Assert.That(w.GetUnit(0).IsAlive,Is.True);Assert.That(w.IsCarryingNuclear(0),Is.False);Assert.That(w.GetStrategy(0,0).NuclearPhase,Is.EqualTo(NuclearPhase.AwaitingPickup));
        }
        [Test] public void EnemyPhysicalDeviceVisibilityNeedsItsOwnViewAndDoesNotCreateAMapReport()
        {
            var w=CarryFixture.World();CarryFixture.Request(w);Assert.That(w.GetStrategyForWorld(0,50).Active,Is.False);
            CarryFixture.Cmd(w,50,yaw:-90);w.Step();Assert.That(w.GetStrategyForWorld(0,50).Active,Is.True);Assert.That(w.GetStrategy(0,1).Active,Is.False);
        }
    }
}
