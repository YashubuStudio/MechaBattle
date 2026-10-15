using System;
using NUnit.Framework;
using MechaBattle.Contracts.Construction;
using MechaBattle.Construction;

namespace MechaBattle.Tests
{
    public sealed class ConstructionTests
    {
        private static ConstructionCatalog Catalog()
        {
            return new ConstructionCatalog
            {
                frames = new[] { new FrameDefinition { id = "frame", connectionStandard = "std",
                    weight = 40, energy = 10, maxWeight = 100, maxEnergy = 60,
                    weaponHardpoints = 1, baseCost = 80, maxHealth = 100, maxShield = 50, moveSpeed = 12,
                    requiredSlots = new[] { PartSlot.LeftArm, PartSlot.RightArm, PartSlot.PrimaryWeapon } } },
                parts = new[] {
                    new PartDefinition { id = "left", slot = PartSlot.LeftArm, connectionStandard = "std", weight = 10, energy = 5, equipmentCost = 10 },
                    new PartDefinition { id = "right", slot = PartSlot.RightArm, connectionStandard = "std", weight = 10, energy = 5, equipmentCost = 10 },
                    new PartDefinition { id = "gun", slot = PartSlot.PrimaryWeapon, connectionStandard = "std", weight = 10, energy = 10, equipmentCost = 20,
                        weaponHardpoints = 1, weapon = new WeaponDefinition { damage = 20, intervalSeconds = 0.5f, range = 60, ammo = 30 } } },
                costPolicy = new CostPolicy { healthPerPoint = 0.25f, shieldPerPoint = 0.125f, speedPerMeter = 2,
                    damagePerSecond = 0.5f, rangePerMeter = 0.25f, ammoPerRound = 0.125f,
                    minimumCooldownSeconds = 10, cooldownPerCost = 0.25f, cooldownQuadratic = 0.0009765625f }
            };
        }
        private static BuildDefinition Build()
        {
            return new BuildDefinition { id = "ordinary-preset", frameId = "frame", equipment = new[] {
                new EquipmentSelection { slot = PartSlot.LeftArm, partId = "left" },
                new EquipmentSelection { slot = PartSlot.RightArm, partId = "right" },
                new EquipmentSelection { slot = PartSlot.PrimaryWeapon, partId = "gun" } } };
        }
        private static void Reject(ConstructionCatalog catalog, BuildDefinition build, string code)
        {
            Assert.That(ConstructionEvaluator.TryEvaluate(catalog, build, out var result, out var errors), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(string.Join(";", errors), Does.Contain(code));
            Assert.Throws<ArgumentException>(() => ConstructionEvaluator.Evaluate(catalog, build));
        }
        [Test]
        public void Construction_ComputesPerformanceCostAndConvexCooldown()
        {
            var result = ConstructionEvaluator.Evaluate(Catalog(), Build());
            Assert.That(result.Cost, Is.EqualTo(214));
            Assert.That(result.CooldownSeconds, Is.EqualTo(10 + 214 * 0.25f + 214 * 214 * 0.0009765625f));
            Assert.That(result.MaxHealth, Is.EqualTo(100));
            Assert.That(result.MoveSpeed, Is.EqualTo(12));
            Assert.That(result.FireDamage, Is.EqualTo(20));
            Assert.That(result.Weight, Is.EqualTo(70));
            Assert.That(result.Energy, Is.EqualTo(30));
        }
        [Test]
        public void Construction_PartSwapChangesStatsAndCombinationCost()
        {
            var catalog = Catalog(); var build = Build();
            var before = ConstructionEvaluator.Evaluate(catalog, build);
            catalog.parts[1].healthDelta = 40;
            catalog.parts[1].equipmentCost += 8;
            catalog.combinationRules = new[] { new CombinationCostRule { id = "synergy", partIds = new[] { "right", "gun" }, addedCost = 12 } };
            var after = ConstructionEvaluator.Evaluate(catalog, build);
            Assert.That(after.MaxHealth, Is.EqualTo(140));
            Assert.That(after.Cost - before.Cost, Is.EqualTo(30));
            Assert.That(after.CooldownSeconds, Is.GreaterThan(before.CooldownSeconds));
        }
        [Test]
        public void Construction_AppearanceAndSelectionOrderDoNotChangeCombat()
        {
            var catalog = Catalog(); var build = Build();
            var before = ConstructionEvaluator.Evaluate(catalog, build);
            build.appearance.paintId = "Orange"; Array.Reverse(build.equipment);
            var after = ConstructionEvaluator.Evaluate(catalog, build);
            Assert.That(after.Cost, Is.EqualTo(before.Cost));
            Assert.That(after.CooldownSeconds, Is.EqualTo(before.CooldownSeconds));
            Assert.That(after.MaxHealth, Is.EqualTo(before.MaxHealth));
            Assert.That(after.Equipment[0].Slot, Is.EqualTo(PartSlot.LeftArm));
        }
        [Test]
        public void Construction_ResultIsDetachedFromEditableData()
        {
            var catalog = Catalog(); var build = Build();
            var result = ConstructionEvaluator.Evaluate(catalog, build);
            build.equipment[0].partId = "changed"; build.appearance.paintId = "changed";
            catalog.parts[2].weapon.damage = 999; catalog.frames[0].maxHealth = 999;
            Assert.That(result.Equipment[0].PartId, Is.EqualTo("left"));
            Assert.That(result.PaintId, Is.EqualTo("Neutral"));
            Assert.That(result.FireDamage, Is.EqualTo(20));
            Assert.That(result.MaxHealth, Is.EqualTo(100));
        }
        [TestCase("weight")]
        [TestCase("energy")]
        [TestCase("hardpoints")]
        [TestCase("skills")]
        public void Construction_RejectsCapacityOverflow(string kind)
        {
            var c = Catalog();
            if (kind == "weight") { c.frames[0].maxWeight = 69; Reject(c, Build(), "WEIGHT_EXCEEDED"); }
            if (kind == "energy") { c.frames[0].maxEnergy = 29; Reject(c, Build(), "ENERGY_EXCEEDED"); }
            if (kind == "hardpoints") { c.parts[2].weaponHardpoints = 2; Reject(c, Build(), "HARDPOINTS_EXCEEDED"); }
            if (kind == "skills") { c.parts[1].skillSlots = 1; Reject(c, Build(), "SKILL_SLOTS_EXCEEDED"); }
        }
        [TestCase("missing")]
        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("slot")]
        [TestCase("standard")]
        [TestCase("exclusive")]
        public void Construction_RejectsInvalidAssembly(string kind)
        {
            var c = Catalog(); var b = Build();
            if (kind == "missing") { b.equipment = new[] { b.equipment[0], b.equipment[2] }; Reject(c, b, "SLOT_REQUIRED"); }
            if (kind == "duplicate") { b.equipment[1].slot = PartSlot.LeftArm; Reject(c, b, "SLOT_DUPLICATE"); }
            if (kind == "unknown") { b.equipment[1].partId = "missing"; Reject(c, b, "PART_UNKNOWN"); }
            if (kind == "slot") { b.equipment[1].partId = "left"; Reject(c, b, "SLOT_MISMATCH"); }
            if (kind == "standard") { c.parts[1].connectionStandard = "other"; Reject(c, b, "STANDARD_MISMATCH"); }
            if (kind == "exclusive") { c.parts[0].exclusiveGroup = c.parts[1].exclusiveGroup = "one-only"; Reject(c, b, "EXCLUSIVE_CONFLICT"); }
        }
        [TestCase("nan")]
        [TestCase("infinity")]
        [TestCase("negative-health")]
        [TestCase("zero-interval")]
        [TestCase("cost-overflow")]
        [TestCase("catalog-duplicate")]
        [TestCase("rule-unknown")]
        public void Construction_RejectsMalformedBalanceData(string kind)
        {
            var c = Catalog();
            if (kind == "nan") { c.parts[1].moveSpeedDelta = float.NaN; Reject(c, Build(), "PART_VALUES_INVALID"); }
            if (kind == "infinity") { c.costPolicy.cooldownQuadratic = float.PositiveInfinity; Reject(c, Build(), "COST_POLICY_INVALID"); }
            if (kind == "negative-health") { c.parts[1].healthDelta = -100; Reject(c, Build(), "PERFORMANCE_INVALID"); }
            if (kind == "zero-interval") { c.parts[2].weapon.intervalSeconds = 0; Reject(c, Build(), "WEAPON_INVALID"); }
            if (kind == "cost-overflow") { c.costPolicy.damagePerSecond = float.MaxValue; Reject(c, Build(), "COST_OUT_OF_RANGE"); }
            if (kind == "catalog-duplicate") { c.parts[1].id = "left"; Reject(c, Build(), "PART_ID_DUPLICATE"); }
            if (kind == "rule-unknown") { c.combinationRules = new[] { new CombinationCostRule { id = "bad", partIds = new[] { "left", "unknown" } } }; Reject(c, Build(), "COMBINATION_PART_INVALID"); }
        }
        [Test]
        public void Construction_NullInputReturnsErrorsWithoutPartialResult()
        {
            Reject(null, Build(), "CATALOG_SCHEMA");
            Reject(Catalog(), null, "BUILD_ID");
        }
    }
}
