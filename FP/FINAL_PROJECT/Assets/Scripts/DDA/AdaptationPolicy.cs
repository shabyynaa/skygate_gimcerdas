using UnityEngine;
using SkyGate.Core;

namespace SkyGate.DDA
{
    /// <summary>
    /// Implements GDD Section 9.3 Adaptation Policy & Guardrails:
    /// - SmoothedSkill < 0.35 -> -0.10 (Player Struggling)
    /// - SmoothedSkill in [0.35, 0.72] -> 0.00 (Flow Zone)
    /// - SmoothedSkill > 0.72 -> +0.08 (Player Dominating)
    /// - Scaled by Cold-Start Confidence (0.25 -> 1.0)
    /// - Clamped strictly to [0.75, 1.50]
    /// </summary>
    public static class AdaptationPolicy
    {
        public const float MinMultiplier = 0.75f;
        public const float MaxMultiplier = 1.50f;
        public const float StruggleThreshold = 0.35f;
        public const float DominateThreshold = 0.72f;

        public static float ComputeNextMultiplier(
            float currentMultiplier,
            PlayerModel model,
            bool isAdaptiveMode,
            out string reason)
        {
            if (!isAdaptiveMode)
            {
                reason = "Static Control Mode (Locked 1.00x)";
                return 1.00f;
            }

            float rawStep = 0f;
            string baseReason;

            if (model.smoothedSkill < StruggleThreshold)
            {
                rawStep = -0.10f;
                baseReason = "Struggling (<0.35) -> Ease";
            }
            else if (model.smoothedSkill > DominateThreshold)
            {
                rawStep = +0.08f;
                baseReason = "Dominating (>0.72) -> Challenge";
            }
            else
            {
                rawStep = 0.00f;
                baseReason = "In Flow Zone [0.35..0.72] -> Hold";
            }

            float effectiveStep = rawStep * model.confidence;
            float nextMult = Mathf.Clamp(currentMultiplier + effectiveStep, MinMultiplier, MaxMultiplier);

            reason = $"{baseReason} (Step {effectiveStep:+0.00;-0.00;0.00}, Conf {model.confidence:0.00})";
            return nextMult;
        }

        public static DifficultyContext BuildContext(float multiplier)
        {
            float m = Mathf.Clamp(multiplier, MinMultiplier, MaxMultiplier);
            return new DifficultyContext
            {
                difficultyMultiplier = m,
                hpMultiplier = m,
                damageMultiplier = m,
                aggressionMultiplier = Mathf.Lerp(1.0f, m, 0.65f),
                speedMultiplier = Mathf.Min(1.15f, Mathf.Lerp(1.0f, m, 0.45f))
            };
        }
    }
}
