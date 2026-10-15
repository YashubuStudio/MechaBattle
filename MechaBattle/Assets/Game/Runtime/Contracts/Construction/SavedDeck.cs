using System;
namespace MechaBattle.Contracts.Construction
{
    [Serializable]
    public sealed class SavedDeck
    {
        public int schemaVersion = 1, team;
        public BuildDefinition[] deck = Array.Empty<BuildDefinition>();
    }
}
