using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
using MechaBattle.Observation;
namespace MechaBattle.Tests
{
    public sealed class ManualLockTests
    {
        private static SimulationConfig Config(){var c=FieldFixture.Config();c.redLockAssistEnabled=false;c.weaponSpreadEnabled=true;return c;}
        private static UnitLoadout Gun(float spread=1.2f,float range=80,float red=24,float yellow=40)=>FieldFixture.Gun(speed:12,range:range,red:red,yellow:yellow,spread:spread);
        private static ProjectileSnapshot Active(BattleSimulation w){for(int i=0;i<w.ProjectileCapacity;i++)if(w.GetProjectile(i).Active)return w.GetProjectile(i);Assert.Fail("Expected a flying projectile.");return default;}
        [Test] public void HipFireSpreadsButExplicitLockFliesStraightWithoutHoming()
        {
            var hip=FieldFixture.World(Gun(),config:Config());FieldFixture.Cmd(hip,fire:true,yaw:90);hip.Step();var scattered=Active(hip);
            Assert.That(Math.Abs(scattered.Dy)+Math.Abs(scattered.Dz),Is.GreaterThan(0.0001));
            var locked=FieldFixture.World(Gun(),config:Config());FieldFixture.Cmd(locked,fire:true,yaw:90,locked:50);locked.Step();var straight=Active(locked);
            Assert.That(straight.Guided,Is.False);Assert.That(straight.Dx,Is.EqualTo(1).Within(0.0001));Assert.That(straight.Dy,Is.Zero.Within(0.0001));Assert.That(straight.Dz,Is.Zero.Within(0.0001));
            FieldFixture.Wait(locked,80);Assert.That(locked.GetUnit(50).Health,Is.EqualTo(480));
        }
        [Test] public void RedLockDoesNotOverrideManualAimOrFollowAStrafingTarget()
        {
            var miss=FieldFixture.World(Gun(),config:Config());Assert.That(miss.IsRedLocked(0,50),Is.True);FieldFixture.Cmd(miss,fire:true,yaw:0,locked:50);miss.Step();
            Assert.That(Active(miss).Dz,Is.EqualTo(1).Within(0.0001));FieldFixture.Wait(miss,100);Assert.That(miss.GetUnit(50).Health,Is.EqualTo(500));
            var dodge=FieldFixture.World(Gun(),config:Config());FieldFixture.Cmd(dodge,fire:true,yaw:90,locked:50);dodge.Step();
            for(int i=0;i<100;i++){FieldFixture.Cmd(dodge,50,z:1);dodge.Step();}Assert.That(dodge.GetUnit(50).Health,Is.EqualTo(500));
        }
        [Test] public void ManualShotCanHitBetweenYellowLockAndPhysicalRange()
        {
            var w=FieldFixture.World(Gun(0,25,5,10),config:Config());Assert.That(w.IsYellowLocked(0,50),Is.False);Assert.That(w.IsRedLocked(0,50),Is.False);
            FieldFixture.Cmd(w,fire:true,yaw:90);w.Step();FieldFixture.Wait(w,80);Assert.That(w.GetUnit(50).Health,Is.EqualTo(480));
        }
        [Test] public void LockRangesMustHaveStrictPhysicalYellowRedOrderingWhenAuthored()
        {Assert.Throws<ArgumentException>(()=>Gun(range:40,yellow:40));Assert.Throws<ArgumentException>(()=>Gun(red:40,yellow:30));Assert.Throws<ArgumentException>(()=>Gun(spread:11));}
        [Test] public void PartialBotLeadKeepsCloseAimButDoesNotPerfectlyInterceptLongRangeStrafing()
        {
            var c=Config();c.botLineCombatEnabled=true;c.botPreferredRangeFraction=0.9f;c.botStrafeFraction=0.3f;c.botRetreatHealthFraction=0.35f;c.botAssaultModulo=6;
            var frame=Gun();var w=FieldFixture.World(frame,config:c);var self=w.GetUnit(1);var full=new BotTraversalAdapter(SimulationTests.Map(),c);
            var partialConfig=Config();partialConfig.botLineCombatEnabled=true;partialConfig.botPreferredRangeFraction=0.9f;partialConfig.botStrafeFraction=0.3f;partialConfig.botRetreatHealthFraction=0.35f;partialConfig.botAssaultModulo=6;partialConfig.botProjectileLeadFraction=0.65f;var partial=new BotTraversalAdapter(SimulationTests.Map(),partialConfig);
            UnitCommand Command(long tick)=>new UnitCommand(self.UnitId,tick,1,1,0,90,0,false,true,false,false,false);
            full.Adapt(self,frame,Command(w.CurrentTick),50,10,self.X+22,self.Y,self.Z);partial.Adapt(self,frame,Command(w.CurrentTick),50,10,self.X+22,self.Y,self.Z);
            var a=full.Adapt(self,frame,Command(w.CurrentTick+10),50,10,self.X+22,self.Y,self.Z+2);var b=partial.Adapt(self,frame,Command(w.CurrentTick+10),50,10,self.X+22,self.Y,self.Z+2);
            Assert.That(b.AimTargetZ,Is.GreaterThan(self.Z+2));Assert.That(b.AimTargetZ,Is.LessThan(a.AimTargetZ));
            Assert.That(b.AimTargetZ-self.Z-2,Is.EqualTo((a.AimTargetZ-self.Z-2)*0.65f).Within(0.001));
        }
        [Test] public void BotLeadUsesConsecutiveVisibleSamplesAndResetsAfterContactIsLost()
        {
            var c=Config();c.botLineCombatEnabled=true;c.botPreferredRangeFraction=0.9f;c.botStrafeFraction=0.3f;c.botRetreatHealthFraction=0.35f;c.botAssaultModulo=6;
            var frame=Gun();var w=FieldFixture.World(frame,config:c);var u=w.GetUnit(1);var bot=new BotTraversalAdapter(SimulationTests.Map(),c);
            UnitCommand Command(long tick)=>new UnitCommand(u.UnitId,tick,1,1,0,90,0,false,true,false,false,false);
            bot.Adapt(u,frame,Command(w.CurrentTick),50,10,u.X+22,u.Y,u.Z);
            var lead=bot.Adapt(u,frame,Command(w.CurrentTick+10),50,10,u.X+22,u.Y,u.Z+2);
            Assert.That(lead.AimTargetZ,Is.GreaterThan(u.Z+10));
            bot.Adapt(u,frame,Command(w.CurrentTick+11));
            var reacquired=bot.Adapt(u,frame,Command(w.CurrentTick+12),50,10,u.X+22,u.Y,u.Z+2);
            Assert.That(reacquired.AimTargetZ,Is.EqualTo(u.Z+2).Within(0.001));
        }
    }
}
