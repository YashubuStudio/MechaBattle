using System;
namespace MechaBattle.Contracts
{
    public enum MeleeStyle { Normal = 0, Combo = 1, Dash = 2, DashCombo = 3, Charge = 4 }
    public enum DisruptionState { Ready = 0, Staggered = 1, KnockedDown = 2 }
    [Serializable]
    public sealed class MeleeAttackDefinition
    {
        public MeleeStyle style;
        public float damage, downPoints, range, halfAngleDegrees, windupSeconds, recoverySeconds, cooldownSeconds;
        public float lungeMeters, boostCost, moveMultiplier;
        public int comboSteps, maximumTargets;
        public float minimumChargeSeconds, fullChargeSeconds, minimumChargeMultiplier;
        // Arm-type-specific charge melee (user request 2026-10-16). Both default to the
        // "legacy" off state so every existing MeleeAttackDefinition (including the shared
        // global Charge entry) is unaffected. Mutually exclusive: a standard-arm definition
        // scales its area with charge (chargeScalesRange) instead of damage; a reinforced-arm
        // definition scales damage/downPoints through a two-phase curve (thresholdChargeFraction)
        // instead of the plain linear minimumChargeMultiplier interpolation.
        public bool chargeScalesRange;
        public float minimumRange;
        public float thresholdChargeFraction;
        public float belowThresholdDamage, belowThresholdDownPoints;
        public MeleeAttackDefinition Copy() { return (MeleeAttackDefinition)MemberwiseClone(); }
        public void Validate()
        {
            foreach (float v in new[] { damage, downPoints, range, halfAngleDegrees, windupSeconds, recoverySeconds, cooldownSeconds, moveMultiplier })
                if (float.IsNaN(v) || float.IsInfinity(v) || v <= 0) throw new ArgumentException("Invalid melee performance.");
            foreach (float v in new[] { lungeMeters, boostCost, minimumChargeSeconds, fullChargeSeconds, minimumChargeMultiplier })
                if (float.IsNaN(v) || float.IsInfinity(v) || v < 0) throw new ArgumentException("Invalid melee timing/resource.");
            if (windupSeconds > 86400 || recoverySeconds > 86400 || cooldownSeconds > 86400 || fullChargeSeconds > 86400 || minimumChargeSeconds > 86400)
                throw new ArgumentException("Melee timing exceeds the prototype limit.");
            if (style < MeleeStyle.Normal || style > MeleeStyle.Charge || halfAngleDegrees > 180 || moveMultiplier > 1 || comboSteps < 1 || comboSteps > 4 || maximumTargets < 1 || maximumTargets > 100
                || (style == MeleeStyle.Charge && (minimumChargeSeconds <= 0 || fullChargeSeconds < minimumChargeSeconds || minimumChargeMultiplier <= 0 || minimumChargeMultiplier > 1)))
                throw new ArgumentException("Invalid melee style.");
            if (chargeScalesRange && thresholdChargeFraction > 0)
                throw new ArgumentException("chargeScalesRange and thresholdChargeFraction are alternate archetypes and cannot both be set.");
            if (chargeScalesRange && (style != MeleeStyle.Charge || !(minimumRange > 0) || !float.IsFinite(minimumRange) || minimumRange >= range))
                throw new ArgumentException("Invalid charge-scales-range configuration.");
            if (thresholdChargeFraction > 0 && (style != MeleeStyle.Charge || thresholdChargeFraction >= 1
                || !(belowThresholdDamage > 0) || !float.IsFinite(belowThresholdDamage) || belowThresholdDamage >= damage
                || !(belowThresholdDownPoints > 0) || !float.IsFinite(belowThresholdDownPoints) || belowThresholdDownPoints >= downPoints))
                throw new ArgumentException("Invalid two-phase charge-damage configuration.");
        }
    }
}
