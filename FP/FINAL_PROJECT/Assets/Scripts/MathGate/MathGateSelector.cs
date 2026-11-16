using UnityEngine;

namespace SkyGate.MathGate
{
    /// <summary>
    /// Generates balanced, honest paired MathGate options (Safe Gate A vs High-Risk Gate B)
    /// using a deterministic System.Random instance and optional DDA support bias.
    /// </summary>
    public static class MathGateSelector
    {
        public static void GenerateGatePair(
            System.Random rng,
            int targetIslandA,
            int targetIslandB,
            float ddaMultiplier,
            out MathGateOptionData safeGateA,
            out MathGateOptionData riskyGateB)
        {
            // When player is struggling (ddaMultiplier < 0.95), slightly boost Safe Gate healing (GDD Section 9.3)
            float healBonus = ddaMultiplier < 0.95f ? 10f : 0f;

            int safeRoll = rng != null ? rng.Next(0, 4) : 0;
            switch (safeRoll)
            {
                case 0:
                    safeGateA = new MathGateOptionData
                    {
                        gateTitle = "GATE A [SAFE]",
                        isHighRisk = false,
                        targetIslandId = targetIslandA,
                        positiveModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.HealHP,
                            value = 25f + healBonus,
                            allowOverhealShield = true
                        },
                        negativeModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.None,
                            value = 0f
                        }
                    };
                    break;

                case 1:
                    safeGateA = new MathGateOptionData
                    {
                        gateTitle = "GATE A [SAFE]",
                        isHighRisk = false,
                        targetIslandId = targetIslandA,
                        positiveModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.MaxHPFlat,
                            value = 15f + healBonus * 0.5f,
                            allowOverhealShield = false
                        },
                        negativeModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.None,
                            value = 0f
                        }
                    };
                    break;

                case 2:
                    safeGateA = new MathGateOptionData
                    {
                        gateTitle = "GATE A [SAFE]",
                        isHighRisk = false,
                        targetIslandId = targetIslandA,
                        positiveModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.DamageFlat,
                            value = 3f,
                            allowOverhealShield = false
                        },
                        negativeModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.None,
                            value = 0f
                        }
                    };
                    break;

                default:
                    safeGateA = new MathGateOptionData
                    {
                        gateTitle = "GATE A [SAFE]",
                        isHighRisk = false,
                        targetIslandId = targetIslandA,
                        positiveModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.MoveSpeedPercent,
                            value = 0.12f,
                            allowOverhealShield = false
                        },
                        negativeModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.None,
                            value = 0f
                        }
                    };
                    break;
            }

            int riskRoll = rng != null ? rng.Next(0, 4) : 0;
            switch (riskRoll)
            {
                case 0:
                    riskyGateB = new MathGateOptionData
                    {
                        gateTitle = "GATE B [RISK]",
                        isHighRisk = true,
                        targetIslandId = targetIslandB,
                        positiveModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.DamageFlat,
                            value = 6f,
                            allowOverhealShield = false
                        },
                        negativeModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.TakeFlatDamage,
                            value = 15f,
                            allowOverhealShield = false
                        }
                    };
                    break;

                case 1:
                    riskyGateB = new MathGateOptionData
                    {
                        gateTitle = "GATE B [RISK]",
                        isHighRisk = true,
                        targetIslandId = targetIslandB,
                        positiveModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.AttackSpeedPercent,
                            value = 0.25f,
                            allowOverhealShield = false
                        },
                        negativeModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.MaxHPFlat,
                            value = -15f,
                            allowOverhealShield = false
                        }
                    };
                    break;

                case 2:
                    riskyGateB = new MathGateOptionData
                    {
                        gateTitle = "GATE B [RISK]",
                        isHighRisk = true,
                        targetIslandId = targetIslandB,
                        positiveModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.DamageFlat,
                            value = 8f,
                            allowOverhealShield = false
                        },
                        negativeModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.MoveSpeedPercent,
                            value = -0.08f,
                            allowOverhealShield = false
                        }
                    };
                    break;

                default:
                    riskyGateB = new MathGateOptionData
                    {
                        gateTitle = "GATE B [RISK]",
                        isHighRisk = true,
                        targetIslandId = targetIslandB,
                        positiveModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.HealHP,
                            value = 40f,
                            allowOverhealShield = true
                        },
                        negativeModifier = new MathGateModifier
                        {
                            effectType = MathGateEffectType.DamageFlat,
                            value = -2f,
                            allowOverhealShield = false
                        }
                    };
                    break;
            }
        }
    }
}
