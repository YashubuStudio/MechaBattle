using System;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Observation;

namespace MechaBattle.AI.Bots
{
    // P0 PLACEHOLDER BOT: no enemy-UNIT perception at all, by design. It only reads its
    // own SelfObservation (never Game.Simulation/world truth for other units) plus
    // public, static map geometry (every main base's location is a known objective, not
    // hidden unit intel) to script: advance to the lane-matched enemy main base, hold
    // and fire once in range, retreat home to resupply when ammo runs dry, repeat. This
    // exists to exercise the full movement/fire/destroy/respawn/capture/base-damage loop
    // for P0 benchmarking (README's first task). Real TeamKnowledge-based perception and
    // decision-making is later P0-03 / P3 Squad / P4 Commander work, not this class.
    public sealed class ForwardMarchBotCommandSource : IUnitCommandSource
    {
        private readonly ISelfObservationSource selfSource;
        private readonly float fireRange;
        private readonly int maxAmmo;
        private readonly (float X, float Z)[] homeBySlot = new (float X, float Z)[MatchLimits.TotalControllerSlots];
        private readonly (float X, float Z)[] targetBySlot = new (float X, float Z)[MatchLimits.TotalControllerSlots];
        private readonly float homeSupplyRadius;
        private readonly bool[] retreating = new bool[MatchLimits.TotalControllerSlots];
        private readonly uint[] nextSequence = new uint[MatchLimits.TotalControllerSlots];

        public ForwardMarchBotCommandSource(MapDefinition map, ISelfObservationSource selfSource,
            float fireRange, int maxAmmo, float homeSupplyRadius)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            this.selfSource = selfSource ?? throw new ArgumentNullException(nameof(selfSource));
            this.fireRange = fireRange;
            this.maxAmmo = maxAmmo;
            this.homeSupplyRadius = homeSupplyRadius;

            var basesByTeam = new MainBaseDefinition[MatchLimits.TeamCount][];
            for (int t = 0; t < MatchLimits.TeamCount; t++)
            {
                int count = 0;
                foreach (var b in map.mainBases) if (b.team == t) count++;
                basesByTeam[t] = new MainBaseDefinition[count];
                int i = 0;
                foreach (var b in map.mainBases) if (b.team == t) basesByTeam[t][i++] = b;
            }

            // Mirror BattleSimulation.TrySpawn's own home-base round robin so each slot's
            // scripted destination matches where it actually spawns/respawns.
            for (int slot = 0; slot < MatchLimits.TotalControllerSlots; slot++)
            {
                int team = slot / MatchLimits.ControllerSlotsPerTeam;
                int local = slot % MatchLimits.ControllerSlotsPerTeam;
                var ownBases = basesByTeam[team];
                var home = ownBases[local % ownBases.Length];
                homeBySlot[slot] = (home.x, home.z);

                // Lane-matched enemy target: the enemy base whose Z is closest to home's Z.
                var enemyBases = basesByTeam[1 - team];
                var best = enemyBases[0];
                float bestDz = Math.Abs(best.z - home.z);
                foreach (var candidate in enemyBases)
                {
                    float dz = Math.Abs(candidate.z - home.z);
                    if (dz < bestDz) { bestDz = dz; best = candidate; }
                }
                targetBySlot[slot] = (best.x, best.z);
            }
        }

        public bool TryGetCommand(int slotId, long tick, out UnitCommand command)
        {
            command = default;
            if (!selfSource.TryGetSelf(slotId, out var self) || !self.IsAlive ||
                self.Controller != ControllerType.Bot)
                return false; // Only ever drives Bot slots; never puppets Human or RemoteHuman.

            if (self.Ammo <= 0) retreating[slotId] = true;
            else if (self.Ammo >= maxAmmo) retreating[slotId] = false;

            var (destX, destZ) = retreating[slotId] ? homeBySlot[slotId] : targetBySlot[slotId];
            float dx = destX - self.X, dz = destZ - self.Z;
            float distance = (float)Math.Sqrt((double)dx * dx + (double)dz * dz);
            float stopDistance = retreating[slotId] ? homeSupplyRadius * 0.5f : fireRange * 0.9f;

            float moveX = 0f, moveY = 0f;
            if (distance > stopDistance)
            {
                moveX = dx / distance;
                moveY = dz / distance;
            }
            // +Z = 0 degrees, +X = 90 degrees (docs/P0_SIMULATION_API.md AimYaw contract).
            float aimYaw = distance > 0.01f
                ? (float)(Math.Atan2(dx, dz) * (180.0 / Math.PI))
                : 0f;
            bool fire = !retreating[slotId] && distance <= fireRange;

            command = new UnitCommand(
                unitId: self.UnitId, tick: tick, sequence: ++nextSequence[slotId],
                moveX: moveX, moveY: moveY, aimYaw: aimYaw, aimPitch: 0f,
                boost: false, firePrimary: fire, fireSecondary: false, melee: false, useAbility: false);
            return true;
        }
    }
}
