using System;
using System.Linq;
using NUnit.Framework;
using MechaBattle.Contracts.Construction;
using MechaBattle.Construction;

namespace MechaBattle.Tests
{
    public sealed class FrameConstructionTests
    {
        private static ConstructionCatalog Catalog()
        {
            var required = new[] { PartSlot.LeftArm, PartSlot.RightArm, PartSlot.PrimaryWeapon };
            return new ConstructionCatalog
            {
                frames = new[] {
                    new FrameDefinition { id="standard", connectionStandard="std", maxHealth=2000, maxShield=900, moveSpeed=12, weight=45, energy=10, maxWeight=145, maxEnergy=135, weaponHardpoints=3, baseCost=80, requiredSlots=required },
                    new FrameDefinition { id="heavy", connectionStandard="std", maxHealth=2650, maxShield=1100, moveSpeed=9.5f, weight=65, energy=10, maxWeight=170, maxEnergy=135, weaponHardpoints=3, baseCost=110, requiredSlots=required, allowedWeaponIds=new[]{"first-allowed","cannon","support"} }
                },
                parts = new[] {
                    new PartDefinition { id="left", slot=PartSlot.LeftArm, connectionStandard="std", weight=8, energy=5, armFamily="std" },
                    new PartDefinition { id="right", slot=PartSlot.RightArm, connectionStandard="std", weight=8, energy=5, armFamily="std" },
                    Gun("rifle"), Gun("first-allowed"), Gun("cannon"), Gun("support")
                },
                presets = new[] { Build("heavy", "cannon") },
                costPolicy = new CostPolicy { healthPerPoint=.01f, shieldPerPoint=.01f, speedPerMeter=1, minimumCooldownSeconds=10, cooldownPerCost=.1f }
            };
        }
        private static PartDefinition Gun(string id) => new PartDefinition { id=id, slot=PartSlot.PrimaryWeapon, connectionStandard="std", weight=10, energy=10, weaponHardpoints=1,
            weapon=new WeaponDefinition { damage=20, intervalSeconds=.5f, range=60, ammo=30 } };
        private static BuildDefinition Build(string frame="standard", string primary="rifle") => new BuildDefinition { id="player-2", frameId=frame, appearance=new AppearanceDefinition{paintId="Custom"},
            equipment=new[] { new EquipmentSelection{slot=PartSlot.LeftArm,partId="left"},new EquipmentSelection{slot=PartSlot.RightArm,partId="right"},new EquipmentSelection{slot=PartSlot.PrimaryWeapon,partId=primary} } };
        private static string Weapon(BuildDefinition build, PartSlot slot) => Array.Find(build.equipment,e=>e.slot==slot).partId;

        [Test]
        public void CompatibleEquipmentIsPreservedAndResultIsDetached()
        {
            var catalog=Catalog();var source=Build(primary:"cannon");
            var before=ConstructionEvaluator.Evaluate(catalog,source);
            Assert.That(FrameConstruction.TryChangeFrame(catalog,source,"heavy",out var result,out var changes,out var errors),Is.True,string.Join(";",errors));
            Assert.That(result.frameId,Is.EqualTo("heavy"));Assert.That(changes,Is.Empty);
            Assert.That(result.id,Is.EqualTo(source.id));Assert.That(result.appearance.paintId,Is.EqualTo("Custom"));
            Assert.That(Weapon(result,PartSlot.PrimaryWeapon),Is.EqualTo("cannon"));
            var after=ConstructionEvaluator.Evaluate(catalog,result);
            Assert.That(after.MaxHealth,Is.EqualTo(2650));Assert.That(after.MoveSpeed,Is.EqualTo(9.5f));
            Assert.That(after.Cost,Is.GreaterThan(before.Cost));Assert.That(after.CooldownSeconds,Is.GreaterThan(before.CooldownSeconds));
            result.equipment[0].partId="mutated";result.appearance.paintId="changed";
            Assert.That(source.frameId,Is.EqualTo("standard"));Assert.That(source.equipment[0].partId,Is.EqualTo("left"));Assert.That(source.appearance.paintId,Is.EqualTo("Custom"));
        }
        [Test]
        public void OnlyIncompatibleWeaponsAreReplacedInTheirOriginalSlots()
        {
            var catalog=Catalog();var source=Build();
            source.equipment=source.equipment.Concat(new[]{new EquipmentSelection{slot=PartSlot.SecondaryWeapon,partId="support"},new EquipmentSelection{slot=PartSlot.TertiaryWeapon,partId="rifle"}}).Reverse().ToArray();
            Assert.That(FrameConstruction.TryChangeFrame(catalog,source,"heavy",out var result,out var changes,out var errors),Is.True,string.Join(";",errors));
            Assert.That(changes.Length,Is.EqualTo(2));Assert.That(changes.All(e=>e.partId=="cannon"),Is.True);
            Assert.That(Weapon(result,PartSlot.PrimaryWeapon),Is.EqualTo("cannon"));Assert.That(Weapon(result,PartSlot.TertiaryWeapon),Is.EqualTo("cannon"));
            Assert.That(Weapon(result,PartSlot.SecondaryWeapon),Is.EqualTo("support"));Assert.That(Weapon(source,PartSlot.PrimaryWeapon),Is.EqualTo("rifle"));
            Assert.That(result.equipment.Length,Is.EqualTo(5));
            changes[0].partId="mutated-report";Assert.That(Weapon(result,PartSlot.TertiaryWeapon),Is.EqualTo("cannon"));
            Assert.That(FrameConstruction.TryChangeFrame(catalog,result,"standard",out var restored,out var backChanges,out errors),Is.True);
            Assert.That(backChanges,Is.Empty);Assert.That(Weapon(restored,PartSlot.SecondaryWeapon),Is.EqualTo("support"));
        }
        [Test]
        public void CatalogFallbackWorksWhenTargetHasNoPreset()
        {
            var catalog=Catalog();catalog.presets=Array.Empty<BuildDefinition>();
            Assert.That(FrameConstruction.TryChangeFrame(catalog,Build(),"heavy",out var result,out var changes,out var errors),Is.True,string.Join(";",errors));
            Assert.That(Weapon(result,PartSlot.PrimaryWeapon),Is.EqualTo("first-allowed"));
        }
        [TestCase("capacity", "WEIGHT_EXCEEDED")]
        [TestCase("arms", "STANDARD_MISMATCH")]
        [TestCase("unknown-part", "PART_UNKNOWN")]
        [TestCase("no-weapon", "PRIMARY_WEAPON_REQUIRED")]
        public void InvalidTransitionDoesNotMutateOrReturnAPartialBuild(string failure,string expected)
        {
            var catalog=Catalog();var source=Build();
            if(failure=="capacity")catalog.frames[1].maxWeight=70;
            if(failure=="arms"){catalog.frames[1].connectionStandard="other";catalog.parts[4].connectionStandard="other";}
            if(failure=="unknown-part")source.equipment[2].partId="missing";
            if(failure=="no-weapon")catalog.frames[1].allowedWeaponIds=new[]{"left"}; // malformed catalog is rejected rather than accepted.
            Assert.That(FrameConstruction.TryChangeFrame(catalog,source,"heavy",out var result,out var changes,out var errors),Is.False);
            Assert.That(result,Is.Null);Assert.That(changes,Is.Empty);Assert.That(string.Join(";",errors),Does.Contain(expected));
            Assert.That(source.frameId,Is.EqualTo("standard"));Assert.That(source.equipment[2].partId,Is.EqualTo(failure=="unknown-part"?"missing":"rifle"));
        }
        [TestCase("unknown-frame")]
        [TestCase("null-build")]
        [TestCase("null-equipment")]
        [TestCase("null-entry")]
        public void MissingInputReturnsFailureWithoutThrowing(string failure)
        {
            var source=Build();if(failure=="null-build")source=null;
            if(failure=="null-equipment")source.equipment=null;
            if(failure=="null-entry")source.equipment[0]=null;
            Assert.That(FrameConstruction.TryChangeFrame(Catalog(),source,failure=="unknown-frame"?"missing":"heavy",out var result,out var changes,out var errors),Is.False);
            Assert.That(result,Is.Null);Assert.That(changes,Is.Empty);Assert.That(errors,Is.Not.Empty);
        }
        [Test]
        public void MountingUsesFrameRulesForEveryWeaponSlotAndNeverTreatsArmsAsWeapons()
        {
            var catalog=Catalog();var heavy=catalog.frames[1];
            foreach(var slot in new[]{PartSlot.PrimaryWeapon,PartSlot.SecondaryWeapon,PartSlot.TertiaryWeapon})
            {Assert.That(FrameConstruction.CanMount(heavy,catalog.parts[2],slot),Is.False);Assert.That(FrameConstruction.CanMount(heavy,catalog.parts[4],slot),Is.True);Assert.That(FrameConstruction.CanMount(heavy,catalog.parts[0],slot),Is.False);}
            Assert.That(FrameConstruction.CanMount(catalog.frames[0],catalog.parts[2],PartSlot.TertiaryWeapon),Is.True);
            Assert.That(FrameConstruction.CanMount(heavy,catalog.parts[0],PartSlot.LeftArm),Is.True);
            Assert.That(FrameConstruction.CanMount(heavy,catalog.parts[0],PartSlot.RightArm),Is.False);
            Assert.That(FrameConstruction.CanMount(null,catalog.parts[2],PartSlot.PrimaryWeapon),Is.False);
            catalog.parts[4].connectionStandard="other";Assert.That(FrameConstruction.CanMount(heavy,catalog.parts[4],PartSlot.PrimaryWeapon),Is.False);
        }
    }
}
