using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class AerialTraversalTests
    {
        private static SimulationConfig Config()
        {
            var c=FieldFixture.Config();c.aerialBoostEnabled=true;c.aerialAscentSpeed=6;c.aerialAscentAcceleration=24;
            c.aerialMaximumRise=10;c.aerialGlideFallSpeed=2.5f;c.aerialControlAcceleration=70;c.airControl=0.95f;
            c.jumpHoldGravityMultiplier=0.35f;c.jumpHoldDrainPerSecond=25;c.boostDrainPerSecond=17.5f;
            c.boostRecoveryPerSecond=100;c.boostOverheatEnabled=true;c.boostOverheatRecoveryPerSecond=20;return c;
        }
        private static void Input(BattleSimulation w,int slot=0,float x=0,float z=0,bool jump=false,bool dash=false)
        {var u=w.GetUnit(slot);Assert.That(w.Submit(slot,new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,x,z,90,0,dash,false,false,false,false,jump:jump)),Is.True);}
        private static void Hold(BattleSimulation w,int ticks,bool jump=false,bool dash=false,float x=0,float z=0)
        {for(int i=0;i<ticks;i++){Input(w,x:x,z:z,jump:jump,dash:dash);w.Step();}}
        [Test] public void HeldSpaceRisesToABoundedCeilingWhileATapStaysLow()
        {
            var held=FieldFixture.World(config:Config());var tap=FieldFixture.World(config:Config());float top=0,tapTop=0;
            for(int i=0;i<120;i++){Input(held,jump:true);held.Step();Input(tap,jump:i==0);tap.Step();top=Math.Max(top,held.GetUnit(0).Y);tapTop=Math.Max(tapTop,tap.GetUnit(0).Y);}
            Assert.That(top,Is.EqualTo(10).Within(0.001));Assert.That(tapTop,Is.LessThan(3));Assert.That(held.GetUnit(0).Grounded,Is.False);
        }
        [Test] public void ReleaseSpaceDescendsAndRepressBrakesWithoutASecondJumpImpulse()
        {
            var w=FieldFixture.World(config:Config());Hold(w,40,jump:true);Hold(w,25);var falling=w.GetUnit(0);
            Assert.That(falling.VerticalVelocity,Is.LessThan(0));Assert.That(falling.Y,Is.GreaterThan(3));
            Hold(w,20,jump:true);var rising=w.GetUnit(0);Assert.That(rising.VerticalVelocity,Is.GreaterThan(0).And.LessThanOrEqualTo(6));
            Assert.That(falling.Boost-rising.Boost,Is.EqualTo(10).Within(0.01),"Only continuous thrust fuel is paid, not another takeoff impulse.");
        }
        [Test] public void DashGlidesAndConsumesOnlyOneHorizontalDashCost()
        {
            var glide=FieldFixture.World(config:Config());var fall=FieldFixture.World(config:Config());Hold(glide,100,jump:true);Hold(fall,100,jump:true);
            float fuel=glide.GetUnit(0).Boost;Hold(glide,50,dash:true,x:1);Hold(fall,50);
            Assert.That(glide.GetUnit(0).Y,Is.GreaterThan(7));Assert.That(fall.GetUnit(0).Grounded,Is.True);
            Assert.That(glide.GetUnit(0).VerticalVelocity,Is.EqualTo(-2.5f).Within(0.001));Assert.That(glide.GetUnit(0).Boost,Is.EqualTo(fuel-17.5f).Within(0.01));
        }
        [Test] public void MidairReversalAcceleratesSmoothlyAndReleaseBrakesTheDrift()
        {
            var w=FieldFixture.World(config:Config());Hold(w,30,jump:true,x:1);float start=w.GetUnit(0).X;
            Hold(w,1,x:-1);Assert.That(w.GetUnit(0).X,Is.GreaterThan(start));Hold(w,25,x:-1);Assert.That(w.GetUnit(0).X,Is.LessThan(start));
            Hold(w,15);float stopped=w.GetUnit(0).X;Hold(w,5);Assert.That(w.GetUnit(0).X,Is.EqualTo(stopped).Within(0.001));
        }
        [Test] public void HoldingAtTheCeilingNeverRegeneratesFuelAndEmptyFuelOverheats()
        {
            var w=FieldFixture.World(config:Config());Hold(w,100,jump:true);float fuel=w.GetUnit(0).Boost;Hold(w,10,jump:true);
            Assert.That(w.GetUnit(0).Y,Is.EqualTo(10).Within(0.001));Assert.That(w.GetUnit(0).Boost,Is.EqualTo(fuel-5).Within(0.01));
            for(int i=0;i<100&&!w.GetUnit(0).BoostOverheated;i++){Input(w,jump:true);w.Step();}
            Assert.That(w.GetUnit(0).BoostOverheated,Is.True);Assert.That(w.GetUnit(0).Boost,Is.Zero);Assert.That(w.GetUnit(0).Grounded,Is.False);
            Hold(w,15,jump:true,dash:true,x:1);Assert.That(w.GetUnit(0).Y,Is.LessThan(10));Assert.That(w.GetUnit(0).Boost,Is.Zero);Assert.That(w.GetUnit(0).BoostMoving,Is.False);
        }
        [Test] public void HeldSpaceCannotAutomaticallyTakeOffAgainAfterLanding()
        {
            var c=Config();c.boostCapacity=13;var w=FieldFixture.World(config:c);bool landed=false;int launches=0;bool previousGrounded=true;
            for(int i=0;i<250;i++){Input(w,jump:true);w.Step();var u=w.GetUnit(0);if(previousGrounded&&!u.Grounded)launches++;if(i>5&&u.Grounded)landed=true;previousGrounded=u.Grounded;}
            Assert.That(landed,Is.True);Assert.That(launches,Is.EqualTo(1));Assert.That(w.GetUnit(0).Grounded,Is.True);
        }
        [Test] public void SustainedAscentHitsCeilingsAndAirDashCannotTunnelThroughWalls()
        {
            var ceiling=FieldFixture.Box(x:-8,top:3.5f,width:8,depth:6);ceiling.y=3.25f;ceiling.height=0.5f;
            var roof=FieldFixture.World(solids:new[]{ceiling},config:Config());float top=0;
            for(int i=0;i<90;i++){Input(roof,jump:true);roof.Step();top=Math.Max(top,roof.GetUnit(0).Y);}Assert.That(top,Is.LessThanOrEqualTo(0.531f));Hold(roof,40);Assert.That(roof.GetUnit(0).Grounded,Is.True);
            var wall=FieldFixture.World(solids:new[]{FieldFixture.Box(top:20,width:1,depth:80)},config:Config());Hold(wall,120,jump:true,dash:true,x:1,z:0.25f);
            Assert.That(wall.GetUnit(0).X,Is.LessThanOrEqualTo(-0.89f));Assert.That(wall.GetUnit(0).Z,Is.GreaterThan(2));
        }
        [Test] public void BoostReachesHighPlatformsThenLandsAndWalksOffNormally()
        {
            var w=FieldFixture.World(solids:new[]{FieldFixture.Box(top:5.4f,width:8,depth:8)},config:Config());
            for(int i=0;i<100&&w.GetUnit(0).X < -1;i++){Input(w,x:1,jump:true);w.Step();}
            Assert.That(w.GetUnit(0).X,Is.GreaterThan(-2));Assert.That(w.GetUnit(0).Y,Is.GreaterThan(5.4f));Hold(w,80);
            Assert.That(w.GetUnit(0).Y,Is.EqualTo(5.4f).Within(0.001));Assert.That(w.GetUnit(0).Grounded,Is.True);
            Hold(w,100,x:1);Assert.That(w.GetUnit(0).Y,Is.Zero);Assert.That(w.GetUnit(0).Grounded,Is.True);
        }
        [Test] public void HighPlatformTakeoffUsesTheNewSurfaceAsItsHeightReference()
        {
            var w=FieldFixture.World(solids:new[]{FieldFixture.Box(top:5.4f,width:8,depth:8)},config:Config());
            for(int i=0;i<100&&w.GetUnit(0).X < -1;i++){Input(w,x:1,jump:true);w.Step();}Hold(w,80);
            Assert.That(w.GetUnit(0).Grounded,Is.True);Assert.That(w.GetUnit(0).Y,Is.EqualTo(5.4f).Within(0.001));Hold(w,120,jump:true);
            Assert.That(w.GetUnit(0).Y,Is.EqualTo(15.4f).Within(0.001));
        }
        [Test] public void DisabledFeatureKeepsTheLegacyBallisticArc()
        {
            var c=Config();c.aerialBoostEnabled=false;var w=FieldFixture.World(config:c);float top=0;
            for(int i=0;i<120;i++){Input(w,jump:true);w.Step();top=Math.Max(top,w.GetUnit(0).Y);}
            Assert.That(top,Is.GreaterThan(6).And.LessThan(8));Assert.That(w.GetUnit(0).Y,Is.LessThan(3));
        }
        [Test] public void RespawnResetsAirMomentumAndKeepsHumanCapacityMultiplier()
        {
            var c=Config();c.humanBoostCapacityMultiplier=1.3f;var gun=new UnitLoadout("aerial-lethal",500,0,10,50,2000,80,0.1f,120,10);
            var w=FieldFixture.World(gun,config:c);Hold(w,25,jump:true,x:1);var self=w.GetUnit(0);var enemy=w.GetUnit(50);
            w.Submit(50,new UnitCommand(enemy.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,-90,0,false,true,false,false,false,hasAimPoint:true,aimTargetX:self.X,aimTargetY:self.Y+1.3f,aimTargetZ:self.Z));w.Step();
            Assert.That(w.GetUnit(0).IsAlive,Is.False);FieldFixture.Wait(w,10);var respawn=w.GetUnit(0);
            Assert.That(respawn.UnitId,Is.Not.EqualTo(self.UnitId));Assert.That(respawn.Grounded,Is.True);Assert.That(respawn.Boost,Is.EqualTo(130).Within(0.01));
            FieldFixture.Wait(w,5);Assert.That(w.GetUnit(0).X,Is.EqualTo(respawn.X).Within(0.001));
        }
        [Test] public void BotCanUseTheSharedMotorToCrossAHighLedge()
        {
            var c=Config();var frame=FieldFixture.Gun();var solid=FieldFixture.Box(top:5.4f,width:4,depth:40);var map=SimulationTests.Map();map.obstacles=new[]{solid};
            var w=FieldFixture.World(frame,map.obstacles,c);FieldFixture.MoveTo(w,1,-8,0);var bot=new BotTraversalAdapter(map,c);float highest=0;
            for(int i=0;i<200&&w.GetUnit(1).X<6;i++){var s=w.GetUnit(1);var d=new UnitCommand(s.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,1,0,90,0,false,false,false,false,false);w.Submit(1,bot.Adapt(s,frame,d));w.Step();highest=Math.Max(highest,w.GetUnit(1).Y);}
            Assert.That(w.GetUnit(1).X,Is.GreaterThan(6));Assert.That(highest,Is.GreaterThan(5.4f));Assert.That(Math.Abs(w.GetUnit(1).Z),Is.LessThan(0.5));
        }
        [Test] public void OnlyALimitedRangedGroupMakesShortVisibleContactAerialMoves()
        {
            var c=Config();c.botLineCombatEnabled=true;c.botPreferredRangeFraction=0.9f;c.botStrafeFraction=0.3f;c.botRetreatHealthFraction=0.35f;c.botAssaultModulo=6;
            c.botAerialManeuversEnabled=true;c.botAerialBurstSeconds=0.65f;c.botAerialIntervalSeconds=6.5f;var frame=FieldFixture.Gun(damage:1);var w=FieldFixture.World(frame,config:c);var bot=new BotTraversalAdapter(SimulationTests.Map(),c);
            int starts=0;float highest=0;bool prior=true;
            for(int i=0;i<200;i++)
            {
                foreach(int slot in new[]{1,2}){var s=w.GetUnit(slot);var d=new UnitCommand(s.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,1,0,90,0,false,false,false,false,false);var cmd=bot.Adapt(s,frame,d,50,w.GetUnit(50).UnitId,s.X+30,0,s.Z);if(slot==1)Assert.That(cmd.Jump,Is.False);w.Submit(slot,cmd);}
                w.Step();var moving=w.GetUnit(2);if(prior&&!moving.Grounded)starts++;prior=moving.Grounded;highest=Math.Max(highest,moving.Y);
            }
            Assert.That(starts,Is.EqualTo(1));Assert.That(highest,Is.GreaterThan(3));Assert.That(w.GetUnit(2).Grounded,Is.True);
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void AerialMotorRejectsInvalidRiseRates(float value)
        {var c=Config();c.aerialAscentSpeed=value;Assert.Throws<ArgumentException>(()=>c.Validate());}
        [Test] public void AerialMotorRequiresTraversalFuelAndValidBotCadence()
        {
            var c=Config();c.traversalEnabled=false;Assert.Throws<ArgumentException>(()=>c.Validate());c=Config();c.jumpHoldDrainPerSecond=0;Assert.Throws<ArgumentException>(()=>c.Validate());
            c=Config();c.botAerialManeuversEnabled=true;c.botLineCombatEnabled=true;c.botAerialBurstSeconds=1;c.botAerialIntervalSeconds=0.5f;Assert.Throws<ArgumentException>(()=>c.Validate());
        }
    }
}
