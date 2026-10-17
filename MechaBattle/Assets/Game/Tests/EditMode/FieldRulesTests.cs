using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    internal static class FieldFixture
    {
        internal static SimulationConfig Config()
        {
            var c=SimulationTests.Config(); c.aimIn3DEnabled=c.traversalEnabled=c.redLockAssistEnabled=c.equipmentRulesEnabled=true;
            c.unitHeight=2.47f;c.shotOriginHeight=1.3f;c.coreHeight=3.6f;c.unitRadius=0.4f;c.gravity=24;c.jumpSpeed=11;c.jumpBoostCost=12;c.airControl=0.8f;c.maximumStepHeight=0.25f;
            c.boostCapacity=100;c.boostDrainPerSecond=35;c.boostRecoveryPerSecond=20;c.boostSpeedMultiplier=1.8f;c.homeSupplyRadius=0.01f;
            c.downPointRecoverySeconds=1.5f;c.staggerSeconds=0.35f;c.knockdownSeconds=1.25f;c.knockdownThresholdMultiplier=1.8f;
            return c;
        }
        internal static ObstacleDefinition Box(float x=0,float z=0,float top=4,float width=4,float depth=20)
        {return new ObstacleDefinition{id="test-solid",x=x,y=top/2,z=z,width=width,height=top,depth=depth};}
        internal static UnitLoadout Gun(float speed=12,float range=80,float red=24,float damage=20,WeaponKind kind=WeaponKind.Ranged,int magazine=2,float yellow=0,float spread=0)
        {
            bool special=kind==WeaponKind.RepairPod||kind==WeaponKind.Grenade||kind==WeaponKind.Mine;
            return new UnitLoadout("field-test",500,0,10,kind==WeaponKind.Melee?1:special?5:20,damage,range,0.1f,120,10,
                special?WeaponFireMode.SingleShot:WeaponFireMode.FullAutomatic,kind==WeaponKind.Melee?0:special?1:magazine,kind==WeaponKind.Melee?0:0.6f,
                downPointCapacity:600,fireDownPoints:100,blastRadius:kind==WeaponKind.Grenade||kind==WeaponKind.Mine?4:0,
                splashDamage:kind==WeaponKind.Grenade||kind==WeaponKind.Mine?40:0,splashDownPoints:kind==WeaponKind.Grenade||kind==WeaponKind.Mine?400:0,
                projectileSpeed:kind==WeaponKind.RepairPod||kind==WeaponKind.Mine||kind==WeaponKind.Melee?0:speed,redLockRange:special?0:red,kind:kind,
                utilityRadius:kind==WeaponKind.RepairPod?10:kind==WeaponKind.Mine?2.5f:0,utilityDuration:kind==WeaponKind.RepairPod?3:kind==WeaponKind.Mine?5:0,
                projectileGravity:kind==WeaponKind.Grenade?18:0,fuseSeconds:kind==WeaponKind.Grenade?2:0,yellowLockRange:yellow,hipSpreadDegrees:spread);
        }
        internal static BattleSimulation World(UnitLoadout frame=null,ObstacleDefinition[] solids=null,SimulationConfig config=null)
        {
            var map=SimulationTests.Map();map.obstacles=solids??Array.Empty<ObstacleDefinition>();foreach(var b in map.mainBases)b.buildingRadius=10;
            frame=frame??Gun();var decks=new UnitLoadout[100][];for(int i=0;i<100;i++)decks[i]=new[]{frame,frame};
            var w=new BattleSimulation(map,config??Config(),0,decks);MoveTo(w,0,-8,0);MoveTo(w,50,8,0);return w;
        }
        internal static void MoveTo(BattleSimulation w,int slot,float x,float z)
        {
            for(int i=0;i<700;i++){var u=w.GetUnit(slot);float dx=x-u.X,dz=z-u.Z,d=(float)Math.Sqrt(dx*dx+dz*dz);if(d<0.02f)return;float scale=Math.Max(d,0.16f);Cmd(w,slot,dx/scale,dz/scale);w.Step();}
            Assert.Fail("Field fixture could not reach its destination.");
        }
        internal static void Cmd(BattleSimulation w,int slot=0,float x=0,float z=0,bool fire=false,bool jump=false,int change=0,int locked=-1,float yaw=90,bool melee=false)
        {var u=w.GetUnit(slot);Assert.That(w.Submit(slot,new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,x,z,yaw,0,false,fire,false,melee,false,
            lockedTargetSlot:locked,lockedTargetUnitId:locked<0?0:w.GetUnit(locked).UnitId,jump:jump,weaponSwitchDelta:change)),Is.True);}
        internal static void Wait(BattleSimulation w,int ticks){for(int i=0;i<ticks;i++)w.Step();}
    }
    public sealed class FieldRulesTests
    {
        [Test] public void JumpIsGroundedImpulseAndCannotRepeatWhileHeld()
        {
            var w=FieldFixture.World();float top=0;
            for(int i=0;i<100;i++){FieldFixture.Cmd(w,jump:true);w.Step();top=Math.Max(top,w.GetUnit(0).Y);}
            Assert.That(top,Is.GreaterThan(2).And.LessThan(2.7));Assert.That(w.GetUnit(0).Y,Is.Zero);Assert.That(w.GetUnit(0).Grounded,Is.True);
            FieldFixture.Cmd(w);w.Step();FieldFixture.Cmd(w,jump:true);w.Step();Assert.That(w.GetUnit(0).Y,Is.GreaterThan(0));
        }
        [Test] public void HeldBoostJumpConsumesResourceAndRisesAboveATap()
        {
            var c=FieldFixture.Config();c.jumpHoldGravityMultiplier=0.35f;c.jumpHoldDrainPerSecond=25;var w=FieldFixture.World(config:c);
            for(int i=0;i<55;i++){FieldFixture.Cmd(w,jump:true);w.Step();}
            Assert.That(w.GetUnit(0).Y,Is.GreaterThan(4));Assert.That(w.GetUnit(0).Boost,Is.LessThan(70));FieldFixture.Wait(w,150);Assert.That(w.GetUnit(0).Y,Is.Zero);
        }
        [Test] public void WallStopsWalkingAndSupportsSlidingWithoutTunnelling()
        {
            var w=FieldFixture.World(solids:new[]{FieldFixture.Box(depth:80)});
            for(int i=0;i<100;i++){FieldFixture.Cmd(w,x:1,z:1);w.Step();}
            Assert.That(w.GetUnit(0).X,Is.LessThanOrEqualTo(-2.39f));Assert.That(w.GetUnit(0).Z,Is.GreaterThan(5));
        }
        [Test] public void JumpLandsOnPlatformAndWalkingOffFallsBackToGround()
        {
            var w=FieldFixture.World(solids:new[]{FieldFixture.Box(top:1.2f,depth:6)});
            FieldFixture.Cmd(w,x:1,jump:true);w.Step();
            for(int i=0;i<80&&w.GetUnit(0).X<0;i++){FieldFixture.Cmd(w,x:1);w.Step();}FieldFixture.Wait(w,60);
            Assert.That(w.GetUnit(0).Y,Is.EqualTo(1.2f).Within(0.001));Assert.That(w.GetUnit(0).Grounded,Is.True);
            for(int i=0;i<70;i++){FieldFixture.Cmd(w,x:1);w.Step();}Assert.That(w.GetUnit(0).Y,Is.Zero);
        }
        [Test] public void CeilingStopsUpwardMotion()
        {
            var ceiling=FieldFixture.Box(x:-8,top:3.5f,width:8,depth:6);ceiling.y=3.25f;ceiling.height=0.5f;
            var w=FieldFixture.World(solids:new[]{ceiling});float top=0;
            for(int i=0;i<60;i++){FieldFixture.Cmd(w,jump:i==0);w.Step();top=Math.Max(top,w.GetUnit(0).Y);}
            Assert.That(top,Is.LessThanOrEqualTo(0.531f));Assert.That(w.GetUnit(0).Grounded,Is.True);
        }
        [Test] public void WallOccludesSightRedLockAndProjectiles()
        {
            var w=FieldFixture.World(solids:new[]{FieldFixture.Box()});Assert.That(w.HasLineOfSight(0,50),Is.False);Assert.That(w.IsRedLocked(0,50),Is.False);
            FieldFixture.Cmd(w,fire:true,locked:50);w.Step();FieldFixture.Wait(w,150);Assert.That(w.GetUnit(50).Health,Is.EqualTo(500));
            int live=0;for(int i=0;i<w.ProjectileCapacity;i++)if(w.GetProjectile(i).Active)live++;Assert.That(live,Is.Zero);
        }
        [Test] public void RedLockGuidesAtRealSpeedAgainstZigzaggingTarget()
        {
            var w=FieldFixture.World();FieldFixture.Cmd(w,fire:true,locked:50,yaw:-90);w.Step();Assert.That(w.GetUnit(50).Health,Is.EqualTo(500));
            for(int i=0;i<125;i++){FieldFixture.Cmd(w,50,z:i/20%2==0?1:-1);w.Step();}
            Assert.That(w.GetUnit(50).Health,Is.EqualTo(480));
        }
        [Test] public void OutsideRedRangeWrongManualAimMissesButCorrectManualAimHits()
        {
            var gun=FieldFixture.Gun(red:5);var miss=FieldFixture.World(gun);FieldFixture.Cmd(miss,fire:true,locked:50,yaw:0);miss.Step();FieldFixture.Wait(miss,130);
            Assert.That(miss.GetUnit(50).Health,Is.EqualTo(500));
            var hit=FieldFixture.World(gun);Assert.That(hit.IsRedLocked(0,50),Is.False);FieldFixture.Cmd(hit,fire:true,yaw:90);hit.Step();FieldFixture.Wait(hit,80);Assert.That(hit.GetUnit(50).Health,Is.EqualTo(480));
        }
        [Test] public void PhysicalRangeCapsProjectileEvenWithALockRequest()
        {
            var w=FieldFixture.World(FieldFixture.Gun(range:10,red:5));FieldFixture.Cmd(w,fire:true,locked:50);w.Step();FieldFixture.Wait(w,100);Assert.That(w.GetUnit(50).Health,Is.EqualTo(500));
        }
        [Test] public void BotToHumanRedLockRangeStillHasPointEightMultiplier()
        {
            var w=FieldFixture.World(FieldFixture.Gun(red:30));Assert.That(w.GetRedLockRange(50,0),Is.EqualTo(24));Assert.That(w.GetRedLockRange(0,50),Is.EqualTo(30));
        }
        [Test] public void CameraSweepStopsBeforeWallAndPermitsHeadFallbackDistance()
        {
            var map=SimulationTests.Map();map.obstacles=new[]{FieldFixture.Box(x:-10,width:2,depth:10)};var w=new BattleSimulation(map,FieldFixture.Config());
            float distance=w.CameraClearDistance(-8,2.2f,0,-15,2.2f,0,0.2f);Assert.That(distance,Is.GreaterThan(0).And.LessThan(1.15));
            Assert.That(w.CameraClearDistance(-8,2.2f,0,-1,2.2f,0,0.2f),Is.GreaterThan(6));
        }
        [Test] public void InvalidSolidVolumesAreRejectedAndActiveGeometryIsCopied()
        {
            var map=SimulationTests.Map();var o=FieldFixture.Box();map.obstacles=new[]{o};o.height=-1;Assert.Throws<ArgumentException>(()=>MapValidator.ThrowIfInvalid(map));
            o=FieldFixture.Box();var w=FieldFixture.World(solids:new[]{o});o.x=90;
            for(int i=0;i<100;i++){FieldFixture.Cmd(w,x:1);w.Step();}Assert.That(w.GetUnit(0).X,Is.LessThan(-2.3));
        }
        [Test] public void DashAndMeleeCannotPassOrHitThroughAWall()
        {
            var c=FieldFixture.Config(); c.meleeAttacks=new MeleeAttackDefinition[5];
            for(int i=0;i<5;i++)c.meleeAttacks[i]=new MeleeAttackDefinition{style=(MeleeStyle)i,damage=80,downPoints=450,range=3.4f,halfAngleDegrees=65,windupSeconds=0.1f,recoverySeconds=0.2f,cooldownSeconds=0.3f,lungeMeters=8,boostCost=0,moveMultiplier=1,comboSteps=1,maximumTargets=3,minimumChargeSeconds=i==4?0.2f:0,fullChargeSeconds=i==4?1:0,minimumChargeMultiplier=i==4?0.3f:0};
            var blade=FieldFixture.Gun(kind:WeaponKind.Melee,red:3.4f);var w=FieldFixture.World(blade,new[]{FieldFixture.Box(width:0.5f)},c);
            FieldFixture.MoveTo(w,0,-0.8f,0);FieldFixture.MoveTo(w,50,0.8f,0);FieldFixture.Cmd(w,melee:true);w.Step();FieldFixture.Wait(w,10);
            Assert.That(w.GetUnit(0).X,Is.LessThan(-0.64));Assert.That(w.GetUnit(50).Health,Is.EqualTo(500));
        }
        [Test] public void GuidedProjectileDoesNotFollowAReplacementUnitAfterRespawn()
        {
            var slow=FieldFixture.Gun(speed:6);var lethal=FieldFixture.Gun(speed:0,damage:1000);var w=FieldFixture.World(slow.WithWeapons(slow,lethal));FieldFixture.MoveTo(w,1,-6,0);
            int original=w.GetUnit(50).UnitId;FieldFixture.Cmd(w,fire:true,locked:50);w.Step();FieldFixture.Cmd(w,1,fire:true,change:1);w.Step();FieldFixture.Wait(w,5);
            Assert.That(w.GetUnit(50).UnitId,Is.Not.EqualTo(original));Assert.That(w.GetUnit(50).Health,Is.EqualTo(500));
            int active=0;for(int i=0;i<w.ProjectileCapacity;i++){var p=w.GetProjectile(i);if(p.Active)active++;Assert.That(p.Guided,Is.False);}Assert.That(active,Is.GreaterThan(0),"The original slow projectile should still exist after the victim respawns.");
        }
    }
}
