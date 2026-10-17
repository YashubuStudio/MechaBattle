using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
using MechaBattle.Observation;
namespace MechaBattle.Tests
{
    public sealed class LineCombatTests
    {
        private sealed class PublicCapture : ICapturePointStatusSource
        {public int CapturePointCount=>1;public CapturePointStatus GetCapturePoint(int index)=>new CapturePointStatus(0,0,0);}
        private static SimulationConfig Config()
        {var c=FieldFixture.Config();c.botLineCombatEnabled=true;c.botPreferredRangeFraction=0.9f;c.botStrafeFraction=0.3f;c.botRetreatHealthFraction=0.35f;c.botAssaultModulo=6;return c;}
        private static UnitCommand Forward(BattleSimulation w,int slot)
        {var u=w.GetUnit(slot);return new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,1,0,90,0,false,true,false,false,false);}
        private static UnitLoadout Frame(float damage=20)
        {var a=FieldFixture.Gun(speed:0,damage:damage);return a.WithWeapons(a,FieldFixture.Gun(kind:WeaponKind.Melee,range:3.4f,red:3.4f),FieldFixture.Gun(kind:WeaponKind.RepairPod));}
        [Test] public void RifleBotHoldsItsLineAndKeepsFractionalStrafingSpeed()
        {
            var c=Config();var frame=Frame();var w=FieldFixture.World(frame,config:c);var self=w.GetUnit(1);var bot=new BotTraversalAdapter(SimulationTests.Map(),c);
            var command=bot.Adapt(self,frame,Forward(w,1),50,w.GetUnit(50).UnitId,self.X+22,self.Y,self.Z);
            Assert.That(command.MoveX,Is.Zero.Within(0.001));Assert.That(Math.Abs(command.MoveY),Is.EqualTo(0.3f).Within(0.001));
            Assert.That(command.FirePrimary,Is.True);Assert.That(command.Melee,Is.False);Assert.That(command.WeaponSwitchDelta,Is.Zero);
        }
        [Test] public void RifleBotBacksOffWhileLimitedAssaultRoleCommitsToCloseMelee()
        {
            var c=Config();var frame=Frame();var w=FieldFixture.World(frame,config:c);var bot=new BotTraversalAdapter(SimulationTests.Map(),c);
            var self=w.GetUnit(1);var rifle=bot.Adapt(self,frame,Forward(w,1),50,w.GetUnit(50).UnitId,self.X+4,self.Y,self.Z);
            Assert.That(rifle.MoveX,Is.LessThan(0));Assert.That(rifle.WeaponSwitchDelta,Is.Zero);Assert.That(rifle.Melee,Is.False);
            self=w.GetUnit(6);var assault=bot.Adapt(self,frame,Forward(w,6),50,w.GetUnit(50).UnitId,self.X+4,self.Y,self.Z);
            Assert.That(assault.WeaponSwitchDelta,Is.EqualTo(1));Assert.That(assault.Melee,Is.True);Assert.That(assault.MeleeStyle,Is.EqualTo(MeleeStyle.Dash));
        }
        [Test] public void WoundedBotStopsShootingAndUsesSharedBoostToWithdraw()
        {
            var c=Config();var frame=Frame(400);var w=FieldFixture.World(frame,config:c);
            FieldFixture.MoveTo(w,1,-8,5);FieldFixture.MoveTo(w,50,8,5);FieldFixture.Cmd(w,50,fire:true,yaw:270);w.Step();
            var self=w.GetUnit(1);Assert.That(self.Health,Is.LessThan(frame.MaxHealth*0.35f));
            var command=new BotTraversalAdapter(SimulationTests.Map(),c).Adapt(self,frame,Forward(w,1),50,w.GetUnit(50).UnitId,self.X+16,self.Y,self.Z);
            Assert.That(command.FirePrimary,Is.False);Assert.That(command.Boost,Is.True);Assert.That(command.MoveX,Is.LessThan(0));
        }
        [Test] public void DisabledLineRulesPreserveTheExistingTravelCommand()
        {
            var c=FieldFixture.Config();var frame=Frame();var w=FieldFixture.World(frame,config:c);var self=w.GetUnit(1);
            var command=new BotTraversalAdapter(SimulationTests.Map(),c).Adapt(self,frame,Forward(w,1),50,w.GetUnit(50).UnitId,self.X+22,self.Y,self.Z);
            Assert.That(command.MoveX,Is.EqualTo(1));Assert.That(command.MoveY,Is.Zero);
        }
        [Test] public void CoreAssaultCommitsToMeleeInsteadOfChasingADistantVisibleEnemy()
        {
            var c=Config();c.botCentralApproachEnabled=true;var frame=Frame(400);var w=FieldFixture.World(frame,config:c);
            FieldFixture.MoveTo(w,6,14,-30);FieldFixture.Cmd(w,53,fire:true,yaw:270);w.Step();var self=w.GetUnit(6);
            Assert.That(self.Health,Is.LessThan(frame.MaxHealth*0.35f));
            var map=SimulationTests.Map();foreach(var b in map.mainBases)b.buildingRadius=20;
            var command=new BotTraversalAdapter(map,c,new PublicCapture()).Adapt(self,frame,Forward(w,6),50,100,self.X-60,self.Y,self.Z);
            Assert.That(command.WeaponSwitchDelta,Is.EqualTo(1));Assert.That(command.MoveX,Is.GreaterThan(0));
            Assert.That(command.HasAimPoint,Is.False);Assert.That(command.AimYaw,Is.EqualTo(90).Within(0.001));
        }
        [Test] public void MainForceUsesPublicCentralObjectiveInsteadOfItsOuterSpawnLane()
        {
            var c=Config();c.botCentralApproachEnabled=true;var frame=Frame();var w=FieldFixture.World(frame,config:c);var self=w.GetUnit(2);
            var command=new BotTraversalAdapter(SimulationTests.Map(),c,new PublicCapture()).Adapt(self,frame,Forward(w,2));
            Assert.That(command.MoveX,Is.GreaterThan(0));Assert.That(command.MoveY,Is.LessThan(0));
        }
        [Test] public void TraversalHomeSpawnClearsTheCoreWithoutLeavingItsSpawnArea()
        {
            var map=SimulationTests.Map();foreach(var b in map.mainBases){b.spawnRadius=10;b.attackRadius=3;}
            var c=FieldFixture.Config();var w=new BattleSimulation(map,c,-1);
            for(int slot=0;slot<100;slot++)
            {var u=w.GetUnit(slot);var home=map.mainBases[u.Team*3+slot%50%3];double distance=Math.Sqrt((u.X-home.x)*(u.X-home.x)+(u.Z-home.z)*(u.Z-home.z));
                Assert.That(distance,Is.GreaterThanOrEqualTo(3.4));Assert.That(distance,Is.LessThanOrEqualTo(10));}
            Assert.That(w.GetUnit(0).X,Is.EqualTo(-w.GetUnit(50).X).Within(0.001));
        }
        [Test] public void BotWalksAClimbableRampWithoutDetouringAroundItsFirstStep()
        {
            var c=FieldFixture.Config();var frame=Frame();var solids=new MechaBattle.Battlefield.ObstacleDefinition[3];
            for(int i=0;i<3;i++){solids[i]=FieldFixture.Box(x:-4+i*4,top:0.2f*(i+1),width:4.1f,depth:8);solids[i].id="ramp-"+i;}
            var map=SimulationTests.Map();map.obstacles=solids;var w=FieldFixture.World(frame,solids,c);FieldFixture.MoveTo(w,1,-8,0);var bot=new BotTraversalAdapter(map,c);float maxY=0;
            for(int i=0;i<200;i++){var u=w.GetUnit(1);var desired=new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,1,0,90,0,false,false,false,false,false);Assert.That(w.Submit(1,bot.Adapt(u,frame,desired)),Is.True);w.Step();maxY=Math.Max(maxY,w.GetUnit(1).Y);if(w.GetUnit(1).X>9)break;}
            Assert.That(w.GetUnit(1).X,Is.GreaterThan(9));Assert.That(Math.Abs(w.GetUnit(1).Z),Is.LessThan(0.2f));Assert.That(maxY,Is.GreaterThanOrEqualTo(0.6f));
        }
        [Test] public void BotReleasesJumpOnLandingAndRetriesAnObstacleInsteadOfHoldingForever()
        {
            var c=FieldFixture.Config();c.jumpSpeed=4;c.jumpBoostCost=2;c.maximumStepHeight=0;
            var frame=Frame();var solids=new[]{FieldFixture.Box(top:0.24f,width:2,depth:8)};var map=SimulationTests.Map();map.obstacles=solids;
            var w=FieldFixture.World(frame,solids,c);FieldFixture.MoveTo(w,1,-8,0);var bot=new BotTraversalAdapter(map,c);int presses=0;bool held=false;
            for(int i=0;i<220;i++){var u=w.GetUnit(1);var desired=new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,1,0,90,0,false,false,false,false,false);var command=bot.Adapt(u,frame,desired);if(command.Jump&&!held)presses++;held=command.Jump;Assert.That(w.Submit(1,command),Is.True);w.Step();if(w.GetUnit(1).X>4)break;}
            Assert.That(presses,Is.GreaterThanOrEqualTo(2));Assert.That(w.GetUnit(1).X,Is.GreaterThan(4));Assert.That(Math.Abs(w.GetUnit(1).Z),Is.LessThan(0.2f));
        }
        [Test] public void FlankingRoleEntersTheSideLaneFromItsOwnRearInsteadOfCrossingTheCenter()
        {
            var c=Config();c.botCentralApproachEnabled=true;var frame=Frame();var w=FieldFixture.World(frame,config:c);
            var command=new BotTraversalAdapter(SimulationTests.Map(),c,new PublicCapture()).Adapt(w.GetUnit(9),frame,Forward(w,9));
            Assert.That(command.MoveX,Is.Zero.Within(0.001));Assert.That(command.MoveY,Is.GreaterThan(0.9f));Assert.That(command.FirePrimary,Is.False);
        }
        [Test] public void BothTeamsUseOppositeRaidingLanesWithTheSameRoleCounts()
        {
            var c=Config();c.botCentralApproachEnabled=true;var frame=Frame();var w=FieldFixture.World(frame,config:c);var bot=new BotTraversalAdapter(SimulationTests.Map(),c,new PublicCapture());
            var blue=bot.Adapt(w.GetUnit(9),frame,Forward(w,9));var orange=bot.Adapt(w.GetUnit(59),frame,Forward(w,59));
            Assert.That(blue.MoveX,Is.Zero.Within(0.001));Assert.That(orange.MoveX,Is.Zero.Within(0.001));
            Assert.That(blue.MoveY,Is.GreaterThan(0.9f));Assert.That(orange.MoveY,Is.LessThan(-0.9f));
        }
        [Test] public void OuterRaiderPassesTheKnownWallBeforeTurningIntoTheEnemyBuilding()
        {
            var c=Config();c.botCentralApproachEnabled=true;c.botFlankOuterOffset=32;c.botFlankIngressDistance=15;
            var map=SimulationTests.Map();map.halfDepth=140;foreach(var b in map.mainBases){b.x=b.team==0?-60:60;b.buildingRadius=10;}
            map.obstacles=new[]{FieldFixture.Box(x:0,z:96,top:8,width:70,depth:3)};
            var frame=Frame();var decks=new UnitLoadout[100][];for(int slot=0;slot<100;slot++)decks[slot]=new[]{frame,frame};
            var w=new BattleSimulation(map,c,-1,decks);var bot=new BotTraversalAdapter(map,c,new PublicCapture());bool outer=false,reached=false;float maxZ=0;
            for(int tick=0;tick<3000;tick++)
            {
                var u=w.GetUnit(9);maxZ=Math.Max(maxZ,u.Z);outer|=u.Z>108;float dx=u.X-60,dz=u.Z-30;if(dx*dx+dz*dz<64){reached=true;break;}
                var command=bot.Adapt(u,frame,Forward(w,9));Assert.That(w.Submit(9,command),Is.True);w.Step();
            }
            Assert.That(outer,Is.True,"maxZ="+maxZ+" end="+w.GetUnit(9).X+","+w.GetUnit(9).Z);Assert.That(reached,Is.True,"Public route must pass the wall end rather than remaining in the inner shooting lane.");
        }
        [Test] public void FlankBotsDeployFromTheirOwnAssignedLaneWithoutOverridingHumanSpawn()
        {
            var c=Config();c.botCentralApproachEnabled=true;var map=SimulationTests.Map();var bots=new BattleSimulation(map,c,-1);
            Assert.That(bots.GetUnit(9).Z,Is.GreaterThan(20));Assert.That(bots.GetUnit(59).Z,Is.LessThan(-20));
            Assert.That(bots.GetUnit(0).Z,Is.LessThan(-20));Assert.That(bots.GetUnit(50).Z,Is.GreaterThan(20));
            var human=new BattleSimulation(map,c,9);Assert.That(human.GetUnit(9).Controller,Is.EqualTo(ControllerType.Human));
            Assert.That(human.GetUnit(9).Z,Is.LessThan(-20),"A BOT role must never relocate the human controller.");
        }
        [Test] public void CapturedRaisedRelayRespawnsOnItsSurfaceInsteadOfInsideTheTerrain()
        {
            var frame=FieldFixture.Gun(speed:0,damage:1100);var c=FieldFixture.Config();c.captureSecondsPerAdvantage=0.1f;
            var w=FieldFixture.World(frame,new[]{FieldFixture.Box(top:1.2f,width:12,depth:12)},c);
            FieldFixture.Cmd(w,x:1,jump:true);w.Step();for(int i=0;i<80&&w.GetUnit(0).X<0;i++){FieldFixture.Cmd(w,x:1);w.Step();}FieldFixture.Wait(w,60);
            Assert.That(w.GetCapturePoint(0).OwnerTeam,Is.EqualTo(0));w.SetAutomaticRespawn(0,false);
            var target=w.GetUnit(0);var shooter=w.GetUnit(50);
            Assert.That(w.Submit(50,new UnitCommand(shooter.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,270,0,false,true,false,false,false,
                hasAimPoint:true,aimTargetX:target.X,aimTargetY:target.Y+w.ShotHeight,aimTargetZ:target.Z)),Is.True);
            w.Step();Assert.That(w.GetUnit(0).IsAlive,Is.False);Assert.That(w.RequestSpawn(0,1,0),Is.True);FieldFixture.Wait(w,4);
            Assert.That(w.GetUnit(0).IsAlive,Is.True);Assert.That(w.GetUnit(0).Y,Is.EqualTo(1.2f).Within(0.001));
        }
    }
}
