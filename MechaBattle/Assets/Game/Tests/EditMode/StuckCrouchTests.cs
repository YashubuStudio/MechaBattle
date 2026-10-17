using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class StuckCrouchTests
    {
        private static SimulationConfig Config()
        {
            var c=FieldFixture.Config();c.crouchEnabled=true;c.crouchBoostRecoveryMultiplier=2;c.boostOverheatEnabled=true;c.boostOverheatRecoveryPerSecond=20;
            c.emergencyReturnEnabled=true;c.emergencyReturnSeconds=.4f;c.returnHealthPerSecond=1200;c.returnShieldPerSecond=600;c.returnAmmoPerSecond=30;
            c.botStuckRecoveryEnabled=true;c.botStuckWatchSeconds=.3f;c.botStuckMinimumProgress=.1f;c.botStuckAvoidSeconds=.3f;c.botStuckAvoidAttempts=2;return c;
        }
        private static UnitCommand Input(BattleSimulation w,int slot=0,float x=0,float z=0,bool boost=false,bool crouch=false,bool back=false,bool jump=false,bool fire=false,bool melee=false,float yaw=90)
        {var u=w.GetUnit(slot);var c=new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,x,z,yaw,0,boost,fire,false,melee,false,jump:jump,emergencyReturn:back,crouch:crouch);Assert.That(w.Submit(slot,c),Is.True);return c;}
        private static void Spend(BattleSimulation w,int ticks=20)
        {for(int i=0;i<ticks;i++){Input(w,x:1,boost:true);w.Step();}}
        [Test] public void GroundedCrouchDoublesRecoveryAndReleaseRestoresTheOrdinaryRate()
        {
            var c=Config();var w=FieldFixture.World(config:c);Spend(w);float start=w.GetUnit(0).Boost;
            Input(w,crouch:true);w.Step();Assert.That(w.GetUnit(0).Crouching,Is.True);float extra=w.GetUnit(0).Boost-start;Assert.That(extra,Is.EqualTo(c.boostRecoveryPerSecond*2*w.FixedDeltaTime).Within(.001));
            start=w.GetUnit(0).Boost;Input(w);w.Step();Assert.That(w.GetUnit(0).Crouching,Is.False);Assert.That(w.GetUnit(0).Boost-start,Is.EqualTo(extra*.5f).Within(.001));
        }
        [Test] public void CrouchingDoublesOverheatRecoveryWithoutUnlockingBoostEarly()
        {
            var c=Config();c.boostCapacity=100;c.boostDrainPerSecond=500;var w=FieldFixture.World(config:c);Spend(w,10);Assert.That(w.GetUnit(0).BoostOverheated,Is.True);
            Input(w,crouch:true);w.Step();Assert.That(w.GetUnit(0).Boost,Is.EqualTo(.8f).Within(.001));Assert.That(w.GetUnit(0).BoostOverheated,Is.True);
            for(int i=0;i<130;i++){Input(w,crouch:true);w.Step();}Assert.That(w.GetUnit(0).Boost,Is.EqualTo(100));Assert.That(w.GetUnit(0).BoostOverheated,Is.False);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void MovementBoostJumpAndMeleeCancelCrouch(int action)
        {var w=FieldFixture.World(config:Config());Input(w,crouch:true);w.Step();Input(w,crouch:true,x:action==0?1:0,boost:action==1,jump:action==2,melee:action==3);w.Step();Assert.That(w.GetUnit(0).Crouching,Is.False);}
        [Test] public void CrouchCannotRegenerateBoostInMidair()
        {var w=FieldFixture.World(config:Config());Input(w,jump:true);w.Step();float start=w.GetUnit(0).Boost;Input(w,crouch:true);w.Step();Assert.That(w.GetUnit(0).Grounded||w.GetUnit(0).Crouching,Is.False);Assert.That(w.GetUnit(0).Boost,Is.EqualTo(start));}
        [Test] public void ReturnChannelAutomaticallyCrouchesAndMovingOrBeingHitCancelsThePose()
        {
            var w=FieldFixture.World(FieldFixture.Gun(speed:0),config:Config());Input(w,back:true);w.Step();Assert.That(w.GetUnit(0).ReturnChanneling&&w.GetUnit(0).Crouching,Is.True);
            Input(w,x:1);w.Step();Assert.That(w.GetUnit(0).ReturnChanneling||w.GetUnit(0).Crouching,Is.False);
            Input(w,back:true);w.Step();Input(w,50,fire:true,yaw:-90);w.Step();Assert.That(w.GetUnit(0).Health,Is.LessThan(500));Assert.That(w.GetUnit(0).ReturnChanneling||w.GetUnit(0).Crouching,Is.False);
        }
        [Test] public void TriggerAdapterAndPortableNuclearFilterPreserveCrouchInput()
        {
            var w=FieldFixture.World(config:Config());var c=Input(w,crouch:true);var adapted=new CombatInputAdapter().Adapt(w.GetUnit(0),w.GetActiveWeapon(0),c);Assert.That(adapted.Crouch,Is.True);
            var carry=CarryFixture.World(config:Config());CarryFixture.Request(carry);CarryFixture.Pickup(carry);Input(carry,crouch:true);carry.Step();Assert.That(carry.GetUnit(0).CarryingNuclear,Is.True);Assert.That(carry.GetUnit(0).Crouching,Is.True);
        }
        private static MapDefinition Trap(bool closed)
        {
            var map=SimulationTests.Map();map.mainBases[0].spawnOverride=true;map.mainBases[0].spawnX=-8;map.mainBases[0].spawnZ=0;
            map.navigationNodes=new[]{new NavigationNodeDefinition{id="start",x=-8,links=new[]{1}},new NavigationNodeDefinition{id="goal",x=10,links=new[]{0}}};
            if(closed)map.obstacles=new[]{FieldFixture.Box(x:-8.65f,width:.4f,depth:2),FieldFixture.Box(x:-7.35f,width:.4f,depth:2),FieldFixture.Box(x:-8,z:-.65f,width:2,depth:.4f),FieldFixture.Box(x:-8,z:.65f,width:2,depth:.4f)};
            else map.obstacles=new[]{FieldFixture.Box(x:-6,width:1,depth:20)};
            for(int i=0;i<map.obstacles.Length;i++)map.obstacles[i].id="trap-"+i;return map;
        }
        private static UnitCommand Adapt(BattleSimulation w,BotTraversalAdapter adapter,int slot=6,float move=1)
        {var u=w.GetUnit(slot);return adapter.Adapt(u,w.GetLoadout(slot,u.DeckIndex),new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,move,0,90,0,false,false,false,false,false));}
        [Test] public void ABlockedNavigationGoalMakesTheBotTryAnotherDirection()
        {
            var map=Trap(false);var c=Config();c.botStuckAvoidSeconds=.5f;var w=new BattleSimulation(map,c,-1);var a=new BotTraversalAdapter(map,c);bool escaped=false;
            for(int i=0;i<300;i++){w.Submit(6,Adapt(w,a));w.Step();if(a.StuckAvoidanceCount>0&&Math.Abs(w.GetUnit(6).Z)>1){escaped=true;break;}}
            Assert.That(escaped,Is.True);Assert.That(a.StuckReturnCount,Is.Zero);
        }
        [Test] public void ACoreTangentEscapeDoesNotOnlyBackAwayThenWalkIntoTheSameSpot()
        {
            var map=SimulationTests.Map();var c=Config();c.baseCollisionEnabled=true;var home=map.mainBases[3];home.attackRadius=8;home.buildingRadius=26;home.spawnOverride=true;home.spawnX=home.x+8.05f;home.spawnZ=home.z+2.5f;
            var w=new BattleSimulation(map,c,-1);var a=new BotTraversalAdapter(map,c);bool passed=false;
            for(int i=0;i<400;i++){var u=w.GetUnit(77);var cmd=a.Adapt(u,w.GetLoadout(77,u.DeckIndex),new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,-1,180,0,false,false,false,false,false));w.Submit(77,cmd);w.Step();if(w.GetUnit(77).Z<home.z-3){passed=true;break;}}
            Assert.That(passed,Is.True,"Escape must make progress around the public core instead of repeating a backward-only detour.");
        }
        [Test] public void ReenteringTheSameInvalidWaypointAfterAnEscapeEventuallyRequestsReturn()
        {
            var map=Trap(false);var c=Config();c.botStuckAvoidSeconds=.2f;var w=new BattleSimulation(map,c,-1);var first=w.GetUnit(6);var a=new BotTraversalAdapter(map,c);
            for(int i=0;i<1000&&w.GetUnit(6).UnitId==first.UnitId;i++){var u=w.GetUnit(6);if(u.IsAlive)w.Submit(6,Adapt(w,a));w.Step();}
            Assert.That(a.StuckReturnCount,Is.GreaterThan(0),"Moving away then returning to the same blocked waypoint must not reset recovery forever.");Assert.That(w.GetUnit(6).UnitId,Is.Not.EqualTo(first.UnitId));
        }
        [Test] public void AnEnclosedBotReturnsNormallyThenRedeploysTheSameFrameAtAnotherBase()
        {
            var map=Trap(true);var c=Config();var w=new BattleSimulation(map,c,-1);var a=new BotTraversalAdapter(map,c);var first=w.GetUnit(6);double gauge=w.GetGauge(0);bool channel=false,reserve=false;
            for(int i=0;i<800;i++)
            {var u=w.GetUnit(6);if(u.IsAlive){var cmd=Adapt(w,a);if(u.ReturnChanneling){channel=true;Assert.That(cmd.MoveX*cmd.MoveX+cmd.MoveY*cmd.MoveY,Is.Zero);Assert.That(cmd.FirePrimary||cmd.Melee||cmd.EmergencyReturn,Is.False);}w.Submit(6,cmd);}w.Step();reserve|=w.GetEmergencyReturn(6).InReserve;if(w.GetUnit(6).UnitId!=first.UnitId)break;}
            Assert.That(a.StuckAvoidanceCount,Is.GreaterThanOrEqualTo(2));Assert.That(a.StuckReturnCount,Is.EqualTo(1));Assert.That(channel&&reserve,Is.True);
            var restored=w.GetUnit(6);Assert.That(restored.IsAlive,Is.True);Assert.That(restored.UnitId,Is.Not.EqualTo(first.UnitId));Assert.That(restored.DeckIndex,Is.EqualTo(first.DeckIndex));Assert.That(restored.X,Is.LessThan(-20));Assert.That(restored.Crouching,Is.False);
            Assert.That(w.GetSlotStats(6).Deaths,Is.Zero);Assert.That(w.GetGauge(0),Is.EqualTo(gauge));Assert.That(restored.Health,Is.EqualTo(first.Health));
        }
        [Test] public void WaitingRotatingAndSuccessfullyMovingDoNotTriggerStuckRecovery()
        {
            var map=SimulationTests.Map();var c=Config();var w=new BattleSimulation(map,c,-1);var a=new BotTraversalAdapter(map,c);
            for(int i=0;i<180;i++){var u=w.GetUnit(6);var cmd=a.Adapt(u,w.GetLoadout(6,u.DeckIndex),new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,i*2,0,false,false,false,false,false));w.Submit(6,cmd);w.Step();}
            for(int i=0;i<100;i++){w.Submit(6,Adapt(w,a,move:.5f));w.Step();}Assert.That(a.StuckAvoidanceCount+a.StuckReturnCount,Is.Zero);
        }
        [Test] public void ABlockedHumanNeverGetsAutomaticAvoidanceOrReturn()
        {var map=Trap(true);var c=Config();var w=new BattleSimulation(map,c,6);var a=new BotTraversalAdapter(map,c);for(int i=0;i<200;i++){var cmd=Adapt(w,a,6);Assert.That(cmd.EmergencyReturn,Is.False);w.Submit(6,cmd);w.Step();}Assert.That(a.StuckAvoidanceCount+a.StuckReturnCount,Is.Zero);}
        [Test] public void DisruptionIsNotMistakenForBeingStuck()
        {
            var c=Config();c.staggerSeconds=4;var gun=new UnitLoadout("stun-test",500,0,10,10,1,80,.1f,120,10,downPointCapacity:600,fireDownPoints:1000);var w=FieldFixture.World(gun,config:c);FieldFixture.MoveTo(w,1,-8,2);FieldFixture.MoveTo(w,50,8,2);Input(w,50,fire:true,yaw:-90);w.Step();Assert.That(w.GetUnit(1).Disruption,Is.EqualTo(DisruptionState.Staggered));
            var a=new BotTraversalAdapter(SimulationTests.Map(),c);for(int i=0;i<100;i++){w.Submit(1,Adapt(w,a,1));w.Step();}Assert.That(a.StuckAvoidanceCount+a.StuckReturnCount,Is.Zero);
        }
        [Test] public void AReturningHumanStaysInReserveUntilExplicitlyRedeployed()
        {var w=FieldFixture.World(config:Config());Input(w,back:true);w.Step();FieldFixture.Wait(w,80);Assert.That(w.GetEmergencyReturn(0).InReserve,Is.True);Assert.That(w.GetUnit(0).IsAlive,Is.False);}
        [Test] public void BotReturnWaitsForRealSupplyAndNeverRefillsForbiddenRadarAmmo()
        {
            var c=Config();c.returnHealthPerSecond=100;c.returnAmmoPerSecond=1;c.homeAmmoPerSecond=.1f;var gun=FieldFixture.Gun(speed:0,damage:220);var radar=ObjectFixture.Utility(WeaponKind.Radar);var frame=gun.WithWeapons(gun,radar);var decks=new UnitLoadout[100][];for(int i=0;i<100;i++)decks[i]=new[]{frame,frame};var w=new BattleSimulation(SimulationTests.Map(),c,-1,decks);
            FieldFixture.MoveTo(w,6,-8,0);FieldFixture.MoveTo(w,50,8,0);Input(w,50,fire:true,yaw:-90);w.Step();Assert.That(w.GetUnit(6).Health,Is.EqualTo(280));
            FieldFixture.Cmd(w,6,fire:true,change:1,yaw:0);w.Step();FieldFixture.Wait(w,40);FieldFixture.Cmd(w,6,fire:true,change:1);w.Step();int old=w.GetUnit(6).UnitId;
            Input(w,6,back:true);w.Step();FieldFixture.Wait(w,20);Assert.That(w.GetEmergencyReturn(6).InReserve,Is.True);FieldFixture.Wait(w,5);Assert.That(w.GetEmergencyReturn(6).InReserve,Is.True);Assert.That(w.GetUnit(6).Health,Is.LessThan(500));
            FieldFixture.Wait(w,160);Assert.That(w.GetUnit(6).IsAlive,Is.True);Assert.That(w.GetUnit(6).UnitId,Is.Not.EqualTo(old));Assert.That(w.GetUnit(6).Health,Is.EqualTo(500));Assert.That(w.GetWeaponAmmo(6,1),Is.EqualTo(4));Assert.That(w.GetSlotStats(6).Deaths,Is.Zero);
        }
        [Test] public void AHitInterruptsBotReturnAndRecoveryDoesNotSpamTheToggle()
        {
            var map=Trap(true);foreach(var box in map.obstacles){box.y=.25f;box.height=.5f;}map.capturePoints=new CapturePointDefinition[6];for(int i=0;i<6;i++)map.capturePoints[i]=new CapturePointDefinition{id="relay-"+i,x=10,radius=3};
            var c=Config();var gun=new UnitLoadout("return-hit",500,0,10,20,80,80,.1f,120,10,WeaponFireMode.SingleShot,1,.6f,blastRadius:3,splashDamage:40,projectileSpeed:80,redLockRange:24);var decks=new UnitLoadout[100][];for(int i=0;i<100;i++)decks[i]=new[]{gun,gun};var w=new BattleSimulation(map,c,-1,decks);FieldFixture.MoveTo(w,50,-4,0);var a=new BotTraversalAdapter(map,c);bool hit=false;
            for(int i=0;i<500;i++){w.Submit(0,Adapt(w,a,0));if(!hit&&w.GetUnit(0).ReturnChanneling){Input(w,50,fire:true,yaw:-90);hit=true;}w.Step();if(hit&&w.GetUnit(0).Health<500)break;}
            Assert.That(hit,Is.True);Assert.That(w.GetUnit(0).Health,Is.LessThan(500));Assert.That(w.GetUnit(0).ReturnChanneling||w.GetEmergencyReturn(0).InReserve,Is.False);int requests=a.StuckReturnCount;
            for(int i=0;i<40;i++){var cmd=Adapt(w,a,0);Assert.That(cmd.EmergencyReturn,Is.False);w.Submit(0,cmd);w.Step();}Assert.That(a.StuckReturnCount,Is.EqualTo(requests));
        }
        [Test] public void MeleeWindupAndRecoveryAreNotMistakenForBeingStuck()
        {
            var c=Config();c.meleeAttacks=new MeleeAttackDefinition[5];for(int i=0;i<5;i++)c.meleeAttacks[i]=new MeleeAttackDefinition{style=(MeleeStyle)i,damage=10,downPoints=1,range=3.5f,halfAngleDegrees=60,windupSeconds=1,recoverySeconds=2,cooldownSeconds=.1f,moveMultiplier=.02f,comboSteps=1,maximumTargets=3,minimumChargeSeconds=i==4?.4f:0,fullChargeSeconds=i==4?1.2f:0,minimumChargeMultiplier=i==4?.3f:0};
            var blade=FieldFixture.Gun(kind:WeaponKind.Melee,range:3.8f,red:0);var w=FieldFixture.World(blade,config:c);Input(w,1,melee:true);w.Step();Assert.That(w.GetUnit(1).MeleePhase,Is.GreaterThanOrEqualTo(0));var a=new BotTraversalAdapter(SimulationTests.Map(),c);
            for(int i=0;i<80;i++){w.Submit(1,Adapt(w,a,1));w.Step();}Assert.That(a.StuckAvoidanceCount+a.StuckReturnCount,Is.Zero);
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)]
        public void InvalidStuckWatchIsRejected(float seconds){var c=Config();c.botStuckWatchSeconds=seconds;Assert.Throws<ArgumentException>(()=>c.Validate());}
        [TestCase(0f)] [TestCase(float.NaN)] [TestCase(11f)]
        public void InvalidCrouchRecoveryIsRejected(float multiplier){var c=Config();c.crouchBoostRecoveryMultiplier=multiplier;Assert.Throws<ArgumentException>(()=>c.Validate());}
    }
}
