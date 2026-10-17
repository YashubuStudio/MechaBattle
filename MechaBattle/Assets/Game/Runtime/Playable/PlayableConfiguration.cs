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
        public MechaBattle.Contracts.BotBehaviorRules botBehavior = new MechaBattle.Contracts.BotBehaviorRules();
        public bool crouchEnabled,botStuckRecoveryEnabled;
        public float crouchBoostRecoveryMultiplier=2,botStuckWatchSeconds=2,botStuckMinimumProgress=.3f,botStuckAvoidSeconds=1.2f;
        public int botStuckAvoidAttempts=2;
        public float boostCapacity, boostDrainPerSecond, boostRecoveryPerSecond, boostSpeedMultiplier;
        public float humanBoostCapacityMultiplier;
        public bool boostOverheatEnabled,seamlessMeleeEnabled;public float boostOverheatRecoveryPerSecond;
        public float meleeDamage, meleeRange, meleeIntervalSeconds, meleeHalfAngleDegrees;
        public float meleeHitRecoveryMultiplier, meleeHitBoostRefundFraction;
        public bool normalMeleeBypassesDisabledEnabled;
        public bool traversalEnabled, redLockAssistEnabled, equipmentRulesEnabled;
        public float gravity, jumpSpeed, jumpBoostCost, airControl, maximumStepHeight, jumpHoldGravityMultiplier, jumpHoldDrainPerSecond, playerUnitRadius;
        public float lockAcquisitionDegrees, cameraHeadHeight, cameraCollisionRadius, firstPersonDistance;
        public bool aerialBoostEnabled, botAerialManeuversEnabled;
        public float aerialAscentSpeed, aerialAscentAcceleration, aerialMaximumRise, aerialGlideFallSpeed, aerialControlAcceleration;
        public float botAerialBurstSeconds, botAerialIntervalSeconds;
        public bool botLineCombatEnabled, botCentralApproachEnabled;
        public float botPreferredRangeFraction, botStrafeFraction, botRetreatHealthFraction, unitGaugeLossPerCost;
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
        public float sharedLockHealthSeconds=1.7f,radarScanSeconds=0.5f,radarContactSeconds=0.75f;
        public bool aimIn3DEnabled;
        public float unitHeight, shotOriginHeight, coreHeight;
        public bool directionalShieldEnabled;
        public float shieldHalfAngleDegrees, shieldRecoverySeconds, downPointRecoverySeconds, staggerSeconds, knockdownSeconds, knockdownThresholdMultiplier;
        public MechaBattle.Contracts.MeleeAttackDefinition[] meleeAttacks;
        public float meleeVerticalToleranceMeters;
        public float initialGauge, baseCounterFireDamage, baseCounterFireRadius, baseGaugeDamageFactor;
        public bool mainBaseDurabilityEnabled,baseCollisionEnabled;public float mainBaseHealth,mainBaseDestructionGaugeLoss;
        public float homeHealthPerSecond, homeShieldPerSecond;
        public float cameraDistance, cameraHeight, cameraShoulderOffset, mouseSensitivity, aimAssistDegrees, uiRefreshHz;
        public float settingsMinimumSensitivity = 0.01f, settingsMaximumSensitivity = 1f;
        public int profileSchemaVersion, starterDeckSlots;
        public void Apply(SimulationConfig target)
        {
            target.crouchEnabled=crouchEnabled;target.crouchBoostRecoveryMultiplier=crouchBoostRecoveryMultiplier;
            target.botStuckRecoveryEnabled=botStuckRecoveryEnabled;target.botStuckWatchSeconds=botStuckWatchSeconds;target.botStuckMinimumProgress=botStuckMinimumProgress;target.botStuckAvoidSeconds=botStuckAvoidSeconds;target.botStuckAvoidAttempts=botStuckAvoidAttempts;
            target.botBehavior=botBehavior?.Copy();
            if (starterDeckSlots != target.deckCosts.Length || starterDeckSlots != 4 || profileSchemaVersion != 1
                || !Positive(cameraDistance) || !Positive(cameraHeight) || !Positive(mouseSensitivity) || !Positive(uiRefreshHz) || !Positive(aimAssistDegrees)
                || !Positive(settingsMinimumSensitivity) || !Positive(settingsMaximumSensitivity) || settingsMaximumSensitivity <= settingsMinimumSensitivity)
                throw new ArgumentException("Invalid playable configuration.");
            if(homeHealthPerSecond>0)target.homeHealthPerSecond=homeHealthPerSecond;
            if(homeShieldPerSecond>0)target.homeShieldPerSecond=homeShieldPerSecond;
            if(baseCounterFireRadius>0)target.baseCounterFireRadius=baseCounterFireRadius;
            if(baseGaugeDamageFactor>0)target.baseGaugeDamageFactor=baseGaugeDamageFactor;
            target.mainBaseDurabilityEnabled=mainBaseDurabilityEnabled;target.baseCollisionEnabled=baseCollisionEnabled;target.mainBaseHealth=mainBaseHealth;target.mainBaseDestructionGaugeLoss=mainBaseDestructionGaugeLoss;
            if (initialGauge > 0) target.initialGauge=initialGauge; if(baseCounterFireDamage>0)target.baseCounterFireDamage=baseCounterFireDamage;
            target.botFlankOuterOffset=botFlankOuterOffset; target.botFlankIngressDistance=botFlankIngressDistance;
            target.botProjectileLeadFraction=botProjectileLeadFraction;
            target.botLockEvadeEnabled=botLockEvadeEnabled; target.botLockEvadeStrafeFraction=botLockEvadeStrafeFraction; target.botLockEvadePeriodSeconds=botLockEvadePeriodSeconds;
            target.botLockRetreatEnabled=botLockRetreatEnabled; target.botRecoveryHealthFraction=botRecoveryHealthFraction; target.botRecoveryShieldFraction=botRecoveryShieldFraction; target.botRecoveryQuietSeconds=botRecoveryQuietSeconds; target.botCoverSearchRadius=botCoverSearchRadius;
            target.sharedLockHealthSeconds=sharedLockHealthSeconds;target.radarScanSeconds=radarScanSeconds;target.radarContactSeconds=radarContactSeconds;
            target.botPerceptionEnabled=botPerceptionEnabled; target.botVisualRange=botVisualRange; target.botVisualDegrees=botVisualDegrees; target.botVerticalVisualDegrees=botVerticalVisualDegrees;
            target.botScanSeconds=botScanSeconds;target.botReactionSeconds=botReactionSeconds;target.botReactionJitterSeconds=botReactionJitterSeconds;target.botViewTurnDegreesPerSecond=botViewTurnDegreesPerSecond;
            target.strategy=strategy?.Copy();
            target.emergencyReturnEnabled=emergencyReturnEnabled;target.emergencyReturnSeconds=emergencyReturnSeconds;target.returnHealthPerSecond=returnHealthPerSecond;target.returnShieldPerSecond=returnShieldPerSecond;target.returnAmmoPerSecond=returnAmmoPerSecond;
            target.weaponSpreadEnabled=weaponSpreadEnabled;
            target.botCentralApproachEnabled = botCentralApproachEnabled; target.botLineCombatEnabled = botLineCombatEnabled; target.botPreferredRangeFraction = botPreferredRangeFraction;
            target.botStrafeFraction = botStrafeFraction; target.botRetreatHealthFraction = botRetreatHealthFraction; target.botAssaultModulo = botAssaultModulo;
            if (unitGaugeLossPerCost > 0) target.unitGaugeLossPerCost = unitGaugeLossPerCost;
            target.boostOverheatEnabled=boostOverheatEnabled;target.boostOverheatRecoveryPerSecond=boostOverheatRecoveryPerSecond;target.seamlessMeleeEnabled=seamlessMeleeEnabled;
            target.boostCapacity = boostCapacity; target.boostDrainPerSecond = boostDrainPerSecond;
            target.boostRecoveryPerSecond = boostRecoveryPerSecond; target.boostSpeedMultiplier = boostSpeedMultiplier;
            target.humanBoostCapacityMultiplier = humanBoostCapacityMultiplier;
            target.meleeHitRecoveryMultiplier=meleeHitRecoveryMultiplier; target.meleeHitBoostRefundFraction=meleeHitBoostRefundFraction;
            target.normalMeleeBypassesDisabledEnabled=normalMeleeBypassesDisabledEnabled;
            target.meleeDamage = meleeDamage; target.meleeRange = meleeRange;
            target.meleeIntervalSeconds = meleeIntervalSeconds; target.meleeHalfAngleDegrees = meleeHalfAngleDegrees;
            target.jumpHoldGravityMultiplier = jumpHoldGravityMultiplier; target.jumpHoldDrainPerSecond = jumpHoldDrainPerSecond;
            target.aerialBoostEnabled=aerialBoostEnabled; target.aerialAscentSpeed=aerialAscentSpeed; target.aerialAscentAcceleration=aerialAscentAcceleration;
            target.aerialMaximumRise=aerialMaximumRise; target.aerialGlideFallSpeed=aerialGlideFallSpeed; target.aerialControlAcceleration=aerialControlAcceleration;
            target.botAerialManeuversEnabled=botAerialManeuversEnabled; target.botAerialBurstSeconds=botAerialBurstSeconds; target.botAerialIntervalSeconds=botAerialIntervalSeconds;
            target.equipmentRulesEnabled = equipmentRulesEnabled;
            target.traversalEnabled = traversalEnabled; target.redLockAssistEnabled = redLockAssistEnabled; target.gravity = gravity; target.jumpSpeed = jumpSpeed; target.jumpBoostCost = jumpBoostCost; target.airControl = airControl; target.maximumStepHeight = maximumStepHeight;
            if (playerUnitRadius > 0) target.unitRadius = playerUnitRadius;
            target.aimIn3DEnabled = aimIn3DEnabled; target.unitHeight = unitHeight; target.shotOriginHeight = shotOriginHeight; target.coreHeight = coreHeight;
            target.directionalShieldEnabled = directionalShieldEnabled; target.shieldHalfAngleDegrees = shieldHalfAngleDegrees; target.shieldRecoverySeconds = shieldRecoverySeconds;
            target.downPointRecoverySeconds = downPointRecoverySeconds; target.staggerSeconds = staggerSeconds; target.knockdownSeconds = knockdownSeconds; target.knockdownThresholdMultiplier = knockdownThresholdMultiplier; target.meleeAttacks = meleeAttacks;
            target.meleeVerticalToleranceMeters = meleeVerticalToleranceMeters;
            target.Validate();
        }
        private static bool Positive(float v) { return !float.IsNaN(v) && !float.IsInfinity(v) && v > 0; }
    }
}
