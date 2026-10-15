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

        internal ResolvedBuild(BuildDefinition build, string connectionStandard, ResolvedEquipment[] equipment,
            float health, float shield, float speed, WeaponDefinition weapon,
            float weight, float energy, int cost, float cooldown)
        {
            Id = build.id; FrameId = build.frameId; ConnectionStandard = connectionStandard;
            PaintId = build.appearance == null ? "Neutral" : build.appearance.paintId;
            Equipment = Array.AsReadOnly(equipment);
            MaxHealth = health; MaxShield = shield; MoveSpeed = speed;
            FireDamage = weapon.damage; FireRange = weapon.range;
            FireIntervalSeconds = weapon.intervalSeconds; MaxAmmo = weapon.ammo;
            Weight = weight; Energy = energy; Cost = cost; CooldownSeconds = cooldown;
        }
    }
}
