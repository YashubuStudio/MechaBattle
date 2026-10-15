using System;
using System.Collections.Generic;

namespace MechaBattle.Contracts
{
    public static class MatchLimits
    {
        public const int TeamCount = 2;
        public const int ControllerSlotsPerTeam = 50;
        public const int CommanderSlotsPerTeam = 1;
        public const int TotalControllerSlots = TeamCount * ControllerSlotsPerTeam;
        public const int MinimumMainBasesPerTeam = 2;
    }

    public enum ControllerType { Bot, Human, RemoteHuman }

    public readonly struct ControllerSlot
    {
        public readonly int Id;
        public readonly int Team;
        public readonly ControllerType Controller;
        public ControllerSlot(int id, int team, ControllerType controller)
        { Id = id; Team = team; Controller = controller; }
    }

    public readonly struct CommanderSlot
    {
        public readonly int Team;
        public readonly ControllerType Controller;
        public CommanderSlot(int team, ControllerType controller)
        { Team = team; Controller = controller; }
    }

    // No active-unit / deck state yet: those belong to the P0 simulation.
    public sealed class MatchRoster
    {
        public IReadOnlyList<ControllerSlot> Controllers { get; }
        public IReadOnlyList<CommanderSlot> Commanders { get; }

        // -1 is the full-BOT test mode; 0 is the initial offline human slot.
        public MatchRoster(int humanSlotId = 0)
        {
            if (humanSlotId < -1 || humanSlotId >= MatchLimits.TotalControllerSlots)
                throw new ArgumentOutOfRangeException(nameof(humanSlotId));
            var slots = new ControllerSlot[MatchLimits.TotalControllerSlots];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new ControllerSlot(i, i / MatchLimits.ControllerSlotsPerTeam,
                    i == humanSlotId ? ControllerType.Human : ControllerType.Bot);
            var commanders = new CommanderSlot[MatchLimits.TeamCount];
            for (int team = 0; team < commanders.Length; team++)
                commanders[team] = new CommanderSlot(team, ControllerType.Bot);
            Controllers = Array.AsReadOnly(slots);
            Commanders = Array.AsReadOnly(commanders);
        }
    }

    // Shared input contract. No controller-specific combat path or automatic counter.
    public readonly struct UnitCommand
    {
        public readonly int UnitId;
        public readonly long Tick;
        public readonly uint Sequence;
        public readonly float MoveX, MoveY, AimYaw, AimPitch;
        public readonly bool Boost, FirePrimary, FireSecondary, Melee, UseAbility;
        public UnitCommand(int unitId, long tick, uint sequence, float moveX, float moveY,
            float aimYaw, float aimPitch, bool boost, bool firePrimary,
            bool fireSecondary, bool melee, bool useAbility)
        {
            UnitId = unitId; Tick = tick; Sequence = sequence;
            MoveX = moveX; MoveY = moveY; AimYaw = aimYaw; AimPitch = aimPitch;
            Boost = boost; FirePrimary = firePrimary; FireSecondary = fireSecondary;
            Melee = melee; UseAbility = useAbility;
        }
    }

    public interface IUnitCommandSource
    {
        bool TryGetCommand(int slotId, long tick, out UnitCommand command);
    }
}
