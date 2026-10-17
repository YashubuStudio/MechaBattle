using System;
namespace MechaBattle.Contracts
{
    public enum WeaponKind { Ranged = 0, Melee = 1, RepairPod = 2, Grenade = 3, Mine = 4, RepairGun = 5, Radar = 6, StrategicNuclear = 7 }
    public enum WeaponFireMode { FullAutomatic = 0, SemiAutomatic = 1, SingleShot = 2, Charge = 3, ContinuousBeam = 4 }

    // Immutable resolved combat data shared by humans, bots and the simulation.
    public sealed class UnitLoadout
    {
        public string BuildId { get; }
        public float MaxHealth { get; }
        public float MaxShield { get; }
        public float MoveSpeed { get; }
        public int MaxAmmo { get; }
        public float FireDamage { get; }
        public float FireRange { get; }
        public float FireIntervalSeconds { get; }
        public int Cost { get; }
        public float CooldownSeconds { get; }
        public WeaponFireMode FireMode { get; }
        public int MagazineCapacity { get; }
        public float ReloadSeconds { get; }
        public float MinimumChargeSeconds { get; }
        public float FullChargeSeconds { get; }
        public float MaximumBeamSeconds { get; }
        public float BeamRecoverySeconds { get; }
        public float FiringMoveMultiplier { get; }
        public float DownPointCapacity { get; }
        public float FireDownPoints { get; }
        public float BlastRadius { get; }
        public float SplashDamage { get; }
        public float SplashDownPoints { get; }
        public float ProjectileSpeed { get; }
        public float RedLockRange { get; }
        public float YellowLockRange { get; }
        public float HipSpreadDegrees { get; }
        public WeaponKind Kind { get; }
        public float UtilityRadius { get; }
        public float UtilityDuration { get; }
        public float ProjectileGravity { get; }
        public float FuseSeconds { get; }
        public bool AmmoResupplyDisabled { get; }
        public float PodShieldPerSecond { get; }
        public float PodAmmoPerSecond { get; }
        public float UtilityHealth { get; }
        // User request 2026-10-16: charge melee is derived from the build's (matched
        // left/right) arm family, not a weapon-hardpoint choice. Null means "no arm-derived
        // override" - Simulation falls back to its own shared config.meleeAttacks Charge entry.
        public MeleeAttackDefinition ChargeMelee { get; }
        private UnitLoadout[] weapons;
        public int WeaponCount => weapons == null ? 1 : weapons.Length;
        public UnitLoadout GetWeapon(int index) { if (index < 0 || index >= WeaponCount) throw new ArgumentOutOfRangeException(nameof(index)); return weapons == null ? this : weapons[index]; }
        public UnitLoadout WithWeapons(params UnitLoadout[] options)
        {
            if (options == null || options.Length < 1 || options.Length > 3) throw new ArgumentException("Expected one to three weapons.");
            foreach (var option in options) if (option == null || option.weapons != null || option.MaxHealth != MaxHealth || option.MaxShield != MaxShield || option.MoveSpeed != MoveSpeed || option.Cost != Cost || option.CooldownSeconds != CooldownSeconds) throw new ArgumentException("Weapons must share the same resolved frame stats.");
            var copy = (UnitLoadout)MemberwiseClone(); copy.weapons = (UnitLoadout[])options.Clone(); return copy;
        }
        public UnitLoadout(string buildId, float health, float shield, float speed, int ammo,
            float damage, float range, float interval, int cost, float cooldown,
            WeaponFireMode fireMode = WeaponFireMode.FullAutomatic, int magazineCapacity = 0, float reloadSeconds = 0,
            float minimumChargeSeconds = 0, float fullChargeSeconds = 0, float maximumBeamSeconds = 0,
            float beamRecoverySeconds = 0, float firingMoveMultiplier = 1, float downPointCapacity = 600, float fireDownPoints = 0,
            float blastRadius = 0, float splashDamage = 0, float splashDownPoints = 0, float projectileSpeed = 0, float redLockRange = 0, WeaponKind kind = WeaponKind.Ranged, float utilityRadius = 0, float utilityDuration = 0, float projectileGravity = 0, float fuseSeconds = 0, float yellowLockRange = 0, float hipSpreadDegrees = 0, bool ammoResupplyDisabled = false, float podShieldPerSecond = 0, float podAmmoPerSecond = 0, float utilityHealth = 0, MeleeAttackDefinition chargeMelee = null)
        {
            foreach (float v in new[] { health, speed, damage, range, interval, cooldown, firingMoveMultiplier, downPointCapacity })
                if (!Finite(v) || v <= 0) throw new ArgumentException("Invalid loadout performance.");
            foreach (float v in new[] { reloadSeconds, minimumChargeSeconds, fullChargeSeconds, maximumBeamSeconds, beamRecoverySeconds, fireDownPoints, blastRadius, splashDamage, splashDownPoints, projectileSpeed, redLockRange, utilityRadius, utilityDuration, projectileGravity, fuseSeconds, yellowLockRange, hipSpreadDegrees, podShieldPerSecond, podAmmoPerSecond, utilityHealth })
                if (!Finite(v) || v < 0 || v > 86400) throw new ArgumentException("Invalid weapon timing or resource scale.");
            if (string.IsNullOrWhiteSpace(buildId) || !Finite(shield) || shield < 0 || ammo < 1 || cost < 1
                || fireMode < WeaponFireMode.FullAutomatic || fireMode > WeaponFireMode.ContinuousBeam || firingMoveMultiplier > 1
                || magazineCapacity < 0 || magazineCapacity > ammo || (magazineCapacity > 0 && reloadSeconds <= 0)
                || (fireMode != WeaponFireMode.Charge && (minimumChargeSeconds != 0 || fullChargeSeconds != 0))
                || (fireMode != WeaponFireMode.ContinuousBeam && (maximumBeamSeconds != 0 || beamRecoverySeconds != 0))
                || kind < WeaponKind.Ranged || kind > WeaponKind.StrategicNuclear
                || ((kind == WeaponKind.RepairPod || kind == WeaponKind.Radar || kind == WeaponKind.StrategicNuclear) && (utilityRadius <= 0 || utilityDuration <= 0))
                || (kind == WeaponKind.RepairGun && (fireMode != WeaponFireMode.FullAutomatic || projectileSpeed != 0 || blastRadius != 0 || fireDownPoints != 0))
                || (kind == WeaponKind.Grenade && (projectileGravity <= 0 || fuseSeconds <= 0 || projectileSpeed <= 0 || blastRadius <= 0))
                || (kind == WeaponKind.Mine && (blastRadius <= 0 || utilityDuration <= 0))
                || (blastRadius > 0 && ((projectileSpeed <= 0 && kind != WeaponKind.Mine) || splashDamage <= 0 || fireMode != WeaponFireMode.SingleShot))
                // Melee's own yellowLockRange may exceed its (short, contact-only) swing range:
                // it is a separate "track this target while closing distance" concept, not a
                // claim that melee can hit from that far (user request 2026-10-16). Every other
                // kind keeps the original "lock range must be within weapon range" invariant.
                || hipSpreadDegrees > 10 || (yellowLockRange > 0 && ((kind != WeaponKind.Melee && yellowLockRange >= range) || redLockRange >= yellowLockRange))
                || redLockRange > range || (fireMode == WeaponFireMode.ContinuousBeam && projectileSpeed != 0)
                || (blastRadius == 0 && (splashDamage != 0 || splashDownPoints != 0))
                || (fireMode == WeaponFireMode.SingleShot && magazineCapacity != 1)
                || (fireMode == WeaponFireMode.Charge && (minimumChargeSeconds <= 0 || fullChargeSeconds < minimumChargeSeconds))
                || (fireMode == WeaponFireMode.ContinuousBeam && (maximumBeamSeconds < interval || beamRecoverySeconds <= 0)))
                throw new ArgumentException("Invalid loadout identity, resources or firing mode.");
            // Defensive copy (GPT review, 2026-10-16): chargeMelee otherwise arrives as the same
            // mutable MeleeAttackDefinition instance stored in the shared catalog's PartDefinition,
            // which would let a catalog mutation reach back into every already-resolved loadout -
            // a regression from this class's "immutable resolved data" invariant.
            UtilityHealth=utilityHealth; AmmoResupplyDisabled=ammoResupplyDisabled; PodShieldPerSecond=podShieldPerSecond; PodAmmoPerSecond=podAmmoPerSecond; ChargeMelee=chargeMelee?.Copy();
            YellowLockRange=yellowLockRange; HipSpreadDegrees=hipSpreadDegrees;
            BuildId = buildId; MaxHealth = health; MaxShield = shield; MoveSpeed = speed; MaxAmmo = ammo;
            FireDamage = damage; FireRange = range; FireIntervalSeconds = interval; Cost = cost; CooldownSeconds = cooldown;
            FireMode = fireMode; MagazineCapacity = magazineCapacity; ReloadSeconds = reloadSeconds;
            MinimumChargeSeconds = minimumChargeSeconds; FullChargeSeconds = fullChargeSeconds;
            MaximumBeamSeconds = maximumBeamSeconds; BeamRecoverySeconds = beamRecoverySeconds; FiringMoveMultiplier = firingMoveMultiplier;
            DownPointCapacity = downPointCapacity; FireDownPoints = fireDownPoints; BlastRadius = blastRadius;
            SplashDamage = splashDamage; SplashDownPoints = splashDownPoints; ProjectileSpeed = projectileSpeed; RedLockRange = redLockRange; Kind = kind; UtilityRadius = utilityRadius; UtilityDuration = utilityDuration; ProjectileGravity = projectileGravity; FuseSeconds = fuseSeconds;
        }
        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    }
}
