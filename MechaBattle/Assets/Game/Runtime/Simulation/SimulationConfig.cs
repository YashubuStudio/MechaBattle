using System;

namespace MechaBattle.Simulation
{
    [Serializable]
    public sealed class SimulationConfig
    {
        // Load from prototype-defaults.json/p0Simulation; no hidden balance defaults.
        public int tickHz, maxAmmo, capturePresenceCap;
        public MechaBattle.Contracts.BotBehaviorRules botBehavior = new MechaBattle.Contracts.BotBehaviorRules();
        public float maxDurationSeconds, initialGauge, maxHealth, maxShield;
        public float moveSpeed, unitRadius, fireDamage, fireRange, fireIntervalSeconds;
        public float redLockRange, botToHumanRedLockMultiplier;
        public int[] deckCosts;
        public float[] deckCooldownSeconds;
        public float respawnDelaySeconds, unitGaugeLossPerCost, baseGaugeDamageFactor;
        public bool mainBaseDurabilityEnabled,baseCollisionEnabled;
        public float mainBaseHealth,mainBaseDestructionGaugeLoss;
        public float captureSecondsPerAdvantage, homeSupplyRadius;
        public float homeHealthPerSecond, homeShieldPerSecond, homeAmmoPerSecond;
        public float spawnScatterFraction;

        // Main-base defensive fire: a static structure's own weapon, not a per-unit
        // melee auto-counter (AGENTS.md's prohibition targets the latter only). Zero on
        // all three means disabled, preserving old no-base-fire behaviour by default.
        public float baseCounterFireDownPoints;
        public float baseCounterFireDamage, baseCounterFireIntervalSeconds, baseCounterFireRadius;

        // Optional shared capabilities. All zeros preserve the P0 regression mode.
        public bool crouchEnabled,botStuckRecoveryEnabled;
        public float crouchBoostRecoveryMultiplier=2,botStuckWatchSeconds=2,botStuckMinimumProgress=.3f,botStuckAvoidSeconds=1.2f;
        public int botStuckAvoidAttempts=2;
        public float boostCapacity, boostDrainPerSecond, boostRecoveryPerSecond, boostSpeedMultiplier;
        // The human player's own boost pool only (user request 2026-10-16); BOTs always use
        // boostCapacity unscaled. 0 means "not configured", applied as 1 (no change) so this
        // is backward compatible with every existing config that doesn't set it.
        public float humanBoostCapacityMultiplier;
        public bool boostOverheatEnabled, seamlessMeleeEnabled;
        public float boostOverheatRecoveryPerSecond;
        public float meleeHitRecoveryMultiplier, meleeHitBoostRefundFraction; // 0 keeps legacy hit recovery unchanged.
        public float meleeDamage, meleeRange, meleeIntervalSeconds, meleeHalfAngleDegrees;
        // User request 2026-10-16: a staggered/knocked-down unit may still throw a plain
        // MeleeStyle.Normal swing (only that style - Combo/Dash/DashCombo/Charge stay locked
        // out). Default false preserves the existing all-actions-locked behaviour.
        public bool normalMeleeBypassesDisabledEnabled;
        public bool traversalEnabled, redLockAssistEnabled, equipmentRulesEnabled;
        public float gravity, jumpSpeed, jumpBoostCost, airControl, maximumStepHeight, jumpHoldGravityMultiplier, jumpHoldDrainPerSecond;
        // Optional AIR-15 motor. Disabled preserves the original ballistic jump/air movement.
        public bool aerialBoostEnabled, botAerialManeuversEnabled;
        public float aerialAscentSpeed, aerialAscentAcceleration, aerialMaximumRise, aerialGlideFallSpeed, aerialControlAcceleration;
        public float botAerialBurstSeconds, botAerialIntervalSeconds;
        public bool botLineCombatEnabled, botCentralApproachEnabled;
        public float botPreferredRangeFraction, botStrafeFraction, botRetreatHealthFraction;
        public int botAssaultModulo;
        public float botProjectileLeadFraction; // 0 preserves legacy full lead.
        public float botFlankOuterOffset, botFlankIngressDistance;
        public bool botLockRetreatEnabled, botLockEvadeEnabled;
        public float botLockEvadeStrafeFraction, botLockEvadePeriodSeconds;
        public float botRecoveryHealthFraction, botRecoveryShieldFraction, botRecoveryQuietSeconds, botCoverSearchRadius;
        public bool botPerceptionEnabled;
        public float botVisualRange, botVisualDegrees, botVerticalVisualDegrees, botScanSeconds, botReactionSeconds, botReactionJitterSeconds, botViewTurnDegreesPerSecond;
        public MechaBattle.Contracts.StrategyRules strategy = new MechaBattle.Contracts.StrategyRules();
        public bool emergencyReturnEnabled;
        public float emergencyReturnSeconds, returnHealthPerSecond, returnShieldPerSecond, returnAmmoPerSecond;
        public bool weaponSpreadEnabled;
        public float sharedLockHealthSeconds=1.7f, radarScanSeconds=0.5f, radarContactSeconds=0.75f;
        public bool aimIn3DEnabled;
        public float unitHeight, shotOriginHeight, coreHeight;
        public bool directionalShieldEnabled;
        public float shieldHalfAngleDegrees, shieldRecoverySeconds;
        public float downPointRecoverySeconds, staggerSeconds, knockdownSeconds, knockdownThresholdMultiplier;
        public MechaBattle.Contracts.MeleeAttackDefinition[] meleeAttacks;
        // User request 2026-10-16: melee against a target on a ledge/platform felt like it
        // never connects because the old height gate shrank with range (range*0.75). 0 keeps
        // that legacy formula; a positive value replaces it with a flat vertical tolerance
        // independent of the attack's range.
        public float meleeVerticalToleranceMeters;

        public void Validate()
        {
            botBehavior?.Validate();
            if(mainBaseDurabilityEnabled && (!IsFinite(mainBaseHealth)||mainBaseHealth<=0||!IsFinite(mainBaseDestructionGaugeLoss)||mainBaseDestructionGaugeLoss<=0))throw new ArgumentException("Invalid main base durability settings.");
            if(baseCollisionEnabled && (!traversalEnabled||!aimIn3DEnabled))throw new ArgumentException("Base collision requires 3D traversal.");
            if(strategy!=null)strategy.Validate();
            if(emergencyReturnEnabled)
            {
                foreach(float v in new[]{emergencyReturnSeconds,returnHealthPerSecond,returnShieldPerSecond,returnAmmoPerSecond})if(!IsFinite(v)||v<=0||v>3600)throw new ArgumentException("Invalid emergency return settings.");
                if(returnHealthPerSecond<=homeHealthPerSecond||returnShieldPerSecond<=homeShieldPerSecond||returnAmmoPerSecond<=homeAmmoPerSecond)throw new ArgumentException("Return repair must be faster than base supply.");
            }
            if(botPerceptionEnabled)
            {
                foreach(float v in new[]{botVisualRange,botVisualDegrees,botVerticalVisualDegrees,botScanSeconds,botReactionSeconds,botViewTurnDegreesPerSecond})
                    if(!IsFinite(v)||v<=0)throw new ArgumentException("Invalid BOT perception configuration.");
                if(botVisualRange>1000 || botVisualDegrees>180 || botVerticalVisualDegrees>160 || botScanSeconds>2 || botReactionSeconds>5 || !IsFinite(botReactionJitterSeconds) || botReactionJitterSeconds<0 || botReactionJitterSeconds>3)
                    throw new ArgumentException("Invalid BOT perception limits.");
            }
            if (!IsFinite(meleeHitRecoveryMultiplier) || meleeHitRecoveryMultiplier < 0 || meleeHitRecoveryMultiplier > 1
                || !IsFinite(meleeHitBoostRefundFraction) || meleeHitBoostRefundFraction < 0 || meleeHitBoostRefundFraction > 1)
                throw new ArgumentException("Invalid confirmed melee hit modifiers.");
            foreach(float v in new[]{sharedLockHealthSeconds,radarScanSeconds,radarContactSeconds})
                if(!IsFinite(v) || v<=0 || v>60)throw new ArgumentException("Invalid shared detection timing.");
            if(radarContactSeconds<radarScanSeconds)throw new ArgumentException("Radar contact must cover the scan interval.");
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
            if (botLineCombatEnabled && (!IsFinite(botPreferredRangeFraction) || botPreferredRangeFraction <= 0 || botPreferredRangeFraction > 1
                || !IsFinite(botStrafeFraction) || botStrafeFraction < 0 || botStrafeFraction > 1
                || !IsFinite(botRetreatHealthFraction) || botRetreatHealthFraction <= 0 || botRetreatHealthFraction >= 1 || botAssaultModulo < 2))
                throw new ArgumentException("Invalid BOT line-combat configuration.");
            if ((botFlankOuterOffset!=0 || botFlankIngressDistance!=0) && (!IsFinite(botFlankOuterOffset) || botFlankOuterOffset<=0 || !IsFinite(botFlankIngressDistance) || botFlankIngressDistance<=0))
                throw new ArgumentException("Invalid outer flank route configuration.");
            if(!IsFinite(botProjectileLeadFraction) || botProjectileLeadFraction<0 || botProjectileLeadFraction>1)throw new ArgumentException("Invalid BOT lead fraction.");
            if(botLockEvadeEnabled && (!IsFinite(botLockEvadeStrafeFraction) || botLockEvadeStrafeFraction<=0 || botLockEvadeStrafeFraction>1 || !IsFinite(botLockEvadePeriodSeconds) || botLockEvadePeriodSeconds<=0))
                throw new ArgumentException("Invalid BOT lock-evasion configuration.");
            if (botLockRetreatEnabled && (!botLineCombatEnabled
                || !IsFinite(botRecoveryHealthFraction) || botRecoveryHealthFraction <= botRetreatHealthFraction || botRecoveryHealthFraction > 1
                || !IsFinite(botRecoveryShieldFraction) || botRecoveryShieldFraction < 0 || botRecoveryShieldFraction > 1
                || !IsFinite(botRecoveryQuietSeconds) || botRecoveryQuietSeconds <= 0
                || !IsFinite(botCoverSearchRadius) || botCoverSearchRadius <= 0))
                throw new ArgumentException("Invalid BOT lock-retreat configuration.");
            if (aerialBoostEnabled)
            {
                if (!traversalEnabled || boostCapacity <= 0 || jumpHoldDrainPerSecond <= 0) throw new ArgumentException("Aerial boost requires traversal and finite boost fuel.");
                foreach (float v in new[] { aerialAscentSpeed, aerialAscentAcceleration, aerialMaximumRise, aerialGlideFallSpeed, aerialControlAcceleration })
                    if (!IsFinite(v) || v <= 0) throw new ArgumentException("Invalid aerial motor configuration.");
            }
            if (botAerialManeuversEnabled && (!aerialBoostEnabled || !botLineCombatEnabled
                || !IsFinite(botAerialBurstSeconds) || botAerialBurstSeconds <= 0
                || !IsFinite(botAerialIntervalSeconds) || botAerialIntervalSeconds <= botAerialBurstSeconds))
                throw new ArgumentException("Invalid tactical aerial cadence.");
            if (traversalEnabled)
            {
                if ((jumpHoldGravityMultiplier != 0 || jumpHoldDrainPerSecond != 0) && (!IsFinite(jumpHoldGravityMultiplier) || jumpHoldGravityMultiplier <= 0 || jumpHoldGravityMultiplier > 1 || !IsFinite(jumpHoldDrainPerSecond) || jumpHoldDrainPerSecond <= 0)) throw new ArgumentException("Invalid sustained jump.");
                foreach (float v in new[] { gravity, jumpSpeed, airControl }) if (!IsFinite(v) || v <= 0) throw new ArgumentException("Invalid traversal configuration.");
                if (!aimIn3DEnabled || airControl > 1 || !IsFinite(jumpBoostCost) || jumpBoostCost < 0 || !IsFinite(maximumStepHeight) || maximumStepHeight < 0 || maximumStepHeight > 1) throw new ArgumentException("Invalid traversal limits.");
            }
            if (aimIn3DEnabled && (!IsFinite(unitHeight) || !IsFinite(shotOriginHeight) || !IsFinite(coreHeight) || shotOriginHeight <= 0 || unitHeight <= shotOriginHeight || coreHeight <= 0))
                throw new ArgumentException("Invalid 3D aim geometry.");
            if (directionalShieldEnabled && (!IsFinite(shieldHalfAngleDegrees) || shieldHalfAngleDegrees <= 0 || shieldHalfAngleDegrees > 180
                || !IsFinite(shieldRecoverySeconds) || shieldRecoverySeconds <= 0)) throw new ArgumentException("Invalid frontal shield.");
            if (downPointRecoverySeconds != 0 || staggerSeconds != 0 || knockdownSeconds != 0 || knockdownThresholdMultiplier != 0)
            {
                foreach (float v in new[] { downPointRecoverySeconds, staggerSeconds, knockdownSeconds, knockdownThresholdMultiplier })
                    if (!IsFinite(v) || v <= 0) throw new ArgumentException("Invalid disruption configuration.");
                if (knockdownThresholdMultiplier <= 1) throw new ArgumentException("Knockdown must follow stagger.");
            }
            if (meleeVerticalToleranceMeters != 0 && (!IsFinite(meleeVerticalToleranceMeters) || meleeVerticalToleranceMeters <= 0))
                throw new ArgumentException("meleeVerticalToleranceMeters must be finite and positive when set.");
            if (normalMeleeBypassesDisabledEnabled && (meleeAttacks == null || meleeAttacks.Length != 5))
                throw new ArgumentException("normalMeleeBypassesDisabledEnabled requires the five melee styles to be configured.");
            if (meleeAttacks != null && meleeAttacks.Length > 0)
            {
                if (meleeAttacks.Length != 5) throw new ArgumentException("All five melee styles are required.");
                var seen = new bool[5];
                foreach (var attack in meleeAttacks) { if (attack == null) throw new ArgumentException("Missing melee definition."); attack.Validate();
                    if (seen[(int)attack.style]) throw new ArgumentException("Duplicate melee style."); seen[(int)attack.style] = true; }
            }
            if(boostOverheatEnabled && (!IsFinite(boostOverheatRecoveryPerSecond) || boostOverheatRecoveryPerSecond<=0 || boostCapacity<=0))throw new ArgumentException("Invalid overheat recovery.");
            if(crouchEnabled && (!IsFinite(crouchBoostRecoveryMultiplier)||crouchBoostRecoveryMultiplier<1||crouchBoostRecoveryMultiplier>10))throw new ArgumentException("Invalid crouch recovery multiplier.");
            if(botStuckRecoveryEnabled && (!Positive(botStuckWatchSeconds)||botStuckWatchSeconds>60||!Positive(botStuckMinimumProgress)||botStuckMinimumProgress>2||!Positive(botStuckAvoidSeconds)||botStuckAvoidSeconds>10||botStuckAvoidAttempts<1||botStuckAvoidAttempts>8))throw new ArgumentException("Invalid BOT stuck-recovery rules.");
            bool boostConfigured = boostCapacity != 0 || boostDrainPerSecond != 0 || boostRecoveryPerSecond != 0 || boostSpeedMultiplier != 0;
            if (boostConfigured && (!IsFinite(boostCapacity) || boostCapacity <= 0 || !IsFinite(boostDrainPerSecond) || boostDrainPerSecond <= 0
                || !IsFinite(boostRecoveryPerSecond) || boostRecoveryPerSecond <= 0 || !IsFinite(boostSpeedMultiplier) || boostSpeedMultiplier <= 1))
                throw new ArgumentException("Configure all boost values with a multiplier greater than one.");
            if (humanBoostCapacityMultiplier != 0 && (!IsFinite(humanBoostCapacityMultiplier) || humanBoostCapacityMultiplier < 1))
                throw new ArgumentException("humanBoostCapacityMultiplier must be at least one when set.");
            bool meleeConfigured = meleeDamage != 0 || meleeRange != 0 || meleeIntervalSeconds != 0 || meleeHalfAngleDegrees != 0;
            if (meleeConfigured && (!IsFinite(meleeDamage) || meleeDamage <= 0 || !IsFinite(meleeRange) || meleeRange <= 0
                || !IsFinite(meleeIntervalSeconds) || meleeIntervalSeconds <= 0 || !IsFinite(meleeHalfAngleDegrees) || meleeHalfAngleDegrees <= 0 || meleeHalfAngleDegrees > 180))
                throw new ArgumentException("Invalid optional melee configuration.");
            if (meleeConfigured && (double)meleeIntervalSeconds * tickHz > long.MaxValue / 4d)
                throw new ArgumentException("Melee interval exceeds tick capacity.");
            if (!IsFinite(baseCounterFireDownPoints) || baseCounterFireDownPoints < 0) throw new ArgumentException("Invalid base defensive DP.");
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
            if (meleeAttacks != null) { copy.meleeAttacks = new MechaBattle.Contracts.MeleeAttackDefinition[meleeAttacks.Length];
                for (int i = 0; i < meleeAttacks.Length; i++) copy.meleeAttacks[i] = meleeAttacks[i].Copy(); }
            copy.strategy = strategy?.Copy();
            copy.botBehavior = botBehavior?.Copy();
            copy.deckCosts = (int[])deckCosts.Clone();
            copy.deckCooldownSeconds = (float[])deckCooldownSeconds.Clone();
            return copy;
        }
        private static bool Positive(float v)=>IsFinite(v)&&v>0;
        internal static bool IsFinite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    }
}
