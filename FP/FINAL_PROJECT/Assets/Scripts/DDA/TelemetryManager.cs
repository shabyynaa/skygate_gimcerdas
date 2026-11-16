using System;
using UnityEngine;
using SkyGate.Core;
using SkyGate.EnemyAI;
using SkyGate.MathGate;
using SkyGate.World;

namespace SkyGate.DDA
{
    public class TelemetryManager : MonoBehaviour
    {
        private readonly ActiveRoomTelemetry currentRoom = new ActiveRoomTelemetry();
        private HealthComponent trackedPlayerHealth;
        private string lastGateChoice = "Start";

        public event Action<RoomTelemetrySummary> OnRoomTelemetryCompiled;

        public ActiveRoomTelemetry CurrentRoom => currentRoom;

        public void SetTrackedPlayer(HealthComponent playerHealth)
        {
            trackedPlayerHealth = playerHealth;
        }

        private void OnEnable()
        {
            GameEvents.OnRoomEntered += HandleRoomEntered;
            GameEvents.OnRoomClearedWithTime += HandleRoomCleared;
            GameEvents.OnPlayerDamaged += HandlePlayerDamaged;
            GameEvents.OnEnemyDamaged += HandleEnemyDamaged;
            GameEvents.OnPlayerDodgeSuccess += HandlePlayerDodgeSuccess;
            GameEvents.OnPlayerDied += HandlePlayerDied;
            GameEvents.OnMathGateChosen += HandleMathGateChosen;
        }

        private void OnDisable()
        {
            GameEvents.OnRoomEntered -= HandleRoomEntered;
            GameEvents.OnRoomClearedWithTime -= HandleRoomCleared;
            GameEvents.OnPlayerDamaged -= HandlePlayerDamaged;
            GameEvents.OnEnemyDamaged -= HandleEnemyDamaged;
            GameEvents.OnPlayerDodgeSuccess -= HandlePlayerDodgeSuccess;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
            GameEvents.OnMathGateChosen -= HandleMathGateChosen;
        }

        private void HandleRoomEntered(IslandNode node)
        {
            float hpRatio = trackedPlayerHealth != null ? trackedPlayerHealth.HPRatio : 1f;
            currentRoom.Reset(node, hpRatio);
            currentRoom.gateChosen = lastGateChoice;
        }

        private void HandleRoomCleared(IslandNode node, float clearTime)
        {
            float hpRatio = trackedPlayerHealth != null ? trackedPlayerHealth.HPRatio : 1f;
            RoomTelemetrySummary summary = currentRoom.ToSummary(clearTime, hpRatio);
            GameEvents.RaiseRoomCleared(node, summary);
            OnRoomTelemetryCompiled?.Invoke(summary);
        }

        private void HandlePlayerDamaged(float amount, float remainingHP, float shield)
        {
            currentRoom.damageTaken += amount;
        }

        private void HandleEnemyDamaged(EnemyBrain enemy, DamageInfo info, bool wasStunned)
        {
            currentRoom.damageDealt += info.amount;
            if (wasStunned)
                currentRoom.stunHits++;
        }

        private void HandlePlayerDodgeSuccess()
        {
            currentRoom.dodgeSuccesses++;
        }

        private void HandlePlayerDied(int remainingLives)
        {
            currentRoom.deathsInRoom++;
        }

        private void HandleMathGateChosen(MathGateDoor door, bool isRisky)
        {
            lastGateChoice = isRisky ? "GateB_Risk" : "GateA_Safe";
            currentRoom.gateChosen = lastGateChoice;
        }
    }
}
