using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class LayeredCityTests
    {
        private static BattleSimulation World()
        {
            var map=SimulationTests.Map();map.surfaceY=16;
            map.obstacles=new[]{new ObstacleDefinition{id="street-deck",x=0,y=15.6f,z=0,width=190,height=.8f,depth=190,platform=true}};
            foreach(var b in map.mainBases){b.y=b.team==0?16:0;b.spawnOverride=true;b.spawnX=-30;b.spawnY=b.y;b.spawnZ=b.z;}
            map.capturePoints=new[]{new CapturePointDefinition{id="upper",x=-30,y=16,z=-30,radius=3},new CapturePointDefinition{id="lower",x=-30,y=0,z=-30,radius=3}};
            var c=FieldFixture.Config();c.captureSecondsPerAdvantage=.1f;return new BattleSimulation(map,c);
        }
        [Test] public void UndergroundSpawnStaysBelowTheRoofAndStreetSpawnUsesItsOwnFloor()
        {var w=World();Assert.That(w.GetUnit(0).Y,Is.EqualTo(16));Assert.That(w.GetUnit(50).Y,Is.Zero);Assert.That(w.GetSurfaceHeight(-30,-30),Is.EqualTo(16));Assert.That(w.GetSurfaceHeightBelow(-30,-30,0),Is.Zero);}
        [Test] public void OverlappingPlanarRelaysCountOnlyUnitsOnTheirOwnFloor()
        {var w=World();for(int i=0;i<20;i++)w.Step();var upper=w.GetCapturePoint(0);var lower=w.GetCapturePoint(1);Assert.That(upper.OwnerTeam,Is.EqualTo(0));Assert.That(lower.OwnerTeam,Is.EqualTo(1));Assert.That(upper.OrangePresence,Is.Zero);Assert.That(lower.BluePresence,Is.Zero);}
        [Test] public void StreetDeckBlocksAShotIntoTheUnderground()
        {var w=World();var u=w.GetUnit(0);var point=w.TraceAim(0,u.X,u.Y+1.3f,u.Z,0,-1,0,30);Assert.That(point.TargetSlot,Is.EqualTo(-1));Assert.That(point.Y,Is.EqualTo(16).Within(.001));}
        [Test] public void UndergroundJumpStaysOnItsOwnFloorAfterLanding()
        {var w=World();for(int i=0;i<100;i++){FieldFixture.Cmd(w,50,jump:true);w.Step();}Assert.That(w.GetUnit(50).Y,Is.Zero);}
    }
}
