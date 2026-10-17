using System;
using MechaBattle.Contracts;
using MechaBattle.Contracts.Construction;
namespace MechaBattle.Construction
{
    public static class WeaponPerformance
    {
        public static bool Valid(WeaponDefinition w)
        {
            if (w == null) return false;
            try { new UnitLoadout("weapon-check", 1, 0, 1, w.ammo, w.damage, w.range, w.intervalSeconds, 1, 1,
                w.mode, w.magazineCapacity, w.reloadSeconds, w.minimumChargeSeconds, w.fullChargeSeconds, w.maximumBeamSeconds, w.beamRecoverySeconds, w.firingMoveMultiplier, 600, w.downPoints, w.blastRadius, w.splashDamage, w.splashDownPoints, w.projectileSpeed, w.redLockRange, w.kind, w.utilityRadius, w.utilityDuration, w.projectileGravity, w.fuseSeconds, w.yellowLockRange, w.hipSpreadDegrees, w.ammoResupplyDisabled, w.podShieldPerSecond, w.podAmmoPerSecond, w.utilityHealth); return true; }
            catch (ArgumentException) { return false; }
        }
        public static bool HasWeaponData(WeaponDefinition w)
        { return w != null && (w.damage != 0 || w.range != 0 || w.intervalSeconds != 0 || w.ammo != 0 || w.magazineCapacity != 0
            || w.mode != WeaponFireMode.FullAutomatic || w.reloadSeconds != 0 || w.minimumChargeSeconds != 0 || w.fullChargeSeconds != 0
            || w.maximumBeamSeconds != 0 || w.beamRecoverySeconds != 0 || w.downPoints != 0 || w.blastRadius != 0 || w.splashDamage != 0 || w.splashDownPoints != 0 || w.projectileSpeed != 0 || w.redLockRange != 0 || w.kind != WeaponKind.Ranged || w.utilityRadius != 0 || w.utilityDuration != 0 || w.projectileGravity != 0 || w.fuseSeconds != 0 || w.yellowLockRange != 0 || w.hipSpreadDegrees != 0 || w.ammoResupplyDisabled || w.podShieldPerSecond != 0 || w.podAmmoPerSecond != 0 || w.utilityHealth != 0); }
        // Long-run cadence of a magazine including the downtime actually imposed after
        // its final shot. Reload and shot recovery overlap; they must not be counted twice.
        public static double SustainedDps(WeaponDefinition w)
        {
            if (!Valid(w)) throw new ArgumentException("Invalid weapon performance.");
            if (w.kind == WeaponKind.RepairPod || w.kind == WeaponKind.RepairGun || w.kind == WeaponKind.Radar) return 0;
            double n = w.magazineCapacity, interval = w.intervalSeconds;
            if (w.mode == WeaponFireMode.Charge)
                return w.damage / (n > 0 ? w.fullChargeSeconds + interval + Math.Max(0, w.reloadSeconds - interval) / n : w.fullChargeSeconds + interval);
            if (w.mode == WeaponFireMode.ContinuousBeam)
            {
                int pulses = Math.Max(1, (int)Math.Min(w.ammo, Math.Ceiling(w.maximumBeamSeconds / interval - 0.0001)));
                if (n <= 0) return w.damage * pulses / (w.maximumBeamSeconds + w.beamRecoverySeconds);
                int completeBursts = ((int)n - 1) / pulses, lastPulse = ((int)n - 1) % pulses;
                double lastShot = completeBursts * (w.maximumBeamSeconds + w.beamRecoverySeconds) + lastPulse * interval;
                return w.damage * n / (lastShot + Math.Max(interval, Math.Max(w.reloadSeconds, w.beamRecoverySeconds)));
            }
            if (n > 0) return w.damage * n / ((n - 1) * interval + Math.Max(interval, w.reloadSeconds));
            return w.damage / interval;
        }
    }
}
