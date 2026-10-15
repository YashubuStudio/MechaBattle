using System;
using UnityEngine;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
using MechaBattle.Observation;
using MechaBattle.AI.Bots;

namespace MechaBattle.UnityAdapters
{
    // Bridges Game.Simulation (Unity-independent, GPT-owned) to scene Transforms.
    // Also exposes ISelfObservationSource so a Game.AI.Bots command source can read its
    // OWN unit's state without ever referencing Game.Simulation (SYSTEM_DESIGN §2.1).
    //
    // Simulation/CommandSource are plain C# objects: they do NOT survive a domain reload
    // or a scene save+reload on their own. Map/config/humanSlotId are kept as serialized
    // JSON so Awake() can rebuild the exact same simulation every time the scene loads
    // (Editor open, Play mode, or a future build) without re-running the generator.
    // unitTransformsBySlot is a normal [SerializeField] Transform[] — Unity serializes
    // scene object references natively, no JSON needed for that part.
    public sealed class BattleSimulationRunner : MonoBehaviour, ISelfObservationSource, INearbyThreatSensor
    {
        [SerializeField] private string mapJson;
        [SerializeField] private string configJson;
        [SerializeField] private int humanSlotId = -1;
        [SerializeField] private Transform[] unitTransformsBySlot;

        public BattleSimulation Simulation { get; private set; }
        public IUnitCommandSource CommandSource { get; private set; }

        private double accumulatedSeconds;

        public void Initialize(MapDefinition map, SimulationConfig config, int humanSlotId, Transform[] unitTransformsBySlot)
        {
            if (unitTransformsBySlot == null || unitTransformsBySlot.Length != MatchLimits.TotalControllerSlots)
                throw new ArgumentException("Expected one transform per controller slot (" + MatchLimits.TotalControllerSlots + ").");
            mapJson = JsonUtility.ToJson(map);
            configJson = JsonUtility.ToJson(config);
            this.humanSlotId = humanSlotId;
            this.unitTransformsBySlot = unitTransformsBySlot;
            Bootstrap();
        }

        private void Awake() => Bootstrap();

        // Idempotent: runs after Initialize() in the Editor, and again on its own after
        // every domain reload / scene reload (that's the whole point of serializing the
        // JSON instead of relying on the live BattleSimulation instance surviving).
        private void Bootstrap()
        {
            if (Simulation != null || string.IsNullOrEmpty(mapJson) || string.IsNullOrEmpty(configJson))
                return;
            var map = JsonUtility.FromJson<MapDefinition>(mapJson);
            var config = JsonUtility.FromJson<SimulationConfig>(configJson);
            Simulation = new BattleSimulation(map, config, humanSlotId);
            // P0 default wiring: the only placeholder BOT that exists today. Swap this
            // for a factory once real TeamKnowledge-based AI (P0-03/P3/P4) lands.
            CommandSource = new ForwardMarchBotCommandSource(
                map, this, this, config.fireRange, config.maxAmmo, config.homeSupplyRadius);
            accumulatedSeconds = 0;
            SyncPresentation();
        }

        public bool TryGetSelf(int slotId, out SelfObservation self)
        {
            if (Simulation == null) { self = default; return false; }
            var u = Simulation.GetUnit(slotId);
            self = new SelfObservation(slotId, u.UnitId, u.Team, u.Controller, u.IsAlive, u.X, u.Z, u.Ammo);
            return true;
        }

        // Weapon-range-only scan: never returns anything beyond rangeMeters, and never
        // exposes team-wide enemy positions to the caller - just "what's closest to me".
        public NearbyEnemy FindNearestEnemy(int slotId, float rangeMeters)
        {
            if (Simulation == null) return NearbyEnemy.None;
            var self = Simulation.GetUnit(slotId);
            if (!self.IsAlive) return NearbyEnemy.None;

            float bestSquared = rangeMeters * rangeMeters;
            bool found = false;
            float bestX = 0f, bestZ = 0f;
            for (int slot = 0; slot < MatchLimits.TotalControllerSlots; slot++)
            {
                var u = Simulation.GetUnit(slot);
                if (!u.IsAlive || u.Team == self.Team) continue;
                float dx = u.X - self.X, dz = u.Z - self.Z;
                float squared = dx * dx + dz * dz;
                if (squared <= bestSquared) { bestSquared = squared; found = true; bestX = u.X; bestZ = u.Z; }
            }
            return found ? new NearbyEnemy(true, bestX, bestZ) : NearbyEnemy.None;
        }

        // Advances the simulation by one fixed tick: collect every slot's command from
        // CommandSource, submit, Step(), then mirror the result onto presentation.
        public void Tick()
        {
            if (Simulation == null || CommandSource == null || Simulation.Phase != MatchPhase.Running)
                return;
            for (int slot = 0; slot < MatchLimits.TotalControllerSlots; slot++)
                if (CommandSource.TryGetCommand(slot, Simulation.CurrentTick, out var command))
                    Simulation.Submit(slot, command);
            Simulation.Step();
            SyncPresentation();
        }

        // Decoupled from Unity's own FixedUpdate/Fixed Timestep (this isn't physics): we
        // accumulate real time and run exactly as many Simulation.FixedDeltaTime ticks as
        // have elapsed, so changing Project Settings' fixed timestep can't desync from
        // the domain's own tickHz.
        private void Update()
        {
            if (Simulation == null) return;
            accumulatedSeconds += Time.deltaTime;
            double step = Simulation.FixedDeltaTime;
            int guard = 0;
            while (accumulatedSeconds >= step && guard < 10)
            {
                Tick();
                accumulatedSeconds -= step;
                guard++;
            }
        }

        private void SyncPresentation()
        {
            for (int slot = 0; slot < unitTransformsBySlot.Length; slot++)
            {
                var t = unitTransformsBySlot[slot];
                if (t == null) continue;
                var u = Simulation.GetUnit(slot);
                if (t.gameObject.activeSelf != u.IsAlive)
                    t.gameObject.SetActive(u.IsAlive);
                if (u.IsAlive)
                    t.position = new Vector3(u.X, t.position.y, u.Z);
            }
        }
    }
}
