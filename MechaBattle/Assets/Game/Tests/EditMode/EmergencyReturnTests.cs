using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class EmergencyReturnTests
    {
        internal static SimulationConfig Config()
        {var c=FieldFixture.Config();c.emergencyReturnEnabled=true;c.emergencyReturnSeconds=.1f;c.returnHealthPerSecond=1200;c.returnShieldPerSecond=600;c.returnAmmoPerSecond=30;return c;}
        private static BattleSimulation World(UnitLoadout gear=null,SimulationConfig c=null)=>FieldFixture.World(gear??SupportFixture.Gear(),config:c??Config());
        private static void Return(BattleSimulation w){CarryFixture.Cmd(w,back:true);w.Step();FieldFixture.Wait(w,6);Assert.That(w.GetEmergencyReturn(0).InReserve,Is.True);}
        [Test] public void ReturnWaitsThenLeavesTheFieldWithoutDeathGaugeLossPointsOrCooldown()
        {var c=Config();c.strategy=StrategyTests.Rules();var w=World(c:c);double gauge=w.GetGauge(0);long ready=w.GetDeckReadyTick(0,0);CarryFixture.Cmd(w,back:true);w.Step();Assert.That(w.GetUnit(0).IsAlive,Is.True);Assert.That(w.GetEmergencyReturn(0).Channeling,Is.True);FieldFixture.Wait(w,6);Assert.That(w.GetUnit(0).IsAlive,Is.False);Assert.That(w.GetEmergencyReturn(0).InReserve,Is.True);Assert.That(w.GetSlotStats(0).Deaths,Is.Zero);Assert.That(w.GetGauge(0),Is.EqualTo(gauge));Assert.That(w.GetStrategyTeam(0).Points,Is.EqualTo(600));Assert.That(w.GetDeckReadyTick(0,0),Is.EqualTo(ready));}
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] public void MovementAttackJumpBoostOrAnotherReturnInterruptsTheChannel(int kind)
        {var w=World();CarryFixture.Cmd(w,back:true);w.Step();CarryFixture.Cmd(w,x:kind==0?1:0,fire:kind==1,jump:kind==2,boost:kind==3,back:kind==4);w.Step();Assert.That(w.GetEmergencyReturn(0).Channeling,Is.False);Assert.That(w.GetUnit(0).IsAlive,Is.True);}
        [Test] public void DamageOnTheCompletionTickCancelsInsteadOfGrantingAnEscape()
        {var w=World();CarryFixture.Cmd(w,back:true);w.Step();FieldFixture.Wait(w,4);CarryFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();Assert.That(w.GetEmergencyReturn(0).InReserve,Is.False);Assert.That(w.GetEmergencyReturn(0).Channeling,Is.False);Assert.That(w.GetUnit(0).Health,Is.LessThan(500));}
        [Test] public void ADeadReturningUnitIsStillAnOrdinaryCombatLoss()
        {var w=World(SupportFixture.Gear(damage:1000));CarryFixture.Cmd(w,back:true);CarryFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();Assert.That(w.GetUnit(0).IsAlive,Is.False);Assert.That(w.GetEmergencyReturn(0).InReserve,Is.False);Assert.That(w.GetSlotStats(0).Deaths,Is.EqualTo(1));}
        [Test] public void AirborneOrNuclearCarriersCannotStartReturn()
        {var w=World();CarryFixture.Cmd(w,jump:true);w.Step();CarryFixture.Cmd(w,back:true);w.Step();Assert.That(w.GetEmergencyReturn(0).Channeling,Is.False);w=CarryFixture.World(config:Config());CarryFixture.Request(w);CarryFixture.Pickup(w);CarryFixture.Cmd(w,back:true);w.Step();Assert.That(w.GetEmergencyReturn(0).Channeling,Is.False);Assert.That(w.IsCarryingNuclear(0),Is.True);}
        [Test] public void ReserveRepairIsGradualAndRedeploymentKeepsTheFrameWithoutFullHealthReset()
        {var w=World();CarryFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();float hurt=w.GetUnit(0).Health;Return(w);var before=w.GetUnit(0);Assert.That(before.Health,Is.GreaterThan(hurt).And.LessThan(500));Assert.That(w.RequestSpawn(0,1,-1,2),Is.False);Assert.That(w.RequestSpawn(0,0,-1,2),Is.True);w.Step();var after=w.GetUnit(0);Assert.That(after.IsAlive,Is.True);Assert.That(after.Health,Is.LessThan(500));Assert.That(after.DeckIndex,Is.EqualTo(before.DeckIndex));Assert.That(after.UnitId,Is.Not.EqualTo(before.UnitId));Assert.That(after.Z,Is.EqualTo(30).Within(.02));Assert.That(w.GetSlotStats(0).Deaths,Is.Zero);}
        [Test] public void NoSupplyPodChargesNeverRefillByReturningOrRedeploying()
        {var gear=SupportFixture.Gear(WeaponKind.RepairPod,noSupply:true,duration:10);var w=World(gear);CarryFixture.Cmd(w,fire:true);w.Step();int ammo=w.GetUnit(0).Ammo;Return(w);FieldFixture.Wait(w,60);Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(ammo));Assert.That(w.RequestSpawn(0,0,-1,1),Is.True);w.Step();Assert.That(w.GetUnit(0).Ammo,Is.EqualTo(ammo));}
        [Test] public void ReserveHealingCountsActualHpAndNewMatchesHaveNoReturnState()
        {var w=World();CarryFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();Return(w);FieldFixture.Wait(w,40);Assert.That(w.GetUnit(0).Health,Is.EqualTo(500));Assert.That(w.GetSlotStats(0).HealingReceived,Is.EqualTo(120).Within(.001));var fresh=World();Assert.That(fresh.GetEmergencyReturn(0).Channeling||fresh.GetEmergencyReturn(0).InReserve,Is.False);}
        [Test] public void FinishedAndDisabledReturnCannotChangeTheMatch()
        {var c=Config();c.maxDurationSeconds=20;var w=World(c:c);while(w.Phase==MatchPhase.Running)w.Step();var u=w.GetUnit(0);Assert.That(w.Submit(0,new UnitCommand(u.UnitId,w.CurrentTick,9999,0,0,0,0,false,false,false,false,false,emergencyReturn:true)),Is.False);Assert.That(w.GetEmergencyReturn(0).Channeling,Is.False);}
    }
}
