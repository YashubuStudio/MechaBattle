using UnityEngine;

namespace MechaBattle.UnityAdapters
{
    public enum ObjectiveKind { MainBase, CapturePoint }

    // Presentation-only marker. Does not hold any authoritative gameplay state;
    // the P0 Simulation (Game.Simulation, GPT-owned) is the source of truth once it exists.
    public sealed class MapObjectiveMarker : MonoBehaviour
    {
        public ObjectiveKind Kind;
        public string ObjectiveId;
        public int Team = -1; // -1 = neutral (capture points start neutral)
        public float Radius;
    }
}
