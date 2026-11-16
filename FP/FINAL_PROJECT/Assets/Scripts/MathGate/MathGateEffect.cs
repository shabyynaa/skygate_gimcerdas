using System;
using UnityEngine;

namespace SkyGate.MathGate
{
    public enum MathGateEffectType
    {
        None = 0,
        HealHP,
        DamageFlat,
        MaxHPFlat,
        MoveSpeedPercent,
        AttackSpeedPercent,
        TakeFlatDamage,

        // Additional enum values supported by PlayerStats
        HealPercent,
        DamageCurrentHP,
        ModifyMaxHP,
        MultiplyATK,
        DivideATK,
        ModifySpeedPct,
        ModifyCooldownPct,
        ModifyDEF
    }

    [Serializable]
    public struct MathGateModifier
    {
        public MathGateEffectType effectType;
        public float value;
        public bool allowOverhealShield;

        public string GetFormattedLabel()
        {
            if (effectType == MathGateEffectType.None || Mathf.Abs(value) < 0.001f)
                return "";

            string sign = value >= 0f ? "+" : "";
            switch (effectType)
            {
                case MathGateEffectType.HealHP:
                    return $"{sign}{Mathf.RoundToInt(value)} HP";

                case MathGateEffectType.HealPercent:
                    return $"{sign}{Mathf.RoundToInt(value)}% HP";

                case MathGateEffectType.DamageFlat:
                    return $"{sign}{Mathf.RoundToInt(value)} ATK";

                case MathGateEffectType.MultiplyATK:
                    return $"x{value:F1} ATK";

                case MathGateEffectType.DivideATK:
                    return $"/{value:F1} ATK";

                case MathGateEffectType.MaxHPFlat:
                case MathGateEffectType.ModifyMaxHP:
                    return $"{sign}{Mathf.RoundToInt(value)} MAX HP";

                case MathGateEffectType.MoveSpeedPercent:
                    return $"{sign}{Mathf.RoundToInt(value * 100f)}% SPD";

                case MathGateEffectType.ModifySpeedPct:
                    return $"{sign}{Mathf.RoundToInt(value)}% SPD";

                case MathGateEffectType.AttackSpeedPercent:
                    return $"{sign}{Mathf.RoundToInt(value * 100f)}% ASPD";

                case MathGateEffectType.ModifyCooldownPct:
                    return $"{sign}{Mathf.RoundToInt(value)}% ASPD";

                case MathGateEffectType.TakeFlatDamage:
                case MathGateEffectType.DamageCurrentHP:
                    return $"-{Mathf.RoundToInt(Mathf.Abs(value))} HP";

                case MathGateEffectType.ModifyDEF:
                    return $"{sign}{Mathf.RoundToInt(value)}% DEF";

                default:
                    return "";
            }
        }
    }

    [Serializable]
    public struct MathGateOptionData
    {
        public string gateTitle;
        public bool isHighRisk;
        public int targetIslandId;
        public MathGateModifier positiveModifier;
        public MathGateModifier negativeModifier;
    }
}
