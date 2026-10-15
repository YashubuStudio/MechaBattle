using System;
namespace MechaBattle.Contracts
{
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
        public UnitLoadout(string buildId, float health, float shield, float speed, int ammo,
            float damage, float range, float interval, int cost, float cooldown,
            WeaponFireMode fireMode = WeaponFireMode.FullAutomatic, int magazineCapacity = 0, float reloadSeconds = 0,
            float minimumChargeSeconds = 0, float fullChargeSeconds = 0, float maximumBeamSeconds = 0,
            float beamRecoverySeconds = 0, float firingMoveMultiplier = 1)
        {
            foreach (float v in new[] { health, speed, damage, range, interval, cooldown, firingMoveMultiplier })
                if (!Finite(v) || v <= 0) throw new ArgumentException("Invalid loadout performance.");
            foreach (float v in new[] { reloadSeconds, minimumChargeSeconds, fullChargeSeconds, maximumBeamSeconds, beamRecoverySeconds })
                if (!Finite(v) || v < 0) throw new ArgumentException("Invalid weapon timing.");
            if (string.IsNullOrWhiteSpace(buildId) || !Finite(shield) || shield < 0 || ammo < 1 || cost < 1
                || fireMode < WeaponFireMode.FullAutomatic || fireMode > WeaponFireMode.ContinuousBeam || firingMoveMultiplier > 1
                || magazineCapacity < 0 || magazineCapacity > ammo || (magazineCapacity > 0 && reloadSeconds <= 0)
                || (fireMode == WeaponFireMode.SingleShot && magazineCapacity != 1)
                || (fireMode == WeaponFireMode.Charge && (minimumChargeSeconds <= 0 || fullChargeSeconds < minimumChargeSeconds))
                || (fireMode == WeaponFireMode.ContinuousBeam && (maximumBeamSeconds < interval || beamRecoverySeconds <= 0)))
                throw new ArgumentException("Invalid loadout identity, resources or firing mode.");
            BuildId = buildId; MaxHealth = health; MaxShield = shield; MoveSpeed = speed; MaxAmmo = ammo;
            FireDamage = damage; FireRange = range; FireIntervalSeconds = interval; Cost = cost; CooldownSeconds = cooldown;
            FireMode = fireMode; MagazineCapacity = magazineCapacity; ReloadSeconds = reloadSeconds;
            MinimumChargeSeconds = minimumChargeSeconds; FullChargeSeconds = fullChargeSeconds;
            MaximumBeamSeconds = maximumBeamSeconds; BeamRecoverySeconds = beamRecoverySeconds; FiringMoveMultiplier = firingMoveMultiplier;
        }
        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    }
}
