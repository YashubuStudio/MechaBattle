using System;
using System.Linq;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;

namespace MechaBattle.Tests
{
    public sealed class PersonalResultTests
    {
        private static void AssertEmpty(SlotStatsSnapshot s)
        {
            Assert.That(s.DamageDealt,Is.Zero);Assert.That(s.DamageReceived,Is.Zero);Assert.That(s.CoreDamageDealt,Is.Zero);
            Assert.That(s.HealingDealt,Is.Zero);Assert.That(s.HealingReceived,Is.Zero);Assert.That(s.Kills,Is.Zero);Assert.That(s.Deaths,Is.Zero);
        }
        [Test] public void NewMatchStartsEmptyEvenAfterAnotherMatchHasAccumulatedDamage()
        {
            var old=SupportFixture.World(SupportFixture.Gear());FieldFixture.Cmd(old,fire:true);old.Step();
            Assert.That(old.GetSlotStats(0).DamageDealt,Is.GreaterThan(0));
            var fresh=SupportFixture.World(SupportFixture.Gear());for(int i=0;i<100;i++)AssertEmpty(fresh.GetSlotStats(i));
        }
        [Test] public void EffectiveDamageIncludesShieldAndCapsOverkillForBothSides()
        {
            var w=SupportFixture.World(SupportFixture.Gear(damage:1000));FieldFixture.Cmd(w,fire:true);w.Step();
            Assert.That(w.GetSlotStats(0).DamageDealt,Is.EqualTo(600));Assert.That(w.GetSlotStats(50).DamageReceived,Is.EqualTo(600));
            Assert.That(w.GetSlotStats(0).CoreDamageDealt,Is.Zero);Assert.That(w.GetSlotStats(0).DamageReceived,Is.Zero);
            Assert.That(w.GetSlotStats(50).HealingReceived,Is.Zero);Assert.That(w.GetSlotStats(1).DamageDealt,Is.Zero);
        }
        [Test] public void SimultaneousContributorsSumToOneVictimsActualLoss()
        {
            var a=SupportFixture.Gear(damage:180);var b=SupportFixture.Gear(damage:480);var w=SupportFixture.World(a.WithWeapons(a,b));FieldFixture.MoveTo(w,1,-6,0);
            FieldFixture.Cmd(w,fire:true);FieldFixture.Cmd(w,1,fire:true,change:1);w.Step();
            Assert.That(w.GetSlotStats(0).DamageDealt+w.GetSlotStats(1).DamageDealt,Is.EqualTo(600).Within(.001));
            Assert.That(w.GetSlotStats(50).DamageReceived,Is.EqualTo(600));
        }
        [Test] public void BaseCounterFireCountsAsReceivedDamageWithoutCreatingAPlayerAttacker()
        {
            var c=FieldFixture.Config();c.baseCounterFireDamage=50;c.baseCounterFireIntervalSeconds=.2f;c.baseCounterFireRadius=10;
            var w=SupportFixture.World(SupportFixture.Gear(),c);FieldFixture.MoveTo(w,0,22,0);
            var u=w.GetUnit(0);Assert.That(w.GetSlotStats(0).DamageReceived,Is.GreaterThan(0));
            Assert.That(w.GetSlotStats(0).DamageReceived,Is.EqualTo(600-u.Health-u.Shield).Within(.001));
            Assert.That(Enumerable.Range(0,100).Sum(i=>w.GetSlotStats(i).DamageDealt),Is.Zero);
        }
        [Test] public void RepairFaCountsOnlyActualHpAndStopsAtFullHealth()
        {
            var gun=SupportFixture.Gear();var repair=SupportFixture.Gear(WeaponKind.RepairGun,45);var w=SupportFixture.World(gun.WithWeapons(gun,repair));SupportFixture.WoundFriend(w);
            for(int i=0;i<65;i++){FieldFixture.Cmd(w,fire:true,change:i==0?1:0);w.Step();}
            Assert.That(w.GetSlotStats(0).HealingDealt,Is.EqualTo(120).Within(.001));
            Assert.That(w.GetSlotStats(1).HealingReceived,Is.EqualTo(120).Within(.001));
            Assert.That(w.GetSlotStats(0).HealingReceived,Is.Zero);Assert.That(w.GetSlotStats(1).HealingDealt,Is.Zero);
            Assert.That(w.GetSlotStats(1).DamageReceived,Is.EqualTo(220));Assert.That(w.GetUnit(1).Shield,Is.Zero);
        }
        [TestCase(false)] [TestCase(true)] public void EnemyAndFullHealthFriendlyTargetsCannotInflateHealing(bool friendly)
        {
            var w=SupportFixture.World(SupportFixture.Gear(WeaponKind.RepairGun,45));if(friendly)FieldFixture.MoveTo(w,1,0,0);
            FieldFixture.Cmd(w,fire:true);w.Step();Assert.That(w.GetSlotStats(0).HealingDealt,Is.Zero);
            Assert.That(w.GetSlotStats(friendly?1:50).HealingReceived,Is.Zero);
        }
        [Test] public void HomeSupplyCountsReceivedHpButNeverClaimsAPlayerHealer()
        {
            var c=FieldFixture.Config();c.homeSupplyRadius=100;c.homeHealthPerSecond=50;
            var w=SupportFixture.World(SupportFixture.Gear(),c);SupportFixture.WoundFriend(w);FieldFixture.Wait(w,200);
            Assert.That(w.GetUnit(1).Health,Is.EqualTo(500));Assert.That(w.GetSlotStats(1).HealingReceived,Is.EqualTo(120).Within(.001));
            Assert.That(Enumerable.Range(0,100).Sum(i=>w.GetSlotStats(i).HealingDealt),Is.Zero);
            Assert.That(w.GetSlotStats(1).DamageReceived,Is.EqualTo(220));
        }
        [Test] public void SelfPodHpCountsAsReceivedHealingButShieldAndAmmoDoNot()
        {
            var gun=SupportFixture.Gear();var pod=SupportFixture.Gear(WeaponKind.RepairPod,100,noSupply:true,shieldRate:100,ammoRate:10,duration:10);
            var w=SupportFixture.World(gun.WithWeapons(gun,pod));FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();
            FieldFixture.Cmd(w,fire:true,change:1);w.Step();FieldFixture.Wait(w,120);
            Assert.That(w.GetUnit(0).Health,Is.EqualTo(500));Assert.That(w.GetUnit(0).Shield,Is.EqualTo(100));
            Assert.That(w.GetSlotStats(0).HealingReceived,Is.EqualTo(120).Within(.001));Assert.That(w.GetSlotStats(0).HealingDealt,Is.Zero);
        }
        [Test] public void PodHealingStillCreditsItsOwnerAfterDeathAndRespawn()
        {
            var gun=SupportFixture.Gear();var pod=SupportFixture.Gear(WeaponKind.RepairPod,100,noSupply:true,duration:10);var kill=SupportFixture.Gear(damage:1000);
            var w=SupportFixture.World(gun.WithWeapons(gun,pod,kill));SupportFixture.WoundFriend(w);FieldFixture.MoveTo(w,50,-8,8);
            int owner=w.GetUnit(0).UnitId;FieldFixture.Cmd(w,fire:true,change:1);w.Step();
            FieldFixture.Cmd(w,50,fire:true,change:2,yaw:180);w.Step();Assert.That(w.GetUnit(0).IsAlive,Is.False);
            FieldFixture.Wait(w,150);Assert.That(w.GetUnit(0).UnitId,Is.Not.EqualTo(owner));
            Assert.That(w.GetUnit(1).Health,Is.EqualTo(500));Assert.That(w.GetSlotStats(0).HealingDealt,Is.EqualTo(120).Within(.001));
            Assert.That(w.GetSlotStats(1).HealingReceived,Is.EqualTo(120).Within(.001));Assert.That(w.GetSlotStats(0).HealingReceived,Is.Zero);
        }
        [Test] public void RespawnKeepsDamageAndHealingTotalsWithoutCountingTheNewHealthPool()
        {
            var gun=SupportFixture.Gear();var repair=SupportFixture.Gear(WeaponKind.RepairGun,45);var w=SupportFixture.World(gun.WithWeapons(gun,repair));SupportFixture.WoundFriend(w);
            FieldFixture.Cmd(w,fire:true,change:1);w.Step();int original=w.GetUnit(1).UnitId;
            for(int i=0;i<3 && w.GetUnit(1).IsAlive;i++){FieldFixture.Wait(w,10);FieldFixture.Cmd(w,50,fire:true,yaw:-90);w.Step();}
            Assert.That(w.GetUnit(1).IsAlive,Is.False);var before=w.GetSlotStats(1);FieldFixture.Wait(w,10);
            Assert.That(w.GetUnit(1).UnitId,Is.Not.EqualTo(original));Assert.That(w.GetSlotStats(1).HealingReceived,Is.EqualTo(45));
            Assert.That(w.GetSlotStats(1).DamageReceived,Is.EqualTo(before.DamageReceived));Assert.That(w.GetSlotStats(0).HealingDealt,Is.EqualTo(45));
        }
        [Test] public void InFlightDamageAndKillsBelongToTheOriginalSlotAfterRespawn()
        {
            var slow=SupportFixture.Gear(damage:1000,speed:6);var instant=SupportFixture.Gear(damage:1000);var w=SupportFixture.World(slow.WithWeapons(slow,instant));int owner=w.GetUnit(0).UnitId;
            FieldFixture.Cmd(w,fire:true);w.Step();FieldFixture.Cmd(w,50,fire:true,change:1,yaw:-90);w.Step();FieldFixture.Wait(w,150);
            Assert.That(w.GetUnit(0).UnitId,Is.Not.EqualTo(owner));Assert.That(w.GetSlotStats(0).DamageDealt,Is.EqualTo(600).Within(.001));
            Assert.That(w.GetSlotStats(0).Kills,Is.EqualTo(1));Assert.That(w.GetSlotStats(0).DamageReceived,Is.EqualTo(600));
        }
        private static BattleSimulation CoreWorld(UnitLoadout frame)
        {
            var w=SupportFixture.World(frame);SimulationTests.MoveTeam(w,1,0,1,100);FieldFixture.MoveTo(w,0,22,0);return w;
        }
        [Test] public void EnemyCoreDamageIsSeparateFromUnitDamage()
        {
            var w=CoreWorld(SupportFixture.Gear(damage:123));FieldFixture.Cmd(w,fire:true);w.Step();
            Assert.That(w.GetSlotStats(0).CoreDamageDealt,Is.EqualTo(123));Assert.That(w.GetSlotStats(0).DamageDealt,Is.Zero);
            Assert.That(w.GetBase(4).DamageReceived,Is.EqualTo(123));Assert.That(w.GetSlotStats(50).CoreDamageDealt,Is.Zero);
        }
        [Test] public void InFlightCoreDamageStillCreditsTheSlotAfterItsOwnerRespawns()
        {
            var slow=SupportFixture.Gear(damage:40,speed:2);var kill=SupportFixture.Gear(damage:1000);var w=CoreWorld(slow.WithWeapons(slow,kill));FieldFixture.MoveTo(w,50,22,8);
            int owner=w.GetUnit(0).UnitId;FieldFixture.Cmd(w,fire:true);w.Step();FieldFixture.Cmd(w,50,fire:true,change:1,yaw:180);w.Step();
            FieldFixture.Wait(w,250);Assert.That(w.GetUnit(0).UnitId,Is.Not.EqualTo(owner));
            Assert.That(w.GetSlotStats(0).CoreDamageDealt,Is.EqualTo(40));Assert.That(w.GetBase(4).DamageReceived,Is.EqualTo(40));
        }
        [Test] public void MatchTotalsDoNotDependOnThePresentationEventRingRetention()
        {
            var gun=new UnitLoadout("long-result-test",10000,0,10,10000,1,80,.02f,120,10,WeaponFireMode.FullAutomatic,10000,.6f);
            var w=SupportFixture.World(gun);
            for(int i=0;i<2500;i++){FieldFixture.Cmd(w,fire:true);w.Step();}
            Assert.That(w.CombatEventSequence,Is.GreaterThan(w.CombatEventCapacity));Assert.That(w.TryGetCombatEvent(1,out _),Is.False);
            Assert.That(w.GetSlotStats(0).DamageDealt,Is.EqualTo(2500));Assert.That(w.GetSlotStats(50).DamageReceived,Is.EqualTo(2500));
        }
        [Test] public void FinishedMatchFreezesItsFinalTotals()
        {
            var c=FieldFixture.Config();c.maxDurationSeconds=20;
            var w=SupportFixture.World(SupportFixture.Gear(),c);FieldFixture.Cmd(w,fire:true);w.Step();
            while(w.Phase==MatchPhase.Running)w.Step();var before=w.GetSlotStats(0);var victim=w.GetSlotStats(50);long tick=w.CurrentTick;
            for(int i=0;i<10;i++)w.Step();Assert.That(w.CurrentTick,Is.EqualTo(tick));
            Assert.That(w.GetSlotStats(0).DamageDealt,Is.EqualTo(before.DamageDealt));Assert.That(w.GetSlotStats(50).DamageReceived,Is.EqualTo(victim.DamageReceived));
        }
    }
}
