using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MechaBattle.Contracts.Construction;
using MechaBattle.Construction;

internal static class ConstructionDefaultsSmoke
{
    public static object Run(string project)
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "config/prototype-defaults.json")));
        var catalog = JsonSerializer.Deserialize<ConstructionCatalog>(doc.RootElement.GetProperty("constructionPrototype").GetProperty("catalog").GetRawText(), options);
        int count = 0;
        var builds = new List<object>();
        foreach (var preset in catalog.presets)
        {
            var original = ConstructionEvaluator.Evaluate(catalog, preset);
            var restored = JsonSerializer.Deserialize<BuildDefinition>(JsonSerializer.Serialize(preset, options), options);
            var copy = ConstructionEvaluator.Evaluate(catalog, restored);
            if (copy.Cost != original.Cost || copy.MaxHealth != original.MaxHealth || copy.FireDamage != original.FireDamage
                || copy.CooldownSeconds != original.CooldownSeconds || copy.Equipment.Count != 3)
                throw new Exception("Saved build did not round-trip: " + preset.id);
            builds.Add(new { id = preset.id, cost = original.Cost, cooldownSeconds = original.CooldownSeconds, health = original.MaxHealth, speed = original.MoveSpeed, damage = original.FireDamage, range = original.FireRange });
            count++;
        }
        if (count != 2) throw new Exception("Expected the two prototype presets.");
        int combinations = 0;
        foreach (var left in catalog.parts.Where(p => p.slot == PartSlot.LeftArm))
        foreach (var right in catalog.parts.Where(p => p.slot == PartSlot.RightArm))
        foreach (var weapon in catalog.parts.Where(p => p.slot == PartSlot.PrimaryWeapon))
        {
            var build = new BuildDefinition { id = "free-build-" + combinations, frameId = catalog.frames[0].id,
                equipment = new[] { new EquipmentSelection { slot = PartSlot.LeftArm, partId = left.id },
                    new EquipmentSelection { slot = PartSlot.RightArm, partId = right.id },
                    new EquipmentSelection { slot = PartSlot.PrimaryWeapon, partId = weapon.id } } };
            ConstructionEvaluator.Evaluate(catalog, build);
            combinations++;
        }
        if (combinations != 8) throw new Exception("Expected all eight free combinations to validate.");
        return new { name = "Construction_ActualCatalogAndSavedBuildRoundTrip", status = "passed", presets = count, freeCombinations = combinations, builds };
    }
}
