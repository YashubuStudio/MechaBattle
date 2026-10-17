using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class BoostMeleeTests
    {
        private static SimulationConfig Config()
        {
            var c=FieldFixture.Config();c.maxDurationSeconds=1000;c.boostOverheatEnabled=true;c.seamlessMeleeEnabled=true;
            c.boostDrainPerSecond=17.5f;c.boostRecoveryPerSecond=100;c.boostOverheatRecoveryPerSecond=20;
            c.meleeAttacks=new MeleeAttackDefinition[5];
            for(int i=0;i<5;i++)c.meleeAttacks[i]=new MeleeAttackDefinition{style=(MeleeStyle)i,damage=i==0?760:i==2?2280:850,downPoints=900,range=3.8f,halfAngleDegrees=65,windupSeconds=i==0?0.06f:0.12f,recoverySeconds=i==0?0.14f:0.22f,cooldownSeconds=i==0?0.15f:0.2f,lungeMeters=i==2?5:0.8f,boostCost=i==0?0:i==2?30:20,moveMultiplier=0.35f,comboSteps=i==1||i==3?2:1,maximumTargets=3,minimumChargeSeconds=i==4?0.4f:0,fullChargeSeconds=i==4?1.4f:0,minimumChargeMultiplier=i==4?0.3f:0};
            return c;
        }
        private static UnitLoadout Blade()=>new UnitLoadout("boost-melee",10000,0,10,1,760,3.8f,0.35f,120,10,kind:WeaponKind.Melee);
        private static void Input(BattleSimulation w,float x=0,bool boost=false,bool jump=false,bool melee=false,MeleeStyle style=MeleeStyle.Normal)
        {var u=w.GetUnit(0);Assert.That(w.Submit(0,new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,x,0,90,0,boost,false,false,melee,false,meleeStyle:style,jump:jump)),Is.True);}
        private static void Drain(BattleSimulation w)
        {for(int i=0;i<400&&!w.GetUnit(0).BoostOverheated;i++){Input(w,i/10%2==0?1:-1,true);w.Step();}Assert.That(w.GetUnit(0).BoostOverheated,Is.True);Assert.That(w.GetUnit(0).Boost,Is.Zero);}
        [Test] public void MovingBoostUsesHalfOldDrainAndNormalRecoveryIsFiveTimesFaster()
        {
            var w=SupportFixture.World(SupportFixture.Gear(),Config());for(int i=0;i<50;i++){Input(w,i/10%2==0?1:-1,true);w.Step();}
            Assert.That(w.GetUnit(0).Boost,Is.EqualTo(82.5f).Within(0.002));Assert.That(w.GetUnit(0).BoostMoving,Is.True);
            Input(w);w.Step();Assert.That(w.GetUnit(0).Boost,Is.EqualTo(84.5f).Within(0.002));Assert.That(w.GetUnit(0).BoostOverheated,Is.False);
        }
        [Test] public void EmptyBoostOverheatsAndHoldingDashOnlyWalksWhileRecoveringAtOldRate()
        {
            var w=SupportFixture.World(SupportFixture.Gear(),Config());Drain(w);float x=w.GetUnit(0).X;Input(w,1,true);w.Step();var u=w.GetUnit(0);
            Assert.That(u.BoostOverheated,Is.True);Assert.That(u.BoostMoving,Is.False);Assert.That(u.Boost,Is.EqualTo(0.4f).Within(0.001));Assert.That(u.X-x,Is.EqualTo(0.2f).Within(0.001));
        }
        [Test] public void OverheatBlocksJumpAndBoostCostMeleeEvenAfterPartialRecovery()
        {
            var w=SupportFixture.World(Blade(),Config());Drain(w);FieldFixture.Wait(w,100);Assert.That(w.GetUnit(0).Boost,Is.EqualTo(40).Within(0.01));
            Input(w,jump:true,melee:true,style:MeleeStyle.Dash);w.Step();Assert.That(w.GetUnit(0).Y,Is.Zero);Assert.That(w.GetUnit(0).MeleePhase,Is.LessThan(0));Assert.That(w.GetUnit(0).BoostOverheated,Is.True);
        }
        [Test] public void OverheatUnlocksOnlyAtFullCapacity()
        {
            var w=SupportFixture.World(SupportFixture.Gear(),Config());Drain(w);FieldFixture.Wait(w,249);Assert.That(w.GetUnit(0).BoostOverheated,Is.True);w.Step();Assert.That(w.GetUnit(0).Boost,Is.EqualTo(100));Assert.That(w.GetUnit(0).BoostOverheated,Is.False);
            Input(w,1,true);w.Step();Assert.That(w.GetUnit(0).BoostMoving,Is.True);
        }
        [Test] public void ZeroCostNormalMeleeStillWorksDuringOverheat()
        {var w=SupportFixture.World(Blade(),Config());Drain(w);Input(w,melee:true);w.Step();Assert.That(w.GetUnit(0).MeleePhase,Is.GreaterThanOrEqualTo(0));Assert.That(w.GetUnit(0).MeleeStyle,Is.EqualTo(MeleeStyle.Normal));}
        [TestCase(MeleeStyle.Normal,MeleeStyle.Dash)] [TestCase(MeleeStyle.Combo,MeleeStyle.DashCombo)] [TestCase(MeleeStyle.Charge,MeleeStyle.Dash)]
        public void ActualBoostMovementConvertsMeleeStyle(MeleeStyle selected,MeleeStyle expected)
        {var w=SupportFixture.World(Blade(),Config());Input(w,1,true,melee:true,style:selected);w.Step();Assert.That(w.GetUnit(0).MeleeStyle,Is.EqualTo(expected));}
        [Test] public void HoldingShiftWithoutMovementKeepsNormalMelee()
        {var w=SupportFixture.World(Blade(),Config());Input(w,boost:true,melee:true);w.Step();Assert.That(w.GetUnit(0).MeleeStyle,Is.EqualTo(MeleeStyle.Normal));}
        [Test] public void SeamlessDashLungeSpansWindupAndHasClearPostHitRecovery()
        {
            var w=SupportFixture.World(Blade(),Config());FieldFixture.MoveTo(w,0,2,0);Input(w,1,true,melee:true);w.Step();
            Assert.That(w.GetUnit(0).X,Is.GreaterThan(2).And.LessThan(3.5f));Assert.That(w.GetUnit(50).Health,Is.EqualTo(10000));
            FieldFixture.Wait(w,6);Assert.That(w.GetUnit(50).Health,Is.EqualTo(7720));FieldFixture.Wait(w,5);Assert.That(w.GetUnit(0).MeleePhase,Is.GreaterThan(0));
            Assert.That(w.GetUnit(0).Disruption,Is.EqualTo(DisruptionState.Ready));
        }
        [Test] public void NormalMeleeUsesEightyPercentDamageAndShortWindupAndRecovery()
        {
            var w=SupportFixture.World(Blade(),Config());FieldFixture.MoveTo(w,0,6,0);Input(w,melee:true);w.Step();FieldFixture.Wait(w,3);
            Assert.That(w.GetUnit(50).Health,Is.EqualTo(9240));FieldFixture.Wait(w,7);Assert.That(w.GetUnit(0).MeleePhase,Is.LessThan(0));
        }
        [Test] public void RespawnClearsOverheatAndRestoresOrdinaryBoostCapacity()
        {
            var gun=new UnitLoadout("boost-melee",10000,0,10,12,20000,80,0.1f,120,10);var w=SupportFixture.World(gun,Config());Drain(w);
            FieldFixture.MoveTo(w,50,w.GetUnit(0).X+5,0);FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();Assert.That(w.GetUnit(0).IsAlive,Is.False);FieldFixture.Wait(w,10);
            Assert.That(w.GetUnit(0).IsAlive,Is.True);Assert.That(w.GetUnit(0).Boost,Is.EqualTo(100));Assert.That(w.GetUnit(0).BoostOverheated,Is.False);
        }
        [Test] public void OverheatRecoveryMustBeFiniteAndPositive()
        {var c=Config();c.boostOverheatRecoveryPerSecond=0;Assert.Throws<ArgumentException>(()=>c.Validate());}

        [Test] public void HumanBoostCapacityMultiplierScalesOnlyTheHumanSlot()
        {
            // User request 2026-10-16: the human player's own boost pool is 1.3x; BOTs are unaffected.
            var c=Config();c.humanBoostCapacityMultiplier=1.3f;var w=SupportFixture.World(SupportFixture.Gear(),c);
            Assert.That(w.GetUnit(0).Controller,Is.EqualTo(ControllerType.Human));Assert.That(w.GetUnit(0).Boost,Is.EqualTo(130).Within(0.01));
            Assert.That(w.GetUnit(1).Controller,Is.EqualTo(ControllerType.Bot));Assert.That(w.GetUnit(1).Boost,Is.EqualTo(100).Within(0.01));
        }
        [Test] public void HumanBoostCapacityMultiplierAppliesToRecoveryCapAndRespawn()
        {
            var c=Config();c.humanBoostCapacityMultiplier=1.3f;var w=SupportFixture.World(SupportFixture.Gear(),c);
            Drain(w);FieldFixture.Wait(w,400);Assert.That(w.GetUnit(0).Boost,Is.EqualTo(130).Within(0.01));Assert.That(w.GetUnit(0).BoostOverheated,Is.False);
            var gun=new UnitLoadout("boost-melee",10000,0,10,12,20000,80,0.1f,120,10);var respawnWorld=SupportFixture.World(gun,c);
            FieldFixture.MoveTo(respawnWorld,50,respawnWorld.GetUnit(0).X+5,0);FieldFixture.Cmd(respawnWorld,50,fire:true,yaw:-90);respawnWorld.Step();
            Assert.That(respawnWorld.GetUnit(0).IsAlive,Is.False);FieldFixture.Wait(respawnWorld,10);
            Assert.That(respawnWorld.GetUnit(0).IsAlive,Is.True);Assert.That(respawnWorld.GetUnit(0).Boost,Is.EqualTo(130).Within(0.01));
        }
        [Test] public void HumanBoostCapacityMultiplierMustBeAtLeastOneWhenSet()
        {var c=Config();c.humanBoostCapacityMultiplier=0.5f;Assert.Throws<ArgumentException>(()=>c.Validate());}
        [Test] public void HumanBoostCapacityMultiplierDefaultsToUnscaled()
        {var w=SupportFixture.World(SupportFixture.Gear(),Config());Assert.That(w.GetUnit(0).Boost,Is.EqualTo(100));}
    }
}
