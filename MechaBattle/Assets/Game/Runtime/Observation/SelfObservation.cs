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
        public readonly float X, Z;

        public SelfObservation(int slotId, int unitId, int team, ControllerType controller,
            bool isAlive, float x, float z, int ammo)
        {
            SlotId = slotId; UnitId = unitId; Team = team; Controller = controller;
            IsAlive = isAlive; X = x; Z = z; Ammo = ammo;
        }
    }

    public interface ISelfObservationSource
    {
        bool TryGetSelf(int slotId, out SelfObservation self);
    }
}
