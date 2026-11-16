using System;
using UnityEngine;
using SkyGate.Core;

namespace SkyGate.DDA
{
    [Serializable]
    public class PlayerModel
    {
        public float normHP = 1.0f;
        public float normDamage = 1.0f;
        public float normTime = 1.0f;
        public float normSurvival = 1.0f;

        public float skillScore = 0.55f;
        public float smoothedSkill = 0.55f;
        public float confidence = 0.25f;
        public int roomsEvaluated = 0;

        public int totalGatesChosen = 0;
        public int riskyGatesChosen = 0;
        public string playstyleLabel = "Balanced";

        public void Reset()
        {
            normHP = 1.0f;
            normDamage = 1.0f;
            normTime = 1.0f;
            normSurvival = 1.0f;
            skillScore = 0.55f;
            smoothedSkill = 0.55f;
            confidence = 0.25f;
            roomsEvaluated = 0;
            totalGatesChosen = 0;
            riskyGatesChosen = 0;
            playstyleLabel = "Balanced";
        }

        /// <summary>
        /// Evaluates room performance using the exact GDD Section 9.2 formula:
        /// SkillScore = 0.35*NormHP + 0.25*NormDamage + 0.25*NormTime + 0.15*NormSurvival
        /// SmoothedSkill = 0.6*SkillScore + 0.4*PrevSmoothedSkill
        /// </summary>
        public void EvaluateRoom(RoomTelemetrySummary summary, float targetClearTime = 35.0f)
        {
            roomsEvaluated++;

            normHP = Mathf.Clamp01(summary.hpRatioEnd);
            normDamage = Mathf.Clamp01(1.0f - (summary.damageTaken / 80.0f));
            normTime = Mathf.Clamp01(targetClearTime / Mathf.Max(summary.clearTimeSeconds, 10.0f));
            normSurvival = (summary.deathsInRoom == 0) ? 1.0f : 0.0f;

            skillScore = (0.35f * normHP)
                       + (0.25f * normDamage)
                       + (0.25f * normTime)
                       + (0.15f * normSurvival);

            if (roomsEvaluated == 1)
            {
                smoothedSkill = (0.6f * skillScore) + (0.4f * 0.55f);
            }
            else
            {
                smoothedSkill = (0.6f * skillScore) + (0.4f * smoothedSkill);
            }

            // Cold Start Guardrail (GDD Section 9.4): Confidence scales 0.25 -> 1.0 over 4 rooms
            confidence = Mathf.Clamp01(roomsEvaluated / 4.0f);

            ClassifyPlaystyle(summary);
        }

        public void RecordGateChoice(bool isRisky)
        {
            totalGatesChosen++;
            if (isRisky)
                riskyGatesChosen++;
        }

        private void ClassifyPlaystyle(RoomTelemetrySummary summary)
        {
            float riskRatio = totalGatesChosen > 0 ? (float)riskyGatesChosen / totalGatesChosen : 0f;
            float dps = summary.damageDealt / Mathf.Max(1f, summary.clearTimeSeconds);

            if (totalGatesChosen >= 1 && riskRatio > 0.50f)
            {
                playstyleLabel = "Risk-Taker";
            }
            else if (summary.dodgeSuccesses >= 3 && summary.damageTaken <= 20f)
            {
                playstyleLabel = "Defensive";
            }
            else if (dps >= 10.5f || summary.clearTimeSeconds <= 25f)
            {
                playstyleLabel = "Aggressive";
            }
            else
            {
                playstyleLabel = "Defensive";
            }
        }
    }
}
