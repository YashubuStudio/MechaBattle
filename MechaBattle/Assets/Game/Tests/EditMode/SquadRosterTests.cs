using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.AI.Squads;

namespace MechaBattle.Tests
{
    public sealed class SquadRosterTests
    {
        [Test]
        public void BuildStandardSquads_GroupsFirstSlotsPerTeamDeterministically()
        {
            var squads = SquadRoster.BuildStandardSquads(squadsPerTeam: 2, squadSize: 4);
            Assert.That(squads.Length, Is.EqualTo(4)); // 2 squads * 2 teams
            foreach (var squad in squads)
            {
                Assert.That(squad.MemberSlots.Count, Is.EqualTo(4));
                foreach (var slot in squad.MemberSlots)
                    Assert.That(slot / MatchLimits.ControllerSlotsPerTeam, Is.EqualTo(squad.Team));
            }
            // Blue squad 0 is the first four slots of team 0; Orange squad 1 is slots 4-7 of team 1.
            Assert.That(squads[0].MemberSlots, Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(squads[3].MemberSlots, Is.EqualTo(new[] { MatchLimits.ControllerSlotsPerTeam + 4, MatchLimits.ControllerSlotsPerTeam + 5, MatchLimits.ControllerSlotsPerTeam + 6, MatchLimits.ControllerSlotsPerTeam + 7 }));
        }
        [Test]
        public void BuildStandardSquads_IsRepeatableAndNonOverlapping()
        {
            var first = SquadRoster.BuildStandardSquads(2, 4);
            var second = SquadRoster.BuildStandardSquads(2, 4);
            for (int i = 0; i < first.Length; i++)
                Assert.That(first[i].MemberSlots, Is.EqualTo(second[i].MemberSlots));
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var squad in first) foreach (var slot in squad.MemberSlots) Assert.That(seen.Add(slot), Is.True, "Slot assigned to more than one squad.");
        }
        [Test]
        public void FindSquadId_ReturnsSquadForMembersAndMinusOneForGeneralBots()
        {
            var squads = SquadRoster.BuildStandardSquads(2, 4);
            Assert.That(SquadRoster.FindSquadId(squads, 0), Is.EqualTo(0));
            Assert.That(SquadRoster.FindSquadId(squads, 5), Is.EqualTo(1));
            Assert.That(SquadRoster.FindSquadId(squads, 8), Is.EqualTo(-1)); // general BOT, not in a squad
            Assert.That(SquadRoster.FindSquadId(squads, MatchLimits.TotalControllerSlots - 1), Is.EqualTo(-1));
        }
        [Test]
        public void BuildStandardSquads_RejectsMoreSquadsThanSlotsAllow()
        {
            Assert.Throws<ArgumentException>(() => SquadRoster.BuildStandardSquads(squadsPerTeam: 20, squadSize: 4));
        }
        [Test]
        public void BuildStandardSquads_AllowsZeroSquadsPerTeam()
        {
            var squads = SquadRoster.BuildStandardSquads(0, 4);
            Assert.That(squads.Length, Is.Zero);
            Assert.That(SquadRoster.FindSquadId(squads, 0), Is.EqualTo(-1));
        }
        [Test]
        public void SquadDefinition_RejectsMemberFromAnotherTeam()
        {
            Assert.Throws<ArgumentException>(() => new MechaBattle.AI.Squads.SquadDefinition(0, 0, new[] { MatchLimits.ControllerSlotsPerTeam }));
        }

        [Test]
        public void BuildStandardSquads_RejectsOverflowingSlotBudgetWithoutWrapping()
        {
            // squadsPerTeam * squadSize overflows a 32-bit int and wraps to a small/negative
            // value (int.MaxValue * 2 == -2), which would silently pass a plain int
            // multiplication check. GPT review 2026-10-16.
            Assert.Throws<ArgumentException>(() => SquadRoster.BuildStandardSquads(squadsPerTeam: int.MaxValue, squadSize: 2));
        }

        [Test]
        public void SquadDefinition_CopiesMemberSlotsSoCallerCannotMutateAfterConstruction()
        {
            var members = new[] { 0, 1, 2, 3 };
            var squad = new MechaBattle.AI.Squads.SquadDefinition(0, 0, members);
            members[0] = 99;
            Assert.That(squad.MemberSlots[0], Is.EqualTo(0), "Mutating the caller's array after construction must not change the squad.");
        }

        [Test]
        public void SquadDefinition_MemberSlotsExposureIsGetOnly()
        {
            // GPT follow-up review 2026-10-16: the defensive copy alone did not stop callers
            // from mutating membership through the public reference itself (squad.MemberSlots[0]=99
            // compiled against an int[]-typed field). MemberSlots is now IReadOnlyList<int>, whose
            // indexer has no setter, so the same assignment is a compile error - this test's
            // existence (and the fact that it compiles without the assignment) is the proof;
            // IReadOnlyList<int> also cannot be cast back to int[] to bypass that.
            var squad = new MechaBattle.AI.Squads.SquadDefinition(0, 0, new[] { 0, 1, 2, 3 });
            Assert.That(squad.MemberSlots, Is.Not.InstanceOf<int[]>(), "Must not expose the backing array by its concrete mutable type.");
        }

        [Test]
        public void SquadDefinition_RejectsDuplicateMemberSlots()
        {
            Assert.Throws<ArgumentException>(() => new MechaBattle.AI.Squads.SquadDefinition(0, 0, new[] { 0, 1, 1 }));
        }
    }
}
