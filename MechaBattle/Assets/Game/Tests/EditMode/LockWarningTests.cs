using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class LockWarningTests
    {
        private static SimulationConfig Config()
        {
            var c=FieldFixture.Config();c.redLockAssistEnabled=false;c.botLineCombatEnabled=c.botLockRetreatEnabled=true;
            c.botPreferredRangeFraction=0.9f;c.botStrafeFraction=0.3f;c.botRetreatHealthFraction=0.35f;c.botAssaultModulo=6;
            c.botRecoveryHealthFraction=c.botRecoveryShieldFraction=1;c.botRecoveryQuietSeconds=2.5f;c.botCoverSearchRadius=55;return c;
        }
        private static UnitLoadout Gun(float damage=20)=>FieldFixture.Gun(speed:0,damage:damage,yellow:60);
        private static UnitCommand Forward(BattleSimulation w,int slot)
        {var u=w.GetUnit(slot);return new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,1,0,90,0,false,true,false,false,false);}
        private static void HitBot(BattleSimulation w,bool locked=true)
        {
            FieldFixture.MoveTo(w,0,-8,20);FieldFixture.MoveTo(w,1,-8,0);
            FieldFixture.Cmd(w,50,fire:true,yaw:270,locked:locked?1:-1);w.Step();
            Assert.That(w.GetUnit(1).Health,Is.EqualTo(480).Within(0.01));
        }
        [Test] public void WarningCountsOnlyCurrentValidExplicitEnemyLocksAndClearsOnRelease()
        {
            var w=FieldFixture.World(Gun(),config:Config());FieldFixture.Cmd(w,locked:50);FieldFixture.Cmd(w,1,locked:50);w.Step();
            Assert.That(w.GetUnit(50).IncomingLockCount,Is.EqualTo(2));Assert.That(w.GetUnit(0).IncomingLockCount,Is.Zero);
            Assert.That(w.GetUnit(50).LastLockedHitTick,Is.EqualTo(-1));w.Step();Assert.That(w.GetUnit(50).IncomingLockCount,Is.Zero);
            FieldFixture.Cmd(w,locked:1);w.Step();Assert.That(w.GetUnit(1).IncomingLockCount,Is.Zero);
            var u=w.GetUnit(0);Assert.That(w.Submit(0,new UnitCommand(u.UnitId,w.CurrentTick,(uint)w.CurrentTick+1,0,0,0,0,false,false,false,false,false,lockedTargetSlot:50,lockedTargetUnitId:999999)),Is.True);
            w.Step();Assert.That(w.GetUnit(50).IncomingLockCount,Is.Zero);
        }
        [Test] public void OcclusionAndYellowRangeRejectLockWarningsWithoutExtendingAwareness()
        {
            var blocked=FieldFixture.World(Gun(),new[]{FieldFixture.Box()},Config());FieldFixture.Cmd(blocked,locked:50);blocked.Step();Assert.That(blocked.GetUnit(50).IncomingLockCount,Is.Zero);
            var far=FieldFixture.World(FieldFixture.Gun(speed:0,red:5,yellow:10),config:Config());FieldFixture.Cmd(far,locked:50);far.Step();Assert.That(far.GetUnit(50).IncomingLockCount,Is.Zero);
        }
        [Test] public void ActualShieldDamageCountsAsLockedHitButAnUnlockedHitDoesNot()
        {
            var frame=new UnitLoadout("shield",500,100,10,20,20,80,0.1f,120,10,redLockRange:24,yellowLockRange:60);
            var w=FieldFixture.World(frame,config:Config());long tick=w.CurrentTick;FieldFixture.Cmd(w,fire:true,locked:50);w.Step();var u=w.GetUnit(50);
            Assert.That(u.Health,Is.EqualTo(500));Assert.That(u.Shield,Is.EqualTo(80));Assert.That(u.LastDamageTick,Is.EqualTo(tick));Assert.That(u.LastLockedHitTick,Is.EqualTo(tick));
            var unlocked=FieldFixture.World(Gun(),config:Config());FieldFixture.Cmd(unlocked,fire:true);unlocked.Step();
            Assert.That(unlocked.GetUnit(50).LastDamageTick,Is.GreaterThanOrEqualTo(0));Assert.That(unlocked.GetUnit(50).LastLockedHitTick,Is.EqualTo(-1));
        }
        [Test] public void AProjectileArrivingAfterLockReleaseDoesNotInventAnActiveLock()
        {
            var w=FieldFixture.World(FieldFixture.Gun(speed:12,yellow:60),config:Config());FieldFixture.Cmd(w,fire:true,locked:50);w.Step();
            Assert.That(w.GetUnit(50).IncomingLockCount,Is.EqualTo(1));FieldFixture.Wait(w,100);
            Assert.That(w.GetUnit(50).Health,Is.EqualTo(480));Assert.That(w.GetUnit(50).IncomingLockCount,Is.Zero);Assert.That(w.GetUnit(50).LastLockedHitTick,Is.EqualTo(-1));
        }
        [Test] public void DeathClearsWarningsAndRespawnClearsHitHistory()
        {
            var w=FieldFixture.World(Gun(1000),config:Config());FieldFixture.Cmd(w,locked:50);FieldFixture.Cmd(w,50,fire:true,yaw:270);w.Step();
            Assert.That(w.GetUnit(0).IsAlive,Is.False);Assert.That(w.GetUnit(50).IncomingLockCount,Is.Zero);
            var target=FieldFixture.World(Gun(1000),config:Config());FieldFixture.Cmd(target,fire:true,locked:50);target.Step();
            Assert.That(target.GetUnit(50).IsAlive,Is.False);Assert.That(target.GetUnit(50).IncomingLockCount,Is.Zero);Assert.That(target.GetUnit(50).LastLockedHitTick,Is.GreaterThanOrEqualTo(0));
            FieldFixture.Wait(target,4);Assert.That(target.GetUnit(50).IsAlive,Is.True);Assert.That(target.GetUnit(50).LastDamageTick,Is.EqualTo(-1));Assert.That(target.GetUnit(50).LastLockedHitTick,Is.EqualTo(-1));
        }
        [Test] public void AMinorLockedHitLatchesRetreatAfterUnlockAndReturnsOnlyAfterSharedHealing()
        {
            var c=Config();c.homeSupplyRadius=3;var frame=Gun();var w=FieldFixture.World(frame,config:c);HitBot(w);var bot=new BotTraversalAdapter(SimulationTests.Map(),c);
            var command=bot.Adapt(w.GetUnit(1),frame,Forward(w,1),50,w.GetUnit(50).UnitId,8,0,0);
            Assert.That(command.FirePrimary,Is.False);Assert.That(command.Melee,Is.False);Assert.That(command.Boost,Is.True);Assert.That(command.MoveX,Is.LessThan(0));Assert.That(command.LockedTargetSlot,Is.EqualTo(-1));
            Assert.That(w.Submit(1,command),Is.True);w.Step();Assert.That(w.GetUnit(1).IncomingLockCount,Is.Zero);
            command=bot.Adapt(w.GetUnit(1),frame,Forward(w,1));Assert.That(command.FirePrimary,Is.False);Assert.That(command.MoveX,Is.LessThan(0));
            bool returned=false;
            for(int i=0;i<250;i++)
            {command=bot.Adapt(w.GetUnit(1),frame,Forward(w,1));if(command.FirePrimary){returned=true;break;}Assert.That(w.Submit(1,command),Is.True);w.Step();}
            Assert.That(returned,Is.True);Assert.That(w.GetUnit(1).Health,Is.EqualTo(frame.MaxHealth));
            Assert.That((w.CurrentTick-w.GetUnit(1).LastDamageTick)*w.FixedDeltaTime,Is.GreaterThanOrEqualTo(c.botRecoveryQuietSeconds));
        }
        [Test] public void UnlockedMinorDamageKeepsTheRifleInCombat()
        {
            var c=Config();var frame=Gun();var w=FieldFixture.World(frame,config:c);HitBot(w,false);
            var command=new BotTraversalAdapter(SimulationTests.Map(),c).Adapt(w.GetUnit(1),frame,Forward(w,1),50,w.GetUnit(50).UnitId,8,0,0);
            Assert.That(command.FirePrimary,Is.True);Assert.That(command.Boost,Is.False);
        }
        [Test] public void BotUsesKnownSideCoverAndAFinitePodBeforeReturningToCombat()
        {
            var c=Config();var gun=Gun();var frame=gun.WithWeapons(gun,FieldFixture.Gun(kind:WeaponKind.RepairPod));
            var wall=FieldFixture.Box(x:-2,z:4,top:6,width:4,depth:4);var map=SimulationTests.Map();map.obstacles=new[]{wall};
            var w=FieldFixture.World(frame,map.obstacles,c);HitBot(w);var bot=new BotTraversalAdapter(map,c);bool spentPod=false,returned=false;
            for(int i=0;i<450;i++)
            {
                var self=w.GetUnit(1);bool visible=w.HasLineOfSight(1,50);
                var command=bot.Adapt(self,frame,Forward(w,1),visible?50:-1,w.GetUnit(50).UnitId,8,0,0);
                if(i==0){Assert.That(command.MoveY,Is.GreaterThan(0));Assert.That(command.FirePrimary,Is.False);}
                spentPod |= w.GetWeaponAmmo(1,1)<frame.GetWeapon(1).MaxAmmo;
                if(spentPod && self.ActiveWeaponIndex==0 && command.FirePrimary && self.Health>=frame.MaxHealth){returned=true;break;}
                Assert.That(w.Submit(1,command),Is.True);w.Step();
            }
            Assert.That(spentPod,Is.True,"Retreat must use the equipped finite repair pod.");Assert.That(returned,Is.True);
            Assert.That(w.GetUnit(1).Health,Is.EqualTo(frame.MaxHealth));Assert.That(w.GetWeaponAmmo(1,1),Is.LessThan(frame.GetWeapon(1).MaxAmmo));
        }
        [Test] public void LockWarningTriggersBoostedWeavingBeforeDamageWithoutKnowingAttackerPosition()
        {
            var c=Config();c.botLockEvadeEnabled=true;c.botLockEvadeStrafeFraction=0.6f;c.botLockEvadePeriodSeconds=0.28f;
            var frame=Gun();var w=FieldFixture.World(frame,config:c);FieldFixture.MoveTo(w,1,-8,0);FieldFixture.Cmd(w,50,locked:1);w.Step();var self=w.GetUnit(1);
            Assert.That(self.Health,Is.EqualTo(500));Assert.That(self.IncomingLockCount,Is.EqualTo(1));Assert.That(self.LastLockedHitTick,Is.EqualTo(-1));
            var bot=new BotTraversalAdapter(SimulationTests.Map(),c);var command=bot.Adapt(self,frame,Forward(w,1));
            Assert.That(command.Boost,Is.True);Assert.That(command.MoveX,Is.GreaterThan(0));Assert.That(Math.Abs(command.MoveY),Is.GreaterThan(0.5f));Assert.That(command.FirePrimary,Is.True);
            var future=new UnitCommand(self.UnitId,w.CurrentTick+15,(uint)w.CurrentTick+16,1,0,90,0,false,true,false,false,false);
            var alternate=bot.Adapt(self,frame,future);Assert.That(command.MoveY*alternate.MoveY,Is.LessThan(0));
        }
        [Test] public void AThirdPartyHitCannotBorrowTheLockOfAnEnemyWhoDidNotFire()
        {
            var w=FieldFixture.World(Gun(),config:Config());FieldFixture.Cmd(w,50,locked:0,yaw:270);FieldFixture.Cmd(w,51,fire:true,yaw:270);w.Step();
            var self=w.GetUnit(0);Assert.That(self.IncomingLockCount,Is.EqualTo(1));Assert.That(self.Health,Is.EqualTo(480));
            Assert.That(self.LastDamageTick,Is.GreaterThanOrEqualTo(0));Assert.That(self.LastLockedHitTick,Is.EqualTo(-1));
        }
        [Test] public void BaseCounterUnderAHarmlessLockIsNotMisreportedAsThatEnemyHitting()
        {
            var c=Config();c.botCentralApproachEnabled=true;c.baseCounterFireDamage=400;c.baseCounterFireIntervalSeconds=5;c.baseCounterFireRadius=10;
            var gun=Gun();var frame=gun.WithWeapons(gun,FieldFixture.Gun(kind:WeaponKind.Melee,range:3.4f,red:3.4f));var w=FieldFixture.World(frame,config:c);FieldFixture.MoveTo(w,6,14,-30);
            for(int i=0;i<40&&w.GetUnit(6).Health==500;i++){FieldFixture.Cmd(w,6,x:1);FieldFixture.Cmd(w,53,locked:6,yaw:270);w.Step();}
            var self=w.GetUnit(6);Assert.That(self.Health,Is.EqualTo(100));Assert.That(self.IncomingLockCount,Is.EqualTo(1));Assert.That(self.LastLockedHitTick,Is.EqualTo(-1));
            var map=SimulationTests.Map();foreach(var core in map.mainBases)core.buildingRadius=10;
            var command=new BotTraversalAdapter(map,c).Adapt(self,frame,Forward(w,6),53,w.GetUnit(53).UnitId,30,0,-30);
            Assert.That(command.CancelHeldAttack,Is.False);Assert.That(command.MoveX,Is.GreaterThan(0));Assert.That(command.WeaponSwitchDelta,Is.EqualTo(1));
        }
        [Test] public void ARespawnedOwnerCannotApplyItsNewLockToAnOldProjectile()
        {
            var c=Config();var slow=FieldFixture.Gun(speed:2,yellow:60);var cannon=Gun(1000);var decks=new UnitLoadout[100][];
            for(int slot=0;slot<100;slot++){var frame=slot==0?slow:cannon;decks[slot]=new[]{frame,frame};}
            var w=new BattleSimulation(SimulationTests.Map(),c,0,decks);FieldFixture.MoveTo(w,0,-8,0);FieldFixture.MoveTo(w,50,8,0);
            FieldFixture.Cmd(w,fire:true);w.Step();int old=w.GetUnit(0).UnitId;FieldFixture.Cmd(w,50,fire:true,yaw:270);w.Step();Assert.That(w.GetUnit(0).IsAlive,Is.False);
            FieldFixture.Wait(w,4);Assert.That(w.GetUnit(0).UnitId,Is.Not.EqualTo(old));
            for(int i=0;i<500&&w.GetUnit(50).Health==500;i++){FieldFixture.Cmd(w,locked:50);w.Step();}
            Assert.That(w.GetUnit(50).Health,Is.EqualTo(480));Assert.That(w.GetUnit(50).IncomingLockCount,Is.EqualTo(1));Assert.That(w.GetUnit(50).LastLockedHitTick,Is.EqualTo(-1));
        }
        [Test] public void AHarmlessHeldLockCannotPinAFullyRecoveredBotAtHomeForever()
        {
            var c=Config();c.homeSupplyRadius=3;var frame=Gun();var w=FieldFixture.World(frame,config:c);HitBot(w);var bot=new BotTraversalAdapter(SimulationTests.Map(),c);bool returned=false;
            for(int i=0;i<250;i++)
            {
                var command=bot.Adapt(w.GetUnit(1),frame,Forward(w,1));
                if(command.FirePrimary){returned=true;break;}
                Assert.That(w.Submit(1,command),Is.True);FieldFixture.Cmd(w,50,locked:1,yaw:270);w.Step();
            }
            Assert.That(returned,Is.True);Assert.That(w.GetUnit(1).IncomingLockCount,Is.EqualTo(1));Assert.That(w.GetUnit(1).Health,Is.EqualTo(frame.MaxHealth));
            Assert.That((w.CurrentTick-w.GetUnit(1).LastDamageTick)*w.FixedDeltaTime,Is.GreaterThanOrEqualTo(c.botRecoveryQuietSeconds));
        }
        [Test] public void UnlockedCoreCounterDamageDoesNotMakeEveryAssaultAbandonTheMainObjective()
        {
            var c=Config();c.botCentralApproachEnabled=true;var frame=Gun(400);var w=FieldFixture.World(frame,config:c);
            FieldFixture.MoveTo(w,6,14,-30);FieldFixture.Cmd(w,53,fire:true,yaw:270);w.Step();Assert.That(w.GetUnit(6).Health,Is.EqualTo(100));
            var map=SimulationTests.Map();foreach(var core in map.mainBases)core.buildingRadius=20;
            var command=new BotTraversalAdapter(map,c).Adapt(w.GetUnit(6),frame,Forward(w,6));
            Assert.That(command.MoveX,Is.GreaterThan(0));Assert.That(command.CancelHeldAttack,Is.False);
        }
        [Test] public void LockedHitOverridesCoreAssaultAndForcesRetreatEvenInsideTheBuilding()
        {
            var c=Config();c.botCentralApproachEnabled=true;var frame=Gun();var w=FieldFixture.World(frame,config:c);
            FieldFixture.MoveTo(w,6,14,-30);FieldFixture.Cmd(w,53,fire:true,yaw:270,locked:6);w.Step();Assert.That(w.GetUnit(6).Health,Is.EqualTo(480));
            var map=SimulationTests.Map();foreach(var core in map.mainBases)core.buildingRadius=20;
            var command=new BotTraversalAdapter(map,c).Adapt(w.GetUnit(6),frame,Forward(w,6));
            Assert.That(command.MoveX,Is.LessThan(0));Assert.That(command.FirePrimary,Is.False);Assert.That(command.CancelHeldAttack,Is.True);
        }
        [Test] public void RetreatCancelsAnArmedChargeThroughTheSharedCommandInsteadOfFiringIt()
        {
            var c=Config();var rifle=Gun();var charge=new UnitLoadout("charge",500,0,10,20,20,80,0.1f,120,10,WeaponFireMode.Charge,2,0.6f,0.1f,0.5f,redLockRange:24,yellowLockRange:60);
            var decks=new UnitLoadout[100][];for(int slot=0;slot<100;slot++)decks[slot]=new[]{slot==1?charge:rifle,rifle};
            var w=new BattleSimulation(SimulationTests.Map(),c,0,decks);FieldFixture.MoveTo(w,0,-8,20);FieldFixture.MoveTo(w,1,-8,0);FieldFixture.MoveTo(w,50,8,0);
            for(int i=0;i<20;i++){FieldFixture.Cmd(w,1,fire:true);w.Step();}
            FieldFixture.Cmd(w,1,fire:true);FieldFixture.Cmd(w,50,fire:true,yaw:270,locked:1);w.Step();
            var self=w.GetUnit(1);Assert.That(self.ChargeFraction,Is.GreaterThan(0.5f));long shots=w.Counters.ShotsFired;
            var command=new BotTraversalAdapter(SimulationTests.Map(),c).Adapt(self,charge,Forward(w,1),50,w.GetUnit(50).UnitId,8,0,0);
            command=new CombatInputAdapter().Adapt(self,charge,command);Assert.That(command.CancelHeldAttack,Is.True);
            Assert.That(w.Submit(1,command),Is.True);w.Step();Assert.That(w.Counters.ShotsFired,Is.EqualTo(shots));Assert.That(w.GetUnit(1).ChargeFraction,Is.Zero);Assert.That(w.GetUnit(1).Ammo,Is.EqualTo(20));
        }
        [Test] public void InvalidRecoveryThresholdsAreRejectedBeforeBattle()
        {var c=Config();c.botRecoveryHealthFraction=0.2f;Assert.Throws<ArgumentException>(()=>c.Validate());c=Config();c.botRecoveryQuietSeconds=float.NaN;Assert.Throws<ArgumentException>(()=>c.Validate());}
    }
}
