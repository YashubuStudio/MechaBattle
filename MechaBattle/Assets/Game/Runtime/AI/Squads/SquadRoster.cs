using System;
using MechaBattle.Contracts;

namespace MechaBattle.AI.Squads
{
    // Phase 1 of SYSTEM_DESIGN.md §10.2 Squad AI: grouping only. No shared decision-making,
    // role assignment, or operation presets yet - those are later phases (see
    // docs/WORK_INVENTORY.md / docs/BRUSHUP_AND_ROADMAP.md). Individual unit command sources
    // are unaffected; this is pure data describing which controller slots belong together.
    // Grouping is purely by slot index within a team and does not yet distinguish the
    // human-controlled slot from BOT slots (that distinction lives in MatchRoster /
    // ForwardMarchBotCommandSource's own slot filtering, not here) - nothing in this
    // assembly issues commands to anyone, so a human slot ending up in a squad is
    // currently harmless, but any future caller that commands squad members must apply
    // its own human/BOT filter before acting on SquadDefinition data.
    public readonly struct SquadDefinition
    {
        public readonly int SquadId, Team;
        // IReadOnlyList<int> (not int[]): an array field would still let callers mutate
        // membership through the public reference after construction even with a defensive
        // copy at the constructor (GPT follow-up review 2026-10-16: squad.MemberSlots[0]=99
        // compiled and worked against the array-typed field). IReadOnlyList<int>'s indexer is
        // get-only, so that assignment is now a compile error, not just a runtime risk.
        public readonly System.Collections.Generic.IReadOnlyList<int> MemberSlots;
        public SquadDefinition(int squadId, int team, int[] memberSlots)
        {
            if (squadId < 0) throw new ArgumentOutOfRangeException(nameof(squadId));
            if (team < 0 || team >= MatchLimits.TeamCount) throw new ArgumentOutOfRangeException(nameof(team));
            if (memberSlots == null || memberSlots.Length == 0) throw new ArgumentException("Squad requires at least one member.");
            // Defensive copy: the caller's own array must not be able to mutate this squad's
            // membership after construction either (GPT review 2026-10-16).
            var copy = new int[memberSlots.Length];
            Array.Copy(memberSlots, copy, memberSlots.Length);
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var slot in copy)
            {
                if (slot < 0 || slot >= MatchLimits.TotalControllerSlots || slot / MatchLimits.ControllerSlotsPerTeam != team)
                    throw new ArgumentException("Squad member slot must belong to the squad's own team.");
                if (!seen.Add(slot)) throw new ArgumentException("Squad member slot listed more than once: " + slot);
            }
            SquadId = squadId; Team = team; MemberSlots = Array.AsReadOnly(copy);
        }
    }

    public static class SquadRoster
    {
        // Deterministic, index-based assignment - same spirit as the existing lane/home-base
        // round robins in ForwardMarchBotCommandSource. The first squadsPerTeam*squadSize local
        // slots of each team are split into fixed-size squads; the remainder are unassigned
        // "general" BOTs per FR-701. No randomness, no per-match variation.
        public static SquadDefinition[] BuildStandardSquads(int squadsPerTeam, int squadSize)
        {
            if (squadsPerTeam < 0) throw new ArgumentOutOfRangeException(nameof(squadsPerTeam));
            if (squadSize <= 0) throw new ArgumentOutOfRangeException(nameof(squadSize));
            // Widen to long before multiplying (GPT review 2026-10-16): squadsPerTeam*squadSize
            // as a plain int product can overflow and wrap to a small/negative value for large
            // inputs (e.g. int.MaxValue * 2), silently passing the slot-budget check below.
            if ((long)squadsPerTeam * squadSize > MatchLimits.ControllerSlotsPerTeam)
                throw new ArgumentException("Requested squads exceed the controller slots available per team.");
            var squads = new SquadDefinition[squadsPerTeam * MatchLimits.TeamCount];
            int next = 0;
            for (int team = 0; team < MatchLimits.TeamCount; team++)
            {
                int baseSlot = team * MatchLimits.ControllerSlotsPerTeam;
                for (int squad = 0; squad < squadsPerTeam; squad++)
                {
                    var members = new int[squadSize];
                    for (int i = 0; i < squadSize; i++) members[i] = baseSlot + squad * squadSize + i;
                    squads[next++] = new SquadDefinition(squad, team, members);
                }
            }
            return squads;
        }

        // -1 when the slot (BOT or human - this phase does not distinguish them, see the
        // SquadDefinition remarks above) is not assigned to any squad, i.e. a general BOT
        // per FR-701 or simply outside the squadsPerTeam*squadSize range requested.
        public static int FindSquadId(SquadDefinition[] squads, int slot)
        {
            if (squads == null) throw new ArgumentNullException(nameof(squads));
            foreach (var squad in squads)
                foreach (var member in squad.MemberSlots)
                    if (member == slot) return squad.SquadId;
            return -1;
        }
    }
}
