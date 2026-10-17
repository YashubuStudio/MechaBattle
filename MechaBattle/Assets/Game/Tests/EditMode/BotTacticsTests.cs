using System;
using System.Linq;
using NUnit.Framework;
using MechaBattle.Battlefield;
using MechaBattle.Contracts;
using MechaBattle.Observation;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class BotTacticsTests
    {
        private static BotBehaviorRules Rules()=>new BotBehaviorRules{enabled=true,smartObjectivesEnabled=true,squadTacticsEnabled=true,radarFraction=0,interceptorFraction=0,frontlineMapReaderFraction=1,mapThinkSeconds=.1f,mapReactionSeconds=.2f,objectiveCommitSeconds=30};
        private static BattleSimulation World(MapDefinition map,int human=-1){var c=FieldFixture.Config();c.maxDurationSeconds=1000;return new BattleSimulation(map,c,human);}
        private static void Gather(BattleSimulation w,int first,float x,float z){for(int i=first;i<first+4;i++)FieldFixture.MoveTo(w,i,x,z);}
        private static MapDefinition TwoRelays(){var map=SimulationTests.Map();map.capturePoints=new[]{new CapturePointDefinition{id="North",x=0,z=-20,radius=3},new CapturePointDefinition{id="South",x=0,z=20,radius=3}};return map;}
        [Test] public void OnlyTheAppointedSquadLeadersMayUseSpecialBattlePaintOnEitherTeam()
        {var map=SimulationTests.Map();var c=new BotSquadCoordinator(World(map,0),map,Rules());Assert.That(Enumerable.Range(0,100).Where(c.IsLeader).ToArray(),Is.EqualTo(new[]{0,4,50,54}));Assert.That(c.IsLeader(-1)||c.IsLeader(100),Is.False);Assert.That(c.SquadIndex(8),Is.EqualTo(-1));}
        [Test] public void ATeamWithNoSquadsHasNoSpecialColorSlots()
        {var rules=Rules();rules.squadsPerTeam=0;var map=SimulationTests.Map();var c=new BotSquadCoordinator(World(map),map,rules);Assert.That(Enumerable.Range(0,100).Any(c.IsLeader),Is.False);}
        [TestCase(-1,4)] [TestCase(2,0)] [TestCase(13,4)] [TestCase(int.MaxValue,2)]
        public void InvalidSquadCountsCannotOverflowOrExceedTheFiftySlotTeam(int count,int size)
        {var rules=Rules();rules.squadsPerTeam=count;rules.squadSize=size;Assert.Throws<ArgumentException>(()=>rules.Validate());}
        [TestCase(0)] [TestCase(51)] public void InvalidResponseLimitsAreRejected(int limit){var rules=Rules();rules.maximumRespondersPerContact=limit;Assert.Throws<ArgumentException>(()=>rules.Validate());}
        [Test] public void BothTeamsShareTheirOwnSquadRelayGoalWithoutAnyEnemyReport()
        {var map=SimulationTests.Map();var w=World(map);Gather(w,0,-30,-30);Gather(w,50,30,-30);var c=new BotSquadCoordinator(w,map,Rules());foreach(int slot in new[]{1,2,51,52}){var goal=c.GetObjective(w.GetUnit(slot),false);Assert.That(goal.HasGoal,Is.True);Assert.That(goal.X,Is.Zero);Assert.That(goal.Z,Is.Zero);}Assert.That(w.GetTeamContact(0,50).Valid||w.GetTeamContact(1,0).Valid,Is.False);}
        [Test] public void AHumanLeaderRemainsFreeWhileBotMembersMayCoordinateAroundTheLeader()
        {var map=SimulationTests.Map();var w=World(map,0);var c=new BotSquadCoordinator(w,map,Rules());Assert.That(c.GetObjective(w.GetUnit(0),true).HasGoal,Is.False);FieldFixture.MoveTo(w,1,-30,-30);Assert.That(c.GetObjective(w.GetUnit(1),false).HasGoal,Is.True);SimulationTests.Command(w,0,x:1);w.Step();Assert.That(w.GetUnit(0).X,Is.GreaterThan(-30));}
        [Test] public void NonReadersOutsideASquadStillIgnoreMapReportsAndObjectives()
        {var map=SimulationTests.Map();var rules=Rules();rules.frontlineMapReaderFraction=0;var w=World(map);var p=new BotBattlefieldPlanner(w,map,rules,new BotPopulation(rules,1));for(int i=0;i<80;i++){Assert.That(p.GetIntent(12,NearbyEnemy.None).HasGoal,Is.False);w.Step();}Assert.That(p.GetThinkCount(12),Is.Zero);}
        [Test] public void DifferentGroupsDistributeAcrossRelaysInsteadOfAllFollowingTheClosestOne()
        {var map=TwoRelays();var w=World(map);FieldFixture.MoveTo(w,12,-30,0);FieldFixture.MoveTo(w,13,-30,0);var c=new BotSquadCoordinator(w,map,Rules());var a=c.GetObjective(w.GetUnit(12),true);var d=c.GetObjective(w.GetUnit(13),true);Assert.That(a.HasGoal&&d.HasGoal,Is.True);Assert.That(a.Z,Is.Not.EqualTo(d.Z));}
        [Test] public void ObjectiveCommitmentPreventsAWholeSquadFromChangingItsMindEveryCheck()
        {var map=TwoRelays();var w=World(map);Gather(w,0,-30,-30);var c=new BotSquadCoordinator(w,map,Rules());var first=c.GetObjective(w.GetUnit(0),true);FieldFixture.MoveTo(w,0,-30,30);var held=c.GetObjective(w.GetUnit(0),true);Assert.That(held.Z,Is.EqualTo(first.Z));}
        [Test] public void CapturingTheSharedRelayImmediatelyReleasesTheOldGoal()
        {var map=TwoRelays();var w=World(map);Gather(w,0,-30,-30);var c=new BotSquadCoordinator(w,map,Rules());var first=c.GetObjective(w.GetUnit(0),true);int relay=first.Z<0?0:1;FieldFixture.MoveTo(w,6,0,first.Z);FieldFixture.Wait(w,65);Assert.That(w.GetCapturePoint(relay).OwnerTeam,Is.EqualTo(0));var next=c.GetObjective(w.GetUnit(0),true);Assert.That(next.Z,Is.Not.EqualTo(first.Z));}
        [Test] public void ASeparatedMemberRejoinsItsLivingLeaderAndAssemblyWaitIsBounded()
        {var map=SimulationTests.Map();var w=World(map);FieldFixture.MoveTo(w,1,-30,40);var c=new BotSquadCoordinator(w,map,Rules());var follow=c.GetObjective(w.GetUnit(1),false);Assert.That(follow.X,Is.EqualTo(w.GetUnit(0).X));Assert.That(follow.Z,Is.EqualTo(w.GetUnit(0).Z));var wait=c.GetObjective(w.GetUnit(0),true);Assert.That(wait.X,Is.EqualTo(w.GetUnit(0).X));FieldFixture.Wait(w,101);var travel=c.GetObjective(w.GetUnit(0),true);Assert.That(travel.X,Is.Zero);}
        [Test] public void InterceptorsLimitRespondersOnBothTeamsButDoNotDropTheReactionDelay()
        {
            var rules=Rules();rules.squadTacticsEnabled=false;rules.interceptorFraction=1;rules.frontlineMapReaderFraction=0;
            var w=SupportFixture.World(SupportFixture.Gear());var map=SimulationTests.Map();var p=new BotBattlefieldPlanner(w,map,rules,new BotPopulation(rules,1));
            for(int tick=0;tick<100;tick++){FieldFixture.Cmd(w,0,locked:50);FieldFixture.Cmd(w,50,locked:0,yaw:-90);w.Step();for(int slot=0;slot<100;slot++)p.GetIntent(slot,NearbyEnemy.None);if(tick<10)Assert.That(p.InterceptionOrders,Is.Zero);}
            for(int team=0;team<2;team++){var target=w.GetTeamContact(team,team==0?50:0);Assert.That(target.Valid,Is.True);int count=0;for(int slot=team*50;slot<team*50+50;slot++){var intent=p.GetIntent(slot,NearbyEnemy.None);if(intent.HasGoal&&Math.Abs(intent.X-target.X)<.01f&&Math.Abs(intent.Z-target.Z)<.01f)count++;}Assert.That(count,Is.InRange(1,2));}
        }
        [TestCase(9)] [TestCase(18)] [TestCase(36)] [TestCase(45)]
        public void IndependentRaidersOnBothTeamsAreNotRecalledToPublicRelays(int local)
        {var map=SimulationTests.Map();var w=World(map);var c=new BotSquadCoordinator(w,map,Rules());foreach(int slot in new[]{local,local+50})Assert.That(c.GetObjective(w.GetUnit(slot),true).HasGoal,Is.False);}
        [Test] public void AReturningLeaderDoesNotPullHealthyMembersBackToItsReturnChannel()
        {
            var map=SimulationTests.Map();var config=FieldFixture.Config();config.emergencyReturnEnabled=true;config.emergencyReturnSeconds=5;config.returnHealthPerSecond=config.returnShieldPerSecond=100;config.returnAmmoPerSecond=30;
            var w=new BattleSimulation(map,config,-1);for(int slot=1;slot<4;slot++)FieldFixture.MoveTo(w,slot,-30,40);
            var leader=w.GetUnit(0);w.Submit(0,new UnitCommand(leader.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,90,0,false,false,false,false,false,emergencyReturn:true));w.Step();Assert.That(w.GetUnit(0).ReturnChanneling,Is.True);
            var c=new BotSquadCoordinator(w,map,Rules());var goal=c.GetObjective(w.GetUnit(1),false);Assert.That(goal.X,Is.Zero);Assert.That(goal.Z,Is.Zero);
        }
        [Test] public void ASeriouslyWoundedLeaderDoesNotBecomeTheSquadsRegroupDestination()
        {
            var w=SupportFixture.World(SupportFixture.Gear(damage:250,shield:0));FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();Assert.That(w.GetUnit(0).Health,Is.EqualTo(250));for(int slot=1;slot<4;slot++)FieldFixture.MoveTo(w,slot,-30,40);
            var c=new BotSquadCoordinator(w,SimulationTests.Map(),Rules());var goal=c.GetObjective(w.GetUnit(1),false);Assert.That(goal.X,Is.Zero);Assert.That(goal.Z,Is.Zero);
        }
        [Test] public void RadarSupportSpreadsThreeFiniteChargesWithoutReplacingItsLiveNetwork()
        {
            var rules=Rules();rules.radarFraction=1;rules.frontlineMapReaderFraction=0;rules.radarMinimumIntervalSeconds=.4f;
            var gun=SupportFixture.Gear();var radar=SupportFixture.Gear(WeaponKind.Radar,noSupply:true,duration:200);var frame=gun.WithWeapons(gun,radar);var decks=Enumerable.Range(0,100).Select(i=>new[]{frame,frame}).ToArray();var w=new BattleSimulation(SimulationTests.Map(),FieldFixture.Config(),-1,decks);FieldFixture.MoveTo(w,0,-8,0);
            var map=SimulationTests.Map();var planner=new BotBattlefieldPlanner(w,map,rules,new BotPopulation(rules,1));var adapter=new BotTraversalAdapter(map,FieldFixture.Config());var input=new CombatInputAdapter();int count=0;
            for(int tick=0;tick<4000;tick++){var self=w.GetUnit(0);var intent=planner.GetIntent(0,NearbyEnemy.None);var desired=new UnitCommand(self.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,90,0,false,false,false,false,false);var cmd=adapter.Adapt(self,frame,desired,intent:intent);int next=(self.ActiveWeaponIndex+cmd.WeaponSwitchDelta+frame.WeaponCount)%frame.WeaponCount;w.Submit(0,input.Adapt(self,frame.GetWeapon(next),cmd));w.Step();count=Enumerable.Range(0,w.DeployableCapacity).Count(i=>{var d=w.GetDeployable(i);return d.Active&&d.OwnerSlot==0&&d.Kind==WeaponKind.Radar;});if(count==3)break;}
            Assert.That(count,Is.EqualTo(3));Assert.That(w.GetWeaponAmmo(0,1),Is.EqualTo(radar.MaxAmmo-3));FieldFixture.Wait(w,100);Assert.That(w.GetWeaponAmmo(0,1),Is.EqualTo(radar.MaxAmmo-3));
        }
    }
}
