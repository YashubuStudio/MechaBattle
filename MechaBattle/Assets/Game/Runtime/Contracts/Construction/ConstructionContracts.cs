using System;

namespace MechaBattle.Contracts.Construction
{
    // Stage one: supported slots only. New slots require validation and visual adapters.
    public enum PartSlot { LeftArm = 0, RightArm = 1, PrimaryWeapon = 2 }

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
        public PartSlot[] requiredSlots = Array.Empty<PartSlot>();
    }
    [Serializable]
    public sealed class WeaponDefinition
    {
        public float damage, range, intervalSeconds;
        public int ammo;
    }
    [Serializable]
    public sealed class PartDefinition
    {
        public string id = "", connectionStandard = "", exclusiveGroup = "";
        public PartSlot slot;
        public float weight, energy, healthDelta, shieldDelta, moveSpeedDelta;
        public int weaponHardpoints, skillSlots, equipmentCost;
        public WeaponDefinition weapon;
    }
    [Serializable]
    public sealed class CostPolicy
    {
        public float healthPerPoint, shieldPerPoint, speedPerMeter;
        public float damagePerSecond, rangePerMeter, ammoPerRound;
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
