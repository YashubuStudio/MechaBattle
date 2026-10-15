using System;

namespace MechaBattle.Simulation
{
    [Serializable]
    public sealed class SimulationConfig
    {
        // Load from prototype-defaults.json/p0Simulation; no hidden balance defaults.
        public int tickHz, maxAmmo, capturePresenceCap;
        public float maxDurationSeconds, initialGauge, maxHealth, maxShield;
        public float moveSpeed, unitRadius, fireDamage, fireRange, fireIntervalSeconds;
        public float redLockRange, botToHumanRedLockMultiplier;
        public int[] deckCosts;
        public float[] deckCooldownSeconds;
        public float respawnDelaySeconds, unitGaugeLossPerCost, baseGaugeDamageFactor;
        public float captureSecondsPerAdvantage, homeSupplyRadius;
        public float homeHealthPerSecond, homeShieldPerSecond, homeAmmoPerSecond;
        public float spawnScatterFraction;

        // Main-base defensive fire: a static structure's own weapon, not a per-unit
        // melee auto-counter (AGENTS.md's prohibition targets the latter only). Zero on
        // all three means disabled, preserving old no-base-fire behaviour by default.
        public float baseCounterFireDamage, baseCounterFireIntervalSeconds, baseCounterFireRadius;

        public void Validate()
        {
            if (tickHz < 1 || tickHz > 240 || maxAmmo < 1 || capturePresenceCap < 1)
                throw new ArgumentException("Invalid tick, ammo, or capture capacity.");
            foreach (float value in new[] { maxDurationSeconds, initialGauge, maxHealth,
                moveSpeed, unitRadius, fireDamage, fireRange, fireIntervalSeconds, redLockRange,
                botToHumanRedLockMultiplier, respawnDelaySeconds, unitGaugeLossPerCost,
                baseGaugeDamageFactor, captureSecondsPerAdvantage, homeSupplyRadius,
                homeHealthPerSecond, homeShieldPerSecond, homeAmmoPerSecond, spawnScatterFraction })
                if (!IsFinite(value) || value <= 0) throw new ArgumentException("P0 config values must be finite and positive.");
            if (!IsFinite(maxShield) || maxShield < 0 || spawnScatterFraction > 1 || botToHumanRedLockMultiplier > 1)
                throw new ArgumentException("Invalid shield, spawn scatter, or BOT red-lock multiplier.");
            bool baseCounterFireConfigured = baseCounterFireDamage != 0 || baseCounterFireIntervalSeconds != 0 || baseCounterFireRadius != 0;
            if (baseCounterFireConfigured && (
                !IsFinite(baseCounterFireDamage) || baseCounterFireDamage <= 0 ||
                !IsFinite(baseCounterFireIntervalSeconds) || baseCounterFireIntervalSeconds <= 0 ||
                !IsFinite(baseCounterFireRadius) || baseCounterFireRadius <= 0))
                throw new ArgumentException("Base counter-fire damage/interval/radius must all be positive and finite when any is set.");
            if (deckCosts == null || deckCooldownSeconds == null || deckCosts.Length < 2 || deckCosts.Length != deckCooldownSeconds.Length)
                throw new ArgumentException("At least two deck entries with matching cooldowns are required.");
            for (int i = 0; i < deckCosts.Length; i++)
                if (deckCosts[i] <= 0 || !IsFinite(deckCooldownSeconds[i]) || deckCooldownSeconds[i] <= 0)
                    throw new ArgumentException("Deck costs and cooldowns must be positive.");
            if ((double)maxDurationSeconds * tickHz > long.MaxValue / 4d)
                throw new ArgumentException("Match duration exceeds tick capacity.");
            foreach (float seconds in deckCooldownSeconds)
                if ((double)seconds * tickHz > long.MaxValue / 4d) throw new ArgumentException("Cooldown exceeds tick capacity.");
            if ((double)fireIntervalSeconds * tickHz > long.MaxValue / 4d)
                throw new ArgumentException("Fire interval exceeds tick capacity.");
            if ((double)respawnDelaySeconds * tickHz > long.MaxValue / 4d)
                throw new ArgumentException("Respawn delay exceeds tick capacity.");
            if (baseCounterFireConfigured && (double)baseCounterFireIntervalSeconds * tickHz > long.MaxValue / 4d)
                throw new ArgumentException("Base counter-fire interval exceeds tick capacity.");
        }

        internal SimulationConfig Copy()
        {
            var copy = (SimulationConfig)MemberwiseClone();
            copy.deckCosts = (int[])deckCosts.Clone();
            copy.deckCooldownSeconds = (float[])deckCooldownSeconds.Clone();
            return copy;
        }
        internal static bool IsFinite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    }
}
