using System;
using MechaBattle.Simulation;
using MechaBattle.Contracts.Construction;
namespace MechaBattle.Playable
{
    [Serializable] public sealed class PlayableConfiguration
    {
        public SimulationConfig p0Simulation = new SimulationConfig();
        public ConstructionPrototype constructionPrototype = new ConstructionPrototype();
        public PlayablePrototype playablePrototype = new PlayablePrototype();
    }
    [Serializable] public sealed class ConstructionPrototype { public ConstructionCatalog catalog = new ConstructionCatalog(); }
    [Serializable] public sealed class PlayablePrototype
    {
        public float boostCapacity, boostDrainPerSecond, boostRecoveryPerSecond, boostSpeedMultiplier;
        public float meleeDamage, meleeRange, meleeIntervalSeconds, meleeHalfAngleDegrees;
        public float cameraDistance, cameraHeight, mouseSensitivity, aimAssistDegrees, uiRefreshHz;
        public int profileSchemaVersion, starterDeckSlots;
        public void Apply(SimulationConfig target)
        {
            if (starterDeckSlots != target.deckCosts.Length || starterDeckSlots != 4 || profileSchemaVersion != 1
                || !Positive(cameraDistance) || !Positive(cameraHeight) || !Positive(mouseSensitivity) || !Positive(uiRefreshHz) || !Positive(aimAssistDegrees))
                throw new ArgumentException("Invalid playable configuration.");
            target.boostCapacity = boostCapacity; target.boostDrainPerSecond = boostDrainPerSecond;
            target.boostRecoveryPerSecond = boostRecoveryPerSecond; target.boostSpeedMultiplier = boostSpeedMultiplier;
            target.meleeDamage = meleeDamage; target.meleeRange = meleeRange;
            target.meleeIntervalSeconds = meleeIntervalSeconds; target.meleeHalfAngleDegrees = meleeHalfAngleDegrees;
            target.Validate();
        }
        private static bool Positive(float v) { return !float.IsNaN(v) && !float.IsInfinity(v) && v > 0; }
    }
}
