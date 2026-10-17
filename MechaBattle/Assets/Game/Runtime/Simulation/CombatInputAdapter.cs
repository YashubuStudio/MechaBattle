using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    // Translates a bot's explicit fire intent into the same trigger gestures a player uses.
    // Receives only that bot's own state and its already filtered command, never enemy truth.
    public sealed class CombatInputAdapter
    {
        private readonly int[] unitIds = new int[MatchLimits.TotalControllerSlots];
        private readonly bool[] held = new bool[MatchLimits.TotalControllerSlots];
        public UnitCommand Adapt(UnitSnapshot self, UnitLoadout weapon, UnitCommand desired)
        {
            int slot = self.SlotId;
            if (unitIds[slot] != self.UnitId) { unitIds[slot] = self.UnitId; held[slot] = false; }
            bool fire = desired.FirePrimary;
            if (weapon.FireMode == WeaponFireMode.SemiAutomatic || weapon.FireMode == WeaponFireMode.SingleShot) fire &= !held[slot];
            else if (weapon.FireMode == WeaponFireMode.Charge && self.ChargeFraction >= 1) fire = false;
            held[slot] = fire;
            return new UnitCommand(desired.UnitId, desired.Tick, desired.Sequence, desired.MoveX, desired.MoveY, desired.AimYaw, desired.AimPitch,
                desired.Boost, fire, desired.FireSecondary, desired.Melee, desired.UseAbility, desired.Reload, desired.MeleeStyle, desired.HasAimPoint, desired.AimTargetX, desired.AimTargetY, desired.AimTargetZ, desired.LockedTargetSlot, desired.LockedTargetUnitId, desired.Jump, desired.WeaponSwitchDelta, desired.CancelHeldAttack, desired.LockedDeployableIndex, desired.LockedDeployableId, desired.EmergencyReturn,desired.Crouch);
        }
    }
}
