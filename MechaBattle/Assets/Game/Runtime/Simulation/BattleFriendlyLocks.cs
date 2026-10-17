using MechaBattle.Contracts;

namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        // Current deployed build only. Reserves in other deck slots never grant a support lock.
        public float GetFriendlyLockRange(int slot)
        {
            var u = Unit(slot); var build = u.Loadouts[u.Deck]; float range = 0;
            for (int i = 0; i < build.WeaponCount; i++)
            {
                var w = build.GetWeapon(i);
                if (w.Kind == WeaponKind.RepairGun || w.Kind == WeaponKind.RepairPod)
                    range = System.Math.Max(range, w.YellowLockRange > 0 ? w.YellowLockRange : w.FireRange);
            }
            return range;
        }
        public bool HasRecoveryEquipment(int slot) => GetFriendlyLockRange(slot) > 0;
        public bool IsFriendlyYellowLocked(int from, int to)
        {
            var source = Unit(from); var target = Unit(to); float range = GetFriendlyLockRange(from);
            return from != to && source.Alive && target.Alive && source.Team == target.Team && range > 0
                && DistanceSquared(source.X, source.Z, target.X, target.Z) + (source.Y - target.Y) * (source.Y - target.Y) <= range * range
                && HasLineOfSight(from, to);
        }
        public float GetFriendlyRedLockRange(int slot)
        {
            var w = ActiveWeapon(Unit(slot)); return w.Kind == WeaponKind.RepairGun ? w.RedLockRange : 0;
        }
        public bool IsFriendlyRedLocked(int from, int to)
        {
            if (!IsFriendlyYellowLocked(from, to)) return false; var source = Unit(from); var target = Unit(to);
            float range = GetFriendlyRedLockRange(from);
            return range > 0 && DistanceSquared(source.X, source.Z, target.X, target.Z) + (source.Y - target.Y) * (source.Y - target.Y) <= range * range;
        }
        // Keep hostile lock APIs separate: support must not generate threat warnings or enemy contacts.
        public bool IsManualUnitYellowLocked(int from, int to) => IsYellowLocked(from, to) || IsFriendlyYellowLocked(from, to);
        public bool IsManualUnitRedLocked(int from, int to) => IsManualUnitYellowLocked(from, to) && (IsRedLocked(from, to) || IsFriendlyRedLocked(from, to));
    }
}
