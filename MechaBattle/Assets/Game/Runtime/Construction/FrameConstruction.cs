using System;
using System.Collections.Generic;
using MechaBattle.Contracts.Construction;

namespace MechaBattle.Construction
{
    // Garage changes are detached transactions: never leave the saved build half converted.
    public static class FrameConstruction
    {
        public static FrameDefinition FindFrame(ConstructionCatalog catalog, string id)
        {
            if (catalog?.frames != null) foreach (var frame in catalog.frames)
                if (frame != null && frame.id == id) return frame;
            return null;
        }

        public static bool CanMount(FrameDefinition frame, PartDefinition part, PartSlot slot)
        {
            if (frame == null || part == null || slot < PartSlot.LeftArm || slot > PartSlot.TertiaryWeapon
                || part.connectionStandard != frame.connectionStandard) return false;
            bool weapon = slot >= PartSlot.PrimaryWeapon;
            if (part.slot != slot && !(weapon && part.slot >= PartSlot.PrimaryWeapon && part.slot <= PartSlot.TertiaryWeapon)) return false;
            return !weapon || frame.allowedWeaponIds == null || frame.allowedWeaponIds.Length == 0
                || Array.IndexOf(frame.allowedWeaponIds, part.id) >= 0;
        }

        public static bool TryChangeFrame(ConstructionCatalog catalog, BuildDefinition source, string frameId,
            out BuildDefinition result, out EquipmentSelection[] replacements, out string[] errors)
        {
            result = null; replacements = Array.Empty<EquipmentSelection>();
            var target = FindFrame(catalog, frameId);
            if (target == null) { errors = new[] { "FRAME_UNKNOWN: " + frameId }; return false; }
            if (source == null || source.equipment == null || Array.IndexOf(source.equipment, null) >= 0 || catalog.parts == null)
            { errors = new[] { "SELECTION_INVALID" }; return false; }
            var candidate = DeckConstruction.Copy(source); candidate.frameId = frameId;
            var changes = new List<EquipmentSelection>();
            PartDefinition fallback = null;
            foreach (var e in candidate.equipment)
            {
                if (e.slot < PartSlot.PrimaryWeapon || e.slot > PartSlot.TertiaryWeapon) continue;
                var part = FindPart(catalog, e.partId);
                // Unknown IDs remain errors; a frame change must not hide corrupt saved data.
                if (part == null || CanMount(target, part, e.slot)) continue;
                if (fallback == null) fallback = DefaultWeapon(catalog, target);
                if (fallback == null) { errors = new[] { "PRIMARY_WEAPON_REQUIRED" }; return false; }
                e.partId = fallback.id;
                changes.Add(new EquipmentSelection { slot = e.slot, partId = e.partId });
            }
            if (!ConstructionEvaluator.TryEvaluate(catalog, candidate, out var resolved, out errors)) return false;
            result = candidate; replacements = changes.ToArray(); return true;
        }

        private static PartDefinition DefaultWeapon(ConstructionCatalog catalog, FrameDefinition target)
        {
            // Prefer the target frame's authored preset over an arbitrary catalog ordering.
            foreach (var preset in catalog.presets ?? Array.Empty<BuildDefinition>())
                if (preset != null && preset.frameId == target.id && preset.equipment != null)
                    foreach (var e in preset.equipment)
                        if (e != null && e.slot == PartSlot.PrimaryWeapon)
                        {
                            var part = FindPart(catalog, e.partId);
                            if (CanMount(target, part, PartSlot.PrimaryWeapon)) return part;
                        }
            foreach (var part in catalog.parts)
                if (CanMount(target, part, PartSlot.PrimaryWeapon)) return part;
            return null;
        }

        private static PartDefinition FindPart(ConstructionCatalog catalog, string id)
        { foreach (var part in catalog.parts) if (part != null && part.id == id) return part; return null; }
    }
}
