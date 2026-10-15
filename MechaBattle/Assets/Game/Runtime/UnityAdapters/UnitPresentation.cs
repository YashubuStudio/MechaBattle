using UnityEngine;

namespace MechaBattle.UnityAdapters
{
    // Pure presentation marker: no gameplay state. BattleSimulationRunner drives
    // position/active-state from BattleSimulation snapshots every Tick().
    public sealed class UnitPresentation : MonoBehaviour
    {
        public int SlotId;
        public int Team;
    }
}
