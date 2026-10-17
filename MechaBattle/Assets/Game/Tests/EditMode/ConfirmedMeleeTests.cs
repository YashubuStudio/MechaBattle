using System;
using System.Linq;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;

namespace MechaBattle.Tests
{
    public sealed class ConfirmedMeleeTests
    {
        private static SimulationConfig Config(bool enabled=true)
        {
            var c=FieldFixture.Config();c.boostOverheatEnabled=true;c.boostOverheatRecoveryPerSecond=20;c.boostRecoveryPerSecond=100;c.seamlessMeleeEnabled=true;
            c.meleeHitRecoveryMultiplier=enabled?0.5f:0;c.meleeHitBoostRefundFraction=enabled?0.5f:0;
            var recover=new[]{0.10f,0.18f,0.16f,0.24f,0.60f};c.meleeAttacks=new MeleeAttackDefinition[5];
            for(int i=0;i<5;i++)c.meleeAttacks[i]=new MeleeAttackDefinition{style=(MeleeStyle)i,damage=80,downPoints=1,range=3.8f,halfAngleDegrees=65,windupSeconds=i==0?0.06f:0.12f,recoverySeconds=recover[i],cooldownSeconds=0.2f,lungeMeters=0,boostCost=i==0?0:i==2?30:20,moveMultiplier=0.35f,comboSteps=i==1?3:1,maximumTargets=3,minimumChargeSeconds=i==4?0.4f:0,fullChargeSeconds=i==4?1.4f:0,minimumChargeMultiplier=i==4?0.3f:0};
            return c;
        }
        private static UnitLoadout Blade()=>new UnitLoadout("confirm-melee",10000,0,10,1,80,3.8f,0.31f,120,10,kind:WeaponKind.Melee);
        private static void Input(BattleSimulation w,bool melee=true,MeleeStyle style=MeleeStyle.Dash,int change=0,bool fire=false,bool jump=false,bool boost=false)
        {var u=w.GetUnit(0);Assert.That(w.Submit(0,new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,90,0,boost,fire,false,melee,false,meleeStyle:style,jump:jump,weaponSwitchDelta:change)),Is.True);}
        private static void Impact(BattleSimulation w,MeleeStyle style=MeleeStyle.Dash)
        {Input(w,style:style);w.Step();FieldFixture.Wait(w,style==MeleeStyle.Normal?3:6);}
        private static int UntilReady(BattleSimulation w)
        {int ticks=0;while(w.GetUnit(0).MeleePhase>=0&&ticks<100){w.Step();ticks++;}return ticks;}
        [TestCase(MeleeStyle.Normal,2,4)] [TestCase(MeleeStyle.Dash,3,7)]
        public void RealHitHalvesRecoveryButMissKeepsFullRecovery(MeleeStyle style,int hitRemaining,int missRemaining)
        {
            var hit=SupportFixture.World(Blade(),Config());FieldFixture.MoveTo(hit,0,6,0);Impact(hit,style);
            Assert.That(hit.GetUnit(50).Health,Is.EqualTo(9920));Assert.That(hit.GetUnit(0).Boost,Is.EqualTo(style==MeleeStyle.Normal?100:85));
            Assert.That(hit.GetUnit(0).MeleePhase,Is.GreaterThanOrEqualTo(hit.GetMeleeContactPhase(0)));Assert.That(UntilReady(hit),Is.EqualTo(hitRemaining));
            var miss=SupportFixture.World(Blade(),Config());Impact(miss,style);Assert.That(miss.GetUnit(50).Health,Is.EqualTo(10000));
            Assert.That(miss.GetUnit(0).Boost,Is.EqualTo(style==MeleeStyle.Normal?100:70));Assert.That(UntilReady(miss),Is.EqualTo(missRemaining));
        }
        [Test] public void MultipleVictimsRefundAndShortenOnlyOnceForTheSameStrike()
        {
            var w=SupportFixture.World(Blade(),Config());FieldFixture.MoveTo(w,0,6,0);FieldFixture.MoveTo(w,51,8,2);Impact(w);
            Assert.That(w.GetUnit(50).Health,Is.EqualTo(9920));Assert.That(w.GetUnit(51).Health,Is.EqualTo(9920));
            Assert.That(w.GetUnit(0).Boost,Is.EqualTo(85));Assert.That(UntilReady(w),Is.EqualTo(3));
        }
        [Test] public void ComboStagesEachGetOneFreshBonusWithoutCompoundingRecovery()
        {
            var w=SupportFixture.World(Blade(),Config());FieldFixture.MoveTo(w,0,6,0);
            for(int i=0;i<80 && w.GetUnit(50).Health>9840;i++){Input(w,style:MeleeStyle.Combo);w.Step();}
            Assert.That(w.GetUnit(50).Health,Is.EqualTo(9840));Assert.That(w.GetUnit(0).ComboStep,Is.EqualTo(2));Assert.That(w.GetUnit(0).Boost,Is.EqualTo(80));
            Assert.That(w.GetMeleeContactPhase(0),Is.EqualTo(0.12f/(0.12f+0.09f)).Within(0.001));
        }
        [Test] public void AHitRefundDoesNotUnlockAnOverheatTriggeredAtZero()
        {
            var c=Config();c.boostCapacity=30;var w=SupportFixture.World(Blade(),c);FieldFixture.MoveTo(w,0,6,0);Impact(w);
            Assert.That(w.GetUnit(0).Boost,Is.EqualTo(15));Assert.That(w.GetUnit(0).BoostOverheated,Is.True);UntilReady(w);
            Input(w,melee:false,jump:true,boost:true);w.Step();Assert.That(w.GetUnit(0).Y,Is.Zero);Assert.That(w.GetUnit(0).BoostOverheated,Is.True);
        }
        [Test] public void ZeroOptionalModifiersKeepThePreviousFullCostAndRecovery()
        {var w=SupportFixture.World(Blade(),Config(false));FieldFixture.MoveTo(w,0,6,0);Impact(w);Assert.That(w.GetUnit(50).Health,Is.EqualTo(9920));Assert.That(w.GetUnit(0).Boost,Is.EqualTo(70));Assert.That(UntilReady(w),Is.EqualTo(7));}
        [Test] public void FriendlyBodiesAndCoverNeverGrantAHitBonus()
        {
            var w=SupportFixture.World(Blade(),Config());FieldFixture.MoveTo(w,1,-6,0);Impact(w);Assert.That(w.GetUnit(1).Health,Is.EqualTo(10000));Assert.That(w.GetUnit(0).Boost,Is.EqualTo(70));
            var c=Config();foreach(var a in c.meleeAttacks)a.range=12;var blocked=FieldFixture.World(Blade(),new[]{FieldFixture.Box()},c);FieldFixture.MoveTo(blocked,50,3,0);Impact(blocked);
            Assert.That(blocked.GetUnit(50).Health,Is.EqualTo(10000));Assert.That(blocked.GetUnit(0).Boost,Is.EqualTo(70));
        }
        [Test] public void AnEarlierRangedProjectileHittingDuringMeleeRecoveryGrantsNoBonus()
        {
            var blade=Blade();var gun=new UnitLoadout("confirm-melee",10000,0,10,20,100,80,0.2f,120,10,WeaponFireMode.FullAutomatic,10,1,projectileSpeed:80);
            var w=SupportFixture.World(blade.WithWeapons(blade,gun),Config());Input(w,melee:false,change:1,fire:true);w.Step();
            Input(w,change:1);w.Step();FieldFixture.Wait(w,10);
            Assert.That(w.GetUnit(50).Health,Is.EqualTo(9900));Assert.That(w.GetUnit(0).Boost,Is.EqualTo(70));Assert.That(w.GetUnit(0).MeleePhase,Is.GreaterThan(0));
        }
        [TestCase(false)] [TestCase(true)]
        public void EnemyEquipmentNeedsResolvedDamageAndAReplacedObjectGrantsNoBonus(bool replace)
        {
            var blade=FieldFixture.Gun(kind:WeaponKind.Melee);var w=FieldFixture.World(blade.WithWeapons(blade,ObjectFixture.Utility()),config:Config());int index=ObjectFixture.Place(w);
            int old=w.GetDeployable(index).Id;FieldFixture.MoveTo(w,50,8,20);
            for(int n=0;n<2;n++){FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();FieldFixture.Wait(w,40);}
            Assert.That(w.GetDeployable(index).Id,Is.EqualTo(old),"The first object must survive until its owner has three.");FieldFixture.MoveTo(w,0,4.5f,0);
            Input(w);w.Step();FieldFixture.Wait(w,5);if(replace)FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();
            Assert.That(w.GetUnit(0).Boost,Is.EqualTo(replace?70:85));
            if(replace){Assert.That(w.GetDeployable(index).Id,Is.Not.EqualTo(old));Assert.That(w.GetDeployable(index).Health,Is.EqualTo(200));}
            else Assert.That(w.GetDeployable(index).Health,Is.EqualTo(120));
        }
        [Test] public void AValidCoreHitAlsoGrantsOnlyOneBonus()
        {var w=SupportFixture.World(Blade(),Config());FieldFixture.MoveTo(w,0,28,-30);Impact(w);Assert.That(SupportFixture.Events(w).Any(e=>e.Kind==CombatEventKind.CoreHit&&e.SourceSlot==0),Is.True);Assert.That(w.GetUnit(0).Boost,Is.EqualTo(85));}
        [TestCase(-0.1f,0)] [TestCase(1.1f,0)] [TestCase(float.NaN,0)] [TestCase(0,-0.1f)] [TestCase(0,1.1f)] [TestCase(0,float.PositiveInfinity)]
        public void InvalidHitModifiersAreRejected(float recovery,float refund)
        {var c=Config();c.meleeHitRecoveryMultiplier=recovery;c.meleeHitBoostRefundFraction=refund;Assert.Throws<ArgumentException>(()=>c.Validate());}
    }
}
