using System.Collections;
using UnityEngine;
using SkyGate.Player;
using SkyGate.World;

namespace SkyGate.Core
{
    public class CheckpointManager : MonoBehaviour
    {
        [SerializeField] private float respawnDelay = 1.35f;

        private PlayerController playerController;
        private PlayerStats playerStats;
        private HealthComponent playerHealth;
        private IslandNode activeIsland;
        private bool isRespawning;

        public IslandNode ActiveIsland => activeIsland;

        public void Initialize(PlayerController controller, IslandNode startingIsland)
        {
            if (playerHealth != null)
            {
                playerHealth.OnDied -= HandlePlayerDied;
            }

            playerController = controller;
            playerStats = controller != null ? controller.GetComponent<PlayerStats>() : null;
            playerHealth = controller != null ? controller.GetComponent<HealthComponent>() : null;
            activeIsland = startingIsland;
            isRespawning = false;

            if (playerHealth != null)
            {
                playerHealth.OnDied += HandlePlayerDied;
            }
        }

        private void OnEnable()
        {
            GameEvents.OnRoomEntered += HandleRoomEntered;
        }

        private void OnDisable()
        {
            GameEvents.OnRoomEntered -= HandleRoomEntered;
            if (playerHealth != null)
            {
                playerHealth.OnDied -= HandlePlayerDied;
            }
        }

        private void HandleRoomEntered(IslandNode node)
        {
            if (node != null)
            {
                activeIsland = node;
            }
        }

        private void HandlePlayerDied(DamageInfo fatalHit)
        {
            if (isRespawning || playerStats == null) return;

            bool hasLivesLeft = playerStats.ConsumeLife();
            GameEvents.RaisePlayerDied(playerStats.RemainingLives);

            if (hasLivesLeft)
            {
                StartCoroutine(RespawnAtIslandCheckpointRoutine());
            }
            else
            {
                GameEvents.RaiseRunEnded(false);
            }
        }

        private IEnumerator RespawnAtIslandCheckpointRoutine()
        {
            isRespawning = true;
            yield return new WaitForSeconds(respawnDelay);

            if (activeIsland != null)
            {
                RoomController rc = activeIsland.GetComponentInChildren<RoomController>();
                if (rc != null)
                {
                    rc.ResetRoomForCheckpoint();
                }

                Transform spawnAnchor = activeIsland.EntranceSpawnPoint;
                Vector3 respawnPos = spawnAnchor != null ? spawnAnchor.position : activeIsland.transform.position;
                Quaternion respawnRot = spawnAnchor != null ? spawnAnchor.rotation : Quaternion.identity;

                if (playerController != null)
                {
                    playerController.ReviveAndTeleport(respawnPos + Vector3.up * 0.15f, respawnRot);
                }
            }

            if (playerStats != null)
            {
                playerStats.RestoreFullHPKeepModifiers();
            }

            GameEvents.RaisePlayerRespawned();
            isRespawning = false;
        }
    }
}
