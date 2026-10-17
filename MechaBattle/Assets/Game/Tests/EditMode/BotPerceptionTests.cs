using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class BotPerceptionTests
    {
        internal static SimulationConfig Config()
        {var c=FieldFixture.Config();c.botPerceptionEnabled=true;c.botVisualRange=60;c.botVisualDegrees=100;c.botVerticalVisualDegrees=80;c.botScanSeconds=.2f;c.botReactionSeconds=.5f;c.botReactionJitterSeconds=.2f;c.botViewTurnDegreesPerSecond=90;return c;}
        [Test] public void AVisibleEnemyRequiresSustainedAcquisitionBeforeAnyFiringContact()
        {
            var c=Config();var w=FieldFixture.World(config:c);var sensor=new BotPerception(w,c);
            for(int i=0;i<25;i++){Assert.That(sensor.FindNearestEnemy(0,20).Found,Is.False);w.Step();}
            for(int i=0;i<30;i++){w.Step();sensor.FindNearestEnemy(0,20);}
            var contact=sensor.FindNearestEnemy(0,20);Assert.That(contact.Found,Is.True);Assert.That(contact.SlotId,Is.EqualTo(50));
        }
        [Test] public void AnUnreportedEnemyBehindTheBotIsNeverDiscoveredByTheRangeScan()
        {
            var c=Config();var w=FieldFixture.World(config:c);FieldFixture.MoveTo(w,50,-20,0);var sensor=new BotPerception(w,c);
            for(int i=0;i<150;i++){Assert.That(sensor.FindNearestEnemy(0,20).Found,Is.False);FieldFixture.Cmd(w,yaw:90);w.Step();}
            Assert.That(w.GetTeamContact(0,50).Valid,Is.False);
        }
        [Test] public void ATargetBehindSolidCoverNeverBecomesAVisualFiringContact()
        {
            var c=Config();var w=FieldFixture.World(solids:new[]{FieldFixture.Box()},config:c);var sensor=new BotPerception(w,c);
            for(int i=0;i<120;i++){Assert.That(sensor.FindNearestEnemy(0,20).Found,Is.False);w.Step();}
        }
        [Test] public void VisualRangeIsIndependentOfALongerWeaponRange()
        {
            var c=Config();c.botVisualRange=10;var w=FieldFixture.World(config:c);var sensor=new BotPerception(w,c);
            for(int i=0;i<100;i++){Assert.That(sensor.FindNearestEnemy(0,500).Found,Is.False);w.Step();}
        }
        [Test] public void StaleBetweenScanSamplesDoNotTrackEveryFrameOfEnemyMovement()
        {
            var c=Config();var w=FieldFixture.World(config:c);var sensor=new BotPerception(w,c);
            for(int i=0;i<61;i++){sensor.FindNearestEnemy(0,20);w.Step();}
            var previous=sensor.FindNearestEnemy(0,20);Assert.That(previous.Found,Is.True);
            FieldFixture.Cmd(w,50,z:1);w.Step();var next=sensor.FindNearestEnemy(0,20);
            Assert.That(next.Z,Is.EqualTo(previous.Z));Assert.That(w.GetUnit(50).Z,Is.GreaterThan(next.Z));
        }
        [Test] public void LosingSightInvalidatesContactImmediatelyAndReacquisitionWaitsAgain()
        {
            var c=Config();var w=FieldFixture.World(config:c);var sensor=new BotPerception(w,c);
            for(int i=0;i<60;i++){sensor.FindNearestEnemy(0,20);w.Step();}
            Assert.That(sensor.FindNearestEnemy(0,20).Found,Is.True);
            FieldFixture.MoveTo(w,50,40,0);Assert.That(sensor.FindNearestEnemy(0,20).Found,Is.False);
            FieldFixture.MoveTo(w,50,8,0);
            Assert.That(sensor.FindNearestEnemy(0,20).Found,Is.False,"Returning to view must require a fresh acquisition.");
        }
        [Test] public void RadarCanCueAnUnseenBearingButNeverFiresThroughTheWall()
        {
            var c=Config();c.strategy=StrategyTests.Rules();var w=FieldFixture.World(solids:new[]{FieldFixture.Box()},config:c);var sensor=new BotPerception(w,c);
            Assert.That(w.RequestStrategy(0,StrategyKind.WideRadar,0,0,out _),Is.True);
            for(int i=0;i<100;i++){Assert.That(sensor.FindNearestEnemy(0,20).Found,Is.False);w.Step();}
        }
        [Test] public void RadarCanEventuallyMakeTheBotLookAtAnOtherwiseUnseenFlank()
        {
            var c=Config();c.strategy=StrategyTests.Rules();c.strategy.wideRadar.durationSeconds=10;
            var w=FieldFixture.World(config:c);FieldFixture.MoveTo(w,50,-20,0);var sensor=new BotPerception(w,c);
            Assert.That(w.RequestStrategy(0,StrategyKind.WideRadar,-20,0,out _),Is.True);
            bool found=false;for(int i=0;i<220;i++){FieldFixture.Cmd(w,yaw:90);w.Step();var seen=sensor.FindNearestEnemy(0,20);if(i<25)Assert.That(seen.Found,Is.False);found|=seen.Found;}
            Assert.That(found,Is.True);
        }
        [Test] public void RepeatedQueriesInOneTickDoNotShortenTheReactionDelay()
        {
            var c=Config();var w=FieldFixture.World(config:c);var sensor=new BotPerception(w,c);
            for(int i=0;i<1000;i++)Assert.That(sensor.FindNearestEnemy(0,20).Found,Is.False);
            Assert.That(sensor.FindNearestEnemy(-1,20).Found||sensor.FindNearestEnemy(0,float.NaN).Found,Is.False);
        }
        [Test] public void PublicVisualTestRejectsBehindAndOccludedUnitsButAllowsFrontUnits()
        {
            var w=FieldFixture.World();FieldFixture.Cmd(w,yaw:90);w.Step();Assert.That(w.CanVisuallySee(0,50,20),Is.True);
            FieldFixture.Cmd(w,yaw:-90);w.Step();Assert.That(w.CanVisuallySee(0,50,20),Is.False);
            w=FieldFixture.World(solids:new[]{FieldFixture.Box()});FieldFixture.Cmd(w,yaw:90);w.Step();Assert.That(w.CanVisuallySee(0,50,20),Is.False);
        }
        [TestCase(false)] [TestCase(true)] public void BotAimPointAndRedLockCannotBypassThePhysicalTurnBudget(bool redAssist)
        {
            var c=Config();c.redLockAssistEnabled=redAssist;var w=FieldFixture.World(config:c);var before=w.GetUnit(50);var target=w.GetUnit(0);
            Assert.That(w.Submit(50,new UnitCommand(before.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,-90,0,false,false,false,false,false,
                hasAimPoint:true,aimTargetX:target.X,aimTargetY:target.Y+c.shotOriginHeight,aimTargetZ:target.Z,lockedTargetSlot:0,lockedTargetUnitId:target.UnitId)),Is.True);
            w.Step();var after=w.GetUnit(50);Assert.That(Math.Abs(after.AimYaw-before.AimYaw),Is.LessThanOrEqualTo(c.botViewTurnDegreesPerSecond*w.FixedDeltaTime+.001f));
            Assert.That(after.AimYaw,Is.Not.EqualTo(-90));
        }
        [Test] public void PhysicalTurnBudgetDoesNotSlowHumanManualAim()
        {
            var c=Config();var w=FieldFixture.World(config:c);FieldFixture.Cmd(w,yaw:-90);w.Step();Assert.That(w.GetUnit(0).AimYaw,Is.EqualTo(-90));
        }
        [Test] public void InvalidPerceptionSettingsAreRejected()
        {var c=Config();c.botVisualDegrees=360;Assert.Throws<ArgumentException>(()=>c.Validate());c=Config();c.botReactionSeconds=0;Assert.Throws<ArgumentException>(()=>c.Validate());c=Config();c.botScanSeconds=float.NaN;Assert.Throws<ArgumentException>(()=>c.Validate());}
    }
}
