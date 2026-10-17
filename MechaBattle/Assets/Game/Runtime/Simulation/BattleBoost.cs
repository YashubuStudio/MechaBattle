using System;
using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        // Human-only boost pool scaling (user request 2026-10-16): BOTs always use
        // config.boostCapacity unscaled. humanBoostCapacityMultiplier==0 means "not
        // configured", applied as 1 (no change), so every existing config is unaffected.
        public float GetBoostCapacity(int slot)=>EffectiveBoostCapacity(Unit(slot));
        private float EffectiveBoostCapacity(UnitState u)
        {
            if (roster.Controllers[u.Slot].Controller != ControllerType.Human) return config.boostCapacity;
            return config.boostCapacity * (config.humanBoostCapacityMultiplier > 0 ? config.humanBoostCapacityMultiplier : 1f);
        }
        private void UpdateOverheat(UnitState u)
        {if(config.boostOverheatEnabled && u.Boost<=0){u.Boost=0;u.BoostOverheated=true;}}
        private void ConsumeBoost(UnitState u,float amount)
        {u.Boost=Math.Max(0,u.Boost-amount);if(u.Boost<=0.00001f){u.Boost=0;UpdateOverheat(u);}}
        private bool HasBoostMotion(UnitState u)
        {
            if(u.BoostOverheated || u.Boost<=0 || !u.HasCommand || !u.Command.Boost)return false;
            return u.Command.MoveX*u.Command.MoveX+u.Command.MoveY*u.Command.MoveY>0.0001f;
        }
        private void ConfirmMeleeHit(int source,int sourceId)
        {
            if(source<0 || source>=units.Length)return;var u=units[source];
            if(!u.Alive || u.Id!=sourceId || u.MeleeHitTick!=CurrentTick || u.MeleeHitAdjusted)return;
            u.MeleeHitAdjusted=true;
            u.Boost=Math.Min(EffectiveBoostCapacity(u),u.Boost+u.MeleeBoostSpent*config.meleeHitBoostRefundFraction);
            // A refund never clears an overheat already triggered at zero.
            if(config.meleeHitRecoveryMultiplier>0 && config.meleeHitRecoveryMultiplier<1 && u.MeleeAttack!=null && u.MeleeUntil>CurrentTick)
            {
                u.MeleeRecoverySeconds*=config.meleeHitRecoveryMultiplier;
                u.MeleeUntil=Math.Min(u.MeleeUntil,CurrentTick+SecondsToTicks(u.MeleeRecoverySeconds));
                u.NextMelee=Math.Min(u.NextMelee,u.MeleeUntil+SecondsToTicks(u.MeleeAttack.cooldownSeconds));
            }
        }
        private void AdvanceMeleeLunge(UnitState u)
        {
            if(!config.seamlessMeleeEnabled || u.MeleeAttack==null || u.MeleeLungeRemaining<=0 || CurrentTick>=u.MeleeUntil)return;
            float seconds=Math.Max(FixedDeltaTime,(u.MeleeHitTick-CurrentTick+1)*FixedDeltaTime);
            float distance=Math.Min(u.MeleeLungeRemaining,u.MeleeLungeRemaining/seconds*FixedDeltaTime);
            double yaw=u.MeleeYaw*Math.PI/180;
            MoveHorizontal(u,(float)Math.Sin(yaw)*distance,(float)Math.Cos(yaw)*distance);u.MeleeLungeRemaining-=distance;
        }
    }
}
