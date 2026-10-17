using System;
using System.Collections.Generic;
using MechaBattle.Contracts.Construction;

namespace MechaBattle.Construction
{
    public readonly struct ResolvedEquipment
    {
        public readonly PartSlot Slot;
        public readonly string PartId;
        public ResolvedEquipment(PartSlot slot, string partId) { Slot = slot; PartId = partId; }
    }

    // Snapshot: later editing the saved build/catalog cannot alter an active unit.
    public sealed class ResolvedBuild
    {
        public string Id { get; }
        public string FrameId { get; }
        public string ConnectionStandard { get; }
        public string PaintId { get; }
        public IReadOnlyList<ResolvedEquipment> Equipment { get; }
        public float MaxHealth { get; }
        public float MaxShield { get; }
        public float MoveSpeed { get; }
        public float FireDamage { get; }
        public float FireRange { get; }
        public float FireIntervalSeconds { get; }
        public int MaxAmmo { get; }
        public float Weight { get; }
        public float Energy { get; }
        public int Cost { get; }
        public float CooldownSeconds { get; }
        public MechaBattle.Contracts.UnitLoadout CombatLoadout { get; }

        public string WeaponPartId(int index)
        {
            foreach (var e in Equipment) if (e.Slot >= PartSlot.PrimaryWeapon && index-- == 0) return e.PartId;
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        internal ResolvedBuild(BuildDefinition build, string connectionStandard, ResolvedEquipment[] equipment,
            float health, float shield, float speed, WeaponDefinition[] weapons,
            float downCapacity, float weight, float energy, int cost, float cooldown,
            MechaBattle.Contracts.MeleeAttackDefinition chargeMelee = null)
        {
            var weapon = weapons[0];
            Id = build.id; FrameId = build.frameId; ConnectionStandard = connectionStandard;
            PaintId = build.appearance == null ? "Neutral" : build.appearance.paintId;
            Equipment = Array.AsReadOnly(equipment);
            MaxHealth = health; MaxShield = shield; MoveSpeed = speed;
            FireDamage = weapon.damage; FireRange = weapon.range;
            FireIntervalSeconds = weapon.intervalSeconds; MaxAmmo = weapon.ammo;
            Weight = weight; Energy = energy; Cost = cost; CooldownSeconds = cooldown;
            var options = new MechaBattle.Contracts.UnitLoadout[weapons.Length];
            for (int i = 0; i < options.Length; i++)
            {
                var w = weapons[i];
                options[i] = new MechaBattle.Contracts.UnitLoadout(Id, health, shield, speed, w.ammo, w.damage, w.range, w.intervalSeconds, cost, cooldown,
                    w.mode, w.magazineCapacity, w.reloadSeconds, w.minimumChargeSeconds, w.fullChargeSeconds, w.maximumBeamSeconds, w.beamRecoverySeconds,
                    w.firingMoveMultiplier, downCapacity, w.downPoints, w.blastRadius, w.splashDamage, w.splashDownPoints, w.projectileSpeed, w.redLockRange,
                    w.kind, w.utilityRadius, w.utilityDuration, w.projectileGravity, w.fuseSeconds, w.yellowLockRange, w.hipSpreadDegrees, w.ammoResupplyDisabled, w.podShieldPerSecond, w.podAmmoPerSecond, w.utilityHealth, chargeMelee);
            }
            CombatLoadout = options.Length == 1 ? options[0] : options[0].WithWeapons(options);
        }
    }
}
