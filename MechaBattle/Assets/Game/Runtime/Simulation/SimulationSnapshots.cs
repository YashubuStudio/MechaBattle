using MechaBattle.Contracts;

namespace MechaBattle.Simulation
{
    public enum MatchPhase { Running, Finished }

    // Copy-only truth views for presentation/observation/tests. AI must receive a
    // filtered observation view, never this world's unrestricted accessor.
    public readonly struct UnitSnapshot
    {
        public readonly int SlotId, UnitId, Team, DeckIndex, Ammo;
        public readonly ControllerType Controller;
        public readonly bool IsAlive;
        public readonly float X, Z, Health, Shield;
        internal UnitSnapshot(int slot, int unit, int team, ControllerType controller,
            bool alive, int deck, float x, float z, float health, float shield, int ammo)
        {
            SlotId = slot; UnitId = unit; Team = team; Controller = controller;
            IsAlive = alive; DeckIndex = deck; X = x; Z = z;
            Health = health; Shield = shield; Ammo = ammo;
        }
    }

    public readonly struct MainBaseSnapshot
    {
        public readonly string Id;
        public readonly int Team;
        public readonly float X, Z, AttackRadius;
        public readonly double DamageReceived;
        internal MainBaseSnapshot(string id, int team, float x, float z, float radius, double damage)
        { Id = id; Team = team; X = x; Z = z; AttackRadius = radius; DamageReceived = damage; }
    }

    public readonly struct CaptureSnapshot
    {
        public readonly string Id;
        public readonly int OwnerTeam, BluePresence, OrangePresence;
        public readonly float X, Z, Radius, Progress;
        internal CaptureSnapshot(string id, float x, float z, float radius, int owner,
            float progress, int blue, int orange)
        { Id = id; X = x; Z = z; Radius = radius; OwnerTeam = owner;
          Progress = progress; BluePresence = blue; OrangePresence = orange; }
    }

    public readonly struct SimulationCounters
    {
        public readonly long ShotsFired, UnitsDestroyed, UnitsSpawned, BaseHits, CaptureChanges;
        internal SimulationCounters(long shots, long destroyed, long spawned, long baseHits, long captures)
        { ShotsFired = shots; UnitsDestroyed = destroyed; UnitsSpawned = spawned;
          BaseHits = baseHits; CaptureChanges = captures; }
    }
}
