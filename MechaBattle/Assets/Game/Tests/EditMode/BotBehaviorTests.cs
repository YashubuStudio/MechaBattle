using System;
using System.Linq;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
using MechaBattle.Construction;
using MechaBattle.Observation;
namespace MechaBattle.Tests
{
    public sealed class BotBehaviorTests
    {
        private static BotBehaviorRules Rules(float radar=.04f,float defense=.08f,float readers=.35f)=>new BotBehaviorRules{enabled=true,radarFraction=radar,interceptorFraction=defense,frontlineMapReaderFraction=readers,mapThinkSeconds=.2f,mapReactionSeconds=.3f};
        [Test] public void RareRolesAreBalancedAcrossTeamsAndExcludeHumanAndRemoteSlots()
        {
            var mask=Enumerable.Repeat(true,100).ToArray();mask[0]=mask[50]=false;var p=new BotPopulation(Rules(),4,mask);
            for(int team=0;team<2;team++){var ids=Enumerable.Range(team*50,50).Where(p.IsBot).ToArray();Assert.That(ids.Count(i=>p.RoleOf(i)==BotRole.RadarSupport),Is.EqualTo(2));Assert.That(ids.Count(i=>p.RoleOf(i)==BotRole.Interceptor),Is.EqualTo(4));Assert.That(ids.Count(p.ReadsMap),Is.EqualTo(21));}
            Assert.That(p.ReadsMap(0)||p.ReadsMap(50),Is.False);
        }
        [Test] public void FixedSeedReproducesRolesAndAnotherMatchDrawsDifferentSlots()
        {var a=new BotPopulation(Rules(),1);var b=new BotPopulation(Rules(),1);var c=new BotPopulation(Rules(),2);Assert.That(Enumerable.Range(0,100).All(i=>a.RoleOf(i)==b.RoleOf(i)&&a.ReadsMap(i)==b.ReadsMap(i)),Is.True);Assert.That(Enumerable.Range(0,100).Any(i=>a.RoleOf(i)!=c.RoleOf(i)),Is.True);}
        [TestCase(0f,0f)] [TestCase(1f,0f)] [TestCase(0f,1f)]
        public void ZeroAndFullRoleFractionsHaveNoUnexpectedSpecialists(float radar,float defense)
        {var p=new BotPopulation(Rules(radar,defense,0),7,0);var ids=Enumerable.Range(1,49).ToArray();Assert.That(ids.Count(i=>p.RoleOf(i)==BotRole.RadarSupport),Is.EqualTo(radar*49));Assert.That(ids.Count(i=>p.RoleOf(i)==BotRole.Interceptor),Is.EqualTo(defense*49));}
        [TestCase(float.NaN)] [TestCase(-.1f)] [TestCase(1.1f)]
        public void InvalidRoleFractionsAreRejected(float value){Assert.Throws<ArgumentException>(()=>Rules(value).Validate());}
        [Test] public void RolesCannotExceedTheAvailablePopulation(){Assert.Throws<ArgumentException>(()=>Rules(.7f,.7f).Validate());}
        [Test] public void RadarRoleUsesAnExistingLegalRadarPresetAndDoesNotChangeNormalDeckSelection()
        {var gun=SupportFixture.Gear();var radar=SupportFixture.Gear(WeaponKind.Radar,noSupply:true);var frames=new[]{gun,gun.WithWeapons(gun,radar)};Assert.That(BotDeckPolicy.SelectPreset(frames,1,1,BotRole.RadarSupport),Is.EqualTo(1));Assert.That(BotDeckPolicy.SelectPreset(frames,1,1,BotRole.Frontline),Is.Zero);Assert.Throws<ArgumentException>(()=>BotDeckPolicy.SelectPreset(new[]{gun},0,0,BotRole.RadarSupport));}
        private static BattleSimulation World(BotBehaviorRules rules,bool wall=false,float duration=10)
        {
            var map=SimulationTests.Map();if(wall)map.obstacles=new[]{FieldFixture.Box(width:2,depth:190)};
            var c=BotPerceptionTests.Config();c.botBehavior=rules;c.maxDurationSeconds=1000;c.baseCounterFireDamage=c.baseCounterFireIntervalSeconds=c.baseCounterFireRadius=0;
            var gun=SupportFixture.Gear(damage:1000,shield:0);var radar=SupportFixture.Gear(WeaponKind.Radar,shield:0,noSupply:true,duration:duration);var frame=gun.WithWeapons(gun,radar);
            var decks=new UnitLoadout[100][];for(int i=0;i<100;i++)decks[i]=new[]{frame,frame};
            var w=new BattleSimulation(map,c,-1,decks);FieldFixture.MoveTo(w,0,-8,0);if(wall){FieldFixture.MoveTo(w,50,8,-98);FieldFixture.MoveTo(w,50,-20,-98);}FieldFixture.MoveTo(w,50,-20,0);FieldFixture.MoveTo(w,1,-10,0);return w;
        }
        private static void Place(BattleSimulation w){FieldFixture.Cmd(w,1,fire:true,change:1);w.Step();FieldFixture.Wait(w,30);Assert.That(w.GetTeamContact(0,50).Valid,Is.True);}
        [Test] public void UnobservedRaiderDoesNotAttractADefender()
        {var rules=Rules(0,1);var w=World(rules);var pop=new BotPopulation(rules,1);var planner=new BotBattlefieldPlanner(w,SimulationTests.Map(),rules,pop);for(int i=0;i<100;i++){planner.GetIntent(0,NearbyEnemy.None);w.Step();}Assert.That(planner.InterceptionOrders,Is.Zero);}
        [Test] public void SharedRadarReportRedirectsDefenderAfterDelayButNeverBecomesAFiringTarget()
        {
            var rules=Rules(0,1);var w=World(rules,wall:true);Place(w);var pop=new BotPopulation(rules,1);var planner=new BotBattlefieldPlanner(w,SimulationTests.Map(),rules,pop);var perception=new BotPerception(w,BotPerceptionTests.Config(),pop);
            Assert.That(planner.InterceptionOrders,Is.Zero);bool pursued=false;
            for(int i=0;i<150;i++){var intent=planner.GetIntent(0,NearbyEnemy.None);if(planner.InterceptionOrders>0){pursued=true;Assert.That(intent.X,Is.EqualTo(w.GetTeamContact(0,50).X));break;}w.Step();}
            Assert.That(pursued,Is.True);var own=w.GetUnit(0);var desired=new UnitCommand(own.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,90,0,false,false,false,false,false);var action=new BotTraversalAdapter(SimulationTests.Map(),BotPerceptionTests.Config()).Adapt(own,w.GetLoadout(0,own.DeckIndex),desired,intent:planner.GetIntent(0,NearbyEnemy.None));Assert.That(action.FirePrimary,Is.False,"Map intent must not fire");Assert.That(action.HasAimPoint,Is.False,"Map intent must not set a ballistic aim point");Assert.That(action.LockedTargetSlot,Is.EqualTo(-1));var last=planner.GetIntent(0,NearbyEnemy.None);FieldFixture.Cmd(w,50,x:-1);w.Step();Assert.That(planner.GetIntent(0,NearbyEnemy.None).X,Is.EqualTo(last.X),"Between map checks, movement must use the last reported position.");
            for(int turn=0;turn<105;turn++){FieldFixture.Cmd(w,0,yaw:-90);w.Step();}FieldFixture.Cmd(w,0,fire:true,yaw:-90);w.Step();Assert.That(w.GetUnit(50).IsAlive,Is.False,"Fixture respects the BOT turn-rate limit before firing.");Assert.That(planner.GetIntent(0,NearbyEnemy.None).X,Is.Not.EqualTo(-20),"An invalidated life/report must cancel pursuit.");
        }
        [Test] public void NonReadersIgnoreMapReportsAndDoNotTurnToAnUnseenRaider()
        {
            var rules=Rules(0,0,0);var w=World(rules,wall:true);Place(w);var pop=new BotPopulation(rules,1);var planner=new BotBattlefieldPlanner(w,SimulationTests.Map(),rules,pop);var sensor=new BotPerception(w,BotPerceptionTests.Config(),pop);
            for(int i=0;i<100;i++){Assert.That(planner.GetIntent(0,NearbyEnemy.None).HasGoal,Is.False);Assert.That(sensor.FindNearestEnemy(0,80).Found,Is.False);w.Step();}Assert.That(planner.GetThinkCount(0),Is.Zero);
        }
        [Test] public void ExpiredRadarContactCancelsTheDefendersInvestigation()
        {
            var rules=Rules(0,1);var w=World(rules,duration:3);Place(w);var planner=new BotBattlefieldPlanner(w,SimulationTests.Map(),rules,new BotPopulation(rules,1));
            for(int i=0;i<70;i++){planner.GetIntent(0,NearbyEnemy.None);w.Step();}Assert.That(planner.InterceptionOrders,Is.GreaterThan(0));FieldFixture.Wait(w,200);Assert.That(w.GetTeamContact(0,50).Valid,Is.False);Assert.That(planner.GetIntent(0,NearbyEnemy.None).X,Is.Not.EqualTo(-20));
        }
        [Test] public void RadarPlannerActuallyPlacesFiniteAmmoThenKeepsItsLiveRadar()
        {
            var rules=Rules(1,0);var w=World(rules);var map=SimulationTests.Map();var pop=new BotPopulation(rules,1);var planner=new BotBattlefieldPlanner(w,map,rules,pop);var adapter=new BotTraversalAdapter(map,BotPerceptionTests.Config());var input=new CombatInputAdapter();bool placed=false;
            for(int i=0;i<1500;i++)
            {var self=w.GetUnit(0);var frame=w.GetLoadout(0,self.DeckIndex);var intent=planner.GetIntent(0,NearbyEnemy.None);var desired=new UnitCommand(self.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,90,0,false,false,false,false,false);var cmd=adapter.Adapt(self,frame,desired,intent:intent);int next=(self.ActiveWeaponIndex+cmd.WeaponSwitchDelta+frame.WeaponCount)%frame.WeaponCount;w.Submit(0,input.Adapt(self,frame.GetWeapon(next),cmd));w.Step();for(int j=0;j<w.DeployableCapacity;j++){var d=w.GetDeployable(j);if(d.Active&&d.OwnerSlot==0&&d.Kind==WeaponKind.Radar)placed=true;}if(placed&&i>300)break;}
            Assert.That(placed,Is.True);Assert.That(w.GetWeaponAmmo(0,1),Is.EqualTo(11));
        }
        [Test] public void PlannerNeverOverridesAHumanEvenWithAnIncorrectPopulationMask()
        {var rules=Rules(1,0);var c=BotPerceptionTests.Config();c.botBehavior=rules;var map=SimulationTests.Map();var w=new BattleSimulation(map,c,0);var p=new BotBattlefieldPlanner(w,map,rules,new BotPopulation(rules,1));Assert.That(p.GetIntent(0,NearbyEnemy.None).HasGoal,Is.False);}
        private sealed class PublicRelays:ICapturePointStatusSource
        {
            public int CapturePointCount=>3;
            public CapturePointStatus GetCapturePoint(int index)=>new CapturePointStatus(-1,0,-30);
        }
        [Test] public void TacticalLayerPathDoesNotRestartAtTheNearestNodeEveryTick()
        {
            var map=SimulationTests.Map();map.navigationNodes=new[]{
                new MechaBattle.Battlefield.NavigationNodeDefinition{id="start",x=-8,y=0,z=0,links=new[]{1}},
                new MechaBattle.Battlefield.NavigationNodeDefinition{id="junction",x=0,y=0,z=0,links=new[]{0,2}},
                new MechaBattle.Battlefield.NavigationNodeDefinition{id="goal",x=10,y=0,z=0,links=new[]{1}}};
            var c=FieldFixture.Config();c.botCentralApproachEnabled=true;var w=new BattleSimulation(map,c,-1);FieldFixture.MoveTo(w,6,-8,0);
            var adapter=new BotTraversalAdapter(map,c,new PublicRelays());
            for(int i=0;i<250;i++){var u=w.GetUnit(6);var desired=new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,90,0,false,false,false,false,false);var cmd=adapter.Adapt(u,w.GetLoadout(6,u.DeckIndex),desired,intent:new BotTacticalIntent(10,0,0,.5f));Assert.That(w.Submit(6,cmd),Is.True);w.Step();}
            Assert.That(w.GetUnit(6).X,Is.GreaterThan(9),"The ordinary relay goal must not rebuild the same cache before the tactical route.");
        }
        [Test] public void LockEvasionDoesNotTurnAnAffordableCoreSwingIntoAnUnaffordableDash()
        {
            var map=SimulationTests.Map();foreach(var b in map.mainBases){b.buildingRadius=26;b.attackRadius=8;}
            var c=FieldFixture.Config();c.botCentralApproachEnabled=c.botLockEvadeEnabled=c.boostOverheatEnabled=true;
            c.botLockEvadeStrafeFraction=.6f;c.botLockEvadePeriodSeconds=.28f;c.boostCapacity=20;c.boostOverheatRecoveryPerSecond=20;
            c.meleeAttacks=new MeleeAttackDefinition[5];for(int i=0;i<5;i++)c.meleeAttacks[i]=new MeleeAttackDefinition{style=(MeleeStyle)i,damage=30,downPoints=350,range=3.5f,halfAngleDegrees=60,windupSeconds=.1f,recoverySeconds=.2f,cooldownSeconds=.3f,lungeMeters=0,boostCost=i==2||i==3?30:0,moveMultiplier=.4f,comboSteps=1,maximumTargets=3,minimumChargeSeconds=i==4?.4f:0,fullChargeSeconds=i==4?1.2f:0,minimumChargeMultiplier=i==4?.3f:0};
            var gun=FieldFixture.Gun();var frame=gun.WithWeapons(gun,FieldFixture.Gun(kind:WeaponKind.Melee,range:3.4f,red:0));var decks=new UnitLoadout[100][];for(int i=0;i<100;i++)decks[i]=new[]{frame,frame};
            var w=new BattleSimulation(map,c,-1,decks);FieldFixture.MoveTo(w,6,20,0);FieldFixture.MoveTo(w,50,20,12);FieldFixture.Cmd(w,50,locked:6);w.Step();var self=w.GetUnit(6);Assert.That(self.IncomingLockCount,Is.GreaterThan(0));Assert.That(self.Boost,Is.LessThan(30));
            var adapter=new BotTraversalAdapter(map,c);var desired=new UnitCommand(self.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,90,0,false,false,false,false,false);var cmd=adapter.Adapt(self,frame,desired);
            Assert.That(cmd.Melee,Is.True,"Fixture is inside normal core swing range.");Assert.That(cmd.Boost,Is.False,"Evasion must not force a 30-cost dash on a normal swing with only 20 boost.");
            w.Submit(6,cmd);w.Step();Assert.That(w.GetUnit(6).LastMeleeTick,Is.GreaterThanOrEqualTo(0));FieldFixture.Wait(w,6);Assert.That(w.Counters.BaseHits,Is.GreaterThan(0));
        }
        [Test] public void MapInvestigationDoesNotPullAnAssaultOutOfAnEnemyCore()
        {
            var map=SimulationTests.Map();foreach(var b in map.mainBases)b.buildingRadius=26;
            var c=FieldFixture.Config();c.botCentralApproachEnabled=true;var w=new BattleSimulation(map,c,-1);FieldFixture.MoveTo(w,6,14,0);
            var self=w.GetUnit(6);var desired=new UnitCommand(self.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,90,0,false,true,false,false,false);
            var cmd=new BotTraversalAdapter(map,c).Adapt(self,w.GetLoadout(6,self.DeckIndex),desired,intent:new BotTacticalIntent(-20,0,0,2));
            Assert.That(cmd.FirePrimary,Is.True,"A reported map contact must not cancel an already committed core attack.");
            Assert.That(cmd.MoveX,Is.Zero);Assert.That(cmd.HasAimPoint,Is.False);Assert.That(cmd.LockedTargetSlot,Is.EqualTo(-1));
        }
        [Test] public void TacticalMapMovementDoesNotCancelAnAlreadySelectedRecoveryPod()
        {
            var gun=SupportFixture.Gear(damage:180,shield:0);var pod=SupportFixture.Gear(WeaponKind.RepairPod,shield:0);var frame=gun.WithWeapons(gun,pod);var w=FieldFixture.World(frame);
            FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();var self=w.GetUnit(0);Assert.That(self.Health,Is.EqualTo(320));
            var desired=new UnitCommand(self.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,90,0,false,false,false,false,false);
            var cmd=new BotTraversalAdapter(SimulationTests.Map(),FieldFixture.Config()).Adapt(self,frame,desired,intent:new BotTacticalIntent(40,0,0,2));
            Assert.That(cmd.FirePrimary,Is.True);Assert.That(cmd.WeaponSwitchDelta,Is.EqualTo(1));Assert.That(cmd.MoveX,Is.Zero);w.Submit(0,cmd);w.Step();Assert.That(w.GetDeployable(0).Kind,Is.EqualTo(WeaponKind.RepairPod));Assert.That(w.GetDeployable(0).Active,Is.True);
        }
    }
}
