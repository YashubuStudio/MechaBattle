using MechaBattle.Contracts;

namespace MechaBattle.Observation
{
    // What a controller (human or BOT) may know about its OWN unit. This is not
    // enemy/world truth: BOT decision code must never reach Game.Simulation directly,
    // only this contract (and later TeamKnowledge/perception types added alongside it
    // for P0-03+). Keeping this assembly Unity- and Simulation-independent lets
    // Game.AI.Bots stay on the AI side of the one-way dependency in SYSTEM_DESIGN §2.1.
    public readonly struct SelfObservation
    {
        public readonly int SlotId, UnitId, Team, Ammo;
        public readonly ControllerType Controller;
        public readonly bool IsAlive;
        public readonly float X, Y, Z;

        public SelfObservation(int slotId, int unitId, int team, ControllerType controller,
            bool isAlive, float x, float z, int ammo, float y = 0)
        {
            SlotId = slotId; UnitId = unitId; Team = team; Controller = controller;
            IsAlive = isAlive; X = x; Y = y; Z = z; Ammo = ammo;
        }
    }

    public interface ISelfObservationSource
    {
        bool TryGetSelf(int slotId, out SelfObservation self);
    }

    public readonly struct NearbyEnemy
    {
        public readonly bool Found;
        public readonly float X, Y, Z;
        public readonly int SlotId, UnitId;
        public NearbyEnemy(bool found, float x, float z, float y = 0, int slotId = -1, int unitId = 0) { Found = found; X = x; Y = y; Z = z; SlotId = slotId; UnitId = unitId; }
        public static readonly NearbyEnemy None = new NearbyEnemy(false, 0f, 0f);
    }

    // Production adapters must enforce FOV, LOS, scan cadence and acquisition delay.
    // A radar report alone is not a visual firing contact.
    // Bounded-range enemy detection ONLY: callers may ask "is anything within
    // rangeMeters of my own position" and nothing else. This is a weapon-range sensor
    // (what you can see down your own gun barrel), not map-wide TeamKnowledge/radar -
    // callers cannot learn about enemies outside the requested range. Full fog-of-war/
    // perception-delay modeling is later P0-03+/P3/P4 work; this exists so a P0 BOT can
    // fight anyone who crosses its weapon range instead of silently walking through them.
    public interface INearbyThreatSensor
    {
        NearbyEnemy FindNearestEnemy(int slotId, float rangeMeters);
    }
}
