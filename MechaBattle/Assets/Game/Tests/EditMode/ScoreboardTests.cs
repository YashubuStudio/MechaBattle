using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;

namespace MechaBattle.Tests
{
    // User request 2026-10-16: scoreboard (per-slot kill/death/damage tracking), presentation
    // only - never read by AI.Bots/Observation.
    public sealed class ScoreboardTests
    {
        // Gun()'s default kind=Ranged weapon is a projectile (projectileSpeed=12), not a
        // hitscan - at 16m apart it needs real travel time to land, not just the firing tick.
        // AutoRespawn defaults to true and the respawn delay is only ~2 ticks, so blindly
        // waiting a fixed tick count risks observing the unit *after* it already respawned -
        // step tick-by-tick and stop at the exact moment the state we're checking for occurs.
        private const int MaxTicks = 150;
        private static void StepUntilDead(BattleSimulation w, int slot)
        { for (int i = 0; i < MaxTicks && w.GetUnit(slot).IsAlive; i++) w.Step(); Assert.That(w.GetUnit(slot).IsAlive, Is.False, "Target never died within the tick budget."); }
        private static void StepUntilAlive(BattleSimulation w, int slot)
        { for (int i = 0; i < MaxTicks && !w.GetUnit(slot).IsAlive; i++) w.Step(); Assert.That(w.GetUnit(slot).IsAlive, Is.True, "Target never respawned within the tick budget."); }

        [Test] public void DamageDealtAccumulatesForTheAttackerWithoutKilling()
        {
            var w = FieldFixture.World(frame: FieldFixture.Gun(damage: 20));
            FieldFixture.Cmd(w, 50, fire: true, yaw: -90); w.Step(); FieldFixture.Wait(w, 90);
            var attacker = w.GetSlotStats(50); var victim = w.GetSlotStats(0);
            Assert.That(attacker.DamageDealt, Is.EqualTo(20).Within(0.01)); Assert.That(attacker.Kills, Is.Zero);
            Assert.That(victim.Deaths, Is.Zero);
        }
        [Test] public void KillIncrementsAttackerKillsAndVictimDeaths()
        {
            var w = FieldFixture.World(frame: FieldFixture.Gun(damage: 600));
            FieldFixture.Cmd(w, 50, fire: true, yaw: -90); w.Step(); StepUntilDead(w, 0);
            var attacker = w.GetSlotStats(50); var victim = w.GetSlotStats(0);
            Assert.That(attacker.Kills, Is.EqualTo(1));
            // Overkill is capped to the target's actual (shield 0 + health 500) pool, not the
            // raw 600 damage value - DamageDealt correctly reflects effective, not nominal, damage.
            Assert.That(attacker.DamageDealt, Is.EqualTo(500).Within(0.01));
            Assert.That(victim.Deaths, Is.EqualTo(1));
        }
        [Test] public void StatsSurviveRespawn()
        {
            // Not re-testing a second kill here: after respawn the victim returns to its
            // natural team spawn, no longer on the fixture's artificial firing line, so a
            // second shot would be a positioning-fragile assertion, not a scoreboard one.
            var w = FieldFixture.World(frame: FieldFixture.Gun(damage: 600));
            FieldFixture.Cmd(w, 50, fire: true, yaw: -90); w.Step(); StepUntilDead(w, 0);
            Assert.That(w.GetSlotStats(0).Deaths, Is.EqualTo(1)); Assert.That(w.GetSlotStats(50).Kills, Is.EqualTo(1));
            StepUntilAlive(w, 0);
            Assert.That(w.GetSlotStats(0).Deaths, Is.EqualTo(1), "Respawning must not reset lifetime scoreboard stats.");
            Assert.That(w.GetSlotStats(50).Kills, Is.EqualTo(1));
        }
        [Test] public void SlotStatsReportsTeamAndController()
        {
            var w = FieldFixture.World();
            var stats = w.GetSlotStats(0); var unit = w.GetUnit(0);
            Assert.That(stats.Team, Is.EqualTo(unit.Team)); Assert.That(stats.Controller, Is.EqualTo(unit.Controller));
            Assert.That(stats.Slot, Is.EqualTo(0)); Assert.That(stats.Kills, Is.Zero); Assert.That(stats.Deaths, Is.Zero);
            Assert.That(stats.DamageDealt, Is.Zero); Assert.That(stats.CoreDamageDealt, Is.Zero);
        }
    }
}
