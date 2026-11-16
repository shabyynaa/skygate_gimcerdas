using System.Collections.Generic;
using UnityEngine;
using SkyGate.Core;

namespace SkyGate.DDA
{
    [RequireComponent(typeof(TelemetryManager))]
    [RequireComponent(typeof(PlayerModelManager))]
    public class DDAManager : MonoBehaviour
    {
        [Header("DDA Mode Configuration")]
        [SerializeField] private bool isAdaptiveEnabled = true;
        [SerializeField] private float currentMultiplier = 1.00f;

        private TelemetryManager telemetryManager;
        private PlayerModelManager playerModelManager;
        private readonly List<DDADecisionRecord> decisionHistory = new List<DDADecisionRecord>();
        private readonly List<RoomTelemetrySummary> roomSummaries = new List<RoomTelemetrySummary>();
        private string lastAdaptationReason = "Initial Start (1.00x)";

        public bool IsAdaptiveEnabled => isAdaptiveEnabled;
        public float CurrentMultiplier => isAdaptiveEnabled ? currentMultiplier : 1.00f;
        public string LastAdaptationReason => lastAdaptationReason;
        public PlayerModel CurrentPlayerModel => playerModelManager != null ? playerModelManager.Model : null;
        public IReadOnlyList<DDADecisionRecord> DecisionHistory => decisionHistory;
        public IReadOnlyList<RoomTelemetrySummary> RoomSummaries => roomSummaries;

        private void Awake()
        {
            telemetryManager = GetComponent<TelemetryManager>();
            playerModelManager = GetComponent<PlayerModelManager>();
        }

        private void OnEnable()
        {
            if (telemetryManager != null)
                telemetryManager.OnRoomTelemetryCompiled += HandleRoomSummary;
        }

        private void OnDisable()
        {
            if (telemetryManager != null)
                telemetryManager.OnRoomTelemetryCompiled -= HandleRoomSummary;
        }

        public void InitializeRun(bool adaptiveMode, HealthComponent playerHealth)
        {
            isAdaptiveEnabled = adaptiveMode;
            currentMultiplier = 1.00f;
            lastAdaptationReason = adaptiveMode ? "Initial Run Start (1.00x)" : "Static Mode Locked (1.00x)";
            decisionHistory.Clear();
            roomSummaries.Clear();

            if (playerModelManager != null)
                playerModelManager.ResetModel();

            if (telemetryManager != null)
                telemetryManager.SetTrackedPlayer(playerHealth);

            BroadcastCurrentState(0);
        }

        public void ToggleAdaptiveMode()
        {
            isAdaptiveEnabled = !isAdaptiveEnabled;
            if (!isAdaptiveEnabled)
            {
                currentMultiplier = 1.00f;
                lastAdaptationReason = "Switched to Static Mode (1.00x)";
            }
            else
            {
                lastAdaptationReason = "Switched to Adaptive Mode";
            }
            BroadcastCurrentState(decisionHistory.Count);
        }

        public DifficultyContext GetCurrentDifficultyContext()
        {
            return AdaptationPolicy.BuildContext(CurrentMultiplier);
        }

        private void HandleRoomSummary(RoomTelemetrySummary summary)
        {
            roomSummaries.Add(summary);

            PlayerModel model = playerModelManager.ProcessRoomSummary(summary);
            currentMultiplier = AdaptationPolicy.ComputeNextMultiplier(
                currentMultiplier,
                model,
                isAdaptiveEnabled,
                out lastAdaptationReason
            );

            DDADecisionRecord record = new DDADecisionRecord
            {
                depth = summary.depth,
                skillScore = model.skillScore,
                smoothedSkill = model.smoothedSkill,
                confidence = model.confidence,
                difficultyMultiplier = CurrentMultiplier,
                playstyleLabel = model.playstyleLabel,
                adaptationReason = lastAdaptationReason
            };

            decisionHistory.Add(record);
            GameEvents.RaiseDDAUpdated(record);
        }

        private void BroadcastCurrentState(int depth)
        {
            PlayerModel model = playerModelManager != null ? playerModelManager.Model : new PlayerModel();
            GameEvents.RaiseDDAUpdated(new DDADecisionRecord
            {
                depth = depth,
                skillScore = model.skillScore,
                smoothedSkill = model.smoothedSkill,
                confidence = model.confidence,
                difficultyMultiplier = CurrentMultiplier,
                playstyleLabel = model.playstyleLabel,
                adaptationReason = lastAdaptationReason
            });
        }
    }
}
