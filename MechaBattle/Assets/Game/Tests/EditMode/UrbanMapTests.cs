using System;
using NUnit.Framework;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class UrbanMapTests
    {
        private static ObstacleDefinition Volume(string id,float x,float y,float z,float width,float height,float depth)=>new ObstacleDefinition{id=id,x=x,y=y,z=z,width=width,height=height,depth=depth,platform=true};
        [Test] public void ElevatedFloorDoesNotFillTheRoomOrThePassageBeneathIt()
        {
            var floor=Volume("upper-floor",0,5.75f,0,12,.5f,12);var w=FieldFixture.World(solids:new[]{floor});
            FieldFixture.MoveTo(w,0,8,0);Assert.That(w.GetUnit(0).Y,Is.Zero);Assert.That(w.HasLineOfSight(0,50),Is.True);Assert.That(w.GetSurfaceHeight(0,0),Is.EqualTo(6));
        }
        [Test] public void BuildingDoorsArePassableButAdjacentWallPanelsBlockMotionAndVision()
        {
            var walls=new[]{Volume("front-left",0,6,-7,1,12,6),Volume("front-right",0,6,7,1,12,6),Volume("upper",0,5.75f,0,12,.5f,20)};
            var w=FieldFixture.World(solids:walls);FieldFixture.MoveTo(w,0,8,0);Assert.That(w.GetUnit(0).X,Is.EqualTo(8).Within(.03));
            w=FieldFixture.World(solids:walls);FieldFixture.MoveTo(w,0,-8,7);FieldFixture.MoveTo(w,50,8,7);
            Assert.That(w.HasLineOfSight(0,50),Is.False);for(int i=0;i<80;i++){FieldFixture.Cmd(w,x:1);w.Step();}Assert.That(w.GetUnit(0).X,Is.LessThan(-.89f));
        }
        [Test] public void AContinuousQuarterMeterStairFlightReachesASixMeterLanding()
        {
            var steps=new ObstacleDefinition[24];for(int i=0;i<24;i++)steps[i]=Volume("step"+i,-6+i,((i+1)*.25f)-.125f,0,1.02f,.25f,6);
            var w=FieldFixture.World(solids:steps);FieldFixture.MoveTo(w,0,17,0);Assert.That(w.GetUnit(0).Y,Is.EqualTo(6).Within(.001));
        }
        [Test] public void SolidCeilingsPreventBoostingThroughAStorey()
        {
            var floor=Volume("ceiling",-8,3.75f,0,10,.5f,12);var c=FieldFixture.Config();c.jumpHoldGravityMultiplier=.35f;c.jumpHoldDrainPerSecond=25;
            var w=FieldFixture.World(solids:new[]{floor},config:c);float highest=0;
            for(int i=0;i<80;i++){FieldFixture.Cmd(w,jump:true);w.Step();highest=Math.Max(highest,w.GetUnit(0).Y);}
            Assert.That(highest,Is.LessThanOrEqualTo(1.031f));
        }
    }
}
