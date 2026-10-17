using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class MainBaseDurabilityTests
    {
        internal static BattleSimulation World(bool collision=false)
        {
            var map=SimulationTests.Map();foreach(var b in map.mainBases){b.attackRadius=1;b.spawnOverride=true;b.spawnX=b.team==0?-27:50;b.spawnY=0;b.spawnZ=b.z;}
            var c=FieldFixture.Config();c.mainBaseDurabilityEnabled=true;c.mainBaseHealth=100;c.mainBaseDestructionGaugeLoss=8000;c.baseGaugeDamageFactor=.08f;c.baseCollisionEnabled=collision;c.maxAmmo=40;c.redLockAssistEnabled=false;
            return new BattleSimulation(map,c);
        }
        [Test] public void DestructionClampsDamageAppliesOneLargeLossAndStopsSpawn()
        {
            var w=World();double before=w.GetGauge(1);SimulationTests.Command(w,0,yaw:90,fire:true);w.Step();
            Assert.That(w.GetBase(3).Health,Is.EqualTo(40));Assert.That(before-w.GetGauge(1),Is.EqualTo(4.8).Within(.001));
            for(int i=0;i<6;i++){SimulationTests.Command(w,0,yaw:90,fire:true);w.Step();}
            Assert.That(w.GetBase(3).Destroyed,Is.True);Assert.That(w.GetBase(3).DamageReceived,Is.EqualTo(100));
            Assert.That(before-w.GetGauge(1),Is.EqualTo(8008).Within(.001));Assert.That(w.GetSlotStats(0).CoreDamageDealt,Is.EqualTo(100));
            Assert.That(w.CanSpawnAtMainBase(1,3),Is.False);double gauge=w.GetGauge(1);
            for(int i=0;i<20;i++){SimulationTests.Command(w,0,yaw:90,fire:true);w.Step();}
            Assert.That(w.GetGauge(1),Is.EqualTo(gauge));Assert.That(w.GetBase(4).Health,Is.EqualTo(100));
        }
        [Test] public void NamesFollowNorthOrderWithinEachTeam()
        {var w=World();Assert.That(w.GetBase(2).Name,Is.EqualTo("A"));Assert.That(w.GetBase(1).Name,Is.EqualTo("B"));Assert.That(w.GetBase(0).Name,Is.EqualTo("C"));Assert.That(w.GetBase(5).Name,Is.EqualTo("X"));Assert.That(w.GetBase(3).Name,Is.EqualTo("Z"));}
        [Test] public void CoreStopsMovementAndCameraBeforeEnteringItsBody()
        {
            var w=World(true);for(int i=0;i<400;i++){FieldFixture.Cmd(w,x:1,z:0);w.Step();}
            Assert.That(w.GetUnit(0).X,Is.InRange(28.4f,28.61f));
            float clear=w.CameraClearDistance(-27,1.3f,-30,-34,1.3f,-30,.2f);Assert.That(clear,Is.GreaterThan(1).And.LessThan(3));
        }
        [Test] public void CameraHitBehindTheMuzzleFallsBackToTheForwardBarrelRay()
        {
            var w=World(true);var self=w.GetUnit(0);var point=w.TraceCameraAim(0,-34,1.3f,self.Z,1,0,0,100);
            Assert.That(point.X,Is.GreaterThan(self.X));Assert.That(point.CoreIndex,Is.EqualTo(3));
        }
        [Test] public void ManualAimPointBehindTheMovedUnitNeverTurnsTheBodyBackwards()
        {
            var w=World(true);var u=w.GetUnit(0);w.Submit(0,new UnitCommand(u.UnitId,w.CurrentTick,1,1,0,90,0,false,false,false,false,false,hasAimPoint:true,aimTargetX:u.X-.1f,aimTargetY:1.3f,aimTargetZ:u.Z));w.Step();Assert.That(w.GetUnit(0).AimYaw,Is.EqualTo(90).Within(.001));
        }
        [TestCase(float.NaN)] [TestCase(0)] [TestCase(-1)]
        public void InvalidDurabilityCannotStartAMatch(float health)
        {var c=FieldFixture.Config();c.mainBaseDurabilityEnabled=true;c.mainBaseHealth=health;c.mainBaseDestructionGaugeLoss=8000;Assert.Throws<ArgumentException>(()=>new BattleSimulation(SimulationTests.Map(),c));}
    }
}
