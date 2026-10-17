using System;
using System.Collections.Generic;
using MechaBattle.Contracts.Construction;

namespace MechaBattle.Construction
{
    public static class ConstructionEvaluator
    {
        public static ResolvedBuild Evaluate(ConstructionCatalog catalog, BuildDefinition build)
        {
            if (!TryEvaluate(catalog, build, out var result, out var errors))
                throw new ArgumentException(string.Join("; ", errors));
            return result;
        }

        public static bool TryEvaluate(ConstructionCatalog catalog, BuildDefinition build,
            out ResolvedBuild result, out string[] errors)
        {
            result = null;
            var issues = new List<string>();
            var frames = new Dictionary<string, FrameDefinition>(StringComparer.Ordinal);
            var parts = new Dictionary<string, PartDefinition>(StringComparer.Ordinal);
            ValidateCatalog(catalog, frames, parts, issues);
            if (issues.Count != 0) { errors = issues.ToArray(); return false; }
            if (build == null || string.IsNullOrWhiteSpace(build.id) || string.IsNullOrWhiteSpace(build.frameId))
                issues.Add("BUILD_ID: build and frame IDs are required.");
            if (issues.Count != 0) { errors = issues.ToArray(); return false; }
            if (!frames.TryGetValue(build.frameId, out var frame))
            { errors = new[] { "FRAME_UNKNOWN: " + build.frameId }; return false; }
            if (build.equipment == null) issues.Add("EQUIPMENT_NULL");
            var selected = new Dictionary<PartSlot, PartDefinition>();
            var exclusive = new HashSet<string>(StringComparer.Ordinal);
            foreach (var choice in build.equipment ?? Array.Empty<EquipmentSelection>())
            {
                if (choice == null || !ValidSlot(choice.slot) || string.IsNullOrWhiteSpace(choice.partId))
                { issues.Add("SELECTION_INVALID"); continue; }
                if (selected.ContainsKey(choice.slot))
                { issues.Add("SLOT_DUPLICATE: " + choice.slot); continue; }
                if (!parts.TryGetValue(choice.partId, out var part))
                { issues.Add("PART_UNKNOWN: " + choice.partId); continue; }
                selected.Add(choice.slot, part);
                if (part.slot != choice.slot && !(IsWeaponSlot(part.slot) && IsWeaponSlot(choice.slot))) issues.Add("SLOT_MISMATCH: " + choice.partId);
                if (!string.Equals(part.connectionStandard, frame.connectionStandard, StringComparison.Ordinal))
                    issues.Add("STANDARD_MISMATCH: " + choice.partId);
                if (IsWeaponSlot(choice.slot) && frame.allowedWeaponIds != null && frame.allowedWeaponIds.Length > 0
                    && Array.IndexOf(frame.allowedWeaponIds, choice.partId) < 0)
                    issues.Add("WEAPON_NOT_ALLOWED_ON_FRAME: " + choice.partId);
                if (!string.IsNullOrEmpty(part.exclusiveGroup) && !exclusive.Add(part.exclusiveGroup))
                    issues.Add("EXCLUSIVE_CONFLICT: " + part.exclusiveGroup);
            }
            foreach (var slot in frame.requiredSlots)
                if (!selected.ContainsKey(slot)) issues.Add("SLOT_REQUIRED: " + slot);
            if (issues.Count != 0) { errors = issues.ToArray(); return false; }
            // User request 2026-10-16: left/right arms must be the same family (both
            // "standard" or both "reinforced"), so the build's charge melee is well-defined.
            MechaBattle.Contracts.MeleeAttackDefinition chargeMelee = null;
            if (selected.TryGetValue(PartSlot.LeftArm, out var leftArm) && selected.TryGetValue(PartSlot.RightArm, out var rightArm))
            {
                if (!string.Equals(leftArm.armFamily, rightArm.armFamily, StringComparison.Ordinal))
                    issues.Add("ARM_FAMILY_MISMATCH: " + leftArm.id + " / " + rightArm.id);
                else chargeMelee = leftArm.chargeMelee ?? rightArm.chargeMelee;
            }
            if (issues.Count != 0) { errors = issues.ToArray(); return false; }

            // Canonical slot order makes serialization order irrelevant to calculations.
            double weight = frame.weight, energy = frame.energy;
            double health = frame.maxHealth, shield = frame.maxShield, speed = frame.moveSpeed, downCapacity = frame.downPointCapacity;
            long hardpoints = 0, skills = 0, equipmentCost = 0;
            var equipment = new List<ResolvedEquipment>();
            var selectedIds = new HashSet<string>(StringComparer.Ordinal);
            WeaponDefinition weapon = null; var weapons = new List<WeaponDefinition>();
            foreach (PartSlot slot in new[] { PartSlot.LeftArm, PartSlot.RightArm, PartSlot.PrimaryWeapon, PartSlot.SecondaryWeapon, PartSlot.TertiaryWeapon })
            {
                if (!selected.TryGetValue(slot, out var part)) continue;
                equipment.Add(new ResolvedEquipment(slot, part.id)); selectedIds.Add(part.id);
                weight += part.weight; energy += part.energy;
                downCapacity += part.downPointCapacityDelta; health += part.healthDelta; shield += part.shieldDelta; speed += part.moveSpeedDelta;
                hardpoints += part.weaponHardpoints; skills += part.skillSlots;
                equipmentCost += part.equipmentCost;
                if (IsWeaponSlot(slot)) { weapons.Add(part.weapon); if (weapon == null) weapon = part.weapon; }
            }
            if (weight > frame.maxWeight) issues.Add("WEIGHT_EXCEEDED");
            if (energy > frame.maxEnergy) issues.Add("ENERGY_EXCEEDED");
            if (hardpoints > frame.weaponHardpoints) issues.Add("HARDPOINTS_EXCEEDED");
            if (skills > frame.skillSlots) issues.Add("SKILL_SLOTS_EXCEEDED");
            if (!Positive(health) || !Nonnegative(shield) || !Positive(speed) || !Positive(downCapacity)) issues.Add("PERFORMANCE_INVALID");
            if (weapon == null) issues.Add("PRIMARY_WEAPON_REQUIRED");
            if (issues.Count != 0) { errors = issues.ToArray(); return false; }

            var policy = catalog.costPolicy;
            long combinationCost = 0;
            foreach (var rule in catalog.combinationRules)
            {
                bool applies = true;
                foreach (var id in rule.partIds) if (!selectedIds.Contains(id)) { applies = false; break; }
                if (applies) combinationCost += rule.addedCost;
            }
            double totalWeaponCost = 0, bestWeaponCost = 0;
            foreach (var w in weapons)
            {
                double value = WeaponPerformance.SustainedDps(w) * policy.damagePerSecond
                    + WeaponPerformance.SustainedDps(w) / w.damage * w.downPoints * policy.downPointsPerSecond
                    + w.blastRadius * policy.blastRadiusPerMeter + (w.redLockRange + w.yellowLockRange*0.25) * policy.redLockPerMeter + w.projectileSpeed * policy.projectileSpeedPerMeter
                    + w.range * policy.rangePerMeter + w.ammo * policy.ammoPerRound;
                totalWeaponCost += value; bestWeaponCost = Math.Max(bestWeaponCost, value);
            }
            double costValue = frame.baseCost + (double)equipmentCost + combinationCost
                + health * policy.healthPerPoint + shield * policy.shieldPerPoint + speed * policy.speedPerMeter
                + downCapacity * policy.downPointCapacityPerPoint + bestWeaponCost + (totalWeaponCost - bestWeaponCost) * policy.reserveWeaponPerformanceMultiplier;
            double roundedCost = Math.Ceiling(costValue);
            if (double.IsNaN(roundedCost) || roundedCost < 1 || roundedCost > int.MaxValue)
                issues.Add("COST_OUT_OF_RANGE");
            double cooldown = policy.minimumCooldownSeconds + roundedCost * policy.cooldownPerCost
                + roundedCost * roundedCost * policy.cooldownQuadratic;
            if (!Positive(cooldown)) issues.Add("COOLDOWN_OUT_OF_RANGE");
            if (!Nonnegative(weight) || !Nonnegative(energy)) issues.Add("CAPACITY_OUT_OF_RANGE");
            if (issues.Count != 0) { errors = issues.ToArray(); return false; }
            result = new ResolvedBuild(build, frame.connectionStandard, equipment.ToArray(), (float)health, (float)shield, (float)speed,
                weapons.ToArray(), (float)downCapacity, (float)weight, (float)energy, (int)roundedCost, (float)cooldown, chargeMelee);
            errors = Array.Empty<string>();
            return true;
        }

        private static void ValidateCatalog(ConstructionCatalog c,
            Dictionary<string, FrameDefinition> frames, Dictionary<string, PartDefinition> parts, List<string> issues)
        {
            if (c == null || c.schemaVersion != 1 || c.frames == null || c.parts == null || c.presets == null || c.combinationRules == null)
            { issues.Add("CATALOG_SCHEMA: expected version 1 and non-null arrays."); return; }
            var p = c.costPolicy;
            if (p == null || !Nonnegative(p.reserveWeaponPerformanceMultiplier) || p.reserveWeaponPerformanceMultiplier > 1 || !Nonnegative(p.healthPerPoint) || !Nonnegative(p.shieldPerPoint)
                || !Nonnegative(p.speedPerMeter) || !Nonnegative(p.damagePerSecond) || !Nonnegative(p.rangePerMeter)
                || !Nonnegative(p.redLockPerMeter) || !Nonnegative(p.projectileSpeedPerMeter) || !Nonnegative(p.downPointsPerSecond) || !Nonnegative(p.downPointCapacityPerPoint) || !Nonnegative(p.blastRadiusPerMeter)
                || !Nonnegative(p.ammoPerRound) || !Positive(p.minimumCooldownSeconds)
                || !Nonnegative(p.cooldownPerCost) || !Nonnegative(p.cooldownQuadratic))
                issues.Add("COST_POLICY_INVALID");
            if (c.frames.Length == 0 || c.parts.Length == 0) issues.Add("CATALOG_EMPTY");
            foreach (var f in c.frames)
            {
                if (f == null || string.IsNullOrWhiteSpace(f.id) || string.IsNullOrWhiteSpace(f.connectionStandard))
                { issues.Add("FRAME_DEFINITION_INVALID"); continue; }
                if (frames.ContainsKey(f.id)) issues.Add("FRAME_ID_DUPLICATE: " + f.id);
                else frames.Add(f.id, f);
                if (!Nonnegative(f.weight) || !Nonnegative(f.energy) || !Positive(f.maxWeight) || !Positive(f.maxEnergy)
                    || f.weight > f.maxWeight || f.energy > f.maxEnergy || f.weaponHardpoints < 1 || f.skillSlots < 0
                    || !Positive(f.downPointCapacity) || f.baseCost <= 0 || !Positive(f.maxHealth) || !Nonnegative(f.maxShield) || !Positive(f.moveSpeed))
                    issues.Add("FRAME_VALUES_INVALID: " + f.id);
                var slots = new HashSet<PartSlot>();
                if (f.requiredSlots == null) issues.Add("FRAME_SLOTS_NULL: " + f.id);
                else foreach (var slot in f.requiredSlots)
                    if (!ValidSlot(slot) || !slots.Add(slot)) issues.Add("FRAME_SLOT_INVALID: " + f.id);
                if (slots.Count != 3 || !slots.Contains(PartSlot.LeftArm) || !slots.Contains(PartSlot.RightArm) || !slots.Contains(PartSlot.PrimaryWeapon)) issues.Add("FRAME_SLOTS: version 1 requires both arms and a primary weapon.");
            }
            foreach (var part in c.parts)
            {
                if (part == null || string.IsNullOrWhiteSpace(part.id) || string.IsNullOrWhiteSpace(part.connectionStandard))
                { issues.Add("PART_DEFINITION_INVALID"); continue; }
                if (parts.ContainsKey(part.id)) issues.Add("PART_ID_DUPLICATE: " + part.id);
                else parts.Add(part.id, part);
                if (!ValidSlot(part.slot) || !Nonnegative(part.weight) || !Nonnegative(part.energy)
                    || !Finite(part.downPointCapacityDelta) || !Finite(part.healthDelta) || !Finite(part.shieldDelta) || !Finite(part.moveSpeedDelta)
                    || part.weaponHardpoints < 0 || part.skillSlots < 0 || part.equipmentCost < 0)
                    issues.Add("PART_VALUES_INVALID: " + part.id);
                // User request 2026-10-16: every arm part must declare which family it belongs
                // to, so a build's left/right arms can be checked for a match and its charge
                // melee (derived from that family) is always well-defined.
                if ((part.slot == PartSlot.LeftArm || part.slot == PartSlot.RightArm) && string.IsNullOrEmpty(part.armFamily))
                    issues.Add("ARM_FAMILY_REQUIRED: " + part.id);
                if (IsWeaponSlot(part.slot))
                {
                    var w = part.weapon;
                    if (!WeaponPerformance.Valid(w) || part.weaponHardpoints < 1) issues.Add("WEAPON_INVALID: " + part.id);
                }
                // Unity inline serialization materializes a missing nested class as a zero-filled object.
                // A zero weapon is absent on arms; any authored weapon capability is still rejected.
                else if (WeaponPerformance.HasWeaponData(part.weapon))
                    issues.Add("WEAPON_SLOT_INVALID: " + part.id);
            }
            foreach (var f in c.frames)
            {
                if (f == null || f.allowedWeaponIds == null) continue;
                var seenWeaponIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var weaponId in f.allowedWeaponIds)
                {
                    if (string.IsNullOrEmpty(weaponId) || !seenWeaponIds.Add(weaponId))
                    { issues.Add("FRAME_ALLOWED_WEAPON_INVALID: " + f.id); continue; }
                    if (!parts.TryGetValue(weaponId, out var allowedPart) || !IsWeaponSlot(allowedPart.slot))
                        issues.Add("FRAME_ALLOWED_WEAPON_UNKNOWN: " + f.id + "/" + weaponId);
                }
            }
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in c.combinationRules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.id) || !ruleIds.Add(rule.id)
                    || rule.partIds == null || rule.partIds.Length < 2 || rule.addedCost < 0)
                { issues.Add("COMBINATION_RULE_INVALID"); continue; }
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in rule.partIds)
                    if (string.IsNullOrEmpty(id) || !parts.ContainsKey(id) || !ids.Add(id))
                        issues.Add("COMBINATION_PART_INVALID: " + rule.id);
            }
            var presetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var preset in c.presets)
                if (preset == null || string.IsNullOrWhiteSpace(preset.id) || !presetIds.Add(preset.id))
                    issues.Add("PRESET_ID_INVALID");
        }
        private static bool IsWeaponSlot(PartSlot value) { return value >= PartSlot.PrimaryWeapon && value <= PartSlot.TertiaryWeapon; }
        private static bool ValidSlot(PartSlot value) { return value >= PartSlot.LeftArm && value <= PartSlot.TertiaryWeapon; }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool Nonnegative(double value) { return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= float.MaxValue; }
        private static bool Positive(double value) { return Nonnegative(value) && value > 0; }
    }
}
