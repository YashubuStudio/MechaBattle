using System;
using NUnit.Framework;
using MechaBattle.Simulation;

namespace MechaBattle.Tests
{
    // User request 2026-10-16: melee against a target on a ledge/platform felt like it never
    // connects, because the old height gate (range*0.75) shrinks with range. Covers the
    // config plumbing; the actual in-simulation height-gate behavior on real jump/terrain
    // arcs is not exercised here (would need a PlayMode/jump-timing fixture) and should be
    // confirmed in Unity.
    public sealed class MeleeVerticalToleranceTests
    {
        [Test] public void DefaultZeroPreservesLegacyBehaviourAndIsAccepted()
        { var c = SimulationTests.Config(); c.meleeVerticalToleranceMeters = 0; Assert.DoesNotThrow(() => c.Validate()); }

        [Test] public void PositiveValueIsAccepted()
        { var c = SimulationTests.Config(); c.meleeVerticalToleranceMeters = 4.0f; Assert.DoesNotThrow(() => c.Validate()); }

        [Test] public void NegativeValueIsRejected()
        { var c = SimulationTests.Config(); c.meleeVerticalToleranceMeters = -1f; Assert.Throws<ArgumentException>(() => c.Validate()); }
    }
}
