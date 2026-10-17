using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MechaBattle.Contracts;
using MechaBattle.Contracts.Construction;
using MechaBattle.Construction;

internal static class ConstructionDefaultsSmoke
{
    public static object Run(string project)
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "config/prototype-defaults.json")));
        var catalog = JsonSerializer.Deserialize<ConstructionCatalog>(doc.RootElement.GetProperty("constructionPrototype").GetProperty("catalog").GetRawText(), options);
        if(catalog.frames[0].maxHealth<2000)throw new Exception("User minimum2000HP is not respected.");
        foreach(var preset in catalog.presets)
        {var b=ConstructionEvaluator.Evaluate(catalog,preset);if(preset.id=="reinforced-longrifle"&&b.MaxHealth<3000)throw new Exception("Heavy customization must exceed3000HP.");}
        var combatRoot=doc.RootElement.GetProperty("playablePrototype");var meleeRules=combatRoot.GetProperty("meleeAttacks");
        if(meleeRules[0].GetProperty("damage").GetSingle()!=760||meleeRules[2].GetProperty("damage").GetSingle()!=3*meleeRules[0].GetProperty("damage").GetSingle()||meleeRules[4].GetProperty("damage").GetSingle()<2000)
            throw new Exception("User minimum melee power is not respected.");
        float[] recovery={0.10f,0.18f,0.16f,0.24f,0.60f};
        for(int i=0;i<5;i++)if(Math.Abs(meleeRules[i].GetProperty("recoverySeconds").GetSingle()-recovery[i])>0.00001f)throw new Exception("Updated melee recovery is not applied.");
        if(combatRoot.GetProperty("meleeHitRecoveryMultiplier").GetSingle()!=0.5f || combatRoot.GetProperty("meleeHitBoostRefundFraction").GetSingle()!=0.5f)throw new Exception("Confirmed melee hit benefits are missing.");
        if(meleeRules[2].GetProperty("recoverySeconds").GetSingle()<0.1f || meleeRules[3].GetProperty("recoverySeconds").GetSingle()<0.1f)throw new Exception("Whiffed dash melee still requires explicit recovery.");
        var repair=catalog.parts.Single(p=>p.id=="weapon.repairgun").weapon;
        if(repair.redLockRange!=25 || repair.yellowLockRange!=60 || repair.range!=65)throw new Exception("Repair lock ranges are not configured.");
        var pod=catalog.parts.Single(p=>p.id=="weapon.repairpod").weapon;var radar=catalog.parts.Single(p=>p.id=="weapon.radar").weapon;
        if(pod.utilityDuration!=180 || radar.utilityDuration<900 || pod.utilityHealth<=0 || radar.utilityHealth<=0 || !pod.ammoResupplyDisabled || !radar.ammoResupplyDisabled)throw new Exception("Long-lived support object configuration is invalid.");
        var mine=catalog.parts.Single(p=>p.id=="weapon.mine").weapon;
        if(pod.ammo<3 || radar.ammo<3 || mine.ammo<5)throw new Exception("Utility inventory cannot fill the user concurrent placement limits.");
        int frameTransitions = 0;
        foreach (var preset in catalog.presets)
        foreach (var frame in catalog.frames)
        { VerifyFrameChange(catalog, preset, frame, options); frameTransitions++; }
        int count = 0;
        var builds = new List<object>();
        foreach (var preset in catalog.presets)
        {
            var original = ConstructionEvaluator.Evaluate(catalog, preset);
            var restored = JsonSerializer.Deserialize<BuildDefinition>(JsonSerializer.Serialize(preset, options), options);
            var copy = ConstructionEvaluator.Evaluate(catalog, restored);
            if (copy.Cost != original.Cost || copy.MaxHealth != original.MaxHealth || copy.FireDamage != original.FireDamage
                || copy.CooldownSeconds != original.CooldownSeconds || copy.Equipment.Count != original.Equipment.Count || copy.CombatLoadout.WeaponCount != original.CombatLoadout.WeaponCount)
                throw new Exception("Saved build did not round-trip: " + preset.id);
            builds.Add(new { id = preset.id, cost = original.Cost, cooldownSeconds = original.CooldownSeconds, health = original.MaxHealth, speed = original.MoveSpeed, damage = original.FireDamage, range = original.FireRange });
            count++;
        }
        if (count != 10) throw new Exception("Expected ten presets including light and support frames.");
        // User request 2026-10-16: left/right arms must share the same family, so the free
        // combination matrices below only pair matching arms, not every left x right cross.
        int combinations = 0;
        foreach (var left in catalog.parts.Where(p => p.slot == PartSlot.LeftArm))
        foreach (var right in catalog.parts.Where(p => p.slot == PartSlot.RightArm && p.armFamily == left.armFamily))
        foreach (var weapon in catalog.parts.Where(p => p.slot == PartSlot.PrimaryWeapon))
        {
            var build = new BuildDefinition { id = "free-build-" + combinations, frameId = catalog.frames[0].id,
                equipment = new[] { new EquipmentSelection { slot = PartSlot.LeftArm, partId = left.id },
                    new EquipmentSelection { slot = PartSlot.RightArm, partId = right.id },
                    new EquipmentSelection { slot = PartSlot.PrimaryWeapon, partId = weapon.id } } };
            ConstructionEvaluator.Evaluate(catalog, build);
            combinations++;
        }
        if (combinations != 39) throw new Exception("Expected all 39 standard-frame single-slot combinations to validate.");
        int tripleCombinations = 0;
        var weapons = catalog.parts.Where(p => p.slot == PartSlot.PrimaryWeapon).ToArray();
        foreach (var left in catalog.parts.Where(p => p.slot == PartSlot.LeftArm))
        foreach (var right in catalog.parts.Where(p => p.slot == PartSlot.RightArm && p.armFamily == left.armFamily))
        foreach (var first in weapons)
        foreach (var second in weapons)
        foreach (var third in weapons)
        {
            var selections = new[] {
                new EquipmentSelection { slot = PartSlot.LeftArm, partId = left.id },
                new EquipmentSelection { slot = PartSlot.RightArm, partId = right.id },
                new EquipmentSelection { slot = PartSlot.PrimaryWeapon, partId = first.id },
                new EquipmentSelection { slot = PartSlot.SecondaryWeapon, partId = second.id },
                new EquipmentSelection { slot = PartSlot.TertiaryWeapon, partId = third.id } };
            var build = new BuildDefinition { id = "three-slot-" + tripleCombinations, frameId = catalog.frames[0].id, equipment = selections };
            var evaluated = ConstructionEvaluator.Evaluate(catalog, build);
            var actual = evaluated.CombatLoadout;
            if (actual.WeaponCount != 3 || actual.GetWeapon(0).Kind != first.weapon.kind
                || actual.GetWeapon(1).Kind != second.weapon.kind || actual.GetWeapon(2).Kind != third.weapon.kind
                || evaluated.WeaponPartId(2) != third.id)
                throw new Exception("Free three-slot inventory was not preserved.");
            Array.Reverse(selections);
            var restored = JsonSerializer.Deserialize<BuildDefinition>(JsonSerializer.Serialize(build, options), options);
            var copy = ConstructionEvaluator.Evaluate(catalog, restored);
            if (copy.Cost != evaluated.Cost || copy.CooldownSeconds != evaluated.CooldownSeconds || copy.WeaponPartId(0) != first.id
                || copy.CombatLoadout.GetWeapon(2).UtilityHealth != third.weapon.utilityHealth
                || copy.CombatLoadout.GetWeapon(1).AmmoResupplyDisabled != second.weapon.ammoResupplyDisabled
                || copy.CombatLoadout.GetWeapon(2).PodAmmoPerSecond != third.weapon.podAmmoPerSecond
                || copy.CombatLoadout.GetWeapon(1).MaxAmmo != second.weapon.ammo || copy.CombatLoadout.GetWeapon(2).FireDamage != third.weapon.damage)
                throw new Exception("Three-slot save/order round-trip changed the build.");
            foreach(var targetFrame in catalog.frames)VerifyFrameChange(catalog, build, targetFrame, options);
            tripleCombinations++;
        }
        if (tripleCombinations != 6591) throw new Exception("Expected all 6591 standard-frame three-slot combinations to validate.");
        return new { name = "Construction_ActualCatalogAndSavedBuildRoundTrip", status = "passed", presets = count,
            freeCombinations = combinations, tripleCombinations, frameTransitions, allFrameTripleTransitions = tripleCombinations * catalog.frames.Length, builds };
    }
    private static void VerifyFrameChange(ConstructionCatalog catalog, BuildDefinition source, FrameDefinition target, JsonSerializerOptions options)
    {
        string before=JsonSerializer.Serialize(source,options);
        if(!FrameConstruction.TryChangeFrame(catalog,source,target.id,out var candidate,out var replacements,out var errors))
            throw new Exception("Frame change failed: "+source.id+" -> "+target.id+": "+string.Join(";",errors));
        if(JsonSerializer.Serialize(source,options)!=before || candidate.id!=source.id || candidate.frameId!=target.id || candidate.equipment.Length!=source.equipment.Length)
            throw new Exception("Frame change mutated the original build or lost its identity/inventory.");
        foreach(var e in source.equipment)
        {
            var part=catalog.parts.Single(p=>p.id==e.partId);
            var after=candidate.equipment.Single(c=>c.slot==e.slot);
            if(FrameConstruction.CanMount(target,part,e.slot) && e.partId!=after.partId)
                throw new Exception("A compatible part was changed: "+e.partId);
            if(!FrameConstruction.CanMount(target,catalog.parts.Single(p=>p.id==after.partId),e.slot))
                throw new Exception("Incompatible part survived frame conversion.");
        }
        var resolved=ConstructionEvaluator.Evaluate(catalog,candidate);
        var restored=JsonSerializer.Deserialize<BuildDefinition>(JsonSerializer.Serialize(candidate,options),options);
        var loaded=ConstructionEvaluator.Evaluate(catalog,restored);
        if(loaded.FrameId!=resolved.FrameId || loaded.Cost!=resolved.Cost || loaded.CooldownSeconds!=resolved.CooldownSeconds || loaded.MaxHealth!=resolved.MaxHealth)
            throw new Exception("Converted frame did not survive save/load.");
    }
}
