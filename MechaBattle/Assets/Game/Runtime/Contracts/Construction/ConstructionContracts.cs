using System;

namespace MechaBattle.Contracts.Construction
{
    // Stage one: supported slots only. New slots require validation and visual adapters.
    public enum PartSlot { LeftArm = 0, RightArm = 1, PrimaryWeapon = 2, SecondaryWeapon = 3, TertiaryWeapon = 4 }

    [Serializable]
    public sealed class EquipmentSelection
    {
        public PartSlot slot;
        public string partId = "";
    }
    [Serializable]
    public sealed class AppearanceDefinition { public string paintId = "Neutral"; }
    [Serializable]
    public sealed class BuildDefinition
    {
        public string id = "", frameId = "";
        public EquipmentSelection[] equipment = Array.Empty<EquipmentSelection>();
        public AppearanceDefinition appearance = new AppearanceDefinition();
    }
    [Serializable]
    public sealed class FrameDefinition
    {
        public string id = "", connectionStandard = "";
        public float weight, energy, maxWeight, maxEnergy;
        public int weaponHardpoints, skillSlots, baseCost;
        public float maxHealth, maxShield, moveSpeed;
        public float downPointCapacity = 600;
        public PartSlot[] requiredSlots = Array.Empty<PartSlot>();
        // User request 2026-10-16: limit which weapons a frame can mount, so frames actually
        // differ from each other. Empty (the default) means unrestricted - every existing
        // frame that doesn't set this keeps its current behaviour.
        public string[] allowedWeaponIds = Array.Empty<string>();
    }
    [Serializable]
    public sealed class WeaponDefinition
    {
        public float damage, range, intervalSeconds, yellowLockRange, hipSpreadDegrees;
        public int ammo, magazineCapacity;
        public MechaBattle.Contracts.WeaponFireMode mode;
        public float reloadSeconds, minimumChargeSeconds, fullChargeSeconds, maximumBeamSeconds, beamRecoverySeconds;
        public MechaBattle.Contracts.WeaponKind kind;
        public float utilityRadius, utilityDuration, projectileGravity, fuseSeconds;
        public bool ammoResupplyDisabled;
        public float podShieldPerSecond, podAmmoPerSecond, utilityHealth;
        public float firingMoveMultiplier = 1;
        public float downPoints, blastRadius, splashDamage, splashDownPoints, projectileSpeed, redLockRange;
    }
    [Serializable]
    public sealed class PartDefinition
    {
        public string id = "", connectionStandard = "", exclusiveGroup = "";
        public PartSlot slot;
        public float weight, energy, healthDelta, shieldDelta, moveSpeedDelta, downPointCapacityDelta;
        public int weaponHardpoints, skillSlots, equipmentCost;
        public WeaponDefinition weapon;
        // User request 2026-10-16: arms must match left/right, so a build's charge melee
        // (derived from whichever arm family is equipped, not a weapon-hardpoint choice) is
        // always well-defined. Empty on non-arm parts - no effect on existing weapon/arm data.
        public string armFamily = "";
        public MechaBattle.Contracts.MeleeAttackDefinition chargeMelee;
    }
    [Serializable]
    public sealed class CostPolicy
    {
        public float healthPerPoint, shieldPerPoint, speedPerMeter;
        public float damagePerSecond, rangePerMeter, ammoPerRound;
        public float downPointsPerSecond, downPointCapacityPerPoint, blastRadiusPerMeter, redLockPerMeter, projectileSpeedPerMeter;
        public float reserveWeaponPerformanceMultiplier = 0.35f;
        public float minimumCooldownSeconds, cooldownPerCost, cooldownQuadratic;
    }
    [Serializable]
    public sealed class CombinationCostRule
    {
        public string id = "";
        public string[] partIds = Array.Empty<string>();
        public int addedCost;
    }
    [Serializable]
    public sealed class ConstructionCatalog
    {
        public int schemaVersion = 1;
        public FrameDefinition[] frames = Array.Empty<FrameDefinition>();
        public PartDefinition[] parts = Array.Empty<PartDefinition>();
        public BuildDefinition[] presets = Array.Empty<BuildDefinition>();
        public CombinationCostRule[] combinationRules = Array.Empty<CombinationCostRule>();
        public CostPolicy costPolicy = new CostPolicy();
    }
}
