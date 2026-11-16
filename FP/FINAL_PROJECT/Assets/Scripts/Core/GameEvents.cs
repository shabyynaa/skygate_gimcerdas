using System;
using UnityEngine;

namespace SkyGate.Core
{
    [Serializable]
    public struct RoomTelemetrySummary
    {
        public int islandId;
        public int depth;
        public string islandType;
        public float hpRatioStart;
        public float hpRatioEnd;
        public float damageTakenInRoom;
        public float damageDealtInRoom;
        public int deathsInRoom;
        public float clearTimeSeconds;
        public int dodgeSuccessCount;
        public int attacksAttempted;
        public int attacksConnected;
        public int environmentalMechanicsUsed;
        public bool gateRiskChosen;
        public string gateChosen;
        public float avgDistanceToEnemy;
        public float coverTimeRatio;
        public int enemiesKilled;

        public float damageTaken
        {
            get => damageTakenInRoom;
            set => damageTakenInRoom = value;
        }

        public float damageDealt
        {
            get => damageDealtInRoom;
            set => damageDealtInRoom = value;
        }

        public int dodgeSuccesses
        {
            get => dodgeSuccessCount;
            set => dodgeSuccessCount = value;
        }

        public int stunHits
        {
            get => environmentalMechanicsUsed;
            set => environmentalMechanicsUsed = value;
        }
    }

    [Serializable]
    public struct DDADecisionRecord
    {
        public int islandDepth;
        public string islandType;
        public float rawSkillScore;
        public float smoothedSkillScore;
        public float previousMultiplier;
        public float newMultiplier;
        public string multiplierTrend; // "UP", "DOWN", "HOLD", "STATIC"
        public string dominantPlayStyle;
        public float aggressiveScore;
        public float defensiveScore;
        public float riskTakerScore;
        public float confidence;
        public string decisionSummary;
        public RoomTelemetrySummary telemetry;

        public int depth
        {
            get => islandDepth;
            set => islandDepth = value;
        }

        public float skillScore
        {
            get => rawSkillScore;
            set => rawSkillScore = value;
        }

        public float smoothedSkill
        {
            get => smoothedSkillScore;
            set => smoothedSkillScore = value;
        }

        public float difficultyMultiplier
        {
            get => newMultiplier;
            set => newMultiplier = value;
        }

        public string playstyleLabel
        {
            get => dominantPlayStyle;
            set => dominantPlayStyle = value;
        }

        public string adaptationReason
        {
            get => decisionSummary;
            set => decisionSummary = value;
        }
    }

    public static class GameEvents
    {
        // Player Events
        public static event Action<float, float, float> OnPlayerDamaged; // amount, remainingHP, shield
        public static event Action<float, float> OnPlayerHealed; // hpAdded, shieldAdded
        public static event Action<float, float, float> OnPlayerHealthChanged; // currentHP, maxHP, shield
        public static event Action OnPlayerDodgeStarted;
        public static event Action OnPlayerDodgeSuccess;
        public static event Action OnPlayerAttackSwung;
        public static event Action<int> OnPlayerLivesChanged; // remainingLives
        public static event Action<int> OnPlayerDied; // remainingLives
        public static event Action OnPlayerRespawned;
        public static event Action OnPlayerStatsChanged;
        public static event Action<float, float, float, int, float, float, float> OnPlayerStatsDetailed;
        public static event Action<string> OnInteractPromptChanged;
        public static event Action<string, bool> OnInteractionPromptChanged;
        public static event Action<string> OnPerkAcquired;

        // Combat & Enemy Events
        public static event Action<SkyGate.EnemyAI.EnemyBrain, DamageInfo, bool> OnEnemyDamaged;
        public static event Action<SkyGate.EnemyAI.EnemyBrain> OnEnemyKilled;
        public static event Action<Vector3, float, bool, bool> OnFloatingDamageRequested; // pos, amount, isCrit, isShield
        public static event Action<float, float> OnCameraShakeRequested; // amplitude, duration
        public static event Action<float> OnHitstopRequested; // duration

        // Boss Events
        public static event Action<string, float, float, int> OnBossStateUpdated; // name, currentHP, maxHP, phase
        public static event Action OnBossDefeated;

        // World, Room & PCG Events
        public static event Action<SkyGate.World.IslandNode> OnRoomEntered;
        public static event Action<SkyGate.World.IslandNode, RoomTelemetrySummary> OnRoomCleared;
        public static event Action<SkyGate.World.IslandNode, float> OnRoomClearedWithTime;
        public static event Action<string, string> OnObjectiveUpdated; // title, progress
        public static event Action<string, int, int> OnObjectiveProgressUpdated; // desc, current, required
        public static event Action<SkyGate.World.Interactables.SunBeacon> OnBeaconLit;
        public static event Action<SkyGate.World.Interactables.RuneAnchor> OnRuneAnchorActivated;
        public static event Action<SkyGate.World.Interactables.StormCrystal> OnStormCrystalDestroyed;
        public static event Action<SkyGate.MathGate.MathGateDoor, bool> OnMathGateChosen;
        public static event Action<int, bool, int, int> OnPCGGenerated; // seed, bfsValid, spawnedCount, rejectedAttempts

        // DDA & Run Lifecycle Events
        public static event Action<DDADecisionRecord> OnDDAAdapted;
        public static event Action<DDADecisionRecord> OnDDAUpdated
        {
            add => OnDDAAdapted += value;
            remove => OnDDAAdapted -= value;
        }
        public static event Action<int, bool> OnRunStarted; // seed, ddaEnabled
        public static event Action OnRunVictory;
        public static event Action OnRunGameOver;
        public static event Action<bool> OnRunEnded;

        public static void ClearAllSubscribers()
        {
            OnPlayerDamaged = null;
            OnPlayerHealed = null;
            OnPlayerHealthChanged = null;
            OnPlayerDodgeStarted = null;
            OnPlayerDodgeSuccess = null;
            OnPlayerAttackSwung = null;
            OnPlayerLivesChanged = null;
            OnPlayerDied = null;
            OnPlayerRespawned = null;
            OnPlayerStatsChanged = null;
            OnPlayerStatsDetailed = null;
            OnInteractPromptChanged = null;
            OnInteractionPromptChanged = null;
            OnPerkAcquired = null;
            OnEnemyDamaged = null;
            OnEnemyKilled = null;
            OnFloatingDamageRequested = null;
            OnCameraShakeRequested = null;
            OnHitstopRequested = null;
            OnBossStateUpdated = null;
            OnBossDefeated = null;
            OnRoomEntered = null;
            OnRoomCleared = null;
            OnRoomClearedWithTime = null;
            OnObjectiveUpdated = null;
            OnObjectiveProgressUpdated = null;
            OnBeaconLit = null;
            OnRuneAnchorActivated = null;
            OnStormCrystalDestroyed = null;
            OnMathGateChosen = null;
            OnPCGGenerated = null;
            OnDDAAdapted = null;
            OnRunStarted = null;
            OnRunVictory = null;
            OnRunGameOver = null;
            OnRunEnded = null;
        }

        public static void RaisePlayerDamaged(float amount, float remainingHP, float shield) => OnPlayerDamaged?.Invoke(amount, remainingHP, shield);
        public static void RaisePlayerHealed(float hpAdded, float shieldAdded) => OnPlayerHealed?.Invoke(hpAdded, shieldAdded);
        public static void RaisePlayerHealthChanged(float currentHP, float maxHP, float shield) => OnPlayerHealthChanged?.Invoke(currentHP, maxHP, shield);
        public static void RaisePlayerDodgeStarted() => OnPlayerDodgeStarted?.Invoke();
        public static void RaisePlayerDodgeSuccess() => OnPlayerDodgeSuccess?.Invoke();
        public static void RaisePlayerAttackSwung() => OnPlayerAttackSwung?.Invoke();
        public static void RaisePlayerLivesChanged(int remainingLives) => OnPlayerLivesChanged?.Invoke(remainingLives);
        public static void RaisePlayerDied(int remainingLives) => OnPlayerDied?.Invoke(remainingLives);
        public static void RaisePlayerRespawned() => OnPlayerRespawned?.Invoke();
        public static void RaisePlayerStatsChanged() => OnPlayerStatsChanged?.Invoke();
        public static void RaisePlayerStatsDetailed(float curHP, float maxHP, float shield, int lives, float atk, float spd, float aspd)
            => OnPlayerStatsDetailed?.Invoke(curHP, maxHP, shield, lives, atk, spd, aspd);

        public static void RaiseInteractPromptChanged(string prompt)
        {
            OnInteractPromptChanged?.Invoke(prompt);
            OnInteractionPromptChanged?.Invoke(prompt, !string.IsNullOrEmpty(prompt));
        }

        public static void RaiseInteractionPromptChanged(string prompt, bool visible)
        {
            OnInteractionPromptChanged?.Invoke(prompt, visible);
            OnInteractPromptChanged?.Invoke(visible ? prompt : string.Empty);
        }

        public static void RaisePerkAcquired(string perkName) => OnPerkAcquired?.Invoke(perkName);

        public static void RaiseEnemyDamaged(SkyGate.EnemyAI.EnemyBrain enemy, DamageInfo info, bool wasStunned) => OnEnemyDamaged?.Invoke(enemy, info, wasStunned);
        public static void RaiseEnemyKilled(SkyGate.EnemyAI.EnemyBrain enemy) => OnEnemyKilled?.Invoke(enemy);
        public static void RaiseFloatingDamage(Vector3 pos, float amount, bool isCrit, bool isShield) => OnFloatingDamageRequested?.Invoke(pos, amount, isCrit, isShield);
        public static void RaiseCameraShake(float amplitude, float duration) => OnCameraShakeRequested?.Invoke(amplitude, duration);
        public static void RaiseHitstop(float duration) => OnHitstopRequested?.Invoke(duration);

        public static void RaiseBossStateUpdated(string name, float currentHP, float maxHP, int phase) => OnBossStateUpdated?.Invoke(name, currentHP, maxHP, phase);
        public static void RaiseBossDefeated() => OnBossDefeated?.Invoke();

        public static void RaiseRoomEntered(SkyGate.World.IslandNode island) => OnRoomEntered?.Invoke(island);
        public static void RaiseRoomCleared(SkyGate.World.IslandNode island, RoomTelemetrySummary summary) => OnRoomCleared?.Invoke(island, summary);
        public static void RaiseRoomCleared(SkyGate.World.IslandNode island, float clearTimeSeconds)
        {
            OnRoomClearedWithTime?.Invoke(island, clearTimeSeconds);
        }

        public static void RaiseObjectiveUpdated(string title, string progress) => OnObjectiveUpdated?.Invoke(title, progress);
        public static void RaiseObjectiveProgressUpdated(string description, int currentCount, int requiredCount)
        {
            OnObjectiveProgressUpdated?.Invoke(description, currentCount, requiredCount);
            OnObjectiveUpdated?.Invoke(description, $"{currentCount}/{requiredCount}");
        }

        public static void RaiseBeaconLit(SkyGate.World.Interactables.SunBeacon beacon) => OnBeaconLit?.Invoke(beacon);
        public static void RaiseRuneAnchorActivated(SkyGate.World.Interactables.RuneAnchor anchor) => OnRuneAnchorActivated?.Invoke(anchor);
        public static void RaiseStormCrystalDestroyed(SkyGate.World.Interactables.StormCrystal crystal) => OnStormCrystalDestroyed?.Invoke(crystal);
        public static void RaiseMathGateChosen(SkyGate.MathGate.MathGateDoor gate, bool isRiskPath) => OnMathGateChosen?.Invoke(gate, isRiskPath);
        public static void RaisePCGGenerated(int seed, bool bfsValid, int spawnedCount, int rejectedAttempts) => OnPCGGenerated?.Invoke(seed, bfsValid, spawnedCount, rejectedAttempts);

        public static void RaiseDDAAdapted(DDADecisionRecord record) => OnDDAAdapted?.Invoke(record);
        public static void RaiseDDAUpdated(DDADecisionRecord record) => OnDDAAdapted?.Invoke(record);
        public static void RaiseRunStarted(int seed, bool ddaEnabled) => OnRunStarted?.Invoke(seed, ddaEnabled);
        public static void RaiseRunVictory()
        {
            OnRunVictory?.Invoke();
            OnRunEnded?.Invoke(true);
        }

        public static void RaiseRunGameOver()
        {
            OnRunGameOver?.Invoke();
            OnRunEnded?.Invoke(false);
        }

        public static void RaiseRunEnded(bool victory)
        {
            OnRunEnded?.Invoke(victory);
            if (victory) OnRunVictory?.Invoke();
            else OnRunGameOver?.Invoke();
        }
    }
}
