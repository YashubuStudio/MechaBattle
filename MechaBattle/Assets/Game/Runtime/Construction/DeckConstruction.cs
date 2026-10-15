using System;
using MechaBattle.Contracts.Construction;
namespace MechaBattle.Construction
{
    public static class DeckConstruction
    {
        public static BuildDefinition Copy(BuildDefinition source, string id = null)
        {
            if (source == null || source.equipment == null) throw new ArgumentException("Missing build.");
            var equipment = new EquipmentSelection[source.equipment.Length];
            for (int i = 0; i < equipment.Length; i++)
            {
                var e = source.equipment[i]; if (e == null) throw new ArgumentException("Missing equipment.");
                equipment[i] = new EquipmentSelection { slot = e.slot, partId = e.partId };
            }
            return new BuildDefinition { id = id ?? source.id, frameId = source.frameId, equipment = equipment,
                appearance = new AppearanceDefinition { paintId = source.appearance == null ? "Neutral" : source.appearance.paintId } };
        }
        public static SavedDeck CreateDefault(ConstructionCatalog catalog, int count)
        {
            if (catalog == null || catalog.presets == null || catalog.presets.Length == 0 || count < 2) throw new ArgumentException("Missing deck presets.");
            var result = new SavedDeck { deck = new BuildDefinition[count] };
            for (int i = 0; i < count; i++) result.deck[i] = Copy(catalog.presets[i % catalog.presets.Length], "player-" + (i + 1));
            Validate(catalog, result, count); return result;
        }
        public static ResolvedBuild[] Validate(ConstructionCatalog catalog, SavedDeck profile, int count)
        {
            if (profile == null || profile.schemaVersion != 1 || profile.team < 0 || profile.team > 1 || profile.deck == null || profile.deck.Length != count || count < 2)
                throw new ArgumentException("Unsupported or incomplete saved deck.");
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var result = new ResolvedBuild[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = ConstructionEvaluator.Evaluate(catalog, profile.deck[i]);
                if (!ids.Add(result[i].Id)) throw new ArgumentException("Duplicate deck build ID.");
            }
            return result;
        }
    }
}
