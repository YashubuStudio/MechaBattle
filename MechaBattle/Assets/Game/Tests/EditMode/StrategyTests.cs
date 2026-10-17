using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class StrategyTests
    {
        internal static StrategyRules Rules()=>new StrategyRules{enabled=true,initialPoints=600,maximumPoints=1000,pointsPerLoss=25,
            nuclear=new StrategyDefinition{cost=600,cooldownSeconds=2,delaySeconds=.1f,durationSeconds=.2f,radius=20,unitDamage=1000,coreDamage=120},
            jammer=new StrategyDefinition{cost=150,cooldownSeconds=1,delaySeconds=.02f,durationSeconds=.4f,radius=30},
            wideRadar=new StrategyDefinition{cost=100,cooldownSeconds=1.5f,delaySeconds=.02f,durationSeconds=1,radius=25}};
        private static BattleSimulation World(UnitLoadout gear=null,SimulationConfig c=null)
        {c=c??FieldFixture.Config();if(c.strategy==null||!c.strategy.enabled)c.strategy=Rules();return FieldFixture.World(gear??SupportFixture.Gear(damage:1000),config:c);}
        [Test] public void OnlyTheTeamLosingAUnitGainsPointsAndOnlyOncePerDeath()
        {
            var w=World();FieldFixture.Cmd(w,fire:true);w.Step();Assert.That(w.GetUnit(50).IsAlive,Is.False);
            Assert.That(w.GetStrategyTeam(0).Points,Is.EqualTo(600));Assert.That(w.GetStrategyTeam(1).Points,Is.EqualTo(625));
            FieldFixture.Wait(w,30);Assert.That(w.GetStrategyTeam(1).Points,Is.EqualTo(625));
        }
        [Test] public void PointCapAndNewMatchResetApplyWithoutRespawnReset()
        {
            var c=FieldFixture.Config();c.strategy=Rules();c.strategy.initialPoints=990;var w=World(c:c);FieldFixture.Cmd(w,fire:true);w.Step();FieldFixture.Wait(w,10);
            Assert.That(w.GetStrategyTeam(1).Points,Is.EqualTo(1000));Assert.That(World(c:c).GetStrategyTeam(1).Points,Is.EqualTo(990));
        }
        [Test] public void InvalidCoordinatesAndUnknownRequestsNeverSpendPoints()
        {
            var w=World();foreach(var x in new[]{float.NaN,float.PositiveInfinity,10000f})Assert.That(w.RequestStrategy(0,StrategyKind.WideRadar,x,0,out _),Is.False);
            Assert.That(w.RequestStrategy(-1,StrategyKind.WideRadar,0,0,out _),Is.False);Assert.That(w.RequestStrategy(0,(StrategyKind)99,0,0,out _),Is.False);
            Assert.That(w.GetStrategyTeam(0).Points,Is.EqualTo(600));
        }
        [Test] public void SpendingIsAtomicAndInsufficientPointsCannotQueueAnotherSupport()
        {
            var w=World();Assert.That(w.RequestStrategy(0,StrategyKind.Nuclear,-30,0,out _),Is.True);
            Assert.That(w.GetStrategyTeam(0).Points,Is.Zero);Assert.That(w.RequestStrategy(0,StrategyKind.WideRadar,0,0,out _),Is.False);
        }
        [Test] public void TeamSupportIsSharedAcrossSlotsAndCannotBeDuplicated()
        {
            var w=World();Assert.That(w.RequestStrategy(0,StrategyKind.WideRadar,0,0,out _),Is.True);
            Assert.That(w.RequestStrategy(1,StrategyKind.WideRadar,0,0,out _),Is.False);Assert.That(w.GetStrategyTeam(0).Points,Is.EqualTo(500));
        }
        [Test] public void ExpiredSupportStillHonorsItsCooldownBeforeReuse()
        {
            var w=World();Assert.That(w.RequestStrategy(0,StrategyKind.Jammer,0,0,out _),Is.True);FieldFixture.Wait(w,25);
            Assert.That(w.GetStrategy(1,0).Active,Is.False);Assert.That(w.RequestStrategy(0,StrategyKind.Jammer,0,0,out _),Is.False);
            FieldFixture.Wait(w,26);Assert.That(w.RequestStrategy(0,StrategyKind.Jammer,0,0,out _),Is.True);
        }
        [Test] public void NuclearWarnsBothTeamsBeforeDamageAndHurtsBothTeamsEqually()
        {
            var w=World();CarryFixture.ArmAt(w,0,0);
            Assert.That(w.GetStrategy(0,1).Active,Is.True);Assert.That(w.GetUnit(0).Health,Is.EqualTo(500));
            FieldFixture.Wait(w,4);Assert.That(w.GetUnit(50).Health,Is.EqualTo(500));w.Step();
            Assert.That(w.GetUnit(0).IsAlive||w.GetUnit(50).IsAlive,Is.False);
            Assert.That(w.GetSlotStats(0).DamageReceived,Is.EqualTo(600));Assert.That(w.GetSlotStats(50).DamageReceived,Is.EqualTo(600));
            Assert.That(w.GetSlotStats(0).DamageDealt,Is.EqualTo(600));Assert.That(w.GetSlotStats(0).Kills,Is.EqualTo(1));
            Assert.That(w.GetStrategyTeam(0).Points,Is.EqualTo(25));Assert.That(w.GetStrategyTeam(1).Points,Is.EqualTo(625));
        }
        [Test] public void NuclearIsASingleBlastAndNeverDirectlyChangesCaptureOwnership()
        {
            var w=World();CarryFixture.ArmAt(w,0,0);int owner=w.GetCapturePoint(0).OwnerTeam;FieldFixture.Wait(w,30);
            Assert.That(w.GetSlotStats(0).Deaths,Is.EqualTo(1));Assert.That(w.GetSlotStats(50).Deaths,Is.EqualTo(1));Assert.That(w.GetCapturePoint(0).OwnerTeam,Is.EqualTo(owner));
        }
        [Test] public void NuclearHitsFriendlyAndEnemyBasesAndLeavesDistantBasesUntouched()
        {
            var c=FieldFixture.Config();c.strategy=Rules();c.strategy.nuclear.radius=35;var w=World(c:c);
            double blue=w.GetGauge(0),orange=w.GetGauge(1);CarryFixture.ArmAt(w,0,0);FieldFixture.Wait(w,6);
            Assert.That(w.GetGauge(0),Is.LessThan(blue-200));Assert.That(w.GetGauge(1),Is.LessThan(orange-200));
            Assert.That(w.GetBase(0).DamageReceived,Is.Zero);
        }
        [Test] public void NuclearAreaDamageStillCrossesCoverForFriendAndEnemy()
        {
            var c=FieldFixture.Config();c.strategy=Rules();var w=FieldFixture.World(SupportFixture.Gear(),new[]{FieldFixture.Box()},c);
            Assert.That(w.HasLineOfSight(0,50),Is.False);CarryFixture.ArmAt(w,-3,0);FieldFixture.Wait(w,6);
            Assert.That(w.GetUnit(0).IsAlive||w.GetUnit(50).IsAlive,Is.False);
        }
        [Test] public void WideRadarSharesBoundedPositionsThroughCoverWithoutHpOrFiringLock()
        {
            var c=FieldFixture.Config();c.strategy=Rules();var w=FieldFixture.World(SupportFixture.Gear(),new[]{FieldFixture.Box()},c);FieldFixture.MoveTo(w,51,45,0);
            w.RequestStrategy(0,StrategyKind.WideRadar,0,0,out _);FieldFixture.Wait(w,30);
            var contact=w.GetTeamContact(0,50);Assert.That(contact.Valid&&contact.RadarDetected,Is.True);Assert.That(contact.HasHealth||contact.LockDetected,Is.False);
            Assert.That(w.GetTeamContact(0,51).Valid,Is.False);Assert.That(w.GetUnit(50).IncomingLockCount,Is.Zero);
            Assert.That(w.GetStrategy(2,1).Active,Is.False);
        }
        [Test] public void JammerSuppressesEnemySharedContactsThenScanningRecoversAfterItExpires()
        {
            var c=FieldFixture.Config();c.strategy=Rules();c.strategy.wideRadar.durationSeconds=2;var w=World(c:c);w.RequestStrategy(0,StrategyKind.WideRadar,0,0,out _);FieldFixture.Wait(w,26);Assert.That(w.GetTeamContact(0,50).Valid,Is.True);
            w.RequestStrategy(50,StrategyKind.Jammer,0,0,out _);FieldFixture.Wait(w,2);
            Assert.That(w.GetTeamContact(0,50).Valid,Is.False);Assert.That(w.IsMapJammed(0,8,0),Is.True);Assert.That(w.IsMapJammed(1,8,0),Is.False);
            Assert.That(w.CanVisuallySee(0,50,20),Is.True,"Jamming must not blind direct visual perception.");
            FieldFixture.Wait(w,30);Assert.That(w.GetTeamContact(0,50).Valid,Is.True,"A scan after jammer expiry must recover the contact.");
        }
        [Test] public void ExpiredWideRadarContactsLoseTheirLastKnownPositionsNormally()
        {
            var w=World();w.RequestStrategy(0,StrategyKind.WideRadar,0,0,out _);FieldFixture.Wait(w,26);Assert.That(w.GetTeamContact(0,50).Valid,Is.True);
            FieldFixture.Wait(w,100);Assert.That(w.GetTeamContact(0,50).Valid,Is.False);
        }
        [Test] public void ConfigurationIsDefensivelyCopiedAndInvalidEconomyIsRejected()
        {
            var c=FieldFixture.Config();c.strategy=Rules();var w=World(c:c);c.strategy.nuclear.cost=1;c.strategy.initialPoints=1;
            Assert.That(w.GetStrategyDefinition(StrategyKind.Nuclear).cost,Is.EqualTo(600));var copy=w.GetStrategyDefinition(StrategyKind.Nuclear);copy.cost=2;Assert.That(w.GetStrategyDefinition(StrategyKind.Nuclear).cost,Is.EqualTo(600));
            c.strategy=Rules();c.strategy.pointsPerLoss=-1;Assert.Throws<ArgumentException>(()=>c.Validate());c.strategy=Rules();c.strategy.nuclear.unitDamage=0;Assert.Throws<ArgumentException>(()=>c.Validate());
        }
        [Test] public void DisabledAndFinishedMatchesRejectAllSpending()
        {
            var c=FieldFixture.Config();var w=FieldFixture.World(config:c);Assert.That(w.RequestStrategy(0,StrategyKind.WideRadar,0,0,out _),Is.False);
            c.strategy=Rules();c.maxDurationSeconds=20;w=World(c:c);while(w.Phase==MatchPhase.Running)w.Step();int points=w.GetStrategyTeam(0).Points;
            Assert.That(w.RequestStrategy(0,StrategyKind.WideRadar,0,0,out _),Is.False);Assert.That(w.GetStrategyTeam(0).Points,Is.EqualTo(points));
        }
    }
}
