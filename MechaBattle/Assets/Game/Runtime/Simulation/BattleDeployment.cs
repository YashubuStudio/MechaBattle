using MechaBattle.Contracts;

namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        // Indices refer to the map's complete mainBases array, not a team's local ordinal.
        public bool CanSpawnAtMainBase(int team, int mainBaseIndex)
        {
            CheckTeam(team);
            return mainBaseIndex >= 0 && mainBaseIndex < bases.Length && bases[mainBaseIndex].Team == team&&!bases[mainBaseIndex].Destroyed;
        }

        // Existing callers retain -1 = automatic own base. A relay and a specific base
        // cannot both be selected. Readiness/ownership are checked again in TrySpawn.
        public bool RequestSpawn(int slot, int deck, int captureIndex = -1, int mainBaseIndex = -1)
        {
            var u = Unit(slot);
            if (Phase != MatchPhase.Running || u.Alive || deck < 0 || deck >= DeckCount || u.DeckReady[deck] > CurrentTick
                || captureIndex < -1 || captureIndex >= captures.Length || mainBaseIndex < -1
                || mainBaseIndex >= 0 && (captureIndex >= 0 || !CanSpawnAtMainBase(u.Team, mainBaseIndex))
                || captureIndex >= 0 && !CanSpawnAtCapture(u.Team, captureIndex)) return false;
            if(u.Returned&&deck!=u.Deck)return false;
            u.PreferredDeck = deck; u.PreferredSpawn = captureIndex; u.PreferredMainBase = mainBaseIndex;
            u.SpawnRequested = true;
            return true;
        }
    }
}
