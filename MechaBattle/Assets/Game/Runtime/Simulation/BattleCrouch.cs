using System;
namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        private void PrepareCrouch(UnitState u)
        {
            var c=u.Command;
            u.Crouching=config.crouchEnabled&&u.Alive&&u.Grounded&&CurrentTick>=u.DisabledUntil&&CurrentTick>=u.MeleeUntil&&!u.MeleeCharging
                &&(u.ReturnUntil>=0||u.HasCommand&&c.Crouch)
                &&(!u.HasCommand||c.MoveX*c.MoveX+c.MoveY*c.MoveY<=.0001f&&!c.Jump&&!c.Boost&&!c.Melee&&!(ActiveWeapon(u).Kind==MechaBattle.Contracts.WeaponKind.Melee&&c.FirePrimary));
        }
        private float BoostRecoveryRate(UnitState u)
        {
            float recovery=u.BoostOverheated?config.boostOverheatRecoveryPerSecond:config.boostRecoveryPerSecond;
            return recovery*(u.Crouching?config.crouchBoostRecoveryMultiplier:1);
        }
    }
}
